const fs = require("fs");
const { Document, Packer, Paragraph, TextRun, Table, TableRow, TableCell,
        Header, Footer, AlignmentType, HeadingLevel, BorderStyle, WidthType,
        LevelFormat, PageNumber, ShadingType, PageBreak } = require("docx");

const tableBorder = { style: BorderStyle.SINGLE, size: 1, color: "CCCCCC" };
const cb = { top: tableBorder, bottom: tableBorder, left: tableBorder, right: tableBorder };
const accent = "0071FF";

function headerCell(text, w) {
  return new TableCell({ borders: cb, width: { size: w, type: WidthType.DXA },
    shading: { fill: "E8F4FD", type: ShadingType.CLEAR },
    children: [new Paragraph({ children: [new TextRun({ text, bold: true })] })] });
}
function cell(text, w, opts) {
  const runs = typeof text === "string" ? [new TextRun(opts || {})] : text;
  if (typeof text === "string") runs[0] = new TextRun({ text, ...(opts || {}) });
  return new TableCell({ borders: cb, width: { size: w, type: WidthType.DXA },
    children: [new Paragraph({ children: runs })] });
}
function bullet(children) {
  return new Paragraph({ numbering: { reference: "bl", level: 0 },
    children: Array.isArray(children) ? children : [new TextRun(children)] });
}
function step(ref, children) {
  return new Paragraph({ numbering: { reference: ref, level: 0 },
    children: Array.isArray(children) ? children : [new TextRun(children)] });
}
function h1(t) { return new Paragraph({ heading: HeadingLevel.HEADING_1, children: [new TextRun(t)] }); }
function h2(t) { return new Paragraph({ heading: HeadingLevel.HEADING_2, children: [new TextRun(t)] }); }
function h3(t) { return new Paragraph({ heading: HeadingLevel.HEADING_3, children: [new TextRun(t)] }); }
function p(children, sp) {
  return new Paragraph({ spacing: { after: sp || 120 },
    children: Array.isArray(children) ? children : [new TextRun(children)] });
}
function b(t) { return new TextRun({ text: t, bold: true }); }
function t(t) { return new TextRun(t); }
function code(t) { return new TextRun({ text: t, font: "Consolas", size: 20 }); }

