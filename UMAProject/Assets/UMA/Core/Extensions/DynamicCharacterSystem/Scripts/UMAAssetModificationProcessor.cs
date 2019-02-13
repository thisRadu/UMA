#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace UMA.CharacterSystem
{
	public class UMAAssetPostProcessor : AssetPostprocessor
	{
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (BuildPipeline.isBuildingPlayer || UnityEditorInternal.InternalEditorUtility.inBatchMode || Application.isPlaying)
                return;
            // don't call if it's the indexer that's being updated!!!
            if (UMAAssetIndexer.Instance != null)
            {
                UMAAssetIndexer.Instance.OnPostprocessAllAssets(importedAssets, deletedAssets, movedAssets, movedFromAssetPaths);
            }
        }

		public void OnPostprocessAssetbundleNameChanged(string assetPath, string previousAssetBundleName, string newAssetBundleName)
		{
			UMAAssetIndexer.Instance.OnPostprocessAssetbundleNameChanged(assetPath, previousAssetBundleName, newAssetBundleName);
		}

	}

}
#endif
