
REM  	UPDATE NUGET PACKAGE

@ECHO	OFF
SET	MSB="C:\Program Files (x86)\MSBuild\14.0\Bin\msbuild.exe"

MKDIR 	nuget

%MSB%	Rest.Fody.sln /property:Configuration=Release;OutDir=..\nuget	>nul
COPY	Rest.Fody.nuspec nuget						>nul
NUGET	pack nuget\Rest.Fody.nuspec  -OutputDirectory .			>nul

RMDIR	nuget