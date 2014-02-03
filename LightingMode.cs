
#region ================== Copyright (c) 2007 Pascal vd Heiden

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
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
using System.Collections.Specialized;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using CodeImp.DoomBuilder.Data;
using CodeImp.DoomBuilder.Windows;
using CodeImp.DoomBuilder.IO;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Rendering;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Editing;
using CodeImp.DoomBuilder.Actions;
using CodeImp.DoomBuilder.Types;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Plugins.Lighting;

#endregion

namespace CodeImp.DoomBuilder.LightingMode
{
	[EditMode(DisplayName = "Lighting Mode",
			  SwitchAction = "lightingmode",		// Action name used to switch to this mode
			  ButtonImage = "Light_16.png",	// Image resource name for the button
			  ButtonOrder = int.MinValue + 400,	// Position of the button (lower is more to the left)
			  ButtonGroup = "000_editing",
			  UseByDefault = true,
			  SafeStartMode = true)]

	public class LightingMode : ClassicMode
	{
		#region ================== Constants

		#endregion

		#region ================== Variables

		// Highlighted item
		private LightingThing highlighted;
		//private Association[] association = new Association[Thing.NUM_ARGS];
		//private Association highlightasso = new Association();

		// Interface
		private bool editpressed;
		private bool thinginserted;
		private bool dragging;
		private Vector2D dragstartpos;

		public static List<LightingThing> lights = new List<LightingThing>();

		private ImageData lightimage;

		#endregion

		#region ================== Properties

		public override object HighlightedObject { get { return highlighted; } }

		#endregion

		#region ================== Constructor / Disposer

		#endregion

		#region ================== Methods

		public static LightingThing NearestLightingThingSquareRange(Vector2D pos, float maxrange)
		{
			RectangleF range = RectangleF.FromLTRB(pos.x - maxrange, pos.y - maxrange, pos.x + maxrange, pos.y + maxrange);
			LightingThing closest = null;
			float distance = float.MaxValue;
			float d;

			// Go for all vertices in selection
			foreach(LightingThing lt in lights)
			{
				// Within range?
				if ((lt.Position.x >= (range.Left - 32)) && (lt.Position.x <= (range.Right + 32)))
				{
					if ((lt.Position.y >= (range.Top - 32)) && (lt.Position.y <= (range.Bottom + 32)))
					{
						// Close than previous find?
						d = Math.Abs(lt.Position.x - pos.x) + Math.Abs(lt.Position.y - pos.y);
						if(d < distance)
						{
							// This one is closer
							closest = lt;
							distance = d;
						}
					}
				}
			}

			// Return result
			return closest;
		}

		public static void SaveLightingData()
		{
			ListDictionary lightdata = new ListDictionary();
			int counter = 0;

			foreach (LightingThing lt in lights)
			{
				ListDictionary data = new ListDictionary();
				data.Add("x", lt.Position.x);
				data.Add("y", lt.Position.y);
				data.Add("z", lt.Position.z);
				data.Add("enabled", lt.Enabled);

				lightdata.Add("light" + counter.ToString(), data);

				counter++;
			}

			General.Map.Options.WritePluginSetting("lights", lightdata);
		}

		private void LoadLightingData()
		{
			ListDictionary lightdata = new ListDictionary();
			
			// load the light info from the .dbs
			lightdata = (ListDictionary)General.Map.Options.ReadPluginSetting("lights", new ListDictionary());

			lights.Clear();

			foreach (DictionaryEntry lightentry in lightdata)
			{
				LightingThing lt = new LightingThing(new Vector3D(0, 0, 0));
				Vector3D pos = new Vector3D();

				foreach (DictionaryEntry entry in (ListDictionary)lightentry.Value)
				{
					if ((string)entry.Key == "x") pos.x = (float)entry.Value;
					if ((string)entry.Key == "y") pos.y = (float)entry.Value;
					if ((string)entry.Key == "z") pos.z = (float)entry.Value;
					if ((string)entry.Key == "enabled") lt.Enabled = (bool)entry.Value;
				}

				lt.Position = pos;

				lights.Add(lt);
			}
		}


		public override void OnHelp()
		{
			General.ShowHelp("e_things.html");
		}

		// Cancel mode
		public override void OnCancel()
		{
			base.OnCancel();

			// Return to this mode
			General.Editing.ChangeMode(new LightingMode());
		}

