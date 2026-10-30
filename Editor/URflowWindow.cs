using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace URflow
{
    public class URflowWindow : EditorWindow
    {
        // ── Curve State ──
        private float _x1 = 0.25f, _y1 = 0.10f, _x2 = 0.25f, _y2 = 1.00f;
        private string _paramInput = "";

        // ── Drag ──
        private int _dragging = 0; // 0=none, 1=P1, 2=P2

        // ── Presets ──
        private List<BezierPreset> _builtInPresets;
        private List<BezierPreset> _userPresets;
        private HashSet<string> _favorites;
        private string _selectedCategory = "All";
        private string _searchQuery = "";
        private Vector2 _presetScrollPos;
        private int _selectedPresetIndex = -1;
        private int _filter = 0; // 0=All, 1=Favorites, 2=UserPresets
        private int _viewMode = 0; // 0=List, 1=Grid
        private URflowToolsHost _toolsHost;

        // ── Layout ──
        private const float Radius = 7f;
        private const float Pad = 16f;
        private Rect _curveInnerRect;
        private double _edgeStarted = -1d;

        // ── Colors (Flow-inspired Theme) ──
        private static readonly Color GridCol = new Color(0.22f, 0.22f, 0.22f);
        private static readonly Color CurveCol = new Color(0.95f, 0.95f, 0.95f);
        private static readonly Color HandleCol = new Color(1.00f, 0.72f, 0.15f, 0.55f);
        private static readonly Color P1Col = new Color(1.00f, 0.72f, 0.15f);
        private static readonly Color P2Col = new Color(1.00f, 0.72f, 0.15f);
        private static readonly Color DiagCol = new Color(0.22f, 0.22f, 0.22f);
        private static readonly Color PNorm = new Color(0.18f, 0.18f, 0.18f);
        private static readonly Color PHover = new Color(0.24f, 0.24f, 0.24f);
        private static readonly Color PSel = new Color(0.28f, 0.28f, 0.30f);
        private static readonly Color FavCol = new Color(1.00f, 0.80f, 0.15f);

        private static Material _mat;

        // ── Logo ──
        private Texture2D _logoTex;

        // ── Settings ──
        private bool _showSettings = false;
        private bool _showSaveDialog = false;
        private string _savePresetName = "";
        private const string VERSION = "1.0.3";
        private static readonly string PREF_LANG = "URflow_Language"; // 0=EN, 1=CN

        private static int _lang = -1; // lazy init
        private static int Lang
        {
            get
            {
                if (_lang < 0) _lang = EditorPrefs.GetInt(PREF_LANG, 0);
                return _lang;
            }
            set
            {
                _lang = value;
                EditorPrefs.SetInt(PREF_LANG, value);
            }
        }

        // Localization helper: L("EN text", "中文文本")
        private static string L(string en, string cn) { return Lang == 0 ? en : cn; }

        [MenuItem("Window/URflow %#E")]
        public static void ShowWindow()
        {
            URflowWindow w = GetWindow<URflowWindow>("URFLOW");
            w.minSize = new Vector2(320, 560);
        }

        private void OnEnable()
        {
            if (_toolsHost != null) _toolsHost.Dispose();
            _toolsHost = new URflowToolsHost(Repaint);
            wantsMouseMove = true;
            wantsMouseEnterLeaveWindow = true;
            _builtInPresets = PresetLibrary.GetBuiltInPresets();
            _userPresets = PresetManager.LoadUserPresets();
            _favorites = PresetManager.LoadFavorites();
            SyncParam();
        }

        private void OnDisable()
        {
            if (_toolsHost != null) _toolsHost.Dispose();
            _toolsHost = null;
            URflowVisuals.Dispose();
            if (_mat != null) DestroyImmediate(_mat);
            _mat = null;
        }

        private void OnFocus()
        {
            if (_toolsHost != null && !_showSettings) _toolsHost.RefreshIfOpen();
        }

        private void Update()
        {
            // Handle deferred dialogs outside of OnGUI
            URflowApplyHelper.CheckPendingDialog();
        }

        private void OnGUI()
        {
            // Repaint on mouse move so hover states update instantly
            if (Event.current.type == EventType.MouseMove)
                Repaint();

            // When save dialog is open, it will be drawn as a modal window
            // No need to block events here - GUI.ModalWindow handles it

            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height),
                URflowVisuals.Background);

            if (_showSettings)
            {
                DrawSettingsPage();
                return;
            }

            if (_showSaveDialog)
            {
                // Draw main UI visually but disabled (no interaction)
                using (new EditorGUI.DisabledScope(true)) DrawMainContent();
                DrawSaveDialog();
                return;
            }

            DrawMainContent();
            if (URflowVisuals.DrawEdgeSweep(new Rect(1f, 1f, position.width - 2f, position.height - 2f), _edgeStarted))
                Repaint();
        }

        private void DrawMainContent()
        {
            // Use a vertical group to manage layout flow
            EditorGUILayout.BeginVertical();
            try
            {
                bool blockBackground = _toolsHost != null && _toolsHost.IsOpen;
                Event backgroundEvent = Event.current;
                bool hideWheel = blockBackground && backgroundEvent.type == EventType.ScrollWheel;
                // IMGUI scroll views handle wheel events even when GUI.enabled is false.
                if (hideWheel) backgroundEvent.type = EventType.Ignore;
                Color backgroundColor = GUI.color;
                try
                {
                    if (blockBackground) GUI.color *= new Color(0.55f, 0.55f, 0.55f, 1f);
                    using (new EditorGUI.DisabledScope(blockBackground))
                    {
                        EditorGUILayout.Space(2);
                        DrawTitleOverlay();
                        DrawCurveEditor();
                        EditorGUILayout.Space(4);
                        DrawParams();
                        EditorGUILayout.Space(4);
                        DrawApplyBar();
                        EditorGUILayout.Space(8);
                        DrawPresets();
                        EditorGUILayout.Space(4);
                        if (_toolsHost != null) _toolsHost.DrawToolbar(MakeHoverStyle(GUI.skin.button), L);
                    }
                }
                finally
                {
                    GUI.color = backgroundColor;
                    if (hideWheel) backgroundEvent.type = EventType.ScrollWheel;
                }

                GUILayout.FlexibleSpace();
                DrawSettingsFooter();
            }
            finally { EditorGUILayout.EndVertical(); }

            if (_toolsHost != null)
            {
                GUIStyle button = MakeHoverStyle(GUI.skin.button);
                GUIStyle selected = new GUIStyle(button);
                URflowVisuals.StyleSelectedButton(selected);
                _toolsHost.DrawPanel(new Rect(12f, 50f, position.width - 24f, Mathf.Max(1f, position.height - 86f)),
                    button, selected, MakeApplyStyle(), L);
            }
        }

        private void DrawTitle()
        {
            // Lazy-load logo texture
            if (_logoTex == null)
            {
                // Search for logo image inside the package / Editor folder
                string[] guids = AssetDatabase.FindAssets("URflow_logo t:Texture2D");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _logoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (_logoTex != null)
            {
                // Fixed height, derive width from aspect ratio
                float logoHeight = 35f;
                float aspect = (float)_logoTex.width / _logoTex.height;
                float logoWidth = logoHeight * aspect;

                EditorGUILayout.Space(6);
                Rect logoRect = GUILayoutUtility.GetRect(logoWidth, logoHeight,
                    GUILayout.ExpandWidth(true), GUILayout.Height(logoHeight));
                Rect drawRect = new Rect(
                    logoRect.x + (logoRect.width - logoWidth) * 0.5f,
                    logoRect.y,
                    logoWidth, logoHeight);
                GUI.DrawTexture(drawRect, _logoTex, ScaleMode.ScaleToFit);
                EditorGUILayout.Space(4);
            }
            else
            {
                // Fallback to text if logo image not found
                GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
                titleStyle.fontSize = 16;
                titleStyle.alignment = TextAnchor.MiddleCenter;
                titleStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
                GUILayout.Label("URFLOW", titleStyle);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Draw LOGO as absolute-positioned overlay, not in layout flow.
        /// Control handles can draw over it.
        /// </summary>
        private void DrawTitleOverlay()
        {
            if (_logoTex == null)
            {
                string[] guids = AssetDatabase.FindAssets("URflow_logo t:Texture2D");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _logoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
            }
            if (_logoTex == null) return;

            float logoHeight = 31f;
            float aspect = (float)_logoTex.width / _logoTex.height;
            float logoWidth = logoHeight * aspect;
            float x = (position.width - logoWidth) / 2f;
            float y = 10f;
            Rect drawRect = new Rect(x, y, logoWidth, logoHeight);
            GUI.DrawTexture(drawRect, _logoTex, ScaleMode.ScaleToFit);
        }

        // ═══════ Curve Editor ═══════

        private void DrawCurveEditor()
        {
            // Reserve extra space above and below for overflow drawing
            float overflow = 60f;
            float overflowTop = 48f;
            float overflowBot = 10f;
            GUILayout.Space(overflowTop);
            float curveHeight = Mathf.Clamp(position.height - (_filter == 2 ? 480f : 450f), 120f, 260f);
            Rect cr = GUILayoutUtility.GetRect(position.width - Pad * 2, curveHeight);
            cr.x += Pad;
            cr.width -= Pad * 2;
            GUILayout.Space(overflowBot);

            // Only draw the dark background for the actual curve box
            URflowVisuals.DrawSurface(cr, URflowVisuals.GraphBackground, new Color(0.23f, 0.23f, 0.23f));

            float m = 20f;
            Rect ir = new Rect(cr.x + m, cr.y + m, cr.width - m * 2, cr.height - m * 2);
            _curveInnerRect = ir;

            if (Event.current.type == EventType.Repaint)
            {
                // All GL drawing in one BeginClip with a large rect
                // that extends above and below the curve box.
                // Key: BeginClip origin = bigClip.position, all GL coords
                // are relative to this origin.
                Rect bigClip = new Rect(cr.x, cr.y - overflow, cr.width, cr.height + overflow * 2);
                GUI.BeginClip(bigClip);
                Vector2 o = bigClip.position;

                Rect crL = new Rect(cr.x - o.x, cr.y - o.y, cr.width, cr.height);
                Rect irL = new Rect(ir.x - o.x, ir.y - o.y, ir.width, ir.height);

                GL.PushMatrix();
                GL.LoadPixelMatrix();

                // Grid & diagonal clipped to box (manual coord clipping)
                DrawGridClipped(irL, crL);
                DrawDiag(irL);

                // Handles, curve, control points — same coord system, can overflow
                DrawHandleLines(irL);
                DrawCurveLine(irL);
                DrawControlPoints(irL);

                GL.PopMatrix();
                GUI.EndClip();
            }

            if (!_showSaveDialog)
                HandleMouse(_curveInnerRect);
        }

        private void DrawGrid(Rect r)
        {
            DrawGridClipped(r, r);
        }

        private void DrawGridClipped(Rect r, Rect clip)
        {
            EnsureMat();
            _mat.SetPass(0);
            GL.Begin(GL.LINES);
            GL.Color(GridCol * GUI.color);
            for (int i = 0; i <= 4; i++)
            {
                float t = (float)i / 4f;
                float x = Mathf.Lerp(r.x, r.xMax, t);
                GL.Vertex3(x, Mathf.Max(r.y, clip.y), 0);
                GL.Vertex3(x, Mathf.Min(r.yMax, clip.yMax), 0);
                float y = Mathf.Lerp(r.yMax, r.y, t);
                if (y >= clip.y && y <= clip.yMax)
                {
                    GL.Vertex3(Mathf.Max(r.x, clip.x), y, 0);
                    GL.Vertex3(Mathf.Min(r.xMax, clip.xMax), y, 0);
                }
            }
            GL.End();
        }

        private void DrawDiag(Rect r)
        {
            EnsureMat();
            _mat.SetPass(0);
            GL.Begin(GL.LINES);
            GL.Color(DiagCol * GUI.color);
            GL.Vertex3(r.x, r.yMax, 0);
            GL.Vertex3(r.xMax, r.y, 0);
            GL.End();
        }

        private void DrawHandleLines(Rect r)
        {
            Vector2 p0 = new Vector2(r.x, r.yMax);
            Vector2 p3 = new Vector2(r.xMax, r.y);
            Vector2 p1 = N2R(_x1, _y1, r);
            Vector2 p2 = N2R(_x2, _y2, r);
            EnsureMat();
            _mat.SetPass(0);
            GL.Begin(GL.LINES);
            GL.Color(HandleCol * GUI.color);
            GL.Vertex3(p0.x, p0.y, 0);
            GL.Vertex3(p1.x, p1.y, 0);
            GL.Vertex3(p3.x, p3.y, 0);
            GL.Vertex3(p2.x, p2.y, 0);
            GL.End();
        }

        private void DrawCurveLine(Rect r)
        {
            URflowVisuals.DrawCurve(r, _x1, _y1, _x2, _y2);
        }

        private void DrawControlPoints(Rect r)
        {
            DrawCircle(N2R(_x1, _y1, r), 4.5f, P1Col);
            DrawCircle(N2R(_x2, _y2, r), 4.5f, P2Col);
        }

        private void DrawCircle(Vector2 center, float rad, Color col)
        {
            EnsureMat();
            _mat.SetPass(0);
            GL.Begin(GL.TRIANGLES);
            GL.Color(col * GUI.color);
            int segs = 24;
            for (int i = 0; i < segs; i++)
            {
                float a1 = (float)i / segs * Mathf.PI * 2f;
                float a2 = (float)(i + 1) / segs * Mathf.PI * 2f;
                GL.Vertex3(center.x, center.y, 0);
                GL.Vertex3(center.x + Mathf.Cos(a1) * rad, center.y + Mathf.Sin(a1) * rad, 0);
                GL.Vertex3(center.x + Mathf.Cos(a2) * rad, center.y + Mathf.Sin(a2) * rad, 0);
            }
            GL.End();
        }

        private int _dragControlId;
        private void HandleMouse(Rect ir)
        {
            _dragControlId = GUIUtility.GetControlID(FocusType.Passive);
            if (!GUI.enabled)
            {
                _dragging = 0;
                if (GUIUtility.hotControl == _dragControlId) GUIUtility.hotControl = 0;
                return;
            }
            Event e = Event.current;
            Vector2 p1s = N2R(_x1, _y1, ir);
            Vector2 p2s = N2R(_x2, _y2, ir);

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                // Use distance to control points directly, no bounding rect limit
                float d1 = Vector2.Distance(e.mousePosition, p1s);
                float d2 = Vector2.Distance(e.mousePosition, p2s);
                if (d1 < Radius * 3f && d1 <= d2) _dragging = 1;
                else if (d2 < Radius * 3f) _dragging = 2;
                if (_dragging != 0)
                {
                    GUIUtility.hotControl = _dragControlId;
                    e.Use();
                    GUI.FocusControl(null);
                }
            }
            else if (e.type == EventType.MouseDrag && _dragging != 0)
            {
                Vector2 n = R2N(e.mousePosition, ir);
                float nx = Mathf.Clamp01(n.x);
                float ny = Mathf.Clamp(n.y, -2f, 3f);

                // Shift held: lock Y to horizontal (Y=0 for P1, Y=1 for P2)
                if (e.shift)
                    ny = _dragging == 1 ? 0f : 1f;

                if (_dragging == 1) { _x1 = nx; _y1 = ny; }
                else { _x2 = nx; _y2 = ny; }
                SyncParam();
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.MouseUp && _dragging != 0)
            {
                _dragging = 0;
                GUIUtility.hotControl = 0;
                e.Use();
            }

            if (_dragging == 0)
            {
                float d1 = Vector2.Distance(e.mousePosition, p1s);
                float d2 = Vector2.Distance(e.mousePosition, p2s);
                if (d1 < Radius * 2.5f || d2 < Radius * 2.5f)
                {
                    Rect cursorArea = new Rect(ir.x - 40, ir.y - 80, ir.width + 80, ir.height + 160);
                    EditorGUIUtility.AddCursorRect(cursorArea, MouseCursor.MoveArrow);
                }
            }
        }

        // ═══════ Parameter Input ═══════

        private void DrawParams()
        {
            // Parameter input field only (no "cubic-bezier(" label)
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);

            EditorGUI.BeginChangeCheck();
            GUIStyle inp = new GUIStyle(EditorStyles.textField);
            inp.alignment = TextAnchor.MiddleCenter;
            inp.fontSize = 12;
            inp.fontStyle = FontStyle.Bold;
            URflowVisuals.StyleButton(inp);
            inp.padding = new RectOffset(7, 7, 3, 3);
            _paramInput = EditorGUILayout.TextField(_paramInput, inp, GUILayout.Height(24));
            if (EditorGUI.EndChangeCheck()) ParseParam();

            GUILayout.Space(Pad);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);
            EditorGUI.BeginChangeCheck();

            GUIStyle s1 = new GUIStyle(EditorStyles.label);
            s1.normal.textColor = P1Col;
            s1.fontSize = 12;
            s1.fontStyle = FontStyle.Bold;
            s1.margin = new RectOffset(0, 0, 4, 0);
            GUIStyle s2 = new GUIStyle(EditorStyles.label);
            s2.normal.textColor = P2Col;
            s2.fontSize = 12;
            s2.fontStyle = FontStyle.Bold;
            s2.margin = new RectOffset(0, 0, 4, 0);

            GUILayout.Label("X1", s1, GUILayout.Width(18));
            _x1 = Mathf.Clamp(EditorGUILayout.FloatField(_x1, GUILayout.Width(45)), 0f, 1f);
            GUILayout.FlexibleSpace();
            GUILayout.Label("Y1", s1, GUILayout.Width(18));
            _y1 = Mathf.Clamp(EditorGUILayout.FloatField(_y1, GUILayout.Width(45)), -0.5f, 1.5f);
            GUILayout.FlexibleSpace();
            GUILayout.Label("X2", s2, GUILayout.Width(18));
            _x2 = Mathf.Clamp(EditorGUILayout.FloatField(_x2, GUILayout.Width(45)), 0f, 1f);
            GUILayout.FlexibleSpace();
            GUILayout.Label("Y2", s2, GUILayout.Width(18));
            _y2 = Mathf.Clamp(EditorGUILayout.FloatField(_y2, GUILayout.Width(45)), -0.5f, 1.5f);

            if (EditorGUI.EndChangeCheck()) { SyncParam(); Repaint(); }
            GUILayout.Space(Pad);
            EditorGUILayout.EndHorizontal();
        }

        // ═══════ Apply ═══════

        private void DrawApplyBar()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);

            // Read Values button
            GUIStyle sideBtn = new GUIStyle(GUI.skin.button);
            sideBtn.fontSize = 12;
            sideBtn.fontStyle = FontStyle.Bold;
            sideBtn.fixedHeight = 34;
            sideBtn.fixedWidth = 36;
            sideBtn.padding = new RectOffset(7, 7, 7, 7);
            sideBtn = MakeHoverStyle(sideBtn);

            Texture2D readIcon = LoadIcon("Read");
            if (GUILayout.Button(new GUIContent(readIcon, L("Read curve from selected keyframes", "从选中的关键帧读取曲线")), sideBtn))
            {
                float rx1, ry1, rx2, ry2;
                if (URflowReadHelper.ReadFromAnimationWindow(out rx1, out ry1, out rx2, out ry2))
                {
                    _x1 = rx1; _y1 = ry1; _x2 = rx2; _y2 = ry2;
                    SyncParam();
                    Repaint();
                }
            }

            GUILayout.Space(4);

            // Apply button (blue, Flow-style)
            GUIStyle applyBtn = MakeApplyStyle();

            if (GUILayout.Button(new GUIContent("APPLY", L("Apply curve to keyframes", "曲线应用关键帧")), applyBtn, GUILayout.ExpandWidth(true)))
            {
                URflowApplyHelper.ApplyToAnimationWindow(_x1, _y1, _x2, _y2, 0);
                _edgeStarted = EditorApplication.timeSinceStartup;
                Repaint();
            }

            GUILayout.Space(4);

            // Save to My Presets button
            GUIStyle saveBtn = MakeHoverStyle(sideBtn);
            Texture2D saveIcon = LoadIcon("Save");
            if (GUILayout.Button(new GUIContent(saveIcon, L("Save current curve to My Presets", "保存当前曲线到我的预设")), saveBtn))
            {
                _showSaveDialog = true;
                _savePresetName = "";
                GUI.FocusControl(null);
            }

            // Hidden: Weighted button
            // GUIStyle wBtn = MakeHoverStyle(sideBtn);
            // if (GUILayout.Button(new GUIContent("WEIGHT", L("Set selected keys to Weighted mode", "选中关键帧设为Weight模式")), wBtn))
            //     URflowWeightedHelper.SetSelectedKeysWeighted();



            GUILayout.Space(Pad);
            EditorGUILayout.EndHorizontal();
        }

        // ═══════ Presets ═══════

        private void DrawPresets()
        {
            Rect card = EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            URflowVisuals.DrawSurface(new Rect(12f, card.y, position.width - 24f, card.height),
                URflowVisuals.PresetBackground, Color.clear);
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);
            GUIStyle tb = new GUIStyle(EditorStyles.label);
            tb.fontSize = 11;
            tb.fontStyle = FontStyle.Bold;
            tb.alignment = TextAnchor.MiddleCenter;
            tb.fixedHeight = 27f;
            tb.margin = new RectOffset(0, 0, 0, 0);
            DrawPresetTab(0, "ALL", L("Show all presets", "显示全部预设"), tb);
            DrawPresetTab(1, "FAVORITES", L("Show favorites only", "仅显示收藏的预设"), tb);
            DrawPresetTab(2, "MY PRESETS", L("Show custom saved presets", "显示自定义保存的预设"), tb);
            GUILayout.Space(Pad);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);

            // ── My Presets action bar ──
            if (_filter == 2)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(Pad);

                GUIStyle actionBtn = new GUIStyle(GUI.skin.button);
                actionBtn.fontSize = 10;
                actionBtn.fontStyle = FontStyle.Bold;
                actionBtn.fixedHeight = 26;
                actionBtn.padding = new RectOffset(4, 4, 4, 4);
                actionBtn = MakeHoverStyle(actionBtn);

                // Load custom icons from Icons folder
                Texture2D importIcon = LoadIcon("import");
                Texture2D exportIcon = LoadIcon("export");
                Texture2D deleteIcon = LoadIcon("delete");

                float btnW = (position.width - Pad * 2 - 8) / 3f;

                if (GUILayout.Button(new GUIContent(importIcon, L("Import presets from JSON", "\u4ece JSON \u5bfc\u5165\u9884\u8bbe")), actionBtn, GUILayout.Width(btnW)))
                {
                    string path = EditorUtility.OpenFilePanel("Import Presets", "", "json");
                    if (!string.IsNullOrEmpty(path))
                    {
                        List<BezierPreset> imported = PresetManager.ImportFromFile(path);
                        for (int i = 0; i < imported.Count; i++)
                            PresetManager.AddUserPreset(imported[i]);
                        _userPresets = PresetManager.LoadUserPresets();
                        Repaint();
                    }
                }

                if (GUILayout.Button(new GUIContent(exportIcon, L("Export presets to JSON", "\u5bfc\u51fa\u9884\u8bbe\u4e3a JSON")), actionBtn, GUILayout.Width(btnW)))
                {
                    string path = EditorUtility.SaveFilePanel("Export Presets", "", "URflow_presets", "json");
                    if (!string.IsNullOrEmpty(path))
                        PresetManager.ExportToFile(_userPresets, path);
                }

                if (GUILayout.Button(new GUIContent(deleteIcon, L("Delete selected preset", "\u5220\u9664\u9009\u4e2d\u7684\u9884\u8bbe")), actionBtn, GUILayout.Width(btnW)))
                {
                    List<BezierPreset> filtered = GetFiltered();
                    if (_selectedPresetIndex >= 0 && _selectedPresetIndex < filtered.Count)
                    {
                        BezierPreset toDelete = filtered[_selectedPresetIndex];
                        if (EditorUtility.DisplayDialog("URflow",
                            L("Delete preset \"" + toDelete.name + "\"?",
                              "\u786e\u5b9a\u5220\u9664\u9884\u8bbe \"" + toDelete.name + "\"\uff1f"),
                            L("Delete", "\u5220\u9664"), L("Cancel", "\u53d6\u6d88")))
                        {
                            PresetManager.RemoveUserPreset(toDelete.name);
                            _userPresets = PresetManager.LoadUserPresets();
                            _selectedPresetIndex = -1;
                            Repaint();
                        }
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("URflow",
                            L("Please select a preset to delete.", "\u8bf7\u5148\u9009\u62e9\u8981\u5220\u9664\u7684\u9884\u8bbe\u3002"),
                            L("OK", "\u786e\u5b9a"));
                    }
                }

                GUILayout.Space(Pad);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);
            _searchQuery = EditorGUILayout.TextField(_searchQuery, EditorStyles.toolbarSearchField);
            Rect searchRect = GUILayoutUtility.GetLastRect();
            if (string.IsNullOrEmpty(_searchQuery) && Event.current.type == EventType.Repaint)
            {
                GUIStyle placeholder = new GUIStyle(EditorStyles.miniLabel);
                placeholder.normal.textColor = new Color(0.52f, 0.52f, 0.52f);
                GUI.Label(new Rect(searchRect.x + 18f, searchRect.y, searchRect.width - 36f, searchRect.height),
                    L("Search presets...", "搜索预设..."), placeholder);
            }

            GUILayout.Space(4);
            GUIStyle viewBtn = new GUIStyle(EditorStyles.toolbarButton);
            viewBtn.fontSize = 11;
            viewBtn.fontStyle = FontStyle.Bold;
            viewBtn.fixedWidth = 24;
            viewBtn = MakeHoverStyle(viewBtn);
            if (GUILayout.Button(_viewMode == 0 ? "\u2261" : "\u2588", viewBtn))
            {
                _viewMode = _viewMode == 0 ? 1 : 0;
                Repaint();
            }

            GUILayout.Space(Pad);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);

            List<BezierPreset> list = GetFiltered();

            _presetScrollPos = EditorGUILayout.BeginScrollView(_presetScrollPos, GUILayout.ExpandHeight(true));
            if (_viewMode == 0)
            {
                for (int i = 0; i < list.Count; i++) DrawItem(list[i], i);
            }
            else
            {
                DrawGrid(list);
            }
            if (list.Count == 0)
            {
                GUILayout.Space(20);
                GUIStyle emptyStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
                emptyStyle.fontSize = 12;
                emptyStyle.wordWrap = true;
                GUILayout.Label(L("No presets found.", "未找到预设。"), emptyStyle);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space(4);
            EditorGUILayout.EndVertical();
        }

        private void DrawPresetTab(int index, string label, string tooltip, GUIStyle baseStyle)
        {
            GUIStyle style = new GUIStyle(baseStyle);
            Color color = _filter == index ? URflowVisuals.Amber : new Color(0.59f, 0.59f, 0.59f);
            style.normal.textColor = color;
            style.hover.textColor = _filter == index ? color : Color.white;
            style.active.textColor = URflowVisuals.Amber;
            if (GUILayout.Button(new GUIContent(label, tooltip), style, GUILayout.ExpandWidth(true)))
            {
                _filter = index;
                Repaint();
            }
            Rect rect = GUILayoutUtility.GetLastRect();
            if (_filter == index && Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(new Rect(rect.x + 8f, rect.yMax - 2f, rect.width - 16f, 1.5f),
                    URflowVisuals.Amber);
        }

        private void DrawCategories()
        {
            List<string> cats = new List<string>();
            cats.Add("All");
            for (int i = 0; i < _builtInPresets.Count; i++)
            {
                if (!cats.Contains(_builtInPresets[i].category))
                    cats.Add(_builtInPresets[i].category);
            }
            if (_userPresets.Count > 0 && !cats.Contains("Custom"))
                cats.Add("Custom");

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);
            GUIStyle pill = new GUIStyle(EditorStyles.miniButton);
            pill.fontSize = 10;
            pill.margin = new RectOffset(1, 1, 0, 0);
            pill = MakeHoverStyle(pill);
            for (int i = 0; i < cats.Count; i++)
            {
                GUIStyle s = new GUIStyle(pill);
                if (_selectedCategory == cats[i])
                {
                    s.normal.textColor = CurveCol;
                    s.fontStyle = FontStyle.Bold;
                }
                if (GUILayout.Button(cats[i], s, GUILayout.MaxWidth(120)))
                    _selectedCategory = cats[i];
            }
            GUILayout.FlexibleSpace();
            GUILayout.Space(Pad);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawItem(BezierPreset p, int idx)
        {
            Event e = Event.current;
            Rect r = EditorGUILayout.BeginHorizontal(GUILayout.Height(28));
            bool hov = r.Contains(e.mousePosition);
            bool sel = _selectedPresetIndex == idx;
            URflowVisuals.DrawSurface(new Rect(r.x + 16f, r.y, r.width - 32f, r.height),
                sel ? PSel : hov ? PHover : PNorm, Color.clear, 4f);

            GUILayout.Space(20);

            bool fav = _favorites.Contains(p.name);
            GUIStyle starStyle = new GUIStyle(EditorStyles.miniLabel);
            starStyle.fontSize = 14;
            starStyle.normal.textColor = fav ? FavCol : new Color(0.35f, 0.35f, 0.38f);
            starStyle.alignment = TextAnchor.MiddleCenter;

            if (GUILayout.Button(fav ? "*" : "o", starStyle, GUILayout.Width(22), GUILayout.Height(24)))
            {
                _favorites = PresetManager.LoadFavorites();
                if (_favorites.Contains(p.name)) _favorites.Remove(p.name);
                else _favorites.Add(p.name);
                PresetManager.SaveFavorites(_favorites);
                Repaint();
            }

            Rect pr = GUILayoutUtility.GetRect(36, 24);
            if (e.type == EventType.Repaint) DrawMini(pr, p);

            GUILayout.Space(4);

            GUIStyle nameStyle = new GUIStyle(EditorStyles.label);
            nameStyle.fontSize = 11;
            nameStyle.normal.textColor = sel ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.72f, 0.72f, 0.72f);
            nameStyle.clipping = TextClipping.Clip;
            GUILayout.Label(p.name, nameStyle, GUILayout.MinWidth(30f), GUILayout.ExpandWidth(true));

            GUILayout.FlexibleSpace();

            GUIStyle paramStyle = new GUIStyle(EditorStyles.miniLabel);
            paramStyle.normal.textColor = new Color(0.50f, 0.50f, 0.50f);
            paramStyle.fontSize = 9;
            GUILayout.Label(p.ToParamString(), paramStyle, GUILayout.Width(105f));

            GUILayout.Space(20);
            EditorGUILayout.EndHorizontal();

            if (GUI.enabled && e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
            {
                _selectedPresetIndex = idx;
                _x1 = p.x1; _y1 = p.y1; _x2 = p.x2; _y2 = p.y2;
                SyncParam();
                e.Use();
                Repaint();
            }
            if (GUI.enabled && e.type == EventType.MouseDown && e.clickCount == 2 && r.Contains(e.mousePosition))
            {
                URflowApplyHelper.ApplyToAnimationWindow(_x1, _y1, _x2, _y2, 0);
                e.Use();
            }
        }

        private void DrawGrid(List<BezierPreset> list)
        {
            float availW = position.width - Pad * 2;
            int cols = Mathf.Max(1, Mathf.FloorToInt(availW / 80f));
            float cellW = availW / cols;
            float cellH = cellW + 18f; // square curve + name label
            Color cardBg = new Color(0.18f, 0.18f, 0.18f);
            Color cardHov = new Color(0.26f, 0.26f, 0.28f);
            Color cardSel = PSel;

            int row = 0;
            for (int i = 0; i < list.Count; i++)
            {
                int col = i % cols;
                if (col == 0)
                {
                    if (i > 0) EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(Pad);
                    row++;
                }

                BezierPreset p = list[i];
                Event e = Event.current;

                Rect cellRect = GUILayoutUtility.GetRect(cellW, cellH);
                bool hover = cellRect.Contains(e.mousePosition);
                bool sel = _selectedPresetIndex == i;

                // Card background
                Rect cardRect = new Rect(cellRect.x + 2, cellRect.y + 2, cellRect.width - 4, cellRect.height - 4);
                URflowVisuals.DrawSurface(cardRect, sel ? cardSel : hover ? cardHov : cardBg, Color.clear, 4f);

                // Curve preview area (square)
                float previewSize = cardRect.width - 8;
                Rect previewRect = new Rect(cardRect.x + 4, cardRect.y + 4, previewSize, previewSize);
                EditorGUI.DrawRect(previewRect, new Color(0.12f, 0.12f, 0.12f));

                // Draw curve
                Color curveColor = CurveCol * 0.85f;
                DrawCurveInRect(previewRect, p, curveColor, 2.5f);

                // Preset name label
                GUIStyle nameStyle = new GUIStyle(EditorStyles.miniLabel);
                nameStyle.fontSize = 10;
                nameStyle.alignment = TextAnchor.MiddleCenter;
                nameStyle.normal.textColor = sel ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.65f, 0.65f, 0.65f);
                nameStyle.clipping = TextClipping.Clip;
                Rect nameRect = new Rect(cardRect.x, cardRect.yMax - 18, cardRect.width, 16);
                GUI.Label(nameRect, p.name, nameStyle);

                // Click to select
                if (GUI.enabled && e.type == EventType.MouseDown && e.button == 0 && cellRect.Contains(e.mousePosition))
                {
                    _selectedPresetIndex = i;
                    _x1 = p.x1; _y1 = p.y1; _x2 = p.x2; _y2 = p.y2;
                    SyncParam();
                    e.Use();
                    Repaint();
                }
                // Double-click to apply
                if (GUI.enabled && e.type == EventType.MouseDown && e.clickCount == 2 && cellRect.Contains(e.mousePosition))
                {
                    URflowApplyHelper.ApplyToAnimationWindow(_x1, _y1, _x2, _y2, 0);
                    e.Use();
                }
            }

            // Close last row
            if (list.Count > 0)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Space(Pad);
                EditorGUILayout.EndHorizontal();
            }
        }

        /// <summary>
        /// Draw a continuous thick curve in a rect using DrawRect.
        /// Fills gaps between samples so steep sections aren't broken.
        /// </summary>
        private static void DrawCurveInRect(Rect area, BezierPreset p, Color col, float thickness)
        {
            int steps = Mathf.Max(16, (int)(area.width * 2));
            float halfT = thickness * 0.5f;
            float prevPx = 0f, prevPy = 0f;
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float v = Mathf.Clamp01(CubicBezierConverter.Evaluate(p.x1, p.y1, p.x2, p.y2, t));
                float px = Mathf.Lerp(area.x, area.xMax, t);
                float py = Mathf.Clamp(Mathf.Lerp(area.yMax, area.y, v), area.y, area.yMax);

                if (i > 0)
                {
                    // Fill the gap between prev and current with rects
                    float dx = px - prevPx;
                    float dy = py - prevPy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    int fill = Mathf.Max(1, Mathf.CeilToInt(dist));
                    for (int f = 0; f <= fill; f++)
                    {
                        float ft = (float)f / fill;
                        float fx = Mathf.Lerp(prevPx, px, ft);
                        float fy = Mathf.Lerp(prevPy, py, ft);
                        EditorGUI.DrawRect(new Rect(fx - halfT, fy - halfT, thickness, thickness), col);
                    }
                }
                prevPx = px;
                prevPy = py;
            }
        }

        private void DrawMini(Rect r, BezierPreset p)
        {
            EditorGUI.DrawRect(r, new Color(0.14f, 0.14f, 0.14f));
            Rect ir = new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4);
            DrawCurveInRect(ir, p, CurveCol * 0.8f, 2f);
        }

        // ═══════ Settings ═══════

        /// <summary>
        /// Bottom footer bar: gear button + version (shown on main view)
        /// </summary>
        private void DrawSettingsFooter()
        {
            EditorGUILayout.Space(4);
            Rect sepRect = GUILayoutUtility.GetRect(position.width, 1);
            EditorGUI.DrawRect(sepRect, URflowVisuals.Divider);
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal(GUILayout.Height(22));
            GUILayout.Space(8);

            GUIStyle gearBtn = new GUIStyle(GUIStyle.none);
            gearBtn.fixedHeight = 20;
            gearBtn.fixedWidth = 20;
            gearBtn.padding = new RectOffset(2, 2, 2, 2);

            Texture2D settingIcon = LoadIcon("Setting");
            if (GUILayout.Button(new GUIContent(settingIcon, L("Settings", "\u8bbe\u7f6e")), gearBtn))
            {
                _showSettings = true;
                Repaint();
            }

            GUILayout.FlexibleSpace();

            GUIStyle verStyle = new GUIStyle(EditorStyles.label);
            verStyle.normal.textColor = new Color(0.45f, 0.45f, 0.45f);
            verStyle.fontSize = 11;
            verStyle.alignment = TextAnchor.MiddleRight;
            GUILayout.Label("v" + VERSION, verStyle);
            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);
        }

        /// <summary>
        /// Full-screen settings/menu page (replaces the main view)
        /// </summary>
        private void DrawSaveDialog()
        {
            // Semi-transparent overlay
            Rect full = new Rect(0, 0, position.width, position.height);
            EditorGUI.DrawRect(full, new Color(0, 0, 0, 0.5f));

            // Dialog box centered
            float dw = 280, dh = 140;
            Rect dialog = new Rect(
                (position.width - dw) / 2,
                (position.height - dh) / 2,
                dw, dh);
            URflowVisuals.DrawSurface(dialog, URflowVisuals.PanelBackground, URflowVisuals.Divider);

            GUILayout.BeginArea(dialog);
            GUILayout.Space(14);

            // Title
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.fontSize = 13;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
            GUILayout.Label(L("SAVE PRESET", "保存预设"), titleStyle);

            GUILayout.Space(10);

            // Name input
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16);
            GUI.SetNextControlName("SavePresetField");
            _savePresetName = EditorGUILayout.TextField(_savePresetName, GUILayout.Height(22));
            GUILayout.Space(16);
            EditorGUILayout.EndHorizontal();

            // Auto-focus the text field
            if (GUI.GetNameOfFocusedControl() != "SavePresetField")
                EditorGUI.FocusTextInControl("SavePresetField");

            GUILayout.Space(12);

            // Buttons
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16);

            GUIStyle cancelBtn = MakeHoverStyle(new GUIStyle(GUI.skin.button));
            cancelBtn.fixedHeight = 26;
            if (GUILayout.Button(L("CANCEL", "取消"), cancelBtn, GUILayout.ExpandWidth(true)))
            {
                _showSaveDialog = false;
            }

            GUILayout.Space(8);

            // Save button with gradient
            GUIStyle saveStyle = new GUIStyle(GUI.skin.button);
            saveStyle.fixedHeight = 26;
            saveStyle.fontStyle = FontStyle.Bold;
            URflowVisuals.StyleButton(saveStyle, true);
            saveStyle.normal.textColor = Color.white;
            saveStyle.hover.textColor = Color.white;

            bool enterPressed = Event.current.type == EventType.KeyDown
                && Event.current.keyCode == KeyCode.Return
                && !string.IsNullOrEmpty(_savePresetName.Trim());

            if (GUILayout.Button(L("SAVE", "保存"), saveStyle, GUILayout.ExpandWidth(true)) || enterPressed)
            {
                string name = _savePresetName.Trim();
                if (!string.IsNullOrEmpty(name))
                {
                    BezierPreset preset = new BezierPreset(name, "My Presets", _x1, _y1, _x2, _y2);
                    PresetManager.AddUserPreset(preset);
                    _userPresets = PresetManager.LoadUserPresets();
                    _showSaveDialog = false;
                    _filter = 2; // Switch to MY PRESETS tab
                    Repaint();
                }
                if (enterPressed) Event.current.Use();
            }

            GUILayout.Space(16);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.EndArea();
        }

        private void DrawSettingsPage()
        {
            float cardH = 52f;
            float cardPad = 6f;
            Color cardBg = new Color(0.20f, 0.20f, 0.20f);
            Color cardHover = new Color(0.26f, 0.26f, 0.28f);
            Color titleCol = new Color(0.90f, 0.90f, 0.90f);
            Color subCol = new Color(0.50f, 0.50f, 0.50f);

            EditorGUILayout.BeginVertical();

            // ── Header: MENU + Close button ──
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);

            GUIStyle menuTitle = new GUIStyle(EditorStyles.boldLabel);
            menuTitle.fontSize = 14;
            menuTitle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            menuTitle.alignment = TextAnchor.MiddleCenter;

            GUILayout.FlexibleSpace();
            GUILayout.Label("MENU", menuTitle);
            GUILayout.FlexibleSpace();

            GUIStyle closeBtn = new GUIStyle(GUI.skin.button);
            closeBtn.fontSize = 16;
            closeBtn.fixedWidth = 28;
            closeBtn.fixedHeight = 28;
            closeBtn.padding = new RectOffset(0, 0, 0, 0);
            closeBtn = MakeHoverStyle(closeBtn);

            if (GUILayout.Button("\u2715", closeBtn))
            {
                _showSettings = false;
                Repaint();
            }

            GUILayout.Space(Pad);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(12);

            // ── Menu Cards ──
            DrawMenuCard("\u2191", L("Updates", "\u68c0\u67e5\u66f4\u65b0"), L("manage updates", "\u7ba1\u7406\u66f4\u65b0"), cardH, cardPad, cardBg, cardHover, titleCol, subCol, delegate()
            {
                UnityEditor.PackageManager.UI.Window.Open("com.liuyiran.urflow");
            }, false);

            DrawMenuCard("\u2630", L("Language", "\u8bed\u8a00\u8bbe\u7f6e"), L("switch language", "\u5207\u6362\u8bed\u8a00"), cardH, cardPad, cardBg, cardHover, titleCol, subCol, delegate()
            {
                Lang = Lang == 0 ? 1 : 0;
                Repaint();
            });

            DrawMenuCard("\u2605", L("Presets", "\u9884\u8bbe\u7ba1\u7406"), L("import / export", "\u5bfc\u5165 / \u5bfc\u51fa"), cardH, cardPad, cardBg, cardHover, titleCol, subCol, delegate()
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent(L("Import from JSON", "\u4ece JSON \u5bfc\u5165")), false, delegate()
                {
                    string path = EditorUtility.OpenFilePanel("Import Presets", "", "json");
                    if (!string.IsNullOrEmpty(path))
                    {
                        List<BezierPreset> imported = PresetManager.ImportFromFile(path);
                        for (int i = 0; i < imported.Count; i++)
                            PresetManager.AddUserPreset(imported[i]);
                        _userPresets = PresetManager.LoadUserPresets();
                        Repaint();
                    }
                });
                menu.AddItem(new GUIContent(L("Export to JSON", "\u5bfc\u51fa\u4e3a JSON")), false, delegate()
                {
                    string path = EditorUtility.SaveFilePanel("Export Presets", "", "URflow_presets", "json");
                    if (!string.IsNullOrEmpty(path))
                        PresetManager.ExportToFile(_userPresets, path);
                });
                menu.ShowAsContext();
            });

            DrawMenuCard("\u2139", L("About", "\u5173\u4e8e"), L("more info", "\u66f4\u591a\u4fe1\u606f"), cardH, cardPad, cardBg, cardHover, titleCol, subCol, delegate()
            {
                EditorUtility.DisplayDialog("URflow",
                    L("URflow " + VERSION + "\nUnity Animation Bezier Curve Editor\n\nFeedback & Bugs:\nliuyiran02@corp.netease.com",
                      "URflow " + VERSION + "\nUnity \u52a8\u753b\u8d1d\u585e\u5c14\u66f2\u7ebf\u7f16\u8f91\u5668\n\n\u529f\u80fd\u5efa\u8bae & Bugs \u8bf7\u79c1\u804a\uff1a\nliuyiran02@corp.netease.com"),
                    L("OK", "\u786e\u5b9a"));
            });

            // ── Bottom: push version to bottom ──
            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);
            GUIStyle footerName = new GUIStyle(EditorStyles.miniLabel);
            footerName.normal.textColor = new Color(0.50f, 0.50f, 0.50f);
            footerName.fontSize = 10;
            GUILayout.Label("URFLOW", footerName);
            GUILayout.FlexibleSpace();

            GUIStyle footerVer = new GUIStyle(GUI.skin.button);
            footerVer.fontSize = 9;
            footerVer.fixedHeight = 20;
            footerVer.padding = new RectOffset(8, 8, 2, 2);
            footerVer = MakeHoverStyle(footerVer);
            GUILayout.Button("v" + VERSION, footerVer);

            GUILayout.Space(Pad);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(8);

            EditorGUILayout.EndVertical();
        }

        private void DrawMenuCard(string icon, string title, string subtitle,
            float cardH, float cardPad, Color cardBg, Color cardHover,
            Color titleCol, Color subCol, System.Action onClick, bool showBadge = false)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);

            Rect cardRect = GUILayoutUtility.GetRect(0, cardH, GUILayout.ExpandWidth(true));
            Event e = Event.current;
            bool hover = cardRect.Contains(e.mousePosition);

            // Card background
            URflowVisuals.DrawSurface(cardRect, hover ? cardHover : cardBg, new Color(0.30f, 0.30f, 0.32f));

            // Icon
            GUIStyle iconStyle = new GUIStyle(EditorStyles.label);
            iconStyle.fontSize = 20;
            iconStyle.alignment = TextAnchor.MiddleCenter;
            iconStyle.normal.textColor = new Color(0.70f, 0.70f, 0.70f);
            Rect iconRect = new Rect(cardRect.x + 8, cardRect.y, 40, cardRect.height);
            GUI.Label(iconRect, icon, iconStyle);

            // Title
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.fontSize = 12;
            titleStyle.normal.textColor = titleCol;
            Rect titleRect = new Rect(cardRect.x + 56, cardRect.y + 8, cardRect.width - 64, 18);
            GUI.Label(titleRect, title, titleStyle);

            // Subtitle
            GUIStyle subStyle = new GUIStyle(EditorStyles.miniLabel);
            subStyle.fontSize = 10;
            subStyle.normal.textColor = subCol;
            Rect subRect = new Rect(cardRect.x + 56, cardRect.y + 26, cardRect.width - 64, 16);
            GUI.Label(subRect, subtitle, subStyle);

            // Red badge dot
            if (showBadge)
            {
                DrawRedDot(new Vector2(cardRect.xMax - 14f, cardRect.y + 10f), 5f);
            }

            // Click handler
            if (e.type == EventType.MouseDown && e.button == 0 && hover)
            {
                onClick();
                e.Use();
            }

            GUILayout.Space(Pad);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(cardPad);
        }

        // ═══════ Helpers ═══════

        // Shared with tool panels so their primary action matches the existing APPLY button.
        private static GUIStyle MakeApplyStyle()
        {
            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.fontSize = 15;
            style.fontStyle = FontStyle.Bold;
            style.fixedHeight = 34;
            URflowVisuals.StyleButton(style, true);
            style.normal.textColor = Color.white;
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
            style.focused.textColor = Color.white;
            style.onNormal.textColor = Color.white;
            style.onHover.textColor = Color.white;
            style.onActive.textColor = Color.white;
            style.onFocused.textColor = Color.white;
            return style;
        }

        /// <summary>
        /// Draw a small red notification dot at the given center position.
        /// </summary>
        private static void DrawRedDot(Vector2 center, float radius)
        {
            Rect dotRect = new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f);
            Color prev = GUI.color;
            GUI.color = new Color(1f, 0.25f, 0.25f);
            GUI.DrawTexture(dotRect, EditorGUIUtility.whiteTexture, ScaleMode.ScaleToFit, true, 1f);
            GUI.color = prev;
        }

        /// <summary>
        /// Creates a GUIStyle based on GUI.skin.button with green hover state.
        /// </summary>
        private static GUIStyle MakeHoverStyle(GUIStyle baseStyle)
        {
            GUIStyle s = new GUIStyle(baseStyle);
            URflowVisuals.StyleButton(s);
            return s;
        }

        private List<BezierPreset> GetFiltered()
        {
            List<BezierPreset> all = new List<BezierPreset>();

            if (_filter == 2)
            {
                all.AddRange(_userPresets);
            }
            else
            {
                all.AddRange(_builtInPresets);
                all.AddRange(_userPresets);

                if (_filter == 1)
                {
                    List<BezierPreset> favList = new List<BezierPreset>();
                    for (int i = 0; i < all.Count; i++)
                    {
                        if (_favorites.Contains(all[i].name))
                            favList.Add(all[i]);
                    }
                    all = favList;
                }
            }

            if (_selectedCategory != "All")
            {
                List<BezierPreset> catList = new List<BezierPreset>();
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i].category == _selectedCategory)
                        catList.Add(all[i]);
                }
                all = catList;
            }

            if (!string.IsNullOrEmpty(_searchQuery))
            {
                string q = _searchQuery.ToLower();
                List<BezierPreset> searchList = new List<BezierPreset>();
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i].name.ToLower().Contains(q))
                        searchList.Add(all[i]);
                }
                all = searchList;
            }

            return all;
        }

        private void SyncParam()
        {
            _paramInput = string.Format("{0:F2}, {1:F2}, {2:F2}, {3:F2}", _x1, _y1, _x2, _y2);
        }

        private void ParseParam()
        {
            string[] parts = _paramInput.Replace(" ", "").Split(',');
            if (parts.Length == 4)
            {
                float a, b, c, d;
                if (float.TryParse(parts[0], out a) &&
                    float.TryParse(parts[1], out b) &&
                    float.TryParse(parts[2], out c) &&
                    float.TryParse(parts[3], out d))
                {
                    _x1 = Mathf.Clamp01(a);
                    _y1 = Mathf.Clamp(b, -0.5f, 1.5f);
                    _x2 = Mathf.Clamp01(c);
                    _y2 = Mathf.Clamp(d, -0.5f, 1.5f);
                    Repaint();
                }
            }
        }

        private static Vector2 N2R(float nx, float ny, Rect r)
        {
            return new Vector2(
                Mathf.LerpUnclamped(r.x, r.xMax, nx),
                Mathf.LerpUnclamped(r.yMax, r.y, ny));
        }

        private static Vector2 R2N(Vector2 pos, Rect r)
        {
            return new Vector2(
                (pos.x - r.x) / r.width,
                (r.yMax - pos.y) / r.height);
        }

        private static void DrawBorder(Rect r, Color c)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 1, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - 1, r.y, 1, r.height), c);
        }

        private static Dictionary<string, Texture2D> _iconCache = new Dictionary<string, Texture2D>();
        private static string _iconFolder;
        private static string GetIconFolder()
        {
            if (_iconFolder != null) return _iconFolder;
            // Find the script itself via its known GUID or search, then derive the Icons folder
            string[] searchPaths = new[]
            {
                "Packages/com.liuyiran.urflow/Editor/Icons", // UPM package
                "Assets/URflow/Editor/Icons",                // local install
            };
            foreach (var p in searchPaths)
            {
                if (AssetDatabase.IsValidFolder(p))
                {
                    _iconFolder = p;
                    return _iconFolder;
                }
            }
            _iconFolder = "";
            return _iconFolder;
        }
        private static Texture2D LoadIcon(string name)
        {
            if (_iconCache.ContainsKey(name) && _iconCache[name] != null)
                return _iconCache[name];
            string folder = GetIconFolder();
            if (string.IsNullOrEmpty(folder)) return null;
            string[] guids = AssetDatabase.FindAssets(name + " t:Texture2D", new[] { folder });
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                _iconCache[name] = tex;
                return tex;
            }
            return null;
        }

        private static void EnsureMat()
        {
            if (_mat != null) return;
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            _mat = new Material(shader);
            _mat.hideFlags = HideFlags.HideAndDontSave;
            _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _mat.SetInt("_ZWrite", 0);
        }
    }
}
