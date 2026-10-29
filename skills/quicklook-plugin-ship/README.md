# quicklook-plugin-ship

Agent Skill for developing and shipping QuickLook file preview plugins.

## What is this?

This skill provides a complete workflow for creating, testing, and publishing QuickLook plugins. It's based on real-world experience from building [PagViewer](https://github.com/yancongya/QuickLook.Plugin.PagViewer) and [SvgaViewer](https://github.com/yancongya/QuickLook.Plugin.SvgaViewer) plugins.

## When to use

Use this skill when you want to:
- Develop a QuickLook plugin for a new file format
- Package and publish a `.qlplugin` file
- Submit a plugin to the official [Available Plugins](https://github.com/QL-Win/QuickLook/wiki/Available-Plugins) list
- Follow the same workflow as PagViewer/SvgaViewer for a new format

## Files

| File | Description |
|------|-------------|
| `SKILL.md` | Main skill document with complete workflow |
| `workflow.md` | End-to-end workflow with inputs/outputs/DoD |
| `checklist.md` | Per-phase verification checklist |
| `templates/` | README skeleton, GitHub issue, wiki row, release notes |

## How to use

### In opencode

```bash
# Copy to skills directory
cp -r quicklook-plugin-ship ~/.config/opencode/skills/

# Or reference directly
# "参考 F:/quicklook-plugin-ship/SKILL.md，帮我开发 QuickLook 插件"
```

### Trigger phrases

- "开发 QuickLook 插件"
- "给 XXX 格式做 QuickLook 预览"
- "打包 qlplugin"
- "发布到 QuickLook Available Plugins"
- "复制 PagViewer 流程做新格式插件"

## Key lessons learned

1. **WebView2 config**: Always set `IsZoomControlEnabled = false`
2. **File loading**: Use C# byte injection, not file:// URLs
3. **Canvas scaling**: `object-fit` doesn't work on canvas, use manual calculation
4. **SVGA 1.x**: Need both JSZip AND JSZipUtils
5. **Metadata panel**: Use `pinned` class for highlight, not `active`
6. **i18n**: Use Translations.config, no hardcoded strings
7. **Scaffold naming**: `Scripts/*.ps1`, `build.yml` and `AGENTS.md` all carry the source plugin's name — replace them all (workflow only changes `env.PLUGIN_NAME`) and grep for the old name afterwards; packaging logic lives only in `build.ps1`, never inline in the workflow

## References

- [QuickLook](https://github.com/QL-Win/QuickLook)
- [Available Plugins](https://github.com/QL-Win/QuickLook/wiki/Available-Plugins)
- [Develop, build and integrate](https://github.com/QL-Win/QuickLook/wiki/Develop,-build-and-integrate)
- [HelloWorld template](https://github.com/QL-Win/QuickLook.Plugin.HelloWorld)
- [PagViewer](https://github.com/yancongya/QuickLook.Plugin.PagViewer)
- [SvgaViewer](https://github.com/yancongya/QuickLook.Plugin.SvgaViewer)

## License

MIT
