# 0.8.1 松夹、回挂和落篮

用户确认：桌面贴图关闭/按 Esc 默认挂回绳子，另设放入篮子。已实现显式文字“挂回绳子”、关闭/Esc回挂、篮子独立按钮。删除、挂回时内部关闭和软件退出不会重入回挂。回挂使用当前贴图物理坐标，原截图来源由首次入场消费，避免复用旧位置。

右侧篮子与卡片角标改为统一蓝青/珊瑚 WPF 矢量；角标28px、不透明底板、常态可见。夹子32×42命中区，长按450ms后180ms开合，短按/提前释放/移动超过6DIP/隐藏/显示器切换取消。图片本体长按标注不变。

截图已知来源的挂入约850ms，未知来源约720ms轻落。收篮/贴图飞行采用140ms松夹和780ms曲线移动；篮口前沿遮挡、缩小落入、篮子轻回弹。动画层只负责视觉，取下仍即时保留文件与篮子记录；窗口隐藏/显示环境改变/游戏勿扰时清理所有视觉层。透明贴图目标在飞行完成前不接收鼠标。主要动效尊重系统动画开关。B组音效原样保留。

应用图标以原构图通过imagegen改为扁平风格。托盘生成参考的透明边缘和16px识别不理想，因此使用原生矢量几何单独绘制深浅主题：深色为白轮廓，浅色为深蓝轮廓，配珊瑚夹子与蓝青山形。16/20/24/32px原尺寸与放大对照在 Assets/IconSource/tray-size-preview.png。不是把应用图标直接缩小。

## 验证
- 修改前 BasketChecks 复现默认关闭不能回挂：artifacts/motion-red-data。
- BasketChecks + ShelfVisualChecks：默认关闭、显式收篮、夹子长按取消/单次触发、动画起终点/上拱/缩放、来源只消费一次、透明目标不命中、贴图飞行完成、游戏中清理飞行、复杂背景角标渲染、文字标注回归；结果 artifacts/motion-final-data。
- SafeChecks 120项：artifacts/motion-safe.log。旧角标20px测试按新28px实际尺寸计算中心与圆角，保留边界外拒绝断言。
- Core.Tests 49项：artifacts/motion-core.log；CollectionChecks 11项：artifacts/motion-collection-data。
- UI渲染：artifacts/motion-visual/shelf-controls.png、basket-flight-frames.png、pin.png，覆盖纯白、纯黑、条纹干扰背景。
- 独立静态代码审查未发现 Important/Critical 问题，检查退出/勿扰/长按取消/跨屏/来源消费/视觉层清理/输入区域。

## 边界
真实双屏热插拔、用户实际屏幕上的主观速度/拟真程度仍需试用确认。截图与录屏来源不变。贴图位置仍不跨重启恢复，文件保留。原始源码备份 artifacts/pre-motion-0.8.1，旧发布目录保留。
