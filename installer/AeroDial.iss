; AeroDial installer (Inno Setup 6)
;
; Build with:
;   dotnet publish src\AeroDial\AeroDial.csproj -c Release -r win-x64
;   ISCC.exe installer\AeroDial.iss
; or just run installer\build-installer.ps1, which does both.
;
; Installs per user so no admin prompt is needed. That matters beyond
; convenience: the app's "start with Windows" setting writes to HKCU\...\Run
; pointing at the install path, so a per-machine install under Program Files
; would leave every other user with a Run entry they cannot manage.

#define AppName        "AeroDial"
#define AppPublisher   "3M Design Solutions"
#define AppAuthor      "Muhtasim Mahbub"
#define AppURL         "https://github.com/mmatul06/AeroDial"
#define AppExeName     "AeroDial.exe"
#define SourceDir      "..\src\AeroDial\bin\Release\net9.0-windows10.0.26100.0\win-x64\publish"

; The version is read out of the published exe rather than repeated here, so it
; can never drift from the csproj. GetFileVersion gives "3.0.1.0"; trim the
; trailing build field to match the release tag. Override with /DAppVersion=x.y.z.
#ifndef AppVersion
  #define FullVersion GetFileVersion(SourceDir + "\" + AppExeName)
  #if FullVersion == ""
    #error Publish the Release build first: dotnet publish src\AeroDial\AeroDial.csproj -c Release -r win-x64
  #endif
  #define AppVersion Copy(FullVersion, 1, RPos(".", FullVersion) - 1)
#endif

[Setup]
; Never change AppId: it is how Windows recognises an existing install and
; upgrades it in place rather than stacking a second entry in Apps & features.
AppId={{2F7A5C34-8B1E-4D6A-9F03-1C4E8A2D7B65}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Setup
VersionInfoCopyright=Copyright (c) 2026 {#AppAuthor}

DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
AllowNoIcons=yes
LicenseFile=..\LICENSE
OutputDir=..\dist
OutputBaseFilename={#AppName}-{#AppVersion}-Setup
SetupIconFile=..\src\AeroDial\Assets\aerodial.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
WizardStyle=modern
; The payload is one already-compressed 110 MB exe, so heavy recompression costs
; minutes of build time and saves almost nothing.
Compression=lzma2/fast
SolidCompression=no

; The published exe is win-x64 and self-contained; refuse to install it
; anywhere it cannot run rather than failing later with no explanation.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Windows 10 2004 (19041) is the app's TargetPlatformMinVersion.
MinVersion=10.0.19041

PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; AeroDial is single-instance and lives in the tray with no window, so an
; upgrade would otherwise fail on a locked exe with nothing visible to close.
; Naming the mutex lets Setup spot it and ask the user to quit it first.
AppMutex=Global\AeroDial_Instance_3MDS
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "startupicon"; Description: "Start {#AppName} automatically when I sign in"; GroupDescription: "Startup:"

[Files]
Source: "{#SourceDir}\{#AppExeName}";        DestDir: "{app}"; Flags: ignoreversion
; Shipped beside the exe so the settings window can use the real icon: a
; single-file publish keeps nothing on disk for it to load.
Source: "..\src\AeroDial\Assets\aerodial.ico"; DestDir: "{app}\Assets"; Flags: ignoreversion
Source: "..\LICENSE";                          DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}";           Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";     Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
const
  RunKey   = 'SOFTWARE\Microsoft\Windows\CurrentVersion\Run';
  RunValue = 'AeroDial';

{ The app's "Start with Windows" setting writes the exe path into HKCU Run.
  Leaving that behind after an uninstall means Windows tries to launch a
  deleted file at every sign-in, so clear it, but only when it still points
  into the directory being removed, in case a copy elsewhere is in use. }
procedure RemoveAutostartEntry;
var
  Existing, AppPath: String;
begin
  if not RegQueryStringValue(HKEY_CURRENT_USER, RunKey, RunValue, Existing) then
    Exit;

  AppPath := RemoveBackslash(ExpandConstant('{app}'));
  if Pos(Uppercase(AppPath), Uppercase(Existing)) > 0 then
    RegDeleteValue(HKEY_CURRENT_USER, RunKey, RunValue);
end;

{ Menus, profiles, themes and settings live in %AppData%\AeroDial because the
  app writes them at runtime, so they survive an uninstall unless removed
  deliberately. Ask rather than assume: someone reinstalling to fix a problem
  wants their dial back exactly as it was. }
procedure RemoveSettingsIfWanted;
var
  SettingsDir: String;
begin
  SettingsDir := ExpandConstant('{userappdata}\{#AppName}');
  if not DirExists(SettingsDir) then
    Exit;

  if SuppressibleMsgBox('Also remove your AeroDial menus, themes and settings?' + #13#10 +
       'Choose No to keep them for a reinstall.',
       mbConfirmation, MB_YESNO or MB_DEFBUTTON2, IDNO) = IDYES then
    DelTree(SettingsDir, True, True, True);
end;

procedure CurUninstallStepChanged(CurStep: TUninstallStep);
begin
  if CurStep = usPostUninstall then
  begin
    RemoveAutostartEntry;
    RemoveSettingsIfWanted;
  end;
end;

{ Two jobs after install:
  - the startup task writes the Run entry, so the app starts at sign-in even
    before the user opens Settings;
  - an install into a new location leaves an old Run entry pointing at the
    previous path, so refresh it rather than silently launching a stale copy. }
procedure CurStepChanged(CurStep: TSetupStep);
var
  Existing, Target: String;
begin
  if CurStep <> ssPostInstall then
    Exit;

  Target := '"' + ExpandConstant('{app}\{#AppExeName}') + '"';

  if WizardIsTaskSelected('startupicon') then
    RegWriteStringValue(HKEY_CURRENT_USER, RunKey, RunValue, Target)
  else if RegQueryStringValue(HKEY_CURRENT_USER, RunKey, RunValue, Existing) then
    RegWriteStringValue(HKEY_CURRENT_USER, RunKey, RunValue, Target);
end;
