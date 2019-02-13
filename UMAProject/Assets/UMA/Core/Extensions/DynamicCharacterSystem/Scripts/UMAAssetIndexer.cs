using UnityEngine;
using System.IO;
using System.Collections.Generic;
using UMA.CharacterSystem;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif

namespace UMA
{
	[PreferBinarySerialization]
	public class UMAAssetIndexer : ScriptableObject, ISerializationCallbackReceiver
	{

		#region constants and static strings
		public static string SortOrder = "Name";
		public static string[] SortOrders = { "Name", "AssetName" };
		public static Dictionary<string, System.Type> TypeFromString = new Dictionary<string, System.Type>();
		public static Dictionary<string, AssetItem> GuidTypes = new Dictionary<string, AssetItem>();
		#endregion
		#region Fields
		public bool AutoUpdate;

		private Dictionary<System.Type, System.Type> TypeToLookup = new Dictionary<System.Type, System.Type>()
		{
		{ (typeof(SlotDataAsset)),(typeof(SlotDataAsset)) },
		{ (typeof(OverlayDataAsset)),(typeof(OverlayDataAsset)) },
		{ (typeof(RaceData)),(typeof(RaceData)) },
		{ (typeof(UMATextRecipe)),(typeof(UMATextRecipe)) },
		{ (typeof(UMAWardrobeRecipe)),(typeof(UMAWardrobeRecipe)) },
		{ (typeof(UMAWardrobeCollection)),(typeof(UMAWardrobeCollection)) },
		{ (typeof(RuntimeAnimatorController)),(typeof(RuntimeAnimatorController)) },
		{ (typeof(AnimatorOverrideController)),(typeof(RuntimeAnimatorController)) },
#if UNITY_EDITOR
        { (typeof(AnimatorController)),(typeof(RuntimeAnimatorController)) },
#endif
        {  typeof(TextAsset), typeof(TextAsset) },
		{ (typeof(DynamicUMADnaAsset)), (typeof(DynamicUMADnaAsset)) }
		};


		// The names of the fully qualified types.
		public List<string> IndexedTypeNames = new List<string>();
		// These list is used so Unity will serialize the data
		public List<AssetItem> SerializedItems = new List<AssetItem>();
		// This is really where we keep the data.
		private Dictionary<System.Type, Dictionary<string, AssetItem>> TypeLookup = new Dictionary<System.Type, Dictionary<string, AssetItem>>();

#if UNITY_EDITOR
		// These list is used so Unity will serialize the data
		public List<AssetItem> AssetBundleSerializedItems = new List<AssetItem>();
		//in the editor we also store assetBundle data too
		private Dictionary<System.Type, Dictionary<string, AssetItem>> AssetBundleTypeLookup = new Dictionary<System.Type, Dictionary<string, AssetItem>>();
#endif

		// This list tracks the types for use in iterating through the dictionaries
		private System.Type[] Types =
		{
		(typeof(SlotDataAsset)),
		(typeof(OverlayDataAsset)),
		(typeof(RaceData)),
		(typeof(UMATextRecipe)),
		(typeof(UMAWardrobeRecipe)),
		(typeof(UMAWardrobeCollection)),
		(typeof(RuntimeAnimatorController)),
		(typeof(AnimatorOverrideController)),
#if UNITY_EDITOR
        (typeof(AnimatorController)),
#endif
        (typeof(DynamicUMADnaAsset)),
		(typeof(TextAsset))
	};
		#endregion
		#region Static Fields
		static GameObject theIndex = null;
		static UMAAssetIndexer theIndexer = null;
		#endregion

		public static System.Diagnostics.Stopwatch StartTimer()
		{
#if TIMEINDEXER
            if(Debug.isDebugBuild)
                Debug.Log("Timer started at " + Time.realtimeSinceStartup + " Sec");
            System.Diagnostics.Stopwatch st = new System.Diagnostics.Stopwatch();
            st.Start();

            return st;
#else
			return null;
#endif
		}

		public static void StopTimer(System.Diagnostics.Stopwatch st, string Status)
		{
#if TIMEINDEXER
            st.Stop();
            if(Debug.isDebugBuild)
                Debug.Log(Status + " Completed " + st.ElapsedMilliseconds + "ms");
            return;
#endif
		}

