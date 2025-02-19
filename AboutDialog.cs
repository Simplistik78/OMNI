using System.Drawing;
using System.Windows.Forms;

namespace OMNI.Forms
{
    public class AboutDialog : Form
    {
        public AboutDialog()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            // Form settings
            this.Text = "About OMNI";
            this.Size = new Size(600, 400);
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;

            // Create title label
            var titleLabel = new Label
            {
                Text = "OMNI - Overlay Map & Navigation Interface",
                Font = new Font(this.Font.FontFamily, 14, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 20)
            };

            // Create version label
            var versionLabel = new Label
            {
                Text = "Version 1.0.0",
                Location = new Point(20, titleLabel.Bottom + 10),
                AutoSize = true
            };

            // Create copyright label
            var copyrightLabel = new Label
            {
                Text = "Copyright © 2024-2025 OMNI Project",
                Location = new Point(20, versionLabel.Bottom + 20),
                AutoSize = true
            };

            // Create license text box with highlighted license notice
            var licenseTextBox = new RichTextBox
            {
                Location = new Point(20, copyrightLabel.Bottom + 20),
                Size = new Size(544, 200),
                ReadOnly = true,
                BackColor = SystemColors.Window
            };

            // Add the license text with highlighted portion
            licenseTextBox.Text = "OMNI is licensed under the GNU Affero General Public License v3 " +
                                "with additional restrictions on commercial use.\n\n" +
                                "This program is distributed in the hope that it will be useful, " +
                                "but WITHOUT ANY WARRANTY; without even the implied warranty of " +
                                "MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the " +
                                "GNU Affero General Public License for more details.\n\n" +
                                "For the complete license text and terms, see the LICENSE file in the project repository.";

            // Highlight the key license text
            licenseTextBox.SelectionStart = 0;
            licenseTextBox.SelectionLength = "OMNI is licensed under the GNU Affero General Public License v3 with additional restrictions on commercial use.".Length;
            licenseTextBox.SelectionFont = new Font(licenseTextBox.Font, FontStyle.Bold);

            // Create GitHub link
            var githubLink = new LinkLabel
            {
                Text = "GitHub: https://github.com/Simplistik78/OMNI",
                Location = new Point(20, licenseTextBox.Bottom + 20),
                AutoSize = true
            };
            githubLink.Click += (s, e) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/Simplistik78/OMNI",
                UseShellExecute = true
            });

            // Create OK button
            var okButton = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(264, githubLink.Bottom + 20),
                Size = new Size(75, 23)
            };

            // Add controls to form
            this.Controls.AddRange(new Control[] {
                titleLabel,
                versionLabel,
                copyrightLabel,
                licenseTextBox,
                githubLink,
                okButton
            });

            // Set form height based on controls
            this.ClientSize = new Size(584, okButton.Bottom + 20);
        }
    }
}