# AI Audio Noise Reduction

Windows local AI noise-reduction tool PoC.

## Current Scope

This repository is intentionally scoped to a first-phase PoC:

- Enumerate Windows recording/rendering devices.
- Capture a selected physical microphone.
- Render processed audio to a virtual audio cable input device.
- Keep the denoise processor behind an interface so Agora SDK integration can be verified without rewriting the audio pipeline.

The first runnable implementation uses a pass-through denoise processor. Agora integration should only replace `IDenoiseProcessor` after SDK behavior is verified.

## Required Local Setup

Install a .NET SDK before building. The current PoC targets `net10.0-windows` because this machine has the .NET 10 SDK installed:

https://dotnet.microsoft.com/download

The first restore also needs NuGet access to download `NAudio`.

After installing:

```powershell
dotnet restore .\src\NoiseReduction.App\NoiseReduction.App.csproj
dotnet build .\src\NoiseReduction.App\NoiseReduction.App.csproj
dotnet run --project .\src\NoiseReduction.App\NoiseReduction.App.csproj
```

For virtual microphone routing, install either VB-CABLE or Virtual Audio Cable for PoC testing.

## Implementation Notes

- `NoiseReduction.Core` contains device, audio-frame, denoise, and pipeline contracts.
- `NoiseReduction.Infrastructure` contains the first WASAPI/NAudio implementation.
- `NoiseReduction.App` contains the WPF PoC UI.
- The current processor is intentionally pass-through. Do not add Agora SDK until the local virtual-cable route is stable.
- The first Agora milestone is a separate `IDenoiseProcessor` implementation that can prove before/after PCM capture.

## Architecture

```text
Physical microphone
  -> WASAPI capture
  -> IDenoiseProcessor
  -> WASAPI render to virtual cable input
  -> Virtual microphone output
  -> Downstream apps
```

## First Validation Target

Use the app to select:

- Input: physical microphone.
- Output: virtual cable input/render device.

Then select the matching virtual cable recording device in OBS, Discord, or Windows Sound Recorder.
