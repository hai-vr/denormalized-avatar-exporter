// MIT License
// 
// Copyright (c) 2023-2024 bd_ (and other contributors)
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

//

// Original source: https://github.com/bdunderscore/ndmf/blob/main/Editor/API/BuildContext.cs

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace HaiDenormalizedAvatarExporter.Editor.ThirdPartyLicense.NDMF
{
    public class ThirdParty_BuildSerialize
    {
        public void Serialize(UnityObject _avatarRootObject, UnityObject AssetContainer)
        {
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(AssetContainer)))
            {
                return; // unit tests with no serialized assets
            }

            HashSet<UnityObject> _savedObjects =
                new HashSet<UnityObject>(AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(AssetContainer)));

            _savedObjects.Remove(AssetContainer);

            int index = 0;
            foreach (var asset in ThirdParty_VisitAssets.ReferencedAssets(_avatarRootObject, traverseSaved: true, includeScene: false))
            {
                if (asset is MonoScript)
                {
                    // MonoScripts aren't considered to be a Main or Sub-asset, but they can't be added to asset
                    // containers either.
                    continue;
                }

                if (_savedObjects.Contains(asset))
                {
                    _savedObjects.Remove(asset);
                    continue;
                }

                if (asset == null)
                {
                    Debug.Log($"Asset {index} is null");
                }

                index++;

                if (!EditorUtility.IsPersistent(asset))
                {
                    try
                    {
                        AssetDatabase.AddObjectToAsset(asset, AssetContainer);
                    }
                    catch (UnityException ex)
                    {
                        Debug.Log(
                            $"Error adding asset {asset} p={AssetDatabase.GetAssetOrScenePath(asset)} isMain={AssetDatabase.IsMainAsset(asset)} " +
                            $"isSub={AssetDatabase.IsSubAsset(asset)} isForeign={AssetDatabase.IsForeignAsset(asset)} isNative={AssetDatabase.IsNativeAsset(asset)}");
                        throw ex;
                    }
                }
            }

            // SaveAssets to make sub-assets visible on the Project window
            AssetDatabase.SaveAssets();
            
            // FIXME: Denromalizer: The following is *not* necessary is it?
            //
            // foreach (var assetToHide in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(AssetContainer)))
            // {
            //     if (assetToHide != AssetContainer && 
            //         GeneratedAssetBundleExtractor.IsAssetTypeHidden(assetToHide.GetType()))
            //     {
            //         assetToHide.hideFlags = HideFlags.HideInHierarchy;
            //     }
            // }
            
            // Remove obsolete temporary assets
            foreach (var asset in _savedObjects)
            {
                if (!(asset is Component || asset is GameObject))
                {
                    // Traversal can't currently handle prefabs, so this must have been manually added. Avoid purging it.
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(asset);
            }
        }
    }
}