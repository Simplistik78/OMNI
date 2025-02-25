using OMNI.Services;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace OMNI.Forms
{
    public partial class UpdateNotificationDialog : Form
    {
        private readonly string _newVersion;
        private readonly string _releaseUrl;
        private readonly string _releaseNotes;

        public UpdateNotificationDialog(string newVersion, string releaseUrl, string releaseNotes)
        {
            _newVersion = newVersion;
            _releaseUrl = releaseUrl;
            _releaseNotes = releaseNotes;

            InitializeComponent(); 
            ConfigureCustomComponents(); 
        }

        private void ConfigureCustomComponents()
        {
            // Form settings
            this.Text = "Update Available";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.Size = new Size(500, 400);
            this.Font = new Font(this.Font.FontFamily, 9f);
            this.Icon = SystemIcons.Information;

            // Title label
            var titleLabel = new Label
            {
                Text = $"OMNI v{_newVersion} is now available!",
                Font = new Font(this.Font.FontFamily, 14f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(0, 10, 0, 0)
            };

            // Current version label
            var currentVersionLabel = new Label
            {
                Text = $"You're currently using v{Application.ProductVersion}",
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 30
            };

            // Release notes section
            var releaseNotesLabel = new Label
            {
                Text = "Release Notes:",
                Font = new Font(this.Font.FontFamily, 9f, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 20,
                Padding = new Padding(10, 5, 0, 0)
            };

            var releaseNotesTextBox = new RichTextBox
            {
                Text = _releaseNotes,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                BackColor = SystemColors.ControlLightLight,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(5),
                Margin = new Padding(10)
            };

            
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50
            };

            var downloadButton = new Button
            {
                Text = "Download Update",
                Size = new Size(150, 30),
                Location = new Point(buttonPanel.Width / 2 - 160, 10),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                BackColor = Color.FromArgb(70, 130, 180) // Steel Blue match buttons
            };

            var remindLaterButton = new Button
            {
                Text = "Remind Me Later",
                Size = new Size(150, 30),
                Location = new Point(buttonPanel.Width / 2 + 10, 10),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            // Checkbox for automatic chickidy checks
            var autoCheckCheckBox = new CheckBox
            {
                Text = "Automatically check for updates",
                Checked = true,
                Location = new Point(10, 15),
                AutoSize = true,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            buttonPanel.Controls.AddRange(new Control[] {
                downloadButton,
                remindLaterButton,
                autoCheckCheckBox
            });

            // Clear existing controls (if any)
            this.Controls.Clear();

            
            this.Controls.AddRange(new Control[] {
                titleLabel,
                currentVersionLabel,
                releaseNotesLabel,
                releaseNotesTextBox,
                buttonPanel
            });

            // Event handlers
            downloadButton.Click += (s, e) => {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _releaseUrl,
                        UseShellExecute = true
                    });
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error opening browser: {ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            remindLaterButton.Click += (s, e) => {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            autoCheckCheckBox.CheckedChanged += (s, e) => {
                var settings = new SettingsService().CurrentSettings;
                settings.AutoCheckForUpdates = autoCheckCheckBox.Checked;
                new SettingsService().SaveSettings(settings);
            };

            // Load the auto-check setting
            autoCheckCheckBox.Checked = new SettingsService().CurrentSettings.AutoCheckForUpdates;
        }
    }
}