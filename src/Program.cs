using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ValorantStretchHelper
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (new Mutex(true, "ValorantUltrawideStretchHelper_SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show(
                        "Der Valorant Ultrawide Stretch Helper läuft bereits.\r\n" +
                        "Bitte im Infobereich (Tray) neben der Uhr nachsehen.",
                        "Bereits gestartet", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                SetProcessDPIAware();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                var form = new MainForm();
                Application.Run(form);

                // Letztes Sicherheitsnetz: Originaleinstellungen wiederherstellen,
                // falls das beim Schließen noch nicht passiert ist.
                form.RestoreOriginalSettingsIfNeeded(false);
            }
        }
    }
}
