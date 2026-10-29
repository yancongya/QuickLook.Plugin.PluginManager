# Quick packaging script (run from project root)
$ErrorActionPreference = "Stop"

Remove-Item QuickLook.Plugin.PluginManager.qlplugin -ErrorAction SilentlyContinue

$files = Get-ChildItem -Path bin/Release -Exclude "*.pdb", "*.xml"
Compress-Archive -Path $files -DestinationPath "QuickLook.Plugin.PluginManager.zip"
Move-Item "QuickLook.Plugin.PluginManager.zip" "QuickLook.Plugin.PluginManager.qlplugin"

Write-Host "Done: QuickLook.Plugin.PluginManager.qlplugin" -ForegroundColor Green
