using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;

namespace DesysiaLOD {
	public class DesysiaLODEditorBatch: EditorWindow {
		// run when focused
		[DllImport("DesysiaLOD")]
		private static extern int create_progressive_mesh(Vector3[] vertex_array, int vertex_count, int[] triangle_array, int triangle_count, Vector3[] normal_array, int normal_count, Color[] color_array, int color_count, Vector2[] uv_array, int uv_count, int protect_boundary, int protect_detail, int protect_symmetry);
		[DllImport("DesysiaLOD")]
		private static extern int get_triangle_list(int index, float goal, int[] triangles, ref int triangle_count);
		[DllImport("DesysiaLOD")]
		private static extern int delete_progressive_mesh(int index);

		private Desysia_Mesh[] Desysia_Meshes = null;

		// define how to optimize the meshes
		private bool protect_boundary = true;
		private bool protect_detail = false;
		private bool protect_symmetry = false;

		private int state = 0;
		private float start_time = 0.0f;
		private List<string> file_name_list = null;
		private int file_name_index = 0;
		private string file_name_hint = null;
		private string message_hint = null;

        private ProgressiveMesh[] progMeshes;

        private string[] meshesNames;

        private List<FileInfo> meshFbxFiles;

        private static string defaultResourcesPath = "Assets/Resources";

        //FAST DEBUGGING PATHS
        //private static string meshGet = "M:/Unity Projects/Projects Tier 2/Rotten NewProjectVersion/Assets/__TESTING__/Resources/scaled_models";
        //private static string meshPut = "M:/Unity Projects/Projects Tier 2/Rotten NewProjectVersion/Assets/__TESTING__/Resources/Batch_Meshes";
        //private static string prefabPut = "M:/Unity Projects/Projects Tier 2/Rotten NewProjectVersion/Assets/__TESTING__/Resources/Batch_Prefabs";

        private static string meshGet = defaultResourcesPath;
        private static string meshPut = defaultResourcesPath;
        private static string prefabPut = defaultResourcesPath;

        private static bool toggleRuntimeSettings;

        private static string addedToFilename = "";

        #region Batch Settings for Runtime

        private static bool useRuntimeSettings = false;

        private static bool runInEditor = false;

        private static int minLod = 0;

        private static int maxLod = 19;

        private static float cullRatio = 0.05f;

        private static LODSwitchIntensity lodIntensity = LODSwitchIntensity.Medium;

        private static bool manualMode = false;

        public static LODLevel[] lodLevels = new LODLevel[3];

        private static string notes = "Enter notes here...";

        private static Vector2 scroll;

        #endregion

        [MenuItem("Desysia/LOD Editor/Assets/Create/LOD Editor Batch")]
		static void Init () {
			// Get existing open window or if none, make a new one:
			DesysiaLODEditorBatch window = (DesysiaLODEditorBatch)EditorWindow.GetWindow (typeof (DesysiaLODEditorBatch));
			window.Show();
		}
		
