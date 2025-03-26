using OMNI.Services;
using OMNI.Services.Update;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using OMNI.Forms.UpdateForms;

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
            this.Size = new Size(500, 450);
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

            // Current version label - use VersionManagerService instead of GetAppVersion.FromAboutDialog
            var currentVersionLabel = new Label
            {
                Text = $"You're currently using v{VersionManagerService.GetCurrentVersion()}",
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

            // Create a flow layout panel for the buttons
            var buttonFlowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(10),
                AutoSize = true
            };

            var downloadButton = new Button
            {
                Text = "Update Now",
                Size = new Size(140, 30),
                Margin = new Padding(10, 0, 0, 0)
            };

            var remindLaterButton = new Button
            {
                Text = "Remind Me Later",
                Size = new Size(140, 30),
                Margin = new Padding(10, 0, 0, 0)
            };

            // Add buttons to the flow panel
            buttonFlowPanel.Controls.Add(downloadButton);
            buttonFlowPanel.Controls.Add(remindLaterButton);

            // Create a panel for the checkbox
            var checkboxPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 40
            };

            var autoCheckCheckBox = new CheckBox
            {
                Text = "Automatically check for updates once daily",
                Checked = true,
                Location = new Point(10, 10),
                AutoSize = true
            };

            checkboxPanel.Controls.Add(autoCheckCheckBox);

            // Clear existing controls and add new ones
            this.Controls.Clear();
            this.Controls.AddRange(new Control[] {
        titleLabel,
        currentVersionLabel,
        releaseNotesLabel,
        releaseNotesTextBox,
        checkboxPanel,
        buttonFlowPanel
    });

            // Event handlers
            downloadButton.Click += (s, e) =>
            {
                try
                {
                    // Open the update manager dialog instead of just opening the browser
                    using (var updateManager = new UpdateManagerDialog(_newVersion, _releaseUrl, _releaseNotes))
                    {
                        this.Hide();
                        updateManager.ShowDialog(this.Owner);
                    }
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error opening update manager: {ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            remindLaterButton.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            autoCheckCheckBox.CheckedChanged += (s, e) =>
            {
                var settings = new SettingsService().CurrentSettings;
                settings.AutoCheckForUpdates = autoCheckCheckBox.Checked;
                new SettingsService().SaveSettings(settings);
            };

            // Load the auto-check setting
            autoCheckCheckBox.Checked = new SettingsService().CurrentSettings.AutoCheckForUpdates;
        }
    }
}