namespace AudioUnitHost;

internal static class Program
{
    private const int DefaultSampleRate = 48_000;
    private const int DefaultSeconds = 1;
    private const double Frequency = 440;

    public static int Main(string[] args)
    {
        try
        {
            if (args.Contains("--help") || args.Length == 0)
            {
                PrintUsage();
                return args.Length == 0 ? 1 : 0;
            }
            if (args.Contains("--list"))
            {
                foreach (var unit in AudioUnitCatalog.FindAll())
                    Console.WriteLine($"{unit.Type}:{unit.Subtype}:{unit.Manufacturer}  {unit.Name}");
                return 0;
            }

            var component = Required(args, "--component");
            var outputPath = Value(args, "--output") ?? "output.wav";
            var sampleRate = int.TryParse(Value(args, "--sample-rate"), out var rate) ? rate : DefaultSampleRate;
            var seconds = double.TryParse(Value(args, "--seconds"), out var duration) ? duration : DefaultSeconds;
            var frames = checked((int)Math.Round(sampleRate * seconds));
            if (frames <= 0)
                throw new ArgumentException("Duration must produce at least one frame.");

            var input = new float[frames * 2];
            for (var frame = 0; frame < frames; frame++)
            {
                var sample = (float)Math.Sin(2 * Math.PI * Frequency * frame / sampleRate) * 0.2f;
                input[frame * 2] = sample;
                input[frame * 2 + 1] = sample;
            }
            var output = new float[input.Length];
            using var audioUnit = AudioUnitInstance.Open(component);
            audioUnit.ConfigureStereo(sampleRate);
            audioUnit.Initialize();
            audioUnit.Render(input, output, frames);
            WavWriter.WritePcm16(outputPath, output, sampleRate, 2);
            Console.WriteLine($"Rendered {frames} stereo frames through {component} to {Path.GetFullPath(outputPath)}.");
            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or AudioUnitException or IOException)
        {
            Console.Error.WriteLine($"error: {exception.Message}");
            return 2;
        }
    }

    private static string Required(string[] args, string name) => Value(args, name) ?? throw new ArgumentException($"{name} is required.");
    private static string? Value(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
    private static void PrintUsage() => Console.WriteLine("""
        AudioUnitHost - render a generated stereo sine wave through an AudioUnit.

        Usage:
          audio-unit-host --list
          audio-unit-host --component <type:subtype:manufacturer> [--output file.wav] [--sample-rate 48000] [--seconds 1]

        Example:
          audio-unit-host --component aufx:aufm:appl --output effect.wav
        """);
}
