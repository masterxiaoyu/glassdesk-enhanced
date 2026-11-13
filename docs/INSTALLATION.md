# 安装与更新

GlassDesk 支持 Windows 11 x64（Build 22000 及以上）。
发布包包含运行环境，无需另装 .NET。

## 选择下载文件

| 文件 | 适合的使用方式 |
| --- | --- |
| `GlassDesk-1.6.0-preview.10-win-x64-Setup.exe` | 安装到当前用户目录，后续原位升级 |
| `GlassDesk-1.6.0-preview.10-win-x64-portable.zip` | 解压后运行，便于单独存放或迁移 |

请从本仓库的 [Releases](https://github.com/masterxiaoyu/glassdesk-enhanced/releases)
下载，并按同页提供的 SHA-256 核对文件。

## 安装版

1. 退出正在运行的 GlassDesk，再打开安装程序。
2. 选择安装目录。默认位置为 `%LOCALAPPDATA%\Programs\GlassDesk`。
3. 升级时保留“保留现有设置和应用布局”。
4. 完成后可选择立即运行，再到设置中选择任务栏模式。

安装针对当前 Windows 用户，不要求管理员权限。
“立即运行”只影响本次启动，不会开启登录自启动。
登录自启动由软件设置单独管理。

同一安装目录的后续版本会覆盖程序文件，默认保留 `Data`。
安装器不会从其他便携目录搜集或迁移数据。
若希望恢复初始状态，可在向导中选择“清除用户数据”，并再次确认。
这会删除当前安装目录下的设置、应用库、布局、自定义图标及本地备份。

## 便携版

将 ZIP 完整解压到可写目录，运行 `GlassDesk.exe`。
请勿只从压缩包中拖出 EXE；程序依赖同目录的运行库和资源。
升级前退出程序并备份 `Data`，再覆盖程序文件，保留原 `Data`。

## 卸载

从 Windows“已安装的应用”中卸载 GlassDesk。
卸载时默认保留用户数据，重新安装后可以继续使用原配置。
只有明确选择清除并确认后，安装目录下的 `Data` 才会删除。
静默安装、静默升级和静默卸载始终保留用户数据。

安装器在清除前检查删除范围，遇到目录联接或符号链接时停止清除。
请先自行备份并检查这些目录，不要通过取消检查来完成卸载。

## 通知功能范围

本次公开包不附带测试证书或系统通知组件，也不会修改证书信任。
完整系统通知同步不属于此次可用功能；网络、音量、电池、日历不受影响。

## 维护者构建

公开入口是 `packaging/GlassDesk.iss`，它引用同目录的
`GlassDesk-Capture.iss`。两者共用一套安装、升级和数据处理规则。
固定安装标识为 `GlassDesk.Independent`，与 1.5.1 保持一致。

暂存目录应来自干净的自包含发布，包含许可、来源和第三方声明，
不应含个人 `Data`、日志、租约、私钥或实验通知组件。

```powershell
$env:GLASSDESK_PACKAGE_STAGE = (Resolve-Path artifacts/publish).Path
ISCC.exe /DAppIdValue=GlassDesk.Independent `
  /DProductVersion=1.6.0-preview.10 /DFileVersion=1.6.0.0 `
  /DPackageBaseName=GlassDesk-1.6.0-preview.10-win-x64 `
  /Oartifacts packaging/GlassDesk.iss
```

安装器源码编译已通过。隔离标识和最小载荷的首次安装、覆盖升级保留数据、
静默卸载保留数据已实测通过；此记录不替代最终完整发行包的验证。
交互式安装清除数据、交互式卸载清除数据尚未实测，不应标为已验收。