		void OnGUI ()
        {
            GUIStyle helpStyle = new GUIStyle(GUI.skin.box);

			// display the title
			GUI.enabled = true;
			GUILayout.Label(
				"Desysia LOD Editor Batch Edition V3.0"
				, helpStyle
				, GUILayout.ExpandWidth(true));

			// display controls
			GUI.enabled = (state == 0);
			protect_boundary = EditorGUILayout.Toggle ("Protect Boundary", protect_boundary);
			protect_detail = EditorGUILayout.Toggle ("More Details", protect_detail);
			protect_symmetry = EditorGUILayout.Toggle ("Protect Symmetry", protect_symmetry);

            //GUILayout.TextField(GetActiveObjectPath());

            //Display where to get meshes
            if (GUILayout.Button("Meshes to Process"))
            {
                meshGet = EditorUtility.OpenFolderPanel("Select Meshes", "Assets", "");
            }

            GUILayout.TextField(string.IsNullOrEmpty(meshGet) ? "--EMPTY--" : meshGet);
            //Display where to put meshes
            if (GUILayout.Button("Processed Meshes Destination"))
            {
                meshPut = EditorUtility.OpenFolderPanel("Meshes Destination", "Assets", "");
            }
            GUILayout.TextField(string.IsNullOrEmpty(meshPut) ? "--EMPTY--" : meshPut);

            GUILayout.Space(5);

            // generate progressive meshes
            if (GUILayout.Button("Batch Generate Progressive Meshes", GUILayout.ExpandWidth(true), GUILayout.ExpandWidth(true))) {
				if (GetFileList(meshGet)) {
					message_hint = null;
					// start the batch
					start_update(1);
				}
			}

			// we are still alive
			if(state != 0) {
				EditorGUILayout.LabelField(file_name_hint);
				EditorGUILayout.LabelField("Time Elapsed: ", (Time.realtimeSinceStartup - start_time).ToString("#0.0"));
			}

            GUILayout.Space(20);
            // display the title
            GUI.enabled = true;
            GUILayout.Label(
                "Create Prefabs"
                , helpStyle
                , GUILayout.ExpandWidth(true));

            // display controls
            GUI.enabled = (state == 0);

            //Display where to put meshes
            if (GUILayout.Button("Processed Prefabs Destination"))
            {
                prefabPut = EditorUtility.OpenFolderPanel("Prefabs Destination", "Assets", "");
            }
            GUILayout.TextField(string.IsNullOrEmpty(meshPut) ? "--EMPTY--" : prefabPut);

            addedToFilename = EditorGUILayout.TextField("Add to Filename:", addedToFilename);

            // generate progressive meshes
            if (GUILayout.Button("Batch Create/Fill Prefabs", GUILayout.ExpandWidth(true), GUILayout.ExpandWidth(true)))
            {
                GetProgressiveMeshesList(meshGet);

                if (GetProgressiveMeshes(meshPut))
                {
                    for (int i = 0; i < progMeshes.Length; i++)
                    {
                        //CreatePrefab(meshGet, prefabPut, progMeshes[i], i);
                        CreateReplacePrefab(meshGet, prefabPut, progMeshes[i], i);
                    }
                }
            }

            useRuntimeSettings = GUILayout.Toggle(useRuntimeSettings, "Use Runtime Settings Below?");

            toggleRuntimeSettings = EditorGUILayout.Foldout(toggleRuntimeSettings, "Runtime Settings");
            if (toggleRuntimeSettings)
            {
                runInEditor = GUILayout.Toggle(runInEditor, "Run in Editor?");

                minLod = EditorGUILayout.IntSlider("Min LOD: ", minLod, 0, 19);

                maxLod = EditorGUILayout.IntSlider("Max LOD: ", maxLod, 0, 19);

                cullRatio = EditorGUILayout.Slider("Cull Ratio: ", cullRatio, 0, 1);

                lodIntensity = (LODSwitchIntensity)EditorGUILayout.EnumPopup("LOD Intensity: ", lodIntensity);

                manualMode = GUILayout.Toggle(manualMode, "Manual Mode?");

                //THIS IS BROKEN AND DOES NOT WORK, POSSIBLY LATER LOOK INTO SUPPORT FOR LISTS/ARRAYS
                //PERSISTENCE WASNT MADE FOR EDITOR WINDOW
                //ScriptableObject target = this;
                //SerializedObject so = new SerializedObject(target);
                //SerializedProperty stringsProperty = so.FindProperty("lodLevels");

                //EditorGUILayout.PropertyField(stringsProperty, true); // True means show children
                //so.ApplyModifiedProperties(); // Remember to apply modified properties

                scroll = EditorGUILayout.BeginScrollView(scroll);
                notes = EditorGUILayout.TextArea(notes, GUILayout.Height(position.height - 30));
                EditorGUILayout.EndScrollView();
            }

            //if(toggleRuntimeSettings = GUILayout.(toggleRuntimeSettings, "Runtime Settings"))
            //{
                
            //}
            //if (GUILayout.Button("__TEST__"))
            //{
            //    GetProgressiveMeshesList(meshGet);
            //}

            GUILayout.Space(20);

            // display something
            if (message_hint != null) {
				EditorUtility.DisplayDialog("Message", message_hint, "OK");
				message_hint = null;
/*				GUILayout.Label(
					message_hint
					, helpStyle
					, GUILayout.ExpandWidth(true));*/
			}
		}
		private void start_update(int one_state) {
			state = one_state;
			start_time = Time.realtimeSinceStartup;
			EditorApplication.update += Update;
		}
		private void end_update() {
			state = 0;
			start_time = 0.0f;
			EditorApplication.update -= Update;
		}
		void Update() {
			// we cannot use nice yield method in Editor class, I have to write ugly code:(
			switch(state) {
			case 1: ShowModelName(); break;
			case 2: GenerateProgressiveMesh(); break;
			case 3: ShowResult(); break;
			}
			// show time elapse label in main thread
			Repaint();
		}
		private void ShowModelName() {
			file_name_hint = file_name_list[file_name_index];
			state = 2;
		}
		private void ShowResult() {
			message_hint = "All Done. Total: " + file_name_index.ToString() + " progressive meshes generated into: " + meshPut + ".";
			end_update();
		}
		private void GenerateProgressiveMesh() {
			try {
				CreateAsset(Path.GetFileNameWithoutExtension(file_name_list[file_name_index]));
			} finally {
			}
			// forward to the next
			file_name_index++;
			// all done
			if (file_name_index == file_name_list.Count) {
				state = 3;
			} else {
				state = 1;
			}
		}
		private bool GetFileList(string meshLoc) {
			if (!Directory.Exists(meshLoc)) {
				message_hint = string.Format("Please create {0} directory, then put all your 3d models into the directory and try again.", meshLoc);
				return false;
			}
			// Reset file name list and its index
			file_name_list = new List<string>();
			file_name_index = 0;

			// Get file name list of supported 3d models
			List<string> temp_file_name_list = new List<string>(Directory.GetFiles(meshLoc));
			foreach (string filename in temp_file_name_list) {
				string extensionname = Path.GetExtension(filename).ToLower();
				// You may add extensions here
				if (string.Compare(extensionname, ".fbx") == 0 ||
				    string.Compare(extensionname, ".dae") == 0 ||
				    string.Compare(extensionname, ".3ds") == 0 ||
				    string.Compare(extensionname, ".dxf") == 0 ||
				    string.Compare(extensionname, ".obj") == 0 ||
				    string.Compare(extensionname, ".mb") == 0 ||
				    string.Compare(extensionname, ".ma") == 0 ||
				    string.Compare(extensionname, ".c4d") == 0 ||
				    string.Compare(extensionname, ".max") == 0 ||
				    string.Compare(extensionname, ".jas") == 0 ||
				    string.Compare(extensionname, ".lxo") == 0 ||
				    string.Compare(extensionname, ".lws") == 0 ||
				    string.Compare(extensionname, ".blend") == 0) { 
					// Add to the list
					file_name_list.Add(filename);
				}
			}
			// No 3d model in the directory
			if (file_name_list.Count == 0) {
				message_hint = string.Format("Please put all your 3d models into {0} and try again.\n\nSupported file extensions:\n.fbx\n.dae\n.3ds\n.dxf\n.obj\n.mb\n.ma\n.c4d\n.max\n.jas\n.lxo\n.lws\n.blend\n\nYou may modify the script(DesysiaLODEditorPro/Editor/ProgressiveMeshBatch.cs) to support future extensions.", meshLoc);
				return false;
			}
			return true;
		}
		public void CreateAsset(string filename) {
			ProgressiveMesh pm = (ProgressiveMesh)ScriptableObject.CreateInstance(typeof(ProgressiveMesh));

			init_all(filename);
			optimize();
			fill_progressive_mesh(pm);
			clean_all();

            string toBeSearched = "Assets/";

            string placeToSave = meshPut.Substring(meshPut.IndexOf(toBeSearched) + toBeSearched.Length);

            string combined = toBeSearched + placeToSave + "/";

            string filePath = combined + filename + "_Progressive_Mesh.asset";

			AssetDatabase.CreateAsset(pm, filePath);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}
		private void fill_progressive_mesh(ProgressiveMesh pm) {
			int triangle_count = 0;
			int[][][][] temp_triangles;
			temp_triangles = new int[ProgressiveMesh.max_lod_count][][][];
			// max lod count
			triangle_count++;
			for (int lod=0; lod<temp_triangles.Length; lod++) {
				float quality = 100.0f * (temp_triangles.Length - lod) / temp_triangles.Length;

                temp_triangles[lod] = new int[Desysia_Meshes.Length][][];

                // mesh count
                triangle_count++;
				int mesh_count = 0;
				foreach (Desysia_Mesh child in Desysia_Meshes) {
					// get triangle list by quality value
					if(child.index != -1 && get_triangle_list(child.index, quality, child.out_triangles, ref child.out_count) == 1) {
						if(child.out_count > 0) {
							int counter = 0;
							int mat = 0;
							temp_triangles[lod][mesh_count] = new int[child.mesh.subMeshCount][];
							// sub mesh count
							triangle_count++;
							while(counter < child.out_count) {
								int len = child.out_triangles[counter];
								// triangle count
								triangle_count++;
								// triangle list count
								triangle_count += len;
								counter++;
								int[] new_triangles = new int[len];
								Array.Copy(child.out_triangles, counter, new_triangles, 0, len);
								temp_triangles[lod][mesh_count][mat] = new_triangles;
								counter += len;
								mat++;
							}
						} else {
							temp_triangles[lod][mesh_count] = new int[child.mesh.subMeshCount][];
							// sub mesh count
							triangle_count++;
							for (int mat=0; mat<temp_triangles[lod][mesh_count].Length; mat++) {
								temp_triangles[lod][mesh_count][mat] = new int[0];
								// triangle count
								triangle_count++;
							}
						}
					}
					mesh_count++;
				}
			}
			// create fix size array
			pm.triangles = new int[triangle_count];
			
			// reset the counter
			triangle_count = 0;
			// max lod count
			pm.triangles[triangle_count] = temp_triangles.Length;
			triangle_count++;
			for (int lod=0; lod<temp_triangles.Length; lod++) {
				// mesh count
				pm.triangles[triangle_count] = temp_triangles[lod].Length;
				triangle_count++;
				for (int mesh_count=0; mesh_count<temp_triangles[lod].Length; mesh_count++) {
					// sub mesh count
					pm.triangles[triangle_count] = temp_triangles[lod][mesh_count].Length;
					triangle_count++;
					for (int mat=0; mat<temp_triangles[lod][mesh_count].Length; mat++) {
						// triangle count
						pm.triangles[triangle_count] = temp_triangles[lod][mesh_count][mat].Length;
						triangle_count++;
						Array.Copy(temp_triangles[lod][mesh_count][mat], 0, pm.triangles, triangle_count, temp_triangles[lod][mesh_count][mat].Length);
						// triangle list count
						triangle_count += temp_triangles[lod][mesh_count][mat].Length;
					}
				}
			}
		}
		private void optimize() {
			if (Desysia_Meshes != null) {
				foreach (Desysia_Mesh child in Desysia_Meshes) {
					int triangle_number = child.mesh.triangles.Length;
					Vector3[] vertices = child.mesh.vertices;
					// in data is large than origin data
					int[] triangles = new int[triangle_number+child.mesh.subMeshCount];
					// we need normal data to protect normal boundary
					Vector3[] normals = child.mesh.normals;
					// we need color data to protect color boundary
					Color[] colors = child.mesh.colors;
					// we need uv data to protect uv boundary
					Vector2[] uvs = child.mesh.uv;
					int counter = 0;
					for(int i=0; i<child.mesh.subMeshCount; i++) {
						int[] sub_triangles = child.mesh.GetTriangles(i);
						triangles[counter] = sub_triangles.Length;
						counter++;
						Array.Copy(sub_triangles, 0, triangles, counter, sub_triangles.Length);
						counter += sub_triangles.Length;
					}
					// create progressive mesh
					child.index = create_progressive_mesh(vertices, vertices.Length, triangles, counter, normals, normals.Length, colors, colors.Length, uvs, uvs.Length, protect_boundary?1:0, protect_detail?1:0, protect_symmetry?1:0);
				}
			}
		}
		private void init_all(string filename) {
			get_all_meshes(filename);
			if (Desysia_Meshes != null) {
				foreach (Desysia_Mesh child in Desysia_Meshes) {
					int triangle_number = child.mesh.triangles.Length;
					// out data is large than origin data
					child.out_triangles = new int[triangle_number+child.mesh.subMeshCount];
					child.index = -1;
				}
			}
		}
		private void get_all_meshes(string filename) {
            string combined = meshGet + "/" + filename;
            string toBeSearched = "Resources/";
            string placeToLoad = combined.Substring(combined.IndexOf(toBeSearched) + toBeSearched.Length);

            Mesh[] meshes = Resources.LoadAll<Mesh>(placeToLoad);

			int mesh_count = meshes.Length;
			if (mesh_count > 0) {
				Desysia_Meshes = new Desysia_Mesh[mesh_count];
				int counter = 0;
				foreach (Mesh mesh in meshes) { 
					Desysia_Meshes[counter] = new Desysia_Mesh();
					Desysia_Meshes[counter].mesh = mesh;
					counter++;
				}
			}
		}
		private void clean_all() {
			if (Desysia_Meshes != null) {
				foreach (Desysia_Mesh child in Desysia_Meshes) {
					if(child.index != -1) {
						Resources.UnloadAsset(child.mesh);
						// do not need it
						delete_progressive_mesh (child.index);
						child.index = -1;
					}
				}
				Desysia_Meshes = null;
			}
		}

