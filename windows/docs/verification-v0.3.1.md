# 0.3.1 复制失败修补 · 2026-10-23

用户已验证 Win+Shift+S 截图能挂到晾衣绳，但复制时弹出 OpenClipboard 失败，HRESULT 0x800401D0 / CLIPBRD_E_CANT_OPEN。

## 原因与处理

Store.Copy 原先同步调用 WPF Clipboard.SetDataObject。WPF 内部使用短暂的阻塞重试，失败就把底层异常显示给用户。不能由错误码断言具体是哪个程序占用了剪贴板。

复制现改为 await 的写入流程：使用 WinForms 单次 OLE 发布（关闭其内置同步重试），在原 STA/UI 线程上通过 Task.Delay 进行限时重试；写入和持久化分开处理，各阶段最多 21 次、间隔 150ms。两阶段之间让出 40ms 给剪贴板监听程序。Flush 阶段先检查自己是否仍是当前剪贴板数据，避免刷新其他程序刚写入的内容。

复制期间暂停本程序的剪贴板导入，作废旧导入任务；新的复制请求取消旧的等待重试。只在完整成功后触发“已复制”；持续占用时只提示一次友好错误，保留截图。无权限、文件损坏等其他错误不会伪装成暂时占用而反复重试。

文件列表、Bitmap、PNG、自身复制标记都保留。图片复制和右键菜单统一使用同一条异步路径。

## 验证

先按旧的单次尝试行为运行新增测试，暂时占用、Flush 重试、限时重试、取消旧请求、外部替换五项失败；修复后通过。

新增 9 项回归覆盖：暂时占用恢复、STA 与界面调度、Flush 不重复发布、有限超时、非暂时错误直接失败、取消旧请求、外部内容替换、完整格式/像素、成功通知和错误通知次数、连续复制最后请求优先。

隔离系统测试 ClipboardIntegrationChecks 只允许使用私有窗口站的剪贴板，禁止回退到用户交互剪贴板。本机创建私有窗口站返回 Win32 error 5（拒绝访问），沙箱外重试也相同，因此真实 Windows 竞争测试未能执行，不能标为通过。测试未访问用户剪贴板。日志见 artifacts/copy-native-data/result.txt。

安全测试：artifacts/copy-fix-data/results.txt。该测试使用可控的系统边界代替真实剪贴板占用，不能代替用户机器上的实际点击复制验收。

## 使用

从托盘退出旧版，运行根目录 run.cmd，或 dist/Liangtu-0.3.1/Liangtu.exe。设置和图片继续使用原本的本地数据目录。用 Win+Shift+S 截图挂起后，单击卡片或右键复制，随后在聊天框/图片编辑器中粘贴检查实际内容。

实现依据：[.NET 8 WPF Clipboard 源码](https://github.com/dotnet/wpf/blob/v8.0.0/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/clipboard.cs)、[.NET 8 WinForms Clipboard 源码](https://github.com/dotnet/winforms/blob/v8.0.0/src/System.Windows.Forms/src/System/Windows/Forms/Clipboard.cs)。
