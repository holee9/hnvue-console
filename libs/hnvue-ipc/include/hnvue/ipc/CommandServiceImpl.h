/**
 * @file CommandServiceImpl.h
 * @brief Implementation of CommandService (GUI -> Core Engine commands)
 * SPEC-IPC-001 Section 4.2.2: CommandService with 5 RPCs
 *
 * This service handles synchronous commands from the GUI to the Core Engine:
 * - StartExposure: Initiate X-ray exposure sequence
 * - AbortExposure: Cancel in-progress exposure
 * - SetCollimator: Adjust collimator blade positions
 * - RunCalibration: Execute calibration routine
 * - GetSystemState: Query current system state
 */

#ifndef HNVE_IPC_COMMAND_SERVICE_IMPL_H
#define HNVE_IPC_COMMAND_SERVICE_IMPL_H

#include <grpcpp/grpcpp.h>
#include <memory>
#include <atomic>
#include <mutex>
#include <spdlog/spdlog.h>

// Generated protobuf headers (will be in build directory)
#include "hnvue_command.grpc.pb.h"
#include "hnvue_command.pb.h"

namespace hnvue::ipc {

using hnvue::ipc::protobuf::CommandService;
using hnvue::ipc::protobuf::StartExposureRequest;
using hnvue::ipc::protobuf::StartExposureResponse;
using hnvue::ipc::protobuf::AbortExposureRequest;
using hnvue::ipc::protobuf::AbortExposureResponse;
using hnvue::ipc::protobuf::SetCollimatorRequest;
using hnvue::ipc::protobuf::SetCollimatorResponse;
using hnvue::ipc::protobuf::RunCalibrationRequest;
using hnvue::ipc::protobuf::RunCalibrationResponse;
using hnvue::ipc::protobuf::GetSystemStateRequest;
using hnvue::ipc::protobuf::GetSystemStateResponse;
using hnvue::ipc::protobuf::SystemState;

/**
 * @class CommandServiceImpl
 * @brief gRPC service implementation for GUI -> Core commands
 *
 * Thread safety: This class must be thread-safe as gRPC may call
 * RPC methods from multiple threads concurrently.
 *
 * Error handling per SPEC-IPC-001 Section 4.4.1:
 * - All RPCs return IpcError in response
 * - No call should be left unresponded (5s timeout default)
 * - Validation errors return ERROR_CODE_INVALID_ARGUMENT
 */
class CommandServiceImpl final : public CommandService::Service {
public:
    /**
     * @brief Construct CommandService implementation
     * @param logger Logger instance
     */
    explicit CommandServiceImpl(std::shared_ptr<spdlog::logger> logger = spdlog::default_logger());

    ~CommandServiceImpl() override = default;

    // Non-copyable, non-movable
    CommandServiceImpl(const CommandServiceImpl&) = delete;
    CommandServiceImpl& operator=(const CommandServiceImpl&) = delete;

    /**
     * @brief Initiate an X-ray exposure sequence
     *
     * Validates exposure parameters and initiates acquisition.
     * Returns unique acquisition_id for tracking.
     *
     * SPEC-IPC-001 Section 4.2.2:
     * - Validates kV, mAs, detector_id ranges
     * - Returns acquisition_id for subsequent operations
     *
     * @param context gRPC server context
     * @param request Exposure parameters
     * @param response Success status, acquisition_id, error info
     * @return gRPC status code
     */
    grpc::Status StartExposure(
        grpc::ServerContext* context,
        const StartExposureRequest* request,
        StartExposureResponse* response) override;

    /**
     * @brief Abort an in-progress exposure
     *
     * Cancels the acquisition with the given acquisition_id.
     *
     * SPEC-IPC-001 Section 4.2.2:
     * - Validates acquisition_id exists
     * - Signals acquisition thread to abort
     *
     * @param context gRPC server context
     * @param request Contains acquisition_id to abort
     * @param response Success status, error info
     * @return gRPC status code
     */
    grpc::Status AbortExposure(
        grpc::ServerContext* context,
        const AbortExposureRequest* request,
        AbortExposureResponse* response) override;

    /**
     * @brief Move collimator blades to specified position
     *
     * Validates and applies collimator position.
     * Returns actual position after command is applied.
     *
     * SPEC-IPC-001 Section 4.2.2:
     * - Validates position bounds (0 to max_opening_mm)
     * - Returns actual_position in response
     *
     * @param context gRPC server context
     * @param request Collimator position (left, right, top, bottom)
     * @param response Success status, actual_position, error info
     * @return gRPC status code
     */
    grpc::Status SetCollimator(
        grpc::ServerContext* context,
        const SetCollimatorRequest* request,
        SetCollimatorResponse* response) override;

    /**
     * @brief Execute calibration routine
     *
     * Runs the specified calibration mode.
     *
     * SPEC-IPC-001 Section 4.2.2:
     * - Supports DARK_FIELD, FLAT_FIELD, GAIN modes
     * - Returns success when calibration completes
     *
     * @param context gRPC server context
     * @param request Calibration mode
     * @param response Success status, error info
     * @return gRPC status code
     */
    grpc::Status RunCalibration(
        grpc::ServerContext* context,
        const RunCalibrationRequest* request,
        RunCalibrationResponse* response) override;

    /**
     * @brief Query current system state
     *
     * Returns synchronous snapshot of system state.
     *
     * SPEC-IPC-001 Section 4.2.2:
     * - Returns current SystemState enum value
     * - Includes timestamp
     *
     * @param context gRPC server context
     * @param request Empty request
     * @param response Current system state, timestamp
     * @return gRPC status code
     */
    grpc::Status GetSystemState(
        grpc::ServerContext* context,
        const GetSystemStateRequest* request,
        GetSystemStateResponse* response) override;

    /**
     * @brief Set the current system state (for internal use)
     * @param state New system state
     */
    void SetSystemState(SystemState state);

    /**
     * @brief Get the current system state
     * @return Current system state
     */
    SystemState GetSystemState() const;

private:
    std::shared_ptr<spdlog::logger> logger_;

    // System state management (thread-safe)
    std::atomic<SystemState> system_state_;

    // Acquisition ID counter (atomic for thread safety)
    std::atomic<uint64_t> next_acquisition_id_;

    // Mutex for acquisition tracking
    mutable std::mutex acquisition_mutex_;

    /**
     * @brief Generate a unique acquisition ID
     * @return New acquisition ID
     */
    uint64_t GenerateAcquisitionId();

    /**
     * @brief Validate exposure parameters
     * @param request Request with parameters to validate
     * @return true if valid, false otherwise
     */
    bool ValidateExposureParameters(const StartExposureRequest* request) const;

    /**
     * @brief Validate collimator position
     * @param request Request with position to validate
     * @return true if valid, false otherwise
     */
    bool ValidateCollimatorPosition(const SetCollimatorRequest* request) const;

    /**
     * @brief Create IpcError for validation failure
     * @param message Error message
     * @return Populated IpcError
     */
    hnvue::ipc::protobuf::IpcError CreateValidationError(const std::string& message) const;
};

} // namespace hnvue::ipc

#endif // HNVE_IPC_COMMAND_SERVICE_IMPL_H
