using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NetworkSpeedTray
{
    /// <summary>
    /// Manages the per-user "run at Windows startup" registry entry.
    /// Uses HKCU so no administrator rights are required.
    /// </summary>
    internal static class StartupManager
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "NetworkSpeedTray";

        private static string QuotedExePath()
        {
            return "\"" + Application.ExecutablePath + "\"";
        }

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null)
                    {
                        return false;
                    }

                    object value = key.GetValue(ValueName);
                    if (value == null)
                    {
                        return false;
                    }

                    // Consider it enabled only if it points at this executable.
                    string current = value.ToString();
                    return string.Equals(current, QuotedExePath(), StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void Enable()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
            {
                if (key != null)
                {
                    key.SetValue(ValueName, QuotedExePath(), RegistryValueKind.String);
                }
            }
        }

        public static void Disable()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
            {
                if (key != null && key.GetValue(ValueName) != null)
                {
                    key.DeleteValue(ValueName, false);
                }
            }
        }
    }
}
