#ifndef AppVersion
  #define AppVersion "0.7.2"
#endif
#ifndef PayloadDir
  #error PayloadDir is required
#endif
#ifndef OutputPath
  #error OutputPath is required
#endif
#ifndef InstallerRevision
  #define InstallerRevision "1"
#endif
#define FFmpegVersion "8.1.3-Jellyfin"
#define FFmpegArchive "jellyfin-ffmpeg_8.1.3-1_portable_win64-clang-gpl.zip"
#define FFmpegSHA "3691f8b2d4c39c511b5faff1c8c4687bd782dac823e63ba69f0755f7ce1af9ce"
#ifndef FFmpegURL
  #define FFmpegURL "https://mirrors.nju.edu.cn/jellyfin/files/ffmpeg/windows/8.x/8.1.3-1/win64/" + FFmpegArchive
#endif
#ifndef FFmpegFallbackURL
  #define FFmpegFallbackURL "https://repo.jellyfin.org/files/ffmpeg/windows/8.x/8.1.3-1/win64/" + FFmpegArchive
#endif
#ifndef FFmpegLastURL
  #define FFmpegLastURL "https://github.com/jellyfin/jellyfin-ffmpeg/releases/download/v8.1.3-1/" + FFmpegArchive
#endif
#ifdef TestInstall
  #define ProductName "晾图安装验证"
  #define ProductId "{{9643F797-0668-459F-9BD8-F42EC65E1233}"
  #define OutputName "Liangtu-" + AppVersion + "-InstallerTest-x64-r" + InstallerRevision
  #define RunValueName "LiangtuInstallerTest"
#else
  #define ProductName "晾图"
  #define ProductId "{{570C3FBA-FB17-4CB3-AC99-2F7686489374}"
  #define OutputName "Liangtu-" + AppVersion + "-Setup-x64-r" + InstallerRevision
  #define RunValueName "Liangtu"
#endif
#ifdef TestSuffix
  #define FinalOutputName OutputName + "-" + TestSuffix
#else
  #define FinalOutputName OutputName
#endif

