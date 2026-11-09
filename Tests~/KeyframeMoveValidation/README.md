# URFlow 关键帧移动：独立验证资料

此目录保存可复制到独立 Unity 工程的 Editor 检查源代码。`Tests~` 不参与产品程序集；请保留这个目录名，不要把检查脚本导入日常制作工程。检查会创建场景、重写测试 clip、清空测试工程的 Undo、改变选中对象，并最终退出编辑器。

## 已取得的结果

2026-10-29，Unity **2022.3.56f1 (a95abeb525bd)**：

| 范围 | 结果 | 证据 |
| --- | --- | --- |
| 开发包导入与真实 Unity C# 编译 | PASS | `Evidence/bootstrap-result.txt` |
| 曲线数学计算 | 19 组 PASS | 同上，完整日志见原测试工程 `Logs/bootstrap-final.log` |
| 真实 AnimationWindow 与写入服务 | 19 组 PASS | `Evidence/service-result.txt`，完整日志见 `Logs/service-atomic-final.log` |
| 原生关键界面事件 | 6 组 PASS | `Evidence/ui-key-interactions-result.txt`，完整日志见 `Logs/ui-owned-cache-8.log` |
| 完整工具入口、保存和设置分支 | 未完成完整验收 | UiActual / UiStrict 探针保留；本次关键事件结果不能替代这些分支 |
| 精确 320×560 / 480×720 尺寸检查 | 未通过验收 | Unity 将请求的 320×560 窗口报告为 320×554，不能把实际尺寸检查计为精确尺寸通过 |
| 截图像素与视觉验收 | NOT_PERFORMED | 无已检查的窗口截图 |

`Evidence/ui-key-layout-trace.txt` 提取通过运行中的实际窗口尺寸、缓存归属、滚轮前后位置、关闭按钮和数字列宽度诊断；它是原日志的节选，未包含截图像素结果。长路径夹具的面板最小宽度为 277，内容区为 307 / 467；21 个数字项的最大右边界为 288 / 448，均位于实际内容区内。

开发目录用于 v1.0.4 试用，但此次记录的 package / UI 版本仍为 **1.0.3**，这是试用阶段的保留版本。目录名不能用来推断已发布版本。

## 建立独立工程

1. 使用 Unity 2022.3.56f1 创建独立空工程，关闭此工程的编辑器。
2. 将本目录 `Editor` 内的全部 `.cs` 文件复制到工程的 `Assets/Editor`。Unity 会为检查源代码生成测试工程自己的 `.meta`。
3. 参考 `ProjectTemplate/Packages/manifest.json` 设置本地 `file:` 包依赖。模板仅包含 animation、imgui 和 jsonserialize 模块；不依赖在线 Unity Test Framework。
4. 修改 `Assets/Editor/URFlowValidationBootstrap.cs` 的 `ExpectedPackagePath`，与 manifest 指向的开发包绝对路径一致。这是加载正确开发副本的强检查。
5. 使用下方脚本，显式传入本机 Unity 和独立工程路径。不要同时打开同一个测试工程。

模板中的本机路径：

- Unity：`G:\L50_1_1_1\UnityEditor\Editor\Unity.exe`
- 测试工程：`D:\插件\URFLOW\6-开发\测试验证\v1.0.4`
- 包依赖和 bootstrap 期望路径：`D:/插件/URFLOW/6-开发/URflow-v1.0.4`

`ProjectTemplate/ProjectSettings/ProjectVersion.txt` 记录实际编辑器版本，可作为新工程版本信息的参考。其余 ProjectSettings 由独立工程自己的 Unity 生成，不复制制作工程的配置。

## 运行

从 PowerShell 执行，先 bootstrap，再依次运行其他套件。每次等待上一个 Unity 进程结束。脚本返回 PID、日志和报告路径，运行成功须同时确认进程退出、报告更新时间属于本次运行、PASS 项数，以及日志中没有 C# 编译错误。启动进程本身不能作为验证通过的证据。

```powershell
.\RunValidation.ps1 -Suite Bootstrap -UnityPath 'G:\L50_1_1_1\UnityEditor\Editor\Unity.exe' -ProjectPath 'D:\插件\URFLOW\6-开发\测试验证\v1.0.4'
.\RunValidation.ps1 -Suite Service -UnityPath 'G:\L50_1_1_1\UnityEditor\Editor\Unity.exe' -ProjectPath 'D:\插件\URFLOW\6-开发\测试验证\v1.0.4'
.\RunValidation.ps1 -Suite UiKeys -UnityPath 'G:\L50_1_1_1\UnityEditor\Editor\Unity.exe' -ProjectPath 'D:\插件\URFLOW\6-开发\测试验证\v1.0.4'
```

