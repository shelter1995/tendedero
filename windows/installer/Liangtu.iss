#ifndef AppVersion
  #define AppVersion "0.6.3"
#endif
#ifndef PayloadDir
  #error PayloadDir is required
#endif
#ifndef OutputPath
  #error OutputPath is required
#endif
#define FFmpegVersion "8.1.2"
#define FFmpegArchive "ffmpeg-8.1.2-essentials_build.7z"
#define FFmpegFolder "ffmpeg-8.1.2-essentials_build"
#define FFmpegSHA "e25b682664025d49034c981afb4bae36238a40f29a3cc1c713ad9a8b5b3528f6"
#ifndef FFmpegURL
  #define FFmpegURL "https://www.gyan.dev/ffmpeg/builds/packages/" + FFmpegArchive
#endif
#ifdef TestInstall
  #define ProductName "晾图安装验证"
  #define ProductId "{{9643F797-0668-459F-9BD8-F42EC65E1233}"
  #define OutputName "Liangtu-" + AppVersion + "-InstallerTest-x64"
  #define RunValueName "LiangtuInstallerTest"
#else
  #define ProductName "晾图"
  #define ProductId "{{570C3FBA-FB17-4CB3-AC99-2F7686489374}"
  #define OutputName "Liangtu-" + AppVersion + "-Setup-x64"
  #define RunValueName "Liangtu"
#endif

[Setup]
AppId={#ProductId}
AppName={#ProductName}
AppVersion={#AppVersion}
AppVerName={#ProductName} {#AppVersion}
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
OutputBaseFilename={#OutputName}
Compression=lzma2/max
SolidCompression=yes
LZMAUseSeparateProcess=yes
ArchiveExtraction=enhanced/nopassword
AppMutex={code:GetAppMutex}
CloseApplications=no
RestartApplications=no
SetupLogging=yes
UninstallLogging=yes
UsePreviousAppDir=yes
InfoBeforeFile=installer-info.txt
VersionInfoVersion={#AppVersion}.0
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
Name: "video"; Description: "视频缩略图与裁剪（下载约 33 MB，需联网）"; Types: full; ExtraDiskSpaceRequired: 210000000

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式："; Flags: unchecked

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: core
Source: "{tmp}\ffmpeg-extracted\{#FFmpegFolder}\bin\ffmpeg.exe"; DestDir: "{app}\tools"; Flags: external ignoreversion; ExternalSize: 130000000; Components: video
Source: "{tmp}\ffmpeg-extracted\{#FFmpegFolder}\bin\ffprobe.exe"; DestDir: "{app}\tools"; Flags: external ignoreversion; ExternalSize: 130000000; Components: video
Source: "{tmp}\ffmpeg-extracted\{#FFmpegFolder}\LICENSE"; DestDir: "{app}\licenses\FFmpeg"; DestName: "LICENSE.txt"; Flags: external ignoreversion; ExternalSize: 40000; Components: video
Source: "{tmp}\ffmpeg-extracted\{#FFmpegFolder}\README.txt"; DestDir: "{app}\licenses\FFmpeg"; Flags: external ignoreversion; ExternalSize: 300000; Components: video

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
  DownloadPage := CreateDownloadPage('下载视频组件', '从上游下载固定版本并校验完整性。首次安装需联网。', nil);
  DownloadPage.ShowBaseNameInsteadOfUrl := True;
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
      if LocalArchive <> '' then begin
        if not CopyFile(LocalArchive, ArchivePath, False) then RaiseException('无法读取指定的视频组件文件。');
        if CompareText(GetSHA256OfFile(ArchivePath), '{#FFmpegSHA}') <> 0 then RaiseException('视频组件校验失败，请重新下载官方版本。');
      end else begin
        DownloadPage.Add('{#FFmpegURL}', '{#FFmpegArchive}', '{#FFmpegSHA}');
        DownloadPage.Download;
      end;
      DownloadPage.SetText('正在解压视频组件…', '请稍候');
      ExtractArchive(ArchivePath, ExpandConstant('{tmp}\ffmpeg-extracted'), '', True, nil);
      if not FileExists(ExpandConstant('{tmp}\ffmpeg-extracted\{#FFmpegFolder}\bin\ffmpeg.exe')) then RaiseException('视频组件目录不完整。');
      VideoPrepared := True;
    except
      Error := GetExceptionMessage;
      Log('Video component preparation failed: ' + Error);
      if not DownloadPage.AbortedByUser then
        SuppressibleMsgBox('视频组件下载或校验失败：' + #13#10 + Error + #13#10#13#10 + '可重试，或返回“选择组件”取消视频组件，先安装图片功能。', mbError, MB_OK, IDOK);
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

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Existing: String;
begin
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#RunValueName}', Existing) then
      if CompareText(RemoveQuotes(Existing), ExpandConstant('{app}\Liangtu.exe')) = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#RunValueName}');
  { The screenshot/settings data directory is deliberately never deleted. }
end;