		// Mode engages
		public override void OnEngage()
		{
			base.OnEngage();

			dragging = false;

			BuilderPlug.Me.CreateBlockmap();

			// Here we load our image. The image is loaded from resource that is embedded
			// in this project. This is done by simply adding the PNG file to the project
			// and set the Build Action property of the image to "Embedded Resource".
			// Embedded means that the image will be compiled into your plugin .DLL file so
			// you don't have to distribute this image separately.
			lightimage = new ResourceImage("CodeImp.DoomBuilder.Plugins.Lighting.Light_128.png");

			// The image is not always directly loaded. Call this to ensure that the image is loaded immediately.
			lightimage.LoadImage();
			if (lightimage.LoadFailed) throw new Exception("Unable to load the Light_128.png resource!");

			// After loading, the image is usable by the GDI (GetBitmap() function) but we want to use
			// it for rendering in the working area. We must call CreateTexture to tranfer the image to
			// the vido memory as texture.
			lightimage.CreateTexture();

			LoadLightingData();

			// This tells the renderer how to display the map.
			// The renderer works with several different layers, each with its own purpose
			// and features. A "presentation" defines how to combine these layers when
			// presented to the user on the display. Here I make a special presentation
			// that includes only the layer we need: the Overlay layer.
			CustomPresentation p = new CustomPresentation();
			p.AddLayer(new PresentLayer(RendererLayer.Overlay, BlendingMode.None, 1.0f, false));
			//renderer.SetPresentation(p);

			renderer.SetPresentation(Presentation.Standard);

//			renderer.SetPresentation(Presentation.Things);

			// Convert geometry selection to linedefs selection
			General.Map.Map.ConvertSelection(SelectionType.Linedefs);
			General.Map.Map.SelectionType = SelectionType.Things;
		}

		// Mode disengages
		public override void OnDisengage()
		{
			lightimage.Dispose();

			base.OnDisengage();

			SaveLightingData();

			// Going to EditSelectionMode?
			//if (General.Editing.NewMode is EditSelectionMode)
			//{
			//    // Not pasting anything?
			//    EditSelectionMode editmode = (General.Editing.NewMode as EditSelectionMode);
			//    if (!editmode.Pasting)
			//    {
			//        // No selection made? But we have a highlight!
			//        if ((General.Map.Map.GetSelectedThings(true).Count == 0) && (highlighted != null))
			//        {
			//            // Make the highlight the selection
			//            highlighted.Selected = true;
			//        }
			//    }
			//}

			// Hide highlight info
			General.Interface.HideInfo();
		}

		// This redraws the display
		public override void OnRedrawDisplay()
		{
			base.OnRedrawDisplay();

			// We don't have to clear the other layers or anything, because they are not used
			// anyway (see how the presentation above is configured to show only the Overlay layer)

			// We now present it to the user on the display,
			// with the previously defined presentation settings.
			//renderer.Present();

			renderer.RedrawSurface();

			// Render lines and vertices
			if (renderer.StartPlotter(true))
			{
				renderer.PlotLinedefSet(General.Map.Map.Linedefs);
				renderer.PlotVerticesSet(General.Map.Map.Vertices);

				if (highlighted != null)
				{
					Queue<DrawnVertex> dvl;

					dvl = BuilderPlug.Me.CreateDVL(highlighted);

					while (dvl.Count != 0)
					{
						renderer.PlotLine(dvl.Dequeue().pos, dvl.Dequeue().pos, new PixelColor(128, 128, 128, 128));
					}
				}

				renderer.Finish();
			}

			// Render things
			if (renderer.StartThings(true))
			{
				renderer.RenderThingSet(General.Map.ThingsFilter.HiddenThings, Presentation.THINGS_HIDDEN_ALPHA);
				renderer.RenderThingSet(General.Map.ThingsFilter.VisibleThings, 1.0f);
				renderer.Finish();
			}

			//// Render things
			//if (renderer.StartThings(true))
			//{
			//    renderer.RenderThingSet(General.Map.ThingsFilter.HiddenThings, Presentation.THINGS_HIDDEN_ALPHA);
			//    renderer.RenderThingSet(General.Map.ThingsFilter.VisibleThings, 1.0f);
			//    //for (int i = 0; i < Thing.NUM_ARGS; i++) BuilderPlug.Me.RenderAssociations(renderer, association[i]);
			//    //if ((highlighted != null) && !highlighted.IsDisposed)
			//    //{
			//    //    BuilderPlug.Me.RenderReverseAssociations(renderer, highlightasso);
			//    //    renderer.RenderThing(highlighted, General.Colors.Highlight, 1.0f);
			//    //}
			//    renderer.Finish();
			//}

			//// Selecting?
			//if (selecting)
			//{
			//    // Render selection
			//    if (renderer.StartOverlay(true))
			//    {
			//        RenderMultiSelection();
			//        renderer.Finish();
			//    }
			//}

			// Clear the overlay and begin rendering to it.
			if (renderer.StartOverlay(true))
			{
				DrawLightingThings();
				// Finish our rendering to this layer.
				renderer.Finish();
			}


			renderer.Present();
		}

