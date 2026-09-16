using AudioUnitHost;
using Xunit;

namespace AudioUnitHost.Tests;

public sealed class WavWriterTests
{
    [Fact]
    public void WritesPcmWaveHeaderAndClampsSamples()
    {
        var path = Path.Combine(Path.GetTempPath(), $"audio-unit-{Guid.NewGuid():N}.wav");
        try
        {
            WavWriter.WritePcm16(path, [-2f, 0f, 2f, 0f], 48_000, 2);
            var bytes = File.ReadAllBytes(path);
            Assert.Equal("RIFF"u8.ToArray(), bytes[..4]);
            Assert.Equal("WAVE"u8.ToArray(), bytes[8..12]);
            Assert.Equal(44 + 8, bytes.Length);
            Assert.Equal(0x00, bytes[44]);
            Assert.Equal(0x80, bytes[45]);
            Assert.Equal(0xFF, bytes[48]);
            Assert.Equal(0x7F, bytes[49]);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void RejectsNonInterleavedSampleCount()
    {
        Assert.Throws<ArgumentException>(() => WavWriter.WritePcm16("unused.wav", [0f], 48_000, 2));
    }
}
