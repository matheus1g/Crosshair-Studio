param(
    [switch]$Test,
    [switch]$DesktopTest
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$windows = [Environment]::GetFolderPath([Environment+SpecialFolder]::Windows)
$framework = Join-Path $windows 'Microsoft.NET\Framework64\v4.0.30319'
$wpf = Join-Path $framework 'WPF'
$csc = Join-Path $framework 'csc.exe'
if (!(Test-Path -LiteralPath $csc)) { throw 'Instale o .NET Framework 4.8 no Windows x64.' }
$bin = Join-Path $root 'build'
$null = New-Item -Path $bin -ItemType Directory -Force
Copy-Item -LiteralPath "$root\assets\fonts" -Destination $bin -Recurse -Force
$references = @(
    (Join-Path $wpf 'PresentationCore.dll'),
    (Join-Path $wpf 'PresentationFramework.dll'),
    (Join-Path $wpf 'WindowsBase.dll'),
    (Join-Path $wpf 'UIAutomationTypes.dll'),
    'System.Web.Extensions.dll','System.Xaml.dll','System.Windows.Forms.dll',
    'System.Drawing.dll','Microsoft.CSharp.dll','System.Xml.Linq.dll'
)
# WindowsBase may also be installed in the Framework directory.
$references = $references | ForEach-Object {
    if ($_ -like '*\WPF\WindowsBase.dll' -and !(Test-Path -LiteralPath $_)) { Join-Path $framework 'WindowsBase.dll' }
    else { $_ }
}
$argsCommon = @('/nologo','/target:winexe','/platform:x64','/optimize+','/warn:4','/warnaserror+','/codepage:65001',
    "/win32manifest:$root\app.manifest","/resource:$root\src\Theme.xaml,CrosshairStudio.Theme")
if (Test-Path -LiteralPath "$root\assets\studio.ico") { $argsCommon += "/win32icon:$root\assets\studio.ico" }
$argsCommon += $references | ForEach-Object { "/reference:$_" }
$exe = Join-Path $bin 'CrosshairStudio.exe'
$sources = Get-ChildItem -LiteralPath "$root\src" -File -Filter '*.cs' | ForEach-Object { $_.FullName }
$compiler = New-Object System.Diagnostics.ProcessStartInfo
$compiler.FileName = $csc
$compiler.UseShellExecute = $false
$compiler.CreateNoWindow = $true
$compiler.Arguments = (($argsCommon + @("/out:$exe") + $sources) | ForEach-Object { '"{0}"' -f $_ }) -join ' '
$compilation = [Diagnostics.Process]::Start($compiler)
$compilation.WaitForExit()
if ($compilation.ExitCode -ne 0) { throw "Falha na compilacao ($($compilation.ExitCode))." }
Write-Output "Windows x64: $exe"
if ($Test -or $DesktopTest) {
    $result = Join-Path $bin 'tests.txt'
    if (Test-Path -LiteralPath $result) { Remove-Item -LiteralPath $result }
    $switches = @('--self-test', ('"{0}"' -f $result))
    if ($DesktopTest) { $switches += '--desktop' }
    $runner = New-Object System.Diagnostics.ProcessStartInfo
    $runner.FileName = $exe
    $runner.Arguments = $switches -join ' '
    $runner.UseShellExecute = $false
    if (!$runner.EnvironmentVariables['WINDIR']) { $runner.EnvironmentVariables['WINDIR'] = $windows }
    if (!$runner.EnvironmentVariables['SystemRoot']) { $runner.EnvironmentVariables['SystemRoot'] = $windows }
    $process = [Diagnostics.Process]::Start($runner)
    if (!$process.WaitForExit(60000)) { $process.Kill(); throw 'Testes excederam 60 segundos.' }
    if (Test-Path -LiteralPath $result) { Get-Content -LiteralPath $result -Encoding UTF8 }
    if ($process.ExitCode -ne 0) { throw "Falha nos testes ($($process.ExitCode))." }
}
