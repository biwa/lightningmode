
#region ================== Copyright (c) 2009 Boris Iwanski

/*
 * Copyright (c) 2009 Boris Iwanski
 * This program is released under GNU General Public License
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 */

#endregion

#region ================== Namespaces

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Reflection;
using CodeImp.DoomBuilder.Windows;
using CodeImp.DoomBuilder.IO;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Rendering;
using CodeImp.DoomBuilder.Geometry;
using System.Drawing;
using CodeImp.DoomBuilder.Editing;
using CodeImp.DoomBuilder.Plugins;
using CodeImp.DoomBuilder.Actions;
using CodeImp.DoomBuilder.Types;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Data;

#endregion

namespace CodeImp.DoomBuilder.LightingMode
{
	//
	// MANDATORY: The plug!
	// This is an important class to the Doom Builder core. Every plugin must
	// have exactly 1 class that inherits from Plug. When the plugin is loaded,
	// this class is instantiated and used to receive events from the core.
	// Make sure the class is public, because only public classes can be seen
	// by the core.
	//

	public class BuilderPlug : Plug
	{
		// Static instance. We can't use a real static class, because BuilderPlug must
		// be instantiated by the core, so we keep a static reference. (this technique
		// should be familiar to object-oriented programmers)
		private static BuilderPlug me;

		// Static property to access the BuilderPlug
		public static BuilderPlug Me { get { return me; } }

		private BlockMap<BlockEntry> blockmap;

		// This event is called when the plugin is initialized
		public override void OnInitialize()
		{
			base.OnInitialize();

			// This binds the methods in this class that have the BeginAction
			// and EndAction attributes with their actions. Without this, the
			// attributes are useless. Note that in classes derived from EditMode
			// this is not needed, because they are bound automatically when the
			// editing mode is engaged.
            General.Actions.BindMethods(this);

			// Keep a static reference
            me = this;
		}

		public override void OnMapSaveBegin(SavePurpose purpose)
		{
			LightingMode.SaveLightingData();
		}

		// This is called when the plugin is terminated
		public override void Dispose()
		{
			base.Dispose();

			// This must be called to remove bound methods for actions.
            General.Actions.UnbindMethods(this);
        }

