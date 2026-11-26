@echo off
setlocal
cd /d "%~dp0"

if not exist "node_modules\vite\bin\vite.js" (
  echo InspectNow Web dependencies are missing. Run pnpm install in InspectNow.Web once. 1>&2
  exit /b 1
)

set "INSPECTNOW_NODE="
for /f "delims=" %%N in ('where.exe node.exe 2^>nul') do if not defined INSPECTNOW_NODE set "INSPECTNOW_NODE=%%N"
if not defined INSPECTNOW_NODE (
  pushd "%ProgramFiles(x86)%\Microsoft Visual Studio\Installer" 2>nul
  if not errorlevel 1 (
    for /f "delims=" %%N in ('vswhere.exe -latest -find MSBuild\Microsoft\VisualStudio\NodeJs\node.exe') do if not defined INSPECTNOW_NODE set "INSPECTNOW_NODE=%%N"
    popd
  )
)

if not defined INSPECTNOW_NODE (
  echo Node.js was not found. Install Node.js or add the Node.js development workload in Visual Studio Installer. 1>&2
  exit /b 1
)

"%INSPECTNOW_NODE%" node_modules\vite\bin\vite.js --port 5176 --strictPort
exit /b %errorlevel%
