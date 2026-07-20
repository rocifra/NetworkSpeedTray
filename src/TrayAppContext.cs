using System;
using System.Drawing;
using System.Windows.Forms;

namespace NetworkSpeedTray
{
    /// <summary>
    /// The application's runtime context. Owns the tray icon, the sampling timer,
    /// and the right-click menu. There is no visible window.
    /// </summary>
    internal sealed class TrayAppContext : ApplicationContext
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly Timer _timer;
        private readonly SpeedSampler _sampler = new SpeedSampler();
        private readonly TrayIconRenderer _renderer = new TrayIconRenderer();
        private readonly ToolStripMenuItem _startupItem;
        private readonly ToolStripMenuItem _widgetItem;
        private readonly ToolStripMenuItem _clickThroughItem;
        private readonly FloatingWidgetForm _widget;

        public TrayAppContext()
        {
            Settings.Load();

            _widgetItem = new ToolStripMenuItem("Show speed widget");
            _widgetItem.CheckOnClick = true;
            _widgetItem.Checked = Settings.WidgetVisible;
            _widgetItem.Click += OnToggleWidget;

            _clickThroughItem = new ToolStripMenuItem("Click-through (locks position)");
            _clickThroughItem.CheckOnClick = true;
            _clickThroughItem.Checked = Settings.ClickThrough;
            _clickThroughItem.Click += OnToggleClickThrough;

            _startupItem = new ToolStripMenuItem("Run at startup");
            _startupItem.CheckOnClick = true;
            _startupItem.Checked = StartupManager.IsEnabled();
            _startupItem.Click += OnToggleStartup;

            ToolStripMenuItem exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += OnExit;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add(_widgetItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(BuildOpacityMenu());
            menu.Items.Add(BuildFontMenu());
            menu.Items.Add(_clickThroughItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_startupItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);

            _widget = new FloatingWidgetForm(menu);

            _notifyIcon = new NotifyIcon();
            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.Text = "Network Speed";
            _notifyIcon.Icon = _renderer.Render();
            _notifyIcon.Visible = true;
            _notifyIcon.DoubleClick += OnTrayDoubleClick;

            if (Settings.WidgetVisible)
            {
                _widget.ShowWidget();
            }

            // Establish the sampling baseline immediately.
            _sampler.Sample();

            _timer = new Timer();
            _timer.Interval = 1000;
            _timer.Tick += OnTick;
            _timer.Start();
        }

        private void OnTick(object sender, EventArgs e)
        {
            _sampler.Sample();
            double down = _sampler.DownBytesPerSec;
            double up = _sampler.UpBytesPerSec;

            string tooltip = SpeedFormatter.Tooltip(down, up);
            // NotifyIcon.Text is capped at 63 chars; guard defensively.
            if (tooltip.Length > 63)
            {
                tooltip = tooltip.Substring(0, 63);
            }
            _notifyIcon.Text = tooltip;

            if (_widget != null && _widget.Visible)
            {
                _widget.UpdateSpeeds(down, up);
            }
        }

        private ToolStripMenuItem BuildOpacityMenu()
        {
            ToolStripMenuItem parent = new ToolStripMenuItem("Opacity");
            int[] values = new int[] { 50, 65, 80, 90, 100 };
            foreach (int v in values)
            {
                ToolStripMenuItem item = new ToolStripMenuItem(v + "%");
                item.Tag = v;
                item.Checked = (v == Settings.WidgetOpacityPercent);
                item.Click += OnOpacityClick;
                parent.DropDownItems.Add(item);
            }
            return parent;
        }

        private void OnOpacityClick(object sender, EventArgs e)
        {
            ToolStripMenuItem item = (ToolStripMenuItem)sender;
            CheckOnly(item);
            int v = (int)item.Tag;
            Settings.WidgetOpacityPercent = v;
            Settings.Save();
            _widget.SetWidgetOpacity(v);
        }

        private ToolStripMenuItem BuildFontMenu()
        {
            ToolStripMenuItem parent = new ToolStripMenuItem("Font size");
            AddFontItem(parent, "Small", 9f);
            AddFontItem(parent, "Medium", 12f);
            AddFontItem(parent, "Large", 16f);
            AddFontItem(parent, "Extra large", 20f);
            return parent;
        }

        private void AddFontItem(ToolStripMenuItem parent, string name, float pt)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(name);
            item.Tag = pt;
            item.Checked = (Math.Abs(Settings.WidgetFontSize - pt) < 0.01f);
            item.Click += OnFontClick;
            parent.DropDownItems.Add(item);
        }

        private void OnFontClick(object sender, EventArgs e)
        {
            ToolStripMenuItem item = (ToolStripMenuItem)sender;
            CheckOnly(item);
            float pt = (float)item.Tag;
            Settings.WidgetFontSize = pt;
            Settings.Save();
            _widget.SetWidgetFontSize(pt);
        }

        // Radio behavior within a submenu: check the clicked item, uncheck its siblings.
        private static void CheckOnly(ToolStripMenuItem item)
        {
            ToolStrip owner = item.Owner;
            if (owner == null)
            {
                return;
            }
            foreach (ToolStripItem sibling in owner.Items)
            {
                ToolStripMenuItem mi = sibling as ToolStripMenuItem;
                if (mi != null)
                {
                    mi.Checked = (mi == item);
                }
            }
        }

        private void OnToggleClickThrough(object sender, EventArgs e)
        {
            Settings.ClickThrough = _clickThroughItem.Checked;
            Settings.Save();
            _widget.SetClickThrough(Settings.ClickThrough);
        }

        private void OnToggleWidget(object sender, EventArgs e)
        {
            SetWidgetVisible(_widgetItem.Checked);
        }

        private void OnTrayDoubleClick(object sender, EventArgs e)
        {
            SetWidgetVisible(!Settings.WidgetVisible);
        }

        private void SetWidgetVisible(bool visible)
        {
            Settings.WidgetVisible = visible;
            _widgetItem.Checked = visible;

            if (visible)
            {
                _widget.UpdateSpeeds(_sampler.DownBytesPerSec, _sampler.UpBytesPerSec);
                _widget.ShowWidget();
            }
            else
            {
                _widget.Hide();
            }

            Settings.Save();
        }

        private void OnToggleStartup(object sender, EventArgs e)
        {
            try
            {
                if (_startupItem.Checked)
                {
                    StartupManager.Enable();
                }
                else
                {
                    StartupManager.Disable();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not update the startup setting:\n" + ex.Message,
                    "Network Speed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                // Revert the checkbox to reflect the real registry state.
                _startupItem.Checked = StartupManager.IsEnabled();
            }
        }

        private void OnExit(object sender, EventArgs e)
        {
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_timer != null)
                {
                    _timer.Stop();
                    _timer.Dispose();
                }
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                }
                if (_renderer != null)
                {
                    _renderer.Dispose();
                }
                if (_widget != null)
                {
                    _widget.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
