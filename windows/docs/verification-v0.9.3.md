# 0.9.3 Win+Shift+S矩形截图源位置

## 实现

- 被动观察可信Microsoft截图浮层内的鼠标按下/抬起，不注册或抢占Win+Shift+S、不注入输入。仅“接管截图”开启且未退出/升级/游戏勿扰时有效。普通鼠标移动立即透传，不读取坐标；键盘取消观察仅在拖动期间存在，且从不读取键值。
- 验证Windows包身份、截图浮层覆盖范围、近期完整拖动、剪贴板来源与序列、实际PNG尺寸后，传递物理源矩形给既有贝塞尔飞入。起点保留真实物理坐标，图片尺寸按照逻辑像素向外取整后严格匹配整数像素。
- 来源快照在首次等待和读取PNG前记录；Windows/OLE释放所有者后，只能以同一剪贴板序列的已验证记录补全空所有者。其他明确应用、新序列、隐私/文字内容均不能借用旧位置。
- 取消、键盘调整、过期、尺寸不符及未决的重叠框选保留普通入场。状态、来源与钩子清理使用同一锁，防止过期清理移除新拖动的观察器。
- 验证通过且自动挂图开启的Windows截图会主动显现；关闭自动挂图仍只进篮子。全屏保护、游戏勿扰不被绕过。
- PNG一次读取并保留原始字节；发布进入退出/更新等待队列。文件先到、剪贴板先到或同时到达只挂一张；不删除Windows原图。尚未显示的照片可补充起点，已经播放的照片不重复飞入。

## 真实Windows证据

使用合成坐标编码画面，在3840×2160、200%缩放的当前机器验证。读取截图内部三个编码像素，反算真实裁剪起点，再与识别结果独立比较。

|方向|鼠标起止点|Windows PNG|编码像素确认的起点|识别结果|
|---|---|---|---|---|
|正向|603,465 → 1390,934|788×470|603,465|603,465,788,470|
|反向|1392,933 → 605,467|788×466|605,467|605,467,788,466|

两次完全一致；重复剪贴板通知返回无新匹配，未重放位置。原始证据：artifacts/snip-probe-093/data/probe.log，UTC17:55段。此前试验定位了三个实际问题：截图浮层也带caption样式位、起点不应按DPI向下取整、PNG呈现后所有者可能为空；均据证据修正。

后续使用正式Store接收代码的新测试窗口，Computer Use返回“app approval timed out”，未能完成该次完整GUI自动化；窗口已自行退出。因此本轮实机证据是源位置识别与PNG像素核对，正式接收/入库/动画组合由下列合成回归覆盖，不能声称新版完整GUI实机链路已自动验收。

## 回归

|检查|结果|记录|
|---|---|---|
|源位置状态机|20 PASS / 0 FAIL|artifacts/final-093-WindowsSnipOriginChecks/results.txt|
|正式剪贴板事件桥与发布|26 PASS / 0 FAIL|artifacts/final-093-WindowsSnipIntakeChecks/results.txt|
|安全回归|126 PASS / 0 FAIL|artifacts/final-093-SafeChecks/results.txt|
|篮子、贴图、编排|37 PASS / 0 FAIL|artifacts/final-093-BasketChecks/results.txt|
|动画行为与性能|23 PASS / 0 FAIL|artifacts/final-093-MotionPerformanceChecks/results.txt|
|保存/退出/更新生命周期|12 PASS / 0 FAIL|artifacts/final-093-CaptureLifecycleChecks/results.txt|
|核心规则|49 PASS / 0 FAIL|artifacts/core-093.log|

独立审查发现并修复了来源观察未接入、小数DPI产生非整数期望尺寸、过期线程可能移除新观察器三项问题。125%/150%/175%的整数转换有状态回归，但未修改用户系统缩放做对应实机试验；真实多屏组合也未穷举。无法匹配时回退，不用近似位置冒充准确位置。

## 交付边界

0.9.3本地便携/安装包均已生成。便携485项文件哈希、捆绑.NET/WPF与原生DLL加载通过；两种ZIP分别以486/5个文件通过真实更新暂存器验证，元数据及二进制版本均为0.9.3。记录：artifacts/shipped-update-validation-093.log。保留0.9.2主题素材与声音，没有发布远程Release或替换正在运行的旧版。真实Windows试验会按系统设置生成合成截图，可能出现在Windows截图文件夹及旧版篮子；未清理或删除这些系统文件。测试代码与隔离数据位于artifacts，正式程序不保存观察日志或普通输入内容。

参考：[Microsoft鼠标钩子坐标定义](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-msllhookstruct)、[Snipping Tool协议](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-snipping-tool)。协议的回调需要MSIX身份且未列出选区坐标返回字段，因此未改用协议接管系统截图入口。
