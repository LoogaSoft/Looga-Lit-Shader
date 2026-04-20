using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Lighting.Editor
{
    public class LoogaSkinShaderGUI : ShaderGUI
    {
        private bool _surfaceOptionsFoldout = true;
        private bool _advancedOptionsFoldout = false;
        
        static GUIStyle _header, _box;

        private static void Styles()
        {
            if (_header != null) return;
            _header = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, padding = new RectOffset(0, 0, 0, 4) };
            _box = new GUIStyle("HelpBox") { padding = new RectOffset(8, 8, 6, 6) };
        }

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            Styles();
            DrawLoogaSoftHeader();

            // Fetch Properties
            MaterialProperty baseMap = FindProperty("_BaseMap", properties);
            MaterialProperty baseColor = FindProperty("_BaseColor", properties);
            MaterialProperty normalMap = FindProperty("_NormalMap", properties);
            MaterialProperty normalScale = FindProperty("_NormalScale", properties);
            
            MaterialProperty useMaskMap = FindProperty("_UseMaskMap", properties);
            MaterialProperty maskMap = FindProperty("_MaskMap", properties);
            
            MaterialProperty metallicMap = FindProperty("_MetallicMap", properties);
            MaterialProperty metallic = FindProperty("_Metallic", properties);
            MaterialProperty occlusionMap = FindProperty("_OcclusionMap", properties);
            MaterialProperty occlusionStrength = FindProperty("_OcclusionStrength", properties);
            
            MaterialProperty emissionMap = FindProperty("_EmissionMap", properties);
            MaterialProperty emissionColor = FindProperty("_EmissionColor", properties);
            
            MaterialProperty smoothnessSource = FindProperty("_SmoothnessTextureChannel", properties);
            MaterialProperty baseSmoothness = FindProperty("_BaseSmoothnessScale", properties);
            
            MaterialProperty cavityMap = FindProperty("_CavityMap", properties);
            MaterialProperty lobeMix = FindProperty("_LobeMix", properties);
            MaterialProperty secondarySmoothness = FindProperty("_SecondarySmoothness", properties);
            
            MaterialProperty ssssColor = FindProperty("_SubsurfaceColor", properties);
            MaterialProperty ssssWidth = FindProperty("_ScatterWidth", properties);
            
            MaterialProperty specHighlights = FindProperty("_SpecularHighlights", properties, false);
            MaterialProperty envReflections = FindProperty("_EnvironmentReflections", properties, false);

            // Surface Options Section
            _surfaceOptionsFoldout = Section("Surface Options", _surfaceOptionsFoldout, () =>
            {
                materialEditor.TexturePropertySingleLine(new GUIContent("Base Map"), baseMap, baseColor);
                materialEditor.TexturePropertySingleLine(new GUIContent("Normal Map"), normalMap, normalScale);
                
                EditorGUILayout.Space(2);
                
                // Mask Map Toggle
                materialEditor.ShaderProperty(useMaskMap, "Use Mask Map");
                
                if (useMaskMap.floatValue > 0.5f)
                {
                    materialEditor.TexturePropertySingleLine(new GUIContent("Mask Map (M, AO, S)"), maskMap);
                    EditorGUI.indentLevel += 2;
                    materialEditor.ShaderProperty(baseSmoothness, new GUIContent("Smoothness"));
                    EditorGUI.indentLevel -= 2;
                }
                else
                {
                    materialEditor.TexturePropertySingleLine(new GUIContent("Metallic Map"), metallicMap, metallic);
                    EditorGUI.indentLevel += 2;
                    materialEditor.ShaderProperty(baseSmoothness, new GUIContent("Smoothness"));
                    materialEditor.ShaderProperty(smoothnessSource, new GUIContent("Source"));
                    EditorGUI.indentLevel -= 2;
                    
                    materialEditor.TexturePropertySingleLine(new GUIContent("Occlusion Map"), occlusionMap, occlusionStrength);
                }
                
                materialEditor.TexturePropertySingleLine(new GUIContent("Emission Map"), emissionMap, emissionColor);

                EditorGUILayout.Space(4);
                GUILayout.Label("Dual Lobe (Oily Layer)", EditorStyles.boldLabel);
                
                materialEditor.TexturePropertySingleLine(new GUIContent("Cavity/Lobe Mask"), cavityMap, lobeMix);
                EditorGUI.indentLevel += 2;
                materialEditor.ShaderProperty(secondarySmoothness, new GUIContent("Secondary Smoothness"));
                EditorGUI.indentLevel -= 2;
                
                EditorGUILayout.Space(4);
                GUILayout.Label("Subsurface Scattering", EditorStyles.boldLabel);
                
                materialEditor.ShaderProperty(ssssColor, "Subsurface Color");
                materialEditor.ShaderProperty(ssssWidth, "Scatter Width");

                EditorGUILayout.Space();
                materialEditor.TextureScaleOffsetProperty(baseMap);
            });
            
            // Advanced Options Section
            _advancedOptionsFoldout = Section("Advanced Options", _advancedOptionsFoldout, () =>
            {
                if (specHighlights != null) materialEditor.ShaderProperty(specHighlights, "Specular Highlights");
                if (envReflections != null) materialEditor.ShaderProperty(envReflections, "Environment Reflections");
                
                EditorGUILayout.Space();
                materialEditor.EnableInstancingField();
                materialEditor.RenderQueueField();
            });
        }

        private bool Section(string title, bool show, System.Action content)
        {
            EditorGUILayout.BeginVertical(_box);
            Rect full = GUILayoutUtility.GetRect(GUIContent.none, _header);
            full.height += 4f; full.y -= 2f; full.width += 8f; full.x -= 4f;
            Rect text  = new Rect(full.x + 4, full.y + 1, full.width - 24, full.height);
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

        private void DrawLoogaSoftHeader()
        {
            GUIStyle titleStyle = new GUIStyle()
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }
            };
            
            EditorGUILayout.Space(3);
            GUILayout.Label("-  LoogaSoft  -", titleStyle);
            EditorGUILayout.Space(3);
        }
    }
}