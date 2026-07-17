; AI Noise Reduction - Inno Setup Installer
; Requires Inno Setup 6+ (https://jrsoftware.org/isinfo.php)
;
; Build:
;   1. dotnet publish src\NoiseReduction.App -c Release -r win-x64 --self-contained false
;   2. ISCC installer\setup.iss

#define AppName "AI Noise Reduction"
#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#define AppPublisher "AI Audio Noise Reduction"
#define AppURL "https://github.com/withoutcat/AI-Audio-Noise-Reduction"
#define DotNetURL "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.9/windowsdesktop-runtime-10.0.9-win-x64.exe"
#define DotNetExe "windowsdesktop-runtime-10.0.9-win-x64.exe"
#define VBCableURL "https://vb-audio.com/Cable/"
#define VBCableDownloadURL "https://download.vb-audio.com/Download_CABLE/VBCABLE_Driver_Pack45.zip"
#define VBCableZipName "VBCABLE_Driver_Pack45.zip"
#define AppExeName "NoiseReductionApp.exe"

; Registry property name for VB-CABLE detection
#define CablePropGUID "{b3f8fa53-0004-438e-9003-51a46e139bfc},6"
#define CablePropPath "\Properties"

; Source paths (relative to this file, in installer\)
#define AppPublishDir "..\src\NoiseReduction.App\bin\Release\net10.0-windows\win-x64\publish"

; Output filename (overridable via ISCC /DOutputFileName=...)
#ifndef OutputFileName
  #define OutputFileName "AINoiseReduction-" + AppVersion + "-win-x64"
#endif

[Setup]
AppId={{8A2B3C4D-5E6F-7890-ABCD-EF1234567890}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={autopf}\AI Noise Reduction
DefaultGroupName={#AppName}
AllowNoIcons=yes
OutputDir=output
OutputBaseFilename={#OutputFileName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline
UninstallDisplayIcon={app}\{#AppExeName}
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
AlwaysRestart=no
SetupLogging=yes
SetupIconFile=..\src\NoiseReduction.App\Assets\application.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: checkedonce

[Files]
; === Managed assemblies (root directory) ===
Source: "{#AppPublishDir}\NoiseReductionApp.exe";              DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppPublishDir}\NoiseReductionApp.dll";              DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppPublishDir}\NoiseReductionApp.deps.json";        DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppPublishDir}\NoiseReductionApp.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppPublishDir}\NoiseReductionCore.dll";             DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppPublishDir}\NoiseReductionInfra.dll";            DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppPublishDir}\NAudio.dll";                         DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppPublishDir}\NAudio.Asio.dll";                    DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppPublishDir}\NAudio.Core.dll";                    DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppPublishDir}\NAudio.Midi.dll";                    DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppPublishDir}\NAudio.Wasapi.dll";                  DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppPublishDir}\NAudio.WinForms.dll";                DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppPublishDir}\NAudio.WinMM.dll";                   DestDir: "{app}"; Flags: ignoreversion

; === Native DLLs (native\ subdirectory) ===
Source: "{#AppPublishDir}\native\*.dll"; DestDir: "{app}\native"; Flags: ignoreversion

; === Application icon (for shortcuts) ===
Source: "{#AppPublishDir}\application.ico"; DestDir: "{app}"; Flags: ignoreversion

