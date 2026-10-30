const fs = require("fs");
const path = require("path");
const { Document, Packer, Paragraph, TextRun, Table, TableRow, TableCell,
        Header, Footer, AlignmentType, HeadingLevel, BorderStyle, WidthType,
        LevelFormat, PageNumber, ShadingType, PageBreak, VerticalAlign } = require("docx");

const tableBorder = { style: BorderStyle.SINGLE, size: 1, color: "D9D9D9" };
const cb = { top: tableBorder, bottom: tableBorder, left: tableBorder, right: tableBorder };
const accent = "000000";

function headerCell(text, w) {
  return new TableCell({ borders: cb, verticalAlign: VerticalAlign.CENTER, margins: { top: 90, bottom: 90, left: 120, right: 120 }, width: { size: w, type: WidthType.DXA },
    shading: { fill: "E8F4FD", type: ShadingType.CLEAR },
    children: [new Paragraph({ children: [new TextRun({ text, bold: true })] })] });
}
function cell(text, w, opts) {
  const runs = typeof text === "string" ? [new TextRun(opts || {})] : text;
  if (typeof text === "string") runs[0] = new TextRun({ text, ...(opts || {}) });
  return new TableCell({ borders: cb, verticalAlign: VerticalAlign.CENTER, margins: { top: 90, bottom: 90, left: 120, right: 120 }, width: { size: w, type: WidthType.DXA },
    children: [new Paragraph({ children: runs })] });
}
function bullet(children) {
  return new Paragraph({ keepLines: true, numbering: { reference: "bl", level: 0 },
    children: Array.isArray(children) ? children : [new TextRun(children)] });
}
function step(ref, children) {
  return new Paragraph({ keepLines: true, numbering: { reference: ref, level: 0 },
    children: Array.isArray(children) ? children : [new TextRun(children)] });
}
// Keep section titles plain; version/date details are preserved in body text.
function headingText(text) { return text.replace(/^([一二三四五六])、/, "$1 ").replace(/^\d+\.\d+ /, "").replace(/^v(\d+)\.(\d+)\.(\d+)（.*）$/, "版本 $1 $2 $3"); }
function h1(t) { return new Paragraph({ heading: HeadingLevel.HEADING_1, children: [new TextRun(headingText(t))] }); }
function h2(t) { return new Paragraph({ heading: HeadingLevel.HEADING_2, children: [new TextRun(headingText(t))] }); }
function h3(t) { return new Paragraph({ heading: HeadingLevel.HEADING_3, children: [new TextRun(headingText(t))] }); }
function p(children, sp) {
  return new Paragraph({ keepLines: true, spacing: { after: sp || 120 },
    children: Array.isArray(children) ? children : [new TextRun(children)] });
}
function b(t) { return new TextRun({ text: t, bold: true }); }
function t(t) { return new TextRun(t); }
function code(t) { return new TextRun({ text: t, font: "Consolas", size: 20 }); }

