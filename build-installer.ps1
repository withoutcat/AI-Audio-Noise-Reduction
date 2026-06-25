# build-installer.ps1
# Build the AI Noise Reduction installer using Inno Setup.
# Prerequisites:
#   - Visual Studio 2022 with C++ tools (for Bridge DLL)
#   - .NET SDK 10.0
#   - Inno Setup 6 (https://jrsoftware.org/isdl.php)
#
# Usage: .\build-installer.ps1
#
# CI: GitHub Actions (.github/workflows/release.yml) builds on windows-latest
#     via: choco install innosetup -y; ISCC installer\setup.iss

$ErrorActionPreference = "Stop"
$rootDir = $PSScriptRoot
$bridgeDir = Join-Path $rootDir "src\NoiseReduction.Bridge"
$appProj = Join-Path $rootDir "src\NoiseReduction.App\NoiseReduction.App.csproj"
$helperProj = Join-Path $rootDir "installer\NoiseReduction.InstallerHelper\NoiseReduction.InstallerHelper.csproj"
$issFile = Join-Path $rootDir "installer\setup.iss"

# -----------------------------------------------------------
# Step 0: Check prerequisites
# -----------------------------------------------------------
Write-Host "=== AI Noise Reduction Installer Build ===" -ForegroundColor Cyan
Write-Host ""

# Check Inno Setup
$isccPaths = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 5\ISCC.exe"
)
$iscc = $null
foreach ($p in $isccPaths) {
    if (Test-Path $p) { $iscc = $p; break }
}
if (-not $iscc) {
    Write-Warning "Inno Setup 6 is not installed. Please download from https://jrsoftware.org/isdl.php"
    Write-Warning "Install it and then re-run this script."
    exit 1
}
Write-Host "[OK] Inno Setup: $iscc" -ForegroundColor Green

# Check .NET SDK
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Error "dotnet CLI not found, please install .NET SDK 10.0"
    exit 1
}
Write-Host "[OK] .NET SDK: $($dotnet.Source)" -ForegroundColor Green

# -----------------------------------------------------------
# Step 1: Build Bridge C++ DLL
# -----------------------------------------------------------
Write-Host ""
Write-Host "Step 1/4: Building Bridge C++ DLL..." -ForegroundColor Yellow

$buildBat = Join-Path $bridgeDir "build.bat"
if (Test-Path $buildBat) {
    Push-Location $bridgeDir
    try {
        & cmd.exe /c "build.bat"
        if ($LASTEXITCODE -ne 0) { throw "Bridge build.bat returned error code $LASTEXITCODE" }
        Write-Host "[OK] Bridge DLL built" -ForegroundColor Green
    }
    finally { Pop-Location }
}
else {
    Write-Warning "build.bat not found at $buildBat, skipping Bridge build."
}

# -----------------------------------------------------------
# Step 2: Publish App (self-contained = false, needs .NET runtime)
# -----------------------------------------------------------
Write-Host ""
Write-Host "Step 2/4: Publishing App..." -ForegroundColor Yellow
Write-Host "       dotnet publish $appProj -c Release -r win-x64 --self-contained false"

Push-Location $rootDir
try {
    & $dotnet publish $appProj -c Release -r win-x64 --self-contained false --nologo
    if ($LASTEXITCODE -ne 0) { throw "App publish failed" }

    # Locate publish output directory
    $appPublishDir = Join-Path $rootDir "src\NoiseReduction.App\bin\Release\net10.0-windows\win-x64\publish"
    if (-not (Test-Path $appPublishDir)) {
        throw "App publish directory not found: $appPublishDir"
    }
    $fileCount = (Get-ChildItem $appPublishDir -File).Count
    Write-Host "[OK] App published, $fileCount files" -ForegroundColor Green
}
finally { Pop-Location }

# -----------------------------------------------------------
# Step 3: Publish InstallerHelper (single-file)
# -----------------------------------------------------------
Write-Host ""
Write-Host "Step 3/4: Publishing InstallerHelper..." -ForegroundColor Yellow
Write-Host "       dotnet publish $helperProj -c Release -r win-x64 --self-contained false"

Push-Location $rootDir
try {
    & $dotnet publish $helperProj -c Release -r win-x64 --self-contained false --nologo
    if ($LASTEXITCODE -ne 0) { throw "InstallerHelper publish failed" }

    $helperPublishDir = Join-Path $rootDir "installer\NoiseReduction.InstallerHelper\bin\Release\net10.0-windows\win-x64\publish"
    if (-not (Test-Path $helperPublishDir)) {
        throw "InstallerHelper publish directory not found: $helperPublishDir"
    }
    Write-Host "[OK] InstallerHelper published" -ForegroundColor Green
}
finally { Pop-Location }

# -----------------------------------------------------------
# Step 4: Run Inno Setup
# -----------------------------------------------------------
Write-Host ""
Write-Host "Step 4/4: Compiling Inno Setup installer..." -ForegroundColor Yellow

$outputDir = Join-Path (Split-Path $issFile -Parent) "output"
if (Test-Path $outputDir) {
    Remove-Item "$outputDir\*" -Recurse -Force -ErrorAction SilentlyContinue
}

Push-Location (Split-Path $issFile -Parent)
try {
    & $iscc $issFile
    if ($LASTEXITCODE -ne 0) { throw "ISCC compilation failed with error code $LASTEXITCODE" }
    Write-Host "[OK] Installer compiled" -ForegroundColor Green
}
finally { Pop-Location }

# -----------------------------------------------------------
# Done
# -----------------------------------------------------------
Write-Host ""
Write-Host "=== Build Complete ===" -ForegroundColor Cyan

# Find the output installer
$installerFiles = Get-ChildItem (Join-Path (Split-Path $issFile -Parent) "output") -Filter "*.exe"
if ($installerFiles) {
    Write-Host "Installer: $($installerFiles[0].FullName)" -ForegroundColor Green
}
