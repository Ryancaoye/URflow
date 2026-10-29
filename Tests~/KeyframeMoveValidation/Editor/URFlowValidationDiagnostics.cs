using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class URFlowValidationDiagnostics
{
    public static void DumpLayout()
    {
        var flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var buffer = new StringBuilder();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = assembly.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
            foreach (var type in types.Where(t => t.Name == "AnimationWindowState" || t.Name == "AnimationWindowCurve" || t.Name == "AnimationWindowKeyframe" || t.Name == "AnimationWindowSelectionItem" || t.Name == "GameObjectSelectionItem" || t.Name == "AnimationWindowHierarchyState" || t.Name == "AnimationWindowHierarchyNode"))
            {
                buffer.AppendLine("TYPE " + type.FullName);
                foreach (var ctor in type.GetConstructors(flags)) buffer.AppendLine("CTOR " + ctor);
                foreach (var prop in type.GetProperties(flags)) buffer.AppendLine("PROP " + prop.PropertyType + " " + prop.Name + " get=" + prop.CanRead + " set=" + prop.CanWrite);
                foreach (var field in type.GetFields(flags)) buffer.AppendLine("FIELD " + field.FieldType + " " + field.Name);
                foreach (var method in type.GetMethods(flags).Where(m => !m.IsSpecialName && (m.Name.IndexOf("Select", StringComparison.OrdinalIgnoreCase) >= 0 || m.Name.IndexOf("Curve", StringComparison.OrdinalIgnoreCase) >= 0 || m.Name.IndexOf("Root", StringComparison.OrdinalIgnoreCase) >= 0 || m.Name.IndexOf("Clip", StringComparison.OrdinalIgnoreCase) >= 0 || m.Name.IndexOf("Refresh", StringComparison.OrdinalIgnoreCase) >= 0))) buffer.AppendLine("METHOD " + method);
            }
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Path.GetFullPath("Logs/reflection-layout.txt"), buffer.ToString());
        Debug.Log("URFLOW_REFLECTION_LAYOUT_WRITTEN");
    }
}