		private void DrawLightingThings()
		{
			foreach (LightingThing lt in lights)
			{
				Vector2D mappos = renderer.MapToDisplay(lt.Position);
				RectangleF r = new RectangleF(mappos.x - (16 * renderer.Scale), mappos.y - (16 * renderer.Scale), (64.0f * renderer.Scale) / 2, (64.0f * renderer.Scale) / 2);

				// Show the picture!
				if (lt == highlighted)
				{
					renderer.RenderRectangleFilled(r, PixelColor.FromColor(General.Colors.Highlight.ToColor()), false, lightimage);
				}
				else if (lt.Selected == true)
				{
					renderer.RenderRectangleFilled(r, PixelColor.FromColor(General.Colors.Selection.ToColor()), false, lightimage);
				}
				else
				{
					Color c = lt.Enabled ? Color.White : Color.Gray;
					renderer.RenderRectangleFilled(r, PixelColor.FromColor(c), false, lightimage);
				}
			}
		}


		// This highlights a new item
		protected void Highlight(LightingThing lt)
		{
			//bool completeredraw = false;
			//LinedefActionInfo action = null;

			highlighted = lt;

			if (lt != null || true)
			{
				General.Interface.RedrawDisplay();
			}

			//// Often we can get away by simply undrawing the previous
			//// highlight and drawing the new highlight. But if associations
			//// are or were drawn we need to redraw the entire display.

			//// Previous association highlights something?
			//if ((highlighted != null) && (highlighted.Tag > 0)) completeredraw = true;

			//// Set highlight association
			//if (t != null)
			//    highlightasso.Set(t.Tag, UniversalType.ThingTag);
			//else
			//    highlightasso.Set(0, 0);

			//// New association highlights something?
			//if ((t != null) && (t.Tag > 0)) completeredraw = true;

			//if (t != null)
			//{
			//    // Check if we can find the linedefs action
			//    if ((t.Action > 0) && General.Map.Config.LinedefActions.ContainsKey(t.Action))
			//        action = General.Map.Config.LinedefActions[t.Action];
			//}

			//// Determine linedef associations
			//for (int i = 0; i < Thing.NUM_ARGS; i++)
			//{
			//    // Previous association highlights something?
			//    if ((association[i].type == UniversalType.SectorTag) ||
			//       (association[i].type == UniversalType.LinedefTag) ||
			//       (association[i].type == UniversalType.ThingTag)) completeredraw = true;

			//    // Make new association
			//    if (action != null)
			//        association[i].Set(t.Args[i], action.Args[i].Type);
			//    else
			//        association[i].Set(0, 0);

			//    // New association highlights something?
			//    if ((association[i].type == UniversalType.SectorTag) ||
			//       (association[i].type == UniversalType.LinedefTag) ||
			//       (association[i].type == UniversalType.ThingTag)) completeredraw = true;
			//}

			//// If we're changing associations, then we
			//// need to redraw the entire display
			//if (completeredraw)
			//{
			//    // Set new highlight and redraw completely
			//    highlighted = t;
			//    General.Interface.RedrawDisplay();
			//}
			//else
			//{
			//    // Update display
			//    if (renderer.StartThings(false))
			//    {
			//        // Undraw previous highlight
			//        if ((highlighted != null) && !highlighted.IsDisposed)
			//            renderer.RenderThing(highlighted, renderer.DetermineThingColor(highlighted), 1.0f);

			//        // Set new highlight
			//        highlighted = t;

			//        // Render highlighted item
			//        if ((highlighted != null) && !highlighted.IsDisposed)
			//            renderer.RenderThing(highlighted, General.Colors.Highlight, 1.0f);

			//        // Done
			//        renderer.Finish();
			//        renderer.Present();
			//    }
			//}

			//// Show highlight info
			//if ((highlighted != null) && !highlighted.IsDisposed)
			//    General.Interface.ShowThingInfo(highlighted);
			//else
			//    General.Interface.HideInfo();
		}

