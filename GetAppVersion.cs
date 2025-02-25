using OMNI.Forms;
using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace OMNI
{
    public static class GetAppVersion
    {
        public static string FromAboutDialog()
        {
            // Default fallback version
            string version = "1.0.0";

            try
            {
                using (var aboutDialog = new AboutDialog())
                {
                    // Create an instance without showing it
                    aboutDialog.CreateControl();

                    // Find the version label by iterating through controls
                    foreach (Control control in aboutDialog.Controls)
                    {
                        if (control is Label label && label.Text.StartsWith("Version "))
                        {
                            version = label.Text.Substring("Version ".Length).Trim();
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting version: {ex.Message}");
            }

            return version;
        }
    }
}