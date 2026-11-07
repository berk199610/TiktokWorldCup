@echo off
chcp 65001 >nul
title Dunya Taraftar Kupasi - Baslatici
cd /d "%~dp0"

rem ---- Node.js: once projedeki tasinabilir surum, yoksa bilgisayardaki
set "NODE=tools\node\node.exe"
set "NPM=tools\node\npm.cmd"
if not exist "%NODE%" (
  where node >nul 2>nul && (set "NODE=node" & set "NPM=npm") || (
    echo [HATA] Node.js bulunamadi. tools\node klasorunu indirin ya da https://nodejs.org adresinden kurun.
    pause
    exit /b 1
  )
)

rem ---- Kopru bagimliliklari (ilk calistirmada bir kez)
if not exist "bridge\node_modules\ws" (
  echo Kopru paketleri kuruluyor...
  pushd bridge
  call "..\%NPM%" install --no-audit --no-fund
  popd
)

rem ---- 1) Kopru (TikTok / simulator -> oyun)
start "Kopru - kapatma" cmd /k ""%NODE%" bridge\src\server.js"
timeout /t 2 /nobreak >nul

rem ---- 2) Yayinci paneli (tarayicida)
start "" "http://localhost:8787"

rem ---- 3) Oyun penceresi (608x1080, cercevesiz, ekranin sag kenarinda)
if exist "Builds\Windows\DunyaTaraftarKupasi.exe" (
  start "" "Builds\Windows\DunyaTaraftarKupasi.exe" -popupwindow
) else (
  echo [UYARI] Oyun henuz derlenmemis: Builds\Windows\DunyaTaraftarKupasi.exe yok.
  echo Unity'de DTK ^> Windows icin derle menusunu calistirin.
  pause
)
