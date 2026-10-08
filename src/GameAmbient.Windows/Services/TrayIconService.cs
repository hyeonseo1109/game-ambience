using System.Drawing;
using Forms = System.Windows.Forms;

namespace GameAmbient.Windows.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _status;
    private readonly Forms.ToolStripMenuItem _profile;
    private readonly Forms.ToolStripMenuItem _pause;

    public TrayIconService()
    {
        _status = new Forms.ToolStripMenuItem("Status: Idle") { Enabled = false };
        _profile = new Forms.ToolStripMenuItem("Profile: None") { Enabled = false };
        _pause = new Forms.ToolStripMenuItem("Pause / Resume", null, (_, _) => PauseResumeRequested?.Invoke(this, EventArgs.Empty));
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(new Forms.ToolStripMenuItem("Game Ambient") { Enabled = false });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_status);
        menu.Items.Add(_profile);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(new Forms.ToolStripMenuItem("Open", null, (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(_pause);
        menu.Items.Add(new Forms.ToolStripMenuItem("Stop Monitoring", null, (_, _) => StopRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(new Forms.ToolStripMenuItem("Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty)));
        _icon = new Forms.NotifyIcon { Text = "Game Ambient", Icon = SystemIcons.Application, ContextMenuStrip = menu, Visible = true };
        _icon.DoubleClick += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? OpenRequested;
    public event EventHandler? PauseResumeRequested;
    public event EventHandler? StopRequested;
    public event EventHandler? ExitRequested;

    public void Update(string status, string? profile)
    {
        _status.Text = $"Status: {status}";
        _profile.Text = $"Profile: {profile ?? "None"}";
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
