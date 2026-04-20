using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Lighting.Editor
{
    [CustomEditor(typeof(LoogaLightingFeature))]
    public class LoogaLightingFeatureEditor : LoogaSoftBaseEditor
    {
        private SerializedProperty _activeLightingModel;
        private SerializedProperty _enableSSSS;
        private SerializedProperty _ssssScatterWidth;
        private SerializedProperty _ssssColor;

        private bool _foldBase = true;
        private bool _foldSSSS = true;

        static GUIStyle _header, _box;

        private void OnEnable()
        {
            _activeLightingModel = serializedObject.FindProperty("activeLightingModel");
            _enableSSSS = serializedObject.FindProperty("enableSSSS");
            _ssssScatterWidth = serializedObject.FindProperty("ssssScatterWidth");
            _ssssColor = serializedObject.FindProperty("ssssColor");
        }

        static void Styles()
        {
            if (_header != null) return;
            _header = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, padding = new RectOffset(0, 0, 0, 4) };
            _box = new GUIStyle("HelpBox") { padding = new RectOffset(8, 8, 6, 6) };
        }

        public override void OnInspectorGUI()
        {
            Styles();
            serializedObject.Update();
            
            DrawLoogaSoftHeader();
            
            _foldBase = Section("Base Lighting", _foldBase, () =>
            {
                EditorGUILayout.PropertyField(_activeLightingModel);
            });
            
            _foldSSSS = Section("Subsurface Scattering", _foldSSSS, () =>
            {
                EditorGUILayout.PropertyField(_enableSSSS);
                EditorGUILayout.HelpBox("SSSS Color and Scatter Width are now defined per-material on shaders utilizing the SSSS pass.", MessageType.Info);
            });

            serializedObject.ApplyModifiedProperties();
        }

        private bool Section(string title, bool show, System.Action content)
        {
            EditorGUILayout.BeginVertical(_box);
            Rect full = GUILayoutUtility.GetRect(GUIContent.none, _header);
            full.height += 4f; full.y -= 2f; full.width += 8f; full.x -= 4f;
            Rect text  = new Rect(full.x + 4, full.y + 1, full.width - 24, full.height);
            
            // Arrow is offset by 13 to push it 5px to the right, matching the material editor
            Rect arrow = new Rect(full.xMax - 10, full.y, 15, full.height); 
            
            if (full.Contains(Event.current.mousePosition)) EditorGUI.DrawRect(full, new Color(1, 1, 1, 0.05f));
            GUI.Label(text, title, _header);
            
            show = EditorGUI.Foldout(arrow, show, GUIContent.none);
            if (Event.current.type == EventType.MouseDown && full.Contains(Event.current.mousePosition) && Event.current.button == 0)
            { 
                show = !show; 
                Event.current.Use(); 
            }
            
            if (show)
            {
                EditorGUILayout.Space(2);
                content();
                EditorGUILayout.Space(2);
            }
            EditorGUILayout.EndVertical();
            return show;
        }
    }
}