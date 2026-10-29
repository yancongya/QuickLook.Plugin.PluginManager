# QuickLook 插件上线验收清单

> 每个阶段完成后，逐项勾选。未全部通过不可进入下一阶段。

---

## 阶段 0：立项

- [ ] 官方插件列表中无同格式插件（或确认要覆盖的原因）
- [ ] 有可用的 Web SDK / 原生渲染方案
- [ ] 确认 Priority 值（覆盖内置用 100，默认 0）
- [ ] 确认许可证（推荐 GPL-3.0）

---

## 阶段 1：脚手架

- [ ] 项目目录结构正确
- [ ] 命名空间统一为 `QuickLook.Plugin.{Format}`
- [ ] 程序集名为 `QuickLook.Plugin.{Format}.dll`
- [ ] `.csproj` 目标框架为 `net462`
- [ ] `dotnet build -c Release` 无错误
- [ ] 输出目录有 `.dll` 文件
- [ ] `Translations.config` 存在且有中英翻译
- [ ] `QuickLook.Plugin.Metadata.config` 描述正确
- [ ] 脚本/CI 命名已替换（build.ps1、pack-zip.ps1、update-version.ps1、build.yml 的 `PLUGIN_NAME`、AGENTS.md）
- [ ] `Scripts/*.ps1` 为 UTF-8 with BOM
- [ ] 残留检查通过：脚本/workflow/源码中 `PagViewer` 为 0（skills/ 模板与参考链接除外）

---

## 阶段 2：核心实现

### Plugin.cs
- [ ] `CanHandle` 检查扩展名
- [ ] `CanHandle` 检查文件头魔字节
- [ ] `CanHandle` 对目录返回 false
- [ ] 格式若带官方布局/对位元数据，优先解析直送渲染器（像素自检只做兜底）
- [ ] `Prepare` 设置合理的 PreferredSize
- [ ] `View` 创建面板并设置 Content
- [ ] `View` 设置 `IsBusy = false`
- [ ] `Cleanup` 释放资源

### WebView2 面板
- [ ] WebView2 初始化成功
- [ ] `IsZoomControlEnabled = false`
- [ ] 使用 `SetVirtualHostNameToFolderMapping` 加载资源
- [ ] `NavigationStarting` 拦截外部导航
- [ ] `WebMessageReceived` 处理消息
- [ ] `Dispose` 释放所有事件和资源
- [ ] `IsWebView2Available` 检测 + 下载按钮回退

### 文件加载
- [ ] C# 读取文件字节
- [ ] 转 base64 传给 WebView
- [ ] JS 端接收并转换为 ArrayBuffer
- [ ] 不使用 file:// 直链

### 播放器 (player.html)
- [ ] SDK 初始化成功
- [ ] 格式解析成功
- [ ] 渲染正常显示
- [ ] 错误状态有提示（不空白）
- [ ] 播放控制正常（如适用）

### 主题与 i18n
- [ ] 暗色主题显示正常
- [ ] 亮色主题显示正常
- [ ] 中文翻译正确
- [ ] 英文翻译正确

---

## 阶段 3：本地验证

### 正常场景
- [ ] 小文件预览正常
- [ ] 中等文件预览正常
- [ ] 大文件预览正常
- [ ] 官方示例文件预览正常

### 异常场景
- [ ] 空文件（0 字节）→ 显示错误提示
- [ ] 坏文件（随机字节）→ 显示错误提示
- [ ] 文件名含中文 → 正常处理
- [ ] 文件名含空格 → 正常处理
- [ ] 文件名含特殊字符 → 正常处理

### 稳定性
- [ ] 快速连续预览 → 无残留
- [ ] 预览中切换文件 → 正常切换
- [ ] 关闭预览 → WebView2 正确释放
- [ ] 无内存泄漏

---

## 阶段 4：优化体验

### UI 对齐
- [ ] 控制栏高度 32px
- [ ] 使用 Segoe Fluent Icons
- [ ] 自动隐藏（鼠标移动显示，1.5s 后淡出）
- [ ] 进度条细线 + 悬停 Thumb
- [ ] 播放/暂停按钮图标切换
- [ ] 循环按钮状态切换

### 功能完善
- [ ] 音量控制（按钮 + 滚轮）
- [ ] 进度条拖拽
- [ ] 保存为 PNG
- [ ] 复制到剪贴板
- [ ] 背景切换（如适用）

### 错误处理
- [ ] SDK 加载失败有提示
- [ ] 格式解析失败有提示
- [ ] WebView2 不可用有下载按钮

---

## 阶段 5：工程化发布

### 构建
- [ ] `dotnet build -c Release` 成功
- [ ] `Scripts/build.ps1` 能生成 .qlplugin，资产名为 `QuickLook.Plugin.{Format}.qlplugin`
- [ ] .qlplugin 文件大小合理（<50MB）

### 版本
- [ ] `update-version.ps1` 能正确更新版本号
- [ ] `update-version.ps1` 对无 tag 仓库回退 1.0.0（不把 `git describe --always` 的 commit hash 当版本号）
- [ ] Git tag 格式为 `v{major}.{minor}.{patch}`

### GitHub
- [ ] 推 tag 后 Actions `Build and Release` 全绿
- [ ] CI 打包走 `build.ps1 -SkipBuild -SkipVersion`（workflow 无内联打包逻辑）
- [ ] Release 页面有 .qlplugin 资产且**命名正确**（非模板源插件名）
- [ ] Release 有说明文字（generate_release_notes）
- [ ] publish 权限为 `contents: write`（非 write-all）

### 文档
- [ ] README 包含中文简介
- [ ] README 包含英文简介
- [ ] README 包含功能列表
- [ ] README 包含安装步骤
- [ ] README 包含系统要求
- [ ] README 包含从源码构建
- [ ] README 包含快捷键
- [ ] README 包含项目结构
- [ ] README 包含 Credits 节
- [ ] CHANGELOG 采用统一版本节格式（见 SKILL.md）
- [ ] LICENSE 文件存在（GPL-3.0）

---

## 阶段 6：官方曝光

- [ ] 在 QL-Win/QuickLook 创建 Issue
- [ ] Issue 标题格式正确
- [ ] Issue 包含插件名、Release 链接、描述
- [ ] Issue 包含 Wiki 行格式（可直接复制）
- [ ] Wiki 行格式与官方一致

---

## 最终验收

- [ ] 用户能从 Release 下载 .qlplugin
- [ ] 用户能安装并预览目标格式
- [ ] 异常文件不导致崩溃
- [ ] 官方 Wiki 已添加插件条目（或 Issue 已创建）
