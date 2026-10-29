using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using URflow;

public static class URFlowValidationBootstrap
{
    const string ExpectedPackagePath = "D:/插件/URFLOW/6-开发/URflow-v1.0.4";
    public static void CompileOnly()
    {
        try
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(URflowWindow).Assembly);
            if (package == null) throw new Exception("URFlow package was not resolved.");
            var actual = Path.GetFullPath(package.resolvedPath).Replace('\\', '/').TrimEnd('/');
            var expected = Path.GetFullPath(ExpectedPackagePath).Replace('\\', '/').TrimEnd('/');
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                throw new Exception("Unexpected URFlow load path: " + actual);
            CreateValidationScene();
            URFlowValidationDiagnostics.DumpLayout();
            int mathCount = URflow.Tests.URflowKeyframeMoveMathChecks.Run();
            var report = "Unity=" + Application.unityVersion + "\nPackage=" + actual + "\nVersion=" + package.version + "\nCompile=PASS\nMathChecks=" + mathCount + " PASS\n";
            File.WriteAllText(Path.GetFullPath("Logs/bootstrap-result.txt"), report);
            Debug.Log("URFLOW_VALIDATION_BOOTSTRAP_PASS\n" + report);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.GetFullPath("Logs/bootstrap-result.txt"), "Compile=PASS\nValidation=FAIL\n" + e);
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    [MenuItem("URFlow Validation/Create and Open Test Scene")]
    public static void CreateValidationScene()
    {
        if (!AssetDatabase.IsValidFolder("Assets/ValidationAssets"))
            AssetDatabase.CreateFolder("Assets", "ValidationAssets");
        const string clipPath = "Assets/ValidationAssets/Alignment.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
        {
            clip = new AnimationClip { name = "Alignment", frameRate = 60 };
            SetCurve(clip, "m_LocalPosition.x", 2f, 6f, 4f);
            SetCurve(clip, "m_LocalPosition.y", -3f, -1f, -2f);
            AssetDatabase.CreateAsset(clip, clipPath);
        }
        const string controllerPath = "Assets/ValidationAssets/Alignment.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddMotion(clip);
        }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var go = new GameObject("URFlow Alignment Test");
        go.AddComponent<Animator>().runtimeAnimatorController = controller;
        Selection.activeGameObject = go;
        EditorSceneManager.SaveScene(scene, "Assets/ValidationAssets/Alignment.unity");
        AssetDatabase.SaveAssets();
    }

    static void SetCurve(AnimationClip clip, string property, float first, float middle, float last)
    {
        var curve = new AnimationCurve(new Keyframe(0, first, 2, 2), new Keyframe(1, middle, -1, -1), new Keyframe(2, last, 1, 1));
        curve.preWrapMode = WrapMode.ClampForever;
        curve.postWrapMode = WrapMode.ClampForever;
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyBroken(curve, i, true);
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Free);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Free);
        }
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), property), curve);
    }
}
