# URflow v1.0.3 使用指南与更新记录

> Unity 动画贝塞尔曲线编辑器 | Inspired by Flow for After Effects
> 适用代码：v1.0.3（83ab779，2026-09-03） | 文档更新：2026-10-30
> 功能建议 & Bugs: liuyiran02@corp.netease.com

---

## 一、插件简介

URflow 是一款用于 Unity Editor 的**三阶贝塞尔曲线编辑器**，灵感来源于 After Effects 的 Flow 插件。它可以让动效设计师在 Unity 中直观地编辑 Animation Curve 的缓动曲线，无需手动调节 Keyframe 的切线参数。

### 核心功能

- 可视化贝塞尔曲线编辑器，支持拖拽控制点
- **控制杆可超出坐标轴框**，实现 Overshoot / Anticipation 效果
- **Shift + 拖拽水平吸附**，快速创建平滑缓动
- 从 Animation Window 读取 / 应用曲线，支持 Dopesheet 和 Curves 视图
- CurveGuard 在检测到权重模式被重置时尝试恢复已应用的曲线
- 37 个内置缓动预设：Standard 5 个、Penner 24 个、UI Motion 8 个
- 自定义预设保存、导入、导出
- 列表 / 网格视图切换
- Flow 风格深色主题，曲线渐变色显示

### 系统要求

- 包声明最低版本为 Unity 2022.3；仓库记录的测试版本为 2022.3.20f1，其他版本需在目标工程验证
- Editor Only（不影响运行时性能）

---

## 二、安装方法

### UPM 安装

打开 **Window → Package Manager → + → Add package from git URL**，填写：

```text
https://github.com/Ryancaoye/URflow.git#v1.0.3
```

省略 `#v1.0.3` 会跟随仓库默认分支。团队协作建议锁定 tag；`v1.0.2` 不包含后续 Apply / CurveGuard 修复。

### 离线安装

