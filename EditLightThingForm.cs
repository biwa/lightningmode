using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CodeImp.DoomBuilder.LightingMode;

namespace CodeImp.DoomBuilder.Plugins.Lighting
{
	public partial class EditLightThingForm : Form
	{
		private LightingThing lightingthing;

		public EditLightThingForm()
		{
			InitializeComponent();
		}

		public EditLightThingForm(LightingThing lt)
		{
			InitializeComponent();

			this.isenabled.Checked = lt.Enabled;

			lightingthing = lt;
		}

		private void acceptbutton_Click(object sender, EventArgs e)
		{
			lightingthing.Enabled = this.isenabled.Checked;

			this.Close();
		}
	}
}
