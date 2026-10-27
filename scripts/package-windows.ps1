param(
    [ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9._-]*$')]
    [string]$OutputName = 'ContentWatch-win-x64',
    [switch]$SkipTests
)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    & ./scripts/build-native.ps1
    if (!$SkipTests) {
        dotnet test ContentWatch.slnx -c Release -p:UseSharedCompilation=false
        if ($LASTEXITCODE) { throw 'Tests failed' }
    }
    $packagePath = Join-Path 'artifacts' $OutputName
    dotnet publish src/ContentWatch.Desktop -c Release -r win-x64 --self-contained true -o $packagePath -p:UseSharedCompilation=false
    if ($LASTEXITCODE) { throw 'Publish failed' }
    if (!(Test-Path (Join-Path $packagePath 'ContentWatch.Spout.dll'))) { throw 'Real Spout bridge missing from package' }
    Copy-Item README.md,LICENSE,THIRD-PARTY-NOTICES.md $packagePath/
    Copy-Item -Recurse docs $packagePath/ -Force
    Copy-Item artifacts/native/Spout2-LICENSE.txt $packagePath/
    Compress-Archive -Path "$packagePath/*" -DestinationPath "$packagePath.zip" -Force
    Get-FileHash "$packagePath.zip" -Algorithm SHA256
} finally { Pop-Location }