        private void CreatePrefab(string meshPath, string preFabPath, ProgressiveMesh progMesh, int iteration)
        {
            string newPathMesh = meshPath.Replace(Application.dataPath, "Assets");
            string newPathPrefab = preFabPath.Replace(Application.dataPath, "Assets");

            string finalMeshPath = newPathMesh +  "/" + meshFbxFiles[iteration].Name;
            string finalPrefabPath = newPathPrefab + "/" + progMeshes[iteration].name + ".prefab";

            GameObject objToPrefab = AssetDatabase.LoadAssetAtPath(finalMeshPath, typeof(GameObject)) as GameObject;

            var progMeshRun = objToPrefab.AddComponent<ProgressiveMeshRuntime>();

            if(progMeshRun != null)
            {
                progMeshRun.progressiveMesh = progMesh;

                if (useRuntimeSettings)
                {
                    progMeshRun.cullRatio = cullRatio;

                    progMeshRun.intensity = lodIntensity;

                    //progMeshRun.lodLevels = lodLevels;

                    progMeshRun.manualMode = manualMode;

                    progMeshRun.maxLod = maxLod;

                    progMeshRun.minLod = minLod;

                    progMeshRun.runInEditor = runInEditor;

                    progMeshRun.notes = notes;
                }
            }

            PrefabUtility.CreatePrefab(finalPrefabPath, objToPrefab);
        }

