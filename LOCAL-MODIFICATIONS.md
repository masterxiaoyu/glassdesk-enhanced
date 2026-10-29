# GlassDesk 1.5.0 非官方修改说明

本版本基于原作者 everlosttime 的
[GlassDesk 原产品](https://github.com/everlosttime/glassdesk) 及
[1.4.0 公开源码](https://github.com/everlosttime/glassdesk-open-source)
快照 `b1eb58c2d7205fca57e192e45d363872ced55624`，是非官方修改版。
原作者和本版维护者不是同一发布方。

## 新增功能

- Dock 增加“软件聚合工具栏”入口。可创建、编辑、删除具名分组，
  从应用库选择软件或导入 `.exe`、`.lnk`，再从分组菜单启动。
  分组保存在 `Data/dock-layout.json`；删除应用库条目时会清理对应引用。
- 设置中心“桌面”页增加 Windows 系统任务栏状态和自动隐藏开关。
  可查看当前任务栏位置及 Windows 透明效果状态，并打开相应系统设置页。

## 修复

- 软件聚合编辑窗使用与控件文字匹配的深色背景，修复白底白字导致的内容不可读。
- 其他软件窗口盖住 Dock 区域时，鼠标悬停不再让 Dock 抢到前台；
  全屏唤出锁仍只控制未被窗口盖住时的全屏悬停行为。

## 使用边界

- “Windows 透明效果”由系统控制，会影响其他 Windows 界面；
  它不会让系统任务栏完全透明。GlassDesk 提供状态和设置入口。
- 可选的系统任务栏位置由 Windows 版本和功能推送决定，
  GlassDesk 显示当前位置并打开系统任务栏设置页。
- Dock 的自动隐藏与系统任务栏的自动隐藏是两个独立开关。

## 署名与赞助

- 设置页标明原作者 everlosttime、原产品地址和公开源码地址。
- 赞助窗口使用本非官方修改版维护者提供的支付宝、微信收款码；
  收款方与原作者无关，全部功能仍可免费使用。

请保留随包附带的 `LICENSE.txt`、`NOTICE.txt` 和第三方许可资料。
版本与升级规则见 [发布政策](packaging/RELEASE-POLICY.md)。
