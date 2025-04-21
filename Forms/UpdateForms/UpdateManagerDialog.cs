using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using OMNI.Services;
using OMNI.Services.Update;
using OMNI.Forms.UpdateForms;

namespace OMNI.Forms.UpdateForms
{
    public partial class UpdateManagerDialog : Form
    {
        private readonly string _newVersion;
        private readonly string _releaseUrl;
        private readonly string _releaseNotes;
        private readonly UpdateInstallerService _updateService;
        private readonly List<BackupInfo> _availableBackups;
        private bool _isUpdating = false;
        private bool _updateCompleted = false;
        private Exception? _lastException = null;
        private string _lastErrorMessage = string.Empty;

        public UpdateManagerDialog(string newVersion, string releaseUrl, string releaseNotes)
        {
            _newVersion = newVersion;
            _releaseUrl = releaseUrl;
            _releaseNotes = releaseNotes;
            _updateService = new UpdateInstallerService();
            _availableBackups = _updateService.GetAvailableBackups();

            InitializeComponents();
            SetupEventHandlers();
        }

        private void InitializeComponents()
        {
            // Form settings
            Text = "OMNI Update Manager";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            Size = new Size(600, 500);
            Font = new Font(Font.FontFamily, 9f);
            Icon = SystemIcons.Information;

            // Title label
            var titleLabel = new Label
            {
                Text = $"OMNI v{_newVersion} is available!",
                Font = new Font(Font.FontFamily, 14f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(0, 10, 0, 0)
            };

            // Current version label - now using VersionManagerService instead of GetAppVersion.FromAboutDialog from 1.3.9
            var currentVersionLabel = new Label
            {
                Text = $"You're currently using v{VersionManagerService.GetCurrentVersion()}",
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 30
            };

            // Tab control for update/rollback options
            var tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(10, 10)
            };

            // Tab pages
            var updateTabPage = new TabPage("Update");
            var rollbackTabPage = new TabPage("Rollback");

            // Update tab content
            var releaseNotesLabel = new Label
            {
                Text = "Release Notes:",
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold),
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
                Margin = new Padding(10)
            };

            // Progress panel
            var progressPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 80,
                Padding = new Padding(10),
                Visible = false
            };

