using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using OMNI.Forms.UpdateForms;

namespace OMNI.Forms.UpdateForms
{
    public class UpdateErrorDialog : Form
    {
        private readonly string _errorMessage;
        private readonly Exception _exception;
        private readonly string _logPath;
        private readonly string _logContent;

        public UpdateErrorDialog(string errorMessage, Exception exception, string logPath, string logContent)
        {
            _errorMessage = errorMessage ?? "An unknown error occurred during the update process.";
            _exception = exception;
            _logPath = logPath;
            _logContent = logContent;

            InitializeComponents();
        }

        private void InitializeComponents()
        {
            // Form setup
            Text = "Update Error";
            Size = new Size(600, 500);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Icon = SystemIcons.Error;

            // Main layout panel
            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(10),
                RowStyles = {
                    new RowStyle(SizeType.AutoSize),
                    new RowStyle(SizeType.AutoSize),
                    new RowStyle(SizeType.AutoSize),
                    new RowStyle(SizeType.Percent, 100),
                    new RowStyle(SizeType.AutoSize)
                }
            };

            // Error icon and message
            var headerPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10)
            };

            var errorIcon = new PictureBox
            {
                Image = SystemIcons.Error.ToBitmap(),
                SizeMode = PictureBoxSizeMode.AutoSize,
                Margin = new Padding(0, 0, 10, 0)
            };

            var errorLabel = new Label
            {
                Text = "The update could not be completed",
                Font = new Font(Font.FontFamily, 12, FontStyle.Bold),
                AutoSize = true
            };

            headerPanel.Controls.Add(errorIcon);
            headerPanel.Controls.Add(errorLabel);

            // Error message
            var messageLabel = new Label
            {
                Text = $"Error: {_errorMessage}",
                Dock = DockStyle.Fill,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10)
            };

            // Exception details (if available)
            var detailsLabel = new Label
            {
                Text = "Technical Details:",
                Font = new Font(Font.FontFamily, 9, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 5)
            };

            // Log content
            var logTextBox = new RichTextBox
            {
                Text = GetLogDetails(),
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 8.25f),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 0, 10)
            };

            // Button panel
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true
            };

            var closeButton = new Button
            {
                Text = "Close",
                AutoSize = true,
                Padding = new Padding(10, 5, 10, 5),
                DialogResult = DialogResult.Cancel
            };

            var openLogButton = new Button
            {
                Text = "Open Log File",
                AutoSize = true,
                Padding = new Padding(10, 5, 10, 5),
                Margin = new Padding(10, 0, 0, 0)
            };

            var copyLogButton = new Button
            {
                Text = "Copy Log",
                AutoSize = true,
                Padding = new Padding(10, 5, 10, 5),
                Margin = new Padding(10, 0, 0, 0)
            };

            buttonPanel.Controls.Add(closeButton);
            buttonPanel.Controls.Add(openLogButton);
            buttonPanel.Controls.Add(copyLogButton);

            // Add all controls to main panel
            mainPanel.Controls.Add(headerPanel, 0, 0);
            mainPanel.Controls.Add(messageLabel, 0, 1);
            mainPanel.Controls.Add(detailsLabel, 0, 2);
            mainPanel.Controls.Add(logTextBox, 0, 3);
            mainPanel.Controls.Add(buttonPanel, 0, 4);

            // Add main panel to form
            Controls.Add(mainPanel);

            // Wire up events
            FormClosed += (s, e) => DialogResult = DialogResult.Cancel;

            closeButton.Click += (s, e) => Close();

            openLogButton.Click += (s, e) =>
            {
                try
                {
                    if (!string.IsNullOrEmpty(_logPath) && File.Exists(_logPath))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = _logPath,
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        MessageBox.Show(
                            "Log file not found or not accessible.",
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error
                        );
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Error opening log file: {ex.Message}",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
            };

            copyLogButton.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetText(logTextBox.Text);
                    MessageBox.Show(
                        "Log details copied to clipboard.",
                        "Copy Successful",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Error copying log: {ex.Message}",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
            };
        }

        private string GetLogDetails()
        {
            var details = new System.Text.StringBuilder();

            details.AppendLine("UPDATE LOG");
            details.AppendLine(new string('-', 50));

            // Add log content if available
            if (!string.IsNullOrEmpty(_logContent))
            {
                details.AppendLine(_logContent);
            }
            else
            {
                details.AppendLine("No detailed log information available.");
            }

            // Add exception details if available
            if (_exception != null)
            {
                details.AppendLine();
                details.AppendLine("EXCEPTION DETAILS");
                details.AppendLine(new string('-', 50));
                details.AppendLine($"Message: {_exception.Message}");
                details.AppendLine($"Type: {_exception.GetType().FullName}");
                details.AppendLine($"Source: {_exception.Source}");
                details.AppendLine();
                details.AppendLine("Stack Trace:");
                details.AppendLine(_exception.StackTrace);

                if (_exception.InnerException != null)
                {
                    details.AppendLine();
                    details.AppendLine("INNER EXCEPTION");
                    details.AppendLine(new string('-', 50));
                    details.AppendLine($"Message: {_exception.InnerException.Message}");
                    details.AppendLine($"Type: {_exception.InnerException.GetType().FullName}");
                    details.AppendLine();
                    details.AppendLine("Stack Trace:");
                    details.AppendLine(_exception.InnerException.StackTrace);
                }
            }

            details.AppendLine();
            details.AppendLine("SYSTEM INFORMATION");
            details.AppendLine(new string('-', 50));
            details.AppendLine($"OS: {Environment.OSVersion}");
            details.AppendLine($".NET Version: {Environment.Version}");
            details.AppendLine($"Application Directory: {AppDomain.CurrentDomain.BaseDirectory}");

            if (!string.IsNullOrEmpty(_logPath))
            {
                details.AppendLine($"Log File: {_logPath}");
            }

            return details.ToString();
        }
    }
}