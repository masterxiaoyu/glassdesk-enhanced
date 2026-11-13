; Public user-scope installer. The registration remains stable across releases.
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
CloseApplicationsFilter=GlassDesk.exe
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
Source: "{#StageDirectory}\*"; DestDir: "{app}"; Excludes: "Data\*,*.lease,*.pfx,*.p12,GlassDesk.Identity.msix,GlassDesk.Notification.Test.cer,INSTALL-NOTIFICATIONS.md"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#StageDirectory}\GlassDesk.ico"; DestDir: "{app}"; DestName: "GlassDesk-{#ProductVersion}.ico"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\GlassDesk"; Filename: "{app}\GlassDesk.exe"; WorkingDir: "{app}"; IconFilename: "{app}\GlassDesk-{#ProductVersion}.ico"
Name: "{autodesktop}\GlassDesk"; Filename: "{app}\GlassDesk.exe"; WorkingDir: "{app}"; IconFilename: "{app}\GlassDesk-{#ProductVersion}.ico"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\{#AppIdValue}_is1"; ValueName: "InstallLocation"; ValueType: string; ValueData: "{app}"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\GlassDesk.exe"; WorkingDir: "{app}"; Description: "立即运行 GlassDesk"; Flags: postinstall nowait skipifsilent; Check: IsInteractiveInstall
Filename: "{app}\GlassDesk.exe"; Parameters: "--autostart"; WorkingDir: "{app}"; Flags: nowait runasoriginaluser; Check: IsGlassDeskUpdate

[Code]
var
  DataPage: TInputOptionWizardPage;
  DeleteDataOnUninstall: Boolean;

function GetFileAttributesW(FileName: String): LongWord;
  external 'GetFileAttributesW@kernel32.dll stdcall';

function DataTreeHasLinks(const Path: String): Boolean;
var
  Entry: TFindRec;
  Child: String;
begin
  Result := (GetFileAttributesW(Path) and $400) <> 0;
  if Result then Exit;
  if not FindFirst(AddBackslash(Path) + '*', Entry) then Exit;
  try
    repeat
      if (Entry.Name <> '.') and (Entry.Name <> '..') then
      begin
        Child := AddBackslash(Path) + Entry.Name;
        // 不跟随目录联接或符号链接，避免清除安装目录之外的文件。
        Result := (Entry.Attributes and $400) <> 0;
        if not Result and ((Entry.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0) then
          Result := DataTreeHasLinks(Child);
        if Result then Exit;
      end;
    until not FindNext(Entry);
  finally
    FindClose(Entry);
  end;
end;

function ClearInstallationData: Boolean;
var
  AppPath, DataPath: String;
begin
  Result := False;
  AppPath := RemoveBackslashUnlessRoot(ExpandFileName(ExpandConstant('{app}')));
  if CompareText(AddBackslash(AppPath),
      AddBackslash(ExtractFileDrive(AppPath))) = 0 then Exit;
  DataPath := ExpandFileName(AddBackslash(AppPath) + 'Data');
  // 删除范围只允许当前安装目录的 Data，不接受联接后的目录树。
  if CompareText(ExtractFileDir(DataPath), AppPath) <> 0 then Exit;
  if not DirExists(DataPath) then
  begin
    Result := True;
    Exit;
  end;
  if (GetFileAttributesW(AppPath) and $400) <> 0 then Exit;
  if DataTreeHasLinks(DataPath) then Exit;
  Result := DelTree(DataPath, True, True, True);
end;

procedure InitializeWizard;
begin
  DataPage := CreateInputOptionPage(wpSelectDir, '用户数据',
    '选择安装后的数据状态',
    '升级时建议保留设置、应用库和布局。首次安装无需更改。',
    True, False);
  DataPage.Add('保留现有设置和应用布局（推荐）');
  DataPage.Add('清除用户数据，恢复初始状态');
  DataPage.SelectedValueIndex := 0;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID <> DataPage.ID then Exit;
  if DataPage.SelectedValueIndex <> 1 then Exit;
  if not DirExists(ExpandConstant('{app}\Data')) then Exit;
  Result := MsgBox('清除后，当前安装目录中的全部用户数据将永久删除，' +
    '包括设置、应用库、布局、自定义图标和本地备份。' + #13#10 +
    '请先确认已备份。是否继续？', mbConfirmation,
    MB_YESNO or MB_DEFBUTTON2) = IDYES;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  // 只有用户在向导中明确选择清除，才删除当前安装目录的 Data。
  if CurStep <> ssInstall then Exit;
  if WizardSilent then Exit;
  if DataPage.SelectedValueIndex <> 1 then Exit;
  if not ClearInstallationData then
    RaiseException('用户数据未清除，安装已停止。请检查文件占用或目录链接。');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    // 静默卸载默认保留；交互卸载只有选“是”才会清除 Data。
    if not UninstallSilent then
      DeleteDataOnUninstall :=
        MsgBox('是否同时永久删除当前安装目录中的全部用户数据？' + #13#10 +
          '包括设置、应用库、布局、自定义图标和本地备份。' + #13#10 +
          '默认保留用户数据，重新安装后可继续使用。', mbConfirmation,
          MB_YESNO or MB_DEFBUTTON2) = IDYES;
  end;
  if (CurUninstallStep = usPostUninstall) and DeleteDataOnUninstall then
    if not ClearInstallationData then
      MsgBox('程序已卸载，用户数据未清除。请检查文件占用或目录链接。',
        mbError, MB_OK);
end;

function IsGlassDeskUpdate: Boolean;
begin
  Result := ExpandConstant('{param:GLASSDESKUPDATE|0}') = '1';
end;

function IsInteractiveInstall: Boolean;
begin
  Result := not IsGlassDeskUpdate;
end;
