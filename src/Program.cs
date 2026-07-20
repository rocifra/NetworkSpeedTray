using System;
using System.Threading;
using System.Windows.Forms;

namespace NetworkSpeedTray
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            // Prevent a second instance from stacking tray icons.
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "NetworkSpeedTray_SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayAppContext());
            }
        }
    }
}
