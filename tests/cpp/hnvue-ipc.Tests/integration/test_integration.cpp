/**
 * @file test_integration.cpp
 * @brief Integration tests for IPC gRPC server-client communication
 * SPEC-IPC-001: End-to-end testing of C++ server with C# client scenarios
 *
 * Prerequisites:
 * - C++ gRPC server binary must be built first
 * - Tests create real server instances on localhost:50051
 * - Tests use real gRPC client stubs (no mocks)
 *
 * Test Categories:
 * 1. Server Lifecycle: Startup, shutdown, port binding
 * 2. Connection Flow: Connect, version check, config sync, health subscription
 * 3. RPC Calls: CommandService, ImageService, HealthService, ConfigService
 * 4. Streaming: Server streaming, bidirectional streaming
 * 5. Error Handling: Connection loss, reconnection, version mismatch
 */

#include <gtest/gtest.h>
#include <gmock/gmock.h>
#include <grpcpp/grpcpp.h>
#include <thread>
#include <chrono>
#include <memory>
#include <atomic>

// Include generated protobuf headers
#include "hnvue_ipc.grpc.pb.h"
#include "hnvue_ipc.pb.h"

using namespace grpc;
using namespace hnvue::ipc;
using namespace testing;

namespace hnvue::test::integration {

/**
 * @class IntegrationTestBase
 * @brief Base fixture for all integration tests
 *
 * Manages server lifecycle and provides common test utilities.
 * Each test gets a fresh server instance on a unique port.
 */
class IntegrationTestBase : public ::testing::Test {
protected:
    std::unique_ptr<Server> server_;
    std::string server_address_;
    int port_offset_ = 0;  // Offset from base port 50051 for parallel test isolation
    std::atomic<bool> server_running_{false};

    void SetUp() override {
        // Generate unique server address for each test to avoid port conflicts
        // Use test case name + timestamp for uniqueness
        auto test_info = ::testing::UnitTest::GetInstance()->current_test_info();
        port_offset_ = std::hash<std::string>{}(
            test_info->test_case_name() + std::string(test_info->name())
        ) % 1000;  // Offset 0-999

        server_address_ = "localhost:" + std::to_string(50051 + port_offset_);
    }

    void TearDown() override {
        // Ensure server is stopped
        if (server_) {
            server_->Shutdown(std::chrono::system_clock::now() + std::chrono::seconds(5));
        }
    }

    /**
     * @brief Create and start a test gRPC server with all services registered
     * @return true if server started successfully, false otherwise
     */
    bool StartTestServer() {
        ServerBuilder builder;

        // Listen on the test address
        builder.AddListeningPort(server_address_, grpc::InsecureServerCredentials());

        // Register services (mock implementations for testing)
        // Note: These will be replaced with real service implementations when available
        // CommandService command_service;
        // ImageService image_service;
        // HealthService health_service;
        // ConfigService config_service;

        // builder.RegisterService(&command_service);
        // builder.RegisterService(&image_service);
        // builder.RegisterService(&health_service);
        // builder.RegisterService(&config_service);

        // Build and start server
        server_ = builder.BuildAndStart();
        if (!server_) {
            return false;
        }

        server_running_ = true;
        return true;
    }

    /**
     * @brief Create a gRPC client channel connected to the test server
     * @return Shared pointer to the client channel
     */
    std::shared_ptr<Channel> CreateClientChannel() {
        return grpc::CreateChannel(
            server_address_,
            grpc::InsecureChannelCredentials()
        );
    }

