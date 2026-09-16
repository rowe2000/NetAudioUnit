using System.Runtime.InteropServices;
using System.Text;

namespace AudioUnitHost;

internal static class Native
{
    internal const uint AudioUnitTypeOutput = 0x61756F75; // aufc
    internal const uint AudioUnitTypeMusicEffect = 0x61756678; // aufx
    internal const uint AudioUnitTypeEffect = 0x61756678; // aufx
    internal const uint AudioUnitTypeFormatConverter = 0x61756663; // aufc
    internal const uint AudioUnitSubTypeDefaultOutput = 0x64656674; // deft
    internal const uint AudioUnitSubTypeAudioUnitEffect = 0x61756566; // auef
    internal const uint AudioUnitManufacturerApple = 0x6170706C; // appl

    internal const uint PropertyClassInfo = 0;
    internal const uint PropertyStreamFormat = 8;
    internal const uint ScopeGlobal = 0;
    internal const uint ScopeInput = 1;
    internal const uint ScopeOutput = 2;
    internal const uint AudioFormatLinearPcm = 0x6C70636D;
    internal const uint AudioFormatFlagIsFloat = 1;
    internal const uint AudioFormatFlagIsPacked = 8;
    internal const uint AudioFormatFlagIsNonInterleaved = 32;
    internal const uint AudioFormatFlagNativeEndian = 0;

    [StructLayout(LayoutKind.Sequential)]
    internal struct AudioComponentDescription
    {
        public uint ComponentType;
        public uint ComponentSubType;
        public uint ComponentManufacturer;
        public uint ComponentFlags;
        public uint ComponentFlagsMask;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct AudioStreamBasicDescription
    {
        public double SampleRate;
        public uint FormatId;
        public uint FormatFlags;
        public uint BytesPerPacket;
        public uint FramesPerPacket;
        public uint BytesPerFrame;
        public uint ChannelsPerFrame;
        public uint BitsPerChannel;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct AudioBuffer
    {
        public uint NumberChannels;
        public uint DataByteSize;
        public nint Data;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct AudioTimeStamp
    {
        public double SampleTime;
        public ulong HostTime;
        public double RateScalar;
        public ulong WordClockTime;
        public uint SMPTETime;
        public uint Flags;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct AudioBufferList
    {
        public uint NumberBuffers;
        public AudioBuffer Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct AudioUnitRenderCallbackStruct
    {
        public nint InputProc;
        public nint InputProcRefCon;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int AudioUnitRenderDelegate(
        nint inRefCon,
        ref uint actionFlags,
        ref AudioTimeStamp timestamp,
        uint busNumber,
        uint numberFrames,
        nint ioData);

    [DllImport("/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox")]
    internal static extern nint AudioComponentFindNext(nint component, ref AudioComponentDescription description);

    [DllImport("/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox")]
    internal static extern int AudioComponentInstanceNew(nint component, out nint instance);

    [DllImport("/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox")]
    internal static extern int AudioComponentInstanceDispose(nint instance);

    [DllImport("/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox")]
    internal static extern int AudioUnitInitialize(nint audioUnit);

    [DllImport("/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox")]
    internal static extern int AudioUnitUninitialize(nint audioUnit);

    [DllImport("/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox")]
    internal static extern int AudioUnitSetProperty(
        nint audioUnit, uint propertyId, uint scope, uint element, nint data, uint dataSize);

    [DllImport("/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox")]
    internal static extern int AudioUnitRender(
        nint audioUnit, ref uint actionFlags, ref AudioTimeStamp timestamp,
        uint outputBusNumber, uint numberFrames, nint ioData);

    [DllImport("/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox")]
    internal static extern nint AudioComponentCopyName(nint component);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    internal static extern void CFRelease(nint cfTypeRef);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool CFStringGetCString(nint handle, byte[] buffer, nint bufferSize, uint encoding);

    internal const uint Utf8Encoding = 0x08000100;

    internal static string GetComponentName(nint component)
    {
        var name = AudioComponentCopyName(component);
        if (name == 0)
            return "(unnamed)";

        try
        {
            var buffer = new byte[1024];
            return CFStringGetCString(name, buffer, buffer.Length, Utf8Encoding)
                ? Encoding.UTF8.GetString(buffer, 0, Array.IndexOf(buffer, (byte)0) is var end && end >= 0 ? end : buffer.Length)
                : "(unnamed)";
        }
        finally
        {
            CFRelease(name);
        }
    }

    internal static uint FourCC(string value)
    {
        if (value.Length != 4)
            throw new ArgumentException($"'{value}' must contain exactly four ASCII characters.");
        return ((uint)value[0] << 24) | ((uint)value[1] << 16) | ((uint)value[2] << 8) | value[3];
    }

    internal static string FourCC(uint value)
    {
        var chars = new[] { (char)(value >> 24), (char)(value >> 16), (char)(value >> 8), (char)value };
        return new string(chars.Select(c => c is >= ' ' and <= '~' ? c : '?').ToArray());
    }
}
