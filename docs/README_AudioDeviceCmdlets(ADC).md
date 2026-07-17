# AudioDeviceCmdlets

A suite of PowerShell Cmdlets to control audio devices on Windows, with CI/CD.

## Fork Notes

This is a fork of [frgnca/AudioDeviceCmdlets](https://github.com/frgnca/AudioDeviceCmdlets). The reason for this fork is to add a CI/CD (including code scanning, dependency alerts) and allow PC's to install straight from Github, instead of from PowerShellGallery via NuGet.

The base code is sync'd with the above project, but this fork also incorporates un-merged pull requests and community enhancements in addition to the ops improvements.

This code is a dependency for [Windows-Audio-and-Display-Baseline-Enforcer](https://github.com/mefranklin6/Windows-Audio-and-Display-Baseline-Enforcer)

## Features

Get list of all audio devices  
Optionally include disabled audio devices when listing  
Get default audio device (playback/recording)  
Get default communication audio device (playback/recording)  
Get volume and mute state of default audio device (playback/recording)  
Get volume and mute state of default communication audio device (playback/recording)  
Set default audio device (playback/recording)  
Set default communication audio device (playback/recording)  
Set volume and mute state of default audio device (playback/recording)  
Set volume and mute state of default communication audio device (playback/recording)

## Install (from GitHub Releases)

This fork publishes a ready-to-install PowerShell module zip to GitHub Releases. Note: I do not publish to PowershellGallery.

### Option A: install via the included installer script (version-pinned)

1. Pick a released version from the [Releases page](https://github.com/mefranklin6/AudioDeviceCmdlets/releases).
2. Run the below commands in Powershell:

- `-Version`: The release version from step 1.

- `-PC`: Use 'localhost' or specify a remote computer.

    ```PowerShell
    & ([ScriptBlock]::Create((Invoke-RestMethod 'https://raw.githubusercontent.com/mefranklin6/AudioDeviceCmdlets/master/install.ps1'))) -PC 'localhost' -Version '3.3'
    ```

### Option B: manual install (download + unzip)

1. Download the release asset named `AudioDeviceCmdlets-<version>.zip`.
2. Unzip it into one of these module locations:

- Current user: `$HOME\Documents\WindowsPowerShell\Modules\`
- All users: `$env:ProgramFiles\WindowsPowerShell\Modules\`

After unzipping, you should end up with:

`...\Modules\AudioDeviceCmdlets\AudioDeviceCmdlets.psd1`

Then you can:

```PowerShell
Import-Module AudioDeviceCmdlets
Get-Command -Module AudioDeviceCmdlets
```

## Usage

```PowerShell
Get-AudioDevice -ID <string>   # Get the device with the ID corresponding to the given <string>
Get-AudioDevice -Index <int>   # Get the device with the Index corresponding to the given <int>
Get-AudioDevice -List    # Get a list of all enabled devices as <AudioDevice>
Get-AudioDevice -List -ShowDisabled    # Get a list of all enabled devices, then append disabled devices as <AudioDevice>
Get-AudioDevice -PlaybackCommunication  # Get the default communication playback device as <AudioDevice>
Get-AudioDevice -PlaybackCommunicationMute # Get the default communication playback device's mute state as <bool>
Get-AudioDevice -PlaybackCommunicationVolume # Get the default communication playback device's volume level on 100 as <float>
Get-AudioDevice -Playback   # Get the default playback device as <AudioDevice>
Get-AudioDevice -PlaybackMute   # Get the default playback device's mute state as <bool>
Get-AudioDevice -PlaybackVolume   # Get the default playback device's volume level on 100 as <float>
Get-AudioDevice -RecordingCommunication  # Get the default communication recording device as <AudioDevice>
Get-AudioDevice -RecordingCommunicationMute # Get the default communication recording device's mute state as <bool>
Get-AudioDevice -RecordingCommunicationVolume # Get the default communication recording device's volume level on 100 as <float>
Get-AudioDevice -Recording   # Get the default recording device as <AudioDevice>
Get-AudioDevice -RecordingMute   # Get the default recording device's mute state as <bool>
Get-AudioDevice -RecordingVolume  # Get the default recording device's volume level on 100 as <float>
```

`Get-AudioDevice -List` keeps its existing behavior and returns active devices only. Use `-ShowDisabled` together with `-List` to append disabled devices to the output without changing the indexes of active devices. Returned `<AudioDevice>` objects now include an `Enabled` property.

```PowerShell
Set-AudioDevice <AudioDevice>    # Set the given playback/recording device as both the default device and the default communication device, for its type
Set-AudioDevice <AudioDevice> -CommunicationOnly # Set the given playback/recording device as the default communication device and not the default device, for its type
Set-AudioDevice <AudioDevice> -DefaultOnly  # Set the given playback/recording device as the default device and not the default communication device, for its type
Set-AudioDevice -ID <string>    # Set the device with the ID corresponding to the given <string> as both the default device and the default communication device, for its type
Set-AudioDevice -ID <string> -CommunicationOnly  # Set the device with the ID corresponding to the given <string> as the default communication device and not the default device, for its type
Set-AudioDevice -ID <string> -DefaultOnly  # Set the device with the ID corresponding to the given <string> as the default device and not the default communication device, for its type
Set-AudioDevice -Index <int>    # Set the device with the Index corresponding to the given <int> as both the default device and the default communication device, for its type
Set-AudioDevice -Index <int> -CommunicationOnly  # Set the device with the Index corresponding to the given <int> as the default communication device and not the default device, for its type
Set-AudioDevice -Index <int> -DefaultOnly  # Set the device with the Index corresponding to the given <int> as the default device and not the default communication device, for its type
Set-AudioDevice -PlaybackCommunicationMuteToggle # Set the default communication playback device's mute state to the opposite of its current mute state
Set-AudioDevice -PlaybackCommunicationMute <bool> # Set the default communication playback device's mute state to the given <bool>
Set-AudioDevice -PlaybackCommunicationVolume <float> # Set the default communication playback device's volume level on 100 to the given <float>
Set-AudioDevice -PlaybackMuteToggle   # Set the default playback device's mute state to the opposite of its current mute state
Set-AudioDevice -PlaybackMute <bool>   # Set the default playback device's mute state to the given <bool>
Set-AudioDevice -PlaybackVolume <float>   # Set the default playback device's volume level on 100 to the given <float>
Set-AudioDevice -RecordingCommunicationMuteToggle # Set the default communication recording device's mute state to the opposite of its current mute state
Set-AudioDevice -RecordingCommunicationMute <bool> # Set the default communication recording device's mute state to the given <bool>
Set-AudioDevice -RecordingCommunicationVolume <float> # Set the default communication recording device's volume level on 100 to the given <float>
Set-AudioDevice -RecordingMuteToggle   # Set the default recording device's mute state to the opposite of its current mute state
Set-AudioDevice -RecordingMute <bool>   # Set the default recording device's mute state to the given <bool>
Set-AudioDevice -RecordingVolume <float>  # Set the default recording device's volume level on 100 to the given <float>
```

```PowerShell
Write-AudioDevice -PlaybackCommunicationMeter # Write the default playback device's power output on 100 as a meter
Write-AudioDevice -PlaybackCommunicationStream # Write the default playback device's power output on 100 as a stream of <int>
Write-AudioDevice -PlaybackMeter  # Write the default playback device's power output on 100 as a meter
Write-AudioDevice -PlaybackStream  # Write the default playback device's power output on 100 as a stream of <int>
Write-AudioDevice -RecordingCommunicationMeter # Write the default recording device's power output on 100 as a meter
Write-AudioDevice -RecordingCommunicationStream # Write the default recording device's power output on 100 as a stream of <int>
Write-AudioDevice -RecordingMeter  # Write the default recording device's power output on 100 as a meter
Write-AudioDevice -RecordingStream  # Write the default recording device's power output on 100 as a stream of <int>
```

## Versions

- v3.3: Added `Get-AudioDevice -List -ShowDisabled` and `AudioDevice.Enabled` while preserving the default `-List` behavior and active-device indexing. Refactored and modernized how version number is used and updated. This is a modification of [PR #25](https://github.com/frgnca/AudioDeviceCmdlets/pull/25) from the root repository, authored by [frgnca](https://github.com/frgnca) with community input. 
- v3.2: Initial fork release. The underlying code and features are unchanged.
  - Updated the readme to reflect changes.
  - Added CI/CD including a build script, installer script and Github Advanced Security setup (CodeQL, Dependabot)

## Attribution

This is a fork of [frgnca/AudioDeviceCmdlets](https://github.com/frgnca/AudioDeviceCmdlets), which attributed the below:

Based on code originally posted to Code Project by Ray Molenkamp with comments and suggestions by MadMidi  
<http://www.codeproject.com/Articles/18520/Vista-Core-Audio-API-Master-Volume-Control>  
Based on code originally posted to GitHub by Chris Hunt  
<https://github.com/cdhunt/WindowsAudioDevice-Powershell-Cmdlet>  

## Donation

Some of the original authors have donation links or crypto wallet addresses. If you would like to donate to them, follow the links above. No donation will be accepted on this fork, but the thought is appreciated.
