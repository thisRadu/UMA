using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace UMA
{
    public abstract class UMAGeneratorBuiltin : UMAGeneratorBase
    {
        public UMAData umaData;
        [NonSerialized]
        public List<UMAData>
            umaDirtyList = new List<UMAData>();
        public int meshUpdates;
        public int maxMeshUpdates;
        public UMAGeneratorCoroutine umaGeneratorCoroutine;
        public UMAGeneratorCoroutine activeGeneratorCoroutine;
        public Transform textureMergePrefab;
        public Matrix4x4 tempMatrix;
        public UMAMeshCombiner meshCombiner;
        public float unityVersion;

        public void Initialize()
        {
            umaGeneratorCoroutine = new UMAGeneratorCoroutine();
        }

        public virtual void Awake()
        {
            
            maxMeshUpdates = 1;
            if (atlasResolution == 0)
                atlasResolution = 256;
            umaGeneratorCoroutine = new UMAGeneratorCoroutine();
            
            if (!textureMerge)
            {
                Transform tempTextureMerger = Instantiate(textureMergePrefab, Vector3.zero, Quaternion.identity) as Transform;
                textureMerge = tempTextureMerger.GetComponent("TextureMerge") as TextureMerge;
                textureMerge.transform.parent = transform;
                textureMerge.gameObject.SetActive(false);
            }
            
            //Garbage Collection hack
            var mb = (System.GC.GetTotalMemory(false) / (1024 * 1024));
            if (mb < 10)
            {
                byte[] data = new byte[10 * 1024 * 1024];
                data [0] = 0;
                data [10 * 1024 * 1024 - 1] = 0;
            }
        }
        
        void Update()
        {
            if (umaDirtyList.Count > 0)
            {
                OnDirtyUpdate();    
            }
            meshUpdates = 0;    
        }

        public virtual bool HandleDirtyUpdate(UMAData data)
        {
            if (umaData != data)
            {
                umaData = data;
                if (!umaData.Validate())
                {
                    return true;
                }
            }
            
            if (umaData.isMeshDirty)
            {
				Profiler.BeginSample("Combine Mesh 1");
                if (!umaData.isTextureDirty)
                {
                    UpdateUMAMesh(false);
                }
                umaData.isMeshDirty = false;
				Profiler.EndSample();
			}
            if (umaData.isTextureDirty)
            {
				Profiler.BeginSample("Combine Texture");
				if (activeGeneratorCoroutine == null)
                {
                    activeGeneratorCoroutine = umaGeneratorCoroutine;
                    TextureProcessBaseCoroutine textureProcessCoroutine;
                    textureProcessCoroutine = new TextureProcessPROCoroutine();
                    textureProcessCoroutine.Prepare(data, this);
                    activeGeneratorCoroutine.Prepare(this, umaData, textureProcessCoroutine);
                }

				bool workDone = umaGeneratorCoroutine.Work();
				Profiler.EndSample();
				if (workDone)
                {
					Profiler.BeginSample("Combine Mesh 2");
					activeGeneratorCoroutine = null;
                    UpdateUMAMesh(true);
                    umaData.isTextureDirty = false;
					Profiler.EndSample();
				}
				else
                {
                    return false;
                }
            } else if (umaData.isShapeDirty)
            {
				Profiler.BeginSample("Apply DNA");
				UpdateUMABody(umaData);
                umaData.isShapeDirty = false;
				Profiler.EndSample();

				Profiler.BeginSample("UMA Ready");
				UMAReady();
				Profiler.EndSample();
				return true;

            } else
            {
				Profiler.BeginSample("UMA Ready");
				UMAReady();
				Profiler.EndSample();
				return true;
            }
            return false;
        }
        
        public virtual void OnDirtyUpdate()
        {
            if (HandleDirtyUpdate(umaDirtyList [0]))
            {
                umaDirtyList.RemoveAt(0);
                umaData = null;
            }           
        }

        private void UpdateUMAMesh(bool updatedAtlas)
        {
            if (meshCombiner != null)
            {
                meshCombiner.UpdateUMAMesh(updatedAtlas, umaData, textureNameList, atlasResolution);
            } else
            {
                Debug.LogError("UMAGenerator.UpdateUMAMesh, no MeshCombiner specified", gameObject);
            }
        }

        public override void addDirtyUMA(UMAData umaToAdd)
        {   
            if (umaToAdd)
            {
                umaDirtyList.Add(umaToAdd);
            }
        }

        public override bool IsIdle()
        {
            return umaDirtyList.Count == 0;
        }
        
        public virtual void UMAReady()
        {   
            if (umaData)
            {
                umaData.myRenderer.enabled = true;
                umaData.FireUpdatedEvent(false); 
            }
        }
    
        public virtual void UpdateUMABody(UMAData umaData)
        {
            if (umaData)
            {
                umaData.GotoOriginalPose();
                umaData.skeleton = new UMASkeletonDefault(umaData.myRenderer.rootBone);
                umaData.ApplyDNA();
                umaData.FireDNAAppliedEvents();
                UpdateAvatar(umaData);
            }
        }
    }
}