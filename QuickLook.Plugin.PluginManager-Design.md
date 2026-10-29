# QuickLook.Plugin.PluginManager 设计文档

> 面向实现 Agent 的需求 / 路线 / 设计说明。
> 目标：在官方暂无完整管理 UI 的前提下，用独立插件提供轻量可视化管理能力。
>
> 版本：v2（调研修订版，2026-10-29）
> 命名结论：保留 `QuickLook.Plugin.PluginManager`。主程序内部类叫 `QuickLook.PluginManager`，但插件的全限定名是 `QuickLook.Plugin.PluginManager.Plugin`，两者不冲突；窗口标题可用「QuickLook 插件管理」，不需要为了避嫌改名为 PluginHub。

---

## 0. 本次修订摘要

相对上一版，以下结论已经过源码核对或本机实测，必须按本版实现：

| # | 上一版 | 本版修正 |
|---|--------|----------|
| 1 | 用路径段含 `pooi.moe\QuickLook` 识别数据根 | 反射 `QuickLook.App.UserPluginPath`，兼容安装版 / 便携版 / Store 版 |
| 2 | `QuickLookException.log` | 正确文件名是 `QuickLook.Exception.log` |
| 3 | 缓存放 `{DataRoot}\QuickLook.Plugin.PluginManager\` | 与插件安装目录重合，升级时会被清理；改放 `{DataRoot}\PluginManager\` |
| 4 | 卸载直接删除插件目录 | DLL 被进程锁住；改为重命名为 `{GUID}.to_be_deleted`，下次启动由官方安装器清理 |
| 5 | `Priority >= 100`，含义模糊 | 固定 `Priority = 100`；`CanHandle` 对 `.qlplugin` 显式返回 `false` |
| 6 | 自己解压安装 | 安装统一交给官方 `.qlplugin` 安装器；临时文件放 `%TEMP%` |
| 7 | 依赖 Enter 键 / `MessageBox` 确认 | Enter 会「打开目录并关窗」；`MessageBox` 会触发 `Deactivated`。改为鼠标点击 + 面板内联确认 |
| 8 | Wiki 版本当作最新版 | Wiki 会过期；同时显示安装版 `Metadata.Version` 与 release tag |
| 9 | 按仓库名匹配 `.qlplugin` 资产 | 存在误传资产、仓库改名；按「唯一 `.qlplugin`」选择，下载后校验 `Metadata.Namespace` |
| 10 | 纯 WPF 但主题未定 | 用 `DynamicResource` 复用主程序 / Wpf.Ui 主题键，不硬编码颜色 |

---

## 1. 背景与目标

### 1.1 背景

- Windows QuickLook（QL-Win）官方以托盘为主，**没有完整的插件管理面板**。
- 安装：选中 `.qlplugin` 空格 → 安装 → 重启。
- 卸载：退出后手动删除数据目录下插件文件夹。
- 可安装列表维护在官方 Wiki：
  https://github.com/QL-Win/QuickLook/wiki/Available-Plugins
  含 **List of plugins**（推荐）与 **List of legacy plugins**（停更 / 已替代 / 已内置）。
- 官方 5.0.0 里程碑中有管理 UI、插件清单等 issue（如 #1916、#1908、#1198），**尚未承诺落地**；本插件作为独立方案补位。

### 1.2 产品目标

在识别到用户 **QuickLook 数据目录**（及其下任意文件/文件夹）时，空格打开 **WPF 管理面板**，提供：

1. 已安装插件的查看 / 打开目录 / 安全卸载
2. Wiki 可安装列表（缓存 + 刷新；区分 active / legacy）
3. 配置与日志的可视化查看与轻量管理
4. 修复类操作（重启、清理残留、解除阻止等）
5. 解析 GitHub latest release 的 `.qlplugin` 资产，下载后交给官方安装器

### 1.3 非目标（第一版不做或慎做）

- 不修改 QuickLook 主程序
- 不做完整「设置中心」（热键、开机启动等以官方 / CONFIG 为准）
- 不做插件热启用 / 禁用（加载器不支持热插拔）
- 不默认接管所有普通文件夹（只接管已确认的数据根子树）
- 不把 WebView 缓存目录当插件卸载
- 不内置完整安装器；安装统一走官方 `.qlplugin` 流程
- 不支持自身卸载 / 自身静默更新
- 不在面板内深度编辑 XML 配置

### 1.4 已核对事实（实现约束）

以下来自本仓库源码与本机实测，实现时不要再猜：

- 插件加载顺序：先 `App.UserPluginPath`，再 `AppPath\QuickLook.Plugin`；扫描模式 `QuickLook.Plugin.*.dll` + `AllDirectories`；按 `Priority` 降序稳定排序（`QuickLook/QuickLook/PluginManager.cs:38-39,89,115`）。
- `FindMatch` 对每个插件调用 `CanHandle`，异常被吞掉并记录耗时；命中后 `CreateInstance` **每次预览新建实例**（`QuickLook/QuickLook/PluginManager.cs:52-79`）。因此 `Plugin` 上的静态缓存跨预览共享，`Prepare/View` 是每次新对象。
- `QuickLook.App.UserPluginPath` 是 `public static readonly`（`QuickLook/QuickLook/App.xaml.cs:41`），可直接反射读取。
- `SettingHelper.LocalDataPath` 也是 `public static readonly`，便携版判断依赖 `QuickLook.Common.dll` 旁的 `portable.lock`（`QuickLook/QuickLook.Common/Helpers/SettingHelper.cs:29-33,63-66`）。
- 官方安装器 `Priority = int.MaxValue`，只处理 `.qlplugin`，读 zip 内 `Metadata.config` 的 `Namespace`，要求以 `QuickLook.Plugin.` 开头，解压到 `UserPluginPath\{Namespace}`，旧文件重命名为 `{GUID}.to_be_deleted`，下次启动 `Init` 清理（`QuickLook/QuickLook.Plugin/QuickLook.Plugin.PluginInstaller/Plugin.cs:28-37,68`、`PluginInfoPanel.xaml.cs:90,97,118-119,135-137`）。
- 官方清理只删除 `*.to_be_deleted` 文件，不删除空目录。
- 预览窗口 `MinWidth=400`、`MinHeight=200`、`CanResize`、`TitlebarOverlap=false`（`QuickLook/QuickLook/ViewerWindow.xaml:15-23`）；`_customWindowSize` 会记住用户调整的尺寸。
- `Deactivated` + `CloseOnLostFocus` 会关窗（`QuickLook/QuickLook/ViewerWindow.xaml.cs:75-79`）。
- 全局键盘钩子：`Enter → RunAndClose`（打开当前路径并关窗）、方向键切换预览、`Space` 关闭、`F5` 重载、`F11` 全屏（`QuickLook/QuickLook/KeystrokeDispatcher.cs`、`ViewerWindow.Actions.cs:46-68`）。
- 扩展名过滤发生在 `FindMatch` 之前，目录与无扩展名文件始终放行（`QuickLook/QuickLook/ViewWindowManager.cs:160,185`、`QuickLook/QuickLook/Helpers/ExtensionFilterHelper.cs`）。
- 异常日志由 `ProcessHelper` 以 `FileShare.Read` 打开写入（`QuickLook/QuickLook.Common/Helpers/ProcessHelper.cs:80-82`），面板读取时要允许共享，清空可能失败。
- `.qlplugin` 是 zip，条目名用反斜杠；.NET Framework 的 `ZipFile.ExtractToDirectory` 能正确还原嵌套目录（本机实测 PagViewer 包 23 个文件）。官方安装器可用，但自写解压仍要防 zip-slip。
- NuGet `QuickLook.Common` 最新只有 `4.5.0`；主程序 4.6.0 的 `IMoreMenu/MoreMenu` 不在该包中，管理器不要使用标题栏菜单接口。

---

## 2. 数据根与触发范围

### 2.1 数据根定位

不要写死 `%APPDATA%\pooi.moe\QuickLook`，也不要用路径段匹配。三种发行版的实际位置：

| 发行版 | 数据根 |
|--------|--------|
| MSI / 普通安装版 | `%APPDATA%\pooi.moe\QuickLook\` |
| 便携版（`portable.lock` 与 `QuickLook.Common.dll` 同目录） | `<app>\UserData\` |
| Store 版 | `%LOCALAPPDATA%\Packages\21090PaddyXu.QuickLook_egxr34yet59cg\LocalCache\Roaming\pooi.moe\QuickLook\` |

推荐实现（按顺序）：

```text
1. 反射 QuickLook.App.UserPluginPath
   - AppDomain.CurrentDomain.GetAssemblies()
   - 找 GetName().Name == "QuickLook"
   - GetType("QuickLook.App").GetField("UserPluginPath",
       BindingFlags.Public | BindingFlags.Static)
   - 取到的就是 {DataRoot}\QuickLook.Plugin\
   - DataRoot = 去掉末尾 QuickLook.Plugin 的父目录