[Setup]
AppId={#ProductId}
AppName={#ProductName}
AppVersion={#AppVersion}
AppVerName={#ProductName} {#AppVersion}（安装器 r{#InstallerRevision}）
AppPublisher=晾图 Windows 移植版
DefaultDirName={localappdata}\Programs\Liangtu
DefaultGroupName={#ProductName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible and not arm64
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
WizardStyle=modern dynamic
WizardSizePercent=110
SetupIconFile=..\src\Liangtu\Assets\app.ico
UninstallDisplayIcon={app}\Liangtu.exe
OutputDir={#OutputPath}
OutputBaseFilename={#FinalOutputName}
Compression=lzma2/max
SolidCompression=yes
LZMAUseSeparateProcess=yes
ArchiveExtraction=full
AppMutex={code:GetAppMutex}
CloseApplications=no
RestartApplications=no
SetupLogging=yes
UninstallLogging=yes
UsePreviousAppDir=yes
InfoBeforeFile=installer-info.txt
VersionInfoVersion={#AppVersion}.{#InstallerRevision}
VersionInfoDescription=晾图安装程序
VersionInfoProductName=晾图
VersionInfoProductVersion={#AppVersion}
Uninstallable=yes

[Languages]
Name: "chinesesimplified"; MessagesFile: "languages\ChineseSimplified.isl"

[Types]
Name: "full"; Description: "图片与视频功能（安装时下载视频组件）"
Name: "basic"; Description: "仅图片功能（可离线安装）"
Name: "custom"; Description: "自定义"; Flags: iscustom

[Components]
Name: "core"; Description: "晾图主程序与 .NET 运行库"; Types: full basic custom; Flags: fixed
Name: "video"; Description: "视频缩略图与裁剪（国内镜像，下载约 65 MB）"; Types: full; ExtraDiskSpaceRequired: 190000000

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式："; Flags: unchecked

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: core
Source: "{tmp}\ffmpeg-extracted\ffmpeg.exe"; DestDir: "{app}\tools"; Flags: external ignoreversion; ExternalSize: 92391424; Components: video
Source: "{tmp}\ffmpeg-extracted\ffprobe.exe"; DestDir: "{app}\tools"; Flags: external ignoreversion; ExternalSize: 92167168; Components: video
Source: "licenses\FFmpeg-GPLv3.txt"; DestDir: "{app}\licenses\FFmpeg"; DestName: "LICENSE.txt"; Flags: ignoreversion; Components: video
Source: "video-component-readme.txt"; DestDir: "{app}\licenses\FFmpeg"; DestName: "README.txt"; Flags: ignoreversion; Components: video

[Icons]
Name: "{group}\{#ProductName}"; Filename: "{app}\Liangtu.exe"; Parameters: "--show"; WorkingDir: "{app}"
Name: "{group}\使用说明"; Filename: "{app}\使用说明.txt"
Name: "{group}\卸载{#ProductName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#ProductName}"; Filename: "{app}\Liangtu.exe"; Parameters: "--show"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Liangtu.exe"; Parameters: "--show"; Description: "启动晾图"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[Code]
var
  DownloadPage: TDownloadWizardPage;
  VideoPrepared: Boolean;
  RemoveUserData: Boolean;

function DataRoot: String;
begin
#ifdef TestInstall
  Result := '{#TestDataRoot}';
#else
  Result := ExpandConstant('{localappdata}\Liangtu');
#endif
end;

function GetAppMutex(Param: String): String;
begin
  Result := 'Local\Liangtu.' + Uppercase(Copy(GetSHA256OfString(Utf8Encode(Uppercase(DataRoot))), 1, 16));
end;

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage('下载视频组件', '优先使用国内镜像，失败后自动切换官方源。所有来源均校验 SHA-256。', nil);
  DownloadPage.ShowBaseNameInsteadOfUrl := True;
end;

procedure DownloadVideoComponent(ArchivePath: String);
var
  Urls, Names: array of String;
  I: Integer;
  LastError: String;
begin
  SetArrayLength(Urls, 3); SetArrayLength(Names, 3);
  Urls[0] := '{#FFmpegURL}'; Names[0] := '南京大学镜像';
  Urls[1] := '{#FFmpegFallbackURL}'; Names[1] := 'Jellyfin 官方源';
  Urls[2] := '{#FFmpegLastURL}'; Names[2] := 'GitHub 官方发布';
  LastError := '';
  for I := 0 to 2 do begin
    if Urls[I] <> '' then begin
      DownloadPage.Clear;
      DownloadPage.SetText('正在下载：' + Names[I], '下载约 65 MB；失败后自动尝试下一来源。');
      DownloadPage.Add(Urls[I], '{#FFmpegArchive}', '{#FFmpegSHA}');
      Log('Video source attempt ' + IntToStr(I + 1) + ': ' + Urls[I]);
      try
        DownloadPage.Download;
        { Explicitly keep the same integrity boundary for every source. }
        if CompareText(GetSHA256OfFile(ArchivePath), '{#FFmpegSHA}') <> 0 then RaiseException('视频组件 SHA-256 不匹配。');
        Log('Video source verified: ' + Names[I]);
        exit;
      except
        LastError := GetExceptionMessage;
        if DownloadPage.AbortedByUser then RaiseException('已取消视频组件下载。');
        Log('Video source failed: ' + Names[I] + ': ' + LastError);
        DeleteFile(ArchivePath);
      end;
    end;
  end;
  RaiseException('所有下载源均未成功。' + #13#10 + LastError);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  ArchivePath, LocalArchive, Error: String;
begin
  Result := True;
  if (CurPageID <> wpReady) or not WizardIsComponentSelected('video') or VideoPrepared then exit;
  DownloadPage.Clear;
  DownloadPage.Show;
  try
    try
      ArchivePath := ExpandConstant('{tmp}\{#FFmpegArchive}');
      LocalArchive := ExpandConstant('{param:VIDEOARCHIVE|}');
      if LocalArchive = '' then begin
        LocalArchive := AddBackslash(ExtractFilePath(ExpandConstant('{srcexe}'))) + '{#FFmpegArchive}';
        if not FileExists(LocalArchive) then LocalArchive := '';
      end;
      if LocalArchive <> '' then begin
        if not CopyFile(LocalArchive, ArchivePath, False) then RaiseException('无法读取指定的视频组件文件。');
        if CompareText(GetSHA256OfFile(ArchivePath), '{#FFmpegSHA}') <> 0 then RaiseException('视频组件校验失败，请重新下载官方版本。');
        Log('Video component loaded from verified local archive.');
      end else begin
        DownloadVideoComponent(ArchivePath);
      end;
      DownloadPage.SetText('正在解压视频组件…', '请稍候');
      ExtractArchive(ArchivePath, ExpandConstant('{tmp}\ffmpeg-extracted'), '', True, nil);
      if not FileExists(ExpandConstant('{tmp}\ffmpeg-extracted\ffmpeg.exe')) or not FileExists(ExpandConstant('{tmp}\ffmpeg-extracted\ffprobe.exe')) then RaiseException('视频组件目录不完整。');
      VideoPrepared := True;
    except
      Error := GetExceptionMessage;
      Log('Video component preparation failed: ' + Error);
      if not DownloadPage.AbortedByUser then
        SuppressibleMsgBox('视频组件下载或校验失败：' + #13#10 + Error + #13#10#13#10 + '可重试，或将官方组件 ZIP 放在安装程序同目录后重新安装。也可返回“选择组件”取消视频组件，先安装图片功能。', mbError, MB_OK, IDOK);
      Result := False;
    end;
  finally
    DownloadPage.Hide;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Existing: String;
begin
  if CurStep = ssPostInstall then begin
    { Preserve an existing opt-in, but never enable startup by default. }
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#RunValueName}', Existing) then
      if CompareText(ExtractFileName(RemoveQuotes(Existing)), 'Liangtu.exe') = 0 then
        RegWriteStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#RunValueName}', '"' + ExpandConstant('{app}\Liangtu.exe') + '"');
  end;
end;

function InitializeUninstall: Boolean;
var
  Form: TSetupForm;
  Explanation, Location: TNewStaticText;
  KeepChoice, DeleteChoice: TNewRadioButton;
  ContinueButton, CancelButton: TNewButton;
begin
  Result := False;
  RemoveUserData := False;
  if CheckForMutexes(GetAppMutex('')) then begin
    SuppressibleMsgBox('请先保存编辑内容，并从托盘退出晾图后再卸载。', mbInformation, MB_OK, IDOK);
    exit;
  end;
  if UninstallSilent then begin
    { Explicit opt-in only. Unknown or missing values always preserve data. }
    RemoveUserData := CompareText(ExpandConstant('{param:REMOVEDATA|no}'), 'yes') = 0;
    Result := True;
    exit;
  end;
  { At InitializeUninstall this constructor applies the initial dialog DPI/font
    scaling itself. Supplying ScaleX/ScaleY here would scale the outer form twice. }
  Form := CreateCustomForm(480, 250, True, True);
  try
    Form.Caption := '卸载晾图 — 保留设置和图片';
    Form.BorderStyle := bsDialog;
    Explanation := TNewStaticText.Create(Form);
    Explanation.Parent := Form; Explanation.Left := ScaleX(20); Explanation.Top := ScaleY(18);
    Explanation.Width := ScaleX(440); Explanation.Height := ScaleY(42); Explanation.AutoSize := False; Explanation.WordWrap := True;
    Explanation.Caption := '卸载程序后，要保留软件设置和收件箱图片吗？' + #13#10 + '外部文件夹中的原图不会被删除。';
    KeepChoice := TNewRadioButton.Create(Form);
    KeepChoice.Parent := Form; KeepChoice.Left := ScaleX(20); KeepChoice.Top := ScaleY(76);
    KeepChoice.Width := ScaleX(440); KeepChoice.Height := ScaleY(24);
    KeepChoice.Caption := '保留设置和图片，重新安装后继续使用（推荐）'; KeepChoice.Checked := True;
    DeleteChoice := TNewRadioButton.Create(Form);
    DeleteChoice.Parent := Form; DeleteChoice.Left := ScaleX(20); DeleteChoice.Top := ScaleY(110);
    DeleteChoice.Width := ScaleX(440); DeleteChoice.Height := ScaleY(24);
    DeleteChoice.Caption := '一并删除设置和收件箱图片（无法撤销）';
    Location := TNewStaticText.Create(Form);
    Location.Parent := Form; Location.Left := ScaleX(20); Location.Top := ScaleY(148);
    Location.Width := ScaleX(440); Location.Height := ScaleY(42); Location.AutoSize := False; Location.WordWrap := True;
    Location.Caption := '数据目录：' + DataRoot;
    ContinueButton := TNewButton.Create(Form);
    ContinueButton.Parent := Form; ContinueButton.Caption := '继续卸载'; ContinueButton.ModalResult := mrOk;
    ContinueButton.SetBounds(ScaleX(276), ScaleY(208), ScaleX(90), ScaleY(28)); ContinueButton.Default := True;
    CancelButton := TNewButton.Create(Form);
    CancelButton.Parent := Form; CancelButton.Caption := '取消'; CancelButton.ModalResult := mrCancel;
    CancelButton.SetBounds(ScaleX(376), ScaleY(208), ScaleX(84), ScaleY(28)); CancelButton.Cancel := True;
    Form.ActiveControl := KeepChoice;
    Log(Format('Uninstall choice client size: %d x %d; scale: %d x %d', [Form.ClientWidth, Form.ClientHeight, ScaleX(480), ScaleY(250)]));
    if Form.ShowModal() = mrOk then begin
      RemoveUserData := DeleteChoice.Checked;
      Result := True;
    end;
  finally
    Form.Free();
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Existing, Root: String;
begin
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#RunValueName}', Existing) then
      if CompareText(RemoveQuotes(Existing), ExpandConstant('{app}\Liangtu.exe')) = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#RunValueName}');
  if (CurUninstallStep = usPostUninstall) and RemoveUserData then begin
    Root := RemoveBackslashUnlessRoot(ExpandFileName(DataRoot));
    { Never use stored photo paths or wildcard user input as deletion targets. }
    if (Length(Root) < 8) or (CompareText(Root, ExtractFileDrive(Root) + '\') = 0) or
       (CompareText(Root, ExpandFileName(ExpandConstant('{app}'))) = 0) then begin
      Log('Refused unsafe data directory: ' + Root);
      SuppressibleMsgBox('数据目录异常，已保留设置和图片。', mbError, MB_OK, IDOK);
      exit;
    end;
    Log('User explicitly requested data removal: ' + Root);
    { DelTree removes reparse points themselves without traversing their targets. }
    if DirExists(Root) then
      if not DelTree(Root, True, True, True) then begin
        Log('Some user data could not be removed: ' + Root);
        SuppressibleMsgBox('程序已卸载，但部分数据无法删除（可能被占用）。剩余文件保留在：' + #13#10 + Root, mbError, MB_OK, IDOK);
      end;
  end;
end;
