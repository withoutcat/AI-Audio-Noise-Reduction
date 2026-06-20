@echo off
REM Build script for NoiseReduction.Bridge DLL
REM Requires Visual Studio Build Tools with C++ desktop development workload

setlocal

REM VS Build Tools path (adjust if installed elsewhere)
set "VS_PATH=D:\Microsoft Visual Studio\18\BuildTools"
set "VCVARSALL=%VS_PATH%\VC\Auxiliary\Build\vcvarsall.bat"

REM SDK paths (relative to project root)
set "PROJECT_ROOT=%~dp0..\.."
set "SDK_INCLUDE=%PROJECT_ROOT%\res\sdk\Shengwang_Native_SDK_for_Windows_FULL\sdk\high_level_api\include"
set "SDK_LIB=%PROJECT_ROOT%\res\sdk\Shengwang_Native_SDK_for_Windows_FULL\sdk\x86_64"
set "OUTPUT_DIR=%~dp0bin"

if not exist "%VCVARSALL%" (
    echo ERROR: vcvarsall.bat not found at %VCVARSALL%
    echo Please update VS_PATH in this script.
    exit /b 1
)

REM Set up VS build environment for x64
call "%VCVARSALL%" amd64

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
    /MD ^
    /O2 ^
    /W3 ^
    /D WIN32 ^
    /D _WINDOWS ^
    /D AGORARTC_EXPORT ^
    /I "%SDK_INCLUDE%" ^
    /Fo"%OUTPUT_DIR%\bridge.obj" ^
    /Fe"%OUTPUT_DIR%\NoiseReduction.Bridge.dll" ^
    /LD ^
    "%~dp0bridge.cpp" ^
    "%SDK_LIB%\agora_rtc_sdk.dll.lib" ^
    /link ^
    /LIBPATH:"%SDK_LIB%" ^
    /OUT:"%OUTPUT_DIR%\NoiseReduction.Bridge.dll"

if errorlevel 1 (
    echo.
    echo BUILD FAILED
    exit /b 1
)

echo.
echo BUILD SUCCESS
echo Output: %OUTPUT_DIR%\NoiseReduction.Bridge.dll
echo.

REM Copy SDK DLLs to output directory for runtime
echo Copying SDK DLLs to output directory...
copy /Y "%SDK_LIB%\agora_rtc_sdk.dll" "%OUTPUT_DIR%\" >nul
copy /Y "%SDK_LIB%\libagora_ai_noise_suppression_extension.dll" "%OUTPUT_DIR%\" >nul
copy /Y "%SDK_LIB%\libaosl.dll" "%OUTPUT_DIR%\" >nul

echo Done.
endlocal