        private void CreateReplacePrefab(string meshPath, string preFabPath, ProgressiveMesh progMesh, int iteration)
        {
            string newPathMesh = meshPath.Replace(Application.dataPath, "Assets");
            string newPathPrefab = preFabPath.Replace(Application.dataPath, "Assets");

            string finalMeshPath = newPathMesh + "/" + meshFbxFiles[iteration].Name;
            string finalPrefabPath = newPathPrefab + "/" + progMeshes[iteration].name.Replace("_Progressive_Mesh", "") + addedToFilename + ".prefab";

            GameObject objToPrefab = null;
            ProgressiveMeshRuntime progMeshRun = null;

            try
            {
                objToPrefab = AssetDatabase.LoadAssetAtPath(finalMeshPath, typeof(GameObject)) as GameObject;
            }
            catch (UnityException e)
            {
                Debug.Log(e);
            }

            if (objToPrefab == null)
            {
                var sceneObj = PrefabUtility.CreatePrefab(finalPrefabPath, objToPrefab, ReplacePrefabOptions.ReplaceNameBased) as GameObject;

                SetSettings(sceneObj, progMeshRun, progMesh);
            }
            else
            {
                var sceneObj = PrefabUtility.CreatePrefab(finalPrefabPath, objToPrefab, ReplacePrefabOptions.ReplaceNameBased) as GameObject;

                SetSettings(sceneObj, progMeshRun, progMesh);
            }
        }

