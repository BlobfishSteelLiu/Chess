; ============================================================
;  Blob的国际象棋 —— Inno Setup 安装脚本
;  用 Inno Setup 6 编译： ISCC.exe installer.iss
; ============================================================

#define MyAppName "Blob的国际象棋"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Blob"
#define MyAppExeName "Chess.exe"

[Setup]
; 固定的 AppId，升级时能识别为同一个程序
AppId={{8F3A2C1E-5B7D-4E2A-9C6F-1D4B8E7A2F53}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoVersion=1.0.0.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} 安装程序
VersionInfoProductName={#MyAppName}

; PrivilegesRequired=lowest 时 {autopf} 指向 %LOCALAPPDATA%\Programs，
; 目录可写，程序才能把棋谱保存到自己的 Records 子目录里
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

OutputDir=dist
OutputBaseFilename=BlobChess-Setup-1.0.0
SetupIconFile=installer.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; 打包前先编译： dotnet build ..\Chess\Chess\Chess.csproj -c Release
Source: "..\Chess\Chess\bin\Release\net10.0-windows\Chess.exe";                DestDir: "{app}"; Flags: ignoreversion
Source: "..\Chess\Chess\bin\Release\net10.0-windows\Chess.dll";                DestDir: "{app}"; Flags: ignoreversion
Source: "..\Chess\Chess\bin\Release\net10.0-windows\Chess.deps.json";          DestDir: "{app}"; Flags: ignoreversion
Source: "..\Chess\Chess\bin\Release\net10.0-windows\Chess.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Chess\Chess\app.ico";                                              DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}";        Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}";  Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
{ 程序依赖 .NET 10 桌面运行时（Microsoft.WindowsDesktop.App 10.x）。
  这里直接查共享框架目录，缺了就给个提示（可被 /SUPPRESSMSGBOXES 抑制，不会卡住静默安装）。 }
function DirHasDotNet10(const BaseDir: String): Boolean;
var
  FindRec: TFindRec;
begin
  Result := False;
  if FindFirst(BaseDir + '\10.*', FindRec) then
  try
    repeat
      if (FindRec.Attributes and $10) <> 0 then
      begin
        Result := True;
        Break;
      end;
    until not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;
end;

function HasDotNet10Desktop(): Boolean;
begin
  Result :=
    DirHasDotNet10(ExpandConstant('{commonpf}\dotnet\shared\Microsoft.WindowsDesktop.App')) or
    DirHasDotNet10(ExpandConstant('{localappdata}\Microsoft\dotnet\shared\Microsoft.WindowsDesktop.App')) or
    DirHasDotNet10(ExpandConstant('{commonpf32}\dotnet\shared\Microsoft.WindowsDesktop.App'));
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if not HasDotNet10Desktop() then
      SuppressibleMsgBox(
        '未检测到 .NET 10 桌面运行时（Desktop Runtime）。' + #13#10#13#10 +
        '若程序无法启动，请先安装后再运行：' + #13#10 +
        'https://dotnet.microsoft.com/download/dotnet/10.0',
        mbInformation, MB_OK, IDOK);
  end;
end;
