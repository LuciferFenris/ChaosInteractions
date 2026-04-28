using System.Runtime.InteropServices;

namespace ChaosInteractions;

public sealed class KeyboardRemapper : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int HC_ACTION = 0;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const uint LLKHF_INJECTED = 0x00000010;
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_SCANCODE = 0x0008;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

    private readonly NativeMethods.LowLevelKeyboardProc hookCallback;
    private readonly IntPtr hookHandle;
    private readonly object syncRoot = new();
    private readonly Random random = new();
    private bool isEnabled;
    private bool isScrambleMode;
    private Dictionary<uint, ushort> currentMapping = CreateInvertMapping();
    private bool disposed;

    public KeyboardRemapper()
    {
        hookCallback = HookCallback;
        hookHandle = NativeMethods.SetWindowsHookEx(WH_KEYBOARD_LL, hookCallback, IntPtr.Zero, 0);
    }

    public bool IsEnabled
    {
        get
        {
            lock (syncRoot)
            {
                return isEnabled;
            }
        }
    }

    public bool IsScrambleMode
    {
        get
        {
            lock (syncRoot)
            {
                return isScrambleMode;
            }
        }
    }

    public event EventHandler? StateChanged;
    public event EventHandler<string>? InjectionFailed;

    public void Start()
    {
        if (hookHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to install the keyboard hook.");
        }
    }

    public void Enable()
    {
        SetEnabled(true);
    }

    public void Disable()
    {
        SetEnabled(false);
    }

    public void SetScrambleMode(bool enabled)
    {
        lock (syncRoot)
        {
            if (isScrambleMode == enabled)
            {
                return;
            }

            isScrambleMode = enabled;
            currentMapping = enabled ? CreateScrambleMapping(random) : CreateInvertMapping();
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(hookHandle);
        }
    }

    private void SetEnabled(bool value)
    {
        var changed = false;
        lock (syncRoot)
        {
            if (isEnabled != value)
            {
                isEnabled = value;
                if (isEnabled && isScrambleMode)
                {
                    currentMapping = CreateScrambleMapping(random);
                }
                changed = true;
            }
        }

        if (changed)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < HC_ACTION)
        {
            return NativeMethods.CallNextHookEx(hookHandle, nCode, wParam, lParam);
        }

        var keyboardData = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
        if ((keyboardData.Flags & LLKHF_INJECTED) != 0)
        {
            return NativeMethods.CallNextHookEx(hookHandle, nCode, wParam, lParam);
        }

        if (keyboardData.dwExtraInfo == NativeMethods.InjectedMarker)
        {
            return NativeMethods.CallNextHookEx(hookHandle, nCode, wParam, lParam);
        }

        var message = wParam.ToInt32();
        if (message is not (WM_KEYDOWN or WM_KEYUP or WM_SYSKEYDOWN or WM_SYSKEYUP))
        {
            return NativeMethods.CallNextHookEx(hookHandle, nCode, wParam, lParam);
        }

        if (!IsEnabled)
        {
            return NativeMethods.CallNextHookEx(hookHandle, nCode, wParam, lParam);
        }

        if (!TryMapKey((uint)keyboardData.vkCode, out var mappedKey))
        {
            return NativeMethods.CallNextHookEx(hookHandle, nCode, wParam, lParam);
        }

        var isKeyUp = message is WM_KEYUP or WM_SYSKEYUP;
        InjectKey(mappedKey, isKeyUp);
        return (IntPtr)1;
    }

    private bool TryMapKey(uint vkCode, out ushort mappedKey)
    {
        lock (syncRoot)
        {
            return currentMapping.TryGetValue(vkCode, out mappedKey);
        }
    }

    private static Dictionary<uint, ushort> CreateInvertMapping()
    {
        return new Dictionary<uint, ushort>
        {
            [0x57] = 0x53,
            [0x53] = 0x57,
            [0x41] = 0x44,
            [0x44] = 0x41
        };
    }

    private static Dictionary<uint, ushort> CreateScrambleMapping(Random random)
    {
        var inputs = new[] { 0x57u, 0x41u, 0x53u, 0x44u };
        var outputs = new List<uint> { 0x57u, 0x41u, 0x53u, 0x44u };

        Shuffle(outputs, random);

        var mapping = new Dictionary<uint, ushort>(4);
        for (var i = 0; i < inputs.Length; i++)
        {
            mapping[inputs[i]] = (ushort)outputs[i];
        }

        return mapping;
    }

    private static void Shuffle(IList<uint> values, Random random)
    {
        for (var i = values.Count - 1; i > 0; i--)
        {
            var swapIndex = random.Next(i + 1);
            (values[i], values[swapIndex]) = (values[swapIndex], values[i]);
        }
    }

    private void InjectKey(ushort vkCode, bool keyUp)
    {
        var scanCode = (ushort)NativeMethods.MapVirtualKey(vkCode, 0);
        var flags = KEYEVENTF_SCANCODE | (keyUp ? KEYEVENTF_KEYUP : 0);
        var input = new NativeMethods.INPUT
        {
            type = INPUT_KEYBOARD,
            Anonymous = new NativeMethods.INPUTUNION
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = 0,
                    wScan = scanCode,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = NativeMethods.InjectedMarker
                }
            }
        };

        var sent = NativeMethods.SendInput(1, [input], Marshal.SizeOf<NativeMethods.INPUT>());
        if (sent == 0)
        {
            var error = Marshal.GetLastWin32Error();
            var message = $"SendInput failed for vk=0x{vkCode:X2}, keyUp={keyUp}, error={error}";
            System.Diagnostics.Debug.WriteLine(message);
            InjectionFailed?.Invoke(this, message);
            return;
        }
    }
}