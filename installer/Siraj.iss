; Siraj for Windows - installer (Inno Setup 6.7+).
;
; Installs per user into %LOCALAPPDATA%\Programs\Siraj with no administrator prompt.
; Pass /ALLUSERS on the command line for a machine-wide install, or /VERYSILENT for unattended setup.
; Built by build.ps1, which publishes into out/<arch> first.

#ifndef Arch
  #define Arch "x64"
#endif
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
AppId={{6E0D7C1A-3F55-4E29-9B6B-2C7E1A9D51F3}
AppName=Siraj
AppVersion={#AppVersion}
AppVerName=Siraj {#AppVersion}
AppPublisher=Siraj
AppCopyright=Siraj
VersionInfoVersion={#AppVersion}
VersionInfoProductName=Siraj
VersionInfoCompany=Siraj
VersionInfoDescription=Siraj - Prayer times, Adhan, Quran, and daily devotions
DefaultDirName={autopf}\Siraj
DefaultGroupName=Siraj
DisableWelcomePage=no
DisableDirPage=auto
DisableProgramGroupPage=yes
DisableReadyPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline
#if Arch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif
MinVersion=10.0.17763
OutputDir=..\out\installer
OutputBaseFilename=SirajSetup-{#AppVersion}-{#Arch}
SetupIconFile=..\src\Siraj.App\Assets\Siraj.ico
UninstallDisplayIcon={app}\Siraj.exe
UninstallDisplayName=Siraj
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
WizardStyle=modern dynamic windows11 hidebevels
WizardBackColor=#F7FBF9
WizardBackColorDynamicDark=#101816
WizardImageFile=assets\banner_100.png,assets\banner_150.png,assets\banner_200.png
WizardImageFileDynamicDark=assets\banner_dark_100.png,assets\banner_dark_150.png,assets\banner_dark_200.png
WizardSmallImageFile=assets\icon_100.png,assets\icon_150.png,assets\icon_200.png
WizardImageStretch=yes
WizardSizePercent=115,115
ShowLanguageDialog=auto
; Siraj closes cleanly via --quit in PrepareToInstall below; Restart Manager would only see the tray window.
CloseApplications=no
RestartApplications=no

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "ar"; MessagesFile: "compiler:Languages\Arabic.isl"
Name: "tr"; MessagesFile: "compiler:Languages\Turkish.isl"

[Messages]
en.WelcomeLabel1=Welcome to Siraj
en.WelcomeLabel2=This will install Siraj {#AppVersion} on your computer.%n%nPrayer times, background Adhan alerts, the Madinah Mushaf, Quran recitation and radio, daily Adhkar, and desktop widgets, all in one native Windows app.%n%nNo administrator permission is needed.
ar.WelcomeLabel1=مرحبًا بك في سراج
ar.WelcomeLabel2=سيتم تثبيت سراج {#AppVersion} على جهازك.%n%nمواقيت الصلاة، الأذان في الخلفية، مصحف المدينة، تلاوات القرآن والإذاعة، الأذكار اليومية، وأدوات سطح المكتب في تطبيق ويندوز واحد.%n%nلا حاجة لصلاحيات المسؤول.
tr.WelcomeLabel1=Siraj'a Hoş Geldiniz
tr.WelcomeLabel2=Bu kurulum bilgisayarınıza Siraj {#AppVersion} sürümünü yükleyecektir.%n%nNamaz vakitleri, arka planda ezan, Medine Mushafı, Kur'an tilavetleri ve radyo, günlük zikirler ve masaüstü araçları tek bir Windows uygulamasında.%n%nYönetici izni gerekmez.

[CustomMessages]
en.DesktopIcon=Create a desktop shortcut
ar.DesktopIcon=إنشاء اختصار على سطح المكتب
tr.DesktopIcon=Masaüstü kısayolu oluştur
en.Launch=Open Siraj now
ar.Launch=فتح سراج الآن
tr.Launch=Siraj'ı şimdi aç
en.RemoveDataPrompt=Do you also want to remove your saved settings, reading progress, and downloaded Quran pages?
ar.RemoveDataPrompt=هل تريد أيضًا حذف إعداداتك المحفوظة وتقدّم القراءة وصفحات المصحف المنزّلة؟
tr.RemoveDataPrompt=Kayıtlı ayarlarınızı, okuma ilerlemenizi ve indirilen Mushaf sayfalarını da silmek ister misiniz?

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; Flags: unchecked

[Files]
Source: "..\out\{#Arch}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\NOTICE.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Siraj"; Filename: "{app}\Siraj.exe"
Name: "{autodesktop}\Siraj"; Filename: "{app}\Siraj.exe"; Tasks: desktopicon

[Registry]
; Allows launching via Win+R -> "Siraj" cleanly for both per-user and all-users installs.
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\Siraj.exe"; ValueType: string; ValueName: ""; ValueData: "{app}\Siraj.exe"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\Siraj.exe"; ValueType: string; ValueName: "Path"; ValueData: "{app}"; Flags: uninsdeletekey

[Run]
Filename: "{app}\Siraj.exe"; Description: "{cm:Launch}"; Flags: postinstall nowait skipifsilent
; Used by the in-app updater (/VERYSILENT /RELAUNCH=1) so Siraj reopens automatically after updating in place.
Filename: "{app}\Siraj.exe"; Parameters: "--updated"; Flags: nowait skipifnotsilent; Check: ShouldRelaunch

[UninstallRun]
; Close the running copy, then remove its Start-with-Windows entry and notification registration.
Filename: "{app}\Siraj.exe"; Parameters: "--quit"; Flags: runhidden waituntilterminated; RunOnceId: "SirajQuit"
Filename: "{app}\Siraj.exe"; Parameters: "--uninstall"; Flags: runhidden waituntilterminated; RunOnceId: "SirajUnregister"

[Code]
function ShouldRelaunch: Boolean;
begin
  Result := ExpandConstant('{param:RELAUNCH|0}') = '1';
end;

// Checks whether Siraj.exe is still locked by a running instance by attempting a no-op rename.
function IsExeLocked(const Path: String): Boolean;
begin
  if not FileExists(Path) then
  begin
    Result := False;
    Exit;
  end;
  if RenameFile(Path, Path + '.chk') then
  begin
    RenameFile(Path + '.chk', Path);
    Result := False;
  end
  else
    Result := True;
end;

// Ask any running Siraj instance to quit gracefully before overwriting files during an update.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode, Waited: Integer;
  Exe: String;
begin
  Exe := ExpandConstant('{app}\Siraj.exe');
  if FileExists(Exe) then
  begin
    Exec(Exe, '--quit', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Waited := 0;
    while IsExeLocked(Exe) and (Waited < 5000) do
    begin
      Sleep(250);
      Waited := Waited + 250;
    end;
  end;
  Result := '';
end;

// On interactive uninstall, optionally let the user remove local settings and cached Mushaf pages.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if (CurUninstallStep = usPostUninstall) and (not UninstallSilent) then
  begin
    DataDir := ExpandConstant('{localappdata}\Siraj');
    if DirExists(DataDir) then
    begin
      if MsgBox(CustomMessage('RemoveDataPrompt'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(DataDir, True, True, True);
    end;
  end;
end;
