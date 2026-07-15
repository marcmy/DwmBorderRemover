#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif

#define MyAppName "DWM Border Remover"
#define MyAppPublisher "Marc"
#define MyAppURL "https://github.com/marcmy/DwmBorderRemover"
#define MyAppExeName "DwmBorderRemover.exe"
#define AppMutexName "Local\DwmBorderRemover-58E7C65D-4DF5-41CF-9BC3-4EC77F352A61"

[Setup]
AppId={{D6215FB9-5DE4-4F89-8E87-A57454BB9B63}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={localappdata}\Programs\DWM Border Remover
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\artifacts\installer
OutputBaseFilename=DwmBorderRemover-Setup-{#MyAppVersion}
SetupIconFile=..\src\DwmBorderRemover\Assets\Setup.ico
UninstallDisplayIcon={app}\DwmBorderRemover.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=no
RestartApplications=no
SetupLogging=yes
MinVersion=10.0.22000

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: checkedonce

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\DwmBorderRemover.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\DwmBorderRemover.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--background"; Flags: nowait runhidden; Check: RestartPreviouslyRunningApp
Filename: "{app}\{#MyAppExeName}"; Parameters: "--background"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent; Check: OfferLaunchAfterInstall

[UninstallRun]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--restore-and-exit"; Flags: runhidden waituntilterminated skipifdoesntexist; RunOnceId: "RestoreAndExit"

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\DwmBorderRemover"
Type: filesandordirs; Name: "{app}"

[Code]
var
  AppWasRunningBeforeInstall: Boolean;

function WaitForAppToStop(TimeoutMilliseconds: Integer): Boolean;
var
  Elapsed: Integer;
begin
  Elapsed := 0;
  while CheckForMutexes('{#AppMutexName}') and (Elapsed < TimeoutMilliseconds) do
  begin
    Sleep(100);
    Elapsed := Elapsed + 100;
  end;

  Result := not CheckForMutexes('{#AppMutexName}');
end;

function StopRunningApp: Boolean;
var
  AppPath: String;
  TaskKillPath: String;
  ResultCode: Integer;
begin
  Result := True;

  if not CheckForMutexes('{#AppMutexName}') then
    Exit;

  AppPath := ExpandConstant('{app}\{#MyAppExeName}');
  if FileExists(AppPath) then
  begin
    Log('Requesting a graceful DWM Border Remover shutdown through its IPC channel.');
    if Exec(AppPath, '--exit', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      if WaitForAppToStop(7000) then
        Exit;
    end
    else
      Log(Format('Unable to launch the app shutdown command. Result code: %d', [ResultCode]));
  end;

  Log('The app did not exit gracefully; attempting a forced shutdown before updating files.');
  TaskKillPath := ExpandConstant('{sys}\taskkill.exe');
  if FileExists(TaskKillPath) then
  begin
    Exec(
      TaskKillPath,
      '/IM "{#MyAppExeName}" /T /F',
      '',
      SW_HIDE,
      ewWaitUntilTerminated,
      ResultCode);

    if WaitForAppToStop(3000) then
      Exit;
  end;

  Result := False;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  NeedsRestart := False;
  AppWasRunningBeforeInstall := CheckForMutexes('{#AppMutexName}');

  if AppWasRunningBeforeInstall and not StopRunningApp then
    Result :=
      'DWM Border Remover is still running and could not be closed. ' +
      'Close it from Task Manager, then click Try Again.'
  else
    Result := '';
end;

function RestartPreviouslyRunningApp: Boolean;
begin
  Result := AppWasRunningBeforeInstall;
end;

function OfferLaunchAfterInstall: Boolean;
begin
  Result := not AppWasRunningBeforeInstall;
end;
