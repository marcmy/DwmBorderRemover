#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif

#define MyAppName "DWM Border Remover"
#define MyAppPublisher "Marc"
#define MyAppURL "https://github.com/marcmy/DwmBorderRemover"
#define MyAppExeName "DwmBorderRemover.exe"

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
SetupIconFile=..\src\DwmBorderRemover\Assets\App.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
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
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--background"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--restore-and-exit"; Flags: runhidden waituntilterminated skipifdoesntexist; RunOnceId: "RestoreAndExit"

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\DwmBorderRemover"
Type: filesandordirs; Name: "{app}"