[InstallDelete]
; Remove legacy files from previous installer versions (two-dot naming, root-level native DLLs)
Name: "{app}\NoiseReduction.App.exe"; Type: files
Name: "{app}\NoiseReduction.App.dll"; Type: files
Name: "{app}\NoiseReduction.App.deps.json"; Type: files
Name: "{app}\NoiseReduction.App.runtimeconfig.json"; Type: files
Name: "{app}\NoiseReduction.Core.dll"; Type: files
Name: "{app}\NoiseReduction.Infrastructure.dll"; Type: files
Name: "{app}\NoiseReduction.Bridge.dll"; Type: files
Name: "{app}\Bridge.dll"; Type: files
Name: "{app}\agora_rtc_sdk.dll"; Type: files
Name: "{app}\glfw3.dll"; Type: files
Name: "{app}\libagora-fdkaac.dll"; Type: files
Name: "{app}\libagora-ffmpeg.dll"; Type: files
Name: "{app}\libagora-soundtouch.dll"; Type: files
Name: "{app}\libagora-wgc.dll"; Type: files
Name: "{app}\libagora_ai_noise_suppression_extension.dll"; Type: files
Name: "{app}\libaosl.dll"; Type: files
Name: "{app}\NoiseReduction.InstallerHelper.exe"; Type: files
Name: "{app}\native\glfw3.dll"; Type: files
Name: "{app}\native\libagora-wgc.dll"; Type: files
Name: "{app}\native\libagora_clear_vision_extension.dll"; Type: files
Name: "{app}\native\libagora_content_inspect_extension.dll"; Type: files
Name: "{app}\native\libagora_face_capture_extension.dll"; Type: files
Name: "{app}\native\libagora_face_detection_extension.dll"; Type: files
Name: "{app}\native\libagora_segmentation_extension.dll"; Type: files
Name: "{app}\native\libagora_screen_capture_extension.dll"; Type: files
Name: "{app}\native\libagora_spatial_audio_extension.dll"; Type: files
Name: "{app}\native\libagora_lip_sync_extension.dll"; Type: files
Name: "{app}\native\libagora_video_av1_encoder_extension.dll"; Type: files
Name: "{app}\native\libagora_video_encoder_extension.dll"; Type: files
Name: "{app}\native\libagora_video_quality_analyzer_extension.dll"; Type: files
Name: "{app}\native\libagora_audio_beauty_extension.dll"; Type: files
Name: "{app}\native\video_dec.dll"; Type: files
Name: "{app}\native\video_enc.dll"; Type: files

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\application.ico"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon; IconFilename: "{app}\application.ico"

[Dirs]
Name: "{app}\logs"; Permissions: users-modify

[Code]
var
  DownloadPage: TDownloadWizardPage;
  VBCableDetected: Boolean;
  VBCableDeviceGUID: String;
  VBCableOkPage: TWizardPage;
  VBCableWaitPage: TWizardPage;
  VBCableWaitLabel: TLabel;
  VBCableDownloadBtn: TNewButton;
  VBCableRecheckBtn: TNewButton;
  VBCableSkipBtn: TNewButton;
  VBCableStatusLabel: TLabel;
  VBCableWaitDone: Boolean;
  NeedDotNetDownload: Boolean;
  DotNetDownloadSuccess: Boolean;
  { ── Download speed tracking ── }
  DLSpeedStartTick: Int64;
  DLSpeedLastTick: Int64;
  DLSpeedLastBytes: Int64;

function GetTickCount64: Int64;
  external 'GetTickCount64@kernel32.dll stdcall';

{ ── OnDownloadProgress: shows real-time speed / size / percentage ── }
function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
var
  NowTick, Elapsed: Int64;
  Speed: Double;
  SpeedStr, SizeStr, PctStr: String;
begin
  NowTick := GetTickCount64;

  { ── Download speed (update every ~500ms) ── }
  if DLSpeedStartTick = 0 then
  begin
    DLSpeedStartTick := NowTick;
    DLSpeedLastTick := NowTick;
    DLSpeedLastBytes := Progress;
  end
  else if (NowTick - DLSpeedLastTick) >= 500 then
  begin
    if NowTick > DLSpeedLastTick then
    begin
      Elapsed := NowTick - DLSpeedLastTick;
      Speed := (Progress - DLSpeedLastBytes) / Elapsed * 1000.0; // bytes/sec
      DLSpeedLastTick := NowTick;
      DLSpeedLastBytes := Progress;
    end;
  end;

  { ── Build status text ── }
  if Speed >= 1048576.0 then
    SpeedStr := Format('%.1f MB/s', [Speed / 1048576.0])
  else if Speed >= 1024.0 then
    SpeedStr := Format('%.0f KB/s', [Speed / 1024.0])
  else
    SpeedStr := '';

  if ProgressMax > 0 then
  begin
    if Progress < 1024 then
      SizeStr := Format('%d B', [Progress])
    else if Progress < 1048576 then
      SizeStr := Format('%.1f KB', [Progress / 1024.0])
    else
      SizeStr := Format('%.1f MB', [Progress / 1048576.0]);

    SizeStr := SizeStr + ' / ';

    if ProgressMax < 1048576 then
      SizeStr := SizeStr + Format('%.1f KB', [ProgressMax / 1024.0])
    else
      SizeStr := SizeStr + Format('%.1f MB', [ProgressMax / 1048576.0]);

    PctStr := Format(' (%d%%)', [Progress * 100 div ProgressMax]);
  end
  else
  begin
    if Progress < 1048576 then
      SizeStr := Format('%.1f KB', [Progress / 1024.0])
    else
      SizeStr := Format('%.1f MB', [Progress / 1048576.0]);
    PctStr := '';
  end;

  if SpeedStr <> '' then
    DownloadPage.Msg2Label.Caption := SpeedStr + '  |  ' + SizeStr + PctStr
  else
    DownloadPage.Msg2Label.Caption := SizeStr + PctStr;

  DownloadPage.Msg1Label.Caption := FileName;

  Result := True; { continue download }