2. 反射 QuickLook.Common.Helpers.SettingHelper.LocalDataPath
   - 同上，找 QuickLook.Common 程序集
   - DataRoot = LocalDataPath

3. 最后回退：自身程序集位置
   - 插件通常位于 {DataRoot}\QuickLook.Plugin\{Namespace}\
   - 取 Assembly.GetExecutingAssembly().Location 的上两级
   - 必须验证 {candidate}\QuickLook.Plugin 存在
```

验证条件（宁严勿松）：

- `Directory.Exists(Path.Combine(root, "QuickLook.Plugin"))`
- 且至少满足其一：
  - `File.Exists(Path.Combine(root, "QuickLook.config"))`
  - `Directory.Exists(Path.Combine(root, "WebView2_Data"))`
  - `root` 的目录名是 `UserData`
  - 本插件自身的程序集路径位于 `{root}\QuickLook.Plugin\` 之下（新装环境可能还没有 `QuickLook.config` / `WebView2_Data`，这一条保证仍能识别）
- 路径规范化：`Path.GetFullPath`、去掉末尾分隔符、`OrdinalIgnoreCase` 比较
- 边界判断：`path == root || path.StartsWith(root + "\\")`，避免 `C:\...\QuickLook2` 误命中
- 若候选根等于 `QuickLook.App.AppPath`，拒绝（插件被手动装进内置目录时，不要把程序目录当数据根）
- 首次解析成功后静态缓存；`CanHandle` 里不要做磁盘 I/O

### 2.2 数据根常见内容

| 名称 | 作用 | 管理策略 |
|------|------|----------|
| `QuickLook.Plugin\` | 用户 / 第三方插件 | **核心管理对象** |
| `QuickLook.config` | 主程序全局配置 | 查看 / 外部打开 |
| `QuickLook.Plugin.*.config` | 各插件配置（如 `QuickLook.Plugin.ImageViewer.config`、`QuickLook.Plugin.TextViewer.config`） | 列表 + 只读预览 + 打开 |
| `QuickLook.Exception.log` | 异常日志 | 查看尾部 / 复制 / 清空（确认，可能失败） |
| `WebView2_Data\` | WebView2 用户数据 | 仅说明 + 打开目录，不按插件卸载 |
| `WebViewPlus_Data\` | WebViewPlus 插件数据 | 同上 |

### 2.3 CanHandle 规则（关键）

**只要当前 path 落在「已确认的 QuickLook 数据根」之下，无论根目录、子文件夹还是任意文件，均由本插件接管。**

判定流程：

```text
1. 取 path（文件或目录）
2. 规范化并做边界判断
3. 命中已缓存的数据根 → CanHandle = true
4. 未命中 → false（交给默认 InfoPanel / 文本 / 图片等）
```

硬性规则：

- `CanHandle` 必须是纯字符串 / 路径判断，不能读磁盘、不能扫目录、不能发网络请求。
- 对 `.qlplugin` 文件显式返回 `false`，即使它位于数据根内，也交给官方安装器。
- 未选中具体项时系统可能预览「第一项」；只要该项仍在数据根下，同样命中。
- 禁止仅凭文件夹名叫 `QuickLook` 就接管。
- 数据根解析失败时返回 `false`，不要猜测。

扩展名过滤提醒：如果用户开启白名单模式，`.config` / `.log` 等文件可能在 `FindMatch` 之前就被拦截；**数据根文件夹入口不受影响**，所以「选中数据目录空格」始终是主入口。

### 2.4 Priority

固定：

```csharp
public int Priority => 100;
```

依据：

| 插件 | Priority |
|------|----------|
| `PluginInstaller` | `int.MaxValue` |
| 本插件 | `100` |
| `ELFViewer` | `11` |
| `ChmViewer` | `2` |
| 多数内置预览器 | `0` |
| `CLSIDViewer` / `OfficeViewer` / `PDFViewer` | `-1` |
| `ArchiveViewer` / `HelixViewer` / `TextViewer` | `-5` |
| `BinaryViewer` | `-10` |
| `InfoPanel`（文件夹兜底） | `int.MinValue` |

不能用 `int.MaxValue`：会与 `PluginInstaller` 打平；由于用户插件先加载，排序后会抢在官方安装器前面，破坏 `.qlplugin` 安装。

---

## 3. 功能需求

### 3.1 已安装插件

扫描范围：

- `{UserPluginPath}\*` 一级子目录（官方安装器的布局）
- `{UserPluginPath}\QuickLook.Plugin.*.dll` 散装 DLL（用户手动解压的情况；有同名目录时以目录为准，避免重复）
- 可选只读分组：`{AppPath}\QuickLook.Plugin\` 内置插件，标记「内置」，不提供卸载

展示字段：

- 名称：优先 `Metadata.config` 的 `Namespace`，否则目录名 / DLL 名
- 版本：读 `QuickLook.Plugin.Metadata.config` 的 `/Metadata/Version`；格式是字符串，可能是 `1`、`1.0.1-beta`、`1.6.3`，解析失败就原样显示
- 描述：`/Metadata/Description`，可空
- 路径、文件数、最后修改时间
- 状态：正常 / 缺少 Metadata / 散装 DLL / 内置

操作：

- 打开文件夹（`explorer.exe /select,"path"` 或打开目录）
- 复制路径
- 卸载（见下）
- 刷新列表

#### 安全卸载算法

不要直接 `Directory.Delete`。正在加载的 DLL 会被锁住，而且官方升级依赖 `*.to_be_deleted` 机制。

```text
1. 拒绝卸载自身：Namespace == QuickLook.Plugin.PluginManager
2. 面板内联二次确认
3. 递归枚举插件目录下的文件（跳过已经是 *.to_be_deleted 的）
4. 逐个重命名为同目录的 {GUID}.to_be_deleted
   - 记录成功/失败
   - 失败通常是文件被占用
