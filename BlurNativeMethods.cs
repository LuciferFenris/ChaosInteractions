using System.Runtime.InteropServices;

namespace ChaosInteractions;

internal static class BlurNativeMethods
{
    private const int WCA_ACCENT_POLICY = 19;
    private const int ACCENT_ENABLE_BLURBEHIND = 3;
    private const int ACCENT_ENABLE_ACRYLICBLURBEHIND = 4;

    [StructLayout(LayoutKind.Sequential)]
    private struct ACCENT_POLICY
    {
        public int AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWCOMPOSITIONATTRIBDATA
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WINDOWCOMPOSITIONATTRIBDATA data);

    internal static bool TryEnableBlur(IntPtr handle, float strength)
    {
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        var clamped = Math.Clamp(strength, 0f, 1f);
        var acrylicAlpha = 0x18 + (int)(0x70 * clamped);
        var blurAlpha = 0x10 + (int)(0x60 * clamped);
        var acrylicGradient = unchecked((int)((uint)acrylicAlpha << 24));
        var blurGradient = unchecked((int)((uint)blurAlpha << 24));

        if (TrySetAccent(handle, ACCENT_ENABLE_ACRYLICBLURBEHIND, acrylicGradient))
        {
            return true;
        }

        return TrySetAccent(handle, ACCENT_ENABLE_BLURBEHIND, blurGradient);
    }

    private static bool TrySetAccent(IntPtr handle, int state, int gradientColor)
    {
        var accent = new ACCENT_POLICY
        {
            AccentState = state,
            AccentFlags = 2,
            GradientColor = gradientColor,
            AnimationId = 0
        };

        var accentSize = Marshal.SizeOf<ACCENT_POLICY>();
        var accentPointer = Marshal.AllocHGlobal(accentSize);

        try
        {
            Marshal.StructureToPtr(accent, accentPointer, false);
            var data = new WINDOWCOMPOSITIONATTRIBDATA
            {
                Attribute = WCA_ACCENT_POLICY,
                Data = accentPointer,
                SizeOfData = accentSize
            };

            return SetWindowCompositionAttribute(handle, ref data) != 0;
        }
        finally
        {
            Marshal.FreeHGlobal(accentPointer);
        }
    }
}