end;

procedure OnDownloadVBCable(Sender: TObject); forward;
procedure OnRecheckVBCable(Sender: TObject); forward;
procedure OnSkipVBCable(Sender: TObject); forward;

function IsVBAudioInstalled(): Boolean;
var
  SubKeys: TArrayOfString;
  I: Integer;
  Value: String;
begin
  Result := False;
  VBCableDeviceGUID := '';
  { Check Capture first (CABLE Output is a capture device) }
  if RegGetSubkeyNames(HKLM64, 'SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture', SubKeys) then
    for I := 0 to GetArrayLength(SubKeys) - 1 do
      if RegQueryStringValue(HKLM64,
        'SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture\' + SubKeys[I] + '\Properties',
        '{#CablePropGUID}', Value) and (Pos('VB-Audio', Value) > 0) then
      begin
        Result := True;
        VBCableDeviceGUID := SubKeys[I];
        Exit;
      end;
  { Fall back to Render (CABLE Input) }
  if RegGetSubkeyNames(HKLM64, 'SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render', SubKeys) then
    for I := 0 to GetArrayLength(SubKeys) - 1 do
      if RegQueryStringValue(HKLM64,
        'SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\' + SubKeys[I] + '\Properties',
        '{#CablePropGUID}', Value) and (Pos('VB-Audio', Value) > 0) then
      begin
        Result := True;
        VBCableDeviceGUID := SubKeys[I];
        Exit;
      end;
end;

procedure WriteDefaultVirtualMicConfig();
var
  ConfigDir: String;
  ConfigPath: String;
  Lines: TArrayOfString;
  DeviceId: String;
begin
  DeviceId := '{0.0.1.00000000}.{' + VBCableDeviceGUID + '}';
  ConfigDir := ExpandConstant('{localappdata}') + '\AINoiseReduction';
  ConfigPath := ConfigDir + '\config.json';

  if not DirExists(ConfigDir) then
    CreateDir(ConfigDir);

  SetArrayLength(Lines, 3);
  Lines[0] := '{';
  Lines[1] := '  "DefaultVirtualMicphoneID": "' + DeviceId + '"';
  Lines[2] := '}';

  if SaveStringsToUTF8File(ConfigPath, Lines, False) then
    Log('Written DefaultVirtualMicphoneID: ' + DeviceId)
  else
    Log('Failed to write config.json');
end;


function IsNetDesktopRuntimeInstalled(): Boolean;
var
  DotOutput: TExecOutput;
  ResultCode, I: Integer;
  Line: String;
begin
  Result := False;
  try
    ExecAndCaptureOutput('dotnet.exe', '--list-runtimes', '', SW_HIDE, ewWaitUntilTerminated, ResultCode, DotOutput);
    if ResultCode = 0 then
      for I := 0 to GetArrayLength(DotOutput.StdOut) - 1 do
      begin
        Line := DotOutput.StdOut[I];
        if Pos('Microsoft.WindowsDesktop.App 10.0.', Line) > 0 then
        begin
          Result := True; Exit;
        end;
      end;
  except
    { dotnet.exe not found — runtime not installed }
  end;
end;

procedure CreateVBCableOkPage();
var
  Lbl: TLabel;
begin
  VBCableOkPage := CreateCustomPage(wpSelectDir,
    'VB-CABLE Virtual Audio Device',
    'VB-CABLE device detected.');

  Lbl := TLabel.Create(VBCableOkPage);
  Lbl.Parent := VBCableOkPage.Surface;
  Lbl.Left := 0; Lbl.Top := 10;
  Lbl.Width := VBCableOkPage.SurfaceWidth;
  Lbl.Height := 60;
  Lbl.Caption :=
    'VB-CABLE Virtual Audio Device is installed and ready to use.' + #13#10#13#10 +
    'Click "Next" to continue with the installation.';
  Lbl.WordWrap := True;
end;