| Suite | 入口 | 运行模式 | 报告 |
| --- | --- | --- | --- |
| Bootstrap | `URFlowValidationBootstrap.CompileOnly` | batchmode / nographics | `Logs/bootstrap-result.txt` |
| Service | `URFlowServiceChecks.Run` | 原生非 batch 编辑器 | `Logs/service-result.txt` |
| UiKeys | `URFlowUiProbeRunner.RunKeys` | 原生非 batch，观测实际尺寸 | `Logs/ui-key-interactions-result.txt` |
| UiActual | `URFlowUiProbeRunner.RunActual` | 原生非 batch，完整实际尺寸探针 | `Logs/ui-actual-native-result.txt` |
| UiStrict | `URFlowUiProbeRunner.Run` | 原生非 batch，精确请求尺寸探针 | `Logs/ui-event-probe-result.txt` |

Bootstrap 在测试工程的 `Assets/ValidationAssets` 创建 `Alignment.unity`、`Alignment.anim`、`Alignment.controller` 和 `URFlow Alignment Test` 对象。Service 必须先有这些资产。AnimationWindow 在 headless 模式不能提供此次检查需要的真实层级状态，故服务和界面检查不得加 `-batchmode` / `-nographics`。各入口会通过 editor update 等待原生状态，不加 `-quit`。

## 覆盖与边界

数学检查包含平移、比例、首尾锚点、负倍率、零倍率、零锚点、常量和无穷切线、溢出拒绝、关键帧时间及 tangent/weight/broken/wrap metadata 保留、输入不变，以及大数消减后的小结果。

Service 使用真实选中 GameObject、真实 AnimationWindow clip / hierarchy / key selection，不注入伪 root 或伪 clip 状态。19 组覆盖整个所选属性曲线写入、其他属性保持、只读预览、Undo/Redo、stale preview 拒绝、混合与空选择拒绝、无变化处理、原有 READ/APPLY 和 CurveGuard、层级行与关键帧冲突、采样静态值及恢复预览时间、RectTransform 和 native driven 属性、constant 切线、关闭 recording 的提示，以及通过 `AnimationUtility.onCurveWasModified` 注入写入后的失败来验证全部属性原子回滚。

UiKeys 直接设置打开状态并种入只读预览，作为界面布局夹具；应用按钮关闭。实际运行通过 **6 组 / 54 个 SendEvent 事件**，观察尺寸为 **320×554** 和 **480×720**，每个尺寸确认滚轮只滚动面板、背景滚动不变、实际缓存关闭按钮可关闭、Escape 可关闭。点位来源于本窗口实际拥有的 IMGUIContainer / layout cache，并证明唯一面板和关闭按钮，不使用猜测坐标。它不能代替真实服务写入或工具入口验收。完整 UiActual / UiStrict 探针保留正常窗口、工具入口、背景拖拽与 preset 阻挡、保存和设置分支等断言，失败须按产品错误与夹具错误分别记录。

产品服务的属性选择解析、服务夹具及界面夹具都依赖 Unity 2022.3 的私有 AnimationWindowState / hierarchy / CurveEditor 或 IMGUI / HostView 结构；升级 Unity 后必须重新确认反射定位。Service 程序化设置真实窗口的属性行和关键帧选择，其 PASS 不能等同于所有鼠标入口通过。当前精确尺寸差异不可忽略，也不能用请求尺寸替代观察尺寸。

可选截图仅尝试窗口自身 `GUIView.CaptureRenderTexture`；缺少 API 时跳过截图，不使用操作系统屏幕抓取。任何生成 PNG 必须实际查看后才可标记视觉验收通过。

## 源代码

- `URFlowValidationBootstrap.cs`：检查包路径、创建场景、执行数学套件。
- `URFlowValidationDiagnostics.cs`：输出原生 AnimationWindow 反射布局。
- `URflowKeyframeMoveMathChecks.cs`：19 组独立曲线计算检查。
- `URFlowServiceChecks.cs`：19 组真实窗口与服务检查。
- `URflowKeyframeMoveUiValidation.cs`：原生事件和尺寸探针。
- `URFlowUiProbeRunner.cs`：等待 editor 稳定、保存报告、恢复语言偏好、可选窗口渲染捕获、退出 Unity。

原始完整日志留在本次独立测试工程 `D:\插件\URFLOW\6-开发\测试验证\v1.0.4\Logs`，本目录仅携带可复用源代码和简要结果。
