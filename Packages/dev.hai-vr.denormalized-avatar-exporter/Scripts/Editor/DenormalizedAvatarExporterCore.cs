#if UNITY_2021 || HVSFEXPORTER_WARUDO_0_14_3_OR_ABOVE_IS_INSTALLED
// Hack: There is no Warudo package and scripting define in OLD VERSIONS of Warudo,
// so we weren't able to use a versionDefines entry in the asmdef
#define WARUDO_IS_INSTALLED
#endif
using System.Linq;
using HaiDenormalizedAvatarExporter.Editor.ThirdPartyLicense.NDMF;
using HaiDenormalizedAvatarExporter.Runtime;
using UniHumanoid;
using UnityEditor;
using UnityEngine;
using VRM;
using Object = UnityEngine.Object;
using System.IO;
using UnityEditor.Animations;
#if HVSFEXPORTER_NDMF_IS_INSTALLED
using nadena.dev.ndmf;
#endif
#if WARUDO_IS_INSTALLED
using System;
using UMod.Shared;
using UnityEditor.SceneManagement;
#endif

namespace HaiDenormalizedAvatarExporter.Editor
{
    public class DenormalizedAvatarExporterCore
    {
        private const string MsgExportCharacterPrefabIsNotAllowed = "You are not allowed to build an avatar based on Character.prefab, as exporting a Warudo Character Mod using this tool will overwrite Character.prefab, erasing your work.";
        internal const string PackageName = "dev.hai-vr.denormalized-avatar-exporter";

