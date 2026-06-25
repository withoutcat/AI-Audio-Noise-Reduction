@echo off
REM Build script for NoiseReduction.Bridge DLL
REM Requires Visual Studio Build Tools with C++ desktop development workload
REM
REM VS_PATH priority:
REM   1. Environment variable VS_PATH
REM   2. vswhere auto-detection (CI / standard install)
REM   3. Hardcoded fallback for local dev

setlocal enabledelayedexpansion

REM SDK paths (relative to project root)
set "PROJECT_ROOT=%~dp0..\.."
set "SDK_INCLUDE=%PROJECT_ROOT%\res\sdk\Shengwang_Native_SDK_for_Windows_FULL\sdk\high_level_api\include"
set "SDK_LIB=%PROJECT_ROOT%\res\sdk\Shengwang_Native_SDK_for_Windows_FULL\sdk\x86_64"
set "OUTPUT_DIR=%~dp0bin"

REM --- Locate VS installation ---
set "VS_PATH="

REM Priority 1: Environment variable
if defined VS_PATH_ENV (
    if exist "%VS_PATH_ENV%\VC\Auxiliary\Build\vcvarsall.bat" (
        set "VS_PATH=%VS_PATH_ENV%"
        goto :found_vs
    )
)

REM Priority 2: vswhere (standard on GitHub Actions / modern VS)
for /f "usebackq tokens=*" %%i in (`where vswhere 2^>nul`) do (
    for /f "usebackq tokens=*" %%p in (`%%i -latest -products * -property installationPath`) do (
        if exist "%%p\VC\Auxiliary\Build\vcvarsall.bat" (
            set "VS_PATH=%%p"
            goto :found_vs
        )
    )
)

REM Priority 3: Hardcoded fallback (local dev machine)
set "VS_FALLBACKS[0]=D:\Microsoft Visual Studio\18\BuildTools"
set "VS_FALLBACKS[1]=C:\Program Files\Microsoft Visual Studio\2022\Community"
set "VS_FALLBACKS[2]=C:\Program Files\Microsoft Visual Studio\2022\BuildTools"
set "VS_FALLBACKS[3]=C:\Program Files\Microsoft Visual Studio\2022\Enterprise"

for /l %%i in (0,1,3) do (
    call :try_path %%i
    if defined VS_PATH goto :found_vs
)

echo ERROR: Cannot find Visual Studio. Please set VS_PATH_ENV environment variable.
echo   e.g. set VS_PATH_ENV=C:\Program Files\Microsoft Visual Studio\2022\Community
exit /b 1

:try_path
set idx=%1
set "try=!VS_FALLBACKS[%idx%]!"
if exist "!try!\VC\Auxiliary\Build\vcvarsall.bat" (
    set "VS_PATH=!try!"
)
goto :eof

:found_vs
set "VCVARSALL=%VS_PATH%\VC\Auxiliary\Build\vcvarsall.bat"
echo Using Visual Studio at: %VS_PATH%

REM Set up VS build environment for x64
call "%VCVARSALL%" amd64
if errorlevel 1 (
    echo ERROR: vcvarsall.bat failed
    exit /b 1
)

REM Create output directory
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

echo.
echo ========================================
echo Building NoiseReduction.Bridge DLL
echo ========================================
echo.

REM Compile and link
cl.exe /nologo ^
    /EHsc ^
    /MT ^
    /O2 ^
    /W3 ^
    /wd4819 ^
    /D WIN32 ^
    /D _WINDOWS ^
    /D AGORARTC_EXPORT ^
    /I "%SDK_INCLUDE%" ^
    /Fo"%OUTPUT_DIR%\bridge.obj" ^
    /Fe"%OUTPUT_DIR%\Bridge.dll" ^
    /LD ^
    "%~dp0bridge.cpp" ^
    "%SDK_LIB%\agora_rtc_sdk.dll.lib" ^
    /link ^
    /LIBPATH:"%SDK_LIB%" ^
    /OUT:"%OUTPUT_DIR%\Bridge.dll"

if errorlevel 1 (
    echo.
    echo BUILD FAILED
    exit /b 1
)

echo.
echo BUILD SUCCESS
echo Output: %OUTPUT_DIR%\Bridge.dll
echo.

REM Copy SDK DLLs to output directory for runtime
echo Copying SDK DLLs to output directory...
copy /Y "%SDK_LIB%\agora_rtc_sdk.dll" "%OUTPUT_DIR%\" >nul
copy /Y "%SDK_LIB%\libagora_ai_noise_suppression_extension.dll" "%OUTPUT_DIR%\" >nul
copy /Y "%SDK_LIB%\libaosl.dll" "%OUTPUT_DIR%\" >nul

echo Done.
endlocal