procedure CreateVBCableWaitPage();
begin
  VBCableWaitPage := CreateCustomPage(wpSelectDir,
    'VB-CABLE Virtual Audio Device',
    'This program requires VB-CABLE virtual audio driver for output.');

  VBCableWaitLabel := TLabel.Create(VBCableWaitPage);
  VBCableWaitLabel.Parent := VBCableWaitPage.Surface;
  VBCableWaitLabel.Left := 0; VBCableWaitLabel.Top := 0;
  VBCableWaitLabel.Width := VBCableWaitPage.SurfaceWidth;
  VBCableWaitLabel.Height := 60;
  VBCableWaitLabel.Caption :=
    'VB-CABLE device not detected.' + #13#10#13#10 +
    'Click "Download & Install" to automatically download (~1.3 MB) and install VB-CABLE. ' +
    'After installation, click "I have installed - Recheck" to verify.';
  VBCableWaitLabel.WordWrap := True;

  VBCableDownloadBtn := TNewButton.Create(VBCableWaitPage);
  VBCableDownloadBtn.Parent := VBCableWaitPage.Surface;
  VBCableDownloadBtn.Left := 0; VBCableDownloadBtn.Top := 80;
  VBCableDownloadBtn.Width := VBCableWaitPage.SurfaceWidth;
  VBCableDownloadBtn.Height := 32;
  VBCableDownloadBtn.Caption := 'Download & Install VB-CABLE';
  VBCableDownloadBtn.OnClick := @OnDownloadVBCable;

  VBCableRecheckBtn := TNewButton.Create(VBCableWaitPage);
  VBCableRecheckBtn.Parent := VBCableWaitPage.Surface;
  VBCableRecheckBtn.Left := 0; VBCableRecheckBtn.Top := 120;
  VBCableRecheckBtn.Width := VBCableWaitPage.SurfaceWidth;
  VBCableRecheckBtn.Height := 30;
  VBCableRecheckBtn.Caption := 'I have installed - Recheck';
  VBCableRecheckBtn.OnClick := @OnRecheckVBCable;

  VBCableSkipBtn := TNewButton.Create(VBCableWaitPage);
  VBCableSkipBtn.Parent := VBCableWaitPage.Surface;
  VBCableSkipBtn.Left := 0; VBCableSkipBtn.Top := 158;
  VBCableSkipBtn.Width := VBCableWaitPage.SurfaceWidth;
  VBCableSkipBtn.Height := 30;
  VBCableSkipBtn.Caption := 'Skip and continue (not recommended)';
  VBCableSkipBtn.OnClick := @OnSkipVBCable;

  VBCableStatusLabel := TLabel.Create(VBCableWaitPage);
  VBCableStatusLabel.Parent := VBCableWaitPage.Surface;
  VBCableStatusLabel.Left := 0; VBCableStatusLabel.Top := 200;
  VBCableStatusLabel.Width := VBCableWaitPage.SurfaceWidth;
  VBCableStatusLabel.Height := 60;
  VBCableStatusLabel.Caption :=
    'Note: Without VB-CABLE, denoised audio cannot be output to other apps. ' +
    'You can install it manually later from {#VBCableURL}';
  VBCableStatusLabel.WordWrap := True;
  VBCableStatusLabel.Font.Color := clGray;
  VBCableWaitDone := False;
end;

{ ── Download & Install VB-CABLE ── }
procedure OnDownloadVBCable(Sender: TObject);
var
  ZipPath, ExtractDir, InstallerPath, LogFile, PSCommand, TS: String;
  ResultCode: Integer;
