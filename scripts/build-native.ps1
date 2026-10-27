$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    $revision = '6b68a5b4fdeca6130b851e5f9366467a844860e5'
    if (!(Test-Path .deps/spout-sdk/.git)) {
        git clone --no-checkout --filter=blob:none https://github.com/leadedge/Spout2.git .deps/spout-sdk
        if ($LASTEXITCODE) { throw 'Spout2 download failed' }
    }
    git -C .deps/spout-sdk checkout $revision
    if ($LASTEXITCODE) { throw 'Pinned Spout2 checkout failed' }
    $cmake = (Get-Command cmake -ErrorAction SilentlyContinue).Source
    if (!$cmake) {
        $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
        $vs = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
        $cmake = Join-Path $vs 'Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe'
    }
    & $cmake -S native -B artifacts/native-build -A x64
    if ($LASTEXITCODE) { throw 'CMake configuration failed' }
    & $cmake --build artifacts/native-build --config Release
    if ($LASTEXITCODE) { throw 'Native compilation failed' }
    Copy-Item .deps/spout-sdk/LICENSE artifacts/native/Spout2-LICENSE.txt
} finally { Pop-Location }
