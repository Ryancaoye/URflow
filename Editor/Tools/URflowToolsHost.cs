using System;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;

namespace URflow
{
    // Navigation belongs to the host; animation data belongs to the tool service.
    internal sealed class URflowToolsHost : IDisposable
    {
        private readonly AnimBool _open = new AnimBool(false);
        private readonly URflowKeyframeMovePanel _panel;
        private readonly Action _repaint;
        private string _panelError;

        public bool IsOpen { get { return _open.target || _open.faded > 0f; } }

        public URflowToolsHost(Action repaint)
        {
            _repaint = repaint;
            _panel = new URflowKeyframeMovePanel(repaint);
            _open.speed = 6f;
            _open.valueChanged.AddListener(Repaint);
            Undo.undoRedoPerformed += RefreshIfOpen;
        }

        private void Repaint() { _repaint(); }

        public void RefreshIfOpen()
        {
            if (_open.target) _panel.Refresh();
        }

        public void DrawToolbar(GUIStyle button, Func<string, string, string> text)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(16f);
                GUIStyle entry = new GUIStyle(button)
                {
                    fixedHeight = 46f,
                    fixedWidth = 92f,
                    fontSize = 11,
                    imagePosition = ImagePosition.ImageAbove,
                    padding = new RectOffset(4, 4, 3, 3)
                };
                Texture icon = EditorGUIUtility.IconContent("MoveTool").image;
                if (GUILayout.Button(new GUIContent(text("Keyframe Move", "关键帧移动"), icon,
                    text("Move selected property curves to static values", "将选中属性的整条曲线对齐静态值")), entry))
                {
                    _open.target = true;
                    _panelError = null;
                    _panel.Refresh(true);
                    GUI.FocusControl(null);
                    GUIUtility.ExitGUI();
                }
                GUILayout.FlexibleSpace();
                GUILayout.Space(16f);
            }
        }

        public void DrawPanel(Rect area, GUIStyle button, GUIStyle selected,
            GUIStyle primary, Func<string, string, string> text)
        {
            if (!IsOpen) return;
            if (GUI.enabled && Event.current.type == EventType.KeyDown &&
                Event.current.keyCode == KeyCode.Escape)
            {
                Close();
                Event.current.Use();
            }

            GUI.BeginGroup(area);
            try
            {
                Rect sheet = new Rect(0f, area.height * (1f - _open.faded), area.width, area.height);
                EditorGUI.DrawRect(sheet, new Color(0.16f, 0.16f, 0.16f));
                EditorGUI.DrawRect(new Rect(sheet.x, sheet.y, sheet.width, 1f),
                    new Color(0.25f, 0.25f, 0.25f));
                GUILayout.BeginArea(sheet);
                try
                {
                    if (string.IsNullOrEmpty(_panelError)) _panel.Draw(button, selected, primary, text, Close);
                    else
                    {
                        using (new EditorGUILayout.VerticalScope())
                        {
                            GUILayout.Space(16f);
                            EditorGUILayout.HelpBox(text("Unable to draw the tool panel: ", "无法绘制工具面板：") +
                                _panelError, MessageType.Error);
                            if (GUILayout.Button(text("Close", "关闭"), button, GUILayout.Height(30f))) Close();
                        }
                    }
                }
                catch (ExitGUIException) { throw; }
                catch (Exception exception)
                {
                    _panelError = exception.Message;
                    Repaint();
                    GUIUtility.ExitGUI();
                }
                finally { GUILayout.EndArea(); }
            }
            finally { GUI.EndGroup(); }
        }

        private void Close()
        {
            _open.target = false;
            GUI.FocusControl(null);
            Repaint();
        }

        public void Dispose()
        {
            _open.valueChanged.RemoveListener(Repaint);
            Undo.undoRedoPerformed -= RefreshIfOpen;
            _panel.Dispose();
        }
    }
}