		public static UMAAssetIndexer Instance
		{
			get
			{
				if (theIndex == null || theIndexer == null)
				{
					var st = StartTimer();
					theIndexer = Resources.Load("AssetIndexer") as UMAAssetIndexer;
					if (theIndexer == null)
					{
						/*
												if (Debug.isDebugBuild)
												{
													Debug.LogError("Unable to load the AssetIndexer. This item is used to index non-asset bundle resources and is required.");
												}
						*/
					}
					StopTimer(st, "Asset index load");
				}
				return theIndexer;
			}
		}

#if UNITY_EDITOR
		public void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
		{
			if (!AutoUpdate)
			{
				return;
			}
			bool changed = false;

			// Build a dictionary of the items by path.
			Dictionary<string, AssetItem> ItemsByPath = new Dictionary<string, AssetItem>();
			Dictionary<string, AssetItem> AssetBundleItemsByPath = new Dictionary<string, AssetItem>();
			UpdateSerializedList();
			foreach (AssetItem ai in SerializedItems)
			{
				if (ItemsByPath.ContainsKey(ai._Path))
				{
					if (Debug.isDebugBuild)
						Debug.Log("Duplicate path for item: " + ai._Path);
					continue;
				}
				ItemsByPath.Add(ai._Path, ai);
			}

			// see if they moved it in the editor.
			for (int i = 0; i < movedAssets.Length; i++)
			{
				string NewPath = movedAssets[i];
				string OldPath = movedFromAssetPaths[i];

				// Check to see if this is an indexed asset.
				if (ItemsByPath.ContainsKey(OldPath))
				{
					changed = true;
					// If they moved it into an Asset Bundle folder, then we need to "unindex" it.
					if (InAssetBundleFolder(NewPath))
					{
						// Null it out, so we don't add it to the index...
						ItemsByPath[OldPath] = null;
						continue;
					}
					// 
					ItemsByPath[OldPath]._Path = NewPath;
				}
			}

			// Rebuild the tables
			SerializedItems.Clear();
			foreach (AssetItem ai in ItemsByPath.Values)
			{
				// We null things out when we want to delete them. This prevents it from going back into 
				// the dictionary when rebuilt.
				if (ai == null)
					continue;
				SerializedItems.Add(ai);
			}
			//Now do the same for the assetBundleItems
			foreach (AssetItem ai in AssetBundleSerializedItems)
			{
				if (AssetBundleItemsByPath.ContainsKey(ai._Path))
				{
					if (Debug.isDebugBuild)
						Debug.Log("Duplicate path for item: " + ai._Path);
					continue;
				}
				AssetBundleItemsByPath.Add(ai._Path, ai);
			}

			// see if they moved it in the editor.
			for (int i = 0; i < movedAssets.Length; i++)
			{
				string NewPath = movedAssets[i];
				string OldPath = movedFromAssetPaths[i];

				// Check to see if this is an indexed asset.
				if (AssetBundleItemsByPath.ContainsKey(OldPath))
				{
					changed = true;
					// If they moved it OUT of an Asset Bundle folder, then we need to "unindex" it.
					if (!InAssetBundleFolder(NewPath))
					{
						// Null it out, so we don't add it to the index...
						AssetBundleItemsByPath[OldPath] = null;
						continue;
					}
					// 
					AssetBundleItemsByPath[OldPath]._Path = NewPath;
				}
			}

			// Rebuild the assetBundle tables
			AssetBundleSerializedItems.Clear();
			foreach (AssetItem ai in AssetBundleItemsByPath.Values)
			{
				// We null things out when we want to delete them. This prevents it from going back into 
				// the dictionary when rebuilt.
				if (ai == null)
					continue;
				AssetBundleSerializedItems.Add(ai);
			}

			//update the dictionaries
			UpdateSerializedDictionaryItems();
			if (changed)
			{
				ForceSave();
			}
		}

		public void OnPostprocessAssetbundleNameChanged(string assetPath, string previousAssetBundleName, string newAssetBundleName)
		{
			//If asset path refers to a file we can deal with it here
			if (System.IO.File.Exists(assetPath))
			{
				//the assetPath hasn't changed so we can check based on that
				//if previousAssetBundleName is not empty, we should have it in our dictionary, so remove it if newAssetBundleName is empty, or give the assetItem newAssetBundleName
				int indexToRemove = -1;
				if (!string.IsNullOrEmpty(previousAssetBundleName))
				{
					for (int i = 0; i < AssetBundleSerializedItems.Count; i++)
					{
						if (AssetBundleSerializedItems[i]._Path == assetPath)
						{
							if (!string.IsNullOrEmpty(newAssetBundleName))
								AssetBundleSerializedItems[i]._AssetBundleName = newAssetBundleName;
							else
								indexToRemove = i;
						}
					}
				}
				if (indexToRemove > -1)
				{
					var aitype = AssetBundleSerializedItems[indexToRemove]._Type;
					AssetBundleTypeLookup[aitype].Remove(AssetBundleSerializedItems[indexToRemove]._Name);
					AssetBundleSerializedItems.RemoveAt(indexToRemove);
				}
				//if the asset was never in an asset bundle but has been put into one we need to add it to our dictionaries
				if (string.IsNullOrEmpty(previousAssetBundleName) && !string.IsNullOrEmpty(newAssetBundleName))
				{
					//TODO we should only bother with types we are actually indexing
					var abObj = AssetDatabase.LoadMainAssetAtPath(assetPath);
					if (abObj != null)
					{
						var ai = new AssetItem(abObj.GetType(), abObj);
						ai._AssetBundleName = newAssetBundleName;
						AddAssetItem(ai, true, true);//skip the bundle check because we know its in one
						ai.ReleaseItem();//Dont serialize the actual object ref
						AssetBundleSerializedItems.Add(ai);
					}
				}
			}
			//Otherwise we have to refresh all assetBundles info which is alot slower
			else if (System.IO.Directory.Exists(assetPath))
			{
				RefreshAssetBundlesInfo();
			}
		}

