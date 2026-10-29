# QuickLook 插件开发工作流

> 端到端流程，从立项到上线。每一步都有明确的输入/输出和验收标准。

## 流程总览

```
[0. 立项] → [1. 脚手架] → [2. 核心实现] → [3. 本地验证]
                                                    ↓
[6. 官方曝光] ← [5. 工程化发布] ← [4. 优化体验]
```

---

## Step 0: 立项与边界（30 分钟）

### 输入
- 用户想要预览的格式名称
- 该格式的 Web SDK 或原生库信息

### 动作
1. 搜索官方插件列表，确认不已有支持
2. 搜索 QuickLook Issues，确认无重复请求
3. 确认格式的 Web SDK 存在且可用
4. 决定 Priority 策略

### 输出
- 格式名称、扩展名、文件头魔字节
- Web SDK 名称和版本
- Priority 值

### 验收
- [ ] 官方无同格式插件（或确认要覆盖的原因）
- [ ] 有可用的渲染方案

---

## Step 1: 脚手架搭建（1 小时）

### 输入
- 格式名称
- PagViewer 项目作为模板

### 动作
1. 创建项目目录
2. 从 PagViewer 复制通用文件
3. 全局替换命名空间
4. 重命名文件
5. **替换脚本/CI 中的插件名**：`Scripts/build.ps1`（`$project`/`$pluginFile`）、
   `Scripts/pack-zip.ps1`（资产名）、`Scripts/update-version.ps1`（程序集信息）、
   `.github/workflows/build.yml`（仅 `env.PLUGIN_NAME`）、`AGENTS.md`（描述 + CI/Release 节）
6. **残留检查**：`Get-ChildItem -Recurse -File -Include *.ps1,*.yml,*.md,*.cs,*.csproj,*.config | Select-String PagViewer`
   —— 脚本/workflow/源码中必须为 0（`skills/` 模板与参考链接除外）
7. 下载格式 Web SDK 到 `Resources/Web/`
8. 更新 csproj 依赖
9. 验证编译通过

### 输出
- 可编译的空壳项目
- `dotnet build -c Release` 成功

### 验收
- [ ] 命名空间统一为 `QuickLook.Plugin.{Format}`
- [ ] 程序集名为 `QuickLook.Plugin.{Format}.dll`
- [ ] 脚本/CI 命名替换完成，残留检查为 0
- [ ] 编译无错误

### 命令
```bash
# 验证编译
dotnet build -c Release

# 验证输出
ls bin/Release/QuickLook.Plugin.{Format}.dll
```

---

## Step 2: 核心实现（2-4 小时）

### 输入
- 可编译的空壳项目
- 格式 Web SDK 文档

### 动作
1. 实现 `Plugin.cs` 的 `CanHandle`（扩展名 + 文件头）
2. 实现 `{Format}Panel.xaml` 布局
3. 实现 `{Format}Panel.xaml.cs`：
   - WebView2 初始化
   - 文件加载（C# 读字节 → base64 → JS 接收）
   - 消息通信（C# ↔ WebView）
   - 主题传递
   - 资源释放
4. 实现 `player.html`：
   - SDK 初始化
   - 格式解析和渲染
   - 播放控制（如适用）
   - 错误提示
5. 实现 i18n（Translations.config + C# → JS 传递）

### 输出
- 能在 QuickLook 中预览目标格式

### 验收
- [ ] `CanHandle` 只匹配目标格式
- [ ] 预览正常显示
- [ ] 控制栏功能正常
- [ ] 关闭后无残留进程

### 关键代码模式

**CanHandle 必须检查文件头：**
```csharp
public bool CanHandle(string path)
{
    if (Directory.Exists(path)) return false;
    if (!path.EndsWith(".ext", StringComparison.OrdinalIgnoreCase)) return false;
    try
    {
        using (var fs = File.OpenRead(path))
        {
            if (fs.Length < 4) return false;
            var header = new byte[MAGIC_LENGTH];
            fs.Read(header, 0, MAGIC_LENGTH);
            return MatchesMagic(header);
        }
    }
    catch { return false; }
}
```

