using System.Runtime.InteropServices;

namespace ChaosInteractions;

public sealed class AudioMuteController : IDisposable
{
    private const uint CLSCTX_INPROC_SERVER = 0x1;
    private const uint CLSCTX_INPROC_HANDLER = 0x2;
    private const uint CLSCTX_LOCAL_SERVER = 0x4;
    private const uint CLSCTX_REMOTE_SERVER = 0x10;
    private const uint CLSCTX_ALL = CLSCTX_INPROC_SERVER | CLSCTX_INPROC_HANDLER | CLSCTX_LOCAL_SERVER | CLSCTX_REMOTE_SERVER;

    private readonly IMMDeviceEnumerator deviceEnumerator;
    private readonly IMMDevice defaultDevice;
    private readonly IAudioEndpointVolume endpointVolume;
    private bool disposed;

    public AudioMuteController()
    {
        var enumeratorType = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"))
            ?? throw new InvalidOperationException("MMDeviceEnumerator COM type is unavailable.");

        deviceEnumerator = (IMMDeviceEnumerator)Activator.CreateInstance(enumeratorType)!;
        CheckResult(deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out defaultDevice));

        var endpointGuid = typeof(IAudioEndpointVolume).GUID;
        CheckResult(defaultDevice.Activate(ref endpointGuid, CLSCTX_ALL, IntPtr.Zero, out var endpointObject));
        endpointVolume = (IAudioEndpointVolume)endpointObject;
    }

    public bool IsMuted
    {
        get
        {
            ThrowIfDisposed();
            CheckResult(endpointVolume.GetMute(out var isMuted));
            return isMuted;
        }
    }

    public bool ToggleMute()
    {
        ThrowIfDisposed();
        var muted = !IsMuted;
        SetMute(muted);
        return muted;
    }

    public void SetMute(bool muted)
    {
        ThrowIfDisposed();
        CheckResult(endpointVolume.SetMute(muted, Guid.Empty));
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Marshal.ReleaseComObject(endpointVolume);
        Marshal.ReleaseComObject(defaultDevice);
        Marshal.ReleaseComObject(deviceEnumerator);
        GC.SuppressFinalize(this);
    }

    private static void CheckResult(int hr)
    {
        if (hr < 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(AudioMuteController));
        }
    }
}

internal enum EDataFlow
{
    eRender = 0,
    eCapture = 1,
    eAll = 2,
    EDataFlow_enum_count = 3
}

internal enum ERole
{
    eConsole = 0,
    eMultimedia = 1,
    eCommunications = 2,
    ERole_enum_count = 3
}

[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    int EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out IntPtr devices);
    int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
    int GetDevice(string id, out IMMDevice device);
    int RegisterEndpointNotificationCallback(IntPtr client);
    int UnregisterEndpointNotificationCallback(IntPtr client);
}

[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    int Activate(ref Guid iid, uint dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
    int OpenPropertyStore(int stgmAccess, out IntPtr properties);
    int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    int GetState(out uint state);
}

[ComImport]
[Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    int RegisterControlChangeNotify(IntPtr notify);
    int UnregisterControlChangeNotify(IntPtr notify);
    int GetChannelCount(out uint channelCount);
    int SetMasterVolumeLevel(float levelDb, Guid eventContext);
    int SetMasterVolumeLevelScalar(float level, Guid eventContext);
    int GetMasterVolumeLevel(out float levelDb);
    int GetMasterVolumeLevelScalar(out float level);
    int SetChannelVolumeLevel(uint channelNumber, float levelDb, Guid eventContext);
    int SetChannelVolumeLevelScalar(uint channelNumber, float level, Guid eventContext);
    int GetChannelVolumeLevel(uint channelNumber, out float levelDb);
    int GetChannelVolumeLevelScalar(uint channelNumber, out float level);
    int SetMute([MarshalAs(UnmanagedType.Bool)] bool isMuted, Guid eventContext);
    int GetMute([MarshalAs(UnmanagedType.Bool)] out bool isMuted);
    int GetVolumeStepInfo(out uint step, out uint stepCount);
    int VolumeStepUp(Guid eventContext);
    int VolumeStepDown(Guid eventContext);
    int QueryHardwareSupport(out uint hardwareSupportMask);
    int GetVolumeRange(out float minLevelDb, out float maxLevelDb, out float incrementDb);
}