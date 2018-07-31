using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DesysiaLOD {
	[ExecuteInEditMode]
    [CanEditMultipleObjects]
	public class ProgressiveMeshRuntime: MonoBehaviour {
        // Drag a reference or assign it with code
        [Tooltip("The Progressive Mesh asset to use for LOD switching")]
        public ProgressiveMesh progressiveMesh = null;

        // Clamp lod to [minLod, maxLod)
        [Tooltip("Min lod to use, this should usually always be 0 unless the first lod is quite high in triangles to begin with")]
        public int minLod = 0;
        [Tooltip("Max lod that can be reached")]
        public int maxLod = ProgressiveMesh.max_lod_count-1;

        // Run in editor or not
        [Tooltip("Run LOD manager in editor?")]
        public bool runInEditor = false;

		[HideInInspector][SerializeField]
		private Mesh[] cloned_meshes = null;

		[HideInInspector][SerializeField]
		private Mesh[] shared_meshes = null;

		private int current_lod = -1;

		private bool working = false;

		private Component[] allBasicRenderers = null;

		private bool culled = false;

		private int token = -1;
        
        [Tooltip("Culls the gameobject after this ratio is reached or passed")]
        [Range(0f, 1f)]
        public float cullRatio = 0.05f;

        [Tooltip("The intensity in which automatic LOD switching will be. Each level fits its role," +
            "Low will take longer to transition (in terms of ratio) while High will transisition much quicker. Extreme has no filter and will transisiton as implemented originally")]
        public LODSwitchIntensity intensity = LODSwitchIntensity.Medium;

        [Tooltip("CEnable or disable manual mode. This mode will let you set the ratio at which LODs are switched")]
        public bool manualMode = false;

        public List<LODLevel> lodLevels = new List<LODLevel>();

        //EXTRAS
        //[Header(" ")]
        [TextArea]
        [SerializeField]
        [Tooltip("Notes that you can make about why things are set up the way they are")]
        public string notes;

        //[Header(" ")]
        [Header("DEBUG")]
        [SerializeField]
        [Tooltip("Current Lod that is being displayed")]
        [Range(0, 19)]
        int currentLod = 0;
        [SerializeField]
        [Tooltip("Current Ratio the mesh is of the screens current size")]
        [Range(0, 1f)]
        float currentRatio = 0f;
        [SerializeField]
        [Tooltip("The amount of LODs the current mesh has available to it, these are useful" +
            "for choosing between in manual mode. REMEMBER: LODs are based on index of 0, so" +
            "to get the LOD wanted, you MUST subtract 1. IE, the first LOD is 0, not 1, if 20 are" +
            "available, 19 is the last")]
        int lodsAvailable = 0;
        [SerializeField]
        [Tooltip("This forces updating to stop, and allows you to input a LOD into 'Current Lod' field. Right click 'Progressive mesh Runtime' and Select 'Set Current LOD Override'." +
            "Untick this box to start normal Behavior")]
        bool lodSetOverride = false;

#if UNITY_EDITOR
        [MenuItem("Desysia/LOD Editor/Component/Runtime/Progressive Mesh Runtime")]
		public static void AddComponent() {
			GameObject SelectedObject = Selection.activeGameObject;
			if (SelectedObject) {
				// Register root object for undo.
				Undo.RegisterCreatedObjectUndo(SelectedObject.AddComponent(typeof(ProgressiveMeshRuntime)), "Add Progressive Mesh Runtime");
			}
		}
		[MenuItem ("Desysia/LOD Editor/Component/Runtime/Progressive Mesh Runtime", true)]
		static bool ValidateAddComponent () {
			// Return false if no gameobject is selected.
			return Selection.activeGameObject != null;
		}
		#endif
		void Awake() {
			get_all_meshes();
			// add to callback list
			token = ProgressiveMeshSchedule.register_me(this);
		}
		private void show_me() {
			foreach (Component child in allBasicRenderers) {
				((Renderer)child).enabled = true;
			}
			culled = false;
		}
		// Use this for initialization
		void Start() {
		}
		private float ratio_of_screen() {
			Camera cam = null;
			// the cameras are different
			if (Application.isPlaying) {
				cam = Camera.main;
			} else {
				if (UnityEditor.SceneView.currentDrawingSceneView != null) {
					cam = UnityEditor.SceneView.currentDrawingSceneView.camera;
				} else if (UnityEditor.SceneView.lastActiveSceneView != null) {
					cam = UnityEditor.SceneView.lastActiveSceneView.camera;
				} else {
					foreach (UnityEditor.SceneView scene_view in UnityEditor.SceneView.sceneViews) {
						if(scene_view != null) {
							cam = scene_view.camera;
							break;
						}
					}
				}
			}
			// sometimes the scene view is not ready, return highest quality instead.
			if (cam == null) return 1.0f;

			Vector3 min = new Vector3(float.MaxValue,float.MaxValue,float.MaxValue);
			Vector3 max = new Vector3(float.MinValue,float.MinValue,float.MinValue);
			foreach (Component child in allBasicRenderers) {
				Renderer rend = (Renderer)child;
				Vector3 center = rend.bounds.center;
				float radius = rend.bounds.extents.magnitude;
				Vector3[] six_points = new Vector3[6];
				six_points[0] = cam.WorldToScreenPoint(new Vector3(center.x-radius, center.y, center.z));
				six_points[1] = cam.WorldToScreenPoint(new Vector3(center.x+radius, center.y, center.z));
				six_points[2] = cam.WorldToScreenPoint(new Vector3(center.x, center.y-radius, center.z));
				six_points[3] = cam.WorldToScreenPoint(new Vector3(center.x, center.y+radius, center.z));
				six_points[4] = cam.WorldToScreenPoint(new Vector3(center.x, center.y, center.z-radius));
				six_points[5] = cam.WorldToScreenPoint(new Vector3(center.x, center.y, center.z+radius));
				foreach (Vector3 v in six_points) {
					if (v.x < min.x) min.x = v.x;
					if (v.y < min.y) min.y = v.y;
					if (v.x > max.x) max.x = v.x;
					if (v.y > max.y) max.y = v.y;
				}
			}
			float ratio_width = (max.x-min.x)/cam.pixelWidth;
			float ratio_height = (max.y-min.y)/cam.pixelHeight;
			float ratio = (ratio_width > ratio_height) ? ratio_width : ratio_height;
			if (ratio > 1.0f) ratio = 1.0f;
			
			return ratio;
		}
		// called from progressive mesh schedule script
		public void Update_Me() {
            if (lodSetOverride)
            {
                LodSetOverride();
                return;
            };
			if (progressiveMesh) {
				// when run in edit mode
				if (!Application.isPlaying) {
					// user's command
					if (runInEditor && !working) {
						get_all_meshes();
						Start();
					}
					// user's command
					if (!runInEditor) {
						if (working) {
							show_me();
							clean_all();
						}
						return;
					}
				}
				// detect if the game object is visible
				bool visible = false;
				if (!culled) {
					foreach (Component child in allBasicRenderers) {
						if(((Renderer)child).isVisible) visible = true;
						break;
					}
				}
				// we only change levels when the game object had been culled by ourselves or is visible
				if (culled || visible) {
					// we only calculate ratio of screen when the main camera is active in hierarchy
					float ratio = (Camera.main != null && Camera.main.gameObject != null && Camera.main.gameObject.activeInHierarchy) ? ratio_of_screen() : 0.0f;
                    // you may change cull condition here
                    //ADDED
                    currentRatio = ratio;
					if (ratio <= cullRatio) {
						// cull the game object
						if (!culled) {
							// cull me
							foreach (Component child in allBasicRenderers) {
								((Renderer)child).enabled = false;
							}
							culled = true;
						}
					} else {
						// show the game object
						if (culled) {
							// show me
							foreach (Component child in allBasicRenderers) {
								((Renderer)child).enabled = true;
							}
							culled = false;
						}
						int max_lod_count = progressiveMesh.triangles[0];

                        int lod = 0;
                        //use manual mode
                        if (manualMode)
                        {
                            //Set lods by manual mode
                            lod = ManualModeLOD(ratio);
                        }
                        else
                        {
                            // set triangle list according to current lod
                            lod = (int)((1.0f - ratio) * AutomaticModeLOD(max_lod_count));
                        }
                        //Set lods available
                        lodsAvailable = max_lod_count;

                        // clamp the value to valid range
                        if (lod > max_lod_count-1) lod = max_lod_count-1;
						// adjust the value by min lod
						if (lod < minLod) lod = minLod;
						// adjust the value by max lod
						if (lod > maxLod) lod = maxLod;
						// lod changed
						if (current_lod != lod) {
							set_triangles(lod);
							current_lod = lod;
                            //ADDED
                            currentLod = lod;
                        }
					}
				}
			}
		}
		private int[] get_triangles_from_progressive_mesh(int lod0, int mesh_count0, int mat0) {
			int triangle_count = 0;
			// max lod count
			int max_lod_count = progressiveMesh.triangles[triangle_count];
			triangle_count++;
			for (int lod=0; lod<max_lod_count; lod++) {
				// max mesh count
				int max_mesh_count = progressiveMesh.triangles[triangle_count];
				triangle_count++;
				for (int mesh_count=0; mesh_count<max_mesh_count; mesh_count++) {
					// max sub mesh count
					int max_sub_mesh_count = progressiveMesh.triangles[triangle_count];
					triangle_count++;
					for (int mat=0; mat<max_sub_mesh_count; mat++) {
						// max triangle count
						int max_triangle_count = progressiveMesh.triangles[triangle_count];
						triangle_count++;
						// here it is
						if(lod == lod0 && mesh_count == mesh_count0 && mat == mat0) {
							int[] new_triangles = new int[max_triangle_count];
							Array.Copy(progressiveMesh.triangles, triangle_count, new_triangles, 0, max_triangle_count);
							return new_triangles;
						}
						// triangle list count
						triangle_count += max_triangle_count;
					}
				}
			}
			return null;
		}
		private void set_triangles(int lod) {
			if(cloned_meshes != null) {
				int mesh_count = 0;
				int total_triangles_count = 0;
				foreach (Mesh child in cloned_meshes) {
					for(int mat=0; mat<child.subMeshCount; mat++) {
						int [] triangle_list = get_triangles_from_progressive_mesh(lod, mesh_count, mat);
                        //----------------------------------------------------------------------------------------------------------------------------
                        //HOT EDIT DONE HERE BY ME
                        if (triangle_list == null) continue;
                        //----------------------------------------------------------------------------------------------------------------------------
                        child.SetTriangles(triangle_list, mat);
						total_triangles_count += triangle_list.Length;
					}
					// time consuming functions, we just comment them here, Unity engine seems automatically update the normals.
					// child.RecalculateNormals();
					// child.RecalculateBounds();
					mesh_count++;
				}
			}
		}
		private void get_all_meshes() {
			if (!working) {
				Component[] allFilters = (Component[])(gameObject.GetComponentsInChildren (typeof(MeshFilter)));
				Component[] allRenderers = (Component[])(gameObject.GetComponentsInChildren (typeof(SkinnedMeshRenderer)));
				int mesh_count = allFilters.Length + allRenderers.Length;
				if (mesh_count > 0) {
					// first time
					if (cloned_meshes == null || cloned_meshes.Length == 0) {
						cloned_meshes = new Mesh[mesh_count];
						shared_meshes = new Mesh[mesh_count];
						int counter = 0;
						foreach (Component child in allFilters) {
							// store original shared mesh
							shared_meshes[counter] = ((MeshFilter)child).sharedMesh;
							// clone the shared mesh
							((MeshFilter)child).sharedMesh = Instantiate(((MeshFilter)child).sharedMesh);
							// store cloned mesh
							cloned_meshes[counter] = ((MeshFilter)child).sharedMesh;
							counter++;
						}
						foreach (Component child in allRenderers) {
							// store original shared mesh
							shared_meshes[counter] = ((SkinnedMeshRenderer)child).sharedMesh;
							// clone the shared mesh
							((SkinnedMeshRenderer)child).sharedMesh = Instantiate(((SkinnedMeshRenderer)child).sharedMesh);
							// store cloned mesh
							cloned_meshes[counter] = ((SkinnedMeshRenderer)child).sharedMesh;
							counter++;
						}
					} else {
						int counter = 0;
						foreach (Component child in allFilters) {
							// clone the shared mesh
							cloned_meshes[counter] = Instantiate(shared_meshes[counter]);
							// restore original shared mesh
							((MeshFilter)child).sharedMesh = cloned_meshes[counter];
							counter++;
						}
						foreach (Component child in allRenderers) {
							// clone the shared mesh
							cloned_meshes[counter] = Instantiate(shared_meshes[counter]);
							// restore original shared mesh
							((SkinnedMeshRenderer)child).sharedMesh = cloned_meshes[counter];
							counter++;
						}
					}
				}
				
				// get all renderers
				allBasicRenderers = (Component[])(gameObject.GetComponentsInChildren (typeof(Renderer)));
				// show me
				show_me();
				// current lod
				current_lod = -1;
				
				working = true;
			}
		}
		void clean_all() {
			if (working) {
				Component[] allFilters = (Component[])(gameObject.GetComponentsInChildren (typeof(MeshFilter)));
				Component[] allRenderers = (Component[])(gameObject.GetComponentsInChildren (typeof(SkinnedMeshRenderer)));
				int mesh_count = allFilters.Length + allRenderers.Length;
				if (mesh_count > 0) {
					int counter = 0;
					foreach (Component child in allFilters) {
						// restore original shared mesh
						((MeshFilter)child).sharedMesh = shared_meshes[counter];
						counter++;
					}
					foreach (Component child in allRenderers) {
						// restore original shared mesh
						((SkinnedMeshRenderer)child).sharedMesh = shared_meshes[counter];
						counter++;
					}
				}

				// clean basic renderers
				for (int i=0; i<allBasicRenderers.Length; i++) {
					allBasicRenderers[i] = null;
				}
				// release the array
				allBasicRenderers = null;

				working = false;
			}
		}
		void OnEnable() {
			get_all_meshes();
			Start();
		}
		void OnDisable() {
			show_me();
			clean_all();
		}
		void OnDestroy() {
			// remove from callback list
			ProgressiveMeshSchedule.unregister_me(token, this);
			clean_all ();
		}

        int ManualModeLOD(float ratio)
        {
            int selectedLod = minLod;
            //Iterate of all lods
            for(int i = 0; i < lodLevels.Count; i++)
            {
                //If the ratio is less than already currently selected lod, update
                if(ratio <= lodLevels[i].ratioNeeded)
                {
                    //Set the new selected LOD
                    selectedLod =  lodLevels[i].lod;
                    //We do NOT break here as if we did, it would only select whatever LOD met
                    //this requirement first
                }
            }

            return selectedLod;
        }

        int AutomaticModeLOD(int lodCount)
        {
            int convertedMaxLodCount = lodCount;
            switch (intensity)
            {
                case LODSwitchIntensity.Extreme:
                    convertedMaxLodCount = lodCount / 1;
                    break;
                case LODSwitchIntensity.High:
                    convertedMaxLodCount = Mathf.RoundToInt(lodCount / 1.5f);
                    break;
                case LODSwitchIntensity.Medium:
                    convertedMaxLodCount = lodCount / 2;
                    break;
                case LODSwitchIntensity.Low:
                    convertedMaxLodCount = lodCount / 3;
                    break;
                case LODSwitchIntensity.Lowest:
                    convertedMaxLodCount = lodCount / 6;
                    break;
                default:
                    convertedMaxLodCount = lodCount;
                    break;
            }
            return convertedMaxLodCount;
        }

        [ContextMenu("Set Current LOD Override")]
        void LodSetOverride()
        {
            int lodsAvailable = progressiveMesh.triangles[0];
            if (currentLod > lodsAvailable - 1 || currentLod < 0)
            {
                Debug.LogError(string.Format("Current LOD: {0} is out of range, please select a LOD between {1} and {2}", currentLod, 0, lodsAvailable - 1));
            }
            set_triangles(currentLod);
            current_lod = currentLod;
        }
	}
}

public enum LODSwitchIntensity
{
    Lowest,
    Low,
    Medium,
    High,
    Extreme
}

[System.Serializable]
public class LODLevel
{
    //This is just used as a hack to name this class in the editor menu panel
    [HideInInspector]
    public string alias = "LOD Level";
    [Tooltip("The LOD to use for this LOD level")]
    public int lod;
    [Tooltip("The ratio that MUST be met to use this lod. LOD chosen will always be whatever " +
        "the last lod was that was closest to the ratio. In math terms less than or equal to is used")]
    public float ratioNeeded;
}