        private void SetSettings(GameObject sceneObj, ProgressiveMeshRuntime progMeshRun, ProgressiveMesh progMesh)
        {
            if (sceneObj != null)
            {
                progMeshRun = sceneObj.AddComponent<ProgressiveMeshRuntime>();

                if (progMeshRun != null)
                {
                    progMeshRun.progressiveMesh = progMesh;

                    if (useRuntimeSettings)
                    {
                        progMeshRun.cullRatio = cullRatio;

                        progMeshRun.intensity = lodIntensity;

                        //progMeshRun.lodLevels = lodLevels;

                        progMeshRun.manualMode = manualMode;

                        progMeshRun.maxLod = maxLod;

                        progMeshRun.minLod = minLod;

                        progMeshRun.runInEditor = runInEditor;

                        progMeshRun.notes = notes;
                    }
                }
                else
                {
                    Debug.LogError(string.Format("Failed to add ProgressiveMesh Runtime Component to: {0}", sceneObj.name));
                }
                AssetDatabase.Refresh();
                AssetDatabase.SaveAssets();
            }
            else
            {
                Debug.LogError(string.Format("Failed to create new prefab instance for: {0}", progMesh.name));
            }
        }

        private void CreatePrefabDepreceated(string preFabPath, ProgressiveMesh progMesh)
        {
            GameObject newObject;

            GameObject prefabCurrent = null;

            ProgressiveMesh progMeshCurrent = null;

            string combined = preFabPath + "/" + progMesh.name;
            string toBeSearched = "Resources/";
            string placeToLoad = combined.Substring(combined.IndexOf(toBeSearched) + toBeSearched.Length);

            try
            {
                prefabCurrent = Resources.Load(preFabPath) as GameObject;
            }
            catch (UnityException e)
            {
                Debug.Log(e);
            }

            if (prefabCurrent == null)
            {
                newObject = new GameObject();
                newObject.name = progMesh.name;

                string combinedPrepath = preFabPath + "/" + progMesh.name;
                string toBeSearchedPrePath = "Resources/";
                string newPlace = combinedPrepath.Substring(combinedPrepath.IndexOf(toBeSearchedPrePath) + toBeSearchedPrePath.Length);

                string filePath = newPlace + ".prefab";
                string newPrefabPath = preFabPath + "/" + progMesh.name + ".prefab";

                string searchedAssetsPath = "Resources/";
                string newPrefabPathing = newPrefabPath.Substring(newPrefabPath.IndexOf(searchedAssetsPath) + searchedAssetsPath.Length);

                Debug.Log(preFabPath);
                PrefabUtility.CreateEmptyPrefab(newPrefabPathing);
                //PrefabUtility.CreateEmptyPrefab(filePath);
                // Save the mesh as an asset.
                Debug.Log(newPlace);
                Debug.Log(newPrefabPathing);
                //AssetDatabase.CreateAsset(newObject, newPlace);
                //AssetDatabase.SaveAssets();
                //AssetDatabase.Refresh();
                //doesn't exist
            }
            else
            {
                Debug.Log("Prefab exists");
                var newProgRunTime = prefabCurrent.AddComponent<ProgressiveMeshRuntime>();
                if(newProgRunTime != null)
                {
                    if(newProgRunTime.progressiveMesh != null)
                    {
                        newProgRunTime.progressiveMesh = progMeshCurrent;
                    }
                }
            }

            //

        }

