# Changelog

## [Unreleased]

## [1.0.0] - 2026-10-29

### Added
- Installed plugin list with user/built-in/loose DLL classification
- Safe uninstall via `*.to_be_deleted` rename, completed by QuickLook on restart
- QuickLook Wiki catalog with active/legacy groups and local cache
- Latest GitHub release resolution, `.qlplugin` selection, download and validation
- Official installer handoff instructions
- Read-only config and exception log viewer
- Repair actions: open plugin folder, restart QuickLook, clean leftovers, unblock files
- Chinese/English UI strings
- GitHub Actions build/release workflow
- Frontend redesign (Services/Models untouched): two-line width-proof installed rows grouped by 用户插件 / 内置插件; config & logs page as adaptive file cards with purpose hints and per-card preview/copy/reveal/clear; repair page as one card with divided rows; fixed per-tab toolbar rows with a single top-right refresh; catalog search by name/description with empty groups hidden; pure 2px-lift row hover; header shows the host QuickLook version while the plugin version moved to the window title bar

### Fixed
- Plugin version display read the Metadata.config schema version (`1`) instead of the assembly file version; assemblies without a file version (0.0.0.0) fall back to InformationalVersion or show 未知版本
- Group headers and list rows had no explicit foregrounds and rendered black on the dark theme; hover scale animation removed (it shifted row action buttons horizontally)
- Network-diagnosis report deduplicated and shown in a single-button monospace dialog
