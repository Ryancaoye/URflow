using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;
using UnityEngine.UIElements;

namespace URflow.Tests
{
    /// <summary>
    /// Real EditorWindow.SendEvent probes, not pixel or screenshot validation.
    /// The preview table is seeded with read-only sample data and apply is disabled.
    /// </summary>
    public static class URflowKeyframeMoveUiValidation
    {
        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly List<string> _guiErrors = new List<string>();
        private static readonly List<string> _observedSizes = new List<string>();
        private static int _passed;
        private static int _sentEvents;
        public static string LastReport { get; private set; }
        public static Action<URflowWindow, string> CaptureCheckpoint { get; set; }

        public static int RunAll()
        {
            return RunCore(true);
        }

        public static int RunActualNativeSizes()
        {
            return RunCore(false);
        }

        public static int RunKeyInteractions()
        {
            _passed = 0;
            _sentEvents = 0;
            _guiErrors.Clear();
            _observedSizes.Clear();
            var failures = new List<string>();
            Application.logMessageReceived += CaptureLog;
            try
            {
                ProbeKeyInteractions(320f, 560f, failures);
                ProbeKeyInteractions(480f, 720f, failures);
                Require(_guiErrors.Count == 0, "GUI/GUILayout errors:\n" + string.Join("\n", _guiErrors.ToArray()));
                Require(failures.Count == 0, string.Join("\n", failures.ToArray()));
                LastReport = "KeyInteractionChecks=" + _passed + " PASS\nSendEventCount=" + _sentEvents
                    + "\nNativeSizeMode=OBSERVED_ACTUAL\nRequestedSizes=320x560,480x720\nObservedSizes="
                    + string.Join(",", _observedSizes.ToArray()) + "\nCoverage=scrollwheel,close,escape\n"
                    + "OpeningFixture=DirectAnimBoolAndReadOnlyPreviewSeed\nVisualPixelQA=NOT_PERFORMED\n";
                Debug.Log(LastReport);
                return _passed;
            }
            catch (Exception exception)
            {
                LastReport = "KeyInteractionChecks=FAIL\nPassedBeforeFailure=" + _passed + "\nSendEventCount=" + _sentEvents
                    + "\nNativeSizeMode=OBSERVED_ACTUAL\nRequestedSizes=320x560,480x720\nObservedSizes="
                    + string.Join(",", _observedSizes.ToArray()) + "\nCoverage=scrollwheel,close,escape\n"
                    + "OpeningFixture=DirectAnimBoolAndReadOnlyPreviewSeed\nVisualPixelQA=NOT_PERFORMED\n" + exception;
                throw;
            }
            finally { Application.logMessageReceived -= CaptureLog; }
        }

        public static void ExecuteKeyInteractions()
        {
            int exitCode = 0;
            try { RunKeyInteractions(); }
            catch (Exception exception) { exitCode = 1; Debug.LogException(exception); }
            string logDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs"));
            Directory.CreateDirectory(logDirectory);
            File.WriteAllText(Path.Combine(logDirectory, "ui-key-interactions-result.txt"), LastReport ?? "KeyInteractionChecks=UNAVAILABLE");
            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
        }

        private static void ProbeKeyInteractions(float requestedWidth, float requestedHeight, List<string> failures)
        {
            URflowWindow window = null;
            GameObject fixtureRoot = null;
            AnimationClip fixtureClip = null;
            try
            {
                window = ScriptableObject.CreateInstance<URflowWindow>();
                window.titleContent = new GUIContent("URFlow Key Interaction Probe");
                window.minSize = new Vector2(320f, 560f);
                window.Show();
                window.position = new Rect(100f, 100f, requestedWidth, requestedHeight);
                window.Focus();
                Set(window, "_viewMode", 0);
                Set(window, "_filter", 0);
                Set(window, "_searchQuery", string.Empty);
                Set(window, "_presetScrollPos", Vector2.zero);
                SetCurveFixture(window);
                Frame(window);
                string size = "requested=" + requestedWidth + "x" + requestedHeight + " actual="
                    + window.position.width + "x" + window.position.height;
                _observedSizes.Add(size);
                Debug.Log("UI key interaction actual dimensions: " + size + " " + NativeWindowDiagnostics(window));
                object host = Get(window, "_toolsHost");
                AnimBool open = (AnimBool)Get(host, "_open");
                fixtureRoot = new GameObject("UIKeyProbeRoot", typeof(RectTransform));
                fixtureRoot.hideFlags = HideFlags.HideAndDontSave;
                fixtureClip = new AnimationClip { name = "UIKeyProbeClip", hideFlags = HideFlags.HideAndDontSave };
                open.value = true;
                SeedPreview(host, fixtureRoot, fixtureClip);
                ForceNativeFrame(window);
                Frame(window);
                AssertNoHostDrawingError(host);
                Rect panelViewport = FindPanelLayoutRect(window, host, open, false);
                Rect legacyViewport = FindLegacyScrollRect(window);
                Rect intersection = Rect.MinMaxRect(Mathf.Max(panelViewport.xMin, legacyViewport.xMin),
                    Mathf.Max(panelViewport.yMin, legacyViewport.yMin), Mathf.Min(panelViewport.xMax, legacyViewport.xMax),
                    Mathf.Min(panelViewport.yMax, legacyViewport.yMax));
                Require(intersection.width > 0f && intersection.height > 0f,
                    "No actual cached viewport overlap for the wheel test. Panel=" + panelViewport + " legacy=" + legacyViewport);
                DiagnosePanelWidths(window);
                object panel = Get(host, "_panel");
                Vector2 legacyBefore = (Vector2)Get(window, "_presetScrollPos");
                Vector2 panelBefore = (Vector2)Get(panel, "_scroll");
                Scroll(window, intersection.center);
                Frame(window);
                Vector2 legacyAfter = (Vector2)Get(window, "_presetScrollPos");
                Vector2 panelAfter = (Vector2)Get(panel, "_scroll");
                Debug.Log("UI key wheel panelViewport=" + panelViewport + " legacyViewport=" + legacyViewport
                    + " intersection=" + intersection + " legacy=" + legacyBefore + " -> " + legacyAfter
                    + " panel=" + panelBefore + " -> " + panelAfter);
                RecordKeyCheck(legacyBefore == legacyAfter && panelAfter.y > panelBefore.y,
                    size + " ScrollWheel routes to the panel while preserving background scroll", failures);

                Rect closeRect = FindPanelLayoutRect(window, host, open, true);
                LogInputDiagnostics(window, host, open, closeRect);
                Click(window, closeRect.center);
                RecordKeyCheck(!open.target, size + " actual cached close button closes the panel", failures);
                open.value = false;
                Frame(window);
                open.value = true;
                Frame(window);
                Key(window, KeyCode.Escape);
                RecordKeyCheck(!open.target, size + " Escape closes the panel", failures);
                open.value = false;
                Frame(window);
                AssertNoHostDrawingError(host);
            }
            finally
            {
                if (window != null) window.Close();
                if (fixtureRoot != null) UnityEngine.Object.DestroyImmediate(fixtureRoot);
                if (fixtureClip != null) UnityEngine.Object.DestroyImmediate(fixtureClip);
            }
        }

