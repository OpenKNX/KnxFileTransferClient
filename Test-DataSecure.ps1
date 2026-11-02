$ErrorActionPreference = 'Stop'
$localDotnet = Join-Path $PSScriptRoot '..\.dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) { (Resolve-Path -LiteralPath $localDotnet).Path } else { (Get-Command dotnet).Source }
$project = Join-Path $PSScriptRoot '..\Kaenx-Konnect\tests\DataSecureRegression\DataSecureRegression.csproj'
& $dotnet run --project $project -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $dotnet run --project $project -c Release --no-build -- --udp
exit $LASTEXITCODE
