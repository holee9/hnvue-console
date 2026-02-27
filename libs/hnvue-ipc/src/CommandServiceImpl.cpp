/**
 * @file CommandServiceImpl.cpp
 * @brief Implementation of CommandService (GUI -> Core Engine commands)
 * SPEC-IPC-001 Section 4.2.2: CommandService with 5 RPCs
 */

#include "hnvue/ipc/CommandServiceImpl.h"

namespace hnvue::ipc {

// Parameter validation constants (SPEC-specific)
static constexpr float MIN_KV = 20.0f;
static constexpr float MAX_KV = 150.0f;
static constexpr float MIN_MAS = 0.1f;
static constexpr float MAX_MAS = 1000.0f;
static constexpr uint32_t MAX_DETECTOR_ID = 16;
static constexpr float MAX_COLLIMATOR_OPENING_MM = 300.0f;

CommandServiceImpl::CommandServiceImpl(std::shared_ptr<spdlog::logger> logger)
    : logger_(logger)
    , system_state_(SystemState::SYSTEM_STATE_INITIALIZING)
    , next_acquisition_id_(1) {
    logger_->info("CommandServiceImpl initialized");
}

grpc::Status CommandServiceImpl::StartExposure(
    grpc::ServerContext* context,
    const StartExposureRequest* request,
    StartExposureResponse* response) {

    logger_->info("StartExposure called");

    // Validate request
    if (!request->has_parameters()) {
        logger_->warn("StartExposure failed: missing parameters");
        *response->mutable_error() = CreateValidationError("Missing exposure parameters");
        response->set_success(false);
        return grpc::Status::OK;
    }

    if (!ValidateExposureParameters(request)) {
        logger_->warn("StartExposure failed: invalid parameters");
        *response->mutable_error() = CreateValidationError("Invalid exposure parameters");
        response->set_success(false);
        return grpc::Status::OK;
    }

    // Check system state
    SystemState current_state = GetSystemState();
    if (current_state != SystemState::SYSTEM_STATE_READY) {
        logger_->warn("StartExposure failed: system not ready (state: {})",
                      static_cast<int>(current_state));
        auto* error = response->mutable_error();
        error->set_code(ErrorCode::ERROR_CODE_HARDWARE_NOT_READY);
        error->set_message("System not ready for exposure");
        response->set_success(false);
        return grpc::Status::OK;
    }

    // Generate acquisition ID
    uint64_t acquisition_id = GenerateAcquisitionId();

    // Update system state
    SetSystemState(SystemState::SYSTEM_STATE_ACQUIRING);

    // Populate response
    response->set_success(true);
    response->set_acquisition_id(acquisition_id);
    response->mutable_error()->set_code(ErrorCode::ERROR_CODE_OK);
    response->mutable_response_timestamp()->set_microseconds_since_start(0);

    logger_->info("StartExposure succeeded: acquisition_id={}", acquisition_id);

    // TODO: Integrate with HAL (SPEC-HAL-001) to actually trigger exposure
    // For now, this is a mock implementation

    return grpc::Status::OK;
}

grpc::Status CommandServiceImpl::AbortExposure(
    grpc::ServerContext* context,
    const AbortExposureRequest* request,
    AbortExposureResponse* response) {

    uint64_t acquisition_id = request->acquisition_id();
    logger_->info("AbortExposure called for acquisition_id={}", acquisition_id);

    // TODO: Check if acquisition exists and is in progress
    // TODO: Signal acquisition thread to abort

    response->set_success(true);
    response->mutable_error()->set_code(ErrorCode::ERROR_CODE_OK);
    response->mutable_response_timestamp()->set_microseconds_since_start(0);

    logger_->info("AbortExposure succeeded for acquisition_id={}", acquisition_id);
    return grpc::Status::OK;
}

grpc::Status CommandServiceImpl::SetCollimator(
    grpc::ServerContext* context,
    const SetCollimatorRequest* request,
    SetCollimatorResponse* response) {

    logger_->info("SetCollimator called");

    if (!request->has_position()) {
        logger_->warn("SetCollimator failed: missing position");
        *response->mutable_error() = CreateValidationError("Missing collimator position");
        response->set_success(false);
        return grpc::Status::OK;
    }

    if (!ValidateCollimatorPosition(request)) {
        logger_->warn("SetCollimator failed: invalid position");
        *response->mutable_error() = CreateValidationError("Invalid collimator position");
        response->set_success(false);
        return grpc::Status::OK;
    }

    // Apply position (TODO: Integrate with HAL)
    const auto& requested = request->position();
    response->mutable_actual_position()->CopyFrom(requested);
    response->set_success(true);
    response->mutable_error()->set_code(ErrorCode::ERROR_CODE_OK);
    response->mutable_response_timestamp()->set_microseconds_since_start(0);

    logger_->info("SetCollimator succeeded: L={}/R={}/T={}/B={}",
                  requested.left_mm(), requested.right_mm(),
                  requested.top_mm(), requested.bottom_mm());

    return grpc::Status::OK;
}

grpc::Status CommandServiceImpl::RunCalibration(
    grpc::ServerContext* context,
    const RunCalibrationRequest* request,
    RunCalibrationResponse* response) {

    auto mode = request->mode();
    logger_->info("RunCalibration called with mode={}", static_cast<int>(mode));

    // Validate calibration mode
    if (mode == hnvue::ipc::protobuf::CALIBRATION_MODE_UNSPECIFIED) {
        logger_->warn("RunCalibration failed: unspecified mode");
        *response->mutable_error() = CreateValidationError("Unspecified calibration mode");
        response->set_success(false);
        return grpc::Status::OK;
    }

    // TODO: Execute calibration (integrate with HAL)

    response->set_success(true);
    response->mutable_error()->set_code(ErrorCode::ERROR_CODE_OK);
    response->mutable_response_timestamp()->set_microseconds_since_start(0);

    logger_->info("RunCalibration succeeded");
    return grpc::Status::OK;
}

grpc::Status CommandServiceImpl::GetSystemState(
    grpc::ServerContext* context,
    const GetSystemStateRequest* request,
    GetSystemStateResponse* response) {

    // Return current state
    SystemState state = GetSystemState();
    response->set_state(state);
    response->mutable_response_timestamp()->set_microseconds_since_start(0);

    logger_->debug("GetSystemState returned: {}", static_cast<int>(state));
    return grpc::Status::OK;
}

void CommandServiceImpl::SetSystemState(SystemState state) {
    system_state_.store(state, std::memory_order_release);
    logger_->info("System state changed to: {}", static_cast<int>(state));
}

SystemState CommandServiceImpl::GetSystemState() const {
    return system_state_.load(std::memory_order_acquire);
}

uint64_t CommandServiceImpl::GenerateAcquisitionId() {
    return next_acquisition_id_.fetch_add(1, std::memory_order_relaxed);
}

bool CommandServiceImpl::ValidateExposureParameters(const StartExposureRequest* request) const {
    const auto& params = request->parameters();

    // Validate kV
    if (params.kv() < MIN_KV || params.kv() > MAX_KV) {
        logger_->debug("kV validation failed: {} (range: {}-{})",
                      params.kv(), MIN_KV, MAX_KV);
        return false;
    }

    // Validate mAs
    if (params.mas() < MIN_MAS || params.mas() > MAX_MAS) {
        logger_->debug("mAs validation failed: {} (range: {}-{})",
                      params.mas(), MIN_MAS, MAX_MAS);
        return false;
    }

    // Validate detector_id
    if (params.detector_id() == 0 || params.detector_id() > MAX_DETECTOR_ID) {
        logger_->debug("detector_id validation failed: {} (range: 1-{})",
                      params.detector_id(), MAX_DETECTOR_ID);
        return false;
    }

    // Validate transfer_mode
    if (params.transfer_mode() == hnvue::ipc::protobuf::IMAGE_TRANSFER_MODE_UNSPECIFIED) {
        logger_->debug("transfer_mode validation failed: unspecified");
        return false;
    }

    return true;
}

bool CommandServiceImpl::ValidateCollimatorPosition(const SetCollimatorRequest* request) const {
    const auto& pos = request->position();

    // Validate all positions are non-negative and within bounds
    if (pos.left_mm() < 0 || pos.left_mm() > MAX_COLLIMATOR_OPENING_MM) return false;
    if (pos.right_mm() < 0 || pos.right_mm() > MAX_COLLIMATOR_OPENING_MM) return false;
    if (pos.top_mm() < 0 || pos.top_mm() > MAX_COLLIMATOR_OPENING_MM) return false;
    if (pos.bottom_mm() < 0 || pos.bottom_mm() > MAX_COLLIMATOR_OPENING_MM) return false;

    return true;
}

hnvue::ipc::protobuf::IpcError CommandServiceImpl::CreateValidationError(const std::string& message) const {
    hnvue::ipc::protobuf::IpcError error;
    error.set_code(ErrorCode::ERROR_CODE_INVALID_ARGUMENT);
    error.set_message(message);
    return error;
}

} // namespace hnvue::ipc