5. 若有失败：尽力把已重命名的文件改回原名，向用户报告失败文件
6. 不删除目录，不删除 .to_be_deleted 文件
7. 提示「重启 QuickLook 后完成清理」，提供重启按钮
```

补充：

- 官方 `PluginInstaller.Init` 在下次启动时删除 `*.to_be_deleted`；它的优先级是 `int.MaxValue`，会先于本插件执行。
- 官方只删文件，不删空目录；本插件在面板打开或 `Init` 时扫描空目录，提供「清理残留」。
- 如果用户坚持不重启，插件目录里会留下 `.to_be_deleted` 文件；加载器只匹配 `QuickLook.Plugin.*.dll`，不会误加载，但要在 UI 里说明。
- 卸载后刷新本地列表；不要假装「已删除」，显示「等待重启清理」。

### 3.2 可安装插件（Wiki）

**数据源（用 raw Markdown，不要抓 HTML）：**

```text
https://raw.githubusercontent.com/wiki/QL-Win/QuickLook/Available-Plugins.md
```

页面结构：

- `### List of plugins`（三级标题）+ 表格
- `## List of legacy plugins` + 表格
- 表格列：`Plugin | Last Update | DL | Description`
- 首列形如 `[Name](repoUrl) v1.2`
- legacy 行可能用 `~~删除线~~` 包裹，并带「已内置 / 已替代」提示
- DL 列多为 `[Link](.../releases)`，也可能指向 `/releases/tag/...`

