# 四套新主题的设计来源

本轮实际调用了 Stitch MCP 和 MiniMax Design MCP，各生成两套设计。原始概念稿和 MCP 返回记录在 `artifacts/design-studies`；软件里的 WPF 控件与矢量美术由这些设计转译实现，运行预览和生成概念稿分开保存。

|主题|设计服务|完整概念稿|主要材料与结构|
|---|---|---|---|
|白瓷工作室|Stitch MCP|[概念稿](../artifacts/design-studies/stitch/porcelain-final.png)|瓷质收纳盒、纤细挂钩、清透相框、圆润控件|
|暗房实验室|Stitch MCP|[概念稿](../artifacts/design-studies/stitch/darkroom-repaired.png)|穿孔金属篮、钢夹、底片框、琥珀强调与机械边框|
|棉麻画室|MiniMax Design MCP / Design Image 2.5 Pro|[概念稿](../artifacts/design-studies/minimax/linen.png)|椭圆藤编篮、皮带、木夹、棉绳与宽底纸相框|
|柔彩积木|MiniMax Design MCP / Design Image 2.5 Pro|[概念稿](../artifacts/design-studies/minimax/color-play.png)|双挂带软胶桶、彩色夹子与叠层框、柔圆控件|

Stitch 项目分别为 `7363412241859558340` 和 `14815464972297361126`。暗房首轮修订出现白底和文字对比度丢失，已通过 Stitch 再次修改；采用的修订屏幕为 `b5b3576c95464594ac3f8a49506496c1`。

MiniMax 结果保存在“晾图页面设计”项目组中的“晾图主题 MCP V3”页面。18810 端口正确；原旧页面未列入 MCP 项目列表，新建 V3 页面后即可连接，原页面保留。完成任务为 `tsk_320ea7d35abd`、`tsk_f6933c146092`，版本为 `ver_9af70f08a786`、`ver_70eaf303acac`。完整关联见 [provenance.json](../artifacts/design-studies/provenance.json)。

PicRope 参考来自用户已安装的 1.1.0 发布程序。REA 托管检查确认五套篮子 PNG 与成套信封图，静态提取验证了程序及资源哈希；只研究公开程序美术，没有读取用户截图。原始美术仅在 `reference/picrope-1.1.0` 作为研究资料，未编入晾图发行包。

概念图生成的摄影参数、自动保存状态、版本号、替代应用标志、鼠标选择工具等不作为产品功能。软件保留已确认的应用/托盘图标与 B 组音效；截图标注通过直接点选和虚线框操作。主题不改变媒体内容、历史记录、入篮与回挂语义。
