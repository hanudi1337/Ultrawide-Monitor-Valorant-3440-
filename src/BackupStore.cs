using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ValorantStretchHelper
{
    public class BackupData
    {
        public string DeviceName = "";
        public int Width;
        public int Height;
        public int Frequency;
        public Dictionary<uint, uint> Scaling = new Dictionary<uint, uint>();
    }

    /// <summary>
    /// Speichert die Originaleinstellungen in %APPDATA%\ValorantStretchHelper\original_settings.txt.
    /// Die Datei wird beim Aktivieren angelegt und nach erfolgreichem Zurücksetzen gelöscht.
    /// Existiert sie beim Start noch, wurde das Tool zuvor nicht sauber beendet (Absturz) –
    /// dann kann daraus wiederhergestellt werden.
    /// </summary>
    public static class BackupStore
    {
        private static string GetFilePath()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ValorantStretchHelper");
            return Path.Combine(dir, "original_settings.txt");
        }

        public static bool Exists()
        {
            try { return File.Exists(GetFilePath()); }
            catch { return false; }
        }

        public static void Save(BackupData data)
        {
            try
            {
                string path = GetFilePath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                var sb = new StringBuilder();
                sb.AppendLine("device=" + data.DeviceName);
                sb.AppendLine("width=" + data.Width.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("height=" + data.Height.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("frequency=" + data.Frequency.ToString(CultureInfo.InvariantCulture));
                foreach (KeyValuePair<uint, uint> kv in data.Scaling)
                    sb.AppendLine("scaling." + kv.Key.ToString(CultureInfo.InvariantCulture) + "=" +
                                  kv.Value.ToString(CultureInfo.InvariantCulture));

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch
            {
                // Backup ist Best-Effort – die In-Memory-Werte bleiben die Hauptquelle
            }
        }

        public static BackupData Load()
        {
            try
            {
                string path = GetFilePath();
                if (!File.Exists(path)) return null;

                var data = new BackupData();
                foreach (string rawLine in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string line = rawLine.Trim();
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq);
                    string value = line.Substring(eq + 1);

                    if (key == "device") data.DeviceName = value;
                    else if (key == "width") data.Width = int.Parse(value, CultureInfo.InvariantCulture);
                    else if (key == "height") data.Height = int.Parse(value, CultureInfo.InvariantCulture);
                    else if (key == "frequency") data.Frequency = int.Parse(value, CultureInfo.InvariantCulture);
                    else if (key.StartsWith("scaling.", StringComparison.Ordinal))
                    {
                        uint id = uint.Parse(key.Substring("scaling.".Length), CultureInfo.InvariantCulture);
                        data.Scaling[id] = uint.Parse(value, CultureInfo.InvariantCulture);
                    }
                }
                return data.Width > 0 && data.Height > 0 ? data : null;
            }
            catch
            {
                return null;
            }
        }

        public static void Delete()
        {
            try
            {
                string path = GetFilePath();
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
            }
        }
    }
}