解析要求：

- 标题大小写不敏感，允许 `##` / `###` / `####`
- 表格行按 `|` 拆分，跳过 `---` 分隔行
- 去掉 `~~` 后再解析；`legacy = true`
- HTML 实体解码；trim；格式不完整的行跳过并计数
- 取第一个仓库链接；releases 链接优先用 DL 列，否则拼 `{repo}/releases`
- 描述保留原文（可能包含替代插件提示），不要执行其中的任何内容

缓存：

```text
{DataRoot}\PluginManager\catalog.json
```

**不要**放在 `{DataRoot}\QuickLook.Plugin.PluginManager\`：那是插件安装目录，升级时会被官方安装器重命名 / 清理。

缓存字段：

```json
{
  "schemaVersion": 1,
  "updatedAt": "ISO-8601",
  "sourceUrl": "https://raw.githubusercontent.com/wiki/QL-Win/QuickLook/Available-Plugins.md",
  "etag": "...",
  "plugins": [
    {
      "name": "PagViewer",
      "versionText": "v1.0",
      "description": "...",
      "repoUrl": "https://github.com/yancongya/QuickLook.Plugin.PagViewer",
      "releasesUrl": "https://github.com/yancongya/QuickLook.Plugin.PagViewer/releases",
      "list": "active",
      "downloadUrl": null,
      "downloadVersion": null,
      "downloadFetchedAt": null
    }
  ]
}
```

刷新策略：

- 打开面板先读缓存，立即显示；缓存不存在才显示加载态
- 缓存超过 24h 可在后台刷新；用户点「刷新」强制刷新
- 条件请求：有 `etag` 就带 `If-None-Match`；304 保留旧缓存
- 超时 10s；失败保留旧缓存并提示；无缓存则错误态 + 「在浏览器打开 Wiki」
- 不因刷新失败清空缓存

与已安装关联：

- 用 `Namespace`、目录名、仓库名做归一化匹配（去掉 `QuickLook.Plugin.`、空格、标点、`-`/`_` 差异）
- 命中显示「已安装」；模糊命中显示「可能已安装」
- 不因为名称匹配就允许直接覆盖；安装仍走官方安装器
- Wiki 版本只作参考，不能当最新版

### 3.3 安装 / 更新

**安装统一交给官方 `.qlplugin` 安装器**，本插件只负责找到、下载、校验、把文件交给用户。

流程：

```text
1. 用户点「安装」/「更新」
2. 按需请求 GitHub latest release（只查当前这一条，不批量）
3. 选择资产：
   - 恰好一个 *.qlplugin → 用它
   - 多个 → 优先名称包含插件名的；否则弹出选择列表
   - 没有 → 打开 releases 页
4. 下载到 %TEMP%\QuickLook.PluginManager\{guid}\{assetName}
   - 超时、取消、大小上限（建议 200 MB）