const doc = new Document({
  styles: {
    default: { document: { run: { font: "Microsoft YaHei", size: 22 }, paragraph: { spacing: { line: 300, after: 100 }, widowControl: true } } },
    paragraphStyles: [
      { id: "Title", name: "Title", basedOn: "Normal",
        run: { size: 52, bold: true, color: "000000", font: "Microsoft YaHei" },
        paragraph: { keepNext: true, spacing: { before: 0, after: 200 }, alignment: AlignmentType.CENTER } },
      { id: "Heading1", name: "Heading 1", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 32, bold: true, color: accent, font: "Microsoft YaHei" },
        paragraph: { keepNext: true, spacing: { before: 360, after: 200 }, outlineLevel: 0 } },
      { id: "Heading2", name: "Heading 2", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 26, bold: true, color: "000000", font: "Microsoft YaHei" },
        paragraph: { keepNext: true, spacing: { before: 240, after: 160 }, outlineLevel: 1 } },
      { id: "Heading3", name: "Heading 3", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 24, bold: true, color: "000000", font: "Microsoft YaHei" },
        paragraph: { keepNext: true, spacing: { before: 200, after: 120 }, outlineLevel: 2 } },
    ]
  },
  numbering: { config: [
    { reference: "bl", levels: [{ level: 0, format: LevelFormat.BULLET, text: "\u2022", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 720, hanging: 360 } } } }] },
    { reference: "bl2", levels: [{ level: 0, format: LevelFormat.BULLET, text: "\u25E6", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 1080, hanging: 360 } } } }] },
    { reference: "s1", levels: [{ level: 0, format: LevelFormat.DECIMAL, text: "%1.", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 720, hanging: 360 } } } }] },
    { reference: "s2", levels: [{ level: 0, format: LevelFormat.DECIMAL, text: "%1.", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 720, hanging: 360 } } } }] },
    { reference: "s3", levels: [{ level: 0, format: LevelFormat.DECIMAL, text: "%1.", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 720, hanging: 360 } } } }] },
    { reference: "s4", levels: [{ level: 0, format: LevelFormat.DECIMAL, text: "%1.", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 720, hanging: 360 } } } }] },
    { reference: "c1", levels: [{ level: 0, format: LevelFormat.DECIMAL, text: "%1.", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 720, hanging: 360 } } } }] },
    { reference: "c2", levels: [{ level: 0, format: LevelFormat.DECIMAL, text: "%1.", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 720, hanging: 360 } } } }] },
  ] },
  sections: [
    // ===== COVER =====
    { properties: { page: { size: { width: 11906, height: 16838 }, margin: { top: 1440, right: 1273, bottom: 1440, left: 1273 } } },
      children: [
        new Paragraph({ spacing: { before: 3000 } }),
        new Paragraph({ heading: HeadingLevel.TITLE, children: [new TextRun({ text: "URflow 使用指南", size: 52, bold: true, color: accent })] }),
        new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 200 }, children: [t("Unity 动画贝塞尔曲线编辑器")] }),
        new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 100 }, children: [new TextRun({ text: "使用指南与更新记录", size: 28, color: "000000" })] }),
        new Paragraph({ spacing: { before: 1500 } }),
        new Paragraph({ alignment: AlignmentType.CENTER, children: [new TextRun({ text: "Inspired by Flow for After Effects", size: 20, color: "000000", italics: true })] }),
        new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 100 }, children: [new TextRun({ text: "适用版本 v1.0.3  文档更新 2026-10-30", size: 22, color: "000000" })] }),
        new Paragraph({ alignment: AlignmentType.CENTER, children: [new TextRun({ text: "功能建议与问题反馈 liuyiran02@corp.netease.com", size: 20, color: "555555" })] }),
      ]
    },
    // ===== MAIN CONTENT =====
    { properties: {
        page: { size: { width: 11906, height: 16838 }, margin: { top: 1440, right: 1273, bottom: 1440, left: 1273 }, pageNumbers: { start: 1 } }
      },
      headers: { default: new Header({ children: [new Paragraph({ alignment: AlignmentType.RIGHT, children: [new TextRun({ text: "URflow v1.0.3 使用指南", size: 18, color: "000000" })] })] }) },
      footers: { default: new Footer({ children: [new Paragraph({ alignment: AlignmentType.CENTER, children: [new TextRun({ text: "— ", size: 18, color: "AAAAAA" }), new TextRun({ children: [PageNumber.CURRENT], size: 18, color: "AAAAAA" }), new TextRun({ text: " —", size: 18, color: "AAAAAA" })] })] }) },
      children: [
        // 一、插件简介
        h1("一、插件简介"),
        p([t("URflow 是一款用于 Unity Editor 的"), b("三阶贝塞尔曲线编辑器"), t("，灵感来源于 After Effects 的 Flow 插件。它可以让动效设计师在 Unity 中直观地编辑 Animation Curve 的缓动曲线，无需手动调节 Keyframe 的切线参数。")]),

        h2("核心功能"),
        bullet("可视化贝塞尔曲线编辑器，支持拖拽控制点"),
        bullet([b("控制杆可超出坐标轴框"), t("，实现 Overshoot / Anticipation 效果")]),
        bullet([b("Shift + 拖拽水平吸附"), t("，快速创建平滑缓动")]),
        bullet("从 Animation Window 读取 / 应用曲线，支持 Dopesheet 和 Curves 视图"),
        bullet("CurveGuard 在权重模式被重置时尝试恢复已应用的曲线"),
        bullet("37 个内置预设：Standard 5 个、Penner 24 个、UI Motion 8 个"),
        bullet("自定义预设保存、导入、导出"),
        bullet("列表 / 网格视图切换"),
        bullet("Flow 风格深色主题，曲线渐变色显示"),

        h2("系统要求"),
        bullet("包声明最低 Unity 2022.3；仓库记录的测试版本为 2022.3.20f1，其他版本需验证"),
        bullet("Editor Only（不影响运行时性能）"),

        // 二、安装方法
        h1("二、安装方法"),
        p("UPM：打开 Window → Package Manager → + → Add package from git URL，填写："),
        p([code("https://github.com/Ryancaoye/URflow.git#v1.0.3")]),
        p("省略 tag 会跟随默认分支。v1.0.2 不包含后续 Apply / CurveGuard 修复，团队协作建议锁定 v1.0.3。"),
        p("离线：解压 URflow_v1.0.3.zip，将完整 URflow 文件夹放到 Assets/URflow；或选择 Add package from disk 并选中 URflow/package.json。保留 .meta 和图标，不要只复制脚本。"),
        p("等待编译完成后，通过 Window → URflow 或 Ctrl+Shift+E 打开。设置页的检查更新会打开 Package Manager，当前没有自动联网检查。"),

        h2("文件结构"),
        new Table({ columnWidths: [4680, 4680], rows: [
          new TableRow({ tableHeader: true, children: [headerCell("文件", 4680), headerCell("说明", 4680)] }),
          ...[
            ["URflowWindow.cs", "主窗口、曲线绘制与交互"],
            ["URflowApplyHelper.cs", "曲线应用到 Animation Window"],
            ["URflowReadHelper.cs", "从 Animation Window 读取曲线"],
            ["URflowWeightedHelper.cs", "保留的 Weighted 工具，按钮已隐藏"],
            ["URflowCurveGuard.cs", "检查权重模式重置并尝试恢复曲线"],
            ["URflowAnimHelper.cs", "反射访问 Animation Window 内部 API"],
            ["CubicBezierConverter.cs", "贝塞尔曲线数学计算"],
            ["BezierPreset.cs", "预设数据结构"],
            ["PresetLibrary.cs", "37 个内置预设"],
            ["PresetManager.cs", "用户预设管理"],
            ["Icons/", "自定义按钮图标（PNG）"],
          ].map(([f, d]) => new TableRow({ cantSplit: true, children: [
            cell(f, 4680, { font: "Consolas", size: 20 }), cell(d, 4680)
          ] }))
        ] }),

        // 三、界面说明
        h1("三、界面说明"),

        h2("3.1 曲线编辑区"),
        p("窗口中央的深色区域是曲线编辑器，显示当前贝塞尔曲线的形状。"),
        bullet([b("渐变曲线"), t("：从青绿色 (#5EF0B0) 渐变到蓝色 (#0071FF)")]),
        bullet([b("橙色控制点"), t("：P1（左下）和 P2（右上），可拖拽")]),
        bullet([b("黄色控制杆"), t("：从起点/终点到控制点的连线")]),
        bullet([b("网格线"), t("：4×4 网格辅助对齐")]),
        bullet([b("对角线"), t("：线性参考线（linear）")]),

        h2("3.2 参数输入"),
        p([t("曲线下方有四个数值输入框："), b("X1、Y1、X2、Y2"), t("，对应 CSS cubic-bezier(x1, y1, x2, y2) 格式。")]),
        bullet("X1/X2 范围：0 ~ 1"),
        bullet("Y1/Y2 数值输入与 READ：-0.5 ~ 1.5，可实现一定程度的 Overshoot / Anticipation"),
        p("拖拽代码允许 -2 ~ 3，但界面更新时数值框会再次限制到 -0.5 ~ 1.5，更大范围不能视为稳定支持。"),
        p("上方文本框填写四个逗号分隔的数字，例如 0.20, 0.00, 0.00, 1.00。粘贴 CSS 参数时去掉 cubic-bezier( 和末尾 )。"),

        h2("3.3 操作按钮"),
        new Table({ columnWidths: [2000, 7360], rows: [
          new TableRow({ tableHeader: true, children: [headerCell("按钮", 2000), headerCell("功能说明", 7360)] }),
          ...[
            ["READ", "读取第一条有效属性曲线的前两个选中关键帧"],
            ["APPLY", "在每条有效属性曲线上，按时间将选中帧两两相邻配对并应用"],
            ["SAVE", "保存当前曲线到 My Presets（弹出命名对话框）"],
          ].map(([a, d]) => new TableRow({ cantSplit: true, children: [
            cell(a, 2000, { bold: true, font: "Consolas" }), cell(d, 7360)
          ] }))
        ] }),

        p("当前操作栏为 READ、APPLY、SAVE，没有独立的方向按钮或 Weighted 按钮。"),
        h2("3.4 预设浏览"),
        p("下方预设区域有三个选项卡："),
        bullet([b("ALL"), t(" — 显示所有内置 + 用户预设")]),
        bullet([b("FAVORITES"), t(" — 收藏的预设")]),
        bullet([b("MY PRESETS"), t(" — 用户自定义预设，带导入/导出/删除操作栏")]),
        p([t("搜索栏右侧可切换"), b("列表视图 / 网格视图"), t("。收藏入口在列表中预设左侧。分类为 Standard、Penner 和 UI Motion，当前没有 Material 3 分类按钮。")]),

        // 四、使用流程
        h1("四、使用流程"),

        h2("4.1 基本工作流"),
        step("s2", "在 Animation Window 同一属性曲线上选择至少两个关键帧；首次使用建议选择相邻的两个帧"),
        step("s2", "打开 URflow 窗口（Window → URflow）"),
        step("s2", [t("点击 "), b("READ"), t(" 读取当前关键帧的曲线")]),
        step("s2", "拖拽控制点或选择预设调整曲线"),
        step("s2", [t("点击 "), b("APPLY"), t(" 应用后，在 Unity 中预览动画")]),
        p("APPLY 自动设置 Broken / Free 和权重，通常无需手动设置 Weighted。可同时处理多条属性曲线，但每条需要至少两个选中帧。若中间还有未选中的帧，请逐段处理并检查结果；READ 不会汇总多段曲线。"),

        h2("4.2 快捷操作"),
        new Table({ columnWidths: [3500, 5860], rows: [
          new TableRow({ tableHeader: true, children: [headerCell("操作", 3500), headerCell("效果", 5860)] }),
          ...[
            ["拖拽控制点", "调整曲线形状"],
            ["Shift + 拖拽", "锁定水平方向（P1 锁 Y=0，P2 锁 Y=1）"],
            ["应用预设", "单击加载后点击 APPLY；双击存在事件处理问题，待 Unity 验证"],
            ["单击预设", "加载到编辑器预览"],
            ["输入数值", "在 X1/Y1/X2/Y2 输入框精确设置"],
          ].map(([a, d]) => new TableRow({ cantSplit: true, children: [
            cell(a, 3500, { bold: true }), cell(d, 5860)
          ] }))
        ] }),

        h2("4.3 保存自定义预设"),
        step("s3", "调整好满意的曲线"),
        step("s3", [t("点击 "), b("SAVE"), t(" 按钮")]),
        step("s3", "在弹窗中输入预设名称，点击保存或按 Enter"),
        step("s3", "预设自动保存到 MY PRESETS 选项卡"),

        h2("4.4 预设导入与导出"),
        p([t("切换到 "), b("MY PRESETS"), t(" 选项卡后，顶部出现三个操作按钮：")]),
        bullet([b("导入"), t("：从 JSON 文件导入预设")]),
        bullet([b("导出"), t("：将所有用户预设导出为 JSON 文件")]),
        bullet([b("删除"), t("：删除选中的预设（弹出确认对话框）")]),
        p("导入采用追加方式，不自动去重。收藏和删除按名称识别，删除时会移除全部同名自定义预设。建议使用不同名称，搜索或切换选项卡后重新点选目标。"),
        p("自定义预设保存在工程的 ProjectSettings/URflow/UserPresets.json，收藏保存在同目录 Favorites.json。JSON 导出不包含独立收藏名单。语言使用本机 EditorPrefs 的 URflow_Language。"),

        // 五、常见问题
        h1("五、常见问题"),
        bullet([b("Q: APPLY 没反应？")]),
        new Paragraph({ numbering: { reference: "bl2", level: 0 }, children: [t("确保 Animation Window 已打开，同一属性曲线上至少选中两个帧，且当前 AnimationClip 可编辑")] }),
        bullet([b("Q: READ 读取的曲线不对？")]),
        new Paragraph({ numbering: { reference: "bl2", level: 0 }, children: [t("先仅选一条属性曲线上的两个相邻帧。READ 只读第一组；等值端点返回默认线性参数，单侧 Weighted 或阶梯切线可能无法准确还原")] }),
        bullet([b("Q: 想要 Overshoot 效果？")]),
        new Paragraph({ numbering: { reference: "bl2", level: 0 }, children: [t("可将 Y1 设为负值或 Y2 设为大于 1；当前数值范围为 -0.5 ~ 1.5，Spring 和 Back In Out 的部分参数会被截断")] }),
        bullet([b("Q: 预设文件保存在哪？")]),
        new Paragraph({ numbering: { reference: "bl2", level: 0 }, children: [t("工程的 ProjectSettings/URflow/UserPresets.json 与 Favorites.json；备份或迁移工程时保留")] }),

        h2("CurveGuard 的保护范围"),
        p("本次编辑器会话中，经 APPLY 注册的曲线会被检查，仅在相关权重模式缺失时恢复。脚本重载或重新打开 Unity 后，保护登记不会保留；曲线本身仍保存在 AnimationClip 中。"),
        p("CurveGuard 按索引追踪。增删帧、跨帧拖动、Undo / Redo 和更换 Unity 版本后应检查结果。自动恢复会记录 Undo，但这些边界行为仍需验证。"),

        // ===== PAGE BREAK =====
        // Let the update history follow naturally to avoid an almost-empty page.

        // 六、更新记录
        h1("六、更新记录"),
        h2("文档同步 2026年9月10日"),
        p("按 v1.0.3 源码校正预设数量、保存位置、安装路径、参数输入和按钮说明，补充 Read / Apply / CurveGuard 限制。此次仅更新文档，相关代码问题尚未修复或完成 Unity 回归验证。"),
        h2("2026年9月3日发布"),
        p("版本 v1.0.3"),
        bullet("统一 4 月 16 日至 22 日已合入改动的版本号，并发布稳定 tag。"),
        bullet("修复首次 APPLY 不生效：先设置 Broken / Free，再写入切线和权重。"),
        bullet("APPLY 自动设置 Broken / Free；CurveGuard 仅在权重模式重置时恢复，避免直接覆盖手工切线调整。"),
        bullet("修复 UPM 图标路径，补齐 .meta，调整 LOGO 和 UI 版本显示。"),
        bullet("移除自动更新检查，设置页改为打开 Package Manager。"),
        h2("2026年4月15日发布"),
        p("版本 v1.0.2"),
        bullet("新增 CurveGuard，在 APPLY 后登记关键帧对，检查权重模式重置并尝试恢复曲线。"),
        bullet("此 tag 不包含 4 月 16 日的后续修复，当前安装建议使用 v1.0.3。"),
        p("以下为早期版本记录，当前功能和限制以前文说明为准。"),

        h2("2026年4月14日功能更新"),
        p("版本 v1.0.1"),

        h3("新增功能"),
        bullet([b("20:45:21 SAVE 按钮"), t(" — 保存当前曲线到 My Presets，弹窗命名")]),
        bullet([b("18:25:58 MY PRESETS 操作栏"), t(" — 导入/导出/删除三按钮")]),
        bullet([b("20:09:48 控制杆超出坐标轴"), t(" — 控制点和曲线可自由画到框外")]),
        bullet([b("20:27:16 Shift + 拖拽水平吸附"), t(" — P1 锁 Y=0，P2 锁 Y=1")]),
        bullet([b("16:29:00 网格视图模式"), t(" — 方形曲线预览 + 名称")]),
        bullet([b("16:40:00 设置页面"), t(" — 全屏卡片式布局")]),
        bullet([b("18:33:29 自定义图标系统"), t(" — PNG 图标加载 + 缓存")]),

        h3("界面优化"),
        bullet([b("16:29:00 Flow 风格深色主题"), t(" — 深灰背景 #232323")]),
        bullet([b("12:47:00 曲线渐变色"), t(" — #5EF0B0 → #0071FF")]),
        bullet([b("16:29:00 APPLY 按钮渐变"), t(" — 与曲线相同的渐变纹理")]),
        bullet([b("11:10:00 曲线线条加粗"), t(" — 主曲线 ~3px，控制杆 ~2px")]),
        bullet([b("16:29:00 选项卡高亮"), t(" — 选中态 #FFB826 黄色底色")]),
        bullet([b("16:40:00 X1/Y1/X2/Y2 改为 FloatField"), t(" — 替代 Slider")]),
        bullet([b("10:50:00 所有英文大写加粗"), t(" — URFLOW / READ / APPLY 等")]),
        bullet([b("10:37:00 中文 Tooltip"), t(" — 所有按钮悬浮显示中文功能说明")]),
        bullet([b("12:48:58 LOGO 图片"), t(" — 替代文字标题")]),
        bullet([b("18:41:49 Settings 无底板图标"), t(" — 左下角齿轮图标")]),

        h3("Bug 修复"),
        bullet([b("16:29:00 GPU 内存泄漏修复"), t(" — 静态缓存 Texture2D，修复 D3D11 swapchain 崩溃")]),
        bullet([b("16:40:00 缩略图曲线溢出修复"), t(" — GL 不受 BeginClip 约束，改用 DrawRect")]),
        bullet([b("16:50:00 陡峭曲线断续修复"), t(" — DrawCurveInRect 插值填充")]),
        bullet([b("16:29:00 设置栏被遮挡修复"), t(" — 改为正常布局流 + FlexibleSpace")]),
        bullet([b("20:31:03 拖拽被 UI 拦截修复"), t(" — GUIUtility.hotControl 锁定")]),
        bullet([b("20:07:15 Mathf.Lerp clamp 修复"), t(" — 改为 LerpUnclamped 支持超出范围")]),

        h2("2026年4月14日首个版本"),
        p("版本 v1.0.0"),

        h3("核心功能"),
        bullet([b("10:37:00 可视化曲线编辑器"), t(" — GL 绘制贝塞尔曲线、拖拽控制点")]),
        bullet([b("10:37:00 cubic-bezier 参数输入"), t(" — X1/Y1/X2/Y2 滑块输入")]),
        bullet([b("12:00:00 内置预设"), t(" — Standard / Penner / UI Motion 分类；当前 v1.0.3 共 37 个")]),
        bullet([b("13:30:00 APPLY 按钮"), t(" — 一键应用曲线到 Animation Window 关键帧")]),
        bullet([b("14:00:00 READ 按钮"), t(" — 从选中关键帧读取当前曲线值")]),
        bullet([b("15:00:00 自定义预设系统"), t(" — 保存/加载/导出（JSON 格式）")]),
        bullet([b("15:30:00 Undo 支持"), t(" — 动画曲线写入记录 Undo；预设 JSON 文件操作不属于动画撤销记录")]),
        bullet([b("15:30:00 收藏功能"), t(" — 预设可标记为收藏")]),
        bullet([b("16:00:00 预设搜索"), t(" — 按名称实时搜索过滤")]),

        h3("技术实现"),
        bullet([b("反射访问 Animation Window 内部 API"), t(" — AnimEditor → state → selectedKeys / activeCurves")]),
        bullet([b("cubic-bezier → Unity tangent 转换"), t(" — outWeight=x1, inWeight=1-x2, tangent=(y/x)*(valueRange/duration)")]),
        bullet([b("Deferred Dialog 模式"), t(" — 避免 OnGUI 中调用 DisplayDialog 破坏 GUILayout 状态")]),
        bullet([b("Dopesheet + Curves 双模式支持"), t(" — URflowAnimHelper 统一封装")]),

        h3("Bug 修复"),
        bullet([b("GL.LINE_STRIP 不支持修复"), t(" — Unity 2022.3 不支持 GL.LINE_STRIP/GL.TRIANGLE_FAN，改用 GL.LINES 逐段画线 + GL.TRIANGLES 画圆")]),
        bullet([b("AmbiguousMatchException 修复"), t(" — 反射 GetMethod 改为 GetMethods() 手动匹配参数类型")]),
        bullet([b("关键帧索引获取修复"), t(" — AnimationWindowKeyframe 无 index 属性，改用 time 值匹配")]),
        bullet([b("DisplayDialog 崩溃修复"), t(" — 从 OnGUI 移到 Update() 中延迟弹出")]),
        bullet([b("曲线数据缓存过期修复"), t(" — GetFreshCurve() 每次从 clip 重新读取")]),
        bullet([b("Curves 视图兼容性修复"), t(" — 新增 URflowAnimHelper 统一 Dopesheet/Curves 双模式")]),

        h3("已知限制"),
        bullet("仅支持 Editor 模式（不影响运行时性能）"),
        bullet("Curves 视图下的选中 API 仍在完善中"),
        p("初始版本发布。"),

        h2("2026年10月29日至30日开发记录"),
        p("目标版本 v1.0.4，开发中，尚未正式发布。教程正文仍对应正式版 v1.0.3。以下记录区分功能实现、Unity 视觉集成与验证状态。"),
        h3("功能开发与兼容修复"),
        bullet("2026年10月29日：建立独立开发分支和 worktree，采用小步验证、阶段保存及正式发版归档的开发流程。新工具分离为宿主入口、面板、曲线计算和 Unity 读写模块。"),
        bullet("关键帧移动已在开发版实现：仅处理 Animation 窗口选中的位置或缩放属性，将整条曲线的首帧或尾帧对齐未受动画驱动的静态默认值。默认整体平移；勾选比例调整后，位置和缩放都可按比例变化。"),
        bullet("支持 Undo 和 Redo。首尾时间不变；平移保持数值差和原切线，比例调整同步缩放数值和切线。零基准无法按比例对齐到非零目标时拒绝应用；透明度暂不纳入。"),
        bullet("2026年10月30日：删除插件中被赋值但未使用的字段，以兼容制作工程将警告视为错误的编译规则。严格纯编译检查通过，用户确认工程可正常启动；未修改制作工程原有代码或资产。"),
        p("关键帧移动此前已通过独立 Unity 工程的曲线计算、服务及部分界面交互检查。本轮更新了视觉和工具入口，实际回归结果单独列出；制作场景效果仍待试用验收。"),
        h3("界面设计与文案"),
        p("以下视觉改动已接入 v1.0.4 开发包，代码保存于开发分支提交 32a6bd6。本轮已执行独立 Unity 回归；外观像素检查未执行，制作工程仍待 Unity 刷新与场景试用。尚未正式发布。"),
        bullet("保留原 Logo 与图标，采用中性深灰、青绿到蓝的主按钮渐变和琥珀选中态；收紧 Logo 留白，X1、Y1、X2、Y2 与数值保持同排，并保留四数值粘贴框。预设使用琥珀下划线选中态，预设区和底部工具区通过底色与分隔线区分。"),
        bullet("底部工具栏高 84 px，关键帧移动为单个 44×44 px 图标，名称显示在工具提示中。详情在同窗底部展开，首选高度 420 px 并随可用空间缩小。工具数量溢出时支持鼠标拖动与滚轮横移；边缘流光在打开或成功应用时扫过一次。"),
        bullet("曲线图常规高度为 260 px，较矮窗口自动缩小以保留预设与底栏空间。主线宽 1.8 px，采用高饱和绿色 #20E7A1、青色 #12CDE0 到蓝色 #168CFF 的渐变，保留柔和光晕。"),
        bullet("面板展开时长固定为 400 ms，边缘流光为 1900 ms。内部固定，不提供时长参数、动画设置面板或相关入口。"),
        bullet("主按钮及预设区域沿用默认英文 APPLY、ALL、FAVORITES、MY PRESETS，搜索提示为 Search presets...。APPLY 使用 15 px 白色粗体，位置、大小和圆角与整体控件统一。"),
        h3("本轮验证结果"),
        p("严格编译及独立 Unity 的数学、服务和实际界面交互检查通过；外观像素检查未执行，制作场景仍待试用。"),
        h3("关键帧移动操作"),
        p("在 Animation 窗口选择需要处理的位置或缩放属性，点击底部 Move 图标，选择 First Frame 或 Last Frame。默认整体偏移；需要整体比例缩放时勾选 Proportional adjustment。点击 Refresh 检查只读预览，再点击 Apply Move；完成后可使用 Undo 或 Redo。仅处理选中的属性，其全部关键帧一起调整；零基准的比例限制按前述规则处理。"),
        h3("持续更新记录规范"),
        p("每个完成的修改阶段都保存日期、目标版本、修改类别、内容与原因、当前状态、验证结果和对应提交。功能、修复、界面、文案、教程和流程规范均纳入记录。同一目标的连续微调可汇总最终确认效果，详细过程保留在开发记录中。"),
        p("统一版本日志维护在 CHANGELOG.md。开发阶段归入 Unreleased，并同步本教程末尾的更新记录、中文 Markdown 和 Word 生成脚本，再同步到整理目录。正式验收和发版后才标注发布日期；纯文档更新不提升插件代码版本。"),

        h2("2026年10月30日光晕与面板反馈修复"),
        p("用户此次截图确认上一轮新界面已在制作工程显示，同时反馈曲线光晕不明显、关键帧移动面板展开与确认预览不同，且底部 Refresh 和 Apply Move 按钮被裁切。本轮按确认预览修正绘制、动画和面板布局。"),
        p("曲线保留 1.8 px 的高饱和绿青蓝核心，并增加柔化光晕。面板从最终位置下方 24 px 轻升展开，时长仍为 400 ms；边缘改为 105° 斜亮带横扫一次，时长为 1900 ms，并保留低亮边缘。动画参数继续使用内部固定值。"),
        p("面板标题和底部操作栏固定在可见范围内，只有中间内容滚动。打开面板时主内容变暗，Logo 与底部工具带保持正常亮度，背景操作继续隔离。"),
        p("上一轮编译与实际界面事件检查通过，但未覆盖底部操作栏的完整显示和外观像素。本次反馈触发补充页脚几何、展开时序与离屏渲染检查；前一阶段记录保留。"),
        p("严格编译及独立 Unity 的数学、服务、实际界面交互、页脚几何与展开时序检查通过，生产曲线绘制的离屏像素检查通过。助手未取得整窗像素截图；本轮修复仍待用户刷新后检查整窗外观和制作场景效果。"),
        p("目标版本 v1.0.4 尚未发布，版本字段仍为 1.0.3。本轮修复保存于开发分支提交 01c1dba，已同步到开发包。教程与更新记录随对应文档提交保存，具体提交见 Git 历史。"),

      ]
    }
  ]
});

// Optional first argument selects the output directory.
const defaultDir = path.basename(__dirname) === "_生成脚本" ? path.dirname(__dirname) : __dirname;
const outputDir = path.resolve(process.argv[2] || defaultDir);
const outputFile = path.join(outputDir, "URflow_v1.0.3_使用指南与更新记录.docx");
Packer.toBuffer(doc).then(buf => {
  fs.mkdirSync(outputDir, { recursive: true });
  fs.writeFileSync(outputFile, buf);
  console.log(outputFile);
}).catch(error => { console.error(error); process.exitCode = 1; });
