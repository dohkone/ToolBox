#define MyAppName "EcomTool Update"
#define MyAppVersion GetEnv("ECOMTOOL_APP_VERSION")
#define MyAppPublisher "EcomTool"
#define OutputDir GetEnv("ECOMTOOL_INSTALL_OUTPUT")
#define FullInstallerUrl GetEnv("ECOMTOOL_FULL_INSTALLER_URL")
#define FullInstallerName GetEnv("ECOMTOOL_FULL_INSTALLER_NAME")
#define FullInstallerSize GetEnv("ECOMTOOL_FULL_INSTALLER_SIZE")

[Setup]
AppId={{6B8D1D1D-A6B2-4C57-9C4A-81AF982989EF}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
CreateAppDir=no
Uninstallable=no
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=EcomTool_Update_{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
SetupIconFile=..\logo\new_logo.ico

[Files]
Source: "{#FullInstallerUrl}"; DestName: "{#FullInstallerName}"; DestDir: "{tmp}"; ExternalSize: {#FullInstallerSize}; Flags: external download ignoreversion

[Run]
Filename: "{tmp}\{#FullInstallerName}"; Description: "启动 EcomTool 安装程序"; Flags: nowait postinstall skipifsilent