5. 校验：
   - 文件存在且非空
   - 前两个字节是 PK
   - 能作为 zip 打开
   - 根目录存在 QuickLook.Plugin.Metadata.config
   - Namespace 以 QuickLook.Plugin. 开头
   - 若已知期望插件，Namespace 必须匹配；不匹配就中止
6. 用资源管理器选中文件，提示：
   「按空格 → 点击 “Click here to install this plugin.” → 重启 QuickLook」
7. 不自动解压、不自动替换、不自动重启
```

注意：下载完成后不要在本面板 `Cleanup` 时删除临时文件。用户按空格会关闭当前预览并打开 `.qlplugin` 的官方安装器；如果此时文件已被删除，安装会失败。临时目录由下次启动的「清理旧下载」处理（超过 24h）。

资产选择的真实坑：

- `yancongya/QuickLook.Plugin.SvgaViewer` 的 `v1.0.0` release 里同时有正确的 Svga 资产和误传的 `QuickLook.Plugin.PagViewer.qlplugin`
- `marcoduering/MIQ-Win` 仓库名与插件名不一致，资产是 `QuickLook.Plugin.MIQ.qlplugin`
- 所以不能只靠仓库名 / 资产名；**下载后校验 `Metadata.Namespace` 是最后一道防线**

更新：

- 安装版版本：读本地 `Metadata.config` 的 `Version`
- 远端版本：点「检查更新」时才查 release tag
- 两边都能解析且远端更高才显示「可更新」；解析失败只显示两个原始字符串
- 更新同样下载后交给官方安装器；官方安装器会把旧文件重命名，重启后清理
- 自身（`QuickLook.Plugin.PluginManager`）不提供自动更新，只打开 Releases 页

GitHub API 约束：

- `GET https://api.github.com/repos/{owner}/{repo}/releases/latest`
- 必须带 `User-Agent: QuickLook.Plugin.PluginManager/{version}`
- net462 显式开 TLS 1.2：`ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12`
- 未认证 60 次/小时；只在用户点击时查，按仓库缓存 24h
- 403 且 `X-RateLimit-Remaining: 0` 时提示重置时间，并回退打开 releases 页
- 可选校验 API 返回的 `digest`（`sha256:...`）；没有就跳过

### 3.4 配置与日志

| 对象 | 查看 | 管理 |
|------|------|------|
| `QuickLook.config` | 只读文本 / 简易键值 | 用系统默认编辑器打开 |
| `QuickLook.Plugin.*.config` | 列出所有匹配文件，点选只读预览 | 打开文件 |
| `QuickLook.Exception.log` | 只读文本区，显示大小 / 修改时间，可滚到底 | 复制、清空（确认）、打开文件 |

日志实现要点：

- 路径：`{DataRoot}\QuickLook.Exception.log`
- 读取用 `FileShare.ReadWrite | FileShare.Delete`，避免 QuickLook 正在写时打不开
- 只读尾部（建议 512 KB）并在 UI 标明「仅显示最后 N KB」，避免大日志卡死
- 清空用确认浮层；尝试 `FileMode.Truncate` / `File.WriteAllText`
- QuickLook 的日志写入以 `FileShare.Read` 打开，可能拒绝其他写入；失败时提示「QuickLook 正在写日志，请重试或重启后清空」，不要删除文件
- 配置只读，不做面板内编辑；需要修改时用默认编辑器打开

### 3.5 修复 / 兼容

| 操作 | 说明 |
|------|------|
| 重启 QuickLook | 安装 / 卸载后生效；面板内联确认 |
| 清理 `.to_be_deleted` 残留 | 删除能删的残留文件；被占用的跳过并报告 |
| 清理空插件目录 | 只删空目录；不碰有文件的目录 |
| 解除插件阻止 | 清除 `UserPluginPath` 下文件的 Zone.Identifier；官方启动时也会做，作为可选手动修复 |
| 打开数据目录 / 插件目录 | 快捷入口 |
| WebView 缓存目录 | 仅打开 + 说明，不默认删除 |

文案用「修复 / 重启」，不要用「重置」，避免被理解成恢复出厂或删光插件。所有破坏性操作都要确认。

重启实现参考官方面板：启动新进程后杀掉当前进程。

```csharp
using var process = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                AppDomain.CurrentDomain.FriendlyName),
        WorkingDirectory = Environment.CurrentDirectory,
        UseShellExecute = true,
    },
};
process.Start();
Process.GetCurrentProcess().Kill();
```

注意单实例竞态：新进程可能因互斥量退出。官方面板就是这么做的；如果实测不稳定，再改成延迟启动（`cmd /c timeout /t 1 /nobreak >nul & start "" "..."`），但不要引入常驻进程。

### 3.6 UI 技术选型与约束

