# 构建与打包

## 环境

- Windows 11 x64（Build 22000 及以上）。
- .NET 10 SDK。`global.json` 指定 10.0.401，允许 `latestFeature` 滚动。
- 目标 Windows SDK：10.0.26100.0。
- 生成安装 EXE 需要 Inno Setup 7。

沿用现有 .NET/WPF 项目和本机缓存，不需要额外的前端工具链。

## 编译与运行

```powershell
dotnet build src/GlassDesk.App/GlassDesk.App.csproj -c Release
dotnet run --project src/GlassDesk.App/GlassDesk.App.csproj -c Release
```

`Data` 保存在实际运行目录旁。构建输出使用该输出目录的数据；
运行源码构建版前，请先退出其他 GlassDesk 实例。

## 测试

```powershell
dotnet test tests/GlassDesk.Core.Tests/GlassDesk.Core.Tests.csproj -c Release
dotnet test tests/GlassDesk.AppServices.Tests/GlassDesk.AppServices.Tests.csproj -c Release
dotnet test tests/GlassDesk.Dock.Tests/GlassDesk.Dock.Tests.csproj -c Release
```

AppServices 测试使用 Windows COM 和外壳能力；Dock 测试创建 WPF 窗口，
需要可交互的 Windows 会话。不要在有未保存工作的桌面上运行会切换窗口、
隐藏任务栏或更改系统状态的交互验收。

若系统 PATH 中的 `dotnet` 版本与本项目不一致，应显式指定 .NET 10 主机，
并让测试子进程使用同一主机：

```powershell
$env:DOTNET_HOST_PATH = '<.NET 10 安装目录>\dotnet.exe'
$env:GLASSDESK_DOTNET = $env:DOTNET_HOST_PATH
& $env:DOTNET_HOST_PATH vstest tests/GlassDesk.Dock.Tests/bin/Release/net10.0-windows10.0.26100.0/GlassDesk.Dock.Tests.dll '/Framework:.NETCoreApp,Version=v10.0'
```

编译成功不代表所有 Windows 版本、显示器和显卡组合均已验证。
当前版本的验收范围见 [更新说明](../packaging/RELEASE-NOTES-1.6.0-preview.10.md)。

## 生成分发包

本版本可使用统一发布脚本生成安装版、便携版和校验清单：

```powershell
pwsh -File build/public-release.ps1 `
  -DotnetPath '<.NET 10 安装目录>\dotnet.exe' `
  -InnoCompilerPath '<Inno Setup 7 安装目录>\ISCC.exe'
```

脚本从新的发布目录打包，不包含用户数据、测试证书或实验通知组件。

当前公开版本为 `1.6.0-preview.10`，安装程序与便携版应使用一致版本。
发布包不包含实验通知组件、测试证书、签名私钥或个人 `Data`。

```powershell
dotnet publish src/GlassDesk.App/GlassDesk.App.csproj -c Release -r win-x64 --self-contained true -p:Version=1.6.0-preview.10 -p:FileVersion=1.6.0.0 -p:InformationalVersion=1.6.0-preview.10 -p:IncludeSourceRevisionInInformationalVersion=false -o artifacts/publish
```

在干净暂存目录中准备发布文件，并保留以下资料：

- `LICENSE.txt`、`NOTICE.txt`。
- `LICENSING.md`、第三方声明与逐文件来源记录。
- `README.md`、`LOCAL-MODIFICATIONS.md`、`SOURCE-PROVENANCE.json`。
- 当前版本更新说明、安装说明与发布政策。

压缩前核对暂存目录，排除 `Data`、日志、租约文件、测试素材、
证书及私钥。便携版文件名为：

```text
GlassDesk-1.6.0-preview.10-win-x64-portable.zip
```

安装版沿用 `GlassDesk.Independent` 安装标识与原安装路径，
以便后续原位更新。安装和卸载的数据处理应遵循
[发布政策](../packaging/RELEASE-POLICY.md)，并对本次实际验证范围作记录。

安装器入口与编译参数由本版本的打包流程确定；不要直接套用历史候选包
`packaging/release.json` 中的旧版本号、安装标识或私有暂存路径。
每次发布同时生成 SHA-256 清单。

## 着色器

`TransitionGlass.hlsl` 与预编译的 `TransitionGlass.ps` 位于
`src/GlassDesk.UI/Effects`。修改着色器后执行
`build/compile-transition-shader.ps1`，再重新编译项目。