		// Selection
		protected override void OnSelectBegin()
		{
			// Item highlighted?
			if (highlighted != null)
			{
				// Flip selection
				highlighted.Selected = !highlighted.Selected;

				if (renderer.StartOverlay(true))
				{
					DrawLightingThings();
					// Finish our rendering to this layer.
					renderer.Finish();
				}

				// Update display
				//if (renderer.StartThings(false))
				//{
				//    // Redraw highlight to show selection
				//    renderer.RenderThing(highlighted, renderer.DetermineThingColor(highlighted), 1.0f);
				//    renderer.Finish();
				//    renderer.Present();
				//}
			}
			else
			{
				// Start making a selection
				StartMultiSelection();
			}

			base.OnSelectBegin();
		}

		// End selection
		protected override void OnSelectEnd()
		{
			//// Not ending from a multi-selection?
			//if (!selecting)
			//{
			//    // Item highlighted?
			//    if ((highlighted != null) && !highlighted.IsDisposed)
			//    {
			//        // Update display
			//        if (renderer.StartThings(false))
			//        {
			//            // Render highlighted item
			//            renderer.RenderThing(highlighted, General.Colors.Highlight, 1.0f);
			//            renderer.Finish();
			//            renderer.Present();
			//        }
			//    }
			//}

			base.OnSelectEnd();
		}

		// Start editing
		protected override void OnEditBegin()
		{
			thinginserted = false;

			if (highlighted != null)
			{
				editpressed = true;
			}

			// Item highlighted?
			//if ((highlighted != null) && !highlighted.IsDisposed)
			//{
			//    // Edit pressed in this mode
			//    editpressed = true;

			//    // Highlighted item not selected?
			//    if (!highlighted.Selected && (BuilderPlug.Me.AutoClearSelection || (General.Map.Map.SelectedThingsCount == 0)))
			//    {
			//        // Make this the only selection
			//        General.Map.Map.ClearSelectedThings();
			//        highlighted.Selected = true;
			//        General.Interface.RedrawDisplay();
			//    }

			//    // Update display
			//    if (renderer.StartThings(false))
			//    {
			//        // Redraw highlight to show selection
			//        renderer.RenderThing(highlighted, renderer.DetermineThingColor(highlighted), 1.0f);
			//        renderer.Finish();
			//        renderer.Present();
			//    }
			//}
			//else
			//{
			//    // Mouse in window?
			//    if (mouseinside)
			//    {
			//        // Edit pressed in this mode
			//        editpressed = true;
			//        thinginserted = true;

			//        // Insert a new item and select it for dragging
			//        General.Map.UndoRedo.CreateUndo("Insert thing");
			//        Thing t = InsertThing(mousemappos);
			//        General.Map.Map.ClearSelectedThings();
			//        t.Selected = true;
			//        Highlight(t);
			//        General.Interface.RedrawDisplay();
			//    }
			//}

			base.OnEditBegin();
		}

		// Done editing
		protected override void OnEditEnd()
		{
			// Edit pressed in this mode?
			if (editpressed && dragging == false)
			{
				EditLightThingForm eltf = new EditLightThingForm(highlighted);

				eltf.ShowDialog();

				General.Interface.RedrawDisplay();

				//// Anything selected?
				//ICollection<Thing> selected = General.Map.Map.GetSelectedThings(true);
				//if (selected.Count > 0)
				//{
				//    if (General.Interface.IsActiveWindow)
				//    {
				//        // Edit only when preferred
				//        if (!thinginserted || BuilderPlug.Me.EditNewThing)
				//        {
				//            // Show thing edit dialog
				//            General.Interface.ShowEditThings(selected);

				//            // When a single thing was selected, deselect it now
				//            if (selected.Count == 1) General.Map.Map.ClearSelectedThings();

				//            // Update things filter
				//            General.Map.ThingsFilter.Update();

				//            // Update entire display
				//            General.Interface.RedrawDisplay();
				//        }
				//    }
				//}
			}

			editpressed = false;
			base.OnEditEnd();
		}