- **纯 WPF**（列表、按钮、内联确认、文本预览）
- **不使用 WebView2**（管理页无 PAG / 网页引擎需求，保持轻量）
- 主题：用 `DynamicResource` 复用主程序资源，不硬编码颜色
- 尺寸：`context.SetPreferredSizeFit(new Size(960, 640), 0.9)`；`CanResize = true`；`TitlebarOverlap = false`
- 窗口最小只有 400×200，布局必须能在小尺寸下滚动 / 折叠
- 不用 `MessageBox`；用面板内联浮层 / 确认条
- 不依赖 Enter / 方向键；第一版不做搜索框，列表规模小，滚动 + 分组足够
- 后续如加搜索：只做「输入即筛选」，不做 Enter 提交；并接受方向键被 QuickLook 全局钩子截获的限制

可用主题键（`ViewerWindow` 合并了 `QuickLook.Common;component/Styles/MainWindowStyles.xaml`，Wpf.Ui 主题字典也在 `Application.Resources`）：

```text
WindowTextForeground
WindowTextForegroundAlternative
MainWindowBackground
MainWindowBackgroundNoTransparent
CardBackground
ButtonBackground
ControlCornerRadius
ControlElevationBorderBrush
SymbolThemeFontFamily
```

如果某个键在运行时不存在，`DynamicResource` 不会抛异常，但视觉会退化；关键颜色可在代码里 `TryFindResource` 后给 fallback。

---

## 4. 面板信息架构

单窗口、Tab，保持轻量：

```text
┌─────────────────────────────────────────────────────────────┐
│ QuickLook 插件管理              [刷新本地]  [打开数据目录]   │
├─────────────────────────────────────────────────────────────┤
│ [已安装]  [可安装]  [配置与日志]  [修复]                       │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  （当前 Tab 内容区）                                          │
│                                                             │
├─────────────────────────────────────────────────────────────┤
│ 数据目录: …\pooi.moe\QuickLook    已安装 n 个 · 缓存更新于 … │
└─────────────────────────────────────────────────────────────┘
```

通用状态：

- 加载中：局部进度 / 文本，不阻塞整个面板
- 空状态：说明 + 主操作按钮
- 错误态：错误摘要 + 重试 / 打开目录 / 打开 Wiki
- 内联确认：覆盖在内容区上方的半透明层，含「取消 / 确认」，不弹系统窗口

### 4.1 Tab：已安装

- 表格：名称 | 版本 | 类型（用户 / 内置 / 散装） | 操作
- 操作：打开文件夹 / 复制路径 / 卸载
- 自身行：卸载按钮禁用，提示「不能卸载正在运行的插件」
- 卸载后行状态变为「等待重启清理」
- 底部显示 `UserPluginPath` 与内置插件目录

### 4.2 Tab：可安装

- 工具条：刷新 Wiki | 在浏览器打开 Wiki | 上次更新时间
- 主列表：active 插件
- 可折叠分组：Legacy（警告样式；描述保留 Wiki 提示）
- 行操作：打开仓库 / 打开 Releases / 安装（或「已安装」/「可更新」）
- 已安装标记；模糊匹配显示「可能已安装」
- 安装结果只显示「已交给官方安装器，请按空格安装」，不伪造进度

### 4.3 Tab：配置与日志

- 入口列表：
  - `QuickLook.config` → 预览 + 打开
  - 插件配置文件列表 → 点选预览 + 打开
  - `QuickLook.Exception.log` → 预览 + 复制 + 清空 + 打开
- 文本区只读，等宽字体，显示文件大小 / 修改时间
- 清空失败时显示原因和「重启后重试」

### 4.4 Tab：修复

- 重启 QuickLook（确认）
- 清理 `.to_be_deleted` 残留（确认 + 结果摘要）
- 清理空插件目录（确认 + 结果摘要）
- 解除插件目录阻止（确认 + 结果摘要）
- 打开数据目录 / 打开插件目录
- WebView 缓存目录：仅打开 + 简短说明

---

## 5. 渐进路线

### Phase 1 — 可用管理壳（优先交付）

- [x] 需求已定义
- 数据根解析（反射 `UserPluginPath` + 校验 + 缓存）
- `CanHandle` 数据根整棵子树；`.qlplugin` 返回 false
- 已安装列表（子目录 + 散装 DLL）、详情、打开目录、复制路径
- 安全卸载（重命名为 `.to_be_deleted` + 重启提示 + 失败回滚）
- 配置 / 日志只读查看、打开文件、日志清空（失败提示）
- 修复：重启、清理残留、打开目录
- 纯 WPF 单面板，内联确认，无 `MessageBox`
- 可安装 Tab：先做「打开 Wiki」按钮 + 空态

### Phase 2 — Wiki 缓存目录

- 拉取 raw Markdown，解析 active + legacy
- 本地 JSON 缓存（`{DataRoot}\PluginManager\catalog.json`）
- 默认读缓存；刷新更新；ETag / 24h
- 可安装列表 UI；Legacy 分组；已安装 / 可能已安装标记
- 行操作：打开 repo / releases