        private static void RecordKeyCheck(bool passed, string label, List<string> failures)
        {
            if (passed) Pass(label);
            else { failures.Add(label); Debug.LogWarning("FAIL UI: " + label); }
        }

        private static Rect FindLegacyScrollRect(URflowWindow window)
        {
            Frame(window);
            object cache = GetWindowLayoutCache(window);
            object top = ReadMember(cache.GetType(), cache, "topLevel", InstanceMembers);
            var candidates = new List<Rect>();
            string dump = string.Empty;
            CollectLayoutRects(top, false, candidates, ref dump);
            if (candidates.Count == 0)
            {
                object areas = ReadMember(cache.GetType(), cache, "windows", InstanceMembers);
                var entries = ReadMember(areas.GetType(), areas, "entries", InstanceMembers) as IEnumerable;
                foreach (object area in entries)
                {
                    Rect areaRect = (Rect)ReadMember(area.GetType(), area, "rect", InstanceMembers);
                    if (Mathf.Abs(areaRect.width - window.position.width) > 1f
                        || Mathf.Abs(areaRect.height - window.position.height) > 1f) continue;
                    var inArea = new List<Rect>();
                    CollectLayoutRects(area, false, inArea, ref dump);
                    foreach (Rect rect in inArea)
                        candidates.Add(new Rect(rect.position + areaRect.position, rect.size));
                    Debug.Log("UI legacy scroll actual cache source=OwnedIMGUIContainerFullWindowArea area=" + areaRect);
                }
            }
            Require(candidates.Count == 1, "Expected one actual legacy ScrollGroup, found " + candidates.Count + ".\n" + dump);
            return new Rect(candidates[0].position + ParentContentOffset(window), candidates[0].size);
        }

        private static void DiagnosePanelWidths(URflowWindow window)
        {
            object cache = GetWindowLayoutCache(window);
            object areas = ReadMember(cache.GetType(), cache, "windows", InstanceMembers);
            var entries = ReadMember(areas.GetType(), areas, "entries", InstanceMembers) as IEnumerable;
            foreach (object area in entries)
            {
                Rect areaRect = (Rect)ReadMember(area.GetType(), area, "rect", InstanceMembers);
                if (Mathf.Abs(areaRect.width - window.position.width) > 1f
                    || Mathf.Abs(areaRect.height - (window.position.height - 92f)) > 1f) continue;
                DiagnoseLayoutEntry(area);
            }
        }

        private static void DiagnoseLayoutEntry(object entry)
        {
            Rect rect = (Rect)ReadMember(entry.GetType(), entry, "rect", InstanceMembers);
            GUIStyle style = ReadMember(entry.GetType(), entry, "style", InstanceMembers) as GUIStyle;
            if (entry.GetType().Name.IndexOf("ScrollGroup", StringComparison.OrdinalIgnoreCase) >= 0)
                Debug.Log("UI panel scroll width cache rect=" + rect
                    + " clientWidth=" + ReadMember(entry.GetType(), entry, "clientWidth", InstanceMembers)
                    + " calculatedMinWidth=" + ReadMember(entry.GetType(), entry, "calculatedMinWidth", InstanceMembers)
                    + " minWidth=" + ReadMember(entry.GetType(), entry, "minWidth", InstanceMembers));
            if (style != null && style.alignment == TextAnchor.MiddleRight && Mathf.Abs(rect.width - 56f) < 0.1f)
                Debug.Log("UI numeric column cached rect=" + rect);
            var children = ReadMember(entry.GetType(), entry, "entries", InstanceMembers) as IEnumerable;
            if (children == null) return;
            foreach (object child in children) DiagnoseLayoutEntry(child);
        }