		// Mouse moves
		public override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);

			// Not holding any buttons?
			if (e.Button == MouseButtons.None)
			{
				// Find the nearest thing within highlight range
				LightingThing lt = NearestLightingThingSquareRange(mousemappos, 8 / renderer.Scale);

				// Highlight if not the same
				if (lt != highlighted) Highlight(lt);
			}
			else if(dragging == true && highlighted != null)
			{
				Vector2D oldpos = highlighted.Position;

				highlighted.Position = General.Map.Grid.SnappedToGrid(renderer.DisplayToMap(mousepos));

				foreach (LightingThing lt in lights)
				{
					if (lt.Selected == true && lt != highlighted)
					{
						lt.Position = lt.OldPosition + (highlighted.Position - highlighted.OldPosition);
					}
				}
				//highlighted.Position = renderer.DisplayToMap(General.Map.Grid.SnappedToGrid((Vector2D)mousepos));
				General.Interface.RedrawDisplay();
			}
		}

		// Mouse leaves
		public override void OnMouseLeave(EventArgs e)
		{
			base.OnMouseLeave(e);

			// Highlight nothing
			Highlight(null);
		}

		// Mouse wants to drag
		protected override void OnDragStart(MouseEventArgs e)
		{
			base.OnDragStart(e);

			if (highlighted != null)
			{
				dragging = true;
				dragstartpos = renderer.DisplayToMap(mousepos);

				foreach (LightingThing lt in lights)
				{
					lt.OldPosition = lt.Position;
				}
			}

			//MessageBox.Show("drag start");

			//// Edit button used?
			//if (General.Actions.CheckActionActive(null, "classicedit"))
			//{
			//    // Anything highlighted?
			//    if ((highlighted != null) && !highlighted.IsDisposed)
			//    {
			//        // Highlighted item not selected?
			//        if (!highlighted.Selected)
			//        {
			//            // Select only this sector for dragging
			//            General.Map.Map.ClearSelectedThings();
			//            highlighted.Selected = true;
			//        }

			//        // Start dragging the selection
			//        //General.Editing.ChangeMode(new DragThingsMode(new ThingsMode(), mousedownmappos));
			//    }
			//}
		}

		protected override void OnDragStop(MouseEventArgs e)
		{
			dragging = false;
		}

		// This is called wheh selection ends
		protected override void OnEndMultiSelection()
		{
			bool selectionvolume = ((Math.Abs(base.selectionrect.Width) > 0.1f) && (Math.Abs(base.selectionrect.Height) > 0.1f));

			//if (BuilderPlug.Me.AutoClearSelection && !selectionvolume)
			//    General.Map.Map.ClearSelectedThings();

			if (selectionvolume)
			{
				if (General.Interface.ShiftState /* ^ BuilderPlug.Me.AdditiveSelect */)
				{
					// Go for all things
					foreach (LightingThing lt in lights)
					{
						lt.Selected |= ((lt.Position.x >= selectionrect.Left) &&
									   (lt.Position.y >= selectionrect.Top) &&
									   (lt.Position.x <= selectionrect.Right) &&
									   (lt.Position.y <= selectionrect.Bottom));
					}
				}
				else
				{
					// Go for all things
					foreach (LightingThing lt in lights)
					{
						lt.Selected = ((lt.Position.x >= selectionrect.Left) &&
									  (lt.Position.y >= selectionrect.Top) &&
									  (lt.Position.x <= selectionrect.Right) &&
									  (lt.Position.y <= selectionrect.Bottom));
					}
				}
			}

			base.OnEndMultiSelection();

			// Clear overlay
			if (renderer.StartOverlay(true)) renderer.Finish();

			// Redraw
			General.Interface.RedrawDisplay();
		}

		// This is called when the selection is updated
		protected override void OnUpdateMultiSelection()
		{
			base.OnUpdateMultiSelection();

			// Render selection
			if (renderer.StartOverlay(true))
			{
				DrawLightingThings();
				RenderMultiSelection();
				renderer.Finish();
				renderer.Present();
			}
		}

		// When copying
		public override bool OnCopyBegin()
		{
			//// No selection made? But we have a highlight!
			//if ((General.Map.Map.GetSelectedThings(true).Count == 0) && (highlighted != null))
			//{
			//    // Make the highlight the selection
			//    highlighted.Selected = true;
			//}

			return base.OnCopyBegin();
		}

		#endregion

		#region ================== Actions

		// This clears the selection
		[BeginAction("clearselection", BaseAction = true)]
		public void ClearSelection()
		{
			// Clear selection
			foreach (LightingThing lt in lights)
			{
				lt.Selected = false;
			}

			// Redraw
			General.Interface.RedrawDisplay();
		}

		// This creates a new thing at the mouse position
		[BeginAction("insertitem", BaseAction = true)]
		public virtual void InsertThing()
		{
			// Mouse in window?
			if (mouseinside)
			{
				LightingThing lt = new LightingThing(renderer.DisplayToMap(mousepos));
				lt.Enabled = true;
				lights.Add(lt);

				General.Map.IsChanged = true;

			//    // Insert new thing
			//    General.Map.UndoRedo.CreateUndo("Insert thing");
			//    Thing t = InsertThing(mousemappos);

			//    // Edit the thing?
			//    if (BuilderPlug.Me.EditNewThing)
			//    {
			//        // Redraw screen
			//        General.Interface.RedrawDisplay();

			//        List<Thing> things = new List<Thing>(1);
			//        things.Add(t);
			//        General.Interface.ShowEditThings(things);
			//    }

			//    General.Interface.DisplayStatus(StatusType.Action, "Inserted a new thing.");

			//    // Update things filter
			//    General.Map.ThingsFilter.Update();

				// Redraw screen
				General.Interface.RedrawDisplay();
			}
		}

		// This creates a new thing
		private Thing InsertThing(Vector2D pos)
		{
			// Create things at mouse position
			Thing t = General.Map.Map.CreateThing();
			General.Settings.ApplyDefaultThingSettings(t);
			t.Move(pos);
			t.UpdateConfiguration();

			// Update things filter so that it includes this thing
			General.Map.ThingsFilter.Update();

			// Snap to grid enabled?
			if (General.Interface.SnapToGrid)
			{
				// Snap to grid
				t.SnapToGrid();
			}
			else
			{
				// Snap to map format accuracy
				t.SnapToAccuracy();
			}

			return t;
		}

		[BeginAction("deleteitem", BaseAction = true)]
		public void DeleteItem()
		{
			//// Make list of selected things
			//List<Thing> selected = new List<Thing>(General.Map.Map.GetSelectedThings(true));
			//if ((selected.Count == 0) && (highlighted != null) && !highlighted.IsDisposed) selected.Add(highlighted);
			List<LightingThing> selected = new List<LightingThing>();

			foreach(LightingThing lt in lights)
			{
				if (lt.Selected == true || lt == highlighted)
				{
					selected.Add(lt);
				}
			}

			if (selected.Count > 0)
			{
				foreach (LightingThing lt in selected)
				{
					lights.Remove(lt);
				}

				General.Map.IsChanged = true;
			}

			General.Interface.RedrawDisplay();

			//// Anything to do?
			//if (selected.Count > 0)
			//{
			//    // Make undo
			//    if (selected.Count > 1)
			//    {
			//        General.Map.UndoRedo.CreateUndo("Delete " + selected.Count + " things");
			//        General.Interface.DisplayStatus(StatusType.Action, "Deleted " + selected.Count + " things.");
			//    }
			//    else
			//    {
			//        General.Map.UndoRedo.CreateUndo("Delete thing");
			//        General.Interface.DisplayStatus(StatusType.Action, "Deleted a thing.");
			//    }

			//    // Dispose selected things
			//    foreach (Thing t in selected) t.Dispose();

			//    // Update cache values
			//    General.Map.IsChanged = true;
			//    General.Map.ThingsFilter.Update();

			//    // Invoke a new mousemove so that the highlighted item updates
			//    MouseEventArgs e = new MouseEventArgs(MouseButtons.None, 0, (int)mousepos.x, (int)mousepos.y, 0);
			//    OnMouseMove(e);

			//    // Redraw screen
			//    General.Interface.RedrawDisplay();
			//}
		}

		#endregion
	}
}
