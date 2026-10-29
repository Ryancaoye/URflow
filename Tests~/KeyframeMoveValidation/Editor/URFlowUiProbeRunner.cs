using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using URflow;

public static class URFlowUiProbeRunner
{
    static int _frames;
    static int _captures;
    static bool _actualSizes;
    static bool _keyOnly;

    public static void Run()
    {
        _actualSizes = false;
        _keyOnly = false;
        Schedule();
    }

    public static void RunActual()
    {
        _actualSizes = true;
        _keyOnly = false;
        Schedule();
    }

    public static void RunKeys()
    {
        _actualSizes = true;
        _keyOnly = true;
        Schedule();
    }

    static void Schedule()
    {
        _frames = 0;
        _captures = 0;
        EditorApplication.update += Ready;
    }

    static void Ready()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || ++_frames < 12) return;
        EditorApplication.update -= Ready;
        int exit = 0;
        bool hadLanguage = EditorPrefs.HasKey("URflow_Language");
        int language = EditorPrefs.GetInt("URflow_Language", 0);
        try
        {
            URflow.Tests.URflowKeyframeMoveUiValidation.CaptureCheckpoint = Capture;
            if (_keyOnly) URflow.Tests.URflowKeyframeMoveUiValidation.RunKeyInteractions();
            else if (_actualSizes) URflow.Tests.URflowKeyframeMoveUiValidation.RunActualNativeSizes();
            else URflow.Tests.URflowKeyframeMoveUiValidation.RunAll();
        }
        catch (Exception e) { exit = 1; Debug.LogException(e); }
        finally
        {
            URflow.Tests.URflowKeyframeMoveUiValidation.CaptureCheckpoint = null;
            if (hadLanguage) EditorPrefs.SetInt("URflow_Language", language);
            else EditorPrefs.DeleteKey("URflow_Language");
            Directory.CreateDirectory("Logs");
            File.WriteAllText(Path.GetFullPath(_keyOnly ? "Logs/ui-key-interactions-result.txt" : _actualSizes ? "Logs/ui-actual-native-result.txt" : "Logs/ui-event-probe-result.txt"), (URflow.Tests.URflowKeyframeMoveUiValidation.LastReport ?? "UIEventChecks=UNAVAILABLE") + "CapturedCheckpointCount=" + _captures + "\nCapturedImagesRequireVisualInspection=YES\n");
            Debug.Log("URFLOW_UI_PROBE_EXIT " + exit + " captures=" + _captures);
            EditorApplication.Exit(exit);
        }
    }

    static void Capture(URflowWindow window, string checkpoint)
    {
        if (Application.isBatchMode || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        var parentField = typeof(EditorWindow).GetField("m_Parent", BindingFlags.Instance | BindingFlags.NonPublic);
        object parent = parentField != null ? parentField.GetValue(window) : null;
        if (parent != null)
        {
            var repaint = parent.GetType().GetMethod("RepaintImmediately", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (repaint != null) repaint.Invoke(parent, null);
        }
        MethodInfo capture = null;
        for (Type type = parent != null ? parent.GetType() : null; type != null && capture == null; type = type.BaseType)
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                if (method.Name == "CaptureRenderTexture" && method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(RenderTexture)) { capture = method; break; }
        if (capture == null) throw new Exception("This Unity GUIView has no CaptureRenderTexture API; screen capture is skipped because other desktop windows may occlude it.");
        var parentPositionProperty = parent.GetType().GetProperty("position", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Rect renderPosition = parentPositionProperty != null ? (Rect)parentPositionProperty.GetValue(parent, null) : window.position;
        int width = Mathf.RoundToInt(renderPosition.width * EditorGUIUtility.pixelsPerPoint);
        int height = Mathf.RoundToInt(renderPosition.height * EditorGUIUtility.pixelsPerPoint);
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            target.Create();
            capture.Invoke(parent, new object[] { target });
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            Directory.CreateDirectory("Logs/WindowCaptures");
            string safe = Regex.Replace(checkpoint, "[^A-Za-z0-9_-]", "_");
            File.WriteAllBytes(Path.GetFullPath("Logs/WindowCaptures/" + safe + ".png"), texture.EncodeToPNG());
            _captures++;
        }
        finally
        {
            RenderTexture.active = previous;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
