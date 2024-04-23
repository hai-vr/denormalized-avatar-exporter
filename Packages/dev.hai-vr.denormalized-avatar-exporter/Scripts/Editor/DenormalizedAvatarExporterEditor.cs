#if UNITY_2021
// Hack: There is no Warudo scripting define, and Warudo is not a package,
// so we can't use a versionDefines entry in the asmdef
#define WARUDO_IS_INSTALLED
#endif
using HaiDenormalizedAvatarExporter.Runtime;
using UnityEditor;
using UnityEngine;
using VRM;

namespace HaiDenormalizedAvatarExporter.Editor
{
    [CustomEditor(typeof(DenormalizedAvatarExporter))]
    public class DenormalizedAvatarExporterEditor : UnityEditor.Editor
    {
        private const string AvatarLabel = "Avatar";
        private const string BakeAvatarLabel = "Bake avatar";
        private const string BuildWarudoModLabel = "Build Warudo mod";
        private const string DebugOptionsLabel = "Debug options";
        private const string ExportToVSFAvatarLabel = "Export to .vsfavatar";
        private const string MetaLabel = "VRM Meta";
        private const string VSFAvatarLabel = "VSFAvatar";
        private const string WarudoLabel = "Warudo";

        public override void OnInspectorGUI()
        {
            // As of 2024-04-22:
            // - Warudo requires Unity 2021.3.18f1
            // - VNyan requires Unity 2020.3.48 (and UniVRM 0.104)
            // - VSeeFace requires Unity 2019.4.31f1
            // - VRChat requires Unity 2022.3.6f1 (meaning the user cannot export from a VRChat project to any of the above)
#if WARUDO_IS_INSTALLED
            var isWarudo = true;
#else
            var isWarudo = false;
#endif
            
            var my = (DenormalizedAvatarExporter)target;

            EditorGUILayout.LabelField(AvatarLabel, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(DenormalizedAvatarExporter.avatarRoot)));
            
            EditorGUILayout.Space();

            if (isWarudo)
            {
                // EditorGUILayout.LabelField(WarudoLabel, EditorStyles.boldLabel);
                // EditorGUILayout.PropertyField(
                    // serializedObject.FindProperty(nameof(DenormalizedAvatarExporter.copyToWarudoAppFolder)));
            }
            else
            {
                EditorGUILayout.LabelField(MetaLabel, EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty(nameof(DenormalizedAvatarExporter.overrideMeta)));
                if (my.overrideMeta || AvatarHasNoMeta(my))
                {
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty(nameof(DenormalizedAvatarExporter.metaName)));
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty(nameof(DenormalizedAvatarExporter.metaVersion)));
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty(nameof(DenormalizedAvatarExporter.metaAuthor)));
                }

                EditorGUILayout.Space();

                EditorGUILayout.LabelField(VSFAvatarLabel, EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty(nameof(DenormalizedAvatarExporter.exportFileName)));
                EditorGUILayout.LabelField($"File will be written to {DenormalizedAvatarExporterCore.EnsureFilenameEndsWithVsfavatar(my.exportFileName)}");
            }

            var vsfLabel = my.doNotExportOrBuild ? BakeAvatarLabel : ExportToVSFAvatarLabel;
            var buttonLabel = isWarudo ? BuildWarudoModLabel : vsfLabel;
            
            EditorGUI.BeginDisabledGroup(!isWarudo && string.IsNullOrWhiteSpace(my.exportFileName));
            if (GUILayout.Button(buttonLabel))
            {
                DenormalizedAvatarExporterCore.ExportAvatar(my, isWarudo);
            }
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField(DebugOptionsLabel, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(DenormalizedAvatarExporter.doNotExportOrBuild)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(DenormalizedAvatarExporter.doNotDeleteWorkObjects)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(DenormalizedAvatarExporter.doNotNormalize)));
            
#if HVSFEXPORTER_NDMF_IS_INSTALLED
            EditorGUILayout.LabelField("NDMF was found in the project.");
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(DenormalizedAvatarExporter.doNotExecuteNDMF)));
#endif

            EditorGUILayout.Space();

            serializedObject.ApplyModifiedProperties();
        }

        private static bool AvatarHasNoMeta(DenormalizedAvatarExporter my)
        {
            return my.avatarRoot == null || my.avatarRoot.GetComponent<VRMMeta>() == null;
        }
    }
}