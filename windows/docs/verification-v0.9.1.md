# 0.9.1 直接操作标注与四套新主题

## 本次实现

- 画完标注自动选中，直接点选已有标注并拖动；移除单独的鼠标选择工具按钮。虚线框四角缩放、顶部圆点旋转，附近提供复制/重复/删除/完成；Shift等比缩放和15°旋转吸附，Alt穿过标注绘制，双击文字继续原生输入。Q仍可作为可选快捷键。
- 一次完整手势只提交一步历史；Esc/丢失捕获取消，局部擦除遮罩随几何变换。复制对象在当前编辑窗口内使用；保存后合并为图片，不能拆回旧图片内已经栅格化的标注。
- 真实缩放低至1%时，操作控件保持可点击尺寸；控制覆盖层与图片画布分离，长窄截图不会裁掉操作条。小选区手柄保留最小间距。裁剪与橡皮擦优先于变换，即使粘贴或右键选中了对象、旧手柄延迟收到事件也不会启动缩放。
- 实际通过Stitch MCP生成白瓷工作室/暗房实验室，通过MiniMax Design MCP的Design Image 2.5 Pro生成棉麻画室/柔彩积木，再实现为独立的WPF矢量篮子、夹子、绳子、相框与控件。保留旧三套主题及其设置ID。七套均可在托盘“外观主题”选择；支持滚动、键盘导航、即时切换。
- 应用图标、浅/深托盘图标及B组声音逐文件SHA256对照0.9.1改造前备份，相同。参考PicRope资源未加入发行包。设计与真实界面分开记录，见 [设计来源](theme-design-sources-0.9.1.md)。

## 验证证据

|检查|结果|记录|
|---|---|---|
|直接标注手势与文字|56 PASS / 0 FAIL|artifacts/direct-transform-priority-final/results.txt|
|可编辑对象与各主题未布局几何|33 PASS / 0 FAIL|artifacts/direct-object-review-final/object-checks.txt|
|核心布局/手势规则|49 PASS / 0 FAIL|artifacts/core-091.log|
|安全回归|120 PASS / 0 FAIL|artifacts/final-091-SafeChecks-confirmed/results.txt|
|篮子/贴图/回挂编排|31 PASS / 0 FAIL|artifacts/final-091-BasketChecks-confirmed/results.txt|
|七套原生主题与窗口|20 PASS / 0 FAIL|artifacts/themes-0.9.1-final-preview/results.txt|
|更新核心|4组通过|本轮运行artifacts/update-checks：版本/渠道资产/下载校验取消/本地包路径完整性|

主题检查覆盖中文及fr-FR/de-DE小数逗号环境、100%/150%/200%篮口坐标、现有窗口几何刷新、设置持久化、未保存编辑/撤销与导出像素不变、正文和辅助文字对比度、视频时间轴。四套新主题另渲染760像素宽编辑器，无工具栏裁切。

独立审查发现并修复：5%缩放下限使手柄过小、覆盖层绕过裁剪/橡皮擦、区域格式破坏几何路径、高对比度模式误用主题帧底色。前三项均由回归覆盖；高对比度改走系统颜色路径，未切换用户Windows设置实测。原生双击输入焦点通过显示窗口检查，中英文文本编辑保留；真实IME候选窗口未自动化验收。

最终验证脚本最初在120项安全测试通过后误判退出失败。以dotnet --version对照复现Windows PowerShell 5的未缓存进程句柄导致ExitCode为null；改为等待前读取Handle后，重新运行安全检查并明确确认进程退出码0。不是应用测试失败。

## 交付与边界

本地0.9.1便携/安装包已生成，包含现有Windows升级助手、update-info.json及完整SHA256SUMS.json。真实便携包通过485项文件哈希、自带.NET/WPF及原生DLL加载检查；两个最终ZIP均经过同一更新暂存器校验，便携486个文件、安装器5个文件，元数据与二进制版本均为0.9.1。记录：artifacts/shipped-update-validation-091.log、artifacts/package-091-portable.log、artifacts/package-091-installer.log。没有上传GitHub Release，也没有替换用户正式安装。

0.9.0可通过内置“Windows更新与升级”导入对应版ZIP；0.8.2及以前使用新安装包或完整解压便携包。安装版与便携版ZIP不可混用。安装程序仍未代码签名，SHA256检查不等同发行者签名。

实际原生界面：artifacts/themes-0.9.1-final-preview。四套对照入口：artifacts/design-studies/index.html。概念稿是设计参考，实际软件未新增生成图中虚构的摄影参数、自动保存、图层面板或其他功能。
