using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ScreenshotTool.Core.Infrastructure;

namespace ScreenshotTool.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly IAppLogger _logger;
    private readonly IStartupRegistrationService _startupRegistration;
    private NotifyIcon? _notifyIcon;
    private Icon? _applicationIcon;

    public TrayIconService(IAppLogger logger, IStartupRegistrationService startupRegistration)
    {
        _logger = logger;
        _startupRegistration = startupRegistration;
    }

    public event EventHandler? ExitRequested;
    public event EventHandler? ScreenshotRequested;
    public event EventHandler? PinRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? RestorePinRequested;
    public event EventHandler? TogglePinsRequested;
    public event EventHandler? ClosePinsRequested;

    public void Start()
    {
        if (_notifyIcon is not null)
        {
            return;
        }

        var menu = new ContextMenuStrip
        {
            Renderer = new PurpleMenuRenderer(),
            BackColor = Color.FromArgb(250, 247, 255),
            ForeColor = Color.FromArgb(68, 56, 82),
            Font = new Font("Microsoft YaHei UI", 9F),
            Padding = new Padding(6),
            ShowImageMargin = false,
            ShowCheckMargin = true
        };
        menu.Opened += (_, _) => ApplyRoundedRegion(menu, 10);
        menu.Items.Add(CreateMenuItem("截图", (_, _) => ScreenshotRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(CreateMenuItem("贴图", (_, _) => PinRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(CreateMenuItem("恢复最近关闭贴图", (_, _) => RestorePinRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(CreateMenuItem("隐藏 / 显示全部贴图", (_, _) => TogglePinsRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(CreateMenuItem("关闭全部贴图", (_, _) => ClosePinsRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(CreateMenuItem("设置", (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty)));
        var autoStartItem = CreateMenuItem("开机自启动", (_, _) => ToggleAutoStart());
        menu.Opening += (_, _) => RefreshAutoStartItem(autoStartItem);
        RefreshAutoStartItem(autoStartItem);
        menu.Items.Add(autoStartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(CreateMenuItem("退出", (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty)));

        _applicationIcon = LoadApplicationIcon();
        _notifyIcon = new NotifyIcon
        {
            Icon = _applicationIcon ?? SystemIcons.Application,
            Text = "周天截图",
            ContextMenuStrip = menu,
            Visible = true
        };

        _logger.Information("系统托盘图标已初始化。");
    }

    public void Dispose()
    {
        if (_notifyIcon is null)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _notifyIcon = null;
        _applicationIcon?.Dispose();
        _applicationIcon = null;
    }

    public void ShowInformation(string message)
    {
        _notifyIcon?.ShowBalloonTip(2000, "周天截图", message, ToolTipIcon.Info);
    }

    private static Icon? LoadApplicationIcon()
    {
        try
        {
            var executablePath = Environment.ProcessPath;
            return string.IsNullOrWhiteSpace(executablePath)
                ? null
                : Icon.ExtractAssociatedIcon(executablePath);
        }
        catch
        {
            return null;
        }
    }

    private void RefreshAutoStartItem(ToolStripMenuItem item)
    {
        try { item.Checked = _startupRegistration.IsEnabled(); }
        catch (Exception exception)
        {
            item.Checked = false;
            _logger.Error("读取开机自启动状态失败。", exception);
        }
    }

    private void ToggleAutoStart()
    {
        try
        {
            var enabled = !_startupRegistration.IsEnabled();
            _startupRegistration.SetEnabled(enabled);
            ShowInformation(enabled ? "已启用开机自启动。" : "已关闭开机自启动。");
        }
        catch (Exception exception)
        {
            _logger.Error("修改开机自启动状态失败。", exception);
            ShowInformation("开机自启动设置失败，请重试。");
        }
    }

    private static ToolStripMenuItem CreateMenuItem(string text, EventHandler click)
    {
        var item = new ToolStripMenuItem(text)
        {
            AutoSize = false,
            Height = 36,
            Width = 166,
            Padding = new Padding(12, 0, 12, 0)
        };
        item.Click += click;
        return item;
    }

    private static void ApplyRoundedRegion(Control control, int radius)
    {
        using var path = RoundedRectangle(new Rectangle(0, 0, control.Width, control.Height), radius);
        control.Region?.Dispose();
        control.Region = new Region(path);
    }

    private static GraphicsPath RoundedRectangle(Rectangle rectangle, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter - 1, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter - 1, rectangle.Bottom - diameter - 1, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter - 1, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private sealed class PurpleMenuRenderer : ToolStripProfessionalRenderer
    {
        public PurpleMenuRenderer() : base(new PurpleColorTable()) => RoundedEdges = true;

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var path = RoundedRectangle(new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1), 10);
            using var pen = new Pen(Color.FromArgb(216, 204, 250));
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawPath(pen, path);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            using var pen = new Pen(Color.FromArgb(118, 82, 213), 2F)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var box = e.ImageRectangle;
            e.Graphics.DrawLines(pen, new System.Drawing.Point[]
            {
                new Point(box.Left + 3, box.Top + box.Height / 2),
                new Point(box.Left + 7, box.Bottom - 4),
                new Point(box.Right - 2, box.Top + 3)
            });
        }
    }

    private sealed class PurpleColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(250, 247, 255);
        public override Color ImageMarginGradientBegin => Color.FromArgb(250, 247, 255);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(250, 247, 255);
        public override Color ImageMarginGradientEnd => Color.FromArgb(250, 247, 255);
        public override Color MenuItemSelected => Color.FromArgb(237, 230, 255);
        public override Color MenuItemBorder => Color.FromArgb(216, 204, 250);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(237, 230, 255);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(237, 230, 255);
        public override Color MenuBorder => Color.FromArgb(216, 204, 250);
        public override Color SeparatorDark => Color.FromArgb(228, 220, 240);
        public override Color SeparatorLight => Color.FromArgb(228, 220, 240);
    }
}
