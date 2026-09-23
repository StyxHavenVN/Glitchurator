[Setup]
AppId=StyxHavenVN.SliderPicturator
AppName=Glitchurator
AppVersion=1.1.0
AppPublisher=Styx (StyxHavenVN)
DefaultDirName={localappdata}\Programs\StyxSliderPicturator
DefaultGroupName=Glitchurator
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\setup
OutputBaseFilename=Glitchurator-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
DisableDirPage=yes
UninstallDisplayIcon={app}\Glitchurator.exe
CloseApplications=yes
[Files]
Source: "..\artifacts\glitchurator-payload\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[InstallDelete]
Type: files; Name: "{app}\StandalonePicturator.exe"
Type: files; Name: "{app}\StandalonePicturator.dll"
Type: files; Name: "{app}\StandalonePicturator.deps.json"
Type: files; Name: "{app}\StandalonePicturator.runtimeconfig.json"
Type: files; Name: "{userdesktop}\Styx Slider Picturator.lnk"
Type: files; Name: "{userprograms}\Styx Slider Picturator\Styx Slider Picturator.lnk"
[Icons]
Name: "{userdesktop}\Glitchurator"; Filename: "{app}\Glitchurator.exe"
Name: "{group}\Glitchurator"; Filename: "{app}\Glitchurator.exe"
[Run]
Filename: "{app}\Glitchurator.exe"; Description: "Open Glitchurator"; Flags: nowait postinstall skipifsilent