		/// <summary>
		/// Force the Index to save and reload
		/// </summary>
		public void ForceSave()
		{
			var st = StartTimer();
			EditorUtility.SetDirty(this);
			//EditorUtility.SetDirty(this.gameObject);
			AssetDatabase.SaveAssets();
			StopTimer(st, "ForceSave");
		}
#endif


		#region Manage Types
		/// <summary>
		/// Returns a list of all types that we know about.
		/// </summary>
		/// <returns></returns>
		public System.Type[] GetTypes()
		{
			return Types;
		}

		public bool IsIndexedType(System.Type type)
		{

			foreach (System.Type check in TypeToLookup.Keys)
			{
				if (check == type)
					return true;
			}
			return false;
		}

		public bool IsAdditionalIndexedType(string QualifiedName)
		{
			foreach (string s in IndexedTypeNames)
			{
				if (s == QualifiedName)
					return true;
			}
			return false;
		}
		/// <summary>
		/// Add a type to the types tracked
		/// </summary>
		/// <param name="sType"></param>
		public void AddType(System.Type sType)
		{
			string QualifiedName = sType.AssemblyQualifiedName;
			if (IsAdditionalIndexedType(QualifiedName)) return;

			List<System.Type> newTypes = new List<System.Type>();
			newTypes.AddRange(Types);
			newTypes.Add(sType);
			Types = newTypes.ToArray();
			TypeToLookup.Add(sType, sType);
			IndexedTypeNames.Add(sType.AssemblyQualifiedName);
			BuildStringTypes();
		}

		public void RemoveType(System.Type sType)
		{
			string QualifiedName = sType.AssemblyQualifiedName;
			if (!IsAdditionalIndexedType(QualifiedName)) return;

			TypeToLookup.Remove(sType);

			List<System.Type> newTypes = new List<System.Type>();
			newTypes.AddRange(Types);
			newTypes.Remove(sType);
			Types = newTypes.ToArray();
			TypeLookup.Remove(sType);
			IndexedTypeNames.Remove(sType.AssemblyQualifiedName);
			BuildStringTypes();
		}
		#endregion

		#region Access the index
		/// <summary>
		/// Return the asset specified, if it exists.
		/// if it can't be found by name, then we do a scan of the assets to see if 
		/// we can find the name directly on the object, and return that. 
		/// We then rebuild the index to make sure it's up to date.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="Name"></param>
		/// <returns></returns>
		public AssetItem GetAssetItem<T>(string Name)
		{
			System.Type ot = typeof(T);
			System.Type theType = TypeToLookup[ot];
			Dictionary<string, AssetItem> TypeDic = GetAssetDictionary(theType);
			if (TypeDic.ContainsKey(Name))
			{
				return TypeDic[Name];
			}
			/*
            foreach (AssetItem ai in TypeDic.Values)
            {
                if (Name == ai.EvilName)
                {
                    RebuildIndex();
                    return ai;
                }
            }*/
			return null;
		}

#if UNITY_EDITOR
		/// <summary>
		/// Return the asset specified, if it exists.
		/// if it can't be found by name, then we do a scan of the assets to see if 
		/// we can find the name directly on the object, and return that. 
		/// We then rebuild the index to make sure it's up to date.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="Name"></param>
		/// <returns></returns>
		public T GetAssetBundleAssetItem<T>(ref Dictionary<string, List<string>> assetBundlesUsedDict, string Name) where T : UnityEngine.Object
		{
			//If we dont have any assetBundle references yet add them? This is editor only so I think so. 
			//People may not have inspected the GlobalIndex yet so this may not have been done. If there are no assetBundles this will be pretty instant
			//If there are assetBundles they will be suffering speed issues (in SimulationMode) anyways until this is done
			//
			//We cant stop the deserialization in playmode creating the typeDicts because it needs to when we deserialize generally
			//so we have to do this shiz
			if (AssetBundleTypeLookup.Count == 0 || (AssetBundleTypeLookup[typeof(SlotDataAsset)].Count == 0 && AssetBundleTypeLookup[typeof(OverlayDataAsset)].Count == 0 && AssetBundleTypeLookup[typeof(RaceData)].Count == 0))
			{
				RefreshAssetBundlesInfo();
			}
			System.Type ot = typeof(T);
			System.Type theType = TypeToLookup[ot];
			Dictionary<string, AssetItem> TypeDic = GetAssetBundleAssetDictionary(theType);
			if (TypeDic != null)
			{
				if (TypeDic.ContainsKey(Name))
				{
					if (!assetBundlesUsedDict.ContainsKey(TypeDic[Name]._AssetBundleName))
						assetBundlesUsedDict.Add(TypeDic[Name]._AssetBundleName, new List<string>());
					if (!assetBundlesUsedDict[TypeDic[Name]._AssetBundleName].Contains(Name))
						assetBundlesUsedDict[TypeDic[Name]._AssetBundleName].Add(Name);
					return (AssetDatabase.LoadAssetAtPath(TypeDic[Name]._Path, TypeDic[Name]._Type) as T);
				}
			}
			return null;
		}
#endif

