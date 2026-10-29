# 0.9.4 主题细节、带框飞入、删除偏好

## 用户反馈落实

1. 棉麻与柔彩绳上篮子72→104px，预留宽度与挂点/篮口同步变化；标题和主题选择器也放大。80张照片、窄屏与各尺寸不会占用篮子区。
2. 工具按钮模板删除所有高光线，仅保留纯色、描边和图标色；纸张/软胶面板材质仍保留。
3. 柔彩底板改用原生抗锯齿圆角白边及叠色层，停止对倾斜PNG进行九宫格拉伸；图片圆角和留白统一。纵图、横图、小图及100%/150%/200%渲染通过。
4. 实际使用Stitch MCP生成图标家族，项目5482405501300014434、屏幕cb5ca87bcb5149d48ceb3c7e951b8683。24个SVG变体转为原生WPF绘图，应用多尺寸ICO与专用16/20px浅/深托盘图标分别生成。窗口与托盘跟随主题；纸与木/夜航采用对应材质家族，晴空保留通用图标。EXE/现有快捷方式保留通用身份，可在外观窗口导出当前ICO。原始设计与原生预览在artifacts/icons-094；未采用Stitch文案中的额外英文名或虚构版本号。
5. X、右键菜单、篮子批量删除走App.DeletePhotos；对应显示器居中，默认取消。“以后删除不再询问（移到回收站）”只有确认时持久化；托盘“删除前确认”可恢复。跳过提示也不永久删除。成功操作/批次一次B音效，失败/取消不触发成功音；受静音、勿扰与连续音效合并策略约束。B音频原文件未改。
6. 飞行从首帧携带当前主题边框，保留原图像素，共用卡片相框绘制；图片四角与相框外沿都匹配最终旋转位置。绘图缓存，位置单独变换；窗口裁剪到虚拟桌面，4K截图不会为屏幕外的边框额外分配大片透明区域。
7. 透明无边框窗口禁用DWM圆角、系统背板、非客户区装饰。旧二进制实测主题切换后为ROUND/AUTO，新版为DONOTROUND/NONE且NCRenderingEnabled=0；WPF空白Alpha为0，原生玻璃区域有界。**用户报告仅棉麻/柔彩出现的半透明矩形尚未完成实屏复现/消失确认。** WGC访问授权超时；此前GDI底色不符合自建基准，未把它当作验证成功，也没有绕过授权换渠道抓取桌面。

## 最终回归

|检查|通过数|证据|
|---|---:|---|
|核心布局|49|artifacts/core-094.log|
|删除确认/偏好/音效事件|14|artifacts/final-094-DeleteConfirmationChecks/results.txt|
|图标多尺寸/对比度/动态切换|3|artifacts/final-094-ThemeIconChecks/results.txt|
|完整主题/框/篮子布局|25|artifacts/themes-0.9.4-final-preview/results.txt|
|带框飞入/透明窗口/屏外裁剪|67|artifacts/final-094-MotionPolishChecks/results.txt|
|安全|126|artifacts/final-094-SafeChecks/results.txt|
|篮子/贴图/编排|37|artifacts/final-094-BasketChecks/results.txt|
|动效性能行为|23|artifacts/final-094-MotionPerformanceChecks/results.txt|
|Windows选区识别|20|artifacts/final-094-WindowsSnipOriginChecks/results.txt|
|Windows接收/去重|26|artifacts/final-094-WindowsSnipIntakeChecks/results.txt|
|保存/退出/更新生命周期|12|artifacts/final-094-CaptureLifecycleChecks/results.txt|

以上零失败，构建零警告/错误。删除测试只移动隔离夹具文件，未调用真实回收站或修改用户AppData。独立代码审查未报告确认的P1/P2问题；不能将代码审查等同桌面视觉验证。

## 性能与交付边界

4K/200%缩放测试中，常驻绳子窗口仍为整屏32.4%；新增完整4K带框飞行窗口限制为3840×2160。120帧飞行零窗口尺寸变化，屏幕外绘图不反复扩展，目标改变需要时只扩展一次。性能受系统负载影响，未承诺固定帧率。

预览入口artifacts/theme-polish-094/index.html。最终同版本安装/便携包均已通过更新暂存器：便携486个文件、安装器5个文件，二进制和包元数据均为0.9.4；便携485项文件哈希、捆绑.NET/WPF与原生DLL加载检查也通过。记录artifacts/shipped-update-validation-094.log。最后把删除提示文案改为不承诺必有确认框，避免启用跳过确认后仍显示“需确认”；未交付候选包保存在artifacts/pre-final-delete-copy-094。未发布GitHub Release，未替换用户运行版本。实屏矩形问题需要新版运行后继续确认，其他既有真实多屏/外部截图软件边界仍保留。
