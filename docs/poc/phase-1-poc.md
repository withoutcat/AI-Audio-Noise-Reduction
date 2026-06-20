# Phase 1 PoC Plan

## Goal

Verify the lowest-risk end-to-end audio route before integrating Agora AI noise reduction:

```text
Physical microphone -> app capture -> processor boundary -> virtual cable input -> downstream app
```

## Manual Prerequisites

1. Install .NET 8 SDK.
2. Install VB-CABLE or Virtual Audio Cable.
3. Confirm Windows Settings shows both:
   - A playback/render endpoint for the cable input.
   - A recording/capture endpoint for the cable output.

## Test Steps

1. Launch the WPF app.
2. Select a physical microphone as input.
3. Select the virtual cable input/render endpoint as output.
4. Start passthrough.
5. Open Windows Sound Recorder, OBS, Discord, or another downstream app.
6. Select the virtual cable recording endpoint as microphone.
7. Confirm voice is received.

## Acceptance Criteria

- Input and output devices are listed.
- Passthrough can start and stop repeatedly.
- Downstream app receives audio through the virtual device.
- App logs basic start/stop/errors.
- No crash during a 30-minute passthrough run.

## Agora Follow-Up

Only after passthrough is stable:

1. Add Agora SDK binaries and C# binding.
2. Implement `IDenoiseProcessor`.
3. Verify whether processed PCM can be retrieved locally.
4. Capture before/after WAV files.
5. Compare latency and voice quality.
