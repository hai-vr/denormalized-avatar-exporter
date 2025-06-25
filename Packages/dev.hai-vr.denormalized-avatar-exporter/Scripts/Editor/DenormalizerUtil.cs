using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;
using static UnityEngine.HumanBodyBones;
using static UnityEditor.BuildAssetBundleOptions;

namespace HaiDenormalizedAvatarExporter.Editor
{
    internal static class DenormalizerUtil
    {
        public static void RebuildHumanoidChain(Dictionary<HumanBodyBones, Transform> boneToParallel)
        {
            CreateChain(boneToParallel, LeftThumbDistal, LeftThumbIntermediate, LeftThumbProximal, LeftHand);
            CreateChain(boneToParallel, LeftIndexDistal, LeftIndexIntermediate, LeftIndexProximal, LeftHand);
            CreateChain(boneToParallel, LeftMiddleDistal, LeftMiddleIntermediate, LeftMiddleProximal, LeftHand);
            CreateChain(boneToParallel, LeftRingDistal, LeftRingIntermediate, LeftRingProximal, LeftHand);
            CreateChain(boneToParallel, LeftLittleDistal, LeftLittleIntermediate, LeftLittleProximal, LeftHand);
            
            CreateChain(boneToParallel, RightThumbDistal, RightThumbIntermediate, RightThumbProximal, RightHand);
            CreateChain(boneToParallel, RightIndexDistal, RightIndexIntermediate, RightIndexProximal, RightHand);
            CreateChain(boneToParallel, RightMiddleDistal, RightMiddleIntermediate, RightMiddleProximal, RightHand);
            CreateChain(boneToParallel, RightRingDistal, RightRingIntermediate, RightRingProximal, RightHand);
            CreateChain(boneToParallel, RightLittleDistal, RightLittleIntermediate, RightLittleProximal, RightHand);
            
            OptionallyLinkTo(boneToParallel, LeftEye, Head);
            OptionallyLinkTo(boneToParallel, RightEye, Head);
            OptionallyLinkTo(boneToParallel, Jaw, Head);
            
            CreateChain(boneToParallel, LeftHand, LeftLowerArm, LeftUpperArm, LeftShoulder);
            CreateChain(boneToParallel, RightHand, RightLowerArm, RightUpperArm, RightShoulder);

            LinkTo(boneToParallel, Head, Neck);
            var hasUpperChest = boneToParallel[UpperChest] != null;
            if (hasUpperChest)
            {
                LinkTo(boneToParallel, LeftShoulder, UpperChest);
                LinkTo(boneToParallel, RightShoulder, UpperChest);
                LinkTo(boneToParallel, Neck, UpperChest);
                LinkTo(boneToParallel, UpperChest, Chest);
            }
            else
            {
                LinkTo(boneToParallel, LeftShoulder, Chest);
                LinkTo(boneToParallel, RightShoulder, Chest);
                LinkTo(boneToParallel, Neck, Chest);
            }

            LinkTo(boneToParallel, Chest, Spine);
            LinkTo(boneToParallel, Spine, Hips);
            
            CreateChain(boneToParallel, LeftToes, LeftFoot, LeftLowerLeg, LeftUpperLeg, Hips);
            CreateChain(boneToParallel, RightToes, RightFoot, RightLowerLeg, RightUpperLeg, Hips);
        }

        private static void CreateChain(Dictionary<HumanBodyBones, Transform> boneToParallel, params HumanBodyBones[] tipToRoot)
        {
            Transform tipBone = null;
            for (var i = 0; i < tipToRoot.Length - 1; i++)
            {
                var tip = tipToRoot[i];
                var root = tipToRoot[i + 1];
                var potentialTipBone = boneToParallel[tip];
                var potentialRootBone = boneToParallel[root];
                if (potentialTipBone != null)
                {
                    tipBone = potentialTipBone;
                }
                if (tipBone != null && potentialRootBone != null)
                {
                    tipBone.SetParent(potentialRootBone, true);
                }
            }
        }

        private static void LinkTo(Dictionary<HumanBodyBones, Transform> boneToParallel, HumanBodyBones child, HumanBodyBones parent)
        {
            boneToParallel[child].SetParent(boneToParallel[parent], true);
        }

        private static void OptionallyLinkTo(Dictionary<HumanBodyBones, Transform> boneToParallel, HumanBodyBones child, HumanBodyBones parent)
        {
            if (boneToParallel[child] == null) return;
            boneToParallel[child].SetParent(boneToParallel[parent], true);
        }