            var progressBar = new ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Dock = DockStyle.Top,
                Height = 20
            };

            var progressLabel = new Label
            {
                Text = "Preparing update...",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(0, 5, 0, 0)
            };

            progressPanel.Controls.AddRange(new Control[] { progressBar, progressLabel });

            // Update tab layout
            updateTabPage.Controls.Add(releaseNotesTextBox);
            updateTabPage.Controls.Add(releaseNotesLabel);
            updateTabPage.Controls.Add(progressPanel);

            // Rollback tab content
            var rollbackInfoLabel = new Label
            {
                Text = "Select a backup version to restore:",
                Dock = DockStyle.Top,
                Height = 30,
                Padding = new Padding(10, 10, 10, 0)
            };

            var backupListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Margin = new Padding(10)
            };

            backupListView.Columns.Add("Version", 80);
            backupListView.Columns.Add("Date", 130);
            backupListView.Columns.Add("Size", 70);
            backupListView.Columns.Add("Filename", 250);

            // Add available backups to the list
            foreach (var backup in _availableBackups)
            {
                var item = new ListViewItem(backup.Version);
                item.SubItems.Add(backup.CreationDate.ToString("yyyy-MM-dd HH:mm:ss"));
                item.SubItems.Add($"{backup.FileSizeInMB} MB");
                item.SubItems.Add(backup.FileName);
                item.Tag = backup;
                backupListView.Items.Add(item);
            }

            var noBackupsLabel = new Label
            {
                Text = "No backups available.",
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                Visible = _availableBackups.Count == 0
            };

            // Rollback tab layout
            rollbackTabPage.Controls.Add(backupListView);
            rollbackTabPage.Controls.Add(rollbackInfoLabel);
            rollbackTabPage.Controls.Add(noBackupsLabel);

            // Add tabs to tab control
            tabControl.TabPages.Add(updateTabPage);
            tabControl.TabPages.Add(rollbackTabPage);

            // Button panel
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                Padding = new Padding(10)
            };

            var updateButton = new Button
            {
                Text = "Install Update",
                Size = new Size(120, 30),
                Location = new Point(buttonPanel.Width - 130, 10),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            var rollbackButton = new Button
            {
                Text = "Restore Selected",
                Size = new Size(120, 30),
                Location = new Point(buttonPanel.Width - 130, 10),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Enabled = _availableBackups.Count > 0
            };

            var closeButton = new Button
            {
                Text = "Close",
                Size = new Size(100, 30),
                Location = new Point(buttonPanel.Width - 240, 10),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            // Add View Log button
            var viewLogButton = new Button
            {
                Text = "View Log",
                Size = new Size(100, 30),
                Location = new Point(10, 10),
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                Visible = false // Only show when there's a log available
            };

            // Adjust buttons based on active tab
            tabControl.SelectedIndexChanged += (s, e) =>
            {
                if (tabControl.SelectedIndex == 0) // Update tab
                {
                    updateButton.Visible = true;
                    rollbackButton.Visible = false;
                }
                else // Rollback tab
                {
                    updateButton.Visible = false;
                    rollbackButton.Visible = true;
                }
            };

            buttonPanel.Controls.AddRange(new Control[] { updateButton, rollbackButton, closeButton, viewLogButton });
            rollbackButton.Visible = false; // Start with update tab selected

            // Add controls to form
            Controls.AddRange(new Control[]
            {
                titleLabel,
                currentVersionLabel,
                tabControl,
                buttonPanel
            });

            // Store controls for event handlers
            Tag = new Dictionary<string, Control>
            {
                { "progressPanel", progressPanel },
                { "progressLabel", progressLabel },
                { "updateButton", updateButton },
                { "rollbackButton", rollbackButton },
                { "closeButton", closeButton },
                { "viewLogButton", viewLogButton },
                { "backupListView", backupListView },
                { "tabControl", tabControl }
            };
        }

        private void SetupEventHandlers()
        {
            if (Tag is not Dictionary<string, Control> controls)
            {
                return;
            }

            if (!controls.TryGetValue("progressPanel", out var panelControl) || panelControl is not Panel progressPanel)
            {
                return;
            }

            if (!controls.TryGetValue("progressLabel", out var labelControl) || labelControl is not Label progressLabel)
            {
                return;
            }

            if (!controls.TryGetValue("updateButton", out var uBtnControl) || uBtnControl is not Button updateButton)
            {
                return;
            }

            if (!controls.TryGetValue("rollbackButton", out var rBtnControl) || rBtnControl is not Button rollbackButton)
            {
                return;
            }

            if (!controls.TryGetValue("closeButton", out var cBtnControl) || cBtnControl is not Button closeButton)
            {
                return;
            }

            if (!controls.TryGetValue("viewLogButton", out var vLogBtnControl) || vLogBtnControl is not Button viewLogButton)
            {
                return;
            }

            if (!controls.TryGetValue("backupListView", out var listControl) || listControl is not ListView backupListView)
            {
                return;
            }

            if (!controls.TryGetValue("tabControl", out var tcControl) || tcControl is not TabControl tabControl)
            {
                return;
            }

            // Update service events
            _updateService.ProgressChanged += (s, message) =>
            {
                if (!IsDisposed && IsHandleCreated && message != null)
                {
                    Invoke(() =>
                    {
                        progressLabel.Text = message;
                    });
                }
            };

            // Add error event handler
            _updateService.UpdateError += (s, args) =>
            {
                if (!IsDisposed && IsHandleCreated)
                {
                    Invoke(() =>
                    {
                        _lastErrorMessage = args.Message;
                        _lastException = args.Exception;

                        // Show the view log button
                        viewLogButton.Visible = true;
                    });
                }
            };

            _updateService.UpdateCompleted += (s, success) =>
            {
                if (!IsDisposed && IsHandleCreated)
                {
                    Invoke(() =>
                    {
                        _isUpdating = false;
                        _updateCompleted = success;

                        UpdateUIState();

                        if (success)
                        {
                            MessageBox.Show(
                                "Update was installed successfully. Please restart the application to complete the update.",
                                "Update Completed",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);

                            DialogResult = DialogResult.OK;
                        }
                        else
                        {
                            // Show detailed error dialog instead of generic message
                            using (var errorDialog = new UpdateErrorDialog(
                            _lastErrorMessage,
                            _lastException ?? new Exception("The code had *one* job. And it failed spectacularly."),//null coalescing operator
                            _updateService.GetLogFilePath(),
                            _updateService.GetLogContent()))
                            {
                                errorDialog.ShowDialog(this);
                            }
                        }
                    });
                }
            };

            // View log button click
            viewLogButton.Click += (s, e) =>
            {
                using (var errorDialog = new UpdateErrorDialog(
                _lastErrorMessage,
                _lastException ?? new Exception("The code had *one* job. And it failed spectacularly."),//null coalescing operator
                _updateService.GetLogFilePath(),
                _updateService.GetLogContent()))
                {
                    errorDialog.ShowDialog(this);
                }
            };

            // Update button click
            updateButton.Click += async (s, e) =>
            {
                var result = MessageBox.Show(
                    "Do you want to download and install this update? The application will be backed up before updating.",
                    "Confirm Update",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    _isUpdating = true;
                    UpdateUIState();
                 

                    await Task.Run(async () =>
                    {
                        await _updateService.InstallUpdateAsync(_newVersion, _releaseUrl);
                    });
                }
            };

            // Rollback button click
            rollbackButton.Click += async (s, e) =>
            {
                if (backupListView.SelectedItems.Count == 0)
                {
                    MessageBox.Show(
                        "Please select a backup version to restore.",
                        "No Backup Selected",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                if (backupListView.SelectedItems[0].Tag is not BackupInfo selectedBackup)
                {
                    MessageBox.Show(
                        "Invalid backup information selected.",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                var result = MessageBox.Show(
                    $"Are you sure you want to roll back to version {selectedBackup.Version}?\n\n" +
                    "A backup of your current version will be created before rolling back.",
                    "Confirm Rollback",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    _isUpdating = true;
                    UpdateUIState();

                    await Task.Run(async () =>
                    {
                        await _updateService.RollbackToVersionAsync(selectedBackup.FileName);
                    });

                    MessageBox.Show(
                        "Rollback completed. Please restart the application to complete the process.",
                        "Rollback Complete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    DialogResult = DialogResult.OK;
                }
            };

            // Close button click
            closeButton.Click += (s, e) =>
            {
                if (_isUpdating)
                {
                    MessageBox.Show(
                        "Please wait for the current operation to complete.",
                        "Operation in Progress",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                DialogResult = DialogResult.Cancel;
                Close();
            };

            // Form closing
            FormClosing += (s, e) =>
            {
                if (_isUpdating)
                {
                    e.Cancel = true;
                    MessageBox.Show(
                        "An update or rollback is in progress. Please wait for it to complete.",
                        "Operation in Progress",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            };
        }

        private void UpdateUIState()
        {
            if (Tag is not Dictionary<string, Control> controls)
            {
                return;
            }

            if (!controls.TryGetValue("progressPanel", out var panelControl) || panelControl is not Panel progressPanel)
            {
                return;
            }

            if (!controls.TryGetValue("progressLabel", out var labelControl) || labelControl is not Label progressLabel)
            {
                return;
            }

            if (!controls.TryGetValue("updateButton", out var uBtnControl) || uBtnControl is not Button updateButton)
            {
                return;
            }

            if (!controls.TryGetValue("rollbackButton", out var rBtnControl) || rBtnControl is not Button rollbackButton)
            {
                return;
            }

            if (!controls.TryGetValue("closeButton", out var cBtnControl) || cBtnControl is not Button closeButton)
            {
                return;
            }

            if (!controls.TryGetValue("viewLogButton", out var vLogBtnControl) || vLogBtnControl is not Button viewLogButton)
            {
                return;
            }

            if (!controls.TryGetValue("tabControl", out var tcControl) || tcControl is not TabControl tabControl)
            {
                return;
            }

            if (_isUpdating)
            {
                // Disable controls during update
                progressPanel.Visible = true;
                updateButton.Enabled = false;
                rollbackButton.Enabled = false;
                closeButton.Enabled = false;
                tabControl.Enabled = false;
            }
            else
            {
                // Re-enable controls after update
                progressPanel.Visible = _updateCompleted;
                updateButton.Enabled = true;
                rollbackButton.Enabled = _availableBackups.Count > 0;
                closeButton.Enabled = true;
                tabControl.Enabled = true;

                // Show View Log button if there was an error
                viewLogButton.Visible = _lastException != null || !string.IsNullOrEmpty(_lastErrorMessage);
            }
        }
    }
}