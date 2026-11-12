using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using URflow;

// Independent D: c1 fixture. No production project or clip assets are changed.
public static class URFlowMoveMode20261112Checks
{
    internal const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly StringBuilder Report = new StringBuilder();
    private static readonly List<string> GuiErrors = new List<string>();
    private static URFlowMoveMode20261112Probe probe;
    private static object panel;
    private static int checks, ticks;

    public static void Run()
    {
        Report.Clear(); GuiErrors.Clear(); checks = ticks = 0;
        Report.AppendLine("Unity=" + Application.unityVersion + "\nScope=Native IMGUI control events in isolated probe; actual movement math. Full-window screenshot equivalence is not claimed.");
        Application.logMessageReceived += Capture;
        EditorApplication.update += Ready;
    }

    private static void Ready()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || ++ticks < 12) return;
        EditorApplication.update -= Ready;
        try
        {
            foreach (float width in new[] { 280f, 340f })
                foreach (bool chinese in new[] { false, true }) CheckNative(width, chinese);
            Check(GuiErrors.Count == 0, "No GUI/GUILayout errors during native mode events");
            CheckMath();
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    private static void CheckNative(float width, bool chinese)
    {
        probe = ScriptableObject.CreateInstance<URFlowMoveMode20261112Probe>();
        Type type = typeof(URflowWindow).Assembly.GetType("URflow.URflowKeyframeMoveScanPanel", true);
        panel = Activator.CreateInstance(type, new object[] { new Action(probe.Repaint), null });
        probe.panel = panel; probe.chinese = chinese;
        probe.Show(); probe.position = new Rect(160, 160, width, 400); probe.Focus(); Frame();
        string label = " width=" + width + " language=" + (chinese ? "CN" : "EN");
        Check(Mode == URflowKeyframeMoveMode.Translate, "Offset is the initial mode" + label);
        Rect offset = RectOf("OffsetRect"), ratio = RectOf("RatioRect");
        Check(offset.height == 32f && ratio.height == 32f && Math.Abs(offset.width - ratio.width) < .1f && offset.xMax <= ratio.x,
            "Offset/Ratio targets are equally sized and nonoverlapping" + label);
        Selected.Add(new URflowKeyframeMoveScanRow()); Click(offset.center);
        Check(Mode == URflowKeyframeMoveMode.Translate && !Pending && Selected.Count == 1,
            "Clicking the selected Offset retains mode and checks without rescanning" + label);
        Click(ratio.center);
        Check(Mode == URflowKeyframeMoveMode.Proportional && Pending && Selected.Count == 0,
            "Clicking Ratio changes mode and clears stale checks before scanning" + label);
        Invoke(panel, "CancelPending"); Frame();
        Selected.Add(new URflowKeyframeMoveScanRow()); Click(RectOf("RatioRect").center);
        Check(Mode == URflowKeyframeMoveMode.Proportional && !Pending && Selected.Count == 1,
            "Clicking selected Ratio retains mode and checks" + label);
        Click(RectOf("OffsetRect").center);
        Check(Mode == URflowKeyframeMoveMode.Translate && Pending && Selected.Count == 0,
            "Clicking Offset explicitly returns to translation and clears stale checks" + label);
        Invoke(panel, "CancelPending");
        Set(panel, "_pending", true); Frame(); Click(RectOf("RatioRect").center);
        Check(Mode == URflowKeyframeMoveMode.Translate, "Disabled pending state cannot switch mode" + label);
        Invoke(panel, "Dispose"); panel = null; probe.panel = null; probe.Close(); probe = null;
    }

    private static void CheckMath()
    {
        AnimationCurve source = new AnimationCurve(new Keyframe(0f, 2f), new Keyframe(1f, 4f));
        URflowKeyframeMoveMathResult result; string error;
        Check(URflowKeyframeMoveMath.TryBuildCurve(source, 10f, URflowKeyframeMoveAnchor.Last,
            URflowKeyframeMoveMode.Translate, out result, out error) && result.Curve.keys[0].value == 8f && result.Curve.keys[1].value == 10f,
            "Offset retains the established additive transformation");
        Check(URflowKeyframeMoveMath.TryBuildCurve(source, 10f, URflowKeyframeMoveAnchor.Last,
            URflowKeyframeMoveMode.Proportional, out result, out error) && result.Curve.keys[0].value == 5f && result.Curve.keys[1].value == 10f,
            "Ratio retains the established multiplicative transformation");
        Check(source.keys[0].value == 2f && source.keys[1].value == 4f, "Mode calculations leave the source untouched");
    }

    private static URflowKeyframeMoveMode Mode { get { return (URflowKeyframeMoveMode)Get(panel, "_mode"); } }
    private static bool Pending { get { return (bool)Get(panel, "_pending"); } }
    private static HashSet<URflowKeyframeMoveScanRow> Selected { get { return (HashSet<URflowKeyframeMoveScanRow>)Get(panel, "_selected"); } }
    private static Rect RectOf(string name) { return (Rect)Get(panel, name); }
    internal static object Get(object target, string name)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, Flags | BindingFlags.DeclaredOnly); if (field != null) return field.GetValue(target);
            PropertyInfo property = type.GetProperty(name, Flags | BindingFlags.DeclaredOnly); if (property != null) return property.GetValue(target, null);
        }
        throw new MissingMemberException(target.GetType().FullName, name);
    }
    internal static object Invoke(object target, string name, params object[] args)
    {
        try { return target.GetType().GetMethod(name, Flags).Invoke(target, args); }
        catch (TargetInvocationException error) { if (error.InnerException is ExitGUIException) throw (ExitGUIException)error.InnerException; throw; }
    }
    private static void Set(object target, string name, object value) { target.GetType().GetField(name, Flags).SetValue(target, value); }
    private static void Frame() { Send(EventType.MouseMove, new Vector2(4f, 4f)); Invoke(Get(probe, "m_Parent"), "RepaintImmediately"); }
    private static void Send(EventType type, Vector2 point)
    {
        RectOffset border = Get(Get(probe, "m_Parent"), "borderSize") as RectOffset;
        Vector2 shift = border == null ? Vector2.zero : new Vector2(border.left, border.top);
        probe.SendEvent(new Event { type = type, mousePosition = point + shift, button = 0, clickCount = 1 });
    }
    private static void Click(Vector2 point) { Send(EventType.MouseDown, point); Send(EventType.MouseUp, point); Frame(); }
    private static void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); checks++; Report.AppendLine("PASS " + label); }
    private static void Capture(string text, string trace, LogType type)
    { if ((type == LogType.Error || type == LogType.Exception) && (text.Contains("GUI") || text.Contains("GUILayout"))) GuiErrors.Add(text); }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Ready; Application.logMessageReceived -= Capture;
        if (panel != null) { Invoke(panel, "Dispose"); panel = null; }
        if (probe != null) { probe.Close(); probe = null; }
        Report.AppendLine("Checks=" + checks + "\nResult=" + (error == null ? "PASS" : "FAIL"));
        if (error != null) { Report.AppendLine(error.ToString()); Debug.LogException(error); }
        File.WriteAllText("Logs/move-mode-20261112-result.txt", Report.ToString(), Encoding.UTF8);
        Debug.Log(Report.ToString()); EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class URFlowMoveMode20261112Probe : EditorWindow
{
    internal object panel;
    internal bool chinese;
    private void OnGUI()
    {
        if (panel == null) return;
        GUIStyle button = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter, fontSize = 11, fontStyle = FontStyle.Bold };
        GUIStyle selected = new GUIStyle(button);
        Type visuals = typeof(URflowWindow).Assembly.GetType("URflow.URflowVisuals", true);
        visuals.GetMethod("StyleSelectedButton", URFlowMoveMode20261112Checks.Flags).Invoke(null, new object[] { selected });
        GUILayout.BeginArea(new Rect(0f, 0f, position.width, position.height));
        try { URFlowMoveMode20261112Checks.Invoke(panel, "DrawBody", button, selected, (Func<string, string, string>)((en, cn) => chinese ? cn : en)); }
        finally { GUILayout.EndArea(); }
    }
}
