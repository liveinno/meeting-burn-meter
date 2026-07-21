; Костёр — Meeting Burn Meter · установщик Inno Setup 6
; ═══════════════════════════════════════════════════════════════════
; ВАЖНО (BUGS.md): этот .iss и LICENSE.txt хранить в UTF-8 С BOM (иначе кириллица → ANSI-кракозябры).
; AppId — стабильный GUID, НЕ менять между версиями (иначе апгрейд не найдёт прошлую установку).
; Умное поведение (апгрейд / переустановка / даунгрейд / сброс конфига) — в разделе [Code].

#define MyAppName "Костёр"
#define MyAppExeName "Kostyor.exe"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Костёр"
; Тот же GUID в фигурных скобках — нужен в [Code] для ключа удаления {GUID}_is1.
#define AppGuidBraced "{287A66FF-DD3F-4A54-9B72-D77B16389045}"

[Setup]
; AppId: {{ = экранированная одинарная скобка. НЕ менять между версиями!
AppId={{287A66FF-DD3F-4A54-9B72-D77B16389045}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoVersion={#MyAppVersion}.0

DefaultDirName={autopf}\Kostyor
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} {#MyAppVersion}

OutputDir=..\..\installer\Output
OutputBaseFilename=Kostyor-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ShowLanguageDialog=yes

; Права: по умолчанию «только для меня» (без админа), но с выбором в диалоге.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; При апгрейде не переспрашивать папку/группу.
UsePreviousAppDir=yes
UsePreviousGroup=yes

; Модерн прячет эти страницы — явно показываем (юзер должен их видеть, BUGS.md).
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=no
LicenseFile=LICENSE.txt

; Обновление: закрыть работающий exe без принудительного ребута.
CloseApplications=yes
CloseApplicationsFilter={#MyAppExeName}
RestartApplications=no

; КРИТИЧНО: у неподписанного крупного exe мастер стартует через 1–2 мин (антивирус/SmartScreen
; синхронно сканирует файл ДО первой инструкции). Без мьютекса нетерпеливый юзер плодит мастера.
SetupMutex=Kostyor_SetupMutex

ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "autostart"; Description: "Запускать Костёр при входе в Windows"; GroupDescription: "Дополнительно:"; Flags: unchecked

[Files]
; Общий свип publish (BUGS.md): не перечислять файлы по одному — ISCC молча пропустит кривой путь.
; dev-config.json НЕ кладём: приложение создаёт дефолты в %APPDATA% при первом запуске.
Source: "..\..\publish\app-inno\*"; DestDir: "{app}"; Excludes: "config.json,*.pdb"; \
  Flags: ignoreversion recursesubdirs createallsubdirs

[Dirs]
; Пользовательские данные (экспорт карточек/CSV) — НЕ удалять при деинсталляции.
Name: "{userdocs}\Kostyor"; Flags: uninsneveruninstall

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Удалить {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Автозапуск (по таске) — пользовательская ветка Run, чистится при удалении.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; \
  ValueName: "Kostyor"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; \
  Flags: nowait postinstall skipifsilent

[Code]
var
  UpgradeMode: Boolean;
  ResetPage: TInputOptionWizardPage;
  OldConfigPath: String;

{ Покомпонентное сравнение версий "x.y.z": -1 / 0 / 1 }
function CompareVer(const A, B: String): Integer;
var
  SA, SB: String;
  PA, PB, NA, NB: Integer;
begin
  Result := 0;
  SA := A; SB := B;
  while ((SA <> '') or (SB <> '')) and (Result = 0) do
  begin
    PA := Pos('.', SA);
    if PA > 0 then begin NA := StrToIntDef(Copy(SA, 1, PA - 1), 0); SA := Copy(SA, PA + 1, Length(SA)); end
    else begin NA := StrToIntDef(SA, 0); SA := ''; end;
    PB := Pos('.', SB);
    if PB > 0 then begin NB := StrToIntDef(Copy(SB, 1, PB - 1), 0); SB := Copy(SB, PB + 1, Length(SB)); end
    else begin NB := StrToIntDef(SB, 0); SB := ''; end;
    if NA > NB then Result := 1
    else if NA < NB then Result := -1;
  end;
end;

{ DisplayVersion установленной версии из ключа удаления (и HKLM, и HKCU) }
function GetInstalledVersion(): String;
var
  V: String;
  Key: String;
begin
  Result := '';
  Key := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#AppGuidBraced}_is1';
  if RegQueryStringValue(HKLM, Key, 'DisplayVersion', V) then Result := V
  else if RegQueryStringValue(HKCU, Key, 'DisplayVersion', V) then Result := V;
end;

function InitializeSetup(): Boolean;
var
  Installed: String;
  Cmp: Integer;
begin
  Result := True;
  UpgradeMode := False;
  Installed := GetInstalledVersion();
  if Installed = '' then Exit; { чистая установка — обычный мастер }

  Cmp := CompareVer('{#MyAppVersion}', Installed);
  if Cmp > 0 then
  begin
    if MsgBox('Найдена установленная версия ' + Installed + '.' + #13#10 +
              'Обновить до {#MyAppVersion}?', mbConfirmation, MB_YESNO) = IDYES then
      UpgradeMode := True
    else
      Result := False;
  end
  else if Cmp = 0 then
  begin
    if MsgBox('Версия {#MyAppVersion} уже установлена.' + #13#10 +
              'Переустановить (восстановить файлы)?', mbConfirmation, MB_YESNO) = IDYES then
      UpgradeMode := True
    else
      Result := False;
  end
  else
  begin
    if MsgBox('Установлена более новая версия ' + Installed + '.' + #13#10 +
              'Установить старую {#MyAppVersion} поверх новой?', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      UpgradeMode := True
    else
      Result := False;
  end;
end;

procedure InitializeWizard();
begin
  { Опциональная страница сброса настроек — только если есть старый конфиг. }
  OldConfigPath := ExpandConstant('{userappdata}\Kostyor\config.json');
  if FileExists(OldConfigPath) then
  begin
    ResetPage := CreateInputOptionPage(wpSelectDir,
      'Настройки', 'Найден конфиг предыдущей установки',
      'Что сделать с текущими настройками Костра?', True, False);
    ResetPage.Add('Сбросить к дефолтам (старый конфиг — в бэкап в Документы) — рекомендуется');
    ResetPage.Add('Оставить мои настройки');
    ResetPage.SelectedValueIndex := 1; { по умолчанию — оставить }
  end;
end;

{ В режиме апгрейда пропускаем welcome/выбор папки/группы — они уже известны. }
function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if UpgradeMode and ((PageID = wpWelcome) or (PageID = wpSelectDir) or (PageID = wpSelectProgramGroup)) then
    Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Backup: String;
begin
  if CurStep = ssInstall then
  begin
    { Сброс конфига: бэкап в Документы, затем удаление (приложение создаст свежие дефолты). }
    if (ResetPage <> nil) and (ResetPage.SelectedValueIndex = 0) and FileExists(OldConfigPath) then
    begin
      try
        ForceDirectories(ExpandConstant('{userdocs}\Kostyor'));
        Backup := ExpandConstant('{userdocs}\Kostyor\config.backup-' +
          GetDateTimeString('yyyymmdd-hhnnss', #0, #0) + '.json');
        CopyFile(OldConfigPath, Backup, False);
        DeleteFile(OldConfigPath);
      except
        { best-effort — не блокируем установку }
      end;
    end;
  end
  else if CurStep = ssPostInstall then
  begin
    ForceDirectories(ExpandConstant('{userappdata}\Kostyor'));
    ForceDirectories(ExpandConstant('{userappdata}\Kostyor\logs'));
  end;
end;
