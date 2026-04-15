# URflow v1.0.1 使用指南与更新记录

> Unity 动画贝塞尔曲线编辑器 | Inspired by Flow for After Effects
> 功能建议 & Bugs: liuyiran02@corp.netease.com

---

## 一、插件简介

URflow 是一款用于 Unity Editor 的**三阶贝塞尔曲线编辑器**，灵感来源于 After Effects 的 Flow 插件。它可以让动效设计师在 Unity 中直观地编辑 Animation Curve 的缓动曲线，无需手动调节 Keyframe 的切线参数。

### 核心功能

- 可视化贝塞尔曲线编辑器，支持拖拽控制点
- **控制杆可超出坐标轴框**，实现 Overshoot / Anticipation 效果
- **Shift + 拖拽水平吸附**，快速创建平滑缓动
- 一键从 Animation Window 读取 / 应用曲线
- 60+ 内置缓动预设（Penner / CSS / UI Motion）
- 自定义预设保存、导入、导出
- 列表 / 网格视图切换
- Flow 风格深色主题，曲线渐变色显示

### 系统要求

- Unity 2022.3 LTS 及以上版本
- Editor Only（不影响运行时性能）

---

## 二、安装方法

1. 复制 **URflow/Editor/** 文件夹到你的 Unity 项目的 **Assets/** 目录下
2. 等待 Unity 编译完成
3. 菜单栏 → **Window → URflow** 打开编辑器窗口

### 文件结构

| 文件 | 说明 |
|------|------|
| `URflowWindow.cs` | 主窗口 UI（~1200 行） |
| `URflowApplyHelper.cs` | 曲线应用到 Animation Window |
| `URflowReadHelper.cs` | 从 Animation Window 读取曲线 |
| `URflowWeightedHelper.cs` | 设置关键帧 Weighted 模式 |
| `URflowAnimHelper.cs` | 反射访问 Animation Window 内部 API |
| `CubicBezierConverter.cs` | 贝塞尔曲线数学计算 |
| `BezierPreset.cs` | 预设数据结构 |
| `PresetLibrary.cs` | 内置预设库（60+） |
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
- Y1/Y2 范围：-2 ~ 3（允许 Overshoot）

### 3.3 操作按钮

| 按钮 | 功能说明 |
|------|----------|
| **READ** | 从 Animation Window 中选中的关键帧读取当前曲线参数 |
| **APPLY** | 将当前编辑器中的曲线应用到 Animation Window 选中的关键帧 |
| **SAVE** | 保存当前曲线到 My Presets（弹出命名对话框） |

### 3.4 预设浏览

下方预设区域有三个选项卡：

- **ALL** — 显示所有内置 + 用户预设
- **FAVORITES** — 收藏的预设
- **MY PRESETS** — 用户自定义预设，带导入/导出/删除操作栏

搜索栏右侧可切换**列表视图 / 网格视图**。

---

## 四、使用流程

### 4.1 基本工作流

1. 在 Animation Window 中选中需要调整的关键帧
2. 打开 URflow 窗口（Window → URflow）
3. 点击 **READ** 读取当前关键帧的曲线
4. 拖拽控制点或选择预设调整曲线
5. 点击 **APPLY** 应用到关键帧

### 4.2 快捷操作

| 操作 | 效果 |
|------|------|
| **拖拽控制点** | 调整曲线形状 |
| **Shift + 拖拽** | 锁定水平方向（P1 锁 Y=0，P2 锁 Y=1） |
| **双击预设** | 立即应用该预设到选中关键帧 |
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

---

## 五、常见问题

- **Q: APPLY 没反应？**
  - 确保 Animation Window 已打开且选中了关键帧
- **Q: READ 读取的曲线不对？**
  - 建议先将关键帧设为 Weighted 模式（Animation Window 右键关键帧）
- **Q: 想要 Overshoot 效果？**
  - 把 Y1 设为负值或 Y2 设为大于 1 的值
- **Q: 预设文件保存在哪？**
  - EditorPrefs 中，路径为 URflow_UserPresets，JSON 格式

---

## 六、更新记录

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

### v1.0.0（2026-04-13）

#### 核心功能

1. **10:37:00 可视化曲线编辑器** — GL 绘制贝塞尔曲线、拖拽控制点
2. **10:37:00 cubic-bezier 参数输入** — X1/Y1/X2/Y2 滑块输入
3. **12:00:00 40+ 内置预设** — Standard / Penner / UI Motion 分类
4. **13:30:00 APPLY 按钮** — 一键应用曲线到 Animation Window 关键帧
5. **14:00:00 READ 按钮** — 从选中关键帧读取当前曲线值
6. **15:00:00 自定义预设系统** — 保存/加载/导出（JSON 格式）
7. **15:30:00 Undo 支持** — 所有操作可撤销
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

