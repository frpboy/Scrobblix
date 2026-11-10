; Per-user only. All payload binaries are self-contained; no runtime downloads or services.
#ifndef PayloadDir
  #error PayloadDir is required
#endif
#ifndef BuildVersion
  #define BuildVersion "0.1.0.10"
#endif
#ifndef BuildLabel
  #define BuildLabel "Windows installer preview"
#endif
#ifndef OutputDir
  #error OutputDir is required
#endif
[Setup]
AppId=FrpBoy.Scrobblix.Windows
AppName=Scrobblix
AppVersion={#BuildVersion}
AppVerName=Scrobblix {#BuildLabel}
AppPublisher=Rahul Muraleedharan
AppPublisherURL=https://www.frpboy.in/
AppSupportURL=https://t.me/FRPboy_Projects
AppUpdatesURL=https://github.com/frpboy/Scrobblix/releases
DefaultDirName={localappdata}\Programs\Scrobblix
DisableDirPage=yes
UsePreviousAppDir=no
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.26100
OutputDir={#OutputDir}
OutputBaseFilename=Scrobblix-Windows-x64-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\..\src\Scrobblix.Desktop\Assets\scrobblix.ico
DisableFinishedPage=no
UninstallDisplayIcon={app}\desktop\Scrobblix.Desktop.exe
CloseApplications=no
RestartApplications=no
AllowCancelDuringInstall=no
SetupLogging=yes
LicenseFile={#PayloadDir}\LICENSE
InfoBeforeFile=InstallerNotice.txt
[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{userprograms}\Scrobblix"; Filename: "{app}\desktop\Scrobblix.Desktop.exe"; WorkingDir: "{app}\desktop"; IconFilename: "{app}\desktop\Assets\scrobblix.ico"
[Run]
Filename: "{app}\desktop\Scrobblix.Desktop.exe"; WorkingDir: "{app}\desktop"; Description: "Run Scrobblix"; Flags: nowait postinstall skipifsilent
Filename: "https://github.com/frpboy/Scrobblix"; Description: "Visit GitHub"; Flags: shellexec nowait postinstall skipifsilent unchecked
[Code]
var
  AppGate: LongWord;
function SHGetFolderPath(Window: LongWord; Folder: Integer; Token: LongWord; Flags: LongWord; Path: String): Integer;
  external 'SHGetFolderPathW@shell32.dll stdcall';
function CreateSemaphore(Security: LongWord; InitialCount, MaximumCount: Integer; Name: String): LongWord;
  external 'CreateSemaphoreW@kernel32.dll stdcall';
function WaitForSingleObject(Handle, Milliseconds: LongWord): LongWord;
  external 'WaitForSingleObject@kernel32.dll stdcall';
function ReleaseSemaphore(Handle: LongWord; ReleaseCount: Integer; PreviousCount: LongWord): Boolean;
  external 'ReleaseSemaphore@kernel32.dll stdcall';
function CloseHandle(Handle: LongWord): Boolean;
  external 'CloseHandle@kernel32.dll stdcall';
function AcquireAppGate(): Boolean;
var GateName, Profile: String; Handle: LongWord;
begin
  Result := AppGate <> 0;
  if Result then exit;
  SetLength(Profile, 260);
  if SHGetFolderPath(0, $0028, 0, 0, Profile) <> 0 then exit;
  Profile := Copy(Profile, 1, Pos(#0, Profile) - 1);
  if Trim(Profile) = '' then exit;
  GateName := 'Local\Scrobblix.' + Lowercase(GetSHA256OfString(UTF8Encode(Profile)));
  Handle := CreateSemaphore(0, 1, 1, GateName);
  if Handle = 0 then exit;
  if WaitForSingleObject(Handle, 0) = 0 then begin
    AppGate := Handle;
    Result := True;
  end else CloseHandle(Handle);
end;
procedure ReleaseAppGate();
begin
  if AppGate <> 0 then begin
    ReleaseSemaphore(AppGate, 1, 0);
    CloseHandle(AppGate);
    AppGate := 0;
  end;
end;
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if CompareText(RemoveBackslashUnlessRoot(ExpandConstant('{app}')), ExpandConstant('{localappdata}\Programs\Scrobblix')) <> 0 then
    Result := 'This preview installs only to your local Programs\Scrobblix folder. No custom directory is supported.'
  else if not AcquireAppGate() then
    Result := 'Scrobblix is running or its safety gate could not be acquired. Exit Scrobblix from its system tray, then try again. No process will be force-closed.';
end;
procedure CurStepChanged(CurStep: TSetupStep);
begin
  { All payload writes are complete before optional post-install launch. }
  if CurStep = ssPostInstall then ReleaseAppGate();
end;
procedure DeinitializeSetup();
begin ReleaseAppGate(); end;
function InitializeUninstall(): Boolean;
begin
  Result := AcquireAppGate();
  if not Result then
    SuppressibleMsgBox('Exit Scrobblix from its system tray before uninstalling. No process will be force-closed.', mbError, MB_OK, IDOK);
end;
procedure DeinitializeUninstall();
begin ReleaseAppGate(); end;