begin
  ZipPath := ExpandConstant('{tmp}\{#VBCableZipName}');
  ExtractDir := ExpandConstant('{tmp}\vbcable');
  LogFile := ExpandConstant('{tmp}\vbcable_install.log');
  TS := '[' + GetDateTimeString('yyyy-mm-dd hh:nn:ss', '#', '#') + '] ';

  SaveStringToFile(LogFile, TS + '=== VB-CABLE auto-install ===' + #13#10, False);

  { ── Step 1: Download with real progress bar ── }
  DownloadPage.Clear;
  DownloadPage.Add('{#VBCableDownloadURL}', '{#VBCableZipName}', '');
  DownloadPage.Show;
  try
    try
      SaveStringToFile(LogFile, TS + 'Downloading: {#VBCableDownloadURL}' + #13#10, True);
      DownloadPage.Download;
      SaveStringToFile(LogFile, TS + 'Download complete: ' + ZipPath + #13#10, True);
    except
      if DownloadPage.AbortedByUser then
        SaveStringToFile(LogFile, TS + 'Download aborted by user' + #13#10, True)
      else begin
        SaveStringToFile(LogFile, TS + 'Download FAILED: ' + GetExceptionMessage + #13#10, True);
        MsgBox('Download failed. Please check your internet connection and try again, ' +
          'or visit {#VBCableURL} to download manually.', mbError, MB_OK);
      end;
      Exit;
    end;
  finally
    DownloadPage.Hide;
  end;

  if not FileExists(ZipPath) then Exit;

  { ── Step 2: Extract ── }
  if DirExists(ExtractDir) then
    DelTree(ExtractDir, True, True, True);

  PSCommand :=
    'Expand-Archive -Path ''' + ZipPath + ''' -DestinationPath ''' + ExtractDir + ''' -Force';

  if not Exec('powershell.exe',
    '-NoProfile -ExecutionPolicy Bypass -Command "' + PSCommand + '"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
  begin
    SaveStringToFile(LogFile, TS + 'Extract FAILED (code ' + IntToStr(ResultCode) + ')' + #13#10, True);
    MsgBox('Failed to extract VB-CABLE package. Please try again or install manually.',
      mbError, MB_OK);
    Exit;
  end;
  SaveStringToFile(LogFile, TS + 'Extract complete: ' + ExtractDir + #13#10, True);

  { ── Step 3: Run VB-CABLE installer ── }
  InstallerPath := ExtractDir + '\VBCABLE_Setup_x64.exe';
  if not FileExists(InstallerPath) then
  begin
    InstallerPath := ExtractDir + '\VBCABLE_Setup.exe';
    if not FileExists(InstallerPath) then
    begin
      SaveStringToFile(LogFile, TS + 'Setup EXE not found in extracted package' + #13#10, True);
      MsgBox('VB-CABLE setup program not found in the downloaded package. ' +
        'Please visit {#VBCableURL} to install manually.', mbError, MB_OK);
      Exit;
    end;
  end;

  SaveStringToFile(LogFile, TS + 'Running: ' + InstallerPath + #13#10, True);

  if Exec(InstallerPath, '', ExtractDir, SW_SHOWNORMAL, ewWaitUntilTerminated, ResultCode) then
    SaveStringToFile(LogFile, TS + 'Installer finished, code: ' + IntToStr(ResultCode) + #13#10, True)
  else
    SaveStringToFile(LogFile, TS + 'Failed to launch installer' + #13#10, True);

  { ── Step 4: Clean up temp files ── }
  try
    DeleteFile(ZipPath);
    DelTree(ExtractDir, True, True, True);
  except
  end;

  { ── Step 5: Recheck ── }
  if IsVBAudioInstalled() then
  begin
    SaveStringToFile(LogFile, TS + 'VB-CABLE detected after installation' + #13#10, True);
    VBCableStatusLabel.Caption := 'VB-CABLE detected! Click "Next" to continue.';
    WriteDefaultVirtualMicConfig();
    VBCableStatusLabel.Font.Color := clGreen;
    VBCableWaitDone := True;
    WizardForm.NextButton.OnClick(nil);
  end
  else
  begin
    SaveStringToFile(LogFile, TS + 'VB-CABLE still not detected after installation' + #13#10, True);
    VBCableStatusLabel.Caption :=
      'Installation completed, but VB-CABLE was not detected.' + #13#10 +
      'You may need to reboot your computer before the driver takes effect.' + #13#10 +
      'After reboot, re-run this installer or install VB-CABLE manually.';
    VBCableStatusLabel.Font.Color := clRed;
  end;
end;

procedure OnRecheckVBCable(Sender: TObject);
begin
  if IsVBAudioInstalled() then
  begin
    VBCableStatusLabel.Caption := 'VB-CABLE detected! Click "Next" to continue.';
    VBCableStatusLabel.Font.Color := clGreen;
    VBCableWaitDone := True;
    WizardForm.NextButton.OnClick(nil);
  end
  else
    MsgBox('VB-CABLE still not detected. Please install it first, or click "Skip" to continue without it.',
      mbInformation, MB_OK);
end;

procedure OnSkipVBCable(Sender: TObject);
begin
  if MsgBox(
    'WARNING' + #13#10#13#10 +
    'You chose to skip VB-CABLE installation.' + #13#10#13#10 +
    '- The AI denoise function can still start' + #13#10 +
    '- But denoised audio cannot be output to other applications' + #13#10 +
    '- You can install VB-CABLE later to restore full functionality' + #13#10#13#10 +
    'Are you sure you want to continue?',
    mbConfirmation, MB_YESNO) = IDYES then
  begin
    VBCableWaitDone := True;
    WizardForm.NextButton.OnClick(nil);
  end;
end;

procedure InitializeWizard();
begin
  { ── Shared download page (used by both VB-CABLE and .NET Runtime) ── }
  DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), @OnDownloadProgress);
  DownloadPage.ShowBaseNameInsteadOfUrl := False;

  { ── VB-CABLE detection ── }
  VBCableDetected := IsVBAudioInstalled();
  if VBCableDetected then
  begin
    CreateVBCableOkPage();
    WriteDefaultVirtualMicConfig();
  end
  else
    CreateVBCableWaitPage();

  { ── Check .NET Desktop Runtime ── }
  NeedDotNetDownload := not IsNetDesktopRuntimeInstalled();
  DotNetDownloadSuccess := False;
end;

function ShouldSkipPage(PageId: Integer): Boolean;
begin
  Result := False;
  if (VBCableOkPage <> nil) and (PageId = VBCableOkPage.ID) and not VBCableDetected then
    Result := True;
  if (VBCableWaitPage <> nil) and (PageId = VBCableWaitPage.ID) and VBCableDetected then
    Result := True;
end;

function NextButtonClick(PageId: Integer): Boolean;
begin
  Result := True;
  { ── Block Next on VB-CABLE wait page until user installs or skips ── }
  if (VBCableWaitPage <> nil) and (PageId = VBCableWaitPage.ID) and not VBCableWaitDone then
  begin
    Result := False;
    Exit;
  end;

  { ── Download .NET Desktop Runtime at wpReady with real progress bar ── }
  if (PageId = wpReady) and NeedDotNetDownload then
  begin
    DownloadPage.Clear;
    DownloadPage.Add('{#DotNetURL}', '{#DotNetExe}', '');
    DownloadPage.Show;
    try
      try
        DownloadPage.Download;
        DotNetDownloadSuccess := True;
        Log('.NET Desktop Runtime download complete');
      except
        if DownloadPage.AbortedByUser then
          Log('.NET Desktop Runtime download aborted by user')
        else
          SuppressibleMsgBox(
            '.NET Desktop Runtime download failed: ' + GetExceptionMessage + #13#10#13#10 +
            'The application will still be installed, but you need to install .NET Desktop Runtime 10.0 manually to run it.',
            mbCriticalError, MB_OK, IDOK);
        Result := True;
      end;
    finally
      DownloadPage.Hide;
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  DotNetPath, LogFile: String;
  ResultCode: Integer;
  TS: String;
begin
  if CurStep <> ssPostInstall then Exit;

  LogFile := ExpandConstant('{app}') + '\logs\installer.log';
  TS := '[' + GetDateTimeString('yyyy-mm-dd hh:nn:ss', '#', '#') + '] ';

  SaveStringToFile(LogFile, TS + '=== Post-install ===' + #13#10, True);

  { ── .NET Desktop Runtime silent install ── }
  if not NeedDotNetDownload then
  begin
    SaveStringToFile(LogFile, TS + '.NET Desktop Runtime 10.0.x already installed, skipping.' + #13#10, True);
    Exit;
  end;

  if not DotNetDownloadSuccess then
  begin
    SaveStringToFile(LogFile, TS + '.NET Desktop Runtime download skipped/failed, skipping install.' + #13#10, True);
    Exit;
  end;

  DotNetPath := ExpandConstant('{tmp}\{#DotNetExe}');
  if FileExists(DotNetPath) then
  begin
    SaveStringToFile(LogFile, TS + 'Installing .NET Desktop Runtime 10.0.9...' + #13#10, True);
    SaveStringToFile(LogFile, TS + 'Running: ' + DotNetPath + ' /install /quiet /norestart' + #13#10, True);
    Exec(DotNetPath, '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    if ResultCode = 0 then
      SaveStringToFile(LogFile, TS + '.NET Desktop Runtime 10.0.9 installed successfully' + #13#10, True)
    else
      SaveStringToFile(LogFile, TS + '.NET Desktop Runtime install FAILED (code ' + IntToStr(ResultCode) + ')' + #13#10, True);
    RegDeleteValue(HKLM,
      'SYSTEM\CurrentControlSet\Control\Session Manager',
      'PendingFileRenameOperations');

  { 写入虚拟麦克风配置到 config.json }
  if IsVBAudioInstalled() then
    WriteDefaultVirtualMicConfig();
  end;
end;


