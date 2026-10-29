# QuickLook.Plugin.PluginManager

预览 QuickLook 自身数据文件夹时打开的插件管理面板。

## 中文

### 简介

一个用来管理 QuickLook 插件的预览插件：在资源管理器里选中 QuickLook 的数据文件夹并按空格，即可打开管理面板。

### 功能特性

- 查看已安装的用户插件与内置插件，含版本与状态
- 安全卸载用户插件（由 QuickLook 下次启动时完成删除）
- 浏览 QuickLook Wiki 插件目录（现行与历史插件分组，本地缓存）
- 从 GitHub Releases 安装/更新插件，带包校验与官方安装器引导
- 查看 `QuickLook.config`、插件配置与 `QuickLook.Exception.log`
- 修复操作：打开插件目录、重启 QuickLook、清理残留、解除下载文件锁定

### 使用方法

1. 在资源管理器中进入 `%APPDATA%\pooi.moe\QuickLook`
2. 选中该文件夹（或其中任意文件），按空格
3. 插件管理面板打开

安装插件会先把 `.qlplugin` 下载到临时目录，并给出配合 QuickLook 官方安装器完成安装的步骤。安装/卸载后需重启 QuickLook。

### 从源码构建

```powershell
dotnet build -c Release
powershell -ExecutionPolicy Bypass -File Scripts/build.ps1
```

产出 `QuickLook.Plugin.PluginManager.qlplugin`。

## English

### Introduction

A QuickLook plugin that opens a management panel when you preview QuickLook's
own data folder.

### Features

- See installed user and built-in plugins, with versions and status
- Safely uninstall user plugins (finished by QuickLook on the next start)
- Browse the QuickLook Wiki catalog (active and legacy plugins)
- Install or update from GitHub releases, with package validation and official
  installer handoff
- Read `QuickLook.config`, plugin configs and `QuickLook.Exception.log`
- Repair actions: open plugin folder, restart QuickLook, clean leftovers,
  unblock downloaded files

### Usage

1. In Explorer, navigate to `%APPDATA%\pooi.moe\QuickLook`.
2. Select the folder (or any file inside it) and press Space.
3. The plugin manager opens.

Installing a plugin downloads the `.qlplugin` to a temporary folder and shows
how to finish with QuickLook's official installer. Restart QuickLook after
installing or uninstalling.

### Build from source

```powershell
dotnet build -c Release
powershell -ExecutionPolicy Bypass -File Scripts/build.ps1
```

The package is `QuickLook.Plugin.PluginManager.qlplugin`.

## Changelog

See [CHANGELOG.md](CHANGELOG.md)

## Credits

- [QuickLook](https://github.com/QL-Win/QuickLook) by QL-Win

## License

[GPL-3.0](LICENSE)
