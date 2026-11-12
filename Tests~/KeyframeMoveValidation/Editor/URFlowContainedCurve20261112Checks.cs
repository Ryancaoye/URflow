using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace URflow
{
    // Installed only in the independent D: validation package, never in the product package.
    public static class URFlowContainedCurve20261112Checks
    {
        internal const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private const string Evidence = "D:/插件/URFLOW/6-开发/暂存资料/URFlow-v1.0.4-ui-20261109/evidence/fixtures/";
        private static readonly StringBuilder Report = new StringBuilder();
        private static readonly List<string> Errors = new List<string>();
        private static readonly Color Background = new Color(32f / 255f, 32f / 255f, 32f / 255f, 1f);
        private static URflowWindow window;
        private static URFlowContainedPreset20261112Probe presetProbe;
        private static string prefix;
        private static int checks, ticks;

        public static void Run()
        {
            Report.Clear(); Errors.Clear(); checks = ticks = 0;
            prefix = Evidence + "contained-curve-20261112-" + DateTime.Now.ToString("HHmmss-fff");
            Report.AppendLine("Unity=" + Application.unityVersion);
            Report.AppendLine("Scope=Production viewport, normalized-x evaluator and renderer; true native URflowWindow mouse events and actual preset-row events. Pixel checks use offscreen rendering, not a full-window screenshot. Cursor tracking feel and production project scenarios require user acceptance.");
            Application.logMessageReceived += Capture;
            EditorApplication.update += Ready;
        }

        private static void Ready()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || ++ticks < 12) return;
            EditorApplication.update -= Ready;
            try
            {
                Geometry(); RangeLifecycle(); PixelChecks(); NativeWindow();
                Check(Errors.Count == 0, "No GUI/GUILayout errors in native curve and preset event probes");
                Finish(null);
            }
            catch (Exception error) { Finish(error); }
        }

        private static void Geometry()
        {
            foreach (float width in new[] { 320f, 350f, 650f })
            {
                Rect canvas = new Rect(16f, 50f, width - 32f, width == 320f ? 120f : 260f);
                Rect inner = new Rect(canvas.x + 24f, canvas.y + 24f, canvas.width - 48f, canvas.height - 48f);
                foreach (Vector4 p in new[] { new Vector4(.33f, 0f, .24f, .85f), new Vector4(.2644f, -.468f, .4169f, .8727f), new Vector4(.3038f, 1.375f, .6666f, 1f), new Vector4(.24f, -2f, .72f, 3f) })
                {
                    var viewport = new URflowCurveViewport(); viewport.Fit(p.y, p.w, 0d, false);
                    foreach (Vector2 point in new[] { new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(p.x, p.y), new Vector2(p.z, p.w) })
                    {
                        Vector2 screen = viewport.ToScreen(point.x, point.y, inner);
                        Check(Contains(canvas, screen, 21f), "All endpoints and complete 21-point handle targets stay inside canvas width=" + width + " parameters=" + p);
                        Check(Vector2.Distance(viewport.FromScreen(screen, inner), point) < .00001f, "Viewport forward/inverse map round trip preserves actual normalized coordinates");
                    }
                    for (int i = 0; i <= 256; i++)
                    {
                        float x = i / 256f, y = CubicBezierConverter.Evaluate(p.x, p.y, p.z, p.w, x);
                        if (!Contains(inner, viewport.ToScreen(x, y, inner), 0f)) throw new InvalidOperationException("Actual normalized-x Bezier sample escaped its fitted inner canvas at " + p + " time=" + x);
                    }
                    Check(true, "257 actual easing samples stay contained for " + p + " width=" + width);
                }
            }
        }

        private static void RangeLifecycle()
        {
            var viewport = new URflowCurveViewport(); Rect r = new Rect(24f, 24f, 280f, 200f);
            viewport.Fit(.25f, .8f, 0d, false); Vector2 ordinary = viewport.Range;
            viewport.BeginDrag(); Vector2 point = viewport.Drag(new Vector2(.3f, .25f), new Vector2(5f, -4f), r, false, 0f);
            Check(viewport.Range == ordinary && point.y > .25f, "Interior dragging preserves display scale and moves the value in the intended direction");
            point = viewport.Drag(point, new Vector2(0f, 300f), r, false, 0f);
            Vector2 expanded = viewport.Range;
            Check(expanded.x < 0f && point.y >= -2f && Contains(r, viewport.ToScreen(point.x, point.y, r), 0f), "Downward edge drag expands range while retaining the handle inside the canvas");
            Vector2 stable = viewport.Drag(point, Vector2.zero, r, false, 0f);
            Check(stable == point && viewport.Range == expanded, "Stationary cursor cannot change parameters after an edge expansion");
            viewport.Fit(point.y, .8f, 10d, true);
            Vector2 fitStart = viewport.Range; viewport.Tick(10.09d); Vector2 fitMiddle = viewport.Range; viewport.Tick(10.3d);
            Check(fitMiddle.x >= fitStart.x && fitMiddle.x <= viewport.Range.x && viewport.Range == URflowCurveViewport.FittedRange(point.y, .8f), "Release fit contracts smoothly and reaches the outward-rounded target");
            viewport.Fit(-2f, 3f, 20d, true);
            Check(viewport.Range.x <= -2f && viewport.Range.y >= 3f, "New extreme preset values are contained immediately before any fit animation");
            viewport.BeginDrag(); Vector2 shifted = viewport.Drag(new Vector2(.7f, 3f), new Vector2(0f, 80f), r, true, 1f);
            Check(shifted.y == 1f, "Shift locks the second handle to the endpoint's normalized Y=1");
            foreach (float readY in new[] { -4.75f, 6.25f })
            {
                viewport.Fit(-4.75f, 6.25f, 30d, false); viewport.BeginDrag(readY);
                Vector2 initial = new Vector2(.3f, readY);
                Vector2 horizontal = viewport.Drag(initial, new Vector2(8f, 0f), r, false, 0f);
                Check(horizontal.y == readY && horizontal.x > initial.x, "Horizontal dragging preserves finite READ value outside manual limits Y=" + readY);
                float inwardDelta = readY < 0f ? -4f : 4f;
                Vector2 inward = viewport.Drag(horizontal, new Vector2(0f, inwardDelta), r, false, 0f);
                Vector2 reversed = viewport.Drag(inward, new Vector2(0f, -inwardDelta), r, false, 0f);
                Check(Mathf.Abs(inward.y - readY) < .3f && Mathf.Abs(reversed.y - readY) < .00001f,
                    "Extreme READ vertical drag remains continuous and reversible within the gesture Y=" + readY);
            }
        }

        private static void PixelChecks()
        {
            Rect r = new Rect(48f, 48f, 390f, 320f);
            Type baseline = typeof(URflowWindow).Assembly.GetType("URflow.URFlowHandleLinesBaselineVisuals", true);
            MethodInfo baselineRender = baseline.GetMethod("RenderCurve", Flags);
            Texture2D old = Render(() => baselineRender.Invoke(null, new object[] { r, .77f, .08f, .34f, .84f, Color.white }));
            Texture2D current = Render(() => URflowVisuals.RenderCurve(r, .77f, .08f, .34f, .84f, Color.white, 0f, 1f));
            try
            {
                Check(Equal(old.GetPixels32(), current.GetPixels32()), "Normal 0..1 renderer pixels exactly match the pre-change baseline including both endpoint halos and saturated colors");
                File.WriteAllBytes(prefix + "-normal.png", current.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(old); UnityEngine.Object.DestroyImmediate(current); }
            foreach (Vector4 p in new[] { new Vector4(.2644f, -.468f, .4169f, .8727f), new Vector4(.3038f, 1.375f, .6666f, 1f), new Vector4(.24f, -2f, .72f, 3f) })
            {
                var viewport = new URflowCurveViewport(); viewport.Fit(p.y, p.w, 0d, false); Vector2 range = viewport.Range;
                Texture2D image = Render(() =>
                {
                    URflowVisuals.RenderCurve(r, p.x, p.y, p.z, p.w, Color.white, range.x, range.y);
                    Color yellow = new Color(1f, .72f, .15f, 1f);
                    URflowVisuals.RenderControlLine(viewport.ToScreen(0f, 0f, r), viewport.ToScreen(p.x, p.y, r), yellow, 2f);
                    URflowVisuals.RenderControlLine(viewport.ToScreen(1f, 1f, r), viewport.ToScreen(p.z, p.w, r), yellow, 2f);
                });
                try
                {
                    Vector2[] actual = (Vector2[])typeof(URflowVisuals).GetField("CurvePoints", Flags).GetValue(null);
                    for (int i = 0; i < actual.Length; i++)
                    {
                        float x = i / (float)(actual.Length - 1);
                        Vector2 expected = viewport.ToScreen(x, CubicBezierConverter.Evaluate(p.x, p.y, p.z, p.w, x), r);
                        if (Vector2.Distance(expected, actual[i]) > .0001f) throw new InvalidOperationException("Renderer and handles use different mappings at " + p);
                    }
                    Check(true, "Actual renderer cache points match handle coordinates for expanded range " + range);
                    Color32[] pixels = image.GetPixels32(); Color32 bg = Background; Rect outer = new Rect(24f, 24f, 438f, 368f); int ink = 0;
                    for (int y = 0; y < 512; y++) for (int x = 0; x < 512; x++)
                    {
                        Color32 c = pixels[(511 - y) * 512 + x];
                        if (Same(c, bg)) continue;
                        ink++;
                        if (!outer.Contains(new Vector2(x + .5f, y + .5f))) throw new InvalidOperationException("Paint escaped the 24-point graph margin at " + p + " pixel=" + x + "," + y);
                    }
                    Check(ink > 1000, "Curve, retained glow and yellow guides have visible pixels without escaping the bounded canvas");
                    File.WriteAllBytes(prefix + "-range-" + range.x + "-" + range.y + ".png", image.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(image); }
            }
            Texture2D before = Render(() => URflowVisuals.RenderCurve(r, .3f, .2f, .7f, .8f, Color.white, 0f, 1f));
            Texture2D after = Render(() => URflowVisuals.RenderCurve(r, .3f, .2f, .7f, .8f, Color.white, -2f, 3f));
            try { Check(!Equal(before.GetPixels32(), after.GetPixels32()), "Changing only display range invalidates cached geometry and updates actual pixels"); }
            finally { UnityEngine.Object.DestroyImmediate(before); UnityEngine.Object.DestroyImmediate(after); }
        }

        private static void NativeWindow()
        {
            window = ScriptableObject.CreateInstance<URflowWindow>(); window.Show(); window.position = new Rect(140f, 120f, 350f, 900f); window.Focus();
            SetCurve(.2644f, -.468f, .4169f, .8727f); Frame();
            Rect initial = window.position; AssertWindowContained();
            Check(Y1 == -.468f && Y2 == .8727f, "Repainting actual input fields preserves the downward screenshot parameters");
            Vector2 start = Point(1); Send(window, EventType.MouseDown, start, Vector2.zero);
            Check(Dragging == 1, "Downward handle MouseDown is captured by the actual URflowWindow");
            Vector2 range = Viewport.Range;
            Send(window, EventType.MouseDrag, start + new Vector2(3f, 40f), new Vector2(3f, 40f)); Frame();
            Check(Y1 < -.468f && Viewport.Range.x < range.x, "Actual downward native drag changes the intended handle and expands only near the bottom edge");
            AssertWindowContained(); Check(window.position == initial, "Extreme downward drag leaves the native window geometry unchanged");
            Send(window, EventType.MouseUp, start + new Vector2(3f, 40f), Vector2.zero); Frame();
            Check(Dragging == 0, "Native mouse release ends curve capture");
            SetCurve(.3038f, 1.375f, .6666f, 1f); Frame(); start = Point(1);
            Send(window, EventType.MouseDown, start, Vector2.zero);
            Send(window, EventType.MouseDrag, start + new Vector2(0f, -50f), new Vector2(0f, -50f)); Frame();
            Check(Y1 > 1.375f && window.position == initial, "Actual upward native drag stays in the graph and cannot move the title bar"); AssertWindowContained();
            Send(window, EventType.KeyDown, start, Vector2.zero, false, KeyCode.Escape); Frame();
            Check(Dragging == 0, "Escape releases the actual curve capture");
            SetCurve(.24f, -2f, .72f, 3f); Frame();
            Check(Y1 == -2f && Y2 == 3f, "Actual FloatField repaint retains both full-range -2 and 3 values"); AssertWindowContained();
            SetCurve(.24f, -4.75f, .72f, 6.25f); Frame();
            foreach (int which in new[] { 1, 2 })
            {
                start = Point(which); float readY = which == 1 ? Y1 : Y2;
                Send(window, EventType.MouseDown, start, Vector2.zero);
                Send(window, EventType.MouseDrag, start + new Vector2(8f, 0f), new Vector2(8f, 0f)); Frame();
                Check((which == 1 ? Y1 : Y2) == readY && Dragging == which,
                    "Actual horizontal native drag does not clip the extreme READ value for handle " + which);
                Send(window, EventType.MouseUp, start + new Vector2(8f, 0f), Vector2.zero); Frame();
            }
            SetCurve(.24f, -2f, .72f, 3f); Frame();
            start = Point(2); Send(window, EventType.MouseDown, start, Vector2.zero);
            Send(window, EventType.MouseDrag, start + new Vector2(5f, 20f), new Vector2(5f, 20f), true); Frame();
            Check(Y2 == 1f && Dragging == 2, "Native Shift drag locks the blue-side control to normalized Y=1");
            Invoke(window, "OnLostFocus"); Check(Dragging == 0, "Losing focus releases curve capture");
            SetCurve(.24f, -2f, .72f, 3f); Frame();
            object host = Get(window, "_toolsHost");
            // Production Open intentionally terminates its current IMGUI event after changing state.
            // The reflected call executes outside OnGUI, so catch only that expected sentinel.
            try { Invoke(host, "Open"); } catch (ExitGUIException) { }
            Frame(); start = Point(1);
            Send(window, EventType.MouseDown, start, Vector2.zero); Send(window, EventType.MouseDrag, start + new Vector2(4f, 30f), new Vector2(4f, 30f));
            Check(Dragging == 0 && Y1 == -2f && Y2 == 3f, "Opening a tool disables background handle editing");
            window.Close(); window = null;
            window = ScriptableObject.CreateInstance<URflowWindow>(); window.Show(); window.position = new Rect(140f, 120f, 320f, 560f); window.Focus();
            SetCurve(.2644f, -.468f, .4169f, .8727f); Frame(); AssertWindowContained();
            Check(((Rect)Get(window, "_curveCanvasRect")).y >= 48f, "Compact native window keeps the graph below the reserved logo row");
            presetProbe = ScriptableObject.CreateInstance<URFlowContainedPreset20261112Probe>(); presetProbe.target = window;
            presetProbe.preset = new BezierPreset("Viewport extreme preset", "All", .24f, -2f, .72f, 3f);
            presetProbe.Show(); presetProbe.position = new Rect(560f, 120f, 400f, 120f); presetProbe.Focus(); Repaint(presetProbe);
            Vector2 rowPoint = new Vector2(presetProbe.row.x + 130f, presetProbe.row.center.y);
            Send(presetProbe, EventType.MouseDown, rowPoint, Vector2.zero); Send(presetProbe, EventType.MouseUp, rowPoint, Vector2.zero); Frame();
            Check(Y1 == -2f && Y2 == 3f, "Actual production preset-row click survives full-window parameter repaint without clipping"); AssertWindowContained();
        }

        private static URflowCurveViewport Viewport { get { return (URflowCurveViewport)Get(window, "_curveViewport"); } }
        private static float Y1 { get { return (float)Get(window, "_y1"); } }
        private static float Y2 { get { return (float)Get(window, "_y2"); } }
        private static int Dragging { get { return (int)Get(window, "_dragging"); } }
        private static Vector2 Point(int which) { return Viewport.ToScreen((float)Get(window, which == 1 ? "_x1" : "_x2"), which == 1 ? Y1 : Y2, (Rect)Get(window, "_curveInnerRect")); }
        private static void AssertWindowContained()
        {
            Rect canvas = (Rect)Get(window, "_curveCanvasRect");
            Check(Contains(canvas, Point(1), 21f) && Contains(canvas, Point(2), 21f), "Actual laid-out handles and full hit radii are contained within their graph surface");
        }
        private static void SetCurve(float x1, float y1, float x2, float y2)
        { Set(window, "_x1", x1); Set(window, "_y1", y1); Set(window, "_x2", x2); Set(window, "_y2", y2); Invoke(window, "SyncParam"); }
        private static void Frame() { Send(window, EventType.MouseMove, new Vector2(4f, 4f), Vector2.zero); Repaint(window); }
        internal static void Repaint(EditorWindow value) { Invoke(Get(value, "m_Parent"), "RepaintImmediately"); }
        private static void Send(EditorWindow value, EventType type, Vector2 point, Vector2 delta, bool shift = false, KeyCode key = KeyCode.None)
        {
            RectOffset border = Get(Get(value, "m_Parent"), "borderSize") as RectOffset;
            Vector2 origin = border == null ? Vector2.zero : new Vector2(border.left, border.top);
            value.SendEvent(new Event { type = type, mousePosition = point + origin, delta = delta, button = 0, shift = shift, keyCode = key, clickCount = 1 });
        }
        internal static object Get(object target, string name)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, Flags | BindingFlags.DeclaredOnly); if (field != null) return field.GetValue(target);
                PropertyInfo property = type.GetProperty(name, Flags | BindingFlags.DeclaredOnly); if (property != null) return property.GetValue(target, null);
            }
            throw new MissingMemberException(target.GetType().FullName, name);
        }
        private static void Set(object target, string name, object value) { target.GetType().GetField(name, Flags).SetValue(target, value); }
        internal static object Invoke(object target, string name, params object[] args)
        {
            try { return target.GetType().GetMethod(name, Flags).Invoke(target, args); }
            catch (TargetInvocationException error) { if (error.InnerException is ExitGUIException) throw (ExitGUIException)error.InnerException; throw; }
        }
        private static Texture2D Render(Action draw)
        {
            RenderTexture previous = RenderTexture.active; var target = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var image = new Texture2D(512, 512, TextureFormat.RGBA32, false, true);
            try
            {
                target.Create(); RenderTexture.active = target; GL.Clear(true, true, Background); GL.PushMatrix();
                try { GL.LoadPixelMatrix(0f, 512f, 512f, 0f); draw(); } finally { GL.PopMatrix(); }
                image.ReadPixels(new Rect(0f, 0f, 512f, 512f), 0, 0); image.Apply(); return image;
            }
            catch { UnityEngine.Object.DestroyImmediate(image); throw; }
            finally { RenderTexture.active = previous; target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        }
        private static bool Contains(Rect r, Vector2 p, float margin) { return p.x - margin >= r.x - .001f && p.x + margin <= r.xMax + .001f && p.y - margin >= r.y - .001f && p.y + margin <= r.yMax + .001f; }
        private static bool Same(Color32 a, Color32 b) { return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a; }
        private static bool Equal(Color32[] a, Color32[] b) { for (int i = 0; i < a.Length; i++) if (!Same(a[i], b[i])) return false; return true; }
        private static void Check(bool ok, string label) { if (!ok) throw new InvalidOperationException(label); checks++; Report.AppendLine("PASS " + label); }
        private static void Capture(string text, string trace, LogType type)
        { if ((type == LogType.Error || type == LogType.Exception) && (text.Contains("GUI") || text.Contains("GUILayout"))) Errors.Add(text); }
        private static void Finish(Exception error)
        {
            EditorApplication.update -= Ready; Application.logMessageReceived -= Capture;
            if (presetProbe != null) { presetProbe.Close(); presetProbe = null; }
            if (window != null) { window.Close(); window = null; }
            Report.AppendLine("Checks=" + checks + "\nResult=" + (error == null ? "PASS" : "FAIL"));
            if (error != null) { Report.AppendLine(error.ToString()); Debug.LogException(error); }
            File.WriteAllText(prefix + "-result.txt", Report.ToString(), Encoding.UTF8);
            File.WriteAllText(Evidence + "contained-curve-20261112-latest-result-path.txt", prefix + "-result.txt", Encoding.UTF8);
            Debug.Log(Report.ToString()); URflowVisuals.Dispose(); EditorApplication.Exit(error == null ? 0 : 1);
        }
    }

    public sealed class URFlowContainedPreset20261112Probe : EditorWindow
    {
        internal URflowWindow target;
        internal BezierPreset preset;
        internal Rect row;
        private void OnGUI()
        {
            if (target == null) return;
            GUILayout.BeginArea(new Rect(0f, 0f, position.width, position.height));
            try { URFlowContainedCurve20261112Checks.Invoke(target, "DrawItem", preset, 0); row = GUILayoutUtility.GetLastRect(); }
            finally { GUILayout.EndArea(); }
        }
    }
}
