; AI Noise Reduction - Inno Setup Installer
; Requires Inno Setup 6+ (https://jrsoftware.org/isinfo.php)
;
; Build:
;   1. dotnet publish src\NoiseReduction.App -c Release -r win-x64 --self-contained false
;   2. dotnet publish installer\NoiseReduction.InstallerHelper -c Release -r win-x64 --self-contained false
;   3. ISCC installer\setup.iss

#define AppName "AI Noise Reduction"
#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#define AppPublisher "AI Audio Noise Reduction"
#define AppURL "https://github.com/withoutcat/AI-Audio-Noise-Reduction"
#define DotNetURL "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.9/windowsdesktop-runtime-10.0.9-win-x64.exe"
#define DotNetExe "windowsdesktop-runtime-10.0.9-win-x64.exe"
#define VBCableURL "https://vb-audio.com/Cable/"
#define AppExeName "NoiseReductionApp.exe"

; Registry property name for VB-CABLE detection
#define CablePropGUID "{a45c254e-df1c-4efd-8020-67d146a850e0},2"
#define CablePropPath "\Properties"

; Source paths (relative to this file, in installer\)
#define AppPublishDir "..\src\NoiseReduction.App\bin\Release\net10.0-windows\win-x64\publish"
#define HelperPublishDir "NoiseReduction.InstallerHelper\bin\Release\net10.0-windows\win-x64\publish"

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

; === Default config (don't overwrite user edits on upgrade) ===
Source: "assets\config.default.json"; DestDir: "{app}"; DestName: "config.json"; Flags: ignoreversion onlyifdoesntexist

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

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\application.ico"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon; IconFilename: "{app}\application.ico"

[Dirs]
Name: "{app}\logs"; Permissions: users-modify

[Code]
var
  VBCableDetected: Boolean;
  VBCableOkPage: TWizardPage;
  VBCableWaitPage: TWizardPage;
  VBCableWaitLabel: TLabel;
  VBCableRecheckBtn: TNewButton;
  VBCableSkipAnywayBtn: TNewButton;
  VBCableOpenUrlBtn: TNewButton;
  VBCableStatusLabel: TLabel;
  VBCableWaitDone: Boolean;
  NetPage: TOutputMarqueeProgressWizardPage;

procedure OnOpenVBCableUrl(Sender: TObject); forward;
procedure OnRecheckVBCable(Sender: TObject); forward;
procedure OnSkipVBCable(Sender: TObject); forward;

function IsCableInstalled(): Boolean;
var
  SubKeys: TArrayOfString;
  I: Integer;
  FriendlyName: String;
begin
  Result := False;
  if RegGetSubkeyNames(HKLM64, 'SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture', SubKeys) then
    for I := 0 to GetArrayLength(SubKeys) - 1 do
      if RegQueryStringValue(HKLM64,
        'SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture\' + SubKeys[I] + '{#CablePropPath}',
        '{#CablePropGUID}', FriendlyName) and (Pos('CABLE', FriendlyName) > 0) then
      begin
        Result := True; Exit;
      end;
  if not Result then
    if RegGetSubkeyNames(HKLM64, 'SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render', SubKeys) then
      for I := 0 to GetArrayLength(SubKeys) - 1 do
        if RegQueryStringValue(HKLM64,
          'SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\' + SubKeys[I] + '{#CablePropPath}',
          '{#CablePropGUID}', FriendlyName) and (Pos('CABLE', FriendlyName) > 0) then
        begin
          Result := True; Exit;
        end;
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
  VBCableWaitLabel.Height := 40;
  VBCableWaitLabel.Caption := 'VB-CABLE device not detected.' + #13#10 +
    'Click below to open the official website, download and install, then click "I have installed".';
  VBCableWaitLabel.WordWrap := True;

  VBCableOpenUrlBtn := TNewButton.Create(VBCableWaitPage);
  VBCableOpenUrlBtn.Parent := VBCableWaitPage.Surface;
  VBCableOpenUrlBtn.Left := 0; VBCableOpenUrlBtn.Top := 60;
  VBCableOpenUrlBtn.Width := VBCableWaitPage.SurfaceWidth;
  VBCableOpenUrlBtn.Height := 30;
  VBCableOpenUrlBtn.Caption := 'Open official download page';
  VBCableOpenUrlBtn.OnClick := @OnOpenVBCableUrl;

  VBCableRecheckBtn := TNewButton.Create(VBCableWaitPage);
  VBCableRecheckBtn.Parent := VBCableWaitPage.Surface;
  VBCableRecheckBtn.Left := 0; VBCableRecheckBtn.Top := 100;
  VBCableRecheckBtn.Width := VBCableWaitPage.SurfaceWidth;
  VBCableRecheckBtn.Height := 30;
  VBCableRecheckBtn.Caption := 'I have installed - Recheck';
  VBCableRecheckBtn.OnClick := @OnRecheckVBCable;

  VBCableSkipAnywayBtn := TNewButton.Create(VBCableWaitPage);
  VBCableSkipAnywayBtn.Parent := VBCableWaitPage.Surface;
  VBCableSkipAnywayBtn.Left := 0; VBCableSkipAnywayBtn.Top := 140;
  VBCableSkipAnywayBtn.Width := VBCableWaitPage.SurfaceWidth;
  VBCableSkipAnywayBtn.Height := 30;
  VBCableSkipAnywayBtn.Caption := 'Skip and continue (not recommended)';
  VBCableSkipAnywayBtn.OnClick := @OnSkipVBCable;

  VBCableStatusLabel := TLabel.Create(VBCableWaitPage);
  VBCableStatusLabel.Parent := VBCableWaitPage.Surface;
  VBCableStatusLabel.Left := 0; VBCableStatusLabel.Top := 190;
  VBCableStatusLabel.Width := VBCableWaitPage.SurfaceWidth;
  VBCableStatusLabel.Height := 60;
  VBCableStatusLabel.Caption := 'Note: Without VB-CABLE, denoised audio cannot be output to other apps. You can install it manually later.';
  VBCableStatusLabel.WordWrap := True;
  VBCableStatusLabel.Font.Color := clRed;
  VBCableWaitDone := False;
end;

procedure OnOpenVBCableUrl(Sender: TObject);
var
  ErrCode: Integer;
begin
  ShellExec('open', '{#VBCableURL}', '', '', SW_SHOWNORMAL, ewNoWait, ErrCode);
end;

procedure OnRecheckVBCable(Sender: TObject);
begin
  if IsCableInstalled() then
  begin
    VBCableWaitDone := True;
    WizardForm.NextButton.OnClick(nil);
  end
  else
    MsgBox('VB-CABLE still not detected. Please confirm installation, or click "Skip".',
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
  VBCableDetected := IsCableInstalled();
  if VBCableDetected then
    CreateVBCableOkPage()
  else
    CreateVBCableWaitPage();
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
  if (VBCableWaitPage <> nil) and (PageId = VBCableWaitPage.ID) and not VBCableWaitDone then
    Result := False;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if (VBCableWaitPage <> nil) and (CurPageID = VBCableWaitPage.ID) then
    VBCableWaitDone := False;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  DownloadPath, LogFile: String;
  ResultCode: Integer;
  TS: String;
begin
  if CurStep <> ssPostInstall then Exit;

  LogFile := ExpandConstant('{app}') + '\logs\installer.log';
  TS := '[' + GetDateTimeString('yyyy-mm-dd hh:nn:ss', '#', '#') + '] ';

  SaveStringToFile(LogFile, TS + '=== Installation post-install phase ===' + #13#10, True);

  { --- .NET Desktop Runtime --- }
  SaveStringToFile(LogFile, TS + 'Checking .NET Desktop Runtime via dotnet --list-runtimes...' + #13#10, True);
  if IsNetDesktopRuntimeInstalled() then
  begin
    SaveStringToFile(LogFile, TS + '.NET Desktop Runtime 10.0.x found, skipping download.' + #13#10, True);
  end
  else
  begin
    SaveStringToFile(LogFile, TS + '.NET Desktop Runtime 10.0.x NOT found.' + #13#10, True);
    DownloadPath := ExpandConstant('{tmp}\{#DotNetExe}');
    SaveStringToFile(LogFile, TS + 'Download target: ' + DownloadPath + #13#10, True);

    if FileExists(DownloadPath) then
    begin
      SaveStringToFile(LogFile, TS + 'Cached installer found, reusing.' + #13#10, True);
    end
    else
    begin
      SaveStringToFile(LogFile, TS + 'Downloading from: {#DotNetURL}' + #13#10, True);

      NetPage := CreateOutputMarqueeProgressPage(
        'Installing .NET Runtime',
        'Downloading .NET Desktop Runtime 10.0.9 (~75 MB)...');
      NetPage.Show;
      try
        NetPage.Animate;
        SaveStringToFile(LogFile, TS + 'Using PowerShell Invoke-WebRequest...' + #13#10, True);
        Exec('powershell.exe',
          '-NoProfile -ExecutionPolicy Bypass -Command "Invoke-WebRequest -Uri ''{#DotNetURL}'' -OutFile ''{tmp}\{#DotNetExe}''"',
          '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
        SaveStringToFile(LogFile, TS + 'PowerShell exit code: ' + IntToStr(ResultCode) + #13#10, True);

        if FileExists(DownloadPath) then
          SaveStringToFile(LogFile, TS + 'Download complete.' + #13#10, True)
        else
          SaveStringToFile(LogFile, TS + 'Download FAILED - file not found after download' + #13#10, True);
      finally
        NetPage.Hide;
      end;
    end;

    if FileExists(DownloadPath) then
    begin
      NetPage := CreateOutputMarqueeProgressPage(
        'Installing .NET Runtime',
        'Installing .NET Desktop Runtime 10.0.9. This may take 1-2 minutes...');
      NetPage.Show;
      try
        NetPage.Animate;
        SaveStringToFile(LogFile, TS + 'Running: ' + DownloadPath + ' /install /quiet /norestart' + #13#10, True);
        Exec(DownloadPath, '/install /quiet /norestart',
          '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
        if ResultCode = 0 then
          SaveStringToFile(LogFile, TS + '.NET Desktop Runtime 10.0.9 installed successfully' + #13#10, True)
        else
          SaveStringToFile(LogFile, TS + '.NET Desktop Runtime install FAILED (code ' + IntToStr(ResultCode) + ')' + #13#10, True);
        RegDeleteValue(HKLM,
          'SYSTEM\CurrentControlSet\Control\Session Manager',
          'PendingFileRenameOperations');
      finally
        NetPage.Hide;
      end;
    end;
  end;
end;