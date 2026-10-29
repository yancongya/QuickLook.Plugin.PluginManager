---
name: quicklook-plugin-ship
description: |
  从零开发并上线一个 QuickLook 文件预览插件。触发条件：
  - 用户说"开发 QuickLook 插件"
  - 用户说"给 XXX 格式做 QuickLook 预览"
  - 用户说"打包 qlplugin"
  - 用户说"发布到 QuickLook Available Plugins"
  - 用户说"复制 PagViewer 流程做新格式插件"
  - 用户提到 QuickLook + 任意文件格式
---

# QuickLook 插件开发与上线 Skill

> 基于 PagViewer 真实项目经验提炼。适用于任意新格式预览插件（WebView2 + Web SDK 方案）。

## 前置判断

开始前必须回答以下问题：

```
Q1: 官方是否已内置支持该格式？
    → 是：检查是否只是兼容性 bug，考虑 PR 修复而非新建插件
    → 否：继续

Q2: 该格式是否有可用的 Web SDK（WASM/JS）？
    → 是：使用 WebView2 方案（本 Skill）
    → 否：考虑 P/Invoke 原生方案或 SkiaSharp 方案

Q3: 是否需要覆盖内置预览？
    → 是：设置 Priority > 0
    → 否：Priority = 0
```

## 项目命名规范

```
仓库名：QuickLook.Plugin.{FormatName}
命名空间：QuickLook.Plugin.{FormatName}
程序集：QuickLook.Plugin.{FormatName}.dll
面板类：{FormatName}Panel
```

示例：`QuickLook.Plugin.SvgaViewer`、`QuickLook.Plugin.GifViewer`

## 阶段 0：立项与边界

### 决策清单

- [ ] 确认官方不支持（或有 bug 需绕过）
- [ ] 确认有可用的 Web SDK 或原生渲染方案
- [ ] 确认 Priority 策略（0 = 默认，>0 = 覆盖内置）
- [ ] 确认许可证兼容（推荐 GPL-3.0，与 QuickLook 一致）

### Priority 策略

```csharp
// 官方不支持 → 默认优先级
public int Priority => 0;

// 官方已支持但有 bug → 覆盖内置
// 官方内置插件 Priority 分布在 int.MinValue ~ 11（ImageViewer = 0，VideoViewer = -3），
// 要可靠覆盖取 100（AlphaVideoViewer 覆盖内置 VideoViewer 的先例）；
// SvgaViewer 用 10 覆盖 ImageViewer 也可行，但 100 更稳妥，不受官方数值变动影响
public int Priority => 100;

// 仅作为备选方案
public int Priority => -1;
```

## 阶段 1：脚手架

### 从 PagViewer 复制的文件（通用骨架）

```
QuickLook.Plugin.{Format}/
├── Plugin.cs                           # 改命名空间和类名
├── {Format}Panel.xaml                  # 改 x:Class
├── {Format}Panel.xaml.cs               # 改命名空间和类名
├── Native/                             # P/Invoke 桩（如需要）
├── Resources/Web/                      # Web SDK + 播放器 HTML
├── Properties/AssemblyInfo.cs          # 改程序集信息
├── Scripts/
│   ├── build.ps1                       # 改 $project/$pluginFile 命名（真相源）
│   ├── pack-zip.ps1                    # 改输出资产名（3 处）
│   └── update-version.ps1              # 改 AssemblyTitle/Product 等
├── .github/
│   ├── workflows/build.yml             # 改 env.PLUGIN_NAME（唯一命名改点）
│   ├── ISSUE_TEMPLATE/                 # 通用
│   └── PULL_REQUEST_TEMPLATE.md        # 通用
├── Translations.config                 # 按格式补充翻译
├── QuickLook.Plugin.Metadata.config    # 改描述
├── QuickLook.Plugin.Metadata.Base.config
├── QuickLook.Plugin.{Format}.csproj    # 改包名和依赖
├── QuickLook.Plugin.{Format}.sln
├── AGENTS.md                           # 改项目描述 + CI/Release 节资产名
├── README.md                           # 重写
├── CHANGELOG.md
├── LICENSE                             # GPL-3.0
├── .gitignore
└── .gitattributes
```

