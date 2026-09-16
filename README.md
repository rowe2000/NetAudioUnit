# NetAudioUnit

`audio-unit-host` is a small macOS command-line AudioUnit host implemented in C#/.NET. It discovers installed AudioUnits, selects one by its four-character component type/subtype/manufacturer identifier, configures a stereo 32-bit floating-point stream, renders a generated 440 Hz sine wave, and writes the result as a 16-bit PCM WAV file.

## Prerequisites

- macOS with Xcode or the Command Line Tools (AudioToolbox and CoreFoundation are system frameworks).
- .NET 9 SDK with the macOS workload. The project uses the modern .NET runtime rather than Xamarin/.NET for macOS UI APIs.
- An AudioUnit that accepts stereo linear PCM input. Components can reject offline rendering or require additional properties; those components are outside this host's scope.

## Build and test

```sh
dotnet build
dotnet test
```

## Run

List installed components:

```sh
dotnet run --project src/AudioUnitHost -- --list
```

Render one second through a selected component:

```sh
dotnet run --project src/AudioUnitHost -- \
  --component aufx:aufm:appl --output effect.wav --sample-rate 48000 --seconds 1
```

The identifier is `type:subtype:manufacturer`, using the same four-character codes displayed by `--list`. The output path is created or overwritten.

## Limitations

- This is an offline, single-threaded render example, not a real-time audio engine. It does not open an audio device or provide a GUI.
- The host supplies one interleaved stereo buffer and expects the AudioUnit to process it in place through `AudioUnitRender`.
- AudioUnit-specific initialization, parameters, presets, MIDI, latency compensation, non-stereo layouts, and sandbox/entitlement setup are not implemented.
- Native execution is macOS-only. WAV writing and other managed logic are covered by tests; tests do not instantiate system AudioUnits.