        private static int RunCore(bool strictSize)
        {
            _passed = 0;
            _sentEvents = 0;
            _guiErrors.Clear();
            _observedSizes.Clear();
            Application.logMessageReceived += CaptureLog;
            try
            {
                ProbeSize(320f, 560f, strictSize);
                ProbeSize(480f, 720f, strictSize);
                Require(_guiErrors.Count == 0, "GUI/GUILayout errors:\n" + string.Join("\n", _guiErrors.ToArray()));
                LastReport = "UIEventChecks=" + _passed + " PASS\n"
                    + "SendEventCount=" + _sentEvents + "\n"
                    + "NativeSizeMode=" + (strictSize ? "STRICT" : "OBSERVED_ACTUAL") + "\n"
                    + "RequestedSizes=320x560,480x720\n"
                    + "ObservedSizes=" + string.Join(",", _observedSizes.ToArray()) + "\n"
                    + "GraphicsDevice=" + SystemInfo.graphicsDeviceType + "\n"
                    + "VisualPixelQA=NOT_PERFORMED\n"
                    + "Coverage=normal,open,close,save-overlay,settings,background-input,preview-table,scrollwheel,escape\n";
                Debug.Log(LastReport);
                return _passed;
            }
            catch (Exception ex)
            {
                LastReport = "UIEventChecks=FAIL\nPassedBeforeFailure=" + _passed
                    + "\nSendEventCount=" + _sentEvents
                    + "\nNativeSizeMode=" + (strictSize ? "STRICT" : "OBSERVED_ACTUAL")
                    + "\nRequestedSizes=320x560,480x720\nObservedSizes=" + string.Join(",", _observedSizes.ToArray())
                    + "\nVisualPixelQA=NOT_PERFORMED\n" + ex;
                throw;
            }
            finally
            {
                Application.logMessageReceived -= CaptureLog;
            }
        }

        public static void Execute()
        {
            ExecuteCore(true);
        }

        public static void ExecuteActualNativeSizes()
        {
            ExecuteCore(false);
        }

        private static void ExecuteCore(bool strictSize)
        {
            int exitCode = 0;
            try { RunCore(strictSize); }
            catch (Exception ex) { exitCode = 1; Debug.LogException(ex); }
            string logDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs"));
            Directory.CreateDirectory(logDirectory);
            File.WriteAllText(Path.Combine(logDirectory, strictSize ? "ui-event-probe-result.txt" : "ui-actual-native-result.txt"),
                LastReport ?? "UIEventChecks=UNAVAILABLE");
            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
        }