### csproj 关键配置

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <TargetFramework>net462</TargetFramework>
    <RootNamespace>QuickLook.Plugin.{Format}</RootNamespace>
    <AssemblyName>QuickLook.Plugin.{Format}</AssemblyName>
    <Platforms>AnyCPU;x64</Platforms>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <LangVersion>latest</LangVersion>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
    <DebugType>full</DebugType>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <EnableDefaultPageItems>false</EnableDefaultPageItems>
    <EnableDefaultEmbeddedResourceItems>false</EnableDefaultEmbeddedResourceItems>
  </PropertyGroup>
  <!-- 配置、Content、Compile、PackageReference... -->
</Project>
```

### 命名空间迁移步骤

1. 全局替换 `QuickLook.Plugin.PagViewer` → `QuickLook.Plugin.{Format}`
2. 重命名 `.csproj` 和 `.sln` 文件
3. 重命名 XAML 的 `x:Class`
4. 更新 `AssemblyInfo.cs`
5. 更新 `QuickLook.Plugin.Metadata.config`
6. 改脚本与 CI 中的**插件名**（模板从 PagViewer 复制时全部是源插件名，漏改即产生错名 Release）：
   - `Scripts/build.ps1`：`$project`、`$pluginFile`
   - `Scripts/pack-zip.ps1`：输出 `.zip`/`.qlplugin` 资产名（3 处）
   - `Scripts/update-version.ps1`：`AssemblyTitle`/`AssemblyProduct`
   - `.github/workflows/build.yml`：`env.PLUGIN_NAME`（workflow 内其余命名均引用它，只改这一处）
   - `AGENTS.md`：项目描述与 `## CI / Release` 节中的资产名
7. **残留检查（不可跳过）**：

```powershell
Get-ChildItem -Recurse -File -Include *.ps1,*.yml,*.yaml,*.md,*.cs,*.xaml,*.csproj,*.config |
  Select-String -Pattern 'PagViewer'
# 期望 0 结果；skills/ 内嵌模板与 README Credits/参考链接中的历史提及除外
```

## 阶段 2：核心实现

### IViewer 最小实现

```csharp
public class Plugin : IViewer
{
    public int Priority => 0;

    public void Init() { }

    public bool CanHandle(string path)
    {
        if (Directory.Exists(path)) return false;
        if (!path.EndsWith(".ext", StringComparison.OrdinalIgnoreCase)) return false;
        // 验证文件头魔数字节
        try
        {
            using (var fs = File.OpenRead(path))
            {
                if (fs.Length < 4) return false;
                var header = new byte[3];
                fs.Read(header, 0, 3);
                return header[0] == 'X' && header[1] == 'Y' && header[2] == 'Z';
            }
        }
        catch { return false; }
    }

    public void Prepare(string path, ContextObject context)
    {
        context.SetPreferredSizeFit(new Size(600, 400), 0.9);
    }

    public void View(string path, ContextObject context)
    {
        var panel = new FormatPanel();
        panel.LoadFile(path);
        context.ViewerContent = panel;
        context.Title = Path.GetFileName(path);
        panel.Dispatcher.Invoke(() => context.IsBusy = false,
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void Cleanup()
    {
        GC.SuppressFinalize(this);
    }
}
```

### WebView2 面板核心模式

