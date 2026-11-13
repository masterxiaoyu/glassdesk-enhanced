# GlassDesk

给 Windows 换一套桌面布局：常用应用放进悬浮 Dock，
同类软件拖到一起收纳，网络、音量和电量留在顶部菜单栏。

**Windows 11 x64 · 免费使用 · 当前预览版 1.6.0-preview.10**

[下载安装版](https://github.com/masterxiaoyu/glassdesk-enhanced/releases/download/v1.6.0-preview.10/GlassDesk-1.6.0-preview.10-win-x64-Setup.exe) ·
[下载便携版](https://github.com/masterxiaoyu/glassdesk-enhanced/releases/download/v1.6.0-preview.10/GlassDesk-1.6.0-preview.10-win-x64-portable.zip) ·
[版本说明](https://github.com/masterxiaoyu/glassdesk-enhanced/releases/tag/v1.6.0-preview.10)




## 常用应用，放在手边

- **悬浮 Dock**：固定常用软件，查看运行中的应用；支持拖拽排序、
  悬停放大和自动隐藏。
- **直接拖叠收纳**：把一个应用拖到另一个应用上方，停留后松手即可收纳。
  点击展开，同类工具放在一起。
- **启动台**：集中浏览与搜索应用，按需调整网格行列和文字亮度。
- **顶部菜单栏**：查看当前应用与系统状态，分别打开网络、音量、电池、
  日历和控制中心。应用菜单由当前软件提供。
- **两种任务栏模式**：可选 macOS 风格菜单栏，也可保留 Windows 任务栏。
  切换模式或正常退出后，恢复对应的桌面工作区。
- **可选窗口切换快捷键**：开启后，单独按下并松开 Win 键即可选择窗口。
  Win+D、Win+E 等组合键继续使用。

接通电源时，电池图标显示闪电；使用电池时显示电量条。
有线连接与 Wi-Fi 共用一个网络入口，有线连接优先显示。
常用后台图标可以从收纳面板拖到菜单栏常驻，再按需要拖回。

## 开始使用

1. 下载上方安装版，或将便携版完整解压到可写文件夹。
2. 运行 `GlassDesk.exe`，把常用应用拖入 Dock。
3. 右键 Dock 打开“设置”，在“Dock → 任务栏模式”选择喜欢的布局。
4. 在“外观”中调整透明度、图标与文字亮度；在“快捷操作”中设置快捷键。

使用安装版更新时，请先退出正在运行的 GlassDesk。
设置、应用库和布局保存在程序目录下的 `Data` 文件夹，更新前建议备份。
分发包不包含个人数据，不会自动迁移其他目录中的旧便携版数据。

## 当前预览版的范围

本版仍为预发布，适合先体验，再决定是否用于日常桌面。

- 支持 Windows 11 x64，最低系统版本为 Build 22000。
  不同系统版本、缩放比例与显卡驱动下的表现可能不同。
- 顶部应用菜单仅显示软件实际提供的菜单。
  使用自绘菜单的软件，仍需在其窗口内操作。
- **完整系统通知同步暂未开放**，不属于本次公开包的可用功能。
  音量、电池、网络和日历不受影响。
- 本版安装程序尚未进行受信任的代码签名，Windows 可能显示安全提示。
  请从本仓库的 Release 页面下载，并核对随包提供的 SHA-256。

问题反馈请附上 Windows 版本、显示缩放比例和复现步骤，
提交到 [Issues](https://github.com/masterxiaoyu/glassdesk-enhanced/issues)。

## 来源、许可与赞助

本项目由独立维护者基于 **everlosttime 的 GlassDesk 1.4.0 公开源码**修改，
并非原作者发布的官方版本。

[原产品](https://github.com/everlosttime/glassdesk) ·
[原公开源码](https://github.com/everlosttime/glassdesk-open-source) ·
[源码来源记录](SOURCE-PROVENANCE.json) ·
[本版修改说明](LOCAL-MODIFICATIONS.md)

软件全部功能免费，赞助完全自愿。软件内展示的支付宝、微信收款码属于
本版维护者，与原作者无关。

合资格的原创新增内容和修改按 **PolyForm Noncommercial 1.0.0** 提供，
允许许可范围内的学习、修改和非商业分享；历史 Apache-2.0 代码与第三方
组件保留各自许可。该许可不属于 OSI 标准开源许可。
具体范围见 [LICENSE](LICENSE)、[授权说明](docs/LICENSING.md) 与
[NOTICE](NOTICE)。分发时请保留许可及第三方声明。

## 开发与构建

构建环境、测试命令和打包方式见 [构建说明](docs/BUILDING.md)。
参与修改前请阅读 [贡献指南](CONTRIBUTING.md) 与
[发布政策](packaging/RELEASE-POLICY.md)。