const doc = new Document({
  styles: {
    default: { document: { run: { font: "Microsoft YaHei", size: 22 } } },
    paragraphStyles: [
      { id: "Title", name: "Title", basedOn: "Normal",
        run: { size: 52, bold: true, color: "000000", font: "Microsoft YaHei" },
        paragraph: { spacing: { before: 0, after: 200 }, alignment: AlignmentType.CENTER } },
      { id: "Heading1", name: "Heading 1", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 32, bold: true, color: accent, font: "Microsoft YaHei" },
        paragraph: { spacing: { before: 360, after: 200 }, outlineLevel: 0 } },
      { id: "Heading2", name: "Heading 2", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 26, bold: true, color: "333333", font: "Microsoft YaHei" },
        paragraph: { spacing: { before: 240, after: 160 }, outlineLevel: 1 } },
      { id: "Heading3", name: "Heading 3", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 24, bold: true, color: "555555", font: "Microsoft YaHei" },
        paragraph: { spacing: { before: 200, after: 120 }, outlineLevel: 2 } },
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
    { properties: { page: { margin: { top: 1440, right: 1440, bottom: 1440, left: 1440 } } },
      children: [
        new Paragraph({ spacing: { before: 3000 } }),
        new Paragraph({ heading: HeadingLevel.TITLE, children: [new TextRun({ text: "URflow v1.0.1", size: 72, bold: true, color: accent })] }),
        new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 200 }, children: [t("Unity 动画贝塞尔曲线编辑器")] }),
        new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 100 }, children: [new TextRun({ text: "使用指南 & 更新记录", size: 28, color: "999999" })] }),
        new Paragraph({ spacing: { before: 1500 } }),
        new Paragraph({ alignment: AlignmentType.CENTER, children: [new TextRun({ text: "Inspired by Flow for After Effects", size: 20, color: "BBBBBB", italics: true })] }),
        new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 100 }, children: [new TextRun({ text: "2026.04.14", size: 22, color: "999999" })] }),
        new Paragraph({ alignment: AlignmentType.CENTER, children: [new TextRun({ text: "功能建议 & Bugs: liuyiran02@corp.netease.com", size: 20, color: "AAAAAA" })] }),
      ]
    },
    // ===== MAIN CONTENT =====
    { properties: {
        page: { margin: { top: 1440, right: 1440, bottom: 1440, left: 1440 }, pageNumbers: { start: 1 } }
      },
      headers: { default: new Header({ children: [new Paragraph({ alignment: AlignmentType.RIGHT, children: [new TextRun({ text: "URflow v1.0.1 使用指南", size: 18, color: "AAAAAA" })] })] }) },
      footers: { default: new Footer({ children: [new Paragraph({ alignment: AlignmentType.CENTER, children: [new TextRun({ text: "— ", size: 18, color: "AAAAAA" }), new TextRun({ children: [PageNumber.CURRENT], size: 18, color: "AAAAAA" }), new TextRun({ text: " —", size: 18, color: "AAAAAA" })] })] }) },
      children: [
        // 一、插件简介
        h1("一、插件简介"),
        p([t("URflow 是一款用于 Unity Editor 的"), b("三阶贝塞尔曲线编辑器"), t("，灵感来源于 After Effects 的 Flow 插件。它可以让动效设计师在 Unity 中直观地编辑 Animation Curve 的缓动曲线，无需手动调节 Keyframe 的切线参数。")]),

        h2("核心功能"),
        bullet("可视化贝塞尔曲线编辑器，支持拖拽控制点"),
        bullet([b("控制杆可超出坐标轴框"), t("，实现 Overshoot / Anticipation 效果")]),
        bullet([b("Shift + 拖拽水平吸附"), t("，快速创建平滑缓动")]),
        bullet("一键从 Animation Window 读取 / 应用曲线"),
        bullet("60+ 内置缓动预设（Penner / CSS / UI Motion）"),
        bullet("自定义预设保存、导入、导出"),
        bullet("列表 / 网格视图切换"),
        bullet("Flow 风格深色主题，曲线渐变色显示"),

        h2("系统要求"),
        bullet("Unity 2022.3 LTS 及以上版本"),
        bullet("Editor Only（不影响运行时性能）"),

        // 二、安装方法
        h1("二、安装方法"),
        step("s1", [t("复制 "), b("URflow/Editor/"), t(" 文件夹到你的 Unity 项目的 "), b("Assets/"), t(" 目录下")]),
        step("s1", "等待 Unity 编译完成"),
        step("s1", [t("菜单栏 → "), b("Window → URflow"), t(" 打开编辑器窗口")]),

        h2("文件结构"),
        new Table({ columnWidths: [4680, 4680], rows: [
          new TableRow({ tableHeader: true, children: [headerCell("文件", 4680), headerCell("说明", 4680)] }),
          ...[
            ["URflowWindow.cs", "主窗口 UI（~1200 行）"],
            ["URflowApplyHelper.cs", "曲线应用到 Animation Window"],
            ["URflowReadHelper.cs", "从 Animation Window 读取曲线"],
            ["URflowWeightedHelper.cs", "设置关键帧 Weighted 模式"],
            ["URflowAnimHelper.cs", "反射访问 Animation Window 内部 API"],
            ["CubicBezierConverter.cs", "贝塞尔曲线数学计算"],
            ["BezierPreset.cs", "预设数据结构"],
            ["PresetLibrary.cs", "内置预设库（60+）"],
            ["PresetManager.cs", "用户预设管理"],
            ["Icons/", "自定义按钮图标（PNG）"],
          ].map(([f, d]) => new TableRow({ children: [
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
        bullet("Y1/Y2 范围：-2 ~ 3（允许 Overshoot）"),

        h2("3.3 操作按钮"),
        new Table({ columnWidths: [2000, 7360], rows: [
          new TableRow({ tableHeader: true, children: [headerCell("按钮", 2000), headerCell("功能说明", 7360)] }),
          ...[
            ["READ", "从 Animation Window 中选中的关键帧读取当前曲线参数"],
            ["APPLY", "将当前编辑器中的曲线应用到 Animation Window 选中的关键帧"],
            ["SAVE", "保存当前曲线到 My Presets（弹出命名对话框）"],
          ].map(([a, d]) => new TableRow({ children: [
            cell(a, 2000, { bold: true, font: "Consolas" }), cell(d, 7360)
          ] }))
        ] }),

        h2("3.4 预设浏览"),
        p("下方预设区域有三个选项卡："),
        bullet([b("ALL"), t(" — 显示所有内置 + 用户预设")]),
        bullet([b("FAVORITES"), t(" — 收藏的预设")]),
        bullet([b("MY PRESETS"), t(" — 用户自定义预设，带导入/导出/删除操作栏")]),
        p([t("搜索栏右侧可切换"), b("列表视图 / 网格视图"), t("。")]),

        // 四、使用流程
        h1("四、使用流程"),

        h2("4.1 基本工作流"),
        step("s2", "在 Animation Window 中选中需要调整的关键帧"),
        step("s2", "打开 URflow 窗口（Window → URflow）"),
        step("s2", [t("点击 "), b("READ"), t(" 读取当前关键帧的曲线")]),
        step("s2", "拖拽控制点或选择预设调整曲线"),
        step("s2", [t("点击 "), b("APPLY"), t(" 应用到关键帧")]),

        h2("4.2 快捷操作"),
        new Table({ columnWidths: [3500, 5860], rows: [
          new TableRow({ tableHeader: true, children: [headerCell("操作", 3500), headerCell("效果", 5860)] }),
          ...[
            ["拖拽控制点", "调整曲线形状"],
            ["Shift + 拖拽", "锁定水平方向（P1 锁 Y=0，P2 锁 Y=1）"],
            ["双击预设", "立即应用该预设到选中关键帧"],
            ["单击预设", "加载到编辑器预览"],
            ["输入数值", "在 X1/Y1/X2/Y2 输入框精确设置"],
          ].map(([a, d]) => new TableRow({ children: [
            cell(a, 3500, { bold: true }), cell(d, 5860)
          ] }))
        ] }),

        h2("4.3 保存自定义预设"),
        step("s3", "调整好满意的曲线"),
        step("s3", [t("点击 "), b("SAVE"), t(" 按钮")]),
        step("s3", "在弹窗中输入预设名称，点击保存或按 Enter"),
        step("s3", "预设自动保存到 MY PRESETS 选项卡"),

        h2("4.4 预设导入 / 导出"),
        p([t("切换到 "), b("MY PRESETS"), t(" 选项卡后，顶部出现三个操作按钮：")]),
        bullet([b("导入"), t("：从 JSON 文件导入预设")]),
        bullet([b("导出"), t("：将所有用户预设导出为 JSON 文件")]),
        bullet([b("删除"), t("：删除选中的预设（弹出确认对话框）")]),

        // 五、常见问题
        h1("五、常见问题"),
        bullet([b("Q: APPLY 没反应？")]),
        new Paragraph({ numbering: { reference: "bl2", level: 0 }, children: [t("确保 Animation Window 已打开且选中了关键帧")] }),
        bullet([b("Q: READ 读取的曲线不对？")]),
        new Paragraph({ numbering: { reference: "bl2", level: 0 }, children: [t("建议先将关键帧设为 Weighted 模式（Animation Window 右键关键帧）")] }),
        bullet([b("Q: 想要 Overshoot 效果？")]),
        new Paragraph({ numbering: { reference: "bl2", level: 0 }, children: [t("把 Y1 设为负值或 Y2 设为大于 1 的值")] }),
        bullet([b("Q: 预设文件保存在哪？")]),
        new Paragraph({ numbering: { reference: "bl2", level: 0 }, children: [t("EditorPrefs 中，路径为 URflow_UserPresets，JSON 格式")] }),

        // ===== PAGE BREAK =====
        new Paragraph({ children: [new PageBreak()] }),

        // 六、更新记录
        h1("六、更新记录"),

        h2("v1.0.1（2026-04-14）"),

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

        h2("v1.0.0（2026-04-13）"),

        h3("核心功能"),
        bullet([b("10:37:00 可视化曲线编辑器"), t(" — GL 绘制贝塞尔曲线、拖拽控制点")]),
        bullet([b("10:37:00 cubic-bezier 参数输入"), t(" — X1/Y1/X2/Y2 滑块输入")]),
        bullet([b("12:00:00 40+ 内置预设"), t(" — Standard / Penner / UI Motion 分类")]),
        bullet([b("13:30:00 APPLY 按钮"), t(" — 一键应用曲线到 Animation Window 关键帧")]),
        bullet([b("14:00:00 READ 按钮"), t(" — 从选中关键帧读取当前曲线值")]),
        bullet([b("15:00:00 自定义预设系统"), t(" — 保存/加载/导出（JSON 格式）")]),
        bullet([b("15:30:00 Undo 支持"), t(" — 所有操作可撤销")]),
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
      ]
    }
  ]
});

Packer.toBuffer(doc).then(buf => {
  fs.writeFileSync("C:/Users/liuyiran02/lobsterai/project/URflow/docs/URflow_v1.0.1_Guide_v2.docx", buf);
  console.log("Done! File saved.");
});