```csharp
public partial class FormatPanel : UserControl
{
    private WebView2 _webView;
    private string _webAssetsDir;
    private string _filePath;
    private Window _hostWindow;

    // Win32 窗口拖拽
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    public FormatPanel()
    {
        _webAssetsDir = Path.Combine(
            Path.GetDirectoryName(typeof(FormatPanel).Assembly.Location) ?? "",
            "Resources", "Web");
        if (!IsWebView2Available()) { Content = CreateDownloadButton(); return; }
        InitializeComponent();
        _webView = WebView;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var env = await CoreWebView2Environment.CreateAsync(null,
            Path.Combine(Path.GetTempPath(), "{Format}_WebView2"));
        await _webView.EnsureCoreWebView2Async(env);
        _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        _webView.CoreWebView2.NavigationStarting += OnNavigationStarting;
        _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
        _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "assets.local", _webAssetsDir, CoreWebView2HostResourceAccessKind.Allow);
        _webView.CoreWebView2.Navigate(Path.Combine(_webAssetsDir, "player.html"));
        _hostWindow = Window.GetWindow(this);
    }

    // 关键：拦截外部导航
    private void OnNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (e.Uri.StartsWith("data:")) return;
        if (e.Uri.StartsWith("https://assets.local/")) return;
        if (e.Uri.StartsWith("file:")) return;
        e.Cancel = true;
    }

    // 关键：完整 Dispose
    public void Dispose()
    {
        if (_webView != null)
        {
            if (_webView.CoreWebView2 != null)
            {
                _webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
                _webView.CoreWebView2.NavigationStarting -= OnNavigationStarting;
            }
            _webView.Dispose();
            _webView = null;
        }
    }
}
```

### 文件加载模式（避免 file:// 安全错误）

```csharp
// C# 端：读取字节，转 base64，传给 WebView
private async Task SendFileToWebView()
{
    var bytes = File.ReadAllBytes(_filePath);
    var base64 = Convert.ToBase64String(bytes);
    var msg = JsonSerializer.Serialize(new
    {
        type = "loadFile",
        playerSrc = "https://assets.local/player.js",
        base64 = base64
    });
    await _webView.CoreWebView2.ExecuteScriptAsync(
        "window.postMessage(" + msg + ", '*');");
}
```

```javascript
// JS 端：接收 base64，转 ArrayBuffer
window.addEventListener('message', function(e) {
    var msg = e.data;
    if (msg.type === 'loadFile') {
        var raw = atob(msg.base64);
        var buf = new Uint8Array(raw.length);
        for (var i = 0; i < raw.length; i++) buf[i] = raw.charCodeAt(i);
        var ab = buf.buffer.slice(buf.byteOffset, buf.byteOffset + buf.byteLength);
        // 用 ab 加载格式...
    }
});
```

### 主题传递

```csharp
private async Task ApplyTheme()
{
    var bg = _theme == Themes.Light ? "#ffffff" : "#202020";
    var fg = _theme == Themes.Light ? "#1a1a1a" : "#cccccc";
    await _webView.CoreWebView2.ExecuteScriptAsync($@"
        document.documentElement.style.setProperty('--bg', '{bg}');
        document.documentElement.style.setProperty('--fg', '{fg}');
    ");
}
```

### i18n 传递

```csharp
private string LoadTranslations()
{
    var doc = new XmlDocument();
    doc.Load(Path.Combine(exeDir, "Translations.config"));
    // 返回 JSON: {"KEY": {"zh": "...", "en": "..."}}
}

// 在 ApplyTheme 中传递
await _webView.CoreWebView2.ExecuteScriptAsync($@"
    window.__lang = '{lang}';
    window.__translations = {translations};
    if (typeof applyI18n === 'function') applyI18n();
");
```

## 阶段 3：本地验证

### 验证顺序（不可跳过）

1. **浏览器验证**：先在 Chrome 中打开 player.html，确认格式能正常渲染
2. **WebView2 验证**：安装 .qlplugin 后在 QuickLook 中测试
3. **样例回归**：用官方样例 + 自建样例 + 坏文件测试

### 必测场景

| 场景 | 预期行为 |
|------|---------|
| 正常文件 | 能预览，控制栏正常 |
| 空文件 (0 字节) | 显示错误提示，不崩溃 |
| 坏文件（随机字节） | 显示错误提示，不崩溃 |
| 超大文件 (>100MB) | 能加载，不 OOM |
| 文件名含中文/空格 | 正常处理 |
| 快速连续预览 | 不残留旧实例 |
| 关闭预览窗口 | WebView2 正确释放 |

### WebView2 典型错误排查

