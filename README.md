# GlassDesk 1.5.0（非官方修改版）

> 本版基于原作者 everlosttime 公开的 GlassDesk 1.4.0 源码，
> 由本版维护者独立修改，并非原作者发布的官方版本。
> [原产品](https://github.com/everlosttime/glassdesk) ·
> [原公开源码](https://github.com/everlosttime/glassdesk-open-source) ·
> [本地修改说明](LOCAL-MODIFICATIONS.md) ·
> [1.5.0 更新说明](packaging/RELEASE-NOTES-1.5.0.md)
> [本修改版仓库](https://github.com/masterxiaoyu/glassdesk-enhanced)

Windows 11 x64 桌面美化工具：玻璃 Dock、启动台、文件夹和统一设置中心。

底座是**最后一个原本不含右上角信息栏的正式版本 1.4.0** 的完整产品源码快照。此修改版未合入原产品 1.4.1 及后续版本的信息栏或其他改动。

[源码下载](https://github.com/everlosttime/glassdesk-open-source/releases/tag/v1.4.0) · [1.4.0 更新说明](docs/Release-notes-v1.4.0.md) · [参与贡献](CONTRIBUTING.md)

## 非官方发布与版本

本版首个正式版本为 **1.5.0**。兼容修复使用 `1.5.x`，新增功能提升次版本（如 `1.6.0`），破坏兼容的改动提升主版本（如 `2.0.0`）。安装包、便携版与程序文件应使用同一版本号；发布规则与安装数据处理见 [发布政策](packaging/RELEASE-POLICY.md)。

软件免费，赞助完全自愿，不影响功能。本版内展示的微信、支付宝收款码属于本版维护者，**不代表原作者收款**。软件按现状提供，Windows 版本、显卡驱动和系统设置可能影响视觉效果或任务栏功能；未签名安装包可能出现 Windows 安全提示。下载时请核对发布来源及 SHA-256，安装或升级前备份 `Data`。

## 授权：允许非商业修改和分享

本次合资格的原创新增内容和修改按 **PolyForm Noncommercial License 1.0.0** 提供，允许许可范围内学习、修改、修复、Fork 和非商业分享；不授予将受限内容拿去售卖、用于商业项目或包装成收费软件的权利。标准许可对部分机构用途有明确允许条款，以 LICENSE 英文原文为准。

**历史 Apache-2.0 代码和第三方组件保留原有权利。** 删除旧版本的公开入口不会撤回旧授权；本次非商用限制不能追溯套用到那些内容。沿用与新增内容的范围见 [授权说明](docs/LICENSING.md) 和 [1.4.0 快照逐文件来源记录](docs/licensing/FILE-LICENSES.json)。完整条款见 [LICENSE](LICENSE)，第三方声明见 [NOTICE](NOTICE) 和 [docs/third-party](docs/third-party)。

本仓库采用“非商业源码公开”的方式协作，新许可不属于 OSI 标准开源许可。本衍生仓库从原公开源码快照发展；原项目及其他 Fork 的历史和授权仍以各自仓库为准。

## 包含的功能

- 玻璃 Dock、启动台、应用拖拽整理与文件夹创建/重命名/排序。
- Dock 软件聚合工具栏：自建分组、从应用库选择或导入软件、从分组启动。
- 外观设置、Dock 自动隐藏、桌面入口和当前用户登录启动。
- Windows 任务栏自动隐藏开关、当前位置和透明效果状态，以及系统设置入口。
- 本版维护者的自愿赞助入口；软件全部功能免费，不赞助也可使用。

原始快照的产品源码、资源和测试来自 1.4.0 正式标签；本工作树的新增与修改见
[本地修改说明](LOCAL-MODIFICATIONS.md)。原始快照来源见
[SOURCE-PROVENANCE.json](SOURCE-PROVENANCE.json)。仓库不包含私有开发历史、
本机用户数据、日志、截图验收记录或已编译程序。

## 编译与运行

需要 Windows 11 x64（Build 22000 及以上）和 .NET 10 SDK。global.json 指定 10.0.401，允许 latestFeature 滚动；目标 Windows SDK 为 10.0.26100.0。

```powershell
dotnet build src/GlassDesk.App/GlassDesk.App.csproj -c Release
dotnet run --project src/GlassDesk.App/GlassDesk.App.csproj -c Release
```

程序将 Data 保存在实际运行目录旁。源码构建输出使用该输出目录的数据；正式程序按用户会话维持单实例，运行开发版前请从托盘退出其他 GlassDesk 实例。

```powershell
dotnet test tests/GlassDesk.Core.Tests/GlassDesk.Core.Tests.csproj -c Release
dotnet test tests/GlassDesk.AppServices.Tests/GlassDesk.AppServices.Tests.csproj -c Release
dotnet test tests/GlassDesk.Dock.Tests/GlassDesk.Dock.Tests.csproj -c Release
```

AppServices 测试使用 Windows COM/外壳能力；Dock 测试创建 WPF 窗口，需可交互的 Windows 会话。历史验收工程与正式项目可能存在目标框架差异，请先按以上 App 项目构建和这三个测试工程验证。需要隔离桌面的鼠标键盘验收应在专门测试环境运行，不能将编译通过视为所有设备已验证。

## 生成便携版与安装包

```powershell
dotnet publish src/GlassDesk.App/GlassDesk.App.csproj -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -p:Version=1.5.0 -p:FileVersion=1.5.0.0 -p:InformationalVersion=1.5.0 -p:IncludeSourceRevisionInInformationalVersion=false -o artifacts/publish
Copy-Item LICENSE artifacts/publish/LICENSE.txt
Copy-Item NOTICE artifacts/publish/NOTICE.txt
Copy-Item docs/third-party artifacts/publish/third-party -Recurse
Copy-Item docs/LICENSING.md artifacts/publish/LICENSING.md
Copy-Item docs/licensing artifacts/publish/licensing -Recurse
Copy-Item README.md artifacts/publish/README.md
Copy-Item LOCAL-MODIFICATIONS.md artifacts/publish/LOCAL-MODIFICATIONS.md
Copy-Item SOURCE-PROVENANCE.json artifacts/publish/SOURCE-PROVENANCE.json
Copy-Item packaging/INSTALLER-NOTICE.txt artifacts/publish/INSTALLER-NOTICE.txt
Copy-Item packaging/RELEASE-POLICY.md artifacts/publish/RELEASE-POLICY.md
Copy-Item packaging/RELEASE-NOTES-1.5.0.md artifacts/publish/RELEASE-NOTES.md
Compress-Archive -Path artifacts/publish/* -DestinationPath artifacts/GlassDesk-1.5.0-win-x64-portable.zip -Force
```

`artifacts/publish/GlassDesk.exe` 可直接运行。不要把个人 Data 放入分发包；保留许可和来源声明。若提供修改后的版本，请注明自己的修改。

生成安装包还需 Inno Setup 7，并使 ISCC.exe 在 PATH 中可用：

```powershell
$env:GLASSDESK_PACKAGE_STAGE = (Resolve-Path artifacts/publish).Path
ISCC /DAppIdValue=GlassDesk.Independent /DProductVersion=1.5.0 /DFileVersion=1.5.0.0 /DPackageBaseName=GlassDesk-1.5.0-win-x64 /Oartifacts packaging/GlassDesk-Capture.iss
```

正式安装包应沿用 `GlassDesk.Independent` 安装标识，在原位置覆盖升级。`Data` 默认保留；安装和卸载都应让用户明确选择是否删除。1.5.0 已验证安装、覆盖升级及安装时保留或清除 `Data`；卸载界面的保留或清除选项按本次发布要求跳过实际测试，详见更新说明。此公开源码流程不依赖私有仓库的旧安装载荷。

TransitionGlass.hlsl 及预编译的 TransitionGlass.ps 位于 src/GlassDesk.UI/Effects；修改着色器后执行 build/compile-transition-shader.ps1，再重新编译。

仓库保留 build/package.ps1、build/verify-package.ps1 及 packaging/release.json 等历史打包材料，供相应回归测试使用；其中旧候选版本号不代表本版版本。构建 1.5.0 的便携版和安装包请使用上面的命令。第三方声明文档中的历史候选记录也不替代当前分发产物的许可核对。
