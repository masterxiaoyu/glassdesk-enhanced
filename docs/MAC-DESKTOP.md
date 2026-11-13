# macOS 风格 Dock 与任务栏模式

这是现有 GlassDesk 中的功能更新，不是独立的新程序。

## 使用

- 底部 Dock 保留应用启动、固定、拖拽排序和悬停放大；采用圆角矩形玻璃面板，
  与工作区边缘留出 12 DIP 悬浮间距。
- “此电脑”使用显示器图标，打开 Windows 此电脑。右侧废纸篓打开回收站。
- 将固定应用拖到另一应用图标中心，停留约 0.4 秒再松手即可收纳；
  边缘拖放仍用于排序。也可以把启动台应用拖到 Dock 图标中心收纳。
  点击收纳展开 32 DIP 小图标，右键可“展开到 Dock”或移除。
  旧版工具栏分组会保留为收纳磁贴，无须“新建聚合”。
- 同一未固定应用的多个运行窗口合并成一枚图标，点击可选择窗口。
- 在 Dock 右键 → 设置 → Dock → 任务栏模式，选择 Windows 原生任务栏
  或 macOS 风格顶部任务栏。两者互斥，默认使用 Windows。
  顶部栏模式隐藏原生任务栏，切回 Windows 后释放顶部空间并恢复原生栏。
- 顶部栏高度为 36 DIP，显示当前应用、应用实际提供的原生菜单和后台图标。
  网络展开独立连接状态面板，音量展开主音量滑块与静音控制，日期展开月历；
  控制中心和通知分别调用各自的 Windows 系统入口。弹层切换时收起前一个。
- 顶部栏左侧 Windows 图标直接打开系统开始菜单，右键打开 GlassDesk 菜单。
  当前应用名优先读取产品名称，例如 Codex 不再被旧文件名 ChatGPT.exe 误标。
  切到桌面或系统界面显示 Windows，切到本程序窗口显示 GlassDesk。
- 顶部栏直接贴屏幕最上沿，不避让隐藏原生栏的旧占位。
  后台托盘面板使用 16 DIP 图标、28 DIP 点击区域。
  小按钮不继承通用内边距，避免图标被挤出圆角区域而裁切。

## 菜单和兼容范围

应用菜单来自 Win32 `GetMenu`，菜单命令回传给原应用。
使用自绘菜单的程序（部分 Electron、WPF、现代 Windows 应用）不会出现通用菜单，
仍在应用自己的窗口内操作；不提供没有实际动作的“文件／编辑”等占位项。

顶部栏通过 AppBar 预留工作区；隐藏原生栏时只补足已有占位以外的厚度，
栏本身始终贴屏幕上沿。这样加高栏后，Explorer 重算也不会覆盖它的空间。
相同布局只提交一次；定时检查仅处理重新出现的原生栏，避免反复修改工作区。
托盘桥与 AppBar 请求分别路由，避免共享内存消息被错误转发。
全屏抑制／游戏休眠时顶部栏跟随收起，恢复后重新显示。

关闭顶部栏、恢复 Windows 任务栏和退出程序分别归还自己占用的区域。
正常退出恢复可见性和工作区；强制结束进程不保证即时恢复，必要时在任务管理器
重启 Windows 资源管理器。

预览版使用自身目录下的 Data，不自动迁移已安装版本的数据。
本机测试系统为 Windows 11 Build 26300，其他 Windows 构建及多显示器仍需验收。

## 构建

沿用 README 中的 .NET 10／WPF 构建方式。
本机已有 SDK 与 NuGet 缓存，无须新增工具链。

```powershell
dotnet build src/GlassDesk.App/GlassDesk.App.csproj -c Release
dotnet test tests/GlassDesk.Dock.Tests/GlassDesk.Dock.Tests.csproj -c Release
```

顶部栏相关测试覆盖：模式保存与旧配置归一、反复开关、工作区恢复、
连续刷新不再广播工作区变更、应用产品名与前台切换、三种 DPI 的图标完整像素、
独立弹层切换与音频接口释放、系统主音量读写原值、
真实 Win32 菜单状态及命令执行。

开始按钮调用系统开始入口，菜单和顶部搜索框保留 Windows 原样。
系统浮层的进程归属随 Windows 版本变化，不能仅用 SearchHost 名称判定为搜索面板。
按钮实际弹出效果仍需在用户桌面点击确认；自动测试不对系统浮层内容作断言。

