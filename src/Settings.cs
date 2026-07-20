using System;
using System.Drawing;
using System.Globalization;
using System.IO;

namespace NetworkSpeedTray
{
    /// <summary>
    /// Tiny key=value settings store persisted under %AppData%\NetworkSpeedTray.
    /// Keeps the floating widget's visibility and last position across sessions.
    /// </summary>
    internal static class Settings
    {
        public static bool WidgetVisible;
        public static Point WidgetLocation;
        public static bool HasSavedLocation;

        // Appearance (with defaults).
        public static int WidgetOpacityPercent = 85;
        public static float WidgetFontSize = 12f;
        public static bool ClickThrough;

        private static string FilePath()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NetworkSpeedTray");
            return Path.Combine(dir, "settings.ini");
        }

        public static void Load()
        {
            WidgetVisible = false;
            HasSavedLocation = false;
            WidgetOpacityPercent = 85;
            WidgetFontSize = 12f;
            ClickThrough = false;
            int x = 0;
            int y = 0;
            bool gotX = false;
            bool gotY = false;

            try
            {
                string path = FilePath();
                if (!File.Exists(path))
                {
                    return;
                }

                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    int eq = line.IndexOf('=');
                    if (eq <= 0)
                    {
                        continue;
                    }

                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();

                    if (key == "WidgetVisible")
                    {
                        WidgetVisible = (val == "1" ||
                            string.Equals(val, "true", StringComparison.OrdinalIgnoreCase));
                    }
                    else if (key == "WidgetX")
                    {
                        gotX = int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out x);
                    }
                    else if (key == "WidgetY")
                    {
                        gotY = int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out y);
                    }
                    else if (key == "WidgetOpacityPercent")
                    {
                        int op;
                        if (int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out op))
                        {
                            if (op < 10) op = 10;
                            if (op > 100) op = 100;
                            WidgetOpacityPercent = op;
                        }
                    }
                    else if (key == "WidgetFontSize")
                    {
                        float fs;
                        if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out fs))
                        {
                            if (fs < 6f) fs = 6f;
                            if (fs > 48f) fs = 48f;
                            WidgetFontSize = fs;
                        }
                    }
                    else if (key == "ClickThrough")
                    {
                        ClickThrough = (val == "1" ||
                            string.Equals(val, "true", StringComparison.OrdinalIgnoreCase));
                    }
                }

                if (gotX && gotY)
                {
                    WidgetLocation = new Point(x, y);
                    HasSavedLocation = true;
                }
            }
            catch (Exception)
            {
                // Corrupt/unreadable settings fall back to defaults.
            }
        }

        public static void Save()
        {
            try
            {
                string path = FilePath();
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string[] lines = new string[]
                {
                    "WidgetVisible=" + (WidgetVisible ? "1" : "0"),
                    "WidgetX=" + WidgetLocation.X.ToString(CultureInfo.InvariantCulture),
                    "WidgetY=" + WidgetLocation.Y.ToString(CultureInfo.InvariantCulture),
                    "WidgetOpacityPercent=" + WidgetOpacityPercent.ToString(CultureInfo.InvariantCulture),
                    "WidgetFontSize=" + WidgetFontSize.ToString("0.##", CultureInfo.InvariantCulture),
                    "ClickThrough=" + (ClickThrough ? "1" : "0")
                };
                File.WriteAllLines(path, lines);
            }
            catch (Exception)
            {
                // Best-effort; ignore write failures (e.g. locked file).
            }
        }
    }
}
