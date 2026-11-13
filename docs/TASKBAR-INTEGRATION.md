# 任务栏集成开发预览

> 此文记录早期底部任务栏集成实验。当前布局已调整为 macOS 风格 Dock
> 加可选顶部栏，实际使用请查看 [当前功能说明](MAC-DESKTOP.md)。

基于 1.5.1 的本地开发版本，尚未作为新正式版本发布。

## 使用

1. 正常退出其他 GlassDesk 实例，再运行预览目录中的 `GlassDesk.exe`。
2. Dock 左侧 Windows 图标打开系统开始菜单。
3. 固定应用后方显示其他运行窗口；单击切换，前台窗口再次单击最小化。
   右键可切换、最小化和请求关闭；关闭确认仍由原应用处理。
4. 右侧上箭头打开后台应用图标，支持单击、双击和右键菜单。
   网络／音量按钮调用 Windows 快捷设置；时间按钮打开通知和日历。
5. Dock 右键进入设置，Dock 页可切换运行窗口、系统入口和原生任务栏隐藏。
   原生任务栏默认保留，确认所需应用的图标都可用后再勾选隐藏。
6. Dock 空白处或内置按钮右键可直接恢复原生任务栏或退出 GlassDesk。
   正常退出也会恢复任务栏可见性及接管前的工作区。

预览程序使用自身目录旁的 `Data`，不自动迁移安装版数据。
临时窗口不进入 `dock-layout.json`，固定后由原有固定项承接，避免重复显示。

## 实现边界

- 延用 C#／WPF／.NET 10，不更换项目技术栈。
- 窗口枚举、应用身份匹配和激活复用原有应用服务。
  新增标题变化监听，临时窗口按首次出现的顺序排列。
- 托盘桥接收 Shell 图标注册消息，复制图标像素，并把注册消息继续转发给
  Explorer；应用回调按版本 3／4 协议投递。图标面板打开时跟随注册变化更新。
- 开始接管和退出时发送标准 `TaskbarCreated` 通知。
  没有处理该通知的旧应用需要重新启动，才能重新提交已有图标。
- 托盘接收协议包含 Windows 私有消息格式，目前实测系统为
  Windows 11 Build 26300；其他 Windows 构建仍需兼容性验收。
- 隐藏原生任务栏保留 Explorer，使用窗口可见性和 `SPI_SETWORKAREA`
  临时释放任务栏工作区，不修改自动隐藏偏好。主／副任务栏均纳入接管，
  多显示器实际交互仍待验收。
- 本机 `ABM_SETSTATE` 返回成功但未改变自动隐藏状态；隐藏功能不依赖它。
  原有“桌面”页的系统自动隐藏开关仍按系统读回结果报告。
- 正常退出恢复已验证；强制结束进程或系统崩溃不在自动恢复保证内。
  尚未增加独立守护进程。异常退出后可在任务管理器重启 Windows 资源管理器。

## 验证

使用本机已有 SDK 10.0.401 和 NuGet 缓存。
命令行关闭在线 NuGet 审计仅用于离线构建，没有修改仓库审计设置。
WPF／Shell 测试需要可交互会话；受限会话无法连接测试宿主。

- Dock 完整回归：566 项通过。
- AppServices 完整回归：391 项通过。
- Core 完整回归：178 项通过。
- 真实 Shell 图标的新增、修改、删除、GUID、隐藏状态和版本 3／4 回调通过。
- 独立测试进程的窗口激活、最小化、标题更新、固定去重、已有托盘图标
  重新注册和双击回调通过。
- 原生任务栏隐藏、工作区释放、退出恢复及自动隐藏偏好保持不变通过。

仍待桌面逐项点击验收：开始菜单、快捷设置、通知日历、常用第三方软件的
托盘右键菜单、不同 DPI 和多显示器。桌面控制工具的应用访问授权曾超时，
当前测试结果不能替代这些人工交互验收。

## 协议参考

- [Microsoft：通知区域](https://learn.microsoft.com/en-us/windows/win32/shell/notification-area)
- [Microsoft：SystemParametersInfo](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfow)
- [ManagedShell：托盘消息服务实现](https://github.com/cairoshell/ManagedShell/blob/master/src/ManagedShell.WindowsTray/TrayService.cs)
- [ManagedShell：托盘输入版本处理](https://github.com/cairoshell/ManagedShell/blob/master/src/ManagedShell.WindowsTray/NotifyIcon.cs)

未引入 ManagedShell 依赖；参考其公开协议说明，自行实现消息桥及集成测试。