| 错误 | 原因 | 修复 |
|------|------|------|
| `file:` URLs are treated as unique security origins | 直接用 file:// 加载 | 改用 virtual host mapping |
| `Cannot set properties of undefined` | DOM 元素未找到 | 检查元素 ID 和脚本执行顺序 |
| 空白页面 | JS 报错或资源路径错 | F12 查看 Console |
| `Initialize data type error` | API 传参类型错 | 检查 SDK 文档要求的类型 |

## 阶段 4：优化体验

### UI 对齐官方风格

参考 VideoViewer / ImageViewer：
- 控制栏高度 32px
- Segoe Fluent Icons 字体
- 自动隐藏（鼠标移动显示，1.5s 后淡出）
- 进度条细线 + 悬停显示 Thumb
- DynamicResource 主题色

### 错误处理

```javascript
// 每个关键步骤都要有状态提示
statusEl.textContent = 'Loading SDK...';
try {
    // ...
    statusEl.textContent = 'Decoding...';
    // ...
} catch(err) {
    statusEl.textContent = 'Error: ' + err.message;
}
```

### 性能监控（可选）

```javascript
// 在 onAnimationUpdate 中获取 debugData
var debug = pagView.getDebugData();
// FPS 显示在左上角
// 进度条颜色反映渲染压力
```

## 阶段 5：工程化发布

### 构建命令

```bash
# 构建
dotnet build -c Release

# 打包
powershell -ExecutionPolicy Bypass -File Scripts/build.ps1

# 更新版本号
powershell -ExecutionPolicy Bypass -File Scripts/update-version.ps1
```

### GitHub Release

```bash
git tag v1.0.0
git push origin master --follow-tags
# GitHub Actions 自动构建并创建 Release
```

### CI/CD 规范（workflow 与 build.ps1 的分工）

**打包与命名的唯一真相源是 `Scripts/build.ps1`。** workflow 的 Package 步只允许：

```yaml
- name: Package
  shell: pwsh
  run: powershell -ExecutionPolicy Bypass -File Scripts/build.ps1 -SkipBuild -SkipVersion
```

禁止在 workflow 内联第二份打包/命名逻辑——SvgaViewer 曾因 workflow 从 PagViewer 复制，
发布出错名资产 `QuickLook.Plugin.PagViewer.qlplugin`。

workflow 关键要素（四个插件仓已按此规范化；PluginManager 为 WPF 原生面板，无 WebView2/Web 资产）：

| 要素 | 约定 |
|------|------|
| `env.PLUGIN_NAME` | `QuickLook.Plugin.{Format}`，artifact 名/资产名的唯一改点 |
| `permissions` | 顶层 `contents: read`；publish job `contents: write`（不写 `write-all`） |
| `concurrency` | 同 ref 只保留最新构建；`v*` tag 构建绝不取消（`cancel-in-progress: ${{ !startsWith(github.ref, 'refs/tags/') }}`） |
| upload-artifact | `if-no-files-found: error`、`retention-days: 14` |
| action-gh-release | `fail_on_unmatched_files: true`、`generate_release_notes: true` |
| publish job | 仅 `if: startsWith(github.ref, 'refs/tags/')`；用 download-artifact 取回 |

触发模型：push `master` / PR / 手动 dispatch 只做构建校验（不发版）；推 `v*` tag 才
构建 + 创建 Release。改 CI 只改 workflow，不改触发模型。

配套约定：

- `AGENTS.md` 必须含 `## CI / Release` 节，与 workflow 使用同一资产名（命名改点变了要同步）
- `Scripts/*.ps1` 一律存 **UTF-8 with BOM**（PowerShell 5.1 读中文注释必需；四个插件仓已统一）
- `update-version.ps1` 必须校验 `git describe` 输出格式：**仓库还没有任何 tag 时 `--always` 会回退打印 commit hash**，直接当 `AssemblyVersion` 写入会 CS7034 编译失败。四个插件仓已统一为 `^v?\d+(\.\d+){1,3}$`，不合法回退 `1.0.0`（hash 只进 InformationalVersion）
- Release 说明由 workflow 的 `generate_release_notes: true` 从提交历史自动生成，无需手写；**提交信息本身就是 Release 说明的素材**，保持一行一条、说清用户可感知的变化
- 落地页从 `releases/latest` 按 `.qlplugin` **扩展名**抓资产，workflow/资产改名不破坏落地页
- 首次推 tag 前先在本地确认 `Scripts/build.ps1` 能产出正确名的 `.qlplugin`

