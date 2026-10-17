namespace RetroAnimalPuzzle.Host;

/// <summary>Main window: hosts the drawing surface and handles window-level concerns such as fullscreen.</summary>
internal sealed class GameForm : Form
{
    private FormWindowState _windowedState = FormWindowState.Normal;
    private Rectangle _windowedBounds;

    public GameForm()
    {
        Text = "Retro Animal Puzzle";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1024, 768);
        MinimumSize = new Size(420, 340);
        BackColor = Color.Black;

        // Title bar / taskbar use the same bamboo icon that is embedded in the executable.
        if (Environment.ProcessPath is { } exe && Icon.ExtractAssociatedIcon(exe) is { } icon)
        {
            Icon = icon;
        }

        Surface = new GameSurface();
        Controls.Add(Surface);
    }

    public GameSurface Surface { get; }

    public bool IsFullscreen { get; private set; }

    public void ToggleFullscreen()
    {
        SuspendLayout();
        if (IsFullscreen)
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            WindowState = FormWindowState.Normal;
            Bounds = _windowedBounds;
            WindowState = _windowedState;
        }
        else
        {
            _windowedState = WindowState;
            _windowedBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            WindowState = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
        }

        IsFullscreen = !IsFullscreen;
        ResumeLayout();
        Surface.Focus();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Surface.Focus();
    }
}