        private bool GetProgressiveMeshes(string path)
        {
            string combined = path + "/";
            string toBeSearched = "Resources/";
            string placeToLoad = combined.Substring(combined.IndexOf(toBeSearched) + toBeSearched.Length);

            progMeshes = Resources.LoadAll<ProgressiveMesh>(placeToLoad);

            if(progMeshes.Length > 0)
            {
                return true;
            }

            return false;
        }

        private void GetProgressiveMeshesList(string path)
        {
            string totalPath = path + "/";

            //Get all files contained in this folder
            DirectoryInfo dirInfo = new DirectoryInfo(totalPath);
            FileInfo[] fileInfo = dirInfo.GetFiles("*.*", SearchOption.AllDirectories);

            //Initialize
            meshFbxFiles = new List<FileInfo>();
            string extension;

            for (int i = 0; i < fileInfo.Length; i++)
            {
                extension = fileInfo[i].Extension.ToLower();
                //FBX
                if (extension == ".fbx" || extension == ".obj" || extension == ".dae" || extension == ".3ds" || extension == ".dxf" || extension == ".ma" || extension == ".mb" || extension == ".c4d" || extension == ".max" || extension == ".jas" || extension == ".lxo" || extension == ".lws" || extension == ".blend")
                {
                    meshFbxFiles.Add(fileInfo[i]);
                }
                //META
                if (fileInfo[i].Extension == ".meta" || fileInfo[i].Extension == ".META")
                {
                    
                }
            }
            //Initialize after this all the mesh files have been added
            meshesNames = new string[meshFbxFiles.Count];

            for (int i = 0; i < meshFbxFiles.Count; i++)
            {
                //FBX
                meshesNames[i] = meshFbxFiles[i].Name.Replace(".FBX", "");
            }
        }

        private static string GetActiveObjectPath()
        {
            string path = "Assets";
            foreach (UnityEngine.Object obj in Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.Assets))
            {
                path = AssetDatabase.GetAssetPath(obj);
                if (File.Exists(path))
                {
                    path = Path.GetDirectoryName(path);
                }
                break;
            }

            return path;
        }

        public static string GetUniqueAssetPathNameOrFallback(string filename)
        {
            string path;
            try
            {
                // Private implementation of a filenaming function which puts the file at the selected path.
                System.Type assetdatabase = typeof(UnityEditor.AssetDatabase);
                path = (string)assetdatabase.GetMethod("GetUniquePathNameAtSelectedPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(assetdatabase, new object[] { filename });
            }
            catch
            {
                // Protection against implementation changes.
                path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath("Assets/" + filename);
            }
            return path;
        }

        public string GetSubstringByString(string a, string b, string c)
        {
            return c.Substring((c.IndexOf(a) + a.Length), (c.IndexOf(b) - c.IndexOf(a) - a.Length));
        }
    }
}