### README 必备结构（中英双语）

标题下方第一行放同系列插件互链（blockquote 一行，中英通用）：

```
> 同系列插件 / More plugins: [插件A](仓库A) · [插件B](仓库B) · ...
```

结构：

```
# 插件名
## 中文
- 简介
- 功能特性
- 安装步骤
- 系统要求
- 从源码构建
- 快捷键
- 项目结构
## English
- (同上英文版)
## Changelog
## Credits
## License
```

### CHANGELOG 格式

统一用简单版本节（四个插件仓一致；**不**采用 Keep a Changelog 的前言与长段散文）：

```markdown
# Changelog

## [Unreleased]

## [1.0.0] - 2026-08-25

### Added
- 一行一个变化，写用户可感知的行为

### Changed
- ...

### Fixed
- ...
```

- 未发布内容放 `## [Unreleased]`，发版时改成 `## [X.Y.Z] - YYYY-MM-DD` 并新建空的 Unreleased 节
- **打 tag 前必须完成这次转换**（checklist 阶段 5 强制项）——内容挂在 Unreleased 却发了版，CHANGELOG 就和 Release 对不上
- 节名只用 `Added` / `Changed` / `Fixed` / `Removed`

## 阶段 6：官方曝光

### 创建登记 Issue

仓库：`QL-Win/QuickLook`
标题：`Add {FormatName} plugin to Available Plugins list`

```markdown
## Description
I'd like to add my {Format} viewer plugin to the Available Plugins list.

**Plugin:** [{FormatName}](https://github.com/{user}/{repo}) v1.0.0
**Release:** https://github.com/{user}/{repo}/releases
**Description:** Preview {Format} files using {technology}.

### Wiki Entry
| [{FormatName}](https://github.com/{user}/{repo}) v1.0.0 | {date} | [Link](https://github.com/{user}/{repo}/releases) | Preview {Format} files. |
```

### Wiki 表格行格式

官方 Available-Plugins 活跃列表是**裸行**（无 `|` 前缀、无表头对齐），追加到列表末尾，描述建议中英双语：

```
[{FormatName}](https://github.com/{user}/{repo}) v{version} | {YYYY-MM-DD} | [Link](https://github.com/{user}/{repo}/releases) | {一句话描述} / {中文描述}
```

## 完成定义（DoD）

### 阶段 0 DoD
- [ ] 确认官方不支持（或有明确 bug 需绕过）
- [ ] 确认技术方案（WebView2 / 原生）

### 阶段 1 DoD
- [ ] 项目能编译（`dotnet build -c Release` 无错误）
- [ ] 命名空间、程序集名、文件名符合规范
- [ ] 脚本/CI 命名已替换（build.ps1、pack-zip.ps1、update-version.ps1、build.yml 的 `PLUGIN_NAME`、AGENTS.md）
- [ ] 残留检查通过（`Select-String PagViewer` 在脚本/workflow/源码中为 0）

### 阶段 2 DoD
- [ ] `CanHandle` 能识别目标格式（扩展名 + 文件头）
- [ ] `View` 能显示预览面板
- [ ] WebView2 能加载并渲染格式内容

### 阶段 3 DoD
- [ ] 浏览器中能正常渲染
- [ ] QuickLook 中能正常预览
- [ ] 坏文件/空文件不崩溃

### 阶段 4 DoD
- [ ] UI 风格与官方插件一致
- [ ] 错误状态有明确提示
- [ ] 主题适配（暗色/亮色）

