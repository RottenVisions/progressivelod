using UnityEngine;
using System.Collections;

namespace DesysiaLOD {
	[System.Serializable]
	public class ProgressiveMesh : ScriptableObject {
		public static int max_lod_count = 20;
		public int[] triangles;
	}
}
