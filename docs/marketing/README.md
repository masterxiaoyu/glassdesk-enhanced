# GlassDesk 图文素材

`images/01-desktop.png` 至 `05-settings.png` 是 1080×1920 竖版图片，
按文件顺序发布即可。`github-hero.png` 是仓库首页的横版封面。
发布正文和口播见 [DOUYIN-COPY.md](DOUYIN-COPY.md)。

图片使用当前代码导出的真实 WPF 控件，应用列表、日期、音量是展示数据。
没有采集个人桌面、聊天、路径或通知。背景与场景框架由离线网页排版，
Dock 使用程序自带的兼容底板，并非桌面实时折射截图。
应用图标只作使用示例，不代表合作、授权背书或内置分发这些软件。
图片没有使用云端生图，不重绘软件按钮或编造未实现的功能。

## 重新导出

在可交互 Windows 会话中构建 Dock 测试项目，再显式执行素材导出：

```powershell
$env:GLASSDESK_MARKETING_EXPORT = '1'
dotnet vstest tests/GlassDesk.Dock.Tests/bin/Release/net10.0-windows10.0.26100.0/GlassDesk.Dock.Tests.dll '/Framework:.NETCoreApp,Version=v10.0' '/TestCaseFilter:FullyQualifiedName~MarketingPreviewTests'
Remove-Item Env:GLASSDESK_MARKETING_EXPORT
node tools/marketing/render-posters.cjs
```

导出测试只使用隔离演示数据。请先退出正在运行的 GlassDesk，
并使用与项目一致的 .NET 10 主机。
排版脚本使用本机 Chrome；可通过 `GLASSDESK_CHROME` 指定已有浏览器路径，
不会下载浏览器或字体。图片完成后应检查文字、裁切和功能描述，再发布。
