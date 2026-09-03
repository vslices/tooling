#ifndef AppVersion
  #define AppVersion "0.1.0-local"
#endif

#ifndef SourceDir
  #define SourceDir "..\\..\\publish\\win-x64"
#endif

#define AppName "VSlices Tooling"
#define AppPublisher "VSlices"
#define AppExeName "vslices.exe"
#define AppUrl "https://github.com/vslices/tooling"

[Setup]
AppId={{E34B0762-C12C-4DC1-93D4-D5997D9C7594}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
AppUpdatesURL={#AppUrl}
DefaultDirName={localappdata}\Programs\VSlices Tooling
DefaultGroupName=VSlices Tooling
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=output
OutputBaseFilename=VSlices-Tooling-Setup-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName=VSlices Tooling
ChangesEnvironment=yes

[Files]
Source: "{#SourceDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\VSlices Tooling Command Prompt"; Filename: "{cmd}"; Parameters: "/K \"{app}\vslices.exe\" --help"; WorkingDir: "{userdocs}"

[Run]
Filename: "{app}\vslices.exe"; Parameters: "--help"; Description: "Verify VSlices Tooling installation"; Flags: postinstall nowait skipifsilent unchecked

[Code]
const
  EnvironmentKey = 'Environment';
  PathValueName = 'Path';

function NormalizePath(Value: string): string;
begin
  Result := RemoveQuotes(Trim(Value));
  while (Length(Result) > 3) and (Result[Length(Result)] = '\') do
    Delete(Result, Length(Result), 1);
end;

function PathContains(PathValue, Entry: string): Boolean;
var
  Remaining: string;
  SeparatorPosition: Integer;
  Candidate: string;
begin
  Result := False;
  Remaining := PathValue;
  Entry := NormalizePath(Entry);

  while Remaining <> '' do
  begin
    SeparatorPosition := Pos(';', Remaining);
    if SeparatorPosition = 0 then
    begin
      Candidate := Remaining;
      Remaining := '';
    end
    else
    begin
      Candidate := Copy(Remaining, 1, SeparatorPosition - 1);
      Delete(Remaining, 1, SeparatorPosition);
    end;

    if CompareText(NormalizePath(Candidate), Entry) = 0 then
    begin
      Result := True;
      Exit;
    end;
  end;
end;

procedure AddToUserPath(Entry: string);
var
  CurrentPath: string;
begin
  if not RegQueryStringValue(HKCU, EnvironmentKey, PathValueName, CurrentPath) then
    CurrentPath := '';

  if PathContains(CurrentPath, Entry) then
    Exit;

  if (CurrentPath <> '') and (CurrentPath[Length(CurrentPath)] <> ';') then
    CurrentPath := CurrentPath + ';';

  RegWriteExpandStringValue(HKCU, EnvironmentKey, PathValueName, CurrentPath + Entry);
end;

procedure RemoveFromUserPath(Entry: string);
var
  CurrentPath: string;
  Remaining: string;
  NewPath: string;
  SeparatorPosition: Integer;
  Candidate: string;
begin
  if not RegQueryStringValue(HKCU, EnvironmentKey, PathValueName, CurrentPath) then
    Exit;

  Remaining := CurrentPath;
  NewPath := '';

  while Remaining <> '' do
  begin
    SeparatorPosition := Pos(';', Remaining);
    if SeparatorPosition = 0 then
    begin
      Candidate := Remaining;
      Remaining := '';
    end
    else
    begin
      Candidate := Copy(Remaining, 1, SeparatorPosition - 1);
      Delete(Remaining, 1, SeparatorPosition);
    end;

    if (Candidate <> '') and (CompareText(NormalizePath(Candidate), NormalizePath(Entry)) <> 0) then
    begin
      if NewPath <> '' then
        NewPath := NewPath + ';';
      NewPath := NewPath + Candidate;
    end;
  end;

  RegWriteExpandStringValue(HKCU, EnvironmentKey, PathValueName, NewPath);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    AddToUserPath(ExpandConstant('{app}'));
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RemoveFromUserPath(ExpandConstant('{app}'));
end;