        public static void RebuildArmatureBone(GameObject copy, Dictionary<HumanBodyBones, Transform> boneToTransform, Dictionary<HumanBodyBones, Transform> boneToParallel)
        {
            var originalHipsParent = boneToTransform[Hips].parent;
            var copyTransform = copy.transform;
            if (originalHipsParent != copyTransform)
            {
                var parallelArmature = new GameObject
                {
                    transform =
                    {
                        position = copyTransform.position,
                        parent = copyTransform
                    },
                    name = originalHipsParent.name
                }.transform;
                
                boneToParallel[Hips].SetParent(parallelArmature, true);
                originalHipsParent.name = $"{originalHipsParent.name}(Denormalized)";
                originalHipsParent.SetParent(parallelArmature, true);
                
                parallelArmature.SetAsFirstSibling();
            }
            else
            {
                boneToParallel[Hips].SetParent(copyTransform, true);
            }

            // Order matters
            
            for (var boneId = Hips; boneId < LastBone; boneId++)
            {
                var original = boneToTransform[boneId];
                if (original != null)
                {
                    var parallel = boneToParallel[boneId];
                    original.SetParent(parallel, true);
                    original.name = $"{original.name}(Denormalized)";
                }
            }
        }

        public static void DeduplicateHumanoidBoneNames(GameObject copy, Dictionary<HumanBodyBones, Transform> boneToParallel)
        {
            var nameToCopies = new Dictionary<string, int>();

            var humanoidBoneNames = new HashSet<string>(boneToParallel.Values.Where(transform => transform != null).Select(transform => transform.name));
            var humanoidBoneTransforms = new HashSet<Transform>(boneToParallel.Values.Where(transform => transform != null));
            
            // Fixes having an object with the same name as the hips parent (armature) hijacking the rotation values of the original hips parent (armature).
            if(boneToParallel.TryGetValue(Hips, out var hips))
            {
                var hipsParent = hips.parent;
                if(hipsParent != null)
                {
                    humanoidBoneTransforms.Add(hipsParent);
                    humanoidBoneNames.Add(hipsParent.name);
                }
            }
            
            foreach (var t in copy.GetComponentsInChildren<Transform>(true))
            {
                var thatName = t.name;
                if (!humanoidBoneTransforms.Contains(t) && humanoidBoneNames.Contains(thatName))
                {
                    if (nameToCopies.TryGetValue(thatName, out var currentValue))
                    {
                        var newValue = currentValue + 1;
                        nameToCopies[thatName] = newValue;
                        t.name = $"{t.name}.{newValue}";
                    }
                    else
                    {
                        var newValue = 1;
                        nameToCopies[thatName] = newValue;
                        t.name = $"{t.name}.{newValue}";
                    }
                }
            }

            if (nameToCopies.Count > 0)
            {
                Debug.LogWarning($"(HaiDenormalizedAvatarExporter) Some transforms of this avatar had the same name as humanoid bones, so they were renamed: {string.Join(", ", nameToCopies.Keys)}");
            }
        }
        
        private const BuildAssetBundleOptions BundleOptions = ForceRebuildAssetBundle | DeterministicAssetBundle | StrictMode;
        private const BuildAssetBundleOptions UncompressedBundleOptions = BundleOptions | UncompressedAssetBundle;

        public static void BuildAssetBundle(GameObject avatar, string writeTo, string addressable)
        {
            var bundleFileName = Path.GetFileName(writeTo);

            var containerRoot = $"Packages/{DenormalizedAvatarExporterCore.PackageName}/__Generated";
            var prefabPath = Path.Combine(containerRoot, $"Temp_{Guid.NewGuid().ToString()}.prefab");
            try
            {
                AssetDatabase.DeleteAsset(prefabPath);
                Directory.CreateDirectory(containerRoot);

                PrefabUtility.SaveAsPrefabAsset(avatar, prefabPath, out var success);
                if (!success)
                {
                    return;
                }
                
                AssetDatabase.RemoveUnusedAssetBundleNames();

                var bundle = new AssetBundleBuild
                {
                    assetBundleName = bundleFileName,
                    assetNames = new[] { prefabPath },
                    addressableNames = new[] { addressable }
                };
                var builds = new[] { bundle };

                var bundleOptions = HasVideoPlayer(avatar) ? UncompressedBundleOptions : BundleOptions;
                BuildPipeline.BuildAssetBundles(Application.temporaryCachePath, builds, bundleOptions, BuildTarget.StandaloneWindows);

                if (File.Exists(writeTo))
                {
                    File.Delete(writeTo);
                }
                File.Move($"{Application.temporaryCachePath}/{bundleFileName}", writeTo);
            }
            finally
            {
                AssetDatabase.DeleteAsset(prefabPath);
            }
        }

        private static bool HasVideoPlayer(GameObject avatar)
        {
            return avatar.GetComponentsInChildren<VideoPlayer>(true).Length > 0;
        }
    }
}