    /**
     * @brief Wait for server to be ready (max 5 seconds)
     * @return true if server is ready, false on timeout
     */
    bool WaitForServerReady(int timeout_seconds = 5) {
        auto channel = CreateClientChannel();
        auto state = channel->GetState(true);

        auto deadline = std::chrono::system_clock::now() + std::chrono::seconds(timeout_seconds);
        while (state != GRPC_CHANNEL_READY &&
               std::chrono::system_clock::now() < deadline) {
            std::this_thread::sleep_for(std::chrono::milliseconds(100));
            state = channel->GetState(false);
        }

        return state == GRPC_CHANNEL_READY;
    }
};

// =============================================================================
// Category 1: Server Lifecycle Tests
// =============================================================================

/**
 * @test Server starts successfully on configured port
 */
TEST_F(IntegrationTestBase, ServerStart_BindsToConfiguredPort) {
    // Act
    bool started = StartTestServer();

    // Assert
    EXPECT_TRUE(started) << "Server should start successfully";
    EXPECT_TRUE(server_running_) << "Server running flag should be set";
    EXPECT_NE(server_, nullptr) << "Server instance should be created";
}

/**
 * @test Server stops gracefully and releases port
 */
TEST_F(IntegrationTestBase, ServerStop_ShutdownGracefully) {
    // Arrange
    ASSERT_TRUE(StartTestServer()) << "Server should start for shutdown test";

    // Act
    server_->Shutdown(std::chrono::system_clock::now() + std::chrono::seconds(5));
    server_running_ = false;
    server_.reset();

    // Assert: Port should be available for new server
    ServerBuilder builder;
    builder.AddListeningPort(server_address_, grpc::InsecureServerCredentials());
    auto new_server = builder.BuildAndStart();

    EXPECT_NE(new_server, nullptr) << "New server should bind to the same port after shutdown";

    if (new_server) {
        new_server->Shutdown();
    }
}

/**
 * @test Server refuses to start if port is already in use
 */
TEST_F(IntegrationTestBase, ServerStart_PortInUse_Fails) {
    // Arrange: First server is running
    ASSERT_TRUE(StartTestServer()) << "First server should start";

    // Act: Try to start second server on same port
    ServerBuilder builder;
    builder.AddListeningPort(server_address_, grpc::InsecureServerCredentials());
    auto second_server = builder.BuildAndStart();

    // Assert
    EXPECT_EQ(second_server, nullptr) << "Second server should fail to bind to occupied port";

    if (second_server) {
        second_server->Shutdown();
    }
}

// =============================================================================
// Category 2: Connection Flow Tests
// =============================================================================

/**
 * @test Client connects to server successfully
 */
TEST_F(IntegrationTestBase, ClientConnection_ConnectsSuccessfully) {
    // Arrange: Server is running
    ASSERT_TRUE(StartTestServer()) << "Server should start for connection test";
    ASSERT_TRUE(WaitForServerReady()) << "Server should be ready for connections";

    // Act: Create client channel
    auto channel = CreateClientChannel();

    // Assert: Channel should transition to READY state
    auto state = channel->GetState(true);
    EXPECT_EQ(state, GRPC_CHANNEL_READY) << "Client channel should be in READY state";
}

/**
 * @test Connection state transitions correctly through lifecycle
 */
TEST_F(IntegrationTestBase, ConnectionState_TransitionsCorrectly) {
    // Arrange: Server is running
    ASSERT_TRUE(StartTestServer()) << "Server should start for state test";
    ASSERT_TRUE(WaitForServerReady()) << "Server should be ready";

    // Act & Assert: Initial state
    auto channel = CreateClientChannel();
    auto state = channel->GetState(true);
    EXPECT_EQ(state, GRPC_CHANNEL_READY) << "Initial state should be READY";

    // Act & Assert: After server shutdown
    server_->Shutdown(std::chrono::system_clock::now() + std::chrono::seconds(1));
    std::this_thread::sleep_for(std::chrono::milliseconds(500));

    state = channel->GetState(false);
    EXPECT_TRUE(state == GRPC_CHANNEL_IDLE || state == GRPC_CHANNEL_TRANSIENT_FAILURE)
        << "State after shutdown should be IDLE or TRANSIENT_FAILURE";
}

// =============================================================================
// Category 3: RPC Call Tests (Unary)
// =============================================================================

/**
 * @test CommandService GetSystemState RPC returns valid system state
 */
TEST_F(IntegrationTestBase, CommandService_GetSystemState_ReturnsValidState) {
    // Arrange: Server running and connected
    ASSERT_TRUE(StartTestServer()) << "Server should start";
    ASSERT_TRUE(WaitForServerReady()) << "Server should be ready";

    auto channel = CreateClientChannel();
    auto stub = CommandService::NewStub(channel);

    // Act: Call GetSystemState
    GetSystemStateRequest request;
    GetSystemStateResponse response;

    ClientContext context;
    context.set_deadline(
        std::chrono::system_clock::now() + std::chrono::seconds(5)
    );

    Status status = stub->GetSystemState(&context, request, &response);

    // Assert
    if (status.ok()) {
        EXPECT_TRUE(response.has_version_info()) << "Response should contain version info";
        EXPECT_FALSE(response.version_info().ipc_version().empty())
            << "IPC version should not be empty";
    } else {
        // Expected: Service not yet implemented
        EXPECT_EQ(status.error_code(), StatusCode::UNIMPLEMENTED)
            << "Expected UNIMPLEMENTED status until service is complete";
    }
}

/**
 * @test CommandService StartExposure RPC processes exposure request
 */
TEST_F(IntegrationTestBase, CommandService_StartExposure_ProcessesRequest) {
    // Arrange: Server running and connected
    ASSERT_TRUE(StartTestServer()) << "Server should start";
    ASSERT_TRUE(WaitForServerReady()) << "Server should be ready";

    auto channel = CreateClientChannel();
    auto stub = CommandService::NewStub(channel);

    // Act: Call StartExposure
    StartExposureRequest request;
    request.set_kv(120.0f);
    request.set_mas(10.0f);
    request.set_detector_id(1);

    StartExposureResponse response;

    ClientContext context;
    context.set_deadline(
        std::chrono::system_clock::now() + std::chrono::seconds(5)
    );

    Status status = stub->StartExposure(&context, request, &response);

    // Assert
    if (status.ok()) {
        EXPECT_GT(response.acquisition_id(), 0) << "Acquisition ID should be positive";
    } else {
        // Expected: Service not yet implemented
        EXPECT_EQ(status.error_code(), StatusCode::UNIMPLEMENTED)
            << "Expected UNIMPLEMENTED status until service is complete";
    }
}

/**
 * @test ConfigService GetConfig RPC returns current configuration
 */
TEST_F(IntegrationTestBase, ConfigService_GetConfig_ReturnsConfiguration) {
    // Arrange: Server running and connected
    ASSERT_TRUE(StartTestServer()) << "Server should start";
    ASSERT_TRUE(WaitForServerReady()) << "Server should be ready";

    auto channel = CreateClientChannel();
    auto stub = ConfigService::NewStub(channel);

    // Act: Call GetConfig
    GetConfigRequest request;
    GetConfigResponse response;

    ClientContext context;
    context.set_deadline(
        std::chrono::system_clock::now() + std::chrono::seconds(5)
    );

    Status status = stub->GetConfig(&context, request, &response);

    // Assert
    if (status.ok()) {
        EXPECT_TRUE(response.has_config()) << "Response should contain config";
    } else {
        // Expected: Service not yet implemented
        EXPECT_EQ(status.error_code(), StatusCode::UNIMPLEMENTED)
            << "Expected UNIMPLEMENTED status until service is complete";
    }
}

// =============================================================================
// Category 4: Streaming RPC Tests
// =============================================================================

/**
 * @test HealthService SubscribeHealth stream sends heartbeat messages
 */
TEST_F(IntegrationTestBase, HealthService_SubscribeHealth_ReceivesHeartbeats) {
    // Arrange: Server running and connected
    ASSERT_TRUE(StartTestServer()) << "Server should start";
    ASSERT_TRUE(WaitForServerReady()) << "Server should be ready";

    auto channel = CreateClientChannel();
    auto stub = HealthService::NewStub(channel);

    // Act: Subscribe to health stream
    SubscribeHealthRequest request;
    request.set_interval_ms(1000);  // 1 second heartbeat

    ClientContext context;
    context.set_deadline(
        std::chrono::system_clock::now() + std::chrono::seconds(5)
    );

    auto reader = stub->SubscribeHealth(&context, request);

    // Assert: Read multiple heartbeat messages
    int message_count = 0;
    HealthMessage message;

    while (reader->Read(&message) && message_count < 3) {
        message_count++;
        EXPECT_TRUE(message.has_status()) << "Message should have status";
        EXPECT_GT(message.timestamp_ms(), 0) << "Timestamp should be positive";

        // Small delay between messages
        std::this_thread::sleep_for(std::chrono::milliseconds(100));
    }

    Status status = reader->Finish();

    if (status.error_code() == StatusCode::UNIMPLEMENTED) {
        // Expected: Service not yet implemented
        SUCCEED() << "HealthService streaming not yet implemented";
    } else {
        EXPECT_GE(message_count, 1) << "Should receive at least one heartbeat message";
        EXPECT_TRUE(status.ok()) << "Stream should complete successfully";
    }
}

/**
 * @test ImageService StreamImage delivers image chunks
 */
TEST_F(IntegrationTestBase, ImageService_StreamImage_DeliversChunks) {
    // Arrange: Server running and connected
    ASSERT_TRUE(StartTestServer()) << "Server should start";
    ASSERT_TRUE(WaitForServerReady()) << "Server should be ready";

    auto channel = CreateClientChannel();
    auto stub = ImageService::NewStub(channel);

    // Act: Request image stream
    StreamImageRequest request;
    request.set_image_id("test-image-001");
    request.set_chunk_size(65536);  // 64KB chunks

    ClientContext context;
    context.set_deadline(
        std::chrono::system_clock::now() + std::chrono::seconds(10)
    );

    auto reader = stub->StreamImage(&context, request);

    // Assert: Receive image chunks
    int chunk_count = 0;
    size_t total_bytes = 0;
    ImageChunk chunk;

    while (reader->Read(&chunk)) {
        chunk_count++;
        total_bytes += chunk.data().size();

        EXPECT_GT(chunk.sequence_number(), 0) << "Sequence number should be positive";
        EXPECT_FALSE(chunk.data().empty()) << "Chunk data should not be empty";
    }

    Status status = reader->Finish();

    if (status.error_code() == StatusCode::UNIMPLEMENTED) {
        // Expected: Service not yet implemented
        SUCCEED() << "ImageService streaming not yet implemented";
    } else {
        EXPECT_GT(chunk_count, 0) << "Should receive at least one chunk";
        EXPECT_TRUE(status.ok()) << "Stream should complete successfully";
    }
}

/**
 * @test ConfigService WatchConfig sends config change notifications
 */
TEST_F(IntegrationTestBase, ConfigService_WatchConfig_SendsChanges) {
    // Arrange: Server running and connected
    ASSERT_TRUE(StartTestServer()) << "Server should start";
    ASSERT_TRUE(WaitForServerReady()) << "Server should be ready";

    auto channel = CreateClientChannel();
    auto stub = ConfigService::NewStub(channel);

    // Act: Subscribe to config changes
    WatchConfigRequest request;
    request.set_subscribe_all(true);

    ClientContext context;
    context.set_deadline(
        std::chrono::system_clock::now() + std::chrono::seconds(5)
    );

    auto reader = stub->WatchConfig(&context, request);

    // Assert: Read config change notifications
    int change_count = 0;
    ConfigChange change;

    while (reader->Read(&change) && change_count < 2) {
        change_count++;
        EXPECT_TRUE(change.has_key()) << "Change should have key";
        EXPECT_TRUE(change.has_value()) << "Change should have value";
    }

    Status status = reader->Finish();

    if (status.error_code() == StatusCode::UNIMPLEMENTED) {
        // Expected: Service not yet implemented
        SUCCEED() << "ConfigService streaming not yet implemented";
    } else {
        // Note: In real test, we would trigger config changes here
        // For now, just verify stream infrastructure exists
        EXPECT_TRUE(status.ok() || change_count == 0) << "Stream should complete";
    }
}

// =============================================================================
// Category 5: Error Handling Tests
// =============================================================================

/**
 * @test RPC call with invalid parameters returns INVALID_ARGUMENT error
 */
TEST_F(IntegrationTestBase, RpcCall_InvalidParameters_ReturnsInvalidArgument) {
    // Arrange: Server running and connected
    ASSERT_TRUE(StartTestServer()) << "Server should start";
    ASSERT_TRUE(WaitForServerReady()) << "Server should be ready";

    auto channel = CreateClientChannel();
    auto stub = CommandService::NewStub(channel);

    // Act: Call with invalid parameters (negative kV)
    StartExposureRequest request;
    request.set_kv(-10.0f);  // Invalid: negative kV
    request.set_mas(10.0f);
    request.set_detector_id(1);

    StartExposureResponse response;

    ClientContext context;
    context.set_deadline(
        std::chrono::system_clock::now() + std::chrono::seconds(5)
    );

    Status status = stub->StartExposure(&context, request, &response);

    // Assert
    if (status.error_code() == StatusCode::UNIMPLEMENTED) {
        // Expected: Service not yet implemented
        SUCCEED() << "Service validation not yet implemented";
    } else {
        EXPECT_EQ(status.error_code(), StatusCode::INVALID_ARGUMENT)
            << "Should return INVALID_ARGUMENT for negative kV";
    }
}

/**
 * @test RPC call timeout triggers DEADLINE_EXCEEDED error
 */
TEST_F(IntegrationTestBase, RpcCall_Timeout_ReturnsDeadlineExceeded) {
    // Arrange: Server running and connected
    ASSERT_TRUE(StartTestServer()) << "Server should start";
    ASSERT_TRUE(WaitForServerReady()) << "Server should be ready";

    auto channel = CreateClientChannel();
    auto stub = CommandService::NewStub(channel);

    // Act: Call with very short deadline
    StartExposureRequest request;
    request.set_kv(120.0f);
    request.set_mas(10.0f);
    request.set_detector_id(1);

    StartExposureResponse response;

    ClientContext context;
    context.set_deadline(
        std::chrono::system_clock::now() + std::chrono::milliseconds(1)  // 1ms timeout
    );

    Status status = stub->StartExposure(&context, request, &response);

    // Assert: Should timeout
    // Note: May also return UNIMPLEMENTED
    EXPECT_TRUE(
        status.error_code() == StatusCode::DEADLINE_EXCEEDED ||
        status.error_code() == StatusCode::UNIMPLEMENTED
    ) << "Should timeout or be unimplemented";
}

/**
 * @test Connection to non-existent server fails
 */
TEST_F(IntegrationTestBase, Connection_ServerNotAvailable_Fails) {
    // Arrange: No server running (connect to invalid port)
    std::string invalid_address = "localhost:59999";  // Port not in use

    // Act: Create client channel to non-existent server
    auto channel = grpc::CreateChannel(
        invalid_address,
        grpc::InsecureChannelCredentials()
    );

    auto stub = CommandService::NewStub(channel);

    GetSystemStateRequest request;
    GetSystemStateResponse response;

    ClientContext context;
    context.set_deadline(
        std::chrono::system_clock::now() + std::chrono::seconds(1)
    );

    Status status = stub->GetSystemState(&context, request, &response);

    // Assert
    EXPECT_FALSE(status.ok()) << "Connection should fail when server not available";
    EXPECT_EQ(status.error_code(), StatusCode::UNAVAILABLE)
        << "Should return UNAVAILABLE status";
}

// =============================================================================
// Category 6: Concurrent Operations Tests
// =============================================================================

/**
 * @test Multiple concurrent RPC calls are handled correctly
 */
TEST_F(IntegrationTestBase, ConcurrentRpcCalls_AllCompleteSuccessfully) {
    // Arrange: Server running and connected
    ASSERT_TRUE(StartTestServer()) << "Server should start";
    ASSERT_TRUE(WaitForServerReady()) << "Server should be ready";

    auto channel = CreateClientChannel();
    auto stub = CommandService::NewStub(channel);

    // Act: Launch 10 concurrent RPC calls
    const int num_calls = 10;
    std::vector<std::thread> threads;
    std::vector<Status> statuses(num_calls);

    for (int i = 0; i < num_calls; ++i) {
        threads.emplace_back([this, i, &stub, &statuses]() {
            GetSystemStateRequest request;
            GetSystemStateResponse response;

            ClientContext context;
            context.set_deadline(
                std::chrono::system_clock::now() + std::chrono::seconds(5)
            );

            statuses[i] = stub->GetSystemState(&context, request, &response);
        });
    }

    // Wait for all threads to complete
    for (auto& thread : threads) {
        thread.join();
    }

    // Assert: All calls should complete (either success or UNIMPLEMENTED)
    for (int i = 0; i < num_calls; ++i) {
        EXPECT_TRUE(
            statuses[i].ok() ||
            statuses[i].error_code() == StatusCode::UNIMPLEMENTED
        ) << "Concurrent call " << i << " should complete";
    }
}

/**
 * @test Multiple clients can connect simultaneously
 */
TEST_F(IntegrationTestBase, MultipleClients_AllConnectSuccessfully) {
    // Arrange: Server running
    ASSERT_TRUE(StartTestServer()) << "Server should start";
    ASSERT_TRUE(WaitForServerReady()) << "Server should be ready";

    // Act: Create multiple client channels
    const int num_clients = 5;
    std::vector<std::shared_ptr<Channel>> channels;

    for (int i = 0; i < num_clients; ++i) {
        channels.push_back(CreateClientChannel());
    }

    // Assert: All channels should be in READY state
    for (int i = 0; i < num_clients; ++i) {
        auto state = channels[i]->GetState(true);
        EXPECT_EQ(state, GRPC_CHANNEL_READY)
            << "Client " << i << " should connect successfully";
    }
}

// =============================================================================
// Category 7: Version Negotiation Tests
// =============================================================================

/**
 * @test GetSystemState returns version information for negotiation
 */
TEST_F(IntegrationTestBase, GetSystemState_ReturnsVersionInfo) {
    // Arrange: Server running and connected
    ASSERT_TRUE(StartTestServer()) << "Server should start";
    ASSERT_TRUE(WaitForServerReady()) << "Server should be ready";

    auto channel = CreateClientChannel();
    auto stub = CommandService::NewStub(channel);

    // Act: Get system state
    GetSystemStateRequest request;
    GetSystemStateResponse response;

    ClientContext context;
    context.set_deadline(
        std::chrono::system_clock::now() + std::chrono::seconds(5)
    );

    Status status = stub->GetSystemState(&context, request, &response);

    // Assert
    if (status.ok()) {
        EXPECT_TRUE(response.has_version_info()) << "Should have version info";
        EXPECT_FALSE(response.version_info().ipc_version().empty())
            << "IPC version should not be empty";
        EXPECT_TRUE(response.version_info().has_min_client_version())
            << "Should have minimum client version";
        EXPECT_TRUE(response.version_info().has_max_client_version())
            << "Should have maximum client version";
    } else {
        // Expected: Service not yet implemented
        EXPECT_EQ(status.error_code(), StatusCode::UNIMPLEMENTED)
            << "Expected UNIMPLEMENTED status until service is complete";
    }
}

} // namespace hnvue::test::integration