        private static void ProbeSize(float width, float height, bool strictSize)
        {
            URflowWindow window = null;
            GameObject fixtureRoot = null;
            AnimationClip fixtureClip = null;
            string requestedSize = width + "x" + height;
            string size = requestedSize;
            try
            {
                window = ScriptableObject.CreateInstance<URflowWindow>();
                window.titleContent = new GUIContent("URFlow UI Probe " + size);
                window.minSize = new Vector2(320f, 560f);
                window.Show();
                window.position = new Rect(100f, 100f, width, height);
                window.Focus();
                Set(window, "_viewMode", 0);
                Set(window, "_filter", 0);
                Set(window, "_searchQuery", string.Empty);
                Set(window, "_presetScrollPos", Vector2.zero);
                SetCurveFixture(window);
                if (strictSize) SettleNativeContentSize(window, width, height);
                else Frame(window);
                _observedSizes.Add("requested=" + requestedSize + " actual=" + window.position.width + "x" + window.position.height);
                if (strictSize)
                    Require(Mathf.Abs(window.position.width - width) < 1f
                        && Mathf.Abs(window.position.height - height) < 1f,
                        "The editor could not provide the requested " + size + " window size. " + NativeWindowDiagnostics(window));
                else
                {
                    width = window.position.width;
                    height = window.position.height;
                    size = "requested=" + requestedSize + " actual=" + width + "x" + height;
                    Debug.Log("UI observed native dimensions: " + size + " " + NativeWindowDiagnostics(window));
                }

                object host = Get(window, "_toolsHost");
                AnimBool open = (AnimBool)Get(host, "_open");
                Require(!open.target, "new tool started open");
                Vector2 presetPoint = FindPresetPoint(window, height);
                SetCurveFixture(window);
                Vector2 handlePoint = FindHandlePoint(window, width);
                SetCurveFixture(window);
                Frame(window);
                Capture(window, size, "normal");
                Pass(size + " normal layout and live legacy input controls");

                CoreSnapshot baseline = new CoreSnapshot(window);
                ClickToolEntry(window, height, open);
                Require(open.target, "toolbar click did not open the panel");
                // Complete the tween for deterministic endpoint layout probes.
                open.value = true;
                fixtureRoot = new GameObject("UIProbeRoot", typeof(RectTransform));
                fixtureRoot.hideFlags = HideFlags.HideAndDontSave;
                fixtureClip = new AnimationClip { name = "UIProbeClip", hideFlags = HideFlags.HideAndDontSave };
                SeedPreview(host, fixtureRoot, fixtureClip);
                Frame(window);
                AssertNoHostDrawingError(host);
                Capture(window, size, "open-preview");
                baseline.Unchanged(window, "opening panel");
                Pass(size + " real toolbar open and scrollable table layout");

                Send(window, EventType.MouseDown, handlePoint);
                Require((int)Get(window, "_dragging") == 0, "background curve captured the mouse");
                Send(window, EventType.MouseDrag, handlePoint + new Vector2(18f, -15f));
                Send(window, EventType.MouseUp, handlePoint + new Vector2(18f, -15f));
                Frame(window);
                baseline.Unchanged(window, "panel-over-background curve interaction");
                Pass(size + " blocked background curve drag");

                Click(window, presetPoint);
                Frame(window);
                baseline.Unchanged(window, "panel-over-background preset interaction");
                Pass(size + " blocked background preset click");

                object panel = Get(host, "_panel");
                Rect panelScrollRect = FindPanelLayoutRect(window, host, open, false);
                Require(panelScrollRect.Contains(presetPoint),
                    "The preset positive-control point is outside the panel scroll viewport: " +
                    presetPoint + " vs " + panelScrollRect);
                Vector2 legacyScrollBefore = (Vector2)Get(window, "_presetScrollPos");
                Vector2 panelScrollBefore = (Vector2)Get(panel, "_scroll");
                Frame(window);
                Scroll(window, presetPoint);
                Frame(window);
                Require((Vector2)Get(window, "_presetScrollPos") == legacyScrollBefore,
                    "panel ScrollWheel changed the disabled background preset scroll position");
                Require(((Vector2)Get(panel, "_scroll")).y > panelScrollBefore.y,
                    "panel ScrollWheel did not scroll the overflowing preview table");
                baseline.Unchanged(window, "panel ScrollWheel");
                Pass(size + " panel scrolling preserves the background scroll position");

                Set(window, "_showSaveDialog", true);
                Frame(window);
                Capture(window, size, "save-overlay");
                Click(window, FindPanelLayoutRect(window, host, open, true).center);
                Key(window, KeyCode.Escape);
                Frame(window);
                Require(open.target, "disabled save-dialog background accepted panel close input");
                Require((bool)Get(window, "_showSaveDialog"), "probe unexpectedly closed the save dialog");
                baseline.Unchanged(window, "save-dialog background");
                AssertNoHostDrawingError(host);
                Set(window, "_showSaveDialog", false);
                Frame(window);
                Pass(size + " save overlay preserves disabled panel/background");

                Set(window, "_showSettings", true);
                Frame(window);
                Capture(window, size, "settings");
                Key(window, KeyCode.Escape);
                Frame(window);
                Require(open.target, "hidden tool panel handled Escape on the settings page");
                Require((bool)Get(window, "_showSettings"), "settings state changed during probe");
                Set(window, "_showSettings", false);
                Frame(window);
                baseline.Unchanged(window, "settings page return");
                Pass(size + " settings hides panel and restores its state");

                Frame(window);
                Rect closeRect = FindPanelLayoutRect(window, host, open, true);
                LogInputDiagnostics(window, host, open, closeRect);
                Click(window, closeRect.center);
                Require(!open.target, "close button did not close the panel");
                open.value = false;
                Frame(window);
                baseline.Unchanged(window, "closing panel");
                Click(window, presetPoint);
                Require((int)Get(window, "_selectedPresetIndex") >= 0,
                    "legacy presets stopped responding after panel close");
                SetCurveFixture(window);
                Frame(window);
                Pass(size + " close button returns to working legacy controls");

                ClickToolEntry(window, height, open);
                open.value = true;
                Frame(window);
                Key(window, KeyCode.Escape);
                Require(!open.target, "Escape did not close the panel");
                open.value = false;
                Frame(window);
                AssertNoHostDrawingError(host);
                Require(_guiErrors.Count == 0, "GUI/GUILayout errors:\n" + string.Join("\n", _guiErrors.ToArray()));
                Pass(size + " Escape close with balanced GUI layout");
            }
            finally
            {
                if (window != null) window.Close();
                if (fixtureRoot != null) UnityEngine.Object.DestroyImmediate(fixtureRoot);
                if (fixtureClip != null) UnityEngine.Object.DestroyImmediate(fixtureClip);
            }
        }

        private static void SeedPreview(object host, GameObject root, AnimationClip clip)
        {
            object panel = Get(host, "_panel");
            MethodInfo readPreview = panel.GetType().GetMethod("ReadPreview", InstanceMembers);
            if (readPreview != null)
            {
                var callback = (EditorApplication.CallbackFunction)Delegate.CreateDelegate(
                    typeof(EditorApplication.CallbackFunction), panel, readPreview);
                EditorApplication.delayCall -= callback;
            }
            var preview = (URflowKeyframeMovePreview)Activator.CreateInstance(typeof(URflowKeyframeMovePreview), true);
            SetProperty(preview, "Root", root);
            SetProperty(preview, "Clip", clip);
            SetProperty(preview, "Anchor", URflowKeyframeMoveAnchor.Last);
            SetProperty(preview, "Mode", URflowKeyframeMoveMode.Proportional);
            SetProperty(preview, "IsValid", true);
            SetProperty(preview, "Notice", "Read-only UI fixture; no animation data is written.");
            string[] properties = { "m_AnchoredPosition.x", "m_AnchoredPosition.y", "m_LocalPosition.z",
                "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z" };
            float[] targets = { 20f, -20f, 0f, 15f, 100000000f, 25f };
            for (int i = 0; i < properties.Length; i++)
            {
                var row = new URflowKeyframeMovePreviewRow();
                EditorCurveBinding binding = EditorCurveBinding.FloatCurve(
                    "Canvas/Navigation/Example_Button_With_A_Long_Hierarchy_Path_" + i,
                    typeof(RectTransform), properties[i]);
                SetProperty(row, "Binding", binding);
                SetProperty(row, "Label", binding.path + " / " + binding.propertyName);
                SetProperty(row, "Error", string.Empty);
                SetProperty(row, "IsSupported", true);
                URflowKeyframeMoveMathResult result;
                string error;
                Require(URflowKeyframeMoveMath.TryBuildCurve(
                    new AnimationCurve(new Keyframe(0f, 2f), new Keyframe(0.5f, 6f), new Keyframe(1f, 10f)),
                    targets[i], URflowKeyframeMoveAnchor.Last, URflowKeyframeMoveMode.Proportional,
                    out result, out error), error);
                SetProperty(row, "Result", result);
                preview.Rows.Add(row);
            }
            Set(panel, "_anchor", URflowKeyframeMoveAnchor.Last);
            Set(panel, "_mode", URflowKeyframeMoveMode.Proportional);
            Set(panel, "_preview", preview);
            Set(panel, "_error", null);
            Set(panel, "_modifiedCount", null);
            Set(panel, "_notice", "Read-only UI fixture; no animation data is written.");
            // Keep both apply and refresh disabled: the fixture is only for layout.
            Set(panel, "_pending", true);
        }