**文件加载必须用字节注入：**
```csharp
// 错误：直接传 file:// 路径
// 正确：读字节 → base64 → postMessage
var bytes = File.ReadAllBytes(path);
var base64 = Convert.ToBase64String(bytes);
await webView.ExecuteScriptAsync($"window.postMessage({{type:'load',data:'{base64}'}}, '*')");
```

---

## Step 3: 本地验证（1 小时）

### 输入
- 能预览的插件
- 测试用例文件

### 动作
1. 准备测试文件：
   - 正常文件（小/中/大）
   - 空文件（0 字节）
   - 坏文件（随机字节）
   - 文件名含中文/空格
   - 官方示例文件（如有）
2. 浏览器验证：直接打开 player.html 测试
3. 打包 .qlplugin：`powershell -File Scripts/build.ps1`
4. QuickLook 安装测试
5. 边界场景测试

### 输出
- 所有测试场景通过

### 验收
- [ ] 正常文件预览正常
- [ ] 空文件显示错误提示
- [ ] 坏文件显示错误提示
- [ ] 大文件能加载
- [ ] 文件名含中文正常
- [ ] 快速连续预览无残留

---

## Step 4: 优化体验（1-2 小时）

### 输入
- 基本可用的插件

### 动作
1. UI 对齐官方风格：
   - 控制栏高度 32px
   - Segoe Fluent Icons
   - 自动隐藏（1.5s 淡出）
   - 进度条细线样式
2. 主题适配（暗色/亮色）
3. 错误状态优化
4. 性能优化（如适用）
5. 窗口尺寸持久化

### 输出
- UI 与官方插件风格一致

### 验收
- [ ] 暗色主题下显示正常
- [ ] 亮色主题下显示正常
- [ ] 错误状态有明确提示
- [ ] 控制栏自动隐藏正常

---

## Step 5: 工程化发布（1 小时）

### 输入
- 完整可用的插件

### 动作
1. 更新版本号：`powershell -File Scripts/update-version.ps1`
2. 构建：`dotnet build -c Release`
3. 本地打包验证：`powershell -File Scripts/build.ps1`（确认 `.qlplugin` 资产名为 `QuickLook.Plugin.{Format}.qlplugin`）
4. 更新 README 和 CHANGELOG
5. 创建 Git tag：`git tag v1.0.0`
6. 推送：`git push origin master --follow-tags`
7. 验证 GitHub Actions `Build and Release` 全绿（push/PR 只构建，tag 触发 publish）
8. 验证 Release 资产名正确且唯一（`fail_on_unmatched_files` 兜底）
9. 若有落地页，确认 latest release 抓取正常（按扩展名识别，无需随 CI 改名）

### 输出
- GitHub Release 有 `.qlplugin` 下载
- README 中英双语完整

### 验收
- [ ] Release 页面有 `.qlplugin` 文件，资产名为 `QuickLook.Plugin.{Format}.qlplugin`
- [ ] Actions 构建全绿（CI 打包走 `build.ps1 -SkipBuild -SkipVersion`，无内联逻辑）
- [ ] README 包含：简介、功能、安装、构建、快捷键、项目结构
- [ ] CHANGELOG 有版本记录

---

## Step 6: 官方曝光（30 分钟）

### 输入
- GitHub Release 链接

### 动作
1. 在 `QL-Win/QuickLook` 创建 Issue
2. 标题：`Add {FormatName} plugin to Available Plugins list`
3. 内容包含：插件名、Release 链接、一句话描述、Wiki 行格式
4. 等待维护者添加到 Wiki

### 输出
- 官方仓库有登记 Issue

### 验收
- [ ] Issue 包含完整信息
- [ ] Wiki 行格式正确（可直接复制）

---

## 时间估算

| 阶段 | 预估时间 |
|------|---------|
| 0. 立项 | 30 分钟 |
| 1. 脚手架 | 1 小时 |
| 2. 核心实现 | 2-4 小时 |
| 3. 本地验证 | 1 小时 |
| 4. 优化体验 | 1-2 小时 |
| 5. 工程化发布 | 1 小时 |
| 6. 官方曝光 | 30 分钟 |
| **总计** | **7-10 小时** |
