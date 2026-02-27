@echo off
REM Build C++ server and run integration tests
REM SPEC-IPC-001: Integration test execution script (Windows)

setlocal enabledelayedexpansion

set SCRIPT_DIR=%~dp0
set REPO_ROOT=%SCRIPT_DIR%..\..\..\
set BUILD_DIR=%REPO_ROOT%build
set SERVER_EXECUTABLE=%BUILD_DIR%\Debug\hnvue-ipc-server.exe

echo === HnVue IPC Integration Test Build ^& Run ===
echo Repository root: %REPO_ROOT%
echo Build directory: %BUILD_DIR%
echo.

REM Step 1: Build C++ server
echo Step 1: Building C++ gRPC server...
if not exist "%BUILD_DIR%" mkdir "%BUILD_DIR%"
cd /d "%BUILD_DIR%"

cmake .. -DCMAKE_BUILD_TYPE=Debug
if errorlevel 1 (
    echo ERROR: CMake configuration failed
    exit /b 1
)

cmake --build . --target hnvue-ipc-server --config Debug
if errorlevel 1 (
    echo ERROR: Build failed
    exit /b 1
)

if not exist "%SERVER_EXECUTABLE%" (
    echo ERROR: Server executable not found at: %SERVER_EXECUTABLE%
    exit /b 1
)

echo C++ server built successfully: %SERVER_EXECUTABLE%
echo.

REM Step 2: Run C# integration tests
echo Step 2: Running C# integration tests...
cd /d "%REPO_ROOT%"

dotnet test tests/integration/HnVue.Integration.Tests\HnVue.Integration.Tests.csproj ^
    --logger "console;verbosity=detailed" ^
    --framework "net8.0-windows"

if errorlevel 1 (
    echo WARNING: Some tests failed or server not running
    echo.
    echo To run tests manually:
    echo 1. Start the server: %SERVER_EXECUTABLE% --port=50051 --verbose
    echo 2. In another terminal, run: dotnet test tests/integration/HnVue.Integration.Tests\HnVue.Integration.Tests.csproj
)

echo.
echo === Integration tests completed ===
