# GlassDesk 1.5.1 更新说明（非官方修改版）

本版基于原作者 everlosttime 的 GlassDesk 1.4.0 公开源码修改。
[原产品](https://github.com/everlosttime/glassdesk) ·
[原公开源码](https://github.com/everlosttime/glassdesk-open-source)。
这是独立维护的版本，并非原作者发布。

## 修复

- Dock 空白区域和内置入口右键可选择“设置…”。通知区域没有显示
  GlassDesk 图标时，也能从 Dock 进入设置。
- 应用图标保留原有专属右键操作，并增加“设置…”入口。

## 安装与数据

安装包沿用 `GlassDesk.Independent` 安装标识，在原位置覆盖升级。
安装时默认保留 `Data`；只有明确选择清除才会删除旧数据。
卸载界面的保留/删除选项按此前要求未做交互实测，不能视为已验收。
数据处理规则详见随包附带的 `RELEASE-POLICY.md`。

本次按要求跳过 1.5.1 的自动与界面测试；已完成编译，Dock 右键交互
尚未实机验收。

本版按现状提供。未签名安装包可能出现 Windows 安全提示；请从本版
GitHub Release 下载、核对 SHA-256，并在安装或升级前备份 `Data`。
软件免费，赞助完全自愿；本版收款方与原作者无关。分发时须保留
`LICENSE.txt`、`NOTICE.txt` 和第三方许可资料。
