using UnityEditor;
using UnityEngine;

namespace LoogaSoft.Lighting.Editor
{
    public class LoogaBarkShaderGUI : LoogaShaderGUIBase
    {
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            Styles();
            DrawLoogaSoftHeader();

            MaterialProperty baseMap = FindProperty("_BaseMap", properties);
            MaterialProperty normalMap = FindProperty("_NormalMap", properties);
            MaterialProperty smoothness = FindProperty("_Smoothness", properties);
            MaterialProperty windInfluence = FindProperty("_WindInfluence", properties);
            
            MaterialProperty specHighlights = FindProperty("_SpecularHighlights", properties, false);
            MaterialProperty envReflections = FindProperty("_EnvironmentReflections", properties, false);

            Section("Surface Options", "LoogaBark_SurfaceOptions", true, () =>
            {
                materialEditor.TexturePropertySingleLine(new GUIContent("Base Map"), baseMap);
                materialEditor.TexturePropertySingleLine(new GUIContent("Normal Map"), normalMap);
                EditorGUILayout.Space(2);
                materialEditor.ShaderProperty(smoothness, "Smoothness");
                EditorGUILayout.Space();
                materialEditor.TextureScaleOffsetProperty(baseMap);
            });

            Section("Procedural Wind", "LoogaBark_ProceduralWind", true, () =>
            {
                materialEditor.ShaderProperty(windInfluence, "Wind Influence");
            });

            Section("Advanced Options", "LoogaBark_AdvancedOptions", false, () =>
            {
                if (specHighlights != null) materialEditor.ShaderProperty(specHighlights, "Specular Highlights");
                if (envReflections != null) materialEditor.ShaderProperty(envReflections, "Environment Reflections");
                materialEditor.EnableInstancingField();
                materialEditor.RenderQueueField();
            });
        }
    }
}