@echo off
setlocal enabledelayedexpansion

set "PROJECT=SpotyDownloaderMP\SpotyDownloaderMP.csproj"
set "OUTPUT=dist"

echo ==^> Limpiando dist anterior...
if exist "%OUTPUT%" rmdir /s /q "%OUTPUT%"
mkdir "%OUTPUT%"

echo.
echo ==^> Compilando para Linux (x64)...
dotnet publish "%PROJECT%" ^
  -c Release ^
  -r linux-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:DebugType=None ^
  -p:DebugSymbols=false ^
  -o "%OUTPUT%\linux"
if errorlevel 1 goto :error

echo.
echo ==^> Compilando para Windows (x64)...
dotnet publish "%PROJECT%" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:DebugType=None ^
  -p:DebugSymbols=false ^
  -o "%OUTPUT%\windows"
if errorlevel 1 goto :error

echo.
echo ==^> Empaquetando...
cd "%OUTPUT%"

REM Comprimir Linux en .tar.gz (usa tar, incluido en Windows 10/11)
tar -czf "SpotyDownloaderMP-linux-x64.tar.gz" -C linux .
if errorlevel 1 goto :error_cd

REM Comprimir Windows en .zip usando PowerShell (Compress-Archive)
powershell -NoProfile -Command "Compress-Archive -Path 'windows\*' -DestinationPath 'SpotyDownloaderMP-windows-x64.zip' -Force"
if errorlevel 1 goto :error_cd

cd ..

echo.
echo Build completado:
echo   Linux:   %OUTPUT%\SpotyDownloaderMP-linux-x64.tar.gz
echo   Windows: %OUTPUT%\SpotyDownloaderMP-windows-x64.zip
echo.

dir "%OUTPUT%\*.tar.gz" "%OUTPUT%\*.zip"

goto :end

:error_cd
cd ..
:error
echo.
echo Ocurrio un error durante el build.
exit /b 1

:end
endlocal