        private static Vector2 FindPresetPoint(URflowWindow window, float height)
        {
            for (float y = height - 100f; y >= height - 175f; y -= 8f)
            {
                Set(window, "_selectedPresetIndex", -1);
                Vector2 point = new Vector2(window.position.width * 0.5f, y);
                Click(window, point);
                if ((int)Get(window, "_selectedPresetIndex") >= 0) return point;
            }
            throw new InvalidOperationException("SendEvent could not identify a live preset control; UI event support or layout needs inspection.");
        }

        private static Vector2 FindHandlePoint(URflowWindow window, float width)
        {
            float pad = (float)typeof(URflowWindow).GetField("Pad", StaticMembers).GetValue(null);
            float curveHeight = (float)typeof(URflowWindow).GetField("CurveH", StaticMembers).GetValue(null);
            Rect inner = new Rect(pad + 20f, 82f, width - pad * 4f - 40f, curveHeight - 40f);
            MethodInfo n2r = typeof(URflowWindow).GetMethod("N2R", StaticMembers);
            Vector2 nominal = (Vector2)n2r.Invoke(null, new object[] { 0.17f, 0.28f, inner });
            float[] adjustments = { 0f, -10f, 10f, -20f, 20f };
            foreach (float dx in adjustments)
            {
                foreach (float dy in adjustments)
                {
                    Vector2 point = nominal + new Vector2(dx, dy);
                    Send(window, EventType.MouseDown, point);
                    bool captured = (int)Get(window, "_dragging") != 0;
                    Send(window, EventType.MouseUp, point);
                    if (captured) return point;
                }
            }
            throw new InvalidOperationException("SendEvent could not identify a live curve handle; UI event support or layout needs inspection.");
        }

        private static void ClickToolEntry(URflowWindow window, float height, AnimBool open)
        {
            float[] offsets = { 56f, 62f, 50f, 70f };
            foreach (float offset in offsets)
            {
                Click(window, new Vector2(58f, height - offset));
                if (open.target) return;
            }
            throw new InvalidOperationException("SendEvent did not open the actual toolbar button; the probe cannot claim UI coverage.");
        }

        private static Rect FindPanelLayoutRect(URflowWindow window, object host, AnimBool open, bool closeButton)
        {
            Frame(window);
            object cache = GetWindowLayoutCache(window);
            object areas = ReadMember(cache.GetType(), cache, "windows", InstanceMembers);
            var entries = ReadMember(areas.GetType(), areas, "entries", InstanceMembers) as IEnumerable;
            Require(entries != null, "The actual GUILayout area cache had no entries.");
            var candidates = new List<Rect>();
            string dump = string.Empty;
            foreach (object area in entries)
            {
                Rect areaRect = (Rect)ReadMember(area.GetType(), area, "rect", InstanceMembers);
                dump += "AREA " + areaRect + "\n";
                if (Mathf.Abs(areaRect.width - window.position.width) > 1f
                    || Mathf.Abs(areaRect.height - (window.position.height - 92f)) > 1f) continue;
                CollectLayoutRects(area, closeButton, candidates, ref dump);
                for (int i = 0; i < candidates.Count; i++)
                    candidates[i] = new Rect(candidates[i].position + areaRect.position + new Vector2(0f, 56f)
                        + ParentContentOffset(window), candidates[i].size);
            }
            Require(candidates.Count == 1, "Expected exactly one cached " + (closeButton ? "26x24 close button" : "panel scroll viewport")
                + ", found " + candidates.Count + ".\n" + dump + "\n" + InputDiagnostics(window, host, open));
            return candidates[0];
        }

        private static void CollectLayoutRects(object entry, bool closeButton, List<Rect> candidates, ref string dump)
        {
            Rect rect = (Rect)ReadMember(entry.GetType(), entry, "rect", InstanceMembers);
            GUIStyle style = ReadMember(entry.GetType(), entry, "style", InstanceMembers) as GUIStyle;
            dump += entry.GetType().Name + " " + rect + " style=" + (style == null ? "null" : style.name) + "\n";
            if (closeButton)
            {
                if (Mathf.Abs(rect.width - 26f) < 0.1f && Mathf.Abs(rect.height - 24f) < 0.1f)
                    candidates.Add(rect);
            }
            else if (entry.GetType().Name.IndexOf("ScrollGroup", StringComparison.OrdinalIgnoreCase) >= 0)
                candidates.Add(rect);
            var children = ReadMember(entry.GetType(), entry, "entries", InstanceMembers) as IEnumerable;
            if (children == null) return;
            foreach (object child in children) CollectLayoutRects(child, closeButton, candidates, ref dump);
        }

        private static Vector2 ParentContentOffset(URflowWindow window)
        {
            object parent = ReadMember(window.GetType(), window, "m_Parent", InstanceMembers);
            Require(parent != null, "The shown EditorWindow had no actual HostView parent.");
            RectOffset offset = HostViewBorder(parent);
            Require(offset != null, "The actual HostView content border offset was unavailable.");
            return new Vector2(offset.left, offset.top);
        }

