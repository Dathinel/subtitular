; Instalador de Subtitular (Inno Setup 7).
; Compilado por build\distribuir.ps1.
; Por usuario y sin permisos de administrador: {userpf}\Subtitular (%LOCALAPPDATA%\Programs\Subtitular).

#ifndef Version
  #error Falta /DVersion
#endif
#ifndef Origen
  #error Falta /DOrigen
#endif
#ifndef Licencia
  #error Falta /DLicencia
#endif

[Setup]
AppId={{8B5C0882-C29C-4148-8C64-07DF0BC59298}}
AppName=Subtitular
AppVersion={#Version}
AppVerName=Subtitular {#Version}
AppPublisher=Dathinel
AppPublisherURL=https://github.com/Dathinel/subtitular
AppSupportURL=https://github.com/Dathinel/subtitular/issues
AppUpdatesURL=https://github.com/Dathinel/subtitular/releases
AppCopyright=Copyright (C) 2026 Dathinel
VersionInfoVersion={#Version}.0
VersionInfoProductName=Subtitular
VersionInfoDescription=Instalador de Subtitular
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=
DefaultDirName={userpf}\Subtitular
DisableProgramGroupPage=yes
DisableDirPage=auto
UsePreviousAppDir=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
LicenseFile={#Licencia}
CloseApplications=yes
RestartApplications=no
UninstallDisplayName=Subtitular
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
OutputBaseFilename=Subtitular-{#Version}-instalador

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "escritorio"; Description: "Crear un acceso directo en el escritorio"; Flags: unchecked

[Files]
Source: "{#Origen}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Subtitular"; Filename: "{app}\Subtitular.exe"; Comment: "Subtítulos automáticos con IA"
Name: "{autodesktop}\Subtitular"; Filename: "{app}\Subtitular.exe"; Comment: "Subtítulos automáticos con IA"; Tasks: escritorio

[Run]
Filename: "{app}\Subtitular.exe"; Description: "Abrir Subtitular"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