		public Queue<DrawnVertex> CreateDVL(LightingThing lt)
		{
			List<Vertex> shadowCastingVertices = new List<Vertex>();
			Queue<DrawnVertex> dvl = new Queue<DrawnVertex>();

			// Update the blockmap if the map was changed
			if (General.Map.IsChanged)
				CreateBlockmap();

			// clear all vertices found for previous light sources
			shadowCastingVertices.Clear();

			// look for the vertices in the map that may cast a shadow
			// vertices that have a "blocking" line both facing and not
			// facing the light source are added to the list
			foreach (Vertex v in General.Map.Map.Vertices)
			{
				int facing = 0;

				if (Vector2D.Distance(lt.Position, v.Position) > 1024.0f) continue;

				foreach (Linedef ld in v.Linedefs)
				{
					// only one sided lines are "blocking" right now
					// TODO: other lines may be blocking too, for example
					// when the height differences between sectors is too big
					if (ld.Back == null)
					{
						if (ld.SideOfLine(lt.Position) <= 0.0f)
						{
							// with only one sided lines "blocking", only
							// one of those lines may face the light source,
							// if two are facing, that vertex can't cast
							// a shadow.
							// This method may not work when other lines
							// besides one sided lines can block
							facing++;
						}
					}
				}

				// only one line connected to the vertex facing the light source?
				// then let's check if the LOS from the light source to that vertex
				// is not blocked by another line. If it's not blocked finaly add
				// the vertex to the shadow casting vertex list
				if (facing == 1)
				{
					bool blocked = false;

					// get all blockmap blocks that are between the light and the
					// shadow casting vertex
					List<BlockEntry> blocks = blockmap.GetLineBlocks(lt.Position, v.Position);

					foreach (BlockEntry be in blocks)
					{
						foreach (Linedef ld in be.Lines)
						{
							// don't process lines that have the current vertex
							// as start or end vertex (they would instantly result
							// in an intersection)
							if (ld.Start == v || ld.End == v) continue;

							// don't process this linedef if it's start or end vertices are on the
							// line between the light and the current vertex
							if (Line2D.GetSideOfLine(lt.Position, v.Position, ld.Start.Position) == 0.0f) continue;
							if (Line2D.GetSideOfLine(lt.Position, v.Position, ld.End.Position) == 0.0f) continue;

							// only check one sided lines
							// the intersection check is done from between:
							// - the line we are looking at (start and end vertex)
							// - the line between the light source position (t.Position)
							//   and the current vertex position (v.Position)
							//
							// if any intersection was found no other lines have to be checked
							if (ld.Back == null && Line2D.GetIntersection(ld.Start.Position, ld.End.Position, lt.Position.x, lt.Position.y, v.Position.x, v.Position.y) == true)
							{
								// MessageBox.Show("vertex " + v.Index.ToString() + " intersection with line " + ld.Index.ToString());
								blocked = true;
								break;
							}
						}

						if (blocked == true) break;
					}

					// only add the vertex if no blocking occured
					if (blocked == false) shadowCastingVertices.Add(v);
				}
			}

			// process all shadow casting vertices
			foreach (Vertex v in shadowCastingVertices)
			{
				Vector2D normal;
				Vector2D closestIntersection;
				DrawnVertex[] dv = new DrawnVertex[2];
				// List<DrawnVertex> dvl = new List<DrawnVertex>();
				List<Vector2D> intersections = new List<Vector2D>();

				// get the unit vector of the line between the shadow casting vertex
				// and the light source
				normal = Line2D.GetNormal(v.Position.x - lt.Position.x, v.Position.y - lt.Position.y);

				// check for intersections between the view line of the light source
				// throught the shadow casting vertex against all one sided lines
				// in the map. The intersections are stored in a list
				foreach (Linedef ld in General.Map.Map.Linedefs)
				{
					float u_ray = 0;
					float u_line = 0;

					// don't process lines that have the current vertex
					// as start or end vertex (they would instantly result
					// in an intersection)
					if (ld.Start == v || ld.End == v) continue;

					// only check one sided lines
					// the intersection check is done from between:
					// - the line we are looking at (start and end vertex)
					// - the line between the shadow casting vertex and an imaginary
					//   end point in the far distance
					//
					// TODO: make Line2D.GetIntersection more readable
					if (ld.Back == null && Line2D.GetIntersection(ld.Start.Position, ld.End.Position, v.Position.x, v.Position.y, v.Position.x + normal.x * 10000, v.Position.y + normal.y * 10000, out u_ray, out u_line) == true)
					{
						// the position is computed by the info returned by Line2D.GetIntersection
						intersections.Add(new Vector2D(Line2D.GetCoordinatesAt(ld.Start.Position, ld.End.Position, u_line)));
					}
				}

				// if there are no intersections found continue with the next vertex
				if (intersections.Count == 0) continue;

				// now the closest intersection has to be found
				// the first intersection in the list is set as
				// the the closest intersection for now
				closestIntersection = intersections[0];

				// check out all intersections
				foreach (Vector2D vec in intersections)
				{
					// if the distance between the shadow casting vertex and the
					// looked at intersection is shorter than the old closest
					// intersection, set the looked at intersection as closest
					if (Vector2D.Distance(vec, v.Position) < Vector2D.Distance(closestIntersection, v.Position))
					{
						closestIntersection = vec;
					}
				}

				// create the vertices we need to draw
				// first the vertex at the shadow casting vertex...
				dv[0].pos.x = v.Position.x;
				dv[0].pos.y = v.Position.y;
				dv[0].stitch = true;
				dv[0].stitchline = true;

				// ... then the vertex at the closest intersection
				dv[1].pos.x = closestIntersection.x;
				dv[1].pos.y = closestIntersection.y;
				dv[1].stitch = true;
				dv[1].stitchline = true;

				// add the two vertices to the list we want to draw
				dvl.Enqueue(dv[0]);
				dvl.Enqueue(dv[1]);
			}

			return dvl;
		}

		public void CreateBlockmap()
		{
			// Make blockmap
			RectangleF area = MapSet.CreateArea(General.Map.Map.Vertices);
			area = MapSet.IncreaseArea(area, General.Map.Map.Things);
			if(blockmap != null) blockmap.Dispose();
			blockmap = new BlockMap<BlockEntry>(area);
			blockmap.AddLinedefsSet(General.Map.Map.Linedefs);
			blockmap.AddSectorsSet(General.Map.Map.Sectors);
			blockmap.AddThingsSet(General.Map.Map.Things);
		}

