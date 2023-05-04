Import-Module BitsTransfer

# check for working dir
if (!(Test-Path -Path ~/bin)) {
    New-Item -Path ~/bin -ItemType Directory | Out-Null
}

# is there a path to the release dir?
$timeout = 0
$path = $args[0] 
if ($path) {
    $path = "$path/tools/"
} else {
    $path = "tools/"
    $timeout = 1
}

Write-Host "Kopiere KnxFtpClient..."

$os = "??-Bit"
$setExecutable = 0

if ($?) {
    if ($Env:OS -eq "Windows_NT") {
        if ([Environment]::Is64BitOperatingSystem)
        {
            $os="Windows 64-Bit"
            Copy-Item $path/KnxFtpClient-x64.exe ~/bin/KnxFtpClient.exe
        }
        else
        {
            $os="Windows 32-Bit"
            Copy-Item $path/KnxFtpClient-x86.exe ~/bin/KnxFtpClient.exe
        }
    } elseif ($IsMacOS) {
        $os = "Mac OS"
        $setExecutable = 1
        Copy-Item $path/KnxFtpClient-osx64.exe ~/bin/KnxFtpClient
    } elseif ($IsLinux) {
        $os = "Linux OS"
        $setExecutable = 1
        Copy-Item $path/KnxFtpClient-linux64.exe ~/bin/KnxFtpClient
    }
}
if (!$?) {
    Write-Host "Kopieren fehlgeschlagen, KnxFtpClient ist nicht verfuegbar. Bitte versuchen Sie es erneut."
    if (timeout) {
        if ($IsMacOS -or $IsLinux) { Start-Sleep -Seconds 20 } else { timeout /T 20 }
    }
    Exit 1
}
$version = (Get-Item ~/bin/KnxFtpClient.exe).VersionInfo
$versionString = "$($version.FileMajorPart).$($version.FileMinorPart).$($version.FileBuildPart)"

Write-Host "
    Folgende Tools ($os) wurden im Verzeichnis ~/bin verfuegbar gemacht:
        KnxFtpClient $versionString - OpenKNX Firmware Update ueber den KNX-Bus
"
if ($setExecutable) {
    Write-Host "ACHTUNG: Die Datei ~/bin/KnxFtpClient muss not mit chmod +x ausfuehrbar gemacht werden. Dies muss ueber Kommandozeile geschehen, solange wir keine andere Loesung hierfuer gefunen haben."
}

if ($timeout) {
    if ($IsMacOS -or $IsLinux) { Start-Sleep -Seconds 20 } else { timeout /T 20 }
}