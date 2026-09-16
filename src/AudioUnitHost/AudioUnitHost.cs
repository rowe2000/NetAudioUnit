using System.Runtime.InteropServices;

namespace AudioUnitHost;

public sealed record AudioUnitInfo(string Type, string Subtype, string Manufacturer, string Name);

public sealed class AudioUnitException(string message, int status) : Exception($"{message} (OSStatus {status})")
{
    public int Status { get; } = status;
}

public static class AudioUnitCatalog
{
    public static IReadOnlyList<AudioUnitInfo> FindAll()
    {
        var description = new Native.AudioComponentDescription();
        var result = new List<AudioUnitInfo>();
        nint component = 0;
        while ((component = Native.AudioComponentFindNext(component, ref description)) != 0)
        {
            result.Add(new AudioUnitInfo(
                Native.FourCC(description.ComponentType),
                Native.FourCC(description.ComponentSubType),
                Native.FourCC(description.ComponentManufacturer),
                Native.GetComponentName(component)));
        }
        return result;
    }

    internal static nint Find(Native.AudioComponentDescription description)
    {
        var component = Native.AudioComponentFindNext(0, ref description);
        if (component == 0)
            throw new InvalidOperationException($"AudioUnit {Native.FourCC(description.ComponentType)}:{Native.FourCC(description.ComponentSubType)}:{Native.FourCC(description.ComponentManufacturer)} was not found.");
        return component;
    }
}

public sealed class AudioUnitInstance : IDisposable
{
    private nint handle;
    private bool initialized;

    private AudioUnitInstance(nint handle) => this.handle = handle;

    public static AudioUnitInstance Open(string identifier)
    {
        var parts = identifier.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length != 3)
            throw new ArgumentException("Component identifier must be type:subtype:manufacturer, for example aufx:aufm:appl.");

        var description = new Native.AudioComponentDescription
        {
            ComponentType = Native.FourCC(parts[0]),
            ComponentSubType = Native.FourCC(parts[1]),
            ComponentManufacturer = Native.FourCC(parts[2])
        };
        var component = AudioUnitCatalog.Find(description);
        var status = Native.AudioComponentInstanceNew(component, out var instance);
        Check(status, "AudioComponentInstanceNew");
        return new AudioUnitInstance(instance);
    }

    public void ConfigureStereo(double sampleRate)
    {
        var format = new Native.AudioStreamBasicDescription
        {
            SampleRate = sampleRate,
            FormatId = Native.AudioFormatLinearPcm,
            FormatFlags = Native.AudioFormatFlagIsFloat | Native.AudioFormatFlagIsPacked | Native.AudioFormatFlagNativeEndian,
            BytesPerPacket = 8,
            FramesPerPacket = 1,
            BytesPerFrame = 8,
            ChannelsPerFrame = 2,
            BitsPerChannel = 32
        };
        var size = (uint)Marshal.SizeOf<Native.AudioStreamBasicDescription>();
        var memory = Marshal.AllocHGlobal((int)size);
        try
        {
            Marshal.StructureToPtr(format, memory, false);
            Check(Native.AudioUnitSetProperty(handle, Native.PropertyStreamFormat, Native.ScopeInput, 0, memory, size), "AudioUnitSetProperty(input stream format)");
            Check(Native.AudioUnitSetProperty(handle, Native.PropertyStreamFormat, Native.ScopeOutput, 0, memory, size), "AudioUnitSetProperty(output stream format)");
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
    }

    public void Initialize()
    {
        Check(Native.AudioUnitInitialize(handle), "AudioUnitInitialize");
        initialized = true;
    }

    public void Render(float[] input, float[] output, int frames)
    {
        if (!initialized)
            throw new InvalidOperationException("The AudioUnit must be initialized before rendering.");
        if (input.Length < frames * 2 || output.Length < frames * 2)
            throw new ArgumentException("Stereo buffers are smaller than the requested frame count.");

        using var inputMemory = new UnmanagedFloatBuffer(input);
        using var outputMemory = new UnmanagedFloatBuffer(frames * 2);
        var inputList = new Native.AudioBufferList
        {
            NumberBuffers = 1,
            Buffer = new Native.AudioBuffer { NumberChannels = 2, DataByteSize = (uint)(frames * 8), Data = inputMemory.Pointer }
        };
        var outputList = new Native.AudioBufferList
        {
            NumberBuffers = 1,
            Buffer = new Native.AudioBuffer { NumberChannels = 2, DataByteSize = (uint)(frames * 8), Data = outputMemory.Pointer }
        };
        var inputListMemory = Marshal.AllocHGlobal(Marshal.SizeOf<Native.AudioBufferList>());
        var outputListMemory = Marshal.AllocHGlobal(Marshal.SizeOf<Native.AudioBufferList>());
        try
        {
            Marshal.StructureToPtr(inputList, inputListMemory, false);
            Marshal.StructureToPtr(outputList, outputListMemory, false);
            var state = new RenderState(inputMemory.Pointer);
            var callback = new Native.AudioUnitRenderDelegate(state.Fill);
            var callbackDescription = new Native.AudioUnitRenderCallbackStruct
            {
                InputProc = Marshal.GetFunctionPointerForDelegate(callback),
                InputProcRefCon = 0
            };
            var callbackMemory = Marshal.AllocHGlobal(Marshal.SizeOf<Native.AudioUnitRenderCallbackStruct>());
            try
            {
                Marshal.StructureToPtr(callbackDescription, callbackMemory, false);
                Check(Native.AudioUnitSetProperty(handle, 23, Native.ScopeInput, 0, callbackMemory,
                    (uint)Marshal.SizeOf<Native.AudioUnitRenderCallbackStruct>()), "AudioUnitSetProperty(input callback)");
            }
            finally
            {
                Marshal.FreeHGlobal(callbackMemory);
            }
            var flags = 0u;
            var timestamp = new Native.AudioTimeStamp { SampleTime = 0, RateScalar = 1 };
            Check(Native.AudioUnitRender(handle, ref flags, ref timestamp, 0, (uint)frames, outputListMemory), "AudioUnitRender");
            Marshal.Copy(outputMemory.Pointer, output, 0, frames * 2);
            GC.KeepAlive(callback);
        }
        finally
        {
            Marshal.FreeHGlobal(inputListMemory);
            Marshal.FreeHGlobal(outputListMemory);
        }
    }

    public void Dispose()
    {
        if (handle == 0)
            return;
        if (initialized)
            Native.AudioUnitUninitialize(handle);
        Native.AudioComponentInstanceDispose(handle);
        handle = 0;
    }

    private static void Check(int status, string operation)
    {
        if (status != 0)
            throw new AudioUnitException(operation + " failed", status);
    }

    private sealed class RenderState(nint inputPointer)
    {
        public unsafe int Fill(nint _, ref uint __, ref Native.AudioTimeStamp ___, uint ____, uint numberFrames, nint ioData)
        {
            var outputPointer = Marshal.ReadIntPtr(ioData, 16);
            var bytes = checked((nuint)numberFrames * 2u * sizeof(float));
            Buffer.MemoryCopy((void*)inputPointer, (void*)outputPointer, bytes, bytes);
            return 0;
        }
    }

    private sealed class UnmanagedFloatBuffer : IDisposable
    {
        public nint Pointer { get; }
        public UnmanagedFloatBuffer(float[] values) : this(values.Length) => Marshal.Copy(values, 0, Pointer, values.Length);
        public UnmanagedFloatBuffer(int length) => Pointer = Marshal.AllocHGlobal(checked(length * sizeof(float)));
        public void Dispose() => Marshal.FreeHGlobal(Pointer);
    }
}