		/// <summary>
		/// Gets the asset hash and name for the given object
		/// </summary>
		private void GetEvilAssetNameAndHash(System.Type type, Object o, ref string assetName, int assetHash)
		{
			if (o is SlotDataAsset)
			{
				SlotDataAsset sd = o as SlotDataAsset;
				assetName = sd.slotName;
				assetHash = sd.nameHash;
			}
			else if (o is OverlayDataAsset)
			{
				OverlayDataAsset od = o as OverlayDataAsset;
				assetName = od.overlayName;
				assetHash = od.nameHash;
			}
			else if (o is RaceData)
			{
				RaceData rd = o as RaceData;
				assetName = rd.raceName;
				assetHash = UMAUtils.StringToHash(assetName);
			}
			else
			{
				assetName = o.name;
				assetHash = UMAUtils.StringToHash(assetName);
			}
		}



		public List<T> GetAllAssets<T>(string[] foldersToSearch = null) where T : UnityEngine.Object
		{
			var st = StartTimer();

			var ret = new List<T>();
			System.Type ot = typeof(T);
			System.Type theType = TypeToLookup[ot];

			Dictionary<string, AssetItem> TypeDic = GetAssetDictionary(theType);

			foreach (KeyValuePair<string, AssetItem> kp in TypeDic)
			{
				if (AssetFolderCheck(kp.Value, foldersToSearch))
					ret.Add((kp.Value.Item as T));
			}
			StopTimer(st, "GetAllAssets type=" + typeof(T).Name);
			return ret;
		}

		public T GetAsset<T>(int nameHash, string[] foldersToSearch = null) where T : UnityEngine.Object
		{
			System.Diagnostics.Stopwatch st = new System.Diagnostics.Stopwatch();
			st.Start();
			System.Type ot = typeof(T);
			Dictionary<string, AssetItem> TypeDic = (Dictionary<string, AssetItem>)TypeLookup[ot];
			string assetName = "";
			int assetHash = -1;
			foreach (KeyValuePair<string, AssetItem> kp in TypeDic)
			{
				assetName = "";
				assetHash = -1;
				GetEvilAssetNameAndHash(typeof(T), kp.Value.Item, ref assetName, assetHash);
				if (assetHash == nameHash)
				{
					if (AssetFolderCheck(kp.Value, foldersToSearch))
					{
						st.Stop();
						if (st.ElapsedMilliseconds > 2)
						{
							if (Debug.isDebugBuild)
								Debug.Log("GetAsset 0 for type " + typeof(T).Name + " completed in " + st.ElapsedMilliseconds + "ms");
						}
						return (kp.Value.Item as T);
					}
					else
					{
						st.Stop();
						if (st.ElapsedMilliseconds > 2)
						{
							if (Debug.isDebugBuild)
								Debug.Log("GetAsset 1 for type " + typeof(T).Name + " completed in " + st.ElapsedMilliseconds + "ms");
						}
						return null;
					}
				}
			}
			st.Stop();
			if (st.ElapsedMilliseconds > 2)
			{
				if (Debug.isDebugBuild)
					Debug.Log("GetAsset 2 for type " + typeof(T).Name + " completed in " + st.ElapsedMilliseconds + "ms");
			}
			return null;
		}

		public T GetAsset<T>(string name, string[] foldersToSearch = null) where T : UnityEngine.Object
		{
			var thisAssetItem = GetAssetItem<T>(name);
			if (thisAssetItem != null)
			{
				if (AssetFolderCheck(thisAssetItem, foldersToSearch))
					return (thisAssetItem.Item as T);
				else
					return null;
			}
			else
			{
				return null;
			}
		}

		/// <summary>
		/// Checks if the given asset path resides in one of the given folder paths. Returns true if foldersToSearch is null or empty and no check is required
		/// </summary>
		private bool AssetFolderCheck(AssetItem itemToCheck, string[] foldersToSearch = null)
		{
			if (foldersToSearch == null)
				return true;
			if (foldersToSearch.Length == 0)
				return true;
			for (int i = 0; i < foldersToSearch.Length; i++)
			{
				if (itemToCheck._Path.IndexOf(foldersToSearch[i]) > -1)
					return true;
			}
			return false;
		}


#if UNITY_EDITOR
		/// <summary>
		/// Check to see if something is an an assetbundle. If so, don't add it
		/// </summary>
		/// <param name="path"></param>
		/// <returns>the name of the asset bundle or null if not in an assetBundle</returns>
		public bool InAssetBundle(string path)
		{
			// path = System.IO.Path.GetDirectoryName(path);
			string[] assetBundleNames = AssetDatabase.GetAllAssetBundleNames();
			List<string> pathsInBundle;
			for (int i = 0; i < assetBundleNames.Length; i++)
			{
				pathsInBundle = new List<string>(AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleNames[i]));
				if (pathsInBundle.Contains(path))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Use this internally to return the assetbundle name as well as whether its in one or not (so you can assign it to the ai.AssetBundleName field)
		/// </summary>
		private string ContainingAssetBundle(string path)
		{
			// path = System.IO.Path.GetDirectoryName(path);
			string[] assetBundleNames = AssetDatabase.GetAllAssetBundleNames();
			List<string> pathsInBundle;
			for (int i = 0; i < assetBundleNames.Length; i++)
			{
				pathsInBundle = new List<string>(AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleNames[i]));
				if (pathsInBundle.Contains(path))
				{
					return assetBundleNames[i];
				}
			}
			return null;
		}

