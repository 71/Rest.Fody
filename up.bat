@ECHO	OFF
REM  	UPDATE NUGET PACKAGE

SET	MSB="C:\Program Files (x86)\MSBuild\14.0\Bin\msbuild.exe"

MKDIR 	nuget>nul
MKDIR	nuget\lib\portable-net45+netcore45+win8+wp8+MonoAndroid+Xamarin.iOS10+MonoTouch>nul

%MSB%	Rest.Fody\Rest.Fody.csproj /property:Configuration=Release;OutDir=..\nuget>nul
%MSB%	Rest.Fody.Portable\Rest.Fody.Portable.csproj /property:Configuration=Release;OutDir=..\nuget\lib>nul

COPY	Rest.Fody.nuspec nuget>nul
COPY	nuget\lib\Rest.Fody.Portable\*.* "nuget\lib\portable-net45+netcore45+win8+wp8+MonoAndroid+Xamarin.iOS10+MonoTouch">nul
RMDIR	nuget\lib\Rest.Fody.Portable /S /Q>nul

NUGET	pack nuget\Rest.Fody.nuspec  -OutputDirectory .>nul
RMDIR	nuget /S /Q>nul