namespace CodeImp.DoomBuilder.Plugins.Lighting
{
	partial class EditLightThingForm
	{
		/// <summary>
		/// Erforderliche Designervariable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Verwendete Ressourcen bereinigen.
		/// </summary>
		/// <param name="disposing">True, wenn verwaltete Ressourcen gelöscht werden sollen; andernfalls False.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Vom Windows Form-Designer generierter Code

		/// <summary>
		/// Erforderliche Methode für die Designerunterstützung.
		/// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
		/// </summary>
		private void InitializeComponent()
		{
			this.isenabled = new System.Windows.Forms.CheckBox();
			this.label1 = new System.Windows.Forms.Label();
			this.comboBox1 = new System.Windows.Forms.ComboBox();
			this.acceptbutton = new System.Windows.Forms.Button();
			this.SuspendLayout();
			// 
			// isenabled
			// 
			this.isenabled.AutoSize = true;
			this.isenabled.Checked = true;
			this.isenabled.CheckState = System.Windows.Forms.CheckState.Checked;
			this.isenabled.Location = new System.Drawing.Point(13, 13);
			this.isenabled.Name = "isenabled";
			this.isenabled.Size = new System.Drawing.Size(81, 17);
			this.isenabled.TabIndex = 0;
			this.isenabled.Text = "Enable light";
			this.isenabled.UseVisualStyleBackColor = true;
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(14, 40);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(53, 13);
			this.label1.TabIndex = 1;
			this.label1.Text = "Light type";
			// 
			// comboBox1
			// 
			this.comboBox1.FormattingEnabled = true;
			this.comboBox1.Location = new System.Drawing.Point(73, 37);
			this.comboBox1.Name = "comboBox1";
			this.comboBox1.Size = new System.Drawing.Size(121, 21);
			this.comboBox1.TabIndex = 2;
			// 
			// acceptbutton
			// 
			this.acceptbutton.Location = new System.Drawing.Point(359, 356);
			this.acceptbutton.Name = "acceptbutton";
			this.acceptbutton.Size = new System.Drawing.Size(75, 23);
			this.acceptbutton.TabIndex = 3;
			this.acceptbutton.Text = "OK";
			this.acceptbutton.UseVisualStyleBackColor = true;
			this.acceptbutton.Click += new System.EventHandler(this.acceptbutton_Click);
			// 
			// EditLightThingForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(504, 420);
			this.Controls.Add(this.acceptbutton);
			this.Controls.Add(this.comboBox1);
			this.Controls.Add(this.label1);
			this.Controls.Add(this.isenabled);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "EditLightThingForm";
			this.Text = "Edit Light";
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.CheckBox isenabled;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.ComboBox comboBox1;
		private System.Windows.Forms.Button acceptbutton;
	}
}