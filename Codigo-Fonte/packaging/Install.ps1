param(
    [switch]$Remove,
    [string]$TestDestination
)
$ErrorActionPreference = 'Stop'
$payload = $PSScriptRoot
$setup = if ($TestDestination) { [IO.Path]::GetFullPath($TestDestination) }
    else { Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\Crosshair Studio' }
$app = Join-Path $setup 'CrosshairStudio.exe'
$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\CrosshairStudio'
$files = @('LICENSE.txt','fonts\inter-OFL.txt','fonts\inter-regular.ttf','fonts\inter-semibold.ttf','fonts\inter-bold.ttf','Install.ps1','CrosshairStudio.exe')
$name = 'Crosshair Studio'

function Remove-Shortcut([string]$Folder) {
    $target = Join-Path ([Environment]::GetFolderPath($Folder)) 'Crosshair Studio.lnk'
    if (Test-Path -LiteralPath $target) {
        $shell = New-Object -ComObject WScript.Shell
        if ([string]::Equals($shell.CreateShortcut($target).TargetPath, $app, [StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $target
        }
    }
}

if (!$TestDestination) {
    Add-Type -AssemblyName System.Windows.Forms
    $message = if ($Remove) { 'Remover o Crosshair Studio? Suas miras e perfis serao mantidos.' }
        else { "Instalar para o usuario atual?"+[Environment]::NewLine+$setup }
    if ([Windows.Forms.MessageBox]::Show($message,$name,[Windows.Forms.MessageBoxButtons]::YesNo) -ne 'Yes') { return }
}
try {
    if ($Remove) {
        if (Test-Path -LiteralPath $app) { Remove-Item -LiteralPath $app }
        if (!$TestDestination) {
            Remove-Shortcut 'Programs'
            Remove-Shortcut 'Startup'
            if (Test-Path -LiteralPath $key) { Remove-Item -LiteralPath $key }
        }
        foreach ($relative in $files) {
            $file = Join-Path $setup $relative
            if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file }
        }
        foreach ($folder in @((Join-Path $setup 'fonts'), $setup)) {
            if ((Test-Path -LiteralPath $folder) -and !(Get-ChildItem -LiteralPath $folder -Force | Select-Object -First 1)) {
                [IO.Directory]::Delete($folder)
            }
        }
        return
    }
    foreach ($relative in $files) {
        if (!(Test-Path -LiteralPath (Join-Path $payload $relative) -PathType Leaf)) { throw "Missing payload file $relative" }
    }
    if (Test-Path -LiteralPath $app) {
        # Fail before copying files if the installed executable is in use.
        $existing = [IO.File]::Open($app,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
        $existing.Dispose()
    }
    foreach ($relative in $files) {
        $target = Join-Path $setup $relative
        $parent = [IO.Path]::GetDirectoryName($target)
        [IO.Directory]::CreateDirectory($parent) | Out-Null
        $temp = $target + '.' + [guid]::NewGuid().ToString('N') + '.tmp'
        try {
            [IO.File]::Copy((Join-Path $payload $relative), $temp)
            if (Test-Path -LiteralPath $target) { [IO.File]::Replace($temp,$target,[NullString]::Value) }
            else { [IO.File]::Move($temp,$target) }
        }
        finally { if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp } }
    }
    if (!$TestDestination) {
        $shell = New-Object -ComObject WScript.Shell
        $link = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Programs')) 'Crosshair Studio.lnk'))
        $link.TargetPath = $app
        $link.WorkingDirectory = $setup
        $link.Save()
        $ps = Join-Path $PSHOME 'powershell.exe'
        $removeCommand = '"{0}" -NoProfile -ExecutionPolicy Bypass -File "{1}" -Remove' -f $ps,(Join-Path $setup 'Install.ps1')
        New-Item -Path $key -Force | Out-Null
        Set-ItemProperty -Path $key -Name DisplayName -Value $name
        Set-ItemProperty -Path $key -Name DisplayVersion -Value '0.3.2'
        Set-ItemProperty -Path $key -Name Publisher -Value 'Crosshair Studio contributors'
        Set-ItemProperty -Path $key -Name DisplayIcon -Value $app
        Set-ItemProperty -Path $key -Name InstallLocation -Value $setup
        Set-ItemProperty -Path $key -Name UninstallString -Value $removeCommand
        Set-ItemProperty -Path $key -Name NoModify -Value 1 -Type DWord
        Set-ItemProperty -Path $key -Name NoRepair -Value 1 -Type DWord
        if ([Windows.Forms.MessageBox]::Show('Instalacao concluida. Abrir o Crosshair Studio?',$name,[Windows.Forms.MessageBoxButtons]::YesNo) -eq 'Yes') {
            Start-Process -FilePath $app -WorkingDirectory $setup
        }
    }
}
catch {
    if ($TestDestination) { throw }
    $null = [Windows.Forms.MessageBox]::Show('Feche o Crosshair Studio na bandeja e tente novamente. '+$_.Exception.Message,$name)
    exit 1
}

