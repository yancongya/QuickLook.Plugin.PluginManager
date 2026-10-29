# Build and package QuickLook.Plugin.PluginManager
param(
    [switch]$Clean,
    [switch]$SkipBuild,
    [switch]$SkipVersion
)

$ErrorActionPreference = "Stop"
$project = "QuickLook.Plugin.PluginManager.csproj"
$releaseDir = "bin/Release"
$pluginFile = "QuickLook.Plugin.PluginManager.qlplugin"

if (-not $SkipVersion) {
    Write-Host "Updating version..." -ForegroundColor Yellow
    powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\update-version.ps1"
}

if ($Clean) {
    Write-Host "Cleaning..." -ForegroundColor Yellow
    dotnet clean $project -c Release
    Remove-Item $pluginFile -ErrorAction SilentlyContinue
}

if (-not $SkipBuild) {
    Write-Host "Building..." -ForegroundColor Yellow
    dotnet build $project -c Release
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        exit 1
    }
}

Write-Host "Packaging..." -ForegroundColor Yellow
Remove-Item $pluginFile -ErrorAction SilentlyContinue

$files = Get-ChildItem -Path $releaseDir -Exclude "*.pdb", "*.xml"
Compress-Archive -Path $files -DestinationPath "$pluginFile.zip"
Move-Item "$pluginFile.zip" $pluginFile -Force

$size = (Get-Item $pluginFile).Length / 1MB
Write-Host "Created: $pluginFile ($([math]::Round($size, 2)) MB)" -ForegroundColor Green
