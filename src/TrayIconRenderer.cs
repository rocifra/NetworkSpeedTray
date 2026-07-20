using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NetworkSpeedTray
{
    /// <summary>
    /// Creates a static upload/download icon suitable for a NotifyIcon and owns
    /// the unmanaged HICON lifetime.
    /// </summary>
    internal sealed class TrayIconRenderer : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr handle);

        private static readonly Color DownColor = Color.FromArgb(255, 90, 210, 105);
        private static readonly Color UpColor = Color.FromArgb(255, 80, 165, 255);

        private Icon _icon;
        private IntPtr _handle = IntPtr.Zero;

        public Icon Render()
        {
            if (_icon != null)
            {
                return _icon;
            }

            Size size = SystemInformation.SmallIconSize;
            using (Bitmap bitmap = new Bitmap(size.Width, size.Height))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;

                float width = size.Width;
                float height = size.Height;
                float stroke = Math.Max(1.5f, width * 0.12f);

                using (Pen upPen = new Pen(UpColor, stroke))
                using (Pen downPen = new Pen(DownColor, stroke))
                {
                    upPen.StartCap = LineCap.Round;
                    upPen.EndCap = LineCap.Round;
                    upPen.LineJoin = LineJoin.Round;
                    downPen.StartCap = LineCap.Round;
                    downPen.EndCap = LineCap.Round;
                    downPen.LineJoin = LineJoin.Round;

                    // Upload arrow on the left, pointing up.
                    PointF upTip = new PointF(width * 0.31f, height * 0.14f);
                    graphics.DrawLine(upPen, upTip, new PointF(width * 0.31f, height * 0.86f));
                    graphics.DrawLine(upPen, upTip, new PointF(width * 0.10f, height * 0.38f));
                    graphics.DrawLine(upPen, upTip, new PointF(width * 0.52f, height * 0.38f));

                    // Download arrow on the right, pointing down.
                    PointF downTip = new PointF(width * 0.69f, height * 0.86f);
                    graphics.DrawLine(downPen, new PointF(width * 0.69f, height * 0.14f), downTip);
                    graphics.DrawLine(downPen, downTip, new PointF(width * 0.48f, height * 0.62f));
                    graphics.DrawLine(downPen, downTip, new PointF(width * 0.90f, height * 0.62f));
                }

                _handle = bitmap.GetHicon();
                _icon = Icon.FromHandle(_handle);
                return _icon;
            }
        }

        public void Dispose()
        {
            if (_icon != null)
            {
                _icon.Dispose();
                _icon = null;
            }
            if (_handle != IntPtr.Zero)
            {
                DestroyIcon(_handle);
                _handle = IntPtr.Zero;
            }
        }
    }
}
