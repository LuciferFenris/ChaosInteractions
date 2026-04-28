namespace ChaosInteractions;

public sealed class ScreenBlurController : IDisposable
{
    private readonly List<BlurOverlayForm> overlays = new();
    private bool disposed;
    private float blurStrength = 0.70f;

    public bool IsEnabled => overlays.Count > 0;
    public bool LastEnableUsedFallbackTint { get; private set; }
    public float BlurStrength => blurStrength;

    public void Toggle()
    {
        if (IsEnabled)
        {
            Disable();
        }
        else
        {
            Enable();
        }
    }

    public void Enable()
    {
        ThrowIfDisposed();
        if (IsEnabled)
        {
            return;
        }

        foreach (var screen in Screen.AllScreens)
        {
            var overlay = new BlurOverlayForm(screen.Bounds);
            overlays.Add(overlay);
            overlay.Show();
        }

        ApplyCurrentStrength();
    }

    public void Disable()
    {
        foreach (var overlay in overlays)
        {
            if (!overlay.IsDisposed)
            {
                overlay.Hide();
                overlay.Close();
                overlay.Dispose();
            }
        }

        overlays.Clear();
        LastEnableUsedFallbackTint = false;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        Disable();
        disposed = true;
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(ScreenBlurController));
        }
    }

    public void SetStrength(float strength)
    {
        ThrowIfDisposed();
        blurStrength = Math.Clamp(strength, 0f, 1f);

        if (IsEnabled)
        {
            ApplyCurrentStrength();
        }
    }

    private void ApplyCurrentStrength()
    {
        var anyBlurApplied = false;
        var anyFallbackApplied = false;

        foreach (var overlay in overlays)
        {
            if (overlay.IsDisposed)
            {
                continue;
            }

            var mode = overlay.ApplyVisualEffect(blurStrength);
            anyBlurApplied |= mode == OverlayVisualMode.Blur;
            anyFallbackApplied |= mode == OverlayVisualMode.FallbackTint;
        }

        LastEnableUsedFallbackTint = anyFallbackApplied || !anyBlurApplied;
    }
}

internal enum OverlayVisualMode
{
    Blur,
    FallbackTint
}

internal sealed class BlurOverlayForm : Form
{
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    public BlurOverlayForm(Rectangle bounds)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Black;
        Opacity = 0.08;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var createParams = base.CreateParams;
            createParams.ExStyle |= WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_LAYERED | WS_EX_NOACTIVATE;
            return createParams;
        }
    }

    public OverlayVisualMode ApplyVisualEffect(float strength)
    {
        var clamped = Math.Clamp(strength, 0f, 1f);

        if (!BlurNativeMethods.TryEnableBlur(Handle, clamped))
        {
            // Fallback is intentionally visible but light so the toggle still has feedback.
            Opacity = 0.06 + (0.16 * clamped);
            return OverlayVisualMode.FallbackTint;
        }

        // Scale overlay blend with slider strength for live control.
        Opacity = 0.06 + (0.40 * clamped);
        return OverlayVisualMode.Blur;
    }
}