### Phase 3 — `.qlplugin` 下载与安装交接

- 按需查询 `releases/latest`
- 资产选择（唯一 `.qlplugin` 优先）
- 下载到 `%TEMP%`，校验 zip + `Metadata.Namespace`
- 资源管理器选中文件，提示用户按空格走官方安装器
- 更新检测：本地 `Metadata.Version` vs release tag
- 失败回退：打开 releases 页

### Phase 4 — 体验增强（可选）

- 版本缓存过期策略（24h）
- 覆盖安装提示、版本对比
- 主配置安全项白名单编辑 + 自动 `.bak`（谨慎）
- 更智能的名称匹配（Namespace / 仓库名 / 目录名）
- 日志搜索、导出
- 可选 GitHub Token（仅当用户明确配置）

---

## 6. 模块划分（建议）

```text
Plugin.cs                     # IViewer：Priority / CanHandle / Prepare / View / Cleanup
Services/
  DataRootResolver.cs         # 反射 + 校验 + 缓存数据根
  InstalledPluginService.cs   # 扫描 / 读取 Metadata / 安全卸载 / 残留清理
  SafeArchive.cs              # zip 校验、zip-slip 防护、读取内嵌 Metadata
  WikiCatalogService.cs       # raw Markdown 拉取 / 解析 / 缓存
  GitHubReleaseService.cs     # latest release / 资产选择 / 下载
  ConfigLogService.cs         # 配置与日志读取 / 清空
  RepairService.cs            # 重启 / unblock / 残留清理
  Loc.cs                      # Translations.config 读取
Models/
  PluginInfo.cs
  CatalogEntry.cs
  ReleaseAsset.cs
Views/
  PluginManagerPanel.xaml(.cs)
```

Cleanup：

- 取消进行中的下载，并删除该次**未完成**的临时文件
- 已经下载完成、已交给用户去按空格的 `.qlplugin` **不要删除**；否则用户按空格时文件可能已经没了
- 下次启动时清理 `%TEMP%\QuickLook.PluginManager\` 下超过 24h 的旧目录
- 取消 `CancellationTokenSource`
- 释放文件句柄 / `HttpClient`
- 不留下后台任务和模态窗口

---

## 7. 工程与依赖

### 7.1 项目基线

照抄 `pagviewer` 的 csproj 结构：

- `TargetFramework`：`net462`
- `UseWPF`：`true`
- `LangVersion`：`latest`
- `RootNamespace` / `AssemblyName`：`QuickLook.Plugin.PluginManager`
- `GenerateAssemblyInfo=false`、`AppendTargetFrameworkToOutputPath=false`
- `EnableDefaultCompileItems=false` 等显式项，按 PagViewer 写法
- `OutputPath`：`bin\Release\`
- `Platforms`：`AnyCPU;x64;ARM64`（按需）
- `QuickLook.Common`：NuGet `4.5.0`（唯一可用版本；不要用 4.6.0 的 `IMoreMenu`）
- JSON：`Newtonsoft.Json 13.0.4` 或 `System.Text.Json 6.0.10`，二选一，保持包体小
- 可选：`UnblockZoneIdentifier 1.0.1`

### 7.2 文件清单

```text
Plugin.cs
PluginManagerPanel.xaml
PluginManagerPanel.xaml.cs
QuickLook.Plugin.PluginManager.csproj
QuickLook.Plugin.PluginManager.sln
QuickLook.Plugin.Metadata.config
QuickLook.Plugin.Metadata.Base.config
Translations.config
Properties/AssemblyInfo.cs
Services/...
Models/...
Scripts/build.ps1
Scripts/pack-zip.ps1
Scripts/update-version.ps1
.github/workflows/build.yml
AGENTS.md
CHANGELOG.md
LICENSE
```

### 7.3 打包与 CI

- `Scripts/build.ps1` 是打包命名的唯一真相源：`$project`、`$pluginFile`
- `Scripts/build.ps1 -SkipBuild -SkipVersion` 用于 CI 只打包
- `.github/workflows/build.yml` 只通过 `env.PLUGIN_NAME` 命名
- `Compress-Archive` 生成的 zip 条目用反斜杠，官方安装器能处理（已实测）
- 发布资产名：`QuickLook.Plugin.PluginManager.qlplugin`
- 残留命名检查：

```powershell
Get-ChildItem -Recurse -File -Include *.ps1,*.yml,*.cs,*.csproj,*.config |
  Select-String "PagViewer"
```

结果必须为 0（文档中的参考链接和 CHANGELOG 历史说明除外）。

### 7.4 Metadata 与版本

```xml
<Metadata>
  <Namespace>QuickLook.Plugin.PluginManager</Namespace>
  <Version>1</Version>
  <Description>Manage QuickLook plugins, configuration and logs.</Description>
</Metadata>
```

- `Version` 是字符串，安装列表原样读取
- 发布时同步更新 `Metadata.config` 的 `Version`（`update-version.ps1` 可扩展为同时写这里）
- `AssemblyInformationalVersion` 可放 git describe，便于诊断

### 7.5 本地化

`Translations.config` 用插件自有的 `Entry / Value / ValueEn` 格式，不是 QuickLook 的 `TranslationHelper` 结构：

```xml
<Translations>
  <Entry Key="Title" Value="QuickLook 插件管理" ValueEn="QuickLook Plugin Manager" />
</Translations>
```

`Loc.cs` 读取当前系统语言，找不到就用 `ValueEn` / `Value` 兜底。

### 7.6 工作区集成

新插件成为受管子仓库后，需要同步：

- `.gitignore` 增加 `/QuickLook.Plugin.PluginManager/`
- `repos.json` 增加条目：`kind: plugin`、`pluginName`、`csproj`
- `AGENTS.md` 目录角色表、构建命令
- `README.md` 仓库清单
- `scripts/build.ps1` 的 `-Plugin` 映射（如 `Manager`）

---

## 8. 验收要点

1. 在数据根、其下任意子文件夹、任意文件上空格 → 均进入管理面板，而不是默认文件夹预览
2. 普通无关目录不误触发；名叫 `QuickLook` 的无关目录也不误触发
3. 便携版 / Store 版能正确定位数据根
4. 已安装列表正确；散装 DLL 不被重复计数；内置插件标记只读
5. 卸载：文件被重命名为 `.to_be_deleted`，不直接删除；重启后残留被清理；失败有回滚和报告
6. 自身卸载按钮禁用；自身更新只打开 Releases
7. `.qlplugin` 始终由官方安装器处理，不被本插件抢走
8. 配置与日志可查看；日志清空失败有明确提示
9. Wiki：首访缓存、再访离线、刷新更新；active / legacy 区分；版本不冒充最新
10. 下载的 `.qlplugin` 通过 zip / Metadata / Namespace 校验；错误资产被拒绝
11. 面板无 `MessageBox`；关闭预览无残留窗口 / 任务
12. 400×200 窗口下仍可操作（滚动 / 折叠）
13. 浅色 / 深色主题下文字与控件可读
14. 构建产物命名正确，残留检查为 0

---

## 9. 风险与对策

| 风险 | 对策 |
|------|------|
| 数据根识别错误 | 反射 `UserPluginPath` 优先；多条件校验；失败返回 false |
| 卸载时 DLL 被占用 | 重命名为 `.to_be_deleted`；失败回滚并报告；重启清理 |
| 误删 / 误装 | 所有写操作内联确认；自身保护；Legacy 警告；下载后校验 Namespace |
| Wiki 表格改版 | 解析失败保留旧缓存；提供打开 Wiki 外链 |
| GitHub API 限流 | 按需查询；按仓库缓存 24h；403 回退 releases 页 |
| 资产误传 / 仓库改名 | 唯一 `.qlplugin` + 下载后 `Metadata.Namespace` 校验 |
| zip-slip | 自写安全解压；目标路径必须在目标目录内；不信任条目名 |
| `MessageBox` 关窗 | 全部改面板内联浮层 |
| Enter / 方向键冲突 | 不依赖键盘；第一版不做搜索框 |
| 日志清空失败 | `FileShare.ReadWrite` 读取；清空失败提示重启后重试 |
| 主题不匹配 | `DynamicResource` + `context.Theme`；关键色 fallback |
| 与官方 5.0 管理 UI 重叠 | 本插件独立、可卸载；职责清晰 |

---

## 10. 参考链接

- Available Plugins Wiki：https://github.com/QL-Win/QuickLook/wiki/Available-Plugins
- Wiki raw Markdown：https://raw.githubusercontent.com/wiki/QL-Win/QuickLook/Available-Plugins.md
- Issue #1916 管理 UI：https://github.com/QL-Win/QuickLook/issues/1916
- Issue #1908 插件清单 / 一键安装：https://github.com/QL-Win/QuickLook/issues/1908
- Issue #1198 插件管理：https://github.com/QL-Win/QuickLook/issues/1198
- 用户插件壳参考：https://github.com/yancongya/QuickLook.Plugin.PagViewer
- 配置说明：https://github.com/QL-Win/QuickLook/blob/master/OPTIONS.md

---

## 11. 给实现 Agent 的一句话

实现一个 `Priority = 100` 的 QuickLook 插件：用反射拿到 `App.UserPluginPath` 并严格识别数据根整棵子树；纯 WPF 提供已安装管理、安全卸载、Wiki 缓存目录、配置 / 日志查看与修复；安装只做「下载 + 校验 + 交给官方 `.qlplugin` 安装器」，不碰自身、不弹 `MessageBox`、不依赖 Enter。
