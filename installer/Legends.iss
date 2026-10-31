; Inno Setup script for the Windows installer: Temnorid-Legends-Setup.exe
; Build the player first (Unity: Temnorid > Legends > Build Windows Player), then compile this with Inno Setup:
;   "ISCC.exe" installer\Legends.iss
; The installer is written to Builds\Installer.

#define AppName "Temnorid Legends"
#define AppVersion "0.2.0"
#define AppExe "TemnoridLegends.exe"

[Setup]
AppId={{7A3F9C20-6B1D-4E55-8A47-2D9E0B6C3F18}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Temnorid
AppPublisherURL=https://legends.temnorid.com
DefaultDirName={autopf}\Temnorid Legends
DefaultGroupName=Temnorid Legends
DisableProgramGroupPage=yes
OutputDir=..\Builds\Installer
OutputBaseFilename=Temnorid-Legends-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#AppExe}
WizardStyle=modern

[Files]
Source: "..\Builds\Windows\TemnoridLegends\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
; No skipifsilent: when the game updates itself the installer runs silently and must start the game again.
Filename: "{app}\{#AppExe}"; Description: "Start {#AppName}"; Flags: nowait postinstall
