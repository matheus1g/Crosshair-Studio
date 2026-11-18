param([switch]$DesktopTest)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$build = Join-Path $root 'build'
$buildOptions = if ($DesktopTest) { @{DesktopTest=$true} } else { @{Test=$true} }
& (Join-Path $root 'build.ps1') @buildOptions

$buildRoot = [IO.Path]::GetFullPath($build).TrimEnd('\') + '\'
$stage = Join-Path $buildRoot ('package-' + [guid]::NewGuid().ToString('N'))
$test = Join-Path $buildRoot ('install-test-' + [guid]::NewGuid().ToString('N'))
function Remove-BuildFolder([string]$Path) {
    if (Test-Path -LiteralPath $Path) {
        $found = (Resolve-Path -LiteralPath $Path).Path
        if (!$found.StartsWith($buildRoot,[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe build directory.' }
        Remove-Item -LiteralPath $found -Recurse -Force
    }
}
try {
    $portable = Join-Path $stage 'Portatil'
    $installer = Join-Path $stage 'Instalador'
    $source = Join-Path $stage 'Codigo-Fonte'
    $validation = Join-Path $stage 'Verificacao'
    foreach ($dir in @($portable,$installer,$source,$validation)) {
        $null = New-Item -ItemType Directory -Path $dir -Force
    }
    $guide = [IO.File]::ReadAllText((Join-Path $root 'README.md')).Replace('(docs/','(Codigo-Fonte/docs/')
    [IO.File]::WriteAllText((Join-Path $stage 'LEIA-ME.md'),$guide,[Text.UTF8Encoding]::new($false))
    foreach ($dir in @($portable,$installer)) {
        Copy-Item -LiteralPath (Join-Path $root 'LICENSE.txt'),(Join-Path $build 'CrosshairStudio.exe') -Destination $dir
        Copy-Item -LiteralPath (Join-Path $build 'fonts') -Destination $dir -Recurse
    }
    $data = Join-Path $portable 'userdata'
    $null = New-Item -ItemType Directory -Path $data
    Set-Content -LiteralPath (Join-Path $data 'LEIA.txt') -Value 'O modo portatil guarda as suas configuracoes nesta pasta.'
    Copy-Item -LiteralPath (Join-Path $root 'packaging\Instalar.cmd'),(Join-Path $root 'packaging\Install.ps1') -Destination $installer
    foreach ($entry in @('README.md','LICENSE.txt','app.manifest','build.ps1','package.ps1','assets','docs','packaging','src')) {
        Copy-Item -LiteralPath (Join-Path $root $entry) -Destination $source -Recurse
    }
    $installScript = Join-Path $installer 'Install.ps1'
    & $installScript -TestDestination $test
    $payloadHash = (Get-FileHash -LiteralPath (Join-Path $installer 'CrosshairStudio.exe')).Hash
    if ((Get-FileHash -LiteralPath (Join-Path $test 'CrosshairStudio.exe')).Hash -ne $payloadHash) { throw 'Installer file did not match.' }
    & $installScript -TestDestination $test
    $keep = Join-Path $test 'personal-setting.txt'
    Set-Content -LiteralPath $keep -Value 'fixture'
    & $installScript -Remove -TestDestination $test
    if (!(Test-Path -LiteralPath $keep) -or (Test-Path -LiteralPath (Join-Path $test 'CrosshairStudio.exe'))) { throw 'Installer removal failed.' }
    Copy-Item -LiteralPath (Join-Path $build 'tests.txt') -Destination (Join-Path $validation 'Testes.txt')
    Add-Content -LiteralPath (Join-Path $validation 'Testes.txt') -Value 'PASS installation, repeat installation, removal and unrelated file preservation (workspace destination)' -Encoding UTF8
    if ($DesktopTest) {
        $screens = Join-Path $stage 'Capturas'
        $null = New-Item -Path $screens -ItemType Directory
        Copy-Item -LiteralPath (Join-Path $build 'dashboard.png'),(Join-Path $build 'editor.png') -Destination $screens
    }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $outputs = Join-Path (Split-Path -Parent $root) 'outputs'
    $null = New-Item -Path $outputs -ItemType Directory -Force
    $archive = Join-Path $outputs 'Crosshair-Studio-0.3.2-Windows-x64.zip'
    $newArchive = Join-Path $buildRoot ('release-' + [guid]::NewGuid().ToString('N') + '.zip')
    try {
        [IO.Compression.ZipFile]::CreateFromDirectory($stage, $newArchive, [IO.Compression.CompressionLevel]::Optimal, $false)
        if (Test-Path -LiteralPath $archive) { [IO.File]::Replace($newArchive,$archive,[NullString]::Value) }
        else { [IO.File]::Move($newArchive,$archive) }
    }
    finally { if (Test-Path -LiteralPath $newArchive) { Remove-Item -LiteralPath $newArchive } }
    Get-FileHash -LiteralPath $archive -Algorithm SHA256 | Format-List Hash,Path
}
finally {
    Remove-BuildFolder $stage
    Remove-BuildFolder $test
}