独立音量面板使用系统默认播放设备，不引入新依赖；接口见
[Microsoft EndpointVolume API](https://learn.microsoft.com/en-us/windows/win32/coreaudio/endpointvolume-api)。

本机交互式 Shell 回归使用 VSTest 直接运行，并显式指定已有 .NET 10 主机：

```powershell
$env:DOTNET_HOST_PATH = "<.NET 10 安装目录>\dotnet.exe"
$env:GLASSDESK_DOTNET = $env:DOTNET_HOST_PATH
& $env:DOTNET_HOST_PATH vstest tests/GlassDesk.Dock.Tests/bin/Release/net10.0-windows10.0.26100.0/GlassDesk.Dock.Tests.dll
```

子进程需要同一个 .NET 10 主机，不能回落到系统 PATH 中的旧版 dotnet。

## preview.5 菜单栏整理

点击顶部右侧的“•••”打开后台图标收纳。将图标拖到菜单栏右侧即可常驻，
在栏内拖动可调整顺序。将常驻图标拖到“•••”，等待收纳面板展开后放入，
即可重新收纳。顺序保存到 `Data/dock-layout.json` 的 `TopBarItemIds`；
优先使用应用的托盘 GUID，否则使用进程路径和图标 ID，避免重启后窗口句柄变化。

网络入口始终只有一个：有线连接优先，其次无线，最后离线。
连接状态变化会刷新图标，网络面板和控制中心使用相同状态。
顶部栏、音量、日历和控制中心统一使用深色圆角面板与细线图标；
控制中心独立提供网络、声音、显示桌面和设置入口。
左侧 Windows 图标继续调用系统开始菜单。

Dock 不再显示系统 `SystemApps` 目录中的 `TextInputHost.exe`、
`InputApp.exe` 输入宿主窗口，旧的固定入口也不展示。
这里只过滤 Dock 和前台应用名称，不结束进程，应用库记录保留。

新增测试覆盖托盘拖入、排序、拖回、保存恢复及失败回滚，
有线与无线组合优先级、输入宿主过滤范围，以及独立控制中心和日历日期。

## preview.6 单击 Win 切换窗口

在“设置 → 快捷操作”开启“单击 Win 键打开窗口切换”。
单独按下并松开左 Win 或右 Win，显示系统窗口缩略图；
用鼠标或方向键选择，Enter 确认，Esc 取消。
长按重复只在最终松开时触发一次。
Win+D、Win+E、Win+Tab、Win+Space 等组合键继续交给系统；
顶部栏 Windows 图标继续打开开始菜单。
关闭开关或退出 GlassDesk 后，单击 Win 恢复系统行为。
通用配置默认关闭，此次用户预览配置已开启。

使用系统自带的 Ctrl+Alt+Tab 入口，无额外界面或依赖；
快捷键行为见 [Microsoft Windows 快捷键说明](https://support.microsoft.com/en-us/accessibility/windows/keyboard-shortcuts-in-windows)。
输入重放受 Windows 前台权限限制，管理员程序或安全桌面需单独验收。
自动测试覆盖左右 Win、按键重复、组合键顺序、重叠按键及设置保存回滚；
自动桌面工具禁止模拟 Win 键，因此实际键盘触发需在用户桌面实按确认。

## preview.7 图标调整

移除托盘收纳面板顶部的拖放提示文字及其留白。
网络图标改为圆角网口、居中无线弧线和离线状态，沿用单入口及有线优先规则。

## preview.8 独立系统状态面板

系统音量、网络和电池图标从应用托盘列表中移除，使用顶部栏独立入口。
电池面板显示系统电量、充电状态、节能状态及可用的续航估计；无电池设备不显示电池按钮。
有线网络采用显示器轮廓，无线采用 Wi-Fi 图标，有线连接仍优先显示。
通知按钮打开 GlassDesk 通知中心，日历继续使用独立月历。
设置文案统一采用亮度、缩放比例、逻辑像素等术语，删除开发过程说明。

完整系统通知通过 `UserNotificationListener` 获取，不读取 Windows 通知数据库。
需先安装应用标识组件，再由用户点击通知面板中的“授权通知访问…”完成系统授权。
授权前不读取通知；拒绝或撤销权限后显示授权入口，不冒充“暂无通知”。
通知内容仅保存在当前界面内存中，定时刷新仅在面板展开时进行；清除操作同步到系统通知列表。
通知监听能力与授权要求见
[Microsoft 通知侦听器说明](https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/notification-listener)。

通知组件采用外部位置应用包，保留既有便携数据路径与安装方式；
安装及卸载分别调用 `GlassDesk.exe --install-notification-component` 与
`GlassDesk.exe --uninstall-notification-component`，不依赖 PowerShell 脚本执行策略。
安装版通过 Inno Setup 的 `NotificationIdentity` 编译开关包含组件安装步骤。
安装失败会显示错误，不自动修改系统策略或证书信任。

本地预览组件使用 CN=GlassDesk Preview 的测试证书签名，属于本机测试包，
不作为公开正式分发包。测试证书由用户手动信任，程序和安装器不修改系统证书信任。正式分发需要受信任签名；
参考 [Microsoft 应用包签名说明](https://learn.microsoft.com/windows/msix/package/sign-msix-package-guide)。
组件包可使用已有 Windows SDK 离线构建：

```powershell
# 将 packaging/notification_identity/AppxManifest.xml 和
# src/GlassDesk.App/Assets/GlassDeskBrand.png 放入临时目录，
# 图片对应 Assets/GlassDesk.png；包内不放程序和 Data。
MakeAppx.exe pack /o /nv /d <组件暂存目录> /p GlassDesk.Identity.msix
```

组件包放在 GlassDesk.exe 同目录，外部程序清单与包标识必须一致。
源码中的测试包标识为 `GlassDesk.Desktop.Preview`，仅注册此标识。


本机验证发现 unsigned marker 的发布者不能用于当前 Windows 的并行清单 msix 元素；
因此预览改用正常发布者名称与测试签名，不能将无签名包标记写入应用清单。

## preview.9 系统图标与启动台

根据本机 Shell 实际注册记录补齐音量 `7820ae73` 与电源 `7820ae75`
图标标识，收纳区与常驻区共用过滤规则；安全删除硬件等其他图标保留。
电池按钮接通电源时显示闪电，电池供电时显示电量条，充满仍按供电来源区分。
启动台使用石墨灰渐变底板与居中圆角九宫格，缩小图案并减细空心描边；
原有实心／空心、亮度和尺寸设置继续生效。
本次不安装通知组件或修改证书信任，通知同步保持待授权状态。
