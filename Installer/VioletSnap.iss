#define MyAppName "VioletSnap"
#define MyAppEnglishName "VioletSnap"
#define MyAppVersion "1.3.2"
#define MyAppPublisher "Zhou Tian"
#define MyAppExeName "VioletSnap.exe"

[Setup]
AppId={{6FE05B7C-D675-4C65-85BD-8A63C5BBC312}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoVersion=1.3.2.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName}安装程序
VersionInfoCopyright=Copyright © 2026 周天. All rights reserved.
DefaultDirName={localappdata}\Programs\VioletSnap
DefaultGroupName={#MyAppName}
OutputDir=..\安装包
OutputBaseFilename=VioletSnap-Setup-v{#MyAppVersion}-x64
SetupIconFile=Assets\VioletSnap.ico
WizardImageFile=Assets\WizardImage.bmp
WizardSmallImageFile=Assets\WizardSmallImage.bmp
LicenseFile=License.txt
InfoBeforeFile=PrivacyNotice.txt
WizardStyle=modern
WizardSizePercent=110
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
Compression=lzma2/max
SolidCompression=yes
CloseApplications=yes
CloseApplicationsFilter={#MyAppExeName}
RestartApplications=no
DisableProgramGroupPage=yes
DisableReadyMemo=no
AllowNoIcons=yes
Uninstallable=yes
UninstallFilesDir={app}\卸载程序
UninstallDisplayName={#MyAppName} {#MyAppVersion}
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplaySize=190000000
SetupLogging=yes
ChangesAssociations=no

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式："; Flags: unchecked
Name: "startup"; Description: "登录 Windows 后自动启动 VioletSnap"; GroupDescription: "启动选项："; Flags: unchecked

[InstallDelete]
Type: files; Name: "{app}\ZhouTianCapture.exe"
Type: files; Name: "{group}\周天截图.lnk"
Type: files; Name: "{autodesktop}\周天截图.lnk"

[Files]
Source: "..\发布\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"
Source: "License.txt"; DestDir: "{app}\Documents"; DestName: "最终用户许可协议.txt"; Flags: ignoreversion
Source: "PrivacyNotice.txt"; DestDir: "{app}\Documents"; DestName: "隐私与第三方服务说明.txt"; Flags: ignoreversion
Source: "README.txt"; DestDir: "{app}"; DestName: "使用与许可说明.txt"; Flags: ignoreversion
Source: "..\AUTHOR.md"; DestDir: "{app}\Documents"; DestName: "作者与版本信息.md"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}\Documents"; DestName: "GNU GPL v3.txt"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\卸载{#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "VioletSnap"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "ZhouTianCapture"; Flags: deletevalue uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "ScreenshotTool"; Flags: deletevalue uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--install-startup-task"; StatusMsg: "正在配置最高权限开机启动…"; Tasks: startup; Flags: runhidden waituntilterminated
Filename: "{app}\{#MyAppExeName}"; Description: "立即运行{#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ""VioletSnap Startup"" /F"; Flags: runhidden waituntilterminated; RunOnceId: "RemoveVioletSnapStartupTask"

[Code]
procedure InitializeWizard;
begin
  WizardForm.Color := $00FFF8FC;
  WizardForm.MainPanel.Color := $00FFF8FC;
  WizardForm.InnerPage.Color := $00FFF8FC;
  WizardForm.PageNameLabel.Font.Color := $00A84D6F;
  WizardForm.PageDescriptionLabel.Font.Color := $00835B72;
  WizardForm.WelcomeLabel1.Font.Color := $00A84D6F;
  WizardForm.NextButton.Caption := '下一步(&N)';
  WizardForm.CancelButton.Caption := '取消(&C)';
  WizardForm.LicenseAcceptedRadio.Caption := '我已阅读 GPLv3 许可与风险告知并同意继续安装';
  WizardForm.LicenseNotAcceptedRadio.Caption := '我不接受协议';
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpReady then
    WizardForm.NextButton.Caption := '开始安装(&I)'
  else if CurPageID = wpFinished then
    WizardForm.NextButton.Caption := '完成(&F)'
  else
    WizardForm.NextButton.Caption := '下一步(&N)';
end;