解压 `URflow_v1.0.3.zip`，将完整的 **URflow** 文件夹放到工程的 **Assets/URflow/**；或在 Package Manager 选择 **Add package from disk**，选中 `URflow/package.json`。保留 `.meta` 和图标，不要只复制脚本。

等待编译完成后，通过 **Window → URflow** 或 **Ctrl+Shift+E** 打开。设置页的“检查更新”会打开 Package Manager，当前没有自动联网检查更新。

### 文件结构

| 文件 | 说明 |
|------|------|
| `URflowWindow.cs` | 主窗口、曲线绘制与交互 |
| `URflowApplyHelper.cs` | 曲线应用到 Animation Window |
| `URflowReadHelper.cs` | 从 Animation Window 读取曲线 |
| `URflowWeightedHelper.cs` | 保留的 Weighted 工具，当前按钮已隐藏 |
| `URflowCurveGuard.cs` | 检测权重模式重置并尝试恢复曲线 |
| `URflowAnimHelper.cs` | 反射访问 Animation Window 内部 API |
| `CubicBezierConverter.cs` | 贝塞尔曲线数学计算 |
| `BezierPreset.cs` | 预设数据结构 |
| `PresetLibrary.cs` | 37 个内置预设 |
| `PresetManager.cs` | 用户预设管理 |
| `Icons/` | 自定义按钮图标（PNG） |

---

## 三、界面说明

### 3.1 曲线编辑区

窗口中央的深色区域是曲线编辑器，显示当前贝塞尔曲线的形状。

- **渐变曲线**：从青绿色 (#5EF0B0) 渐变到蓝色 (#0071FF)
- **橙色控制点**：P1（左下）和 P2（右上），可拖拽
- **黄色控制杆**：从起点/终点到控制点的连线
- **网格线**：4×4 网格辅助对齐
- **对角线**：线性参考线（linear）

### 3.2 参数输入

曲线下方有四个数值输入框：**X1、Y1、X2、Y2**，对应 CSS cubic-bezier(x1, y1, x2, y2) 格式。

- X1/X2 范围：0 ~ 1
- Y1/Y2 数值输入与 READ：-0.5 ~ 1.5，可实现一定程度的 Overshoot / Anticipation
- 拖拽代码允许 -2 ~ 3，但数值框会在界面更新时再次限制到 -0.5 ~ 1.5，更大范围不能视为稳定支持

上方文本框接受四个逗号分隔的数字，例如 `0.20, 0.00, 0.00, 1.00`。粘贴 CSS 参数时，去掉 `cubic-bezier(` 和末尾 `)`。

### 3.3 操作按钮

| 按钮 | 功能说明 |
|------|----------|
| **READ** | 读取第一条有效属性曲线的前两个选中关键帧 |
| **APPLY** | 在每条有效属性曲线上，按时间将选中帧两两相邻配对并应用 |
| **SAVE** | 保存当前曲线到 My Presets（弹出命名对话框） |

当前操作栏为 READ、APPLY、SAVE，没有单独的方向按钮或 Weighted 按钮。

### 3.4 预设浏览

下方预设区域有三个选项卡：

- **ALL** — 显示所有内置 + 用户预设
- **FAVORITES** — 收藏的预设
- **MY PRESETS** — 用户自定义预设，带导入/导出/删除操作栏

搜索栏右侧可切换**列表视图 / 网格视图**。收藏入口位于列表视图中每个预设左侧；内置分类为 Standard、Penner 和 UI Motion，当前没有 Material 3 分类按钮。

---

## 四、使用流程

### 4.1 基本工作流

1. 在 Animation Window 同一属性曲线上选择至少两个关键帧；首次使用建议选择时间相邻的两个帧
2. 打开 URflow 窗口（Window → URflow）
3. 点击 **READ** 读取当前关键帧的曲线
4. 拖拽控制点或选择预设调整曲线
5. 点击 **APPLY** 应用后，在 Unity 中预览动画

APPLY 自动设置 Broken / Free 和权重，通常无需手动设置 Weighted。可以同时选择多条属性曲线，但每条都需要至少两个选中帧。若中间还有未选中的帧，请逐段处理并检查结果；READ 不会汇总多段曲线。

### 4.2 快捷操作

| 操作 | 效果 |
|------|------|
| **拖拽控制点** | 调整曲线形状 |
| **Shift + 拖拽** | 锁定水平方向（P1 锁 Y=0，P2 锁 Y=1） |
| **应用预设** | 单击加载后点击 APPLY；双击存在事件处理问题，待 Unity 验证 |
| **单击预设** | 加载到编辑器预览 |
| **输入数值** | 在 X1/Y1/X2/Y2 输入框精确设置 |

### 4.3 保存自定义预设

1. 调整好满意的曲线
2. 点击 **SAVE** 按钮
3. 在弹窗中输入预设名称，点击保存或按 Enter
4. 预设自动保存到 MY PRESETS 选项卡

### 4.4 预设导入 / 导出

切换到 **MY PRESETS** 选项卡后，顶部出现三个操作按钮：

- **导入**：从 JSON 文件导入预设
- **导出**：将所有用户预设导出为 JSON 文件
- **删除**：删除选中的预设（弹出确认对话框）

导入采用追加方式，不自动去重。收藏和删除按名称识别，删除时会移除全部同名自定义预设；建议使用不同名称，并在搜索或切换选项卡后重新点选目标。

自定义预设保存在工程的 `ProjectSettings/URflow/UserPresets.json`，收藏保存在同目录的 `Favorites.json`。语言使用本机 `EditorPrefs` 的 `URflow_Language`。JSON 导出包含自定义预设，不包含独立的收藏名单。

---

## 五、常见问题

- **Q: APPLY 没反应？**
  - 确保 Animation Window 已打开，同一属性曲线上至少选中两个帧，且当前 AnimationClip 可编辑
- **Q: READ 读取的曲线不对？**
  - 先仅选一条属性曲线上的两个相邻帧。READ 只读第一组；等值端点返回默认线性参数，单侧 Weighted 或阶梯切线可能无法准确还原
- **Q: 想要 Overshoot 效果？**
  - 可将 Y1 设为负值或 Y2 设为大于 1；当前数值范围为 -0.5 ~ 1.5，Spring 和 Back In Out 的部分内置参数会被截断
- **Q: 预设文件保存在哪？**
  - 工程的 `ProjectSettings/URflow/UserPresets.json` 和 `Favorites.json`，备份或迁移工程时保留
- **Q: 拖动关键帧后会一直自动保护曲线吗？**
  - 本次编辑器会话中，经 APPLY 注册的曲线会被检查，仅在相关权重模式缺失时恢复。脚本重载或重新打开 Unity 后，保护登记不会保留；曲线本身仍保存在 AnimationClip 中
- **Q: 哪些操作需要额外检查结果？**
  - CurveGuard 按索引追踪，增删帧、跨帧拖动、Undo / Redo 和更换 Unity 版本后应检查结果。自动恢复会记录 Undo，但这些边界行为仍需验证

---

## 六、更新记录

### 文档同步（2026-09-10）

按 v1.0.3 源码校正预设数量、保存位置、安装路径、参数输入和当前按钮说明，补充 Read / Apply / CurveGuard 限制。此次仅更新文档，相关代码问题尚未修复或完成 Unity 回归验证。

### v1.0.3（2026-09-03）

- 统一 4 月 16 日至 22 日已合入改动的版本号，并发布稳定 tag。
- 修复首次 APPLY 不生效：先设置 Broken / Free，再写入切线和权重。
- APPLY 自动设置 Broken / Free；CurveGuard 仅在权重模式重置时恢复，避免直接覆盖手工切线调整。
- 修复 UPM 图标路径，补齐 `.meta`，调整 LOGO 和 UI 版本显示。
- 移除自动更新检查，设置页改为打开 Package Manager。

### v1.0.2（2026-04-15）

- 新增 CurveGuard，在 APPLY 后登记关键帧对，检查权重模式重置并尝试恢复曲线。
- 此 tag 不包含 4 月 16 日的后续修复，当前安装建议使用 v1.0.3。

以下为早期版本记录，当前功能和限制以前文说明为准。

### v1.0.1（2026-04-14）

#### 新增功能

1. **20:45:21 SAVE 按钮** — 保存当前曲线到 My Presets，弹窗命名
2. **18:25:58 MY PRESETS 操作栏** — 导入/导出/删除三按钮
3. **20:09:48 控制杆超出坐标轴** — 控制点和曲线可自由画到框外
4. **20:27:16 Shift + 拖拽水平吸附** — P1 锁 Y=0，P2 锁 Y=1
5. **16:29:00 网格视图模式** — 方形曲线预览 + 名称
6. **16:40:00 设置页面** — 全屏卡片式布局
7. **18:33:29 自定义图标系统** — PNG 图标加载 + 缓存

#### 界面优化

1. **16:29:00 Flow 风格深色主题** — 深灰背景 #232323
2. **12:47:00 曲线渐变色** — #5EF0B0 → #0071FF
3. **16:29:00 APPLY 按钮渐变** — 与曲线相同的渐变纹理
4. **11:10:00 曲线线条加粗** — 主曲线 ~3px，控制杆 ~2px
5. **16:29:00 选项卡高亮** — 选中态 #FFB826 黄色底色
6. **16:40:00 X1/Y1/X2/Y2 改为 FloatField** — 替代 Slider
7. **10:50:00 所有英文大写加粗** — URFLOW / READ / APPLY 等
8. **10:37:00 中文 Tooltip** — 所有按钮悬浮显示中文功能说明
9. **12:48:58 LOGO 图片** — 替代文字标题
10. **18:41:49 Settings 无底板图标** — 左下角齿轮图标

#### Bug 修复

1. **16:29:00 GPU 内存泄漏修复** — 静态缓存 Texture2D，修复 D3D11 swapchain 崩溃
2. **16:40:00 缩略图曲线溢出修复** — GL 不受 BeginClip 约束，改用 DrawRect
3. **16:50:00 陡峭曲线断续修复** — DrawCurveInRect 插值填充
4. **16:29:00 设置栏被遮挡修复** — 改为正常布局流 + FlexibleSpace
5. **20:31:03 拖拽被 UI 拦截修复** — GUIUtility.hotControl 锁定
6. **20:07:15 Mathf.Lerp clamp 修复** — 改为 LerpUnclamped 支持超出范围

---

### v1.0.0（2026-04-14）

#### 核心功能

1. **10:37:00 可视化曲线编辑器** — GL 绘制贝塞尔曲线、拖拽控制点
2. **10:37:00 cubic-bezier 参数输入** — X1/Y1/X2/Y2 滑块输入
3. **12:00:00 内置预设** — Standard / Penner / UI Motion 分类；当前 v1.0.3 共 37 个
4. **13:30:00 APPLY 按钮** — 一键应用曲线到 Animation Window 关键帧
5. **14:00:00 READ 按钮** — 从选中关键帧读取当前曲线值
6. **15:00:00 自定义预设系统** — 保存/加载/导出（JSON 格式）
7. **15:30:00 Undo 支持** — 动画曲线写入记录 Undo；预设 JSON 文件操作不属于动画撤销记录
8. **15:30:00 收藏功能** — 预设可标记为收藏
9. **16:00:00 预设搜索** — 按名称实时搜索过滤

#### 技术实现

1. **反射访问 Animation Window 内部 API** — AnimEditor → state → selectedKeys / activeCurves
2. **cubic-bezier → Unity tangent 转换** — outWeight=x1, inWeight=1-x2, tangent=(y/x)*(valueRange/duration)
3. **Deferred Dialog 模式** — 避免 OnGUI 中调用 DisplayDialog 破坏 GUILayout 状态
4. **Dopesheet + Curves 双模式支持** — URflowAnimHelper 统一封装

#### Bug 修复

1. **GL.LINE_STRIP 不支持修复** — Unity 2022.3 不支持 GL.LINE_STRIP/GL.TRIANGLE_FAN，改用 GL.LINES + GL.TRIANGLES
2. **AmbiguousMatchException 修复** — 反射 GetMethod 改为 GetMethods() 手动匹配参数类型
3. **关键帧索引获取修复** — AnimationWindowKeyframe 无 index 属性，改用 time 值匹配
4. **DisplayDialog 崩溃修复** — 从 OnGUI 移到 Update() 中延迟弹出
5. **曲线数据缓存过期修复** — GetFreshCurve() 每次从 clip 重新读取
6. **Curves 视图兼容性修复** — 新增 URflowAnimHelper 统一 Dopesheet/Curves 双模式

#### 已知限制

- 仅支持 Editor 模式（不影响运行时性能）
- Curves 视图下的选中 API 仍在完善中

初始版本发布。


---

### 2026年10月29日至30日开发记录

目标版本 v1.0.4，开发中，尚未正式发布。教程正文仍对应正式版 v1.0.3。以下记录区分功能实现、Unity 视觉集成与验证状态。

#### 功能开发与兼容修复

- 2026年10月29日：建立独立开发分支和 worktree，采用小步验证、阶段保存及正式发版归档的开发流程。新工具分离为宿主入口、面板、曲线计算和 Unity 读写模块。

- 关键帧移动已在开发版实现：仅处理 Animation 窗口选中的位置或缩放属性，将整条曲线的首帧或尾帧对齐未受动画驱动的静态默认值。默认整体平移；勾选比例调整后，位置和缩放都可按比例变化。

- 支持 Undo 和 Redo。首尾时间不变；平移保持数值差和原切线，比例调整同步缩放数值和切线。零基准无法按比例对齐到非零目标时拒绝应用；透明度暂不纳入。

- 2026年10月30日：删除插件中被赋值但未使用的字段，以兼容制作工程将警告视为错误的编译规则。严格纯编译检查通过，用户确认工程可正常启动；未修改制作工程原有代码或资产。

关键帧移动此前已通过独立 Unity 工程的曲线计算、服务及部分界面交互检查。本轮更新了视觉和工具入口，实际回归结果单独列出；制作场景效果仍待试用验收。

#### 界面设计与文案

以下视觉改动已接入 v1.0.4 开发包，代码保存于开发分支提交 32a6bd6。本轮已执行独立 Unity 回归；外观像素检查未执行，制作工程仍待 Unity 刷新与场景试用。尚未正式发布。

- 保留原 Logo 与图标，采用中性深灰、青绿到蓝的主按钮渐变和琥珀选中态；收紧 Logo 留白，X1、Y1、X2、Y2 与数值保持同排，并保留四数值粘贴框。预设使用琥珀下划线选中态，预设区和底部工具区通过底色与分隔线区分。

- 底部工具栏高 84 px，关键帧移动为单个 44×44 px 图标，名称显示在工具提示中。详情在同窗底部展开，首选高度 420 px 并随可用空间缩小。工具数量溢出时支持鼠标拖动与滚轮横移；边缘流光在打开或成功应用时扫过一次。

- 曲线图常规高度为 260 px，较矮窗口自动缩小以保留预设与底栏空间。主线宽 1.8 px，采用高饱和绿色 #20E7A1、青色 #12CDE0 到蓝色 #168CFF 的渐变，保留柔和光晕。

- 面板展开时长固定为 400 ms，边缘流光为 1900 ms。内部固定，不提供时长参数、动画设置面板或相关入口。

- 主按钮及预设区域沿用默认英文 APPLY、ALL、FAVORITES、MY PRESETS，搜索提示为 Search presets...。APPLY 使用 15 px 白色粗体，位置、大小和圆角与整体控件统一。

#### 本轮验证结果

严格编译及独立 Unity 的数学、服务和实际界面交互检查通过；外观像素检查未执行，制作场景仍待试用。

#### 关键帧移动操作

在 Animation 窗口选择需要处理的位置或缩放属性，点击底部 Move 图标，选择 First Frame 或 Last Frame。默认整体偏移；需要整体比例缩放时勾选 Proportional adjustment。点击 Refresh 检查只读预览，再点击 Apply Move；完成后可使用 Undo 或 Redo。仅处理选中的属性，其全部关键帧一起调整；零基准的比例限制按前述规则处理。

#### 持续更新记录规范

每个完成的修改阶段都保存日期、目标版本、修改类别、内容与原因、当前状态、验证结果和对应提交。功能、修复、界面、文案、教程和流程规范均纳入记录。同一目标的连续微调可汇总最终确认效果，详细过程保留在开发记录中。

统一版本日志维护在 CHANGELOG.md。开发阶段归入 Unreleased，并同步本教程末尾的更新记录、中文 Markdown 和 Word 生成脚本，再同步到整理目录。正式验收和发版后才标注发布日期；纯文档更新不提升插件代码版本。

