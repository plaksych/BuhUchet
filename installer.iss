#define MyAppName "BuhUchet"
#define MyAppVersion "1.0"
#define MyAppPublisher "plaksych"
#define MyAppExeName "BuhUchet.exe"
#define MyAppSourceDir ".\publish"
#define DotNetVersion "8.0"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=.\installer_output
OutputBaseFilename=BuhUchet_Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
SetupIconFile=.\assets\appicon.ico

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Дополнительно:";

[Files]
; Все файлы приложения из папки publish
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Удалить {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Запустить {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
// Проверка наличия .NET 8 Runtime
function IsDotNetInstalled(): Boolean;
var
  ResultCode: Integer;
begin
  Result := ShellExec('', 'dotnet', '--list-runtimes', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function GetDotNetUrl(): String;
begin
  Result := 'https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-8.0.0-windows-x64-installer';
end;

function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
  DotNetMissing: Boolean;
begin
  Result := True;
  DotNetMissing := not IsDotNetInstalled();

  if DotNetMissing then
  begin
    if MsgBox(
      '.NET 8 Desktop Runtime не найден на этом компьютере.' + #13#10 +
      'Приложение требует .NET 8 для работы.' + #13#10#13#10 +
      'Нажмите "Да" чтобы открыть страницу загрузки .NET 8,' + #13#10 +
      'установите его, затем запустите установщик снова.',
      mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', GetDotNetUrl(), '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
    end;
    Result := False;
  end;
end;