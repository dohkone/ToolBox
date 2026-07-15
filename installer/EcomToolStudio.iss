#define MyAppName "EcomTool"
#define MyAppVersion GetEnv("ECOMTOOL_APP_VERSION")
#define MyAppPublisher "EcomTool"
#define MyAppExeName "EcomTool.exe"
#define SourceDir GetEnv("ECOMTOOL_INSTALL_SOURCE")
#define OutputDir GetEnv("ECOMTOOL_INSTALL_OUTPUT")

[Setup]
AppId={{D0F45B2B-74AF-456D-83BB-5B0E25E1E3F4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=EcomTool_Setup_{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=..\logo\new_logo.ico

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加选项："; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
Type: files; Name: "{app}\EcomTool Studio.exe"
Type: files; Name: "{app}\EcomTool Studio.dll"
Type: files; Name: "{app}\EcomTool Studio.pdb"
Type: files; Name: "{app}\EcomTool Studio.deps.json"
Type: files; Name: "{app}\EcomTool Studio.runtimeconfig.json"
Type: files; Name: "{app}\EcomTool Studio.exe.config"
Type: files; Name: "{autodesktop}\EcomTool Studio.lnk"
Type: files; Name: "{commondesktop}\EcomTool Studio.lnk"
Type: files; Name: "{userdesktop}\EcomTool Studio.lnk"
Type: files; Name: "{autoprograms}\EcomTool Studio\EcomTool Studio.lnk"
Type: files; Name: "{autoprograms}\EcomTool Studio\卸载 EcomTool Studio.lnk"
Type: dirifempty; Name: "{autoprograms}\EcomTool Studio"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\卸载 {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 {#MyAppName}"; Flags: nowait postinstall skipifsilent