		public bool InAssetBundleFolder(string path)
		{
			path = System.IO.Path.GetDirectoryName(path);
			string[] assetBundleNames = AssetDatabase.GetAllAssetBundleNames();
			List<string> pathsInBundle;
			for (int i = 0; i < assetBundleNames.Length; i++)
			{
				pathsInBundle = new List<string>(AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleNames[i]));
				foreach (string s in pathsInBundle)
				{
					if (System.IO.Path.GetDirectoryName(s) == path)
						return true;
				}
			}
			return false;
		}
		//This is not instant, maybe we only show the progressBar if the user actually has the UAI window visible?
		//not sure how to check if the window is visible so I'm just showing it if this gets called from the windows inspector
		public void RefreshAssetBundlesInfo(bool showProgressBar = false)
		{
			AssetBundleTypeLookup.Clear();
			string[] assetBundleNames = AssetDatabase.GetAllAssetBundleNames();
			List<string> pathsInBundle;
			for (int i = 0; i < assetBundleNames.Length; i++)
			{
				pathsInBundle = new List<string>(AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleNames[i]));
				for (int abi = 0; abi < pathsInBundle.Count; abi++)
				{
					if (showProgressBar)
						EditorUtility.DisplayProgressBar("Adding AssetBundle Refs to Global Library.", assetBundleNames[i], ((float)abi / (float)pathsInBundle.Count));
					//TODO we should only bother with types we are actually indexing
					var abObj = AssetDatabase.LoadMainAssetAtPath(pathsInBundle[abi]);
					if (abObj != null)
					{
						var ai = new AssetItem(abObj.GetType(), abObj);
						ai._AssetBundleName = assetBundleNames[i];
						AddAssetItem(ai, true, true);//skip the bundle check because we know its in one
						ai.ReleaseItem();
					}
				}
			}
			if (showProgressBar)
				EditorUtility.ClearProgressBar();
			ForceSave();
		}


#endif
		#endregion

		#region Add Remove Assets
		/// <summary>
		/// Adds an asset to the index. Does NOT save the asset! you must do that separately.
		/// </summary>
		/// <param name="type">System Type of the object to add.</param>
		/// <param name="name">Name for the object.</param>
		/// <param name="path">Path to the object.</param>
		/// <param name="o">The Object to add.</param>
		/// <param name="skipBundleCheck">Option to skip checking Asset Bundles.</param>
		public void AddAsset(System.Type type, string name, string path, Object o, bool skipBundleCheck = false)
		{
			if (o == null)
			{
				if (Debug.isDebugBuild)
					Debug.Log("Skipping null item");

				return;
			}
			if (type == null)
			{
				type = o.GetType();
			}

			AssetItem ai = new AssetItem(type, name, path, o);
			AddAssetItem(ai, skipBundleCheck);
			AddAssetItem(ai, skipBundleCheck, true);
		}

		/// <summary>
		/// Adds an asset to the index. If the name already exists, it is not added. (Should we do this, or replace it?)
		/// </summary>
		/// <param name="ai"></param>
		/// <param name="SkipBundleCheck"></param>
		private void AddAssetItem(AssetItem ai, bool SkipBundleCheck = false, bool addToBundlesIndex = false)
		{
			try
			{
				if (!addToBundlesIndex)
				{
					System.Type theType = TypeToLookup[ai._Type];
					Dictionary<string, AssetItem> TypeDic = GetAssetDictionary(theType);
					bool indexed = false;
					// Get out if we already have it.
					if (TypeDic.ContainsKey(ai._Name))
					{
						indexed = true;
					}

					if (ai._Name.ToLower().Contains((ai._Type.Name + "placeholder").ToLower()))
					{
						indexed = true;
					}

#if UNITY_EDITOR
					if (!SkipBundleCheck)
					{
						string Path = AssetDatabase.GetAssetPath(ai.Item.GetInstanceID());//calling ai.Item makes it cache the item- we dont want that
						ai._AssetBundleName = ContainingAssetBundle(Path);
						if (!string.IsNullOrEmpty(ai._AssetBundleName))
						{
							indexed = true;
						}
					}
#endif
					if (indexed)
						return;

					TypeDic.Add(ai._Name, ai);
					if (GuidTypes.ContainsKey(ai._Guid))
					{
						return;
					}
					GuidTypes.Add(ai._Guid, ai);
				}
				else
				{
#if UNITY_EDITOR
					if (!TypeToLookup.ContainsKey(ai._Type))
						return;
					System.Type theType = TypeToLookup[ai._Type];
					Dictionary<string, AssetItem> TypeDic = GetAssetDictionary(theType);
					Dictionary<string, AssetItem> AssetBundleTypeDic = GetAssetBundleAssetDictionary(theType);
					//If the assetBundleTypeDic is null its a type we are not indexing
					if (AssetBundleTypeDic == null)
						return;

					if (!SkipBundleCheck)
					{
						ai._AssetBundleName = ContainingAssetBundle(ai._Path);
						if (string.IsNullOrEmpty(ai._AssetBundleName))
							return;
						else
							ai.IsAssetBundle = true;
					}
					bool indexed = false;
					// Get out if we already have it.
					if (AssetBundleTypeDic.ContainsKey(ai._Name))
					{
						indexed = true;
					}
					//people might assign already indexed assets to an assetBundle- in this case it needs to be removed from the normal list
					//in the opposite scenario it should not get added to the normal list unless the user does rebuild from project
					if (TypeDic.ContainsKey(ai._Name))
					{
						RemoveAsset(ai._Type, ai._Name);
					}

					if (ai._Name.ToLower().Contains((ai._Type.Name + "placeholder").ToLower()))
					{
						indexed = true;
					}
					if (indexed)
						return;

					AssetBundleTypeDic.Add(ai._Name, ai);

					if (GuidTypes.ContainsKey(ai._Guid))
					{
						return;
					}
					GuidTypes.Add(ai._Guid, ai);
#endif
				}
			}
			catch (System.Exception ex)
			{
				UnityEngine.Debug.LogWarning("Exception in UMAAssetIndexer.AddAssetItem: " + ex);
			}
		}

