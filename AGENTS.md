# AGENTS.md

## Build Commands

```powershell
# Build
dotnet build -c Release

# Package (CI calls Scripts/build.ps1 -SkipBuild -SkipVersion; naming lives only in build.ps1)
powershell -ExecutionPolicy Bypass -File Scripts/pack-zip.ps1
```

## Project Overview

QuickLook plugin manager. Pure WPF (no WebView2). It is offered when QuickLook
previews a path under its own data root, so opening the data folder shows a
management panel instead of the binary viewer.

## Key Files

- `Plugin.cs` - `IViewer` entry point; `Priority = 100`, rejects `.qlplugin`,
  accepts paths under the QuickLook data root.
- `PluginManagerPanel.xaml(.cs)` - tabbed UI: Installed, Available, Config & Logs, Repair.
- `Services/DataRootResolver.cs` - reflects `App.UserPluginPath` /
  `SettingHelper.LocalDataPath`; validates the data root.
- `Services/InstalledPluginService.cs` - scans user and built-in plugins, safe uninstall.
- `Services/WikiCatalogService.cs` - parses the QuickLook Wiki catalog, caches to
  `{DataRoot}\PluginManager\catalog.json`.
- `Services/GitHubReleaseService.cs` - resolves the latest release, selects and
  downloads a `.qlplugin`.
- `Services/SafeArchive.cs` - zip-slip and metadata validation before handoff.
- `Services/ConfigLogService.cs`, `Services/RepairService.cs` - config/log viewer,
  restart, cleanup, unblock.
- `Translations.config` - Chinese/English UI strings, read directly by `Loc`.

## Architecture

```
Plugin.cs (IViewer)
  └── PluginManagerPanel (WPF UserControl)
        ├── InstalledPluginService  -> data root scan / safe uninstall
        ├── WikiCatalogService      -> Wiki markdown -> catalog.json
        ├── GitHubReleaseService    -> latest release -> .qlplugin in %TEMP%
        ├── SafeArchive             -> zip-slip + Metadata.config validation
        ├── ConfigLogService        -> read-only config/log view
        └── RepairService           -> restart / cleanup / unblock
```

## Install Handoff

The plugin never extracts packages itself. It downloads and validates a
`.qlplugin`, then tells the user to select it in Explorer, press Space, and
click "Click here to install this plugin." The official `PluginInstaller`
extracts it and asks for a restart.

## Uninstall

Files are renamed to `{GUID}.to_be_deleted`; the official installer deletes
them on the next QuickLook start. The manager never uninstalls itself and never
touches the built-in plugin folder.

## CI / Release

- push `master` / PR / manual: build + package, upload artifact (14 days), no release
- tag `v*`: build + package, create GitHub Release with
  `QuickLook.Plugin.PluginManager.qlplugin`
- packaging names are maintained only in `Scripts/build.ps1`; the workflow must
  not inline a second copy

## Conventions

- Target framework: .NET Framework 4.6.2
- Namespace: `QuickLook.Plugin.PluginManager`
- XAML files at project root
- No `MessageBox`; use the inline confirm overlay
- Do not rely on Enter or arrow keys (QuickLook consumes them)
- Keep `CanHandle` free of disk I/O
