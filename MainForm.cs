namespace ChaosInteractions;

public sealed class MainForm : Form
{
    private const int InvertHotkeyId = 1;
    private const int MuteHotkeyId = 2;
    private const int BlurHotkeyId = 3;
    private readonly KeyboardRemapper remapper;
    private readonly AudioMuteController muteController;
    private readonly ScreenBlurController blurController;
    private readonly NotifyIcon trayIcon;
    private readonly Label statusLabel;
    private readonly Label blurNoticeLabel;
    private readonly Label blurStrengthLabel;
    private readonly TrackBar blurStrengthSlider;
    private readonly CheckBox scrambleCheckBox;
    private readonly Button toggleButton;
    private readonly Button muteToggleButton;
    private readonly Button blurToggleButton;
    private readonly Button exitButton;
    private readonly IntPtr invertHotkeyHandle;
    private readonly IntPtr muteHotkeyHandle;
    private readonly IntPtr blurHotkeyHandle;
    private readonly ChaosApiServer apiServer;
    private bool isExiting;
    private string? lastInjectionError;
    private string? lastAudioError;
    private string? lastVisualError;

    public MainForm()
    {
        Text = "Chaos Interactions";
        ClientSize = new Size(760, 340);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        statusLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 132,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(16, 16, 16, 0)
        };

        blurNoticeLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(16, 0, 16, 0),
            ForeColor = Color.RoyalBlue,
            Visible = false,
            Text = "Blur unsupported on this system. Using a light tint fallback."
        };

        blurStrengthLabel = new Label
        {
            AutoSize = false,
            Location = new Point(16, 138),
            Size = new Size(260, 22),
            TextAlign = ContentAlignment.MiddleLeft
        };

        blurStrengthSlider = new TrackBar
        {
            Location = new Point(276, 132),
            Size = new Size(430, 45),
            Minimum = 0,
            Maximum = 100,
            TickFrequency = 10,
            SmallChange = 1,
            LargeChange = 5,
            Value = 70
        };
        blurStrengthSlider.Scroll += (_, _) => UpdateBlurStrengthFromSlider();

        scrambleCheckBox = new CheckBox
        {
            AutoSize = true,
            Location = new Point(16, 166),
            Text = "Scramble inputs instead of invert"
        };
        scrambleCheckBox.CheckedChanged += (_, _) => UpdateRemapModeFromCheckbox();

        toggleButton = new Button
        {
            Text = "Enable inversion",
            AutoSize = true,
            Location = new Point(16, 220)
        };
        toggleButton.Click += (_, _) => ToggleRemapper();

        muteToggleButton = new Button
        {
            Text = "Mute system audio",
            AutoSize = true,
            Location = new Point(170, 220)
        };
        muteToggleButton.Click += (_, _) => ToggleSystemMute();

        blurToggleButton = new Button
        {
            Text = "Enable blur filter",
            AutoSize = true,
            Location = new Point(350, 220)
        };
        blurToggleButton.Click += (_, _) => ToggleBlurFilter();

        exitButton = new Button
        {
            Text = "Exit",
            AutoSize = true,
            Location = new Point(670, 220)
        };
        exitButton.Click += (_, _) => ExitApplication();

        Controls.Add(statusLabel);
        Controls.Add(blurNoticeLabel);
        Controls.Add(blurStrengthLabel);
        Controls.Add(blurStrengthSlider);
        Controls.Add(scrambleCheckBox);
        Controls.Add(toggleButton);
        Controls.Add(muteToggleButton);
        Controls.Add(blurToggleButton);
        Controls.Add(exitButton);

        remapper = new KeyboardRemapper();
        muteController = new AudioMuteController();
        blurController = new ScreenBlurController();
        blurStrengthSlider.Value = (int)Math.Round(blurController.BlurStrength * 100f);
        scrambleCheckBox.Checked = remapper.IsScrambleMode;
        remapper.StateChanged += (_, _) => RefreshUi();
        remapper.InjectionFailed += (_, message) =>
        {
            lastInjectionError = message;
            RefreshUi();
        };
        remapper.Start();

        trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "Chaos Interactions"
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Show", null, (_, _) => ShowWindow());
        menu.Items.Add("Toggle inversion", null, (_, _) => ToggleRemapper());
        menu.Items.Add("Toggle system mute", null, (_, _) => ToggleSystemMute());
        menu.Items.Add("Toggle blur filter", null, (_, _) => ToggleBlurFilter());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());
        trayIcon.ContextMenuStrip = menu;
        trayIcon.DoubleClick += (_, _) => ShowWindow();

        invertHotkeyHandle = (IntPtr)InvertHotkeyId;
        muteHotkeyHandle = (IntPtr)MuteHotkeyId;
        blurHotkeyHandle = (IntPtr)BlurHotkeyId;
        apiServer = new ChaosApiServer(this);
        Shown += (_, _) =>
        {
            Hide();
            ShowInTaskbar = false;
            RegisterHotkey();
        };

        RefreshUi();
    }

    protected override void WndProc(ref Message message)
    {
        const int WM_HOTKEY = 0x0312;

        if (message.Msg == WM_HOTKEY && message.WParam == invertHotkeyHandle)
        {
            ToggleRemapper();
        }

        if (message.Msg == WM_HOTKEY && message.WParam == muteHotkeyHandle)
        {
            ToggleSystemMute();
        }

        if (message.Msg == WM_HOTKEY && message.WParam == blurHotkeyHandle)
        {
            ToggleBlurFilter();
        }

        base.WndProc(ref message);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!isExiting)
        {
            e.Cancel = true;
            Hide();
            ShowInTaskbar = false;
            return;
        }

        UnregisterHotkey();
        apiServer.Dispose();
        blurController.Dispose();
        trayIcon.Visible = false;
        trayIcon.Dispose();
        remapper.Dispose();
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            apiServer.Dispose();
            trayIcon.Dispose();
            remapper.Dispose();
            muteController.Dispose();
            blurController.Dispose();
        }

        base.Dispose(disposing);
    }

    private void ToggleRemapper()
    {
        if (remapper.IsEnabled)
        {
            remapper.Disable();
        }
        else
        {
            remapper.Enable();
        }
    }

    public bool IsRemapperEnabled => remapper.IsEnabled;
    public bool IsScrambleMode => remapper.IsScrambleMode;
    public bool IsMuted => muteController.IsMuted;
    public bool IsBlurEnabled => blurController.IsEnabled;
    public float BlurStrength => blurController.BlurStrength;

    public void InvokeAppAction(Action action)
    {
        if (InvokeRequired)
        {
            Invoke(action);
            return;
        }

        action();
    }

    public void ToggleRemapperFromApi() => ToggleRemapper();

    public void ToggleSystemMuteFromApi() => ToggleSystemMute();

    public void ToggleBlurFilterFromApi() => ToggleBlurFilter();

    public void SetScrambleModeFromApi(bool enabled)
    {
        remapper.SetScrambleMode(enabled);
        RefreshUi();
    }

    public void SetBlurStrengthFromApi(float strength)
    {
        blurStrengthSlider.Value = (int)Math.Round(Math.Clamp(strength, 0f, 1f) * 100f);
        UpdateBlurStrengthFromSlider();
    }

    private void ShowWindow()
    {
        ShowInTaskbar = true;
        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }

        Show();
        Activate();
    }

    private void RefreshUi()
    {
        var state = remapper.IsEnabled ? "Enabled" : "Disabled";
        var muteState = muteController.IsMuted ? "Muted" : "Unmuted";
        var blurState = blurController.IsEnabled ? "Enabled" : "Disabled";
        var blurPercent = (int)Math.Round(blurController.BlurStrength * 100f);
        var remapMode = remapper.IsScrambleMode ? "Scramble" : "Invert";
        statusLabel.Text =
            "Chaos Interactions swaps W with S and A with D while it is enabled.\r\n" +
            "Use tray menu/buttons/hotkeys: Ctrl+Shift+I (invert), Ctrl+Shift+M (mute), Ctrl+Shift+B (blur).\r\n\r\n" +
            $"WASD inversion: {state}\r\n" +
            $"WASD mode: {remapMode}\r\n" +
            $"System audio: {muteState}\r\n" +
            $"Blur filter: {blurState} ({blurPercent}%)" +
            (lastInjectionError is null ? string.Empty : $"\r\nLast input error: {lastInjectionError}") +
            (lastAudioError is null ? string.Empty : $"\r\nLast audio error: {lastAudioError}") +
            (lastVisualError is null ? string.Empty : $"\r\nLast visual error: {lastVisualError}");
        blurStrengthLabel.Text = $"Blur strength: {blurPercent}%";
        blurNoticeLabel.Visible = blurController.IsEnabled && blurController.LastEnableUsedFallbackTint;
        if (blurNoticeLabel.Visible)
        {
            blurNoticeLabel.BringToFront();
        }
        scrambleCheckBox.Checked = remapper.IsScrambleMode;
        toggleButton.Text = remapper.IsEnabled ? "Disable inversion" : "Enable inversion";
        muteToggleButton.Text = muteController.IsMuted ? "Unmute system audio" : "Mute system audio";
        blurToggleButton.Text = blurController.IsEnabled ? "Disable blur filter" : "Enable blur filter";
        trayIcon.Text = $"Chaos Interactions - {state}";
    }

    private void RegisterHotkey()
    {
        const uint MOD_CONTROL = 0x0002;
        const uint MOD_SHIFT = 0x0004;
        const uint VK_I = 0x49;
        const uint VK_M = 0x4D;
        const uint VK_B = 0x42;

        NativeMethods.RegisterHotKey(Handle, InvertHotkeyId, MOD_CONTROL | MOD_SHIFT, VK_I);
        NativeMethods.RegisterHotKey(Handle, MuteHotkeyId, MOD_CONTROL | MOD_SHIFT, VK_M);
        NativeMethods.RegisterHotKey(Handle, BlurHotkeyId, MOD_CONTROL | MOD_SHIFT, VK_B);
    }

    private void UnregisterHotkey()
    {
        NativeMethods.UnregisterHotKey(Handle, InvertHotkeyId);
        NativeMethods.UnregisterHotKey(Handle, MuteHotkeyId);
        NativeMethods.UnregisterHotKey(Handle, BlurHotkeyId);
    }

    private void ToggleSystemMute()
    {
        try
        {
            muteController.ToggleMute();
            lastAudioError = null;
        }
        catch (Exception ex)
        {
            lastAudioError = ex.Message;
        }

        RefreshUi();
    }

    private void ToggleBlurFilter()
    {
        try
        {
            blurController.Toggle();
            lastVisualError = null;
        }
        catch (Exception ex)
        {
            lastVisualError = ex.Message;
        }

        RefreshUi();
    }

    private void UpdateBlurStrengthFromSlider()
    {
        try
        {
            var strength = blurStrengthSlider.Value / 100f;
            blurController.SetStrength(strength);
            lastVisualError = null;
        }
        catch (Exception ex)
        {
            lastVisualError = ex.Message;
        }

        RefreshUi();
    }

    private void UpdateRemapModeFromCheckbox()
    {
        try
        {
            remapper.SetScrambleMode(scrambleCheckBox.Checked);
            lastInjectionError = null;
        }
        catch (Exception ex)
        {
            lastInjectionError = ex.Message;
        }

        RefreshUi();
    }

    public void ExitApplication()
    {
        isExiting = true;
        Close();
    }
}