#if UNITY_EDITOR

		public AssetItem FromGuid(string GUID)
		{
			if (GuidTypes.ContainsKey(GUID))
			{
				return GuidTypes[GUID];
			}
			return null;
		}
		/// <summary>
		/// This is the evil version of AddAsset. This version cares not for the good of the project, nor
		/// does it care about readability, expandibility, and indeed, hates goodness with every beat of it's 
		/// tiny evil shrivelled heart. 
		/// I started going down the good path - I created an interface to get the name info, added it to all the
		/// classes. Then we ran into RuntimeAnimatorController. I would have had to wrap it. And Visual Studio kept
		/// complaining about the interface, even though Unity thought it was OK.
		/// 
		/// So in the end, good was defeated. And would never raise it's sword in the pursuit of chivalry again.
		/// 
		/// And EvilAddAsset doesn't save either. You have to do that manually. 
		/// </summary>
		/// <param name="type"></param>
		/// <param name="o"></param>
		public void EvilAddAsset(System.Type type, Object o)
		{
			AssetItem ai = null;
			ai = new AssetItem(type, o);
			ai._Path = AssetDatabase.GetAssetPath(o.GetInstanceID());
			AddAssetItem(ai);
			AddAssetItem(ai, false, true);
		}

		/// <summary>
		/// Removes an asset from the index
		/// </summary>
		/// <param name="type"></param>
		/// <param name="Name"></param>
		public void RemoveAsset(System.Type type, string Name)
		{
			System.Type theType = TypeToLookup[type];
			Dictionary<string, AssetItem> TypeDic = GetAssetDictionary(theType);
			if (TypeDic.ContainsKey(Name))
			{
				AssetItem ai = TypeDic[Name];
				TypeDic.Remove(Name);
				GuidTypes.Remove(Name);
			}
			Dictionary<string, AssetItem> AssetBundleTypeDic = GetAssetBundleAssetDictionary(theType);
			if (AssetBundleTypeDic.ContainsKey(Name))
			{
				AssetItem ai = AssetBundleTypeDic[Name];
				AssetBundleTypeDic.Remove(Name);
				GuidTypes.Remove(Name);
			}
		}
#endif

		#endregion

		#region Maintenance

		/// <summary>
		/// Updates the dictionaries from this list.
		/// Used when restoring items after modification, or after deserialization.
		/// </summary>
		private void UpdateSerializedDictionaryItems()
		{
			GuidTypes = new Dictionary<string, AssetItem>();
			foreach (System.Type type in Types)
			{
				CreateLookupDictionary(type);
			}
			foreach (AssetItem ai in SerializedItems)
			{
				// We null things out when we want to delete them. This prevents it from going back into 
				// the dictionary when rebuilt.
				if (ai == null)
					continue;
				AddAssetItem(ai, true);
			}
#if UNITY_EDITOR
			foreach (AssetItem ai in AssetBundleSerializedItems)
			{
				// We null things out when we want to delete them. This prevents it from going back into 
				// the dictionary when rebuilt.
				if (ai == null)
					continue;
				AddAssetItem(ai, true, true);
			}
#endif
		}
		/// <summary>
		/// Creates a lookup dictionary for a list. Used when reloading after deserialization
		/// </summary>
		/// <param name="type"></param>
		private void CreateLookupDictionary(System.Type type)
		{
			Dictionary<string, AssetItem> dic = new Dictionary<string, AssetItem>();
			if (TypeLookup.ContainsKey(type))
			{
				TypeLookup[type] = dic;
			}
			else
			{
				TypeLookup.Add(type, dic);
			}
#if UNITY_EDITOR
			if (type == typeof(SlotDataAsset) || type == typeof(OverlayDataAsset) || type == typeof(RaceData))
			{
				Dictionary<string, AssetItem> abdic = new Dictionary<string, AssetItem>();
				if (!AssetBundleTypeLookup.ContainsKey(type))
				{
					/*AssetBundleTypeLookup[type] = abdic; //why arent we doing this?
				}
				else
				{*/
					AssetBundleTypeLookup.Add(type, abdic);
				}
			}
#endif
		}

		/// <summary>
		/// Updates the list so all items can be processed at once, or for 
		/// serialization.
		/// </summary>
		private void UpdateSerializedList()
		{
			SerializedItems.Clear();
#if UNITY_EDITOR
			AssetBundleSerializedItems.Clear();
#endif
			foreach (System.Type type in TypeToLookup.Keys)
			{
				if (type == TypeToLookup[type])
				{
					Dictionary<string, AssetItem> TypeDic = GetAssetDictionary(type);
					foreach (AssetItem ai in TypeDic.Values)
					{
						// Don't add asset bundle or resource items to index. They are loaded on demand.
						if (ai.IsAssetBundle == false && ai.IsResource == false)
						{
							SerializedItems.Add(ai);
						}
					}
#if UNITY_EDITOR
					//if we are indexing assetbundleitems into a seperate dict we need them here
					Dictionary<string, AssetItem> AssetBundleTypeDic = GetAssetBundleAssetDictionary(type);
					if (AssetBundleTypeDic != null)
					{
						int i = 0;
						foreach (AssetItem ai in AssetBundleTypeDic.Values)
						{
							// Don't add resource items to index. They are loaded on demand.
							if (ai.IsResource == false)
							{
								AssetBundleSerializedItems.Add(ai);
								i++;
							}
						}
					}
#endif
				}
			}

		}

		/// <summary>
		/// Builds a list of types and a string to look them up.
		/// </summary>
		private void BuildStringTypes()
		{
			TypeFromString.Clear();
			foreach (System.Type st in Types)
			{
				TypeFromString.Add(st.Name, st);
			}
		}

