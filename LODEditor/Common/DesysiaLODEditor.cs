using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections;

public class DesysiaLODEditor: MonoBehaviour {
	#if UNITY_EDITOR
	[MenuItem("Desysia/LOD Editor/Component/Editor/LOD Editor")]
	public static void AddComponent() {
		GameObject SelectedObject = Selection.activeGameObject;
		if (SelectedObject) {
			// Register root object for undo.
			Undo.RegisterCreatedObjectUndo(SelectedObject.AddComponent(typeof(DesysiaLODEditor)), "Add LOD Editor");
		}
	}
	[MenuItem ("Desysia/LOD Editor/Component/Editor/LOD Editor", true)]
	static bool ValidateAddComponent () {
		// Return false if no gameobject is selected.
		return Selection.activeGameObject != null;
	}
	#endif
}
