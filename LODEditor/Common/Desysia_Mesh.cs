using UnityEngine;
using System.Collections;

namespace DesysiaLOD {
	public class Desysia_Mesh {
		public Mesh mesh;
		public int index;
		public string uuid;
		public int[][] origin_triangles;
		public int[] out_triangles;
		public int out_count;
		public Desysia_Mesh() {
			index = -1;
			uuid = null;
			out_count = 0;
		}
	}
}
