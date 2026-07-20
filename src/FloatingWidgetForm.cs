using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NetworkSpeedTray
{
    /// <summary>
    /// A borderless, always-on-top single-line readout that floats on the desktop.
    /// Draggable by default; position and appearance are remembered. Appearance
    /// (opacity, font size, click-through) is driven from the tray menu.
    /// </summary>
    internal sealed class FloatingWidgetForm : Form
    {
        private static readonly Color DownColor = Color.FromArgb(120, 220, 120); // green
        private static readonly Color UpColor = Color.FromArgb(120, 190, 255);   // blue

        // Extended-window-style bits for click-through.
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;

        // Gap (px) between the download and upload segments.
        private const int SegmentGap = 16;

        // Each segment reserves the width of this template so the widget never
        // resizes as values change. This is the widest string the formatter can
        // produce (values roll to the next unit at 1024, so 1023.9 is the max).
        private const string DownArrow = "↓";
        private const string UpArrow = "↑";
        private const string ArrowColumnTemplate = "↓ ";
        private const string ReserveValueTemplate = "1023.9 MB/s";

        // Measuring/drawing flags: single line, no extra glyph padding, exact metrics.
        private const TextFormatFlags MeasureFlags =
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
        private const TextFormatFlags DrawFlags =
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix |
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter;
        private const TextFormatFlags LeftDrawFlags =
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix |
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter;

        // Vertical padding above and below the glyphs (px).
        private const int VerticalPad = 3;

        private Font _font;
        private string _downText = "";
        private string _upText = "";
        private int _reservedWidth;
        private int _arrowColumnWidth;

        private bool _placed;
        private bool _dragging;
        private Point _dragOffset;
        private bool _clickThrough;
        private bool _pinnedToAllDesktops;

        public FloatingWidgetForm(ContextMenuStrip sharedMenu)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(24, 24, 24);
            Padding = new Padding(10, 0, 12, 0); // vertical size is set from the font metrics
            AutoSize = false;
            ContextMenuStrip = sharedMenu;
            DoubleBuffered = true;
            ResizeRedraw = true;

            _font = new Font("Consolas", Settings.WidgetFontSize, FontStyle.Bold, GraphicsUnit.Point);
            Opacity = Clamp(Settings.WidgetOpacityPercent, 10, 100) / 100.0;
            _clickThrough = Settings.ClickThrough;

            // The whole client area is draggable; there are no child controls.
            AttachDrag(this);

            Relayout();
            UpdateSpeeds(0, 0);
        }

        public void UpdateSpeeds(double downBytesPerSec, double upBytesPerSec)
        {
            // Width is fixed by Relayout(), so changing values never resizes the widget.
            _downText = SpeedFormatter.Full(downBytesPerSec);
            _upText = SpeedFormatter.Full(upBytesPerSec);
            Invalidate();
        }

        // Size the window to two fixed-width segments (depends only on the font).
        private void Relayout()
        {
            float dpiY;
            using (Graphics g = CreateGraphics())
            {
                dpiY = g.DpiY;
            }

            _arrowColumnWidth = TextRenderer.MeasureText(
                ArrowColumnTemplate,
                _font,
                Size.Empty,
                MeasureFlags).Width;
            int reservedValueWidth = TextRenderer.MeasureText(
                ReserveValueTemplate,
                _font,
                Size.Empty,
                MeasureFlags).Width;
            _reservedWidth = _arrowColumnWidth + reservedValueWidth;

            // Tight height: the glyph cell (ascent + descent) at this DPI, excluding the
            // font's external line-spacing, plus a small padding above and below.
            FontFamily family = _font.FontFamily;
            const FontStyle style = FontStyle.Bold;
            float emPx = _font.SizeInPoints * dpiY / 72f;
            float cellPx = emPx * (family.GetCellAscent(style) + family.GetCellDescent(style)) / family.GetEmHeight(style);
            int glyphHeight = (int)Math.Ceiling(cellPx);

            int width = Padding.Left + _reservedWidth + SegmentGap + _reservedWidth + Padding.Right;
            int height = glyphHeight + VerticalPad * 2;
            ClientSize = new Size(width, height);
        }

        public void ShowWidget()
        {
            Show();
            BringToFront();
        }

        // --- Appearance controls (invoked from the tray menu) ---

        public void SetWidgetOpacity(int percent)
        {
            percent = Clamp(percent, 10, 100);
            Opacity = percent / 100.0;
        }

        public void SetWidgetFontSize(float pointSize)
        {
            Font newFont = new Font("Consolas", pointSize, FontStyle.Bold, GraphicsUnit.Point);
            Font old = _font;
            _font = newFont;
            if (old != null)
            {
                old.Dispose();
            }
            Relayout();
            Invalidate();
        }

        public void SetClickThrough(bool on)
        {
            _clickThrough = on;
            // Apply immediately to the live window; CreateParams keeps it sticky.
            if (IsHandleCreated)
            {
                int ex = GetExStyle(Handle);
                if (on)
                {
                    ex |= WS_EX_TRANSPARENT | WS_EX_LAYERED;
                }
                else
                {
                    ex &= ~WS_EX_TRANSPARENT;
                }
                SetExStyle(Handle, ex);
            }
        }

        // Don't steal keyboard focus from the user's active window when shown.
        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        // Bake the extended styles into window creation so WinForms' own restyling
        // (triggered by setting Opacity/TopMost) can't clobber click-through.
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED; // needed for opacity and click-through
                if (_clickThrough)
                {
                    cp.ExStyle |= WS_EX_TRANSPARENT;
                }
                return cp;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (!_placed)
            {
                ApplyInitialPlacement();
                _placed = true;
            }

            // Pin after the window is visible so Explorer has created its
            // ApplicationView. BeginInvoke gives the shell one message cycle to
            // register the new top-level window.
            if (!_pinnedToAllDesktops)
            {
                BeginInvoke(new MethodInvoker(PinToAllDesktops));
            }
        }

        private void PinToAllDesktops()
        {
            _pinnedToAllDesktops = VirtualDesktopPinning.TryPinWindow(Handle);
        }

        private void ApplyInitialPlacement()
        {
            if (Settings.HasSavedLocation)
            {
                Rectangle bounds = new Rectangle(Settings.WidgetLocation, Size);
                if (IsVisibleOnAnyScreen(bounds))
                {
                    Location = Settings.WidgetLocation;
                    return;
                }
            }

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - 16, wa.Bottom - Height - 16);
        }

        private static bool IsVisibleOnAnyScreen(Rectangle bounds)
        {
            foreach (Screen s in Screen.AllScreens)
            {
                if (s.WorkingArea.IntersectsWith(bounds))
                {
                    return true;
                }
            }
            return false;
        }

        // --- Dragging (disabled while click-through is on, since the mouse passes through) ---

        private void AttachDrag(Control c)
        {
            c.MouseDown += OnDragDown;
            c.MouseMove += OnDragMove;
            c.MouseUp += OnDragUp;
        }

        private void OnDragDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                _dragOffset = new Point(
                    Cursor.Position.X - Location.X,
                    Cursor.Position.Y - Location.Y);
            }
        }

        private void OnDragMove(object sender, MouseEventArgs e)
        {
            if (_dragging)
            {
                Location = new Point(
                    Cursor.Position.X - _dragOffset.X,
                    Cursor.Position.Y - _dragOffset.Y);
            }
        }

        private void OnDragUp(object sender, MouseEventArgs e)
        {
            if (_dragging && e.Button == MouseButtons.Left)
            {
                _dragging = false;
                Settings.WidgetLocation = Location;
                Settings.HasSavedLocation = true;
                Settings.Save();
            }
        }

        // --- Owner-drawn, vertically-centered text (no border) ---

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            int height = ClientSize.Height;
            Rectangle downRect = new Rectangle(Padding.Left, 0, _reservedWidth, height);
            Rectangle downArrowRect = new Rectangle(
                downRect.X,
                downRect.Y,
                _arrowColumnWidth,
                downRect.Height);
            Rectangle downValueRect = new Rectangle(
                downRect.X + _arrowColumnWidth,
                downRect.Y,
                downRect.Width - _arrowColumnWidth,
                downRect.Height);
            TextRenderer.DrawText(e.Graphics, DownArrow, _font, downArrowRect, DownColor, LeftDrawFlags);
            TextRenderer.DrawText(e.Graphics, _downText, _font, downValueRect, DownColor, DrawFlags);

            int upX = Padding.Left + _reservedWidth + SegmentGap;
            Rectangle upRect = new Rectangle(upX, 0, _reservedWidth, height);
            Rectangle upArrowRect = new Rectangle(
                upRect.X,
                upRect.Y,
                _arrowColumnWidth,
                upRect.Height);
            Rectangle upValueRect = new Rectangle(
                upRect.X + _arrowColumnWidth,
                upRect.Y,
                upRect.Width - _arrowColumnWidth,
                upRect.Height);
            TextRenderer.DrawText(e.Graphics, UpArrow, _font, upArrowRect, UpColor, LeftDrawFlags);
            TextRenderer.DrawText(e.Graphics, _upText, _font, upValueRect, UpColor, DrawFlags);

        }

        // --- Click-through via extended window styles ---

        private static int GetExStyle(IntPtr hwnd)
        {
            if (IntPtr.Size == 8)
            {
                return (int)GetWindowLongPtr64(hwnd, GWL_EXSTYLE);
            }
            return GetWindowLong32(hwnd, GWL_EXSTYLE);
        }

        private static void SetExStyle(IntPtr hwnd, int style)
        {
            if (IntPtr.Size == 8)
            {
                SetWindowLongPtr64(hwnd, GWL_EXSTYLE, new IntPtr(style));
            }
            else
            {
                SetWindowLong32(hwnd, GWL_EXSTYLE, style);
            }
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hwnd, int index);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hwnd, int index, int value);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr value);

        private static int Clamp(int v, int lo, int hi)
        {
            if (v < lo) return lo;
            if (v > hi) return hi;
            return v;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _font != null)
            {
                _font.Dispose();
                _font = null;
            }
            base.Dispose(disposing);
        }
    }
}
