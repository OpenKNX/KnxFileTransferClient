# check for working dir
if (Test-Path -Path release) {
    # clean working dir
    Remove-Item -Recurse release\*
} else {
    New-Item -Path release -ItemType Directory | Out-Null
}

# create required directories
New-Item -Path release/tools -ItemType Directory | Out-Null

# build publish version of KnxFileTransferClient
dotnet.exe build KnxFileTransferClient.csproj
dotnet.exe publish KnxFileTransferClient.csproj -c Debug -r win-x64   --self-contained true /p:PublishSingleFile=true
dotnet.exe publish KnxFileTransferClient.csproj -c Debug -r win-x86   --self-contained true /p:PublishSingleFile=true
dotnet.exe publish KnxFileTransferClient.csproj -c Debug -r osx-x64   --self-contained true /p:PublishSingleFile=true
dotnet.exe publish KnxFileTransferClient.csproj -c Debug -r linux-x64 --self-contained true /p:PublishSingleFile=true

# copy package content 
Copy-Item bin/Debug/net6.0/win-x64/publish/KnxFileTransferClient.exe   release/tools/KnxFileTransferClient-x64.exe
Copy-Item bin/Debug/net6.0/win-x86/publish/KnxFileTransferClient.exe   release/tools/KnxFileTransferClient-x86.exe
Copy-Item bin/Debug/net6.0/osx-x64/publish/KnxFileTransferClient       release/tools/KnxFileTransferClient-osx64.exe
Copy-Item bin/Debug/net6.0/linux-x64/publish/KnxFileTransferClient     release/tools/KnxFileTransferClient-linux64.exe

# add necessary scripts
# Copy-Item scripts/Readme-Release.txt release/
Copy-Item scripts/Install-KnxFileTransferClient.ps1 release/

# We execute the install script to get the current KnxFileTransferClient-version to our bin directory for the right hardware
release/Install-KnxFileTransferClient.ps1 release

# build release name
$version = (Get-Item release/tools/KnxFileTransferClient-x64.exe).VersionInfo
$versionString = "$($version.FileMajorPart).$($version.FileMinorPart).$($version.FileBuildPart)"
$ReleaseName = "KnxFileTransferClient $versionString"
$ReleaseName = $ReleaseName.Replace(" ", "-") + ".zip"

# create package 
Compress-Archive -Force -Path release/* -DestinationPath "$ReleaseName"
Remove-Item -Recurse release/*
Move-Item "$ReleaseName" release/