        private static RectOffset HostViewBorder(object parent)
        {
            return (ReadMember(parent.GetType(), parent, "borderSize", InstanceMembers)
                ?? ReadMember(parent.GetType(), parent, "m_BorderSize", InstanceMembers)) as RectOffset;
        }

        private static void SettleNativeContentSize(URflowWindow window, float width, float height)
        {
            object parent = ReadMember(window.GetType(), window, "m_Parent", InstanceMembers);
            Require(parent != null, "The shown EditorWindow had no native HostView.");
            object container = ReadMember(parent.GetType(), parent, "window", InstanceMembers);
            Require(container != null, "The shown HostView had no native ContainerWindow.");
            Debug.Log("UI native size before settle: " + NativeWindowDiagnostics(window));
            for (int attempt = 0; attempt < 3; attempt++)
            {
                MethodInfo repaint = parent.GetType().GetMethod("RepaintImmediately", InstanceMembers);
                if (repaint != null) repaint.Invoke(parent, null);
                Frame(window);
                Vector2 actual = window.position.size;
                if (Mathf.Abs(actual.x - width) < 1f && Mathf.Abs(actual.y - height) < 1f) break;
                Rect outer = (Rect)ReadMember(container.GetType(), container, "position", InstanceMembers);
                // Use the actual content-size difference to resize its native container.
                // This includes any tab/border added after Show/Focus without guessing it.
                outer.size += new Vector2(width, height) - actual;
                SetMember(container, "position", outer);
            }
            Frame(window);
            Debug.Log("UI native size after settle: " + NativeWindowDiagnostics(window));
        }

        private static void ForceNativeFrame(URflowWindow window)
        {
            // Supported native input causes the UI Toolkit IMGUI container to run
            // its own layout pass; synthetic Layout/Repaint events may be filtered.
            Send(window, EventType.MouseMove, ParentContentOffset(window) + new Vector2(20f, 20f));
            object parent = ReadMember(window.GetType(), window, "m_Parent", InstanceMembers);
            MethodInfo repaint = parent.GetType().GetMethod("RepaintImmediately", InstanceMembers);
            Require(repaint != null, "The actual HostView had no native RepaintImmediately method.");
            repaint.Invoke(parent, null);
        }

        private static string NativeWindowDiagnostics(URflowWindow window)
        {
            object parent = ReadMember(window.GetType(), window, "m_Parent", InstanceMembers);
            object container = parent == null ? null : ReadMember(parent.GetType(), parent, "window", InstanceMembers);
            return "window.position=" + window.position + " window.minSize=" + window.minSize + " window.maxSize=" + window.maxSize
                + " parent.position=" + (parent == null ? null : ReadMember(parent.GetType(), parent, "position", InstanceMembers))
                + " parent.screenPosition=" + (parent == null ? null : ReadMember(parent.GetType(), parent, "screenPosition", InstanceMembers))
                + " parent.borderSize=" + (parent == null ? null : HostViewBorder(parent))
                + " parent.minSize=" + (parent == null ? null : ReadMember(parent.GetType(), parent, "minSize", InstanceMembers))
                + " parent.maxSize=" + (parent == null ? null : ReadMember(parent.GetType(), parent, "maxSize", InstanceMembers))
                + " container.position=" + (container == null ? null : ReadMember(container.GetType(), container, "position", InstanceMembers));
        }

        private static void SetMember(object target, string name, object value)
        {
            for (Type current = target.GetType(); current != null; current = current.BaseType)
            {
                PropertyInfo property = current.GetProperty(name, InstanceMembers | BindingFlags.DeclaredOnly);
                if (property != null && property.CanWrite) { property.SetValue(target, value, null); return; }
                FieldInfo field = current.GetField(name, InstanceMembers | BindingFlags.DeclaredOnly);
                if (field != null) { field.SetValue(target, value); return; }
            }
            throw new MissingMemberException(target.GetType().FullName, name);
        }

