# Update version from git tag
# Usage: powershell -File Scripts/update-version.ps1

$ErrorActionPreference = "Stop"

$tag = git describe --always --tags "--abbrev=0" 2>$null
if ($tag -notmatch '^v?\d+(\.\d+){1,3}$') { $tag = "1.0.0" }

$revision = git describe --always --tags 2>$null
if (-not $revision) { $revision = "$tag-0-g$(git rev-parse --short HEAD)" }

$version = $tag.TrimStart('v')

$assemblyInfo = @"
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;

[assembly: AssemblyTitle("QuickLook.Plugin.PluginManager")]
[assembly: AssemblyDescription("QuickLook plugin manager")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("QuickLook.Plugin.PluginManager")]
[assembly: AssemblyCopyright("")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)]
[assembly: AssemblyVersion("$version")]
[assembly: AssemblyFileVersion("$version")]
[assembly: AssemblyInformationalVersion("$revision")]
"@

$assemblyInfo | Out-File "$PSScriptRoot\..\Properties\AssemblyInfo.cs" -Encoding utf8

Write-Host "Version updated to: $version ($revision)" -ForegroundColor Green
