/**
 * @file IpcServer.cpp
 * @brief gRPC server lifecycle management implementation
 * SPEC-IPC-001 Section 4.3.1: Server startup and graceful shutdown
 */

#include "hnvue/ipc/IpcServer.h"
#include "hnvue/ipc/CommandServiceImpl.h"
#include "hnvue/ipc/ImageServiceImpl.h"
#include "hnvue/ipc/HealthServiceImpl.h"
#include "hnvue/ipc/ConfigServiceImpl.h"

namespace hnvue::ipc {

// Interface version (SPEC-IPC-001 Section 4.5)
static constexpr uint32_t IPC_INTERFACE_VERSION_MAJOR = 1;
static constexpr uint32_t IPC_INTERFACE_VERSION_MINOR = 0;
static constexpr uint32_t IPC_INTERFACE_VERSION_PATCH = 0;

IpcServer::IpcServer(
    const std::string& server_address,
    std::shared_ptr<spdlog::logger> logger)
    : server_address_(server_address)
    , logger_(logger)
    , server_(nullptr)
    , command_service_(nullptr)
    , image_service_(nullptr)
    , health_service_(nullptr)
    , config_service_(nullptr)
    , is_running_(false) {
}

IpcServer::~IpcServer() {
    if (is_running_) {
        Stop();
    }
}

bool IpcServer::Start() {
    if (is_running_) {
        logger_->warn("IpcServer already running on {}", server_address_);
        return true;
    }

    logger_->info("Starting HnVue IPC server on {}", server_address_);

    try {
        grpc::ServerBuilder builder;

        // Add listening port
        builder.AddListeningPort(server_address_, grpc::InsecureServerCredentials());

        // Register all services
        RegisterServices(builder);

        // Build and start server
        server_ = builder.BuildAndStart();
        if (!server_) {
            logger_->error("Failed to build gRPC server");
            return false;
        }

        is_running_ = true;
        LogStartupInfo();

        logger_->info("IpcServer started successfully");
        return true;

    } catch (const std::exception& e) {
        logger_->error("Exception starting IpcServer: {}", e.what());
        return false;
    }
}

void IpcServer::Stop(int timeout_ms) {
    if (!is_running_) {
        logger_->warn("IpcServer not running");
        return;
    }

    logger_->info("Stopping IpcServer (timeout: {}ms)", timeout_ms);

    // SPEC-IPC-001 Section 4.3.4: Send shutting down state before closing
    // This would be done via HealthServiceImpl state change notification

    try {
        if (server_) {
            // Graceful shutdown with timeout
            server_->Shutdown(std::chrono::system_clock::now() +
                            std::chrono::milliseconds(timeout_ms));
        }

        is_running_ = false;
        logger_->info("IpcServer stopped");

    } catch (const std::exception& e) {
        logger_->error("Exception stopping IpcServer: {}", e.what());
    }
}

bool IpcServer::IsRunning() const {
    return is_running_;
}

const std::string& IpcServer::GetServerAddress() const {
    return server_address_;
}

std::string IpcServer::GetInterfaceVersion() const {
    return fmt::format("{}.{}.{}",
        IPC_INTERFACE_VERSION_MAJOR,
        IPC_INTERFACE_VERSION_MINOR,
        IPC_INTERFACE_VERSION_PATCH);
}

void IpcServer::RegisterServices(grpc::ServerBuilder& builder) {
    // Create service instances
    command_service_ = std::make_unique<CommandServiceImpl>(logger_);
    image_service_ = std::make_unique<ImageServiceImpl>(logger_);
    health_service_ = std::make_unique<HealthServiceImpl>(logger_);
    config_service_ = std::make_unique<ConfigServiceImpl>(logger_);

    // Register with server
    builder.RegisterService(command_service_.get());
    builder.RegisterService(image_service_.get());
    builder.RegisterService(health_service_.get());
    builder.RegisterService(config_service_.get());

    logger_->debug("Registered 4 services: Command, Image, Health, Config");
}

void IpcServer::LogStartupInfo() {
    logger_->info("IpcServer listening on: {}", server_address_);
    logger_->info("IPC Interface Version: {}.{}.{}",
        IPC_INTERFACE_VERSION_MAJOR,
        IPC_INTERFACE_VERSION_MINOR,
        IPC_INTERFACE_VERSION_PATCH);
}

} // namespace hnvue::ipc
