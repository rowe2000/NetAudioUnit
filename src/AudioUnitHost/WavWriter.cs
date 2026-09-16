using System.Buffers.Binary;

namespace AudioUnitHost;

public static class WavWriter
{
    public static void WritePcm16(string path, ReadOnlySpan<float> samples, int sampleRate, short channels)
    {
        if (sampleRate <= 0 || channels <= 0 || samples.Length % channels != 0)
            throw new ArgumentException("Invalid sample rate, channel count, or sample buffer.");
        var dataBytes = checked(samples.Length * sizeof(short));
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8.ToArray());
        writer.Write(checked(36 + dataBytes));
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(checked(sampleRate * channels * sizeof(short)));
        writer.Write(checked((short)(channels * sizeof(short))));
        writer.Write((short)16);
        writer.Write("data"u8.ToArray());
        writer.Write(dataBytes);
        Span<byte> sample = stackalloc byte[2];
        foreach (var value in samples)
        {
            var clamped = Math.Clamp(value, -1f, 1f);
            var pcm = clamped <= -1 ? short.MinValue : (short)Math.Round(clamped * short.MaxValue);
            BinaryPrimitives.WriteInt16LittleEndian(sample, pcm);
            writer.Write(sample);
        }
    }
}
