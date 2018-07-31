using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections;
using System.Runtime.InteropServices;

namespace DesysiaLOD {
	[ExecuteInEditMode]
	public class ProgressiveMeshSchedule : MonoBehaviour {
		private static int max_lod_queue_count = 30;
		private static Hashtable[] lod_queues = null;
		private int current_queue_index = 0;

		#if UNITY_EDITOR
		[MenuItem("Desysia/LOD Editor/Component/Runtime/Progressive Mesh Schedule")]
		public static void AddComponent() {
			GameObject SelectedObject = Selection.activeGameObject;
			if (SelectedObject) {
				// Register root object for undo.
				Undo.RegisterCreatedObjectUndo(SelectedObject.AddComponent(typeof(ProgressiveMeshSchedule)), "Add Progressive Mesh Schedule");
			}
		}
		[MenuItem ("Desysia/LOD Editor/Component/Runtime/Progressive Mesh Schedule", true)]
		static bool ValidateAddComponent () {
			// Return false if no gameobject is selected.
			return Selection.activeGameObject != null;
		}
		#endif

		private static void init_all() {
			if (lod_queues == null) {
				lod_queues = new Hashtable[max_lod_queue_count];
				for (int i=0; i<lod_queues.Length; i++) {
					lod_queues[i] = new Hashtable();
				}
			}
		}
		void Awake() {
		}
		public static int register_me(ProgressiveMeshRuntime rtm) {
			init_all();
			int token = (int)(UnityEngine.Random.value * (max_lod_queue_count-1));
			lod_queues[token].Add(rtm, rtm);
			return token;
		}

		public static void unregister_me(int token, ProgressiveMeshRuntime rtm) {
			if (lod_queues != null) {
				lod_queues[token].Remove(rtm);
			}
		}

		// Use this for initialization
		void Start () {
			init_all();
			if (!Application.isPlaying) {
				EditorApplication.update += Update;
			}
		}
		
		// Update is called once per frame
		void Update () {
			if (lod_queues != null) {
				Hashtable queue = lod_queues[current_queue_index];
				foreach (DictionaryEntry entry in queue) {
					((ProgressiveMeshRuntime)entry.Key).Update_Me();
				}
				current_queue_index++;
				current_queue_index %= max_lod_queue_count;
			}
		}
		void OnEnable() {
			Awake();
			Start();
		}
		void OnDisable() {
			OnDestroy();
		}
		void OnDestroy() {
			if (!Application.isPlaying) {
				EditorApplication.update -= Update;
			}
		}
	}
}