#if UNITY_EDITOR

		public void RepairAndCleanup()
		{
			// Rebuild the tables
			UpdateSerializedList();

			for (int i = 0; i < SerializedItems.Count; i++)
			{
				AssetItem ai = SerializedItems[i];
				if (!ai.IsAssetBundle)
				{
					// If we already have a reference to the item, let's verify that everything is correct on it.
					Object obj = ai.Item;
					if (obj != null)
					{
						ai._Name = ai.EvilName;
						ai._Path = AssetDatabase.GetAssetPath(obj.GetInstanceID());
						ai._Guid = AssetDatabase.AssetPathToGUID(ai._Path);
					}
					else
					{
						// Clear out the item reference so we will attempt to fix it if it's broken.
						ai._SerializedItem = null;
						// This will attempt to load the item, using the path, guid or name (in that order).
						// This is in case we didn't have a reference to the item, and it was moved
						ai.CachSerializedItem();
						// If an item can't be found and we didn't ahve a reference to it, then we need to delete it.
						if (ai._SerializedItem == null)
						{
							// Can't be found or loaded
							// null it out, so it doesn't get added back.
							SerializedItems[i] = null;
						}
					}
				}
			}

			UpdateSerializedDictionaryItems();
			ForceSave();
		}

		public void AddEverything(bool includeText)
		{
			Clear(false);

			foreach (string s in TypeFromString.Keys)
			{
				System.Type CurrentType = TypeFromString[s];
				if (!includeText)
				{
					if (CurrentType == typeof(TextAsset))
					{
						continue;
					}
				}
				if (s != "AnimatorController")
				{
					string[] guids = AssetDatabase.FindAssets("t:" + s);
					for (int i = 0; i < guids.Length; i++)
					{
						string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);

						string fileName = Path.GetFileName(assetPath);
						EditorUtility.DisplayProgressBar("Adding Items to Global Library.", fileName, ((float)i / (float)guids.Length));

						if (assetPath.ToLower().Contains(".shader"))
						{
							continue;
						}
						Object o = AssetDatabase.LoadAssetAtPath(assetPath, CurrentType);
						if (o != null)
						{
							AssetItem ai = new AssetItem(CurrentType, o);
							AddAssetItem(ai);
							AddAssetItem(ai, false, true);
						}
						else
						{
							if (assetPath == null)
							{
								if (Debug.isDebugBuild)
									Debug.LogWarning("Cannot instantiate item " + guids[i]);
							}
							else
							{
								if (Debug.isDebugBuild)
									Debug.LogWarning("Cannot instantiate item " + assetPath);
							}
						}
					}
					EditorUtility.ClearProgressBar();
				}
			}
			ForceSave();
		}

		/// <summary>
		/// Clears the index
		/// </summary>
		public void Clear(bool forceSave = true)
		{
			// Rebuild the tables
			GuidTypes.Clear();
			ClearReferences();
			SerializedItems.Clear();
			AssetBundleSerializedItems.Clear();
			UpdateSerializedDictionaryItems();
			if (forceSave)
				ForceSave();
		}

		/// <summary>
		/// Adds references to all items by accessing the item property.
		/// This forces Unity to load the item and return a reference to it.
		/// When building, Unity needs the references to the items because we 
		/// cannot demand load them without the AssetDatabase.
		/// </summary>
		public void AddReferences()
		{
			// Rebuild the tables
			UpdateSerializedList();
			foreach (AssetItem ai in SerializedItems)
			{
				if (!ai.IsAssetBundle)
					ai.CachSerializedItem();
			}
			UpdateSerializedDictionaryItems();
			ForceSave();
		}

		/// <summary>
		/// This releases items by dereferencing them so they can be 
		/// picked up by garbage collection.
		/// This also makes working with the index much faster.
		/// </summary>
		public void ClearReferences()
		{
			// Rebuild the tables
			UpdateSerializedList();
			foreach (AssetItem ai in SerializedItems)
			{
				ai.ReleaseItem();
			}
#if UNITY_EDITOR
			foreach (AssetItem ai in AssetBundleSerializedItems)
			{
				ai.ReleaseItem();
			}
#endif
			UpdateSerializedDictionaryItems();
			ForceSave();

		}