### 阶段 5 DoD
- [ ] 有 `.qlplugin` 产物且资产名为 `QuickLook.Plugin.{Format}.qlplugin`
- [ ] GitHub Release 有资产（tag 推送后 Actions 全绿，publish 未报 unmatched files）
- [ ] CI 打包走 `build.ps1 -SkipBuild -SkipVersion`（workflow 无内联打包逻辑）
- [ ] README 中英双语完整

### 阶段 6 DoD
- [ ] 官方仓库有登记 Issue
- [ ] Wiki 行格式正确，可直接复制

## 参考链接

- 官方插件列表：https://github.com/QL-Win/QuickLook/wiki/Available-Plugins
- 开发文档：https://github.com/QL-Win/QuickLook/wiki/Develop,-build-and-integrate
- HelloWorld 模板：https://github.com/QL-Win/QuickLook.Plugin.HelloWorld
- PagViewer 参考：https://github.com/yancongya/QuickLook.Plugin.PagViewer
- SvgaViewer 参考：https://github.com/yancongya/QuickLook.Plugin.SvgaViewer
- AlphaVideoViewer：工作区本地仓库（远端待建），Priority=100 覆盖内置 VideoViewer、Media Foundation 解码像素自检的先例
- PluginManager：工作区本地仓库（远端待建），WPF 原生面板（无 WebView2/Web 资产）插件的先例——SKILL 的 WebView2 方案之外的另一条路
- IViewer 接口：https://github.com/QL-Win/QuickLook.Common/blob/master/Plugin/IViewer.cs

## 实战经验（PagViewer + SvgaViewer + AlphaVideoViewer 总结）

### 覆盖内置预览插件的先例（AlphaVideoViewer）

```csharp
// 1. Priority 取 100，稳超官方内置全集（int.MinValue ~ 11，VideoViewer = -3）
public int Priority => 100;

// 2. CanHandle 做双向判定，不抢不属于自己的文件：
//    - 确认为普通文件（解码像素自检：两半都是彩色画面）→ return false，交回内置 VideoViewer
//    - 命中目标特征、或无法定论（解码器缺失/单色歧义/解码失败）→ return true 接管（宁漏勿抢）
// 3. CanHandle 的探测结果在 View 复用前缓存（60s TTL），避免重复解码
```

### 元数据优先，像素兜底（AlphaVideoViewer / 腾讯 VAP 先例）

若格式容器携带官方对位/布局元数据（自定义盒、sidecar 字段等），**优先解析元数据直送渲染器，
像素统计只做无元数据素材的兜底**——VAP 的 `vapc` 顶级盒里写着 `rgbFrame`/`aFrame` 精确坐标时，
任何"半区 + 象限"网格拟合都是放着标准答案不抄、自己在猜。

- 一次顶层扫描顺手抓元数据盒（它可能出现在 `moov` 之前，如 ftyp→vapc→free→moov→mdat），不要分两次开文件
- 元数据命中即定案，跳过像素自检（像素自检是给没有元数据的压平素材用的）
- 元数据坏了（坏 JSON / 缺字段 / 坐标越界）静默回退几何/像素路径，宁可多走兜底也不因脏数据误接管
- 归一化矩形连同输出尺寸一起下发前端，合成器只负责"两个任意源矩形"，对位决策留在探测层

### WebView2 关键配置

```csharp
// 必须禁用 WebView2 自带缩放
_webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

// 使用 virtual host 加载本地资源（避免 file:// 安全问题）
_webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
    "assets.local", _webAssetsDir, CoreWebView2HostResourceAccessKind.Allow);

// 拦截外部导航
_webView.CoreWebView2.NavigationStarting += (s, e) => {
    if (!e.Uri.StartsWith("https://assets.local/") && !e.Uri.StartsWith("file:"))
        e.Cancel = true;
};
```

### 文件加载最佳实践

```csharp
// C# 端：读取字节 → base64 → 传给 WebView
var bytes = File.ReadAllBytes(path);
var base64 = Convert.ToBase64String(bytes);
var msg = JsonSerializer.Serialize(new { type = "loadFile", base64 = base64 });
await _webView.CoreWebView2.ExecuteScriptAsync("window.postMessage(" + msg + ", '*');");
```

