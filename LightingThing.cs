#region ================== Namespaces

using System;
using System.Windows.Forms;
using CodeImp.DoomBuilder.Geometry;

#endregion

namespace CodeImp.DoomBuilder.LightingMode
{
	public class LightingThing
	{
		#region ================== Variables

		private Vector3D pos;
		private Vector3D oldpos;
		private bool enabled;
		private int maxradius;

		private bool selected;

		#endregion

		#region ================== Properties

		public Vector3D Position { get { return pos; } set { pos = value; } }
		public Vector3D OldPosition { get { return oldpos; } set { oldpos = value; } }
		public bool Enabled { get { return enabled; } set { enabled = value; } }
		public int MaxRadius { get { return maxradius; } set { maxradius = value; } }

		public bool Selected { get { return selected; } set { selected = value; } }

		#endregion

		#region ================== Constructor / Disposer

		internal LightingThing(Vector3D p)
		{
			//MessageBox.Show("Inserted light at " + p.ToString());
			pos = p;
			enabled = true;
			maxradius = 1024;
		}

		#endregion
	}
}