        private static object ReadMember(Type type, object target, string name, BindingFlags flags)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, flags | BindingFlags.DeclaredOnly);
                if (field != null) return field.GetValue(target);
                PropertyInfo property = current.GetProperty(name, flags | BindingFlags.DeclaredOnly);
                if (property != null && property.GetIndexParameters().Length == 0) return property.GetValue(target, null);
            }
            return null;
        }

        private static object GetWindowLayoutCache(URflowWindow window)
        {
            object parent = ReadMember(window.GetType(), window, "m_Parent", InstanceMembers);
            int windowId = window.GetInstanceID();
            int parentId = parent is UnityEngine.Object ? ((UnityEngine.Object)parent).GetInstanceID() : 0;
            object originalIdValue = ReadMember(typeof(GUIUtility), null, "originalID", StaticMembers)
                ?? ReadMember(typeof(GUIUtility), null, "s_OriginalID", StaticMembers);
            int originalId = originalIdValue == null ? 0 : Convert.ToInt32(originalIdValue);
            var candidates = new List<object>();
            var sources = new List<string>();
            var registeredCaches = new List<object>();
            var registeredSources = new List<string>();
            string dump = "windowId=" + windowId + " parentId=" + parentId + " originalId=" + originalId + "\n";
            string[] dictionaries = { "s_StoredLayouts", "s_StoredWindows" };
            foreach (string dictionaryName in dictionaries)
            {
                var dictionary = ReadMember(typeof(GUILayoutUtility), null, dictionaryName, StaticMembers) as IDictionary;
                if (dictionary == null) { dump += dictionaryName + " unavailable\n"; continue; }
                foreach (DictionaryEntry pair in dictionary)
                {
                    dump += dictionaryName + " key=" + pair.Key + " " + LayoutCacheSummary(pair.Value) + "\n";
                    int key = Convert.ToInt32(pair.Key);
                    AddCacheCandidate(registeredCaches, registeredSources, pair.Value, dictionaryName + "[" + key + "]");
                    if (key == windowId || key == parentId || (originalId != 0 && key == originalId))
                        AddCacheCandidate(candidates, sources, pair.Value, dictionaryName + "[" + key + "]");
                }
            }
            foreach (MethodInfo method in typeof(GUILayoutUtility).GetMethods(StaticMembers))
            {
                ParameterInfo[] parameters = method.GetParameters();
                if (method.Name != "GetLayoutCache" || parameters.Length != 2
                    || parameters[0].ParameterType != typeof(int) || parameters[1].ParameterType != typeof(bool)) continue;
                foreach (int id in new[] { windowId, parentId, originalId })
                {
                    if (id == 0) continue;
                    foreach (bool isWindow in new[] { false, true })
                        AddCacheCandidate(candidates, sources, method.Invoke(null, new object[] { id, isWindow }),
                            "GetLayoutCache(" + id + "," + isWindow + ")");
                }
            }
            AddOwnedLayoutCaches(window, "EditorWindow", candidates, sources, ref dump);
            if (parent != null) AddOwnedLayoutCaches(parent, "HostView", candidates, sources, ref dump);
            VisualElement visualRoot = window.rootVisualElement;
            while (visualRoot.parent != null) visualRoot = visualRoot.parent;
            dump += "Actual EditorWindow visual ancestor root=" + visualRoot.GetType().FullName
                + " name=" + visualRoot.name + " childCount=" + visualRoot.hierarchy.childCount + "\n";
            AddVisualTreeLayoutCaches(visualRoot, candidates, sources, ref dump);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (!ContainsExpectedPanelArea(candidates[i], window)) continue;
                Debug.Log("UI actual owned layout cache source=" + sources[i] + " " + LayoutCacheSummary(candidates[i]));
                return candidates[i];
            }
            var inferred = new List<object>();
            var inferredSources = new List<string>();
            for (int i = 0; i < registeredCaches.Count; i++)
                if (ContainsExpectedPanelArea(registeredCaches[i], window))
                    AddCacheCandidate(inferred, inferredSources, registeredCaches[i], registeredSources[i]);
            if (inferred.Count == 1)
            {
                Debug.Log("UI actual layout cache source=" + inferredSources[0]
                    + " OwnershipEvidence=UniqueMatchingSheetAnd26x24CloseLeaf " + LayoutCacheSummary(inferred[0]));
                return inferred[0];
            }
            throw new InvalidOperationException("No unique window/HostView layout cache contained the actual panel sheet and close leaf. Inferred="
                + inferred.Count + "\n" + dump);
        }

        private static void AddCacheCandidate(List<object> candidates, List<string> sources, object cache, string source)
        {
            if (cache == null) return;
            for (int i = 0; i < candidates.Count; i++) if (ReferenceEquals(candidates[i], cache)) return;
            candidates.Add(cache);
            sources.Add(source);
        }

        private static void AddOwnedLayoutCaches(object owner, string ownerName, List<object> candidates,
            List<string> sources, ref string dump)
        {
            for (Type type = owner.GetType(); type != null; type = type.BaseType)
            {
                foreach (FieldInfo field in type.GetFields(InstanceMembers | BindingFlags.DeclaredOnly))
                {
                    if (field.FieldType.Name != "LayoutCache") continue;
                    object cache = field.GetValue(owner);
                    dump += ownerName + "." + field.Name + " " + LayoutCacheSummary(cache) + "\n";
                    AddCacheCandidate(candidates, sources, cache, ownerName + "." + field.Name);
                }
            }
        }

        private static void AddVisualTreeLayoutCaches(VisualElement element, List<object> candidates,
            List<string> sources, ref string dump)
        {
            if (element is IMGUIContainer)
                AddOwnedLayoutCaches(element, "OwnedIMGUIContainer(" + element.name + ")", candidates, sources, ref dump);
            for (int i = 0; i < element.hierarchy.childCount; i++)
                AddVisualTreeLayoutCaches(element.hierarchy[i], candidates, sources, ref dump);
        }

        private static bool ContainsExpectedPanelArea(object cache, URflowWindow window)
        {
            object areas = ReadMember(cache.GetType(), cache, "windows", InstanceMembers);
            if (areas == null) return false;
            var entries = ReadMember(areas.GetType(), areas, "entries", InstanceMembers) as IEnumerable;
            if (entries == null) return false;
            foreach (object entry in entries)
            {
                object rectValue = ReadMember(entry.GetType(), entry, "rect", InstanceMembers);
                if (!(rectValue is Rect)) continue;
                Rect rect = (Rect)rectValue;
                if (Mathf.Abs(rect.width - window.position.width) < 1f
                    && Mathf.Abs(rect.height - (window.position.height - 92f)) < 1f)
                {
                    var closeRects = new List<Rect>();
                    string unusedDump = string.Empty;
                    CollectLayoutRects(entry, true, closeRects, ref unusedDump);
                    if (closeRects.Count == 1) return true;
                }
            }
            return false;
        }

        private static string LayoutCacheSummary(object cache)
        {
            if (cache == null) return "null";
            string summary = "id=" + ReadMember(cache.GetType(), cache, "id", InstanceMembers);
            foreach (string groupName in new[] { "topLevel", "windows" })
            {
                object group = ReadMember(cache.GetType(), cache, groupName, InstanceMembers);
                var entries = group == null ? null : ReadMember(group.GetType(), group, "entries", InstanceMembers) as IEnumerable;
                int count = 0;
                string rects = string.Empty;
                if (entries != null)
                    foreach (object entry in entries)
                    {
                        count++;
                        rects += " " + ReadMember(entry.GetType(), entry, "rect", InstanceMembers);
                    }
                summary += " " + groupName + "Count=" + count + " rects=" + rects;
            }
            return summary;
        }

        private static string InputDiagnostics(URflowWindow window, object host, AnimBool open)
        {
            return "HostViewOffset=" + ParentContentOffset(window) + " GUI.enabled=" + GUI.enabled
                + " hotControl=" + GUIUtility.hotControl + " faded=" + open.faded
                + " target=" + open.target + " panelError=" + Get(host, "_panelError");
        }

        private static void LogInputDiagnostics(URflowWindow window, object host, AnimBool open, Rect closeRect)
        {
            Debug.Log("UI close actual cached rect=" + closeRect + " " + InputDiagnostics(window, host, open));
        }

        private static void SetCurveFixture(URflowWindow window)
        {
            Set(window, "_x1", 0.17f);
            Set(window, "_y1", 0.28f);
            Set(window, "_x2", 0.83f);
            Set(window, "_y2", 0.72f);
            Set(window, "_selectedPresetIndex", -1);
            Set(window, "_dragging", 0);
            typeof(URflowWindow).GetMethod("SyncParam", InstanceMembers).Invoke(window, null);
        }

        private static void Capture(URflowWindow window, string size, string stage)
        {
            Action<URflowWindow, string> callback = CaptureCheckpoint;
            if (callback == null) return;
            try { callback(window, size + "-" + stage); }
            catch (Exception exception)
            {
                // An optional diagnostic capture must not interrupt the event probe.
                Debug.LogWarning("UI checkpoint capture failed for " + size + "-" + stage + ": " + exception.Message);
            }
        }

        private static void Frame(URflowWindow window)
        {
            Send(window, EventType.Layout, Vector2.zero);
            Send(window, EventType.Repaint, Vector2.zero);
        }

        private static void Click(URflowWindow window, Vector2 position)
        {
            Frame(window);
            Send(window, EventType.MouseDown, position);
            Send(window, EventType.MouseUp, position);
            Frame(window);
        }

        private static void Key(URflowWindow window, KeyCode key)
        {
            Event input = new Event { type = EventType.KeyDown, keyCode = key };
            _sentEvents++;
            window.SendEvent(input);
        }

        private static void Scroll(URflowWindow window, Vector2 position)
        {
            Event input = new Event { type = EventType.ScrollWheel, mousePosition = position, delta = new Vector2(0f, 5f) };
            _sentEvents++;
            window.SendEvent(input);
        }

        private static void Send(URflowWindow window, EventType type, Vector2 position)
        {
            Event input = new Event { type = type, mousePosition = position, button = 0, clickCount = 1 };
            _sentEvents++;
            window.SendEvent(input);
        }

        private static void CaptureLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (condition.IndexOf("GUILayout", StringComparison.OrdinalIgnoreCase) >= 0
                || condition.IndexOf("GUI Error", StringComparison.OrdinalIgnoreCase) >= 0
                || condition.IndexOf("Getting control", StringComparison.OrdinalIgnoreCase) >= 0
                || condition.IndexOf("Invalid GUI", StringComparison.OrdinalIgnoreCase) >= 0)
                _guiErrors.Add(condition + "\n" + stackTrace);
        }

        private static void AssertNoHostDrawingError(object host)
        {
            foreach (FieldInfo field in host.GetType().GetFields(InstanceMembers))
            {
                if (field.FieldType != typeof(string) || field.Name.IndexOf("error", StringComparison.OrdinalIgnoreCase) < 0) continue;
                string value = field.GetValue(host) as string;
                Require(string.IsNullOrEmpty(value), "Tool host caught a drawing failure: " + value);
            }
        }

        private static object Get(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, InstanceMembers);
            if (field == null) throw new MissingFieldException(target.GetType().FullName, name);
            return field.GetValue(target);
        }

        private static void Set(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, InstanceMembers);
            if (field == null) throw new MissingFieldException(target.GetType().FullName, name);
            field.SetValue(target, value);
        }

        private static void SetProperty(object target, string name, object value)
        {
            PropertyInfo property = target.GetType().GetProperty(name, InstanceMembers);
            MethodInfo setter = property == null ? null : property.GetSetMethod(true);
            if (setter == null) throw new MissingMemberException(target.GetType().FullName, name);
            setter.Invoke(target, new[] { value });
        }

        private static void Pass(string label)
        {
            _passed++;
            Debug.Log("PASS UI: " + label);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message ?? "UI assertion failed.");
        }

        private sealed class CoreSnapshot
        {
            private readonly Dictionary<string, object> _values = new Dictionary<string, object>();
            private static readonly string[] Names = {
                "_x1", "_y1", "_x2", "_y2", "_selectedPresetIndex", "_filter", "_viewMode", "_searchQuery", "_presetScrollPos"
            };

            public CoreSnapshot(URflowWindow window)
            {
                foreach (string name in Names) _values[name] = Get(window, name);
            }

            public void Unchanged(URflowWindow window, string context)
            {
                foreach (string name in Names)
                    Require(object.Equals(_values[name], Get(window, name)), context + " modified legacy state " + name);
            }
        }
    }
}