        public static void ExportAvatar(DenormalizedAvatarExporter my, bool isWarudo)
        {
            var exportFullPath = $"{Application.dataPath}/{EnsureFilenameEndsWithVsfavatar(my.exportFileName)}";

            if (isWarudo)
            {
                if (MayBeCharacterPrefab(my.avatarRoot))
                {
                    EditorUtility.DisplayDialog("Export", MsgExportCharacterPrefabIsNotAllowed, "OK");
                    return;
                }
            }

            try
            {
                AssetDatabase.StartAssetEditing();
                DoExportAvatar(my, exportFullPath, isWarudo);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        private static bool MayBeCharacterPrefab(GameObject avatarRoot)
        {
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(avatarRoot);
            if (string.IsNullOrEmpty(path)) return false;
            
            var mayBeCharacterPrefab = path.ToLowerInvariant().EndsWith("character.prefab");
            return mayBeCharacterPrefab;
        }

        private static void DoExportAvatar(DenormalizedAvatarExporter my, string exportFullPath, bool isWarudo)
        {
            var initialCopy = Object.Instantiate(my.avatarRoot);
            var originalAvatarName = my.avatarRoot.name;
            initialCopy.name = $"{originalAvatarName}(Copy)";

            var metaBehaviour = DefineMetaIfNotExists(my, initialCopy);

            // Make sure VRMBlendShapeProxy exist before NDMF runs so that NDMF plugins can add to it.
            if (WhenNotExists<VRMBlendShapeProxy>(initialCopy, out var blendshapeProxy))
            {
                var blendShapeAvatar = ScriptableObject.CreateInstance<BlendShapeAvatar>();
                blendshapeProxy.BlendShapeAvatar = blendShapeAvatar;
            }

            if (!my.doNotExecuteNDMF)
            {
#if HVSFEXPORTER_NDMF_IS_INSTALLED
                Debug.Log("(HaiDenormalizedAvatarExporter) Running NDMF...");
                AvatarProcessor.ProcessAvatar(initialCopy);
#endif
            }
            
            // Prefabs cannot be created if there are missing scripts.
            // (NDMF auto-removes them, but let's execute this all the time regardless of whether NDMF is installed)
            foreach (Transform t in initialCopy.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            }

            if (!my.doNotNormalize)
            {
                CreateParallelHierarchy(initialCopy, metaBehaviour.Meta);
            }
            
            // initialCopy = OldWayNormalizer.OldWayNormalizeBones(my, copy, metaBehaviour, needsPersisting);

            MakeSureAvatarAssetsArePersisted(initialCopy);

            if (!my.doNotExportOrBuild)
            {
                if (!isWarudo)
                {
                    // To export to VSF and delegate to the original VSFAvatar exporter we need to select the avatar in the scene.
                    Selection.activeObject = initialCopy;
                }
                
                // (Hai) I'm not sure why that's the case, but the bundle would not build itself unless it's delayed.
                // TODO: Is it still the case?
                Debug.Log("(HaiDenormalizedAvatarExporter) Waiting for next frame...");
                var buildPlan = new HaiVSFAvatarBuildLater(
                    initialCopy,
                    exportFullPath,
                    my.doNotDeleteWorkObjects,
                    isWarudo,
                    false,
                    originalAvatarName
                );
                buildPlan.ExecuteNextFrame();
            }
        }

        private static void MakeSureAvatarAssetsArePersisted(GameObject vrmNormalized)
        {
            var container = new AnimatorController();
            var containerRoot = $"Packages/{PackageName}/__Generated";
            var containerPath = $"{Path.Combine(containerRoot, "Temp")}.asset";
            Directory.CreateDirectory(containerRoot);
            AssetDatabase.GenerateUniqueAssetPath(containerPath);
            AssetDatabase.CreateAsset(container, containerPath);
            new ThirdParty_BuildSerialize().Serialize(vrmNormalized, container);
        }

        private static void CreateParallelHierarchy(GameObject copy, VRMMetaObject meta)
        {
            var metaComp = copy.GetComponent<VRMMeta>();
            if (metaComp == null) metaComp = copy.AddComponent<VRMMeta>();
            metaComp.Meta = meta; // TODO: Check how VRM does it

            var humanoidComp = copy.GetComponent<VRMHumanoidDescription>();
            if (humanoidComp == null) humanoidComp = copy.AddComponent<VRMHumanoidDescription>();

            var animator = copy.GetComponent<Animator>();
            var boneToTransform = Enumerable.Range(0, (int)HumanBodyBones.LastBone)
                .Cast<HumanBodyBones>()
                .ToDictionary(boneId => boneId, boneId => animator.GetBoneTransform(boneId));
            var boneToParallel = boneToTransform
                .ToDictionary(pair => pair.Key, pair =>
                {
                    if (pair.Value == null) return null;

                    return new GameObject
                    {
                        transform =
                        {
                            position = pair.Value.position
                        },
                        name = pair.Value.name
                    }.transform;
                });

            DenormalizerUtil.RebuildHumanoidChain(boneToParallel);
            DenormalizerUtil.RebuildArmatureBone(copy, boneToTransform, boneToParallel);
            DenormalizerUtil.DeduplicateHumanoidBoneNames(copy, boneToParallel);

            var rebuiltDict = boneToParallel
                .Where(pair => pair.Value != null)
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            var avatarDescription = AvatarDescription.Create();
            avatarDescription.SetHumanBones(rebuiltDict);

#if HVSFEXPORTER_WARUDO_0_14_3_OR_ABOVE_IS_INSTALLED
            var avatarAsset = avatarDescription.CreateAvatarAndSetup(copy.transform);
#else
            var avatarAsset = avatarDescription.CreateAvatar(copy.transform);
#endif
            avatarAsset.name = "AvatarAsset.Normalized";

            // Directly setting the avatar causes issues (the skinned meshes become completely deformed)
            // don't do this: newAnim.avatar = avatarAsset;
            var so = new SerializedObject(animator);
            so.FindProperty("m_Avatar").objectReferenceValue = avatarAsset;
            so.ApplyModifiedPropertiesWithoutUndo();

            humanoidComp.Avatar = avatarAsset;
            humanoidComp.Description = avatarDescription;
        }

        private static bool WhenNotExists<T>(GameObject copy, out T newlyCreated) where T : Component
        {
            var compNullable = copy.GetComponent<T>();
            if (compNullable == null)
            {
                newlyCreated = copy.AddComponent<T>();
                return true;
            }

            newlyCreated = null;
            return false;
        }

        private static VRMMeta DefineMetaIfNotExists(DenormalizedAvatarExporter my, GameObject copy)
        {
            var metaBehaviour = copy.GetComponent<VRMMeta>();
            var avatarHasNoMeta = metaBehaviour == null;
            if (avatarHasNoMeta)
            {
                metaBehaviour = copy.AddComponent<VRMMeta>();
                metaBehaviour.Meta = ScriptableObject.CreateInstance<VRMMetaObject>();
            }

            if (my.overrideMeta || avatarHasNoMeta)
            {
                var meta = metaBehaviour.Meta;
                meta.Title = my.metaName;
                meta.Version = my.metaVersion;
                meta.Author = my.metaAuthor;
            }

            return metaBehaviour;
        }

        public static string EnsureFilenameEndsWithVsfavatar(string fileName)
        {
            if (fileName == null) fileName = "";
            if (!fileName.ToLowerInvariant().EndsWith(".vsfavatar"))
            {
                return fileName + ".vsfavatar";
            }
            return fileName;
        }
    }
    
    internal class HaiVSFAvatarBuildLater
    {
        private readonly GameObject _vrmNormalized;
        private readonly string _exportFullPath;
        private readonly bool _doNotDeleteWorkObjects;
        private readonly bool _buildWarudoMod;
        private readonly bool _copyWarudoFileToWarudoAppFolder;
        private readonly string _originalAvatarName;

        public HaiVSFAvatarBuildLater(GameObject vrmNormalized, string exportFullPath, bool doNotDeleteWorkObjects,
            bool buildWarudoMod, bool copyWarudoFileToWarudoAppFolder, string originalAvatarName)
        {
            _vrmNormalized = vrmNormalized;
            _exportFullPath = exportFullPath;
            _doNotDeleteWorkObjects = doNotDeleteWorkObjects;
            _buildWarudoMod = buildWarudoMod;
            _copyWarudoFileToWarudoAppFolder = copyWarudoFileToWarudoAppFolder;
            _originalAvatarName = originalAvatarName;
        }

        public void ExecuteNextFrame()
        {
            // If we use EditorApplication.delayCall, and the user doesn't have the window in focus,
            // it will not run until it's focused again, which is really confusing.
            // We use the Update hook so that it runs anyway when it's out of focus.
            // Maybe there's a better way to do this.
            EditorApplication.update -= Execute;
            EditorApplication.update += Execute;
        }

        private void Execute()
        {
            EditorApplication.update -= Execute;
            DoExecute();
        }

        private void DoExecute()
        {
            var modifiedName = _vrmNormalized.name;
            try
            {
                _vrmNormalized.name = _originalAvatarName;
                if (_buildWarudoMod)
                {
#if WARUDO_IS_INSTALLED
                    var settings = UMod.ModTools.Export.ExportSettings.Active.Load();
                    
                    var baseAbsolutePath = settings.ActiveExportProfile.ModAssetsPath;
                    var basePath = FileUtil.GetProjectRelativePath(FileSystemUtil.NormalizeDirectory(new DirectoryInfo(baseAbsolutePath)).ToString());
                    var prefabPath = Path.Combine(basePath, "Character.prefab");
                    _vrmNormalized.name = "Character";
                    PrefabUtility.SaveAsPrefabAssetAndConnect(_vrmNormalized, prefabPath, InteractionMode.AutomatedAction);
                    
                    if (!_doNotDeleteWorkObjects)
                    {
                        // Starting a Warudo build will reload the scene, so delete it now.
                        Object.DestroyImmediate(_vrmNormalized);
                    }
                    
                    EditorSceneManager.SaveOpenScenes();
                    
                    var result = UMod.BuildEngine.ModToolsUtil.StartBuild(settings);
                    // if (_copyWarudoFileToWarudoAppFolder)
                    // {
                    //     var fileName = result.BuiltModFile.Name;
                    //     File.Copy(fileName, 
                    //         Path.Combine("C:\\Program Files (x86)\\Steam\\steamapps\\common\\Warudo\\Warudo_Data\\StreamingAssets\\Characters",
                    //             DateTime.Now.ToString("yyyy-MM-dd_HHmmss_") + fileName
                    //         ));
                    // }
#endif
                }
                else
                {
                    Debug.Log("(HaiDenormalizedAvatarExporter) Exporting as a VSFAvatar bundle...");
                    DenormalizerUtil.BuildAssetBundle(_vrmNormalized, _exportFullPath, "VSFAvatar");
                }
            }
            finally
            {
                if (!_doNotDeleteWorkObjects && _vrmNormalized != null)
                {
                    Object.DestroyImmediate(_vrmNormalized);
                }
                else if (_vrmNormalized != null)
                {
                    _vrmNormalized.name = modifiedName;
                }
            }
        }
    }
}