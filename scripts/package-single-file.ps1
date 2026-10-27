$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    & ./scripts/build-native.ps1
    if (!(Test-Path artifacts/native/ContentWatch.Spout.dll)) { throw 'Native Spout bridge missing' }
    dotnet publish src/ContentWatch.Desktop -p:PublishProfile=WindowsSingleFile -p:DebugType=embedded -p:UseSharedCompilation=false -o artifacts/single-file
    if ($LASTEXITCODE) { throw 'Single-file publish failed' }
    $executable = 'artifacts/single-file/ContentWatch.exe'
    if (!(Test-Path $executable)) { throw 'ContentWatch.exe was not produced' }
    $sidecars = @(Get-ChildItem artifacts/single-file -File | Where-Object { $_.Extension -in '.dll', '.json' })
    if ($sidecars.Count) { throw 'Publish unexpectedly produced runtime sidecar files' }
    Copy-Item $executable artifacts/ContentWatch.exe -Force
    Get-FileHash artifacts/ContentWatch.exe -Algorithm SHA256
} finally { Pop-Location }