```javascript
// JS 端：接收 base64 → 转 ArrayBuffer/Blob
var raw = atob(base64);
var buf = new Uint8Array(raw.length);
for (var i = 0; i < raw.length; i++) buf[i] = raw.charCodeAt(i);
// 根据 SDK 要求选择传递方式：
// 1. ArrayBuffer: buf.buffer.slice(buf.byteOffset, buf.byteOffset + buf.byteLength)
// 2. Blob URL: URL.createObjectURL(new Blob([buf]))
// 3. Data URI: 'data:svga/1.0;base64,' + base64
```

### Canvas 缩放陷阱

```css
/* ❌ object-fit 对 canvas 不生效 */
#canvas { max-width: 100%; max-height: 100%; object-fit: contain; }

/* ✅ 需要手动计算缩放 */
function fitCanvasToContainer() {
    var scaleX = container.clientWidth / canvas.width;
    var scaleY = container.clientHeight / canvas.height;
    var scale = Math.min(scaleX, scaleY);
    canvas.style.width = Math.floor(canvas.width * scale) + 'px';
    canvas.style.height = Math.floor(canvas.height * scale) + 'px';
}
```

### SVGA 格式特殊处理

```javascript
// 1.x 和 2.x 属性名不同
var fps = videoItem.FPS || videoItem.fps || 20;           // 1.x: FPS, 2.x: fps
var size = videoItem.videoSize || videoItem.size || {};   // 1.x: videoSize, 2.x: size

// 1.x 需要 JSZip + JSZipUtils（两个都需要！）
<script src="jszip.min.js"></script>
<script src="jszip-utils.min.js"></script>  <!-- 必须！SVGAPlayer-Web 检测两个全局变量 -->
<script src="svga.min.js"></script>

// 1.x 使用 data URI 格式
var dataUri = 'data:svga/1.0;base64,' + base64;
parser.load(dataUri, success, failure);

// 2.x 图片是 Uint8Array，不是 base64 字符串
if (typeof imgData === 'string') {
    imgSize = imgData.length * 0.75;  // 1.x base64
} else if (imgData instanceof Uint8Array) {
    imgSize = imgData.length;          // 2.x binary
    base64Data = btoa(String.fromCharCode.apply(null, imgData));
}
```

### 元数据面板最佳实践

```javascript
// 点击固定 + 高亮状态（使用 pinned 类，不要用 active）
var metaPinned = false;
btnMeta.addEventListener('click', function(e) {
    e.stopPropagation();
    metaPinned = !metaPinned;
    btnMeta.classList.toggle('pinned', metaPinned);
    // ...
});

// 自定义滚动条
#metaPanel::-webkit-scrollbar { width: 4px; }
#metaPanel::-webkit-scrollbar-track { background: transparent; }
#metaPanel::-webkit-scrollbar-thumb { background: rgba(255,255,255,0.2); border-radius: 2px; }
```

### 双语支持

```xml
<!-- Metadata.config: 单字段双语 -->
<Description>Preview PAG files. 预览 PAG 动画文件。</Description>

<!-- Translations.config: 中英分开 -->
<Entry Key="BTN_Play" Value="播放" ValueEn="Play" />
```

### 常见错误清单

| 错误 | 原因 | 修复 |
|------|------|------|
| `file:` URLs are treated as unique security origins | 直接用 file:// 加载 | 改用 virtual host mapping |
| `Cannot set properties of undefined` | DOM 元素未找到 | 检查元素 ID 和脚本执行顺序 |
| `incorrect header check` | zlib 解压失败 | 检查文件格式，使用正确的 data URI 前缀 |
| Canvas 空白 | object-fit 不生效 | 手动计算 style.width/height |
| 图片不显示 | 1.x/2.x 图片格式不同 | 检查是 string 还是 Uint8Array |
| WebView2 缩放干扰 | Ctrl+滚轮触发 WebView2 缩放 | 设置 IsZoomControlEnabled = false |
- ContextObject：https://github.com/QL-Win/QuickLook.Common/blob/master/Plugin/ContextObject.cs
