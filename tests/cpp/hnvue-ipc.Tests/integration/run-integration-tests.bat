@echo off
REM run-integration-tests.bat - Script to run IPC integration tests on Windows
REM SPEC-IPC-001: Integration test automation

SETLOCAL EnableDelayedExpansion

REM Configuration
SET BUILD_DIR=build
SET CPP_SERVER_PORT=50051
SET TEST_FILTER=*

REM Parse command line arguments
SET TEST_TYPE=%1
IF "%TEST_TYPE%"=="" SET TEST_TYPE=all

REM Function to check if server is running (PowerShell helper)
:check_server
powershell -Command "$tcp = New-Object System.Net.Sockets.TcpClient; try { $tcp.Connect('localhost', %CPP_SERVER_PORT%); $tcp.Close(); exit 0 } catch { exit 1 }"
GOTO :EOF

REM Function to wait for server
:wait_for_server
SET MAX_WAIT=10
SET COUNT=0

:wait_loop
IF !COUNT! GEQ %MAX_WAIT% (
    echo [ERROR] Server failed to start within %MAX_WAIT% seconds
    EXIT /B 1
)

REM Check if server is running
powershell -Command "$tcp = New-Object System.Net.Sockets.TcpClient; try { $tcp.Connect('localhost', %CPP_SERVER_PORT%'); $tcp.Close(); exit 0 } catch { exit 1 }"
IF !ERRORLEVEL! EQU 0 (
    echo [INFO] Server is ready!
    GOTO :server_ready
)

TIMEOUT /T 1 /NOBREAK >NUL
SET /A COUNT+=1
GOTO :wait_loop

:server_ready
EXIT /B 0

REM Main execution
echo [INFO] HnVue IPC Integration Tests
echo [INFO] ================================
echo.

IF "%TEST_TYPE%"=="cpp" GOTO :run_cpp
IF "%TEST_TYPE%"=="csharp" GOTO :run_csharp
IF "%TEST_TYPE%"=="all" GOTO :run_all

echo [ERROR] Unknown test type: %TEST_TYPE%
echo [INFO] Usage: %0 [cpp^|csharp^|all]
EXIT /B 1

:run_cpp
echo [INFO] Running C++ integration tests only

REM Check if server binary exists
IF NOT EXIST "%BUILD_DIR%\libs\hnvue-ipc\hnvue-ipc-server.exe" (
    echo [ERROR] C++ server binary not found at %BUILD_DIR%\libs\hnvue-ipc\hnvue-ipc-server.exe
    echo [INFO] Please build the server first:
    echo [INFO]   cmake --build %BUILD_DIR% --target hnvue-ipc-server
    EXIT /B 1
)

REM Start server
echo [INFO] Starting C++ gRPC server on port %CPP_SERVER_PORT%...
START /B "" "%BUILD_DIR%\libs\hnvue-ipc\hnvue-ipc-server.exe" --port %CPP_SERVER_PORT% > %TEMP%\hnvue-server.log 2>&1
SET SERVER_PID=%ERRORLEVEL%

REM Wait for server
CALL :wait_for_server
IF !ERRORLEVEL! NEQ 0 (
    echo [ERROR] Failed to start C++ server
    TYPE %TEMP%\hnvue-server.log
    EXIT /B 1
)

REM Run tests
IF NOT EXIST "%BUILD_DIR%\tests\cpp\hnvue-ipc.Tests\hnvue-ipc.IntegrationTests.exe" (
    echo [ERROR] C++ integration test binary not found
    EXIT /B 1
)

"%BUILD_DIR%\tests\cpp\hnvue-ipc.Tests\hnvue-ipc.IntegrationTests.exe" --gtest_filter=%TEST_FILTER%

REM Stop server
TASKKILL /F /IM hnvue-ipc-server.exe >NUL 2>&1

EXIT /B !ERRORLEVEL!

:run_csharp
echo [INFO] Running C# integration tests only

REM Start server (same as above)
IF NOT EXIST "%BUILD_DIR%\libs\hnvue-ipc\hnvue-ipc-server.exe" (
    echo [ERROR] C++ server binary not found
    EXIT /B 1
)

echo [INFO] Starting C++ gRPC server on port %CPP_SERVER_PORT%...
START /B "" "%BUILD_DIR%\libs\hnvue-ipc\hnvue-ipc-server.exe" --port %CPP_SERVER_PORT% > %TEMP%\hnvue-server.log 2>&1

CALL :wait_for_server
IF !ERRORLEVEL! NEQ 0 (
    echo [ERROR] Failed to start C++ server
    TYPE %TEMP%\hnvue-server.log
    EXIT /B 1
)

REM Run tests
IF NOT EXIST "tests\HnVue.Ipc.Client.Tests\HnVue.Ipc.Client.Tests.csproj" (
    echo [ERROR] C# test project not found
    EXIT /B 1
)

dotnet test tests\HnVue.Ipc.Client.Tests\HnVue.Ipc.Client.Tests.csproj --filter "FullyQualifiedName~Integration" --no-build

REM Stop server
TASKKILL /F /IM hnvue-ipc-server.exe >NUL 2>&1

EXIT /B !ERRORLEVEL!

:run_all
echo [INFO] Running all integration tests

REM Start server
IF NOT EXIST "%BUILD_DIR%\libs\hnvue-ipc\hnvue-ipc-server.exe" (
    echo [ERROR] C++ server binary not found
    EXIT /B 1
)

echo [INFO] Starting C++ gRPC server on port %CPP_SERVER_PORT%...
START /B "" "%BUILD_DIR%\libs\hnvue-ipc\hnvue-ipc-server.exe" --port %CPP_SERVER_PORT% > %TEMP%\hnvue-server.log 2>&1

CALL :wait_for_server
IF !ERRORLEVEL! NEQ 0 (
    echo [ERROR] Failed to start C++ server
    TYPE %TEMP%\hnvue-server.log
    EXIT /B 1
)

SET RESULT=0

REM Run C++ tests
IF NOT EXIST "%BUILD_DIR%\tests\cpp\hnvue-ipc.Tests\hnvue-ipc.IntegrationTests.exe" (
    echo [WARN] C++ tests not found, skipping
) ELSE (
    "%BUILD_DIR%\tests\cpp\hnvue-ipc.Tests\hnvue-ipc.IntegrationTests.exe" --gtest_filter=%TEST_FILTER%
    IF !ERRORLEVEL! NEQ 0 SET RESULT=1
)

REM Run C# tests
IF NOT EXIST "tests\HnVue.Ipc.Client.Tests\HnVue.Ipc.Client.Tests.csproj" (
    echo [WARN] C# tests not found, skipping
) ELSE (
    dotnet test tests\HnVue.Ipc.Client.Tests\HnVue.Ipc.Client.Tests.csproj --filter "FullyQualifiedName~Integration" --no-build
    IF !ERRORLEVEL! NEQ 0 SET RESULT=1
)

REM Stop server
TASKKILL /F /IM hnvue-ipc-server.exe >NUL 2>&1

IF !RESULT! NEQ 0 (
    echo [ERROR] Some integration tests FAILED
    EXIT /B 1
) ELSE (
    echo [INFO] All integration tests PASSED
)

EXIT /B 0