		public bool HasLOS(Linedef ld, Vector3D p)
		{
			Vector2D v1 = Line2D.GetCoordinatesAt(ld.Start.Position, ld.End.Position, 0.5f);
			string bla = "";

			if (Line2D.GetSideOfLine(ld.Start.Position, ld.End.Position, p) == 0.0f)
			{
			    return false;
			}

			foreach (Linedef ldx in General.Map.Map.Linedefs)
			{
				if (ld == ldx) continue;
				/*
				if (ld.Start == sd.Line.Start) continue;
				if (ld.Start == sd.Line.End) continue;
				if (ld.End == sd.Line.Start) continue;
				if (ld.End == sd.Line.End) continue;
				 */

				//if (Line2D.GetSideOfLine(ld.Start.Position, ld.End.Position, p) <= 0.0f)
				//{
				//    continue;
				//}

				bla += "checking ld " + ldx.Index.ToString() + " @ " + ldx.Start.Position.ToString() + " / " + ldx.End.Position.ToString() + " -vs- " + v1.ToString() + " / " + p.ToString();

				if (ldx.Back == null && Line2D.GetIntersection(ldx.Start.Position, ldx.End.Position, v1.x, v1.y, p.x, p.y) == true)
				{
						bla += " collision\n";
						//MessageBox.Show(bla);

					// MessageBox.Show(ld.Index.ToString());
					return false;
				}

				bla += " no collision\n";
			}

			//if (ld.Index == 12)
			
			// MessageBox.Show(bla);

			return true;
		}

		public bool HasLOS(Vector3D p1, Vector3D p2)
		{
			Line2D line = new Line2D(p1, p2);

			// TODO: use blockmap
			foreach (Linedef ld in General.Map.Map.Linedefs)
			{
				if (ld.Back == null && Line2D.GetIntersection(line, ld.Line) == true)
					return false;
			}

			return true;
		}

		private Vector2D GetIncenter(List<Vector2D> points)
		{
			Line2D a = new Line2D(points[0], points[1]);
			Line2D b = new Line2D(points[1], points[2]);
			Line2D c = new Line2D(points[2], points[0]);
			Vector2D A = points[2];
			Vector2D B = points[0];
			Vector2D C = points[1];
			float p = a.GetLength() + b.GetLength() + c.GetLength();

			float x = (a.GetLength() * A.x + b.GetLength() * B.x + c.GetLength() * C.x) / p;
			float y = (a.GetLength() * A.y + b.GetLength() * B.y + c.GetLength() * C.y) / p;

			return new Vector2D(x, y);
		}

        #region ================== Actions

        [BeginAction("do_lighting")]
        public void DoLighting()
        {
			Queue<DrawnVertex> dvl = new Queue<DrawnVertex>();

			// Make it not crash on drawing
			General.Settings.FindDefaultDrawSettings();

			// Make the action undo-able
			General.Map.UndoRedo.CreateUndo("Create lighting");
			
			// process every light source in the map
			foreach (LightingThing lt in LightingMode.lights)
			{
				Queue<DrawnVertex> dvltmp;

				// only process lights that are enabled
				if (lt.Enabled == false) continue;

				dvltmp = CreateDVL(lt);

				while (dvltmp.Count != 0)
				{
					dvl.Enqueue(dvltmp.Dequeue());
				}
			}

			General.Interface.DisplayStatus(StatusType.Action, "Drawing lighting...");

			// draw all lines
			while(dvl.Count != 0)
			{
				Tools.DrawLines(new List<DrawnVertex> { dvl.Dequeue(), dvl.Dequeue() });
				if(!General.Map.UDMF) General.Map.Map.SnapAllToAccuracy();
				General.Map.Map.Update();
			}

			// change the light values of the sectors
			foreach (LightingThing lt in LightingMode.lights)
			{
				// right now the light source must be a floor lamp
				if (lt.Enabled == false) continue;

				foreach (Sector s in General.Map.Map.Sectors)
				{
					bool lightit = false;

					s.Triangles.Triangulate(s);

					Vector2D incenter = GetIncenter(new List<Vector2D>() { s.Triangles.Vertices[0], s.Triangles.Vertices[1], s.Triangles.Vertices[2] });

					lightit = HasLOS(lt.Position, incenter);
						

					if (lightit == true)
					{
						s.Brightness += lt.Brightness;
					}
				}
			}

			General.Map.IsChanged = true;
			General.Map.Map.Update();
			General.Map.Data.UpdateUsedTextures();
			General.Interface.RedrawDisplay();

			General.Interface.DisplayStatus(StatusType.Action, "Created lighting.");
        }

        #endregion
    }
}
