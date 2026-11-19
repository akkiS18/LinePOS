; Script generated for Inno Setup 7
#define MyAppName "SMART kassa"
#define MyAppVersion "1.1.2"
#define MyAppPublisher "Line Soft"
#define MyAppExeName "PosElectro.Desktop.exe"

[Setup]
AppId={{D37E8450-4F8A-4BC6-8AA9-7F32D065C7A1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\LineKassa
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=Output
OutputBaseFilename=Line_kassa_Desktop_Setup
; Windows ikonka keshini yangilaydi (taskbar/Start eski logoni ko'rsatmasin)
ChangesAssociations=yes
SetupIconFile=PosElectro.Desktop\Assets\app_logo.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "PosElectro.Desktop\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; 1.0.1 o'rnatuvchisi {app}\Assets ga eski logoni yozgan, keyingilari uni yangilamagan edi — yorliqlar eski ikonkani ko'rsatardi.
Source: "PosElectro.Desktop\Assets\app_logo.ico"; DestDir: "{app}\Assets"; Flags: ignoreversion
Source: "PosElectro.Desktop\Assets\app_logo.png"; DestDir: "{app}\Assets"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; IconFilename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