#endif
		/// <summary>
		/// returns the entire lookup dictionary for a specific type.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public Dictionary<string, AssetItem> GetAssetDictionary(System.Type type)
		{
			System.Type LookupType = TypeToLookup[type];
			if (TypeLookup.ContainsKey(LookupType) == false)
			{
				TypeLookup[LookupType] = new Dictionary<string, AssetItem>();
			}
			return TypeLookup[LookupType];
		}

#if UNITY_EDITOR
		/// <summary>
		/// returns the entire lookup dictionary for a specific type for any assetBundles
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public Dictionary<string, AssetItem> GetAssetBundleAssetDictionary(System.Type type)
		{
			//for assetBundles indexing we only need to worry about slotDataAssets/OverlayDataAssets/RaceData because those hvae shitty 'names' inside the asset
			if (type != typeof(SlotDataAsset) && type != typeof(OverlayDataAsset) && type != typeof(RaceData))
				return null;
			System.Type LookupType = TypeToLookup[type];
			if (AssetBundleTypeLookup.ContainsKey(LookupType) == false)
			{
				AssetBundleTypeLookup[LookupType] = new Dictionary<string, AssetItem>();
			}
			return AssetBundleTypeLookup[LookupType];
		}
#endif

		/// <summary>
		/// Rebuilds the name indexes by dumping everything back to the list, updating the name, and then rebuilding 
		/// the dictionaries.
		/// </summary>
		public void RebuildIndex()
		{
			UpdateSerializedList();
			foreach (AssetItem ai in SerializedItems)
			{
				ai._Name = ai.EvilName;
			}
#if UNITY_EDITOR
			foreach (AssetItem ai in AssetBundleSerializedItems)
			{
				ai._Name = ai.EvilName;
			}
#endif
			UpdateSerializedDictionaryItems();
		}

		#endregion

		#region Serialization
		void ISerializationCallbackReceiver.OnBeforeSerialize()
		{
			UpdateSerializedList();// this.SerializeAllObjects);
		}
		void ISerializationCallbackReceiver.OnAfterDeserialize()
		{
			var st = StartTimer();
			#region typestuff
			List<System.Type> newTypes = new List<System.Type>()
				{
				(typeof(SlotDataAsset)),
				(typeof(OverlayDataAsset)),
				(typeof(RaceData)),
				(typeof(UMATextRecipe)),
				(typeof(UMAWardrobeRecipe)),
				(typeof(UMAWardrobeCollection)),
				(typeof(RuntimeAnimatorController)),
				(typeof(AnimatorOverrideController)),
		#if UNITY_EDITOR
				(typeof(AnimatorController)),
		#endif
				(typeof(DynamicUMADnaAsset)),
				(typeof(TextAsset))
				};

			TypeToLookup = new Dictionary<System.Type, System.Type>()
				{
				{ (typeof(SlotDataAsset)),(typeof(SlotDataAsset)) },
				{ (typeof(OverlayDataAsset)),(typeof(OverlayDataAsset)) },
				{ (typeof(RaceData)),(typeof(RaceData)) },
				{ (typeof(UMATextRecipe)),(typeof(UMATextRecipe)) },
				{ (typeof(UMAWardrobeRecipe)),(typeof(UMAWardrobeRecipe)) },
				{ (typeof(UMAWardrobeCollection)),(typeof(UMAWardrobeCollection)) },
				{ (typeof(RuntimeAnimatorController)),(typeof(RuntimeAnimatorController)) },
				{ (typeof(AnimatorOverrideController)),(typeof(RuntimeAnimatorController)) },
				#if UNITY_EDITOR
				{ (typeof(AnimatorController)),(typeof(RuntimeAnimatorController)) },
		#endif
				{  typeof(TextAsset), typeof(TextAsset) },
				{ (typeof(DynamicUMADnaAsset)), (typeof(DynamicUMADnaAsset)) }
				};

			List<string> invalidTypeNames = new List<string>();
			// Add the additional Types.
			foreach (string s in IndexedTypeNames)
			{
				if (s == "")
					continue;
				System.Type sType = System.Type.GetType(s);
				if (sType == null)
				{
					invalidTypeNames.Add(s);
					if (Debug.isDebugBuild)
						Debug.LogWarning("Could not find type for " + s);
					continue;
				}
				newTypes.Add(sType);
				if (!TypeToLookup.ContainsKey(sType))
				{
					TypeToLookup.Add(sType, sType);
				}
			}

			Types = newTypes.ToArray();

			if (invalidTypeNames.Count > 0)
			{
				foreach (string ivs in invalidTypeNames)
				{
					IndexedTypeNames.Remove(ivs);
				}
			}
			BuildStringTypes();
			#endregion
			UpdateSerializedDictionaryItems();
			StopTimer(st, "Before Serialize");
		}
		#endregion
	}
}
