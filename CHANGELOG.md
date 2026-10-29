# Changelog

## [Unreleased]

### Changed
- Frontend redesign for lightness (Services/Models untouched): installed list is now two-line width-proof rows (name + actions on top, version / type / status / description below) instead of a misaligned 4-column table; config & logs page is file cards with preview/copy/reveal/clear actions on the card itself; repair page collapsed from five repeated cards into one card with divided rows. Wiki catalog groups clarified as 活跃插件 / 旧版插件 (the Wiki's own sections)
- Feedback round: config cards stretch to fill the panel (adaptive columns) and show each file's purpose; catalog toolbar (refresh / Wiki / hide-installed / cache hint) merged into the active-group header row, right-aligned; installed list grouped by 用户插件 / 内置插件 so per-row update/uninstall availability is self-explanatory; pill text now uses explicit foregrounds (fixed black text on dark theme); network-diagnosis report deduplicated and shown in a single-button monospace dialog; header shows the plugin's own version
- Row hover is now a subtle scale (1.012) + lift (-1px) animation on the row itself, no background overlay (installed and catalog lists)
- Header title now shows the host QuickLook version (app name + version); the plugin's own version moved to the preview window title bar
- All four tabs now share one fixed toolbar row directly under the tab bar (does not scroll with content); the catalog toolbar moved out of the group header; per-tab refresh buttons removed in favor of a single top-right refresh that acts on the active tab
- Row hover is a pure 2px lift now (scale removed — it shifted the row's action buttons horizontally); catalog toolbar gained a left-aligned search box filtering by name/description, empty groups hide automatically

### Fixed
- Plugin version display read the Metadata.config schema version (`1`) instead of the assembly file version; assemblies without a file version (0.0.0.0) now fall back to InformationalVersion or show 未知版本 instead of `0.0.0.0`
- Group headers (用户 / 内置 / 活跃插件 / 旧版插件) had no explicit foreground and rendered black on the dark theme

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
