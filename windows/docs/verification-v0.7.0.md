# 0.7.0 文字编辑、卸载与提示音验证

日期：2026-10-24。设计已获用户确认。

## 文字标注

- 移除添加文字模态弹窗。文字工具点击图片后直接输入，支持多段、多行文字。
- 系统字体、6–512px 字号、颜色、粗体、斜体可提前设置或选中已有文字后调整。字体等默认值随设置保存。
- 选中框左侧移动柄按屏幕大小显示，缩小查看图片时仍可操作；部分裁剪、旋转后的文字仍保留可见的移动柄，拖动不跳回非负原点。
- 每次输入完成、样式修改、移动与删除进入撤销历史。文本框内保留文本自身的撤销操作；属性栏/画布焦点下使用编辑器撤销。
- 裁剪与旋转同步转换文字坐标；绘制其他图形后文字仍可编辑。保存按原始像素合成图片，编辑框、光标和移动柄不进入文件。文字对象不跨保存/重新打开保留。
- 预览和导出使用相同 TextBox/Border 排版结构、Ideal 格式化、无布局取整和灰阶文字渲染，避免长文本和多行文字保存后漂移。

## 卸载

- Windows 原生单选按钮和按钮，由 Inno Setup 的自定义确认对话框承载。默认保留软件设置与收件箱图片；明确选中删除才清理应用数据目录。
- 外部原图仅作为引用，不随数据目录删除。目录链接目标也保留。清理失败会提示剩余数据路径。
- 取消卸载不移除程序或测试图片。静默卸载默认保留；仅显式 `/REMOVEDATA=yes` 删除。
- 已安装旧版本需要运行 0.7.0 安装包更新卸载器，仅运行新版便携程序不会改变旧卸载器。
- 修正卸载初始化阶段的重复 DPI 缩放，使用 480×250 的逻辑客户区、固定对话框边框。当前机器日志从错误的 2185×1137 客户区修正为 1024×533，与控件缩放一致；实际窗口已视觉检查，不再有大面积空白。
- 删除目录链接的行为遵循 [Inno Setup DelTree 官方说明](https://jrsoftware.org/ishelp/topic_isxfunc_deltree.htm)。

## 声音

- 原创本地合成 PCM：挂图 180ms 木夹双触点、取下 240ms 松夹轻落、复制 140ms 纸张轻触。
- 44.1kHz/16bit/单声道，轻量音量与平滑包络，无系统 Asterisk 声音。保留托盘声音开关，关闭时停止当前声音。
- 启动恢复、测试模式静音；280ms 内事件合并且不叠加。试听文件在 `artifacts/safety-v2-final-data/sounds/`。

## 证据

- 核心测试 49 项通过；界面与安全测试 87 项通过：`artifacts/text-layout-green.log`、`artifacts/safety-v2-final-data/results.txt`。
- 文字旧实现失败记录：`artifacts/text-red.log`。预览/导出及裁剪移动专项修复前失败记录：`artifacts/text-review-red.log`、`artifacts/text-layout-red.log`。
- 声音旧实现失败记录：`artifacts/sound-red.log`；修复后 PCM 格式、峰值、时长及静音/节流检查通过。
- 卸载旧实现显式删除失败：`artifacts/uninstall-red.log`。修复后的默认保留、明确删除、外部原图、junction 目标验证：`artifacts/uninstall-green.log`。
- 最终 0.7.0 隔离安装包 12 项检查通过：`artifacts/uninstall-0.7.0.log`，含两次已安装程序的运行时与文件哈希校验；卸载布局尺寸证据：`artifacts/uninstall-0.7.0/dialog.log`。
- 真实 UI 已验证：图片上点击后直接输入中文与英文、空格、Enter 换行、输入后勾选粗体、拖动移动柄、Ctrl+Z 一次恢复位置、Ctrl+S 保存示例。真实操作仅使用工作区合成样例。
- WPF 焦点探针：`artifacts/text-focus-probe-red/focus-result.txt`（该目录名为诊断阶段名，实际结果为 PASS）。中文测试通过 Unicode 文本输入，未逐一验证各第三方输入法候选窗口。
- 标准与紧凑界面离屏渲染：`artifacts/safety-v2-final-data/ui/text-editor-1120.png`、`text-editor-760.png`。

本轮没有重新测试所有真实多屏组合或更改视频下载链路。卸载集成只操作隔离测试安装，不清理正式用户数据。

## 交付

- `dist/Liangtu-0.7.0-Windows-x64-portable.zip`，483 个文件哈希、自带 .NET/WPF 与原生 DLL 校验通过，见 `artifacts/release-0.7.0.log`。
- `dist/Liangtu-0.7.0-Windows-x64-Installer-r1.zip`，含中文安装程序及校验值，编译与打包记录见 `artifacts/installer-0.7.0.log`。
- `run.cmd` 已指向新版便携程序。正式已安装版本使用新安装器升级，才能更新其卸载行为。
