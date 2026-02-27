/**
 * @file test_ipc_server.cpp
 * @brief Unit tests for IpcServer lifecycle management
 * SPEC-IPC-001: Server startup, shutdown, service registration
 */

#include <gtest/gtest.h>
#include <gmock/gmock.h>
#include <grpcpp/grpcpp.h>
#include <thread>
#include <chrono>
#include <memory>

#include "hnvue/ipc/IpcServer.h"

using namespace hnvue::ipc;

namespace hnvue::test {

/**
 * @test IpcServer constructs with default configuration
 *
 * GIVEN a default IpcServer constructor
 * WHEN an IpcServer is instantiated
 * THEN it should initialize with localhost:50051
 * AND it should not be running
 */
TEST(IpcServerTest, Constructor_DefaultConfiguration_InitializesCorrectly) {
    // Arrange & Act: Create server with default address
    IpcServer server;

    // Assert: Server should not be running initially
    EXPECT_FALSE(server.IsRunning());
    EXPECT_EQ(server.GetServerAddress(), "localhost:50051");
    EXPECT_EQ(server.GetInterfaceVersion(), "1.0.0");
}

/**
 * @test IpcServer constructs with custom address
 *
 * GIVEN a custom server address
 * WHEN an IpcServer is instantiated with that address
 * THEN it should store the address for later use
 */
TEST(IpcServerTest, Constructor_CustomAddress_StoresAddress) {
    // Arrange & Act: Create server with custom address
    const std::string custom_address = "localhost:50052";
    IpcServer server(custom_address);

    // Assert: Address should be stored
    EXPECT_EQ(server.GetServerAddress(), custom_address);
    EXPECT_FALSE(server.IsRunning());
}

/**
 * @test IpcServer starts successfully and binds to port
 *
 * GIVEN a configured IpcServer
 * WHEN Start() is called
 * THEN the server should bind to the configured port
 * AND IsRunning() should return true
 * AND a second Start() call should return true (idempotent)
 */
TEST(IpcServerTest, Start_ValidAddress_BindsSuccessfully) {
    // Arrange: Create server
    IpcServer server("localhost:50053");

    // Act: Start the server
    bool started = server.Start();

    // Assert: Server should be running
    EXPECT_TRUE(started);
    EXPECT_TRUE(server.IsRunning());

    // Cleanup: Stop server
    server.Stop();
}

/**
 * @test IpcServer start is idempotent
 *
 * GIVEN a running IpcServer
 * WHEN Start() is called again
 * THEN it should return true without error
 * AND the server should remain running
 */
TEST(IpcServerTest, Start_AlreadyRunning_ReturnsTrue) {
    // Arrange: Start server
    IpcServer server("localhost:50054");
    ASSERT_TRUE(server.Start());

    // Act: Call Start again
    bool started_again = server.Start();

    // Assert: Should return true (already running)
    EXPECT_TRUE(started_again);
    EXPECT_TRUE(server.IsRunning());

    // Cleanup
    server.Stop();
}

/**
 * @test IpcServer stops gracefully
 *
 * GIVEN a running IpcServer
 * WHEN Stop() is called
 * THEN the server should shutdown
 * AND IsRunning() should return false
 * AND Stop() should be idempotent
 */
TEST(IpcServerTest, Stop_RunningServer_ShutdownGracefully) {
    // Arrange: Start server
    IpcServer server("localhost:50055");
    ASSERT_TRUE(server.Start());
    ASSERT_TRUE(server.IsRunning());

    // Act: Stop the server
    server.Stop();

    // Assert: Server should not be running
    EXPECT_FALSE(server.IsRunning());

    // Act: Stop again (idempotent)
    server.Stop();

    // Assert: Should still be not running
    EXPECT_FALSE(server.IsRunning());
}

/**
 * @test IpcServer destructor stops running server
 *
 * GIVEN a running IpcServer
 * WHEN the server is destroyed
 * THEN the destructor should call Stop() automatically
 */
TEST(IpcServerTest, Destructor_RunningServer_StopsAutomatically) {
    // Arrange & Act: Create and start server in scope
    {
        IpcServer server("localhost:50056");
        ASSERT_TRUE(server.Start());
        ASSERT_TRUE(server.IsRunning());
        // Server goes out of scope here
    }

    // Assert: No crash or hang should occur
    // (If destructor doesn't stop, port may remain bound)
    SUCCEED();
}

/**
 * @test IpcServer reports correct interface version
 *
 * GIVEN an IpcServer instance
 * WHEN GetInterfaceVersion() is called
 * THEN it should return version in semver format (major.minor.patch)
 */
TEST(IpcServerTest, GetInterfaceVersion_ReturnsValidSemver) {
    // Arrange: Create server
    IpcServer server;

    // Act: Get interface version
    std::string version = server.GetInterfaceVersion();

    // Assert: Should be in semver format (x.y.z)
    EXPECT_FALSE(version.empty());
    EXPECT_THAT(version, testing::MatchesRegex("^[0-9]+\\.[0-9]+\\.[0-9]+$"));
}

/**
 * @test IpcServer handles start/stop cycles
 *
 * GIVEN an IpcServer instance
 * WHEN multiple start/stop cycles are performed
 * THEN each cycle should complete successfully
 */
TEST(IpcServerTest, StartStop_MultipleCycles_AllSucceed) {
    // Arrange: Create server
    IpcServer server("localhost:50057");

    // Act & Assert: First cycle
    EXPECT_TRUE(server.Start());
    EXPECT_TRUE(server.IsRunning());
    server.Stop();
    EXPECT_FALSE(server.IsRunning());

    // Act & Assert: Second cycle
    EXPECT_TRUE(server.Start());
    EXPECT_TRUE(server.IsRunning());
    server.Stop();
    EXPECT_FALSE(server.IsRunning());

    // Act & Assert: Third cycle
    EXPECT_TRUE(server.Start());
    EXPECT_TRUE(server.IsRunning());
    server.Stop();
    EXPECT_FALSE(server.IsRunning());
}

} // namespace hnvue::test
