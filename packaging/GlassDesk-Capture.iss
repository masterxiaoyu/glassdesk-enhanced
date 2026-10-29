; User-scope x64 installer for the local GlassDesk candidate.
#ifndef AppIdValue
  #error AppIdValue is required.
#endif
#if AppIdValue != "GlassDesk.Independent"
  #error This installer must use the standard GlassDesk registration.
#endif
#ifndef ProductVersion
  #error ProductVersion is required.
#endif
#ifndef FileVersion
  #error FileVersion is required.
#endif
#ifndef PackageBaseName
  #error PackageBaseName is required.
#endif
#define StageDirectory GetEnv("GLASSDESK_PACKAGE_STAGE")
#if StageDirectory == ""
  #error GLASSDESK_PACKAGE_STAGE is required.
#endif

[Setup]
AppId={#AppIdValue}
AppName=GlassDesk
AppVersion={#ProductVersion}
AppVerName=GlassDesk {#ProductVersion}
DefaultDirName={localappdata}\Programs\GlassDesk
DefaultGroupName=GlassDesk
DisableDirPage=no
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
SetupArchitecture=x64
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
UsePreviousAppDir=yes
CloseApplications=yes
RestartApplications=no
MinVersion=10.0.22000
UninstallDisplayName=GlassDesk
UninstallDisplayIcon={app}\GlassDesk.exe
SetupIconFile=..\src\GlassDesk.App\Assets\GlassDesk.ico
AppPublisher=masterxiaoyu（GlassDesk 非官方修改版）
AppPublisherURL=https://github.com/masterxiaoyu/glassdesk-enhanced
AppSupportURL=https://github.com/masterxiaoyu/glassdesk-enhanced/issues
AppUpdatesURL=https://github.com/masterxiaoyu/glassdesk-enhanced/releases
AppComments=非官方修改版。原作者 everlosttime；原项目 https://github.com/everlosttime/glassdesk-open-source
InfoBeforeFile=INSTALLER-NOTICE.txt
VersionInfoProductName=GlassDesk
VersionInfoProductVersion={#FileVersion}
VersionInfoVersion={#FileVersion}
VersionInfoTextVersion={#FileVersion}
VersionInfoProductTextVersion={#ProductVersion}
OutputBaseFilename={#PackageBaseName}-Setup
Compression=lzma2/normal
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加快捷方式:"; Flags: unchecked

[Files]
Source: "{#StageDirectory}\*"; DestDir: "{app}"; Excludes: "Data\*"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#StageDirectory}\GlassDesk.ico"; DestDir: "{app}"; DestName: "GlassDesk-{#ProductVersion}.ico"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\GlassDesk"; Filename: "{app}\GlassDesk.exe"; WorkingDir: "{app}"; IconFilename: "{app}\GlassDesk-{#ProductVersion}.ico"
Name: "{autodesktop}\GlassDesk"; Filename: "{app}\GlassDesk.exe"; WorkingDir: "{app}"; IconFilename: "{app}\GlassDesk-{#ProductVersion}.ico"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\{#AppIdValue}_is1"; ValueName: "InstallLocation"; ValueType: string; ValueData: "{app}"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\GlassDesk.exe"; Parameters: "--autostart"; WorkingDir: "{app}"; Flags: nowait runasoriginaluser; Check: IsGlassDeskUpdate

[Code]
var
  DataPage: TInputOptionWizardPage;
  DeleteDataOnUninstall: Boolean;

procedure InitializeWizard;
begin
  DataPage := CreateInputOptionPage(wpSelectDir, '保留用户数据',
    '选择如何处理已有的 GlassDesk Data',
    'Data 包含设置、应用库和布局；首次安装时此选项不产生影响。',
    True, False);
  DataPage.Add('保留已有 Data（推荐）');
  DataPage.Add('清除已有 Data（设置、应用库和布局将永久删除）');
  DataPage.SelectedValueIndex := 0;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  // 只有用户在向导中明确选择清除，才删除当前安装目录的 Data。
  if CurStep <> ssInstall then Exit;
  if WizardSilent then Exit;
  if DataPage.SelectedValueIndex <> 1 then Exit;
  if DirExists(ExpandConstant('{app}\Data')) then
    if not DelTree(ExpandConstant('{app}\Data'), True, True, True) then
      RaiseException('无法清除旧 Data；安装已中止。');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    // 静默卸载默认保留；交互卸载只有选“是”才会清除 Data。
    if not UninstallSilent then
      DeleteDataOnUninstall :=
        MsgBox('是否同时永久删除 GlassDesk Data（设置、应用库和布局）？' + #13#10 +
          '选择“否”会保留 Data。', mbConfirmation,
          MB_YESNO or MB_DEFBUTTON2) = IDYES;
  end;
  if (CurUninstallStep = usPostUninstall) and DeleteDataOnUninstall then
    if DirExists(ExpandConstant('{app}\Data')) then
      if not DelTree(ExpandConstant('{app}\Data'), True, True, True) then
        MsgBox('卸载已完成，但 Data 未能清除。', mbError, MB_OK);
end;

function IsGlassDeskUpdate: Boolean;
begin
  Result := ExpandConstant('{param:GLASSDESKUPDATE|0}') = '1';
end;
