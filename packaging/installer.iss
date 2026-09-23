[Setup]
AppId=StyxHavenVN.SliderPicturator
AppName=Styx Slider Picturator & Sliderball
AppVersion=1.0.0
AppPublisher=Styx (StyxHavenVN)
DefaultDirName={localappdata}\Programs\StyxSliderPicturator
DefaultGroupName=Styx Slider Picturator
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\setup
OutputBaseFilename=StyxSliderPicturator-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
DisableDirPage=yes
UninstallDisplayIcon={app}\StandalonePicturator.exe
CloseApplications=yes
[Files]
Source: "..\artifacts\installer-payload\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{userdesktop}\Styx Slider Picturator"; Filename: "{app}\StandalonePicturator.exe"
Name: "{group}\Styx Slider Picturator"; Filename: "{app}\StandalonePicturator.exe"
[Run]
Filename: "{app}\StandalonePicturator.exe"; Description: "Open Styx Slider Picturator"; Flags: nowait postinstall skipifsilent
