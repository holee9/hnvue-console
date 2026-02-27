/**
 * @file IpcServer.h
 * @brief gRPC server lifecycle management for HnVue IPC
 * SPEC-IPC-001 Section 4.3.1: Server startup and graceful shutdown
 *
 * This class manages the gRPC server that hosts all IPC services:
 * - CommandService: GUI to Core commands
 * - ImageService: Image streaming to GUI
 * - HealthService: Health monitoring events
 * - ConfigService: Configuration synchronization
 */

#ifndef HNVE_IPC_IPC_SERVER_H
#define HNVE_IPC_IPC_SERVER_H

#include <memory>
#include <string>
#include <grpcpp/grpcpp.h>
#include <spdlog/spdlog.h>

namespace hnvue::ipc {

// Forward declarations for service implementations
class CommandServiceImpl;
class ImageServiceImpl;
class HealthServiceImpl;
class ConfigServiceImpl;

/**
 * @brief Interface version constants (SPEC-IPC-001 Section 4.5)
 */
struct IpcInterfaceVersion
{
    static constexpr uint32_t kMajor = 1;
    static constexpr uint32_t kMinor = 0;
    static constexpr uint32_t kPatch = 0;
};

/**
 * @class IpcServer
 * @brief Manages gRPC server lifecycle for HnVue IPC
 *
 * Responsibilities:
 * - Bind to configured port (default: localhost:50051)
 * - Register all four service implementations
 * - Handle graceful shutdown
 * - Log lifecycle events
 */
class IpcServer {
public:
    /**
     * @brief Construct IpcServer with configuration
     * @param server_address Server binding address (e.g., "localhost:50051")
     * @param logger Logger instance (defaults to spdlog default logger)
     */
    explicit IpcServer(
        const std::string& server_address = "localhost:50051",
        std::shared_ptr<spdlog::logger> logger = spdlog::default_logger()
    );

    /**
     * @brief Destructor - ensures server is stopped
     */
    ~IpcServer();

    // Non-copyable, non-movable (manages unique resources)
    IpcServer(const IpcServer&) = delete;
    IpcServer& operator=(const IpcServer&) = delete;
    IpcServer(IpcServer&&) = delete;
    IpcServer& operator=(IpcServer&&) = delete;

    /**
     * @brief Start the gRPC server
     *
     * Binds to the configured address and begins serving RPCs.
     * This is a blocking call - the server runs in the calling thread.
     *
     * @return true if server started successfully, false on error
     *
     * @pre All services are registered
     * @post Server is accepting connections on configured port
     *
     * SPEC-IPC-001 Section 4.3.1:
     * - Bind to configured port
     * - Register all four services
     * - Log bound address and InterfaceVersion
     */
    bool Start();

    /**
     * @brief Stop the gRPC server gracefully
     *
     * Initiates graceful shutdown:
     * - Stops accepting new connections
     * - Waits for existing RPCs to complete (with timeout)
     * - Releases port binding
     *
     * SPEC-IPC-001 Section 4.3.4:
     * - Send SYSTEM_STATE_SHUTTING_DOWN before closing connections
     *
     * @param timeout_ms Maximum time to wait for RPC completion (ms)
     */
    void Stop(int timeout_ms = 5000);

    /**
     * @brief Check if server is currently running
     * @return true if server is accepting connections
     */
    bool IsRunning() const;

    /**
     * @brief Get the server's bound address
     * @return The address the server is listening on
     */
    const std::string& GetServerAddress() const;

    /**
     * @brief Get the InterfaceVersion for compatibility checking
     * @return Version string (major.minor.patch)
     *
     * SPEC-IPC-001 Section 4.5: Version negotiation
     */
    std::string GetInterfaceVersion() const;

private:
    // Server configuration
    std::string server_address_;
    std::shared_ptr<spdlog::logger> logger_;

    // gRPC server
    std::unique_ptr<grpc::Server> server_;

    // Service implementations (owned by server after registration)
    std::unique_ptr<CommandServiceImpl> command_service_;
    std::unique_ptr<ImageServiceImpl> image_service_;
    std::unique_ptr<HealthServiceImpl> health_service_;
    std::unique_ptr<ConfigServiceImpl> config_service_;

    // Server state
    bool is_running_;

    /**
     * @brief Register all service implementations
     *
     * Called during Start() to add services to the server builder.
     *
     * @param builder The ServerBuilder to register services with
     */
    void RegisterServices(grpc::ServerBuilder& builder);

    /**
     * @brief Log server startup information
     *
     * Logs bound address and InterfaceVersion per SPEC-IPC-001 Section 4.3.1.
     */
    void LogStartupInfo();

public:
    // Interface version constants for access from main.cpp
    static constexpr uint32_t kInterfaceVersionMajor = IpcInterfaceVersion::kMajor;
    static constexpr uint32_t kInterfaceVersionMinor = IpcInterfaceVersion::kMinor;
    static constexpr uint32_t kInterfaceVersionPatch = IpcInterfaceVersion::kPatch;
};

} // namespace hnvue::ipc

#endif // HNVE_IPC_IPC_SERVER_H
