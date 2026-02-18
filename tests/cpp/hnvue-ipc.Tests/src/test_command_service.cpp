/**
 * @file test_command_service.cpp
 * @brief Unit tests for CommandServiceImpl
 * SPEC-IPC-001 Section 4.2.2: CommandService with 5 RPCs
 */

#include <gtest/gtest.h>
#include <gmock/gmock.h>
#include <memory>
#include <spdlog/sinks/stdout_color_sinks.h>

// Include generated protobuf headers
#include "hnvue_command.grpc.pb.h"
#include "hnvue_command.pb.h"

// Include service implementation
#include "hnvue/ipc/CommandServiceImpl.h"

using namespace hnvue::ipc;
using namespace hnvue::ipc::protobuf;
using grpc::Status;
using grpc::ServerContext;

namespace hnvue::test {

/**
 * @class CommandServiceTestFixture
 * @brief Test fixture for CommandServiceImpl tests
 */
class CommandServiceTestFixture : public ::testing::Test {
protected:
    void SetUp() override {
        // Create logger for tests
        logger_ = spdlog::stdout_color_mt("test_command");
        logger_->set_level(spdlog::level::debug);

        // Create service instance
        service_ = std::make_unique<CommandServiceImpl>(logger_);

        // Set system to READY state for most tests
        service_->SetSystemState(SystemState::SYSTEM_STATE_READY);
    }

    void TearDown() override {
        service_.reset();
        spdlog::drop("test_command");
    }

    std::shared_ptr<spdlog::logger> logger_;
    std::unique_ptr<CommandServiceImpl> service_;
};

/**
 * @test StartExposure accepts valid parameters
 * FR-IPC-04a: Initiate X-ray exposure with validation
 */
TEST_F(CommandServiceTestFixture, StartExposure_ValidParameters_ReturnsSuccess) {
    // Arrange: Create request with valid kV, mAs, detector_id
    StartExposureRequest request;
    request.mutable_parameters()->set_kv(120.0f);
    request.mutable_parameters()->set_mas(100.0f);
    request.mutable_parameters()->set_detector_id(1);
    request.mutable_parameters()->set_transfer_mode(IMAGE_TRANSFER_MODE_FULL_QUALITY);

    StartExposureResponse response;
    ServerContext context;

    // Act: Call StartExposure
    Status status = service_->StartExposure(&context, &request, &response);

    // Assert: Response indicates success
    EXPECT_TRUE(status.ok());
    EXPECT_TRUE(response.success());
    EXPECT_GT(response.acquisition_id(), 0u);
    EXPECT_EQ(response.error().code(), ERROR_CODE_OK);

    // Assert: System state changed to ACQUIRING
    EXPECT_EQ(service_->GetSystemState(), SystemState::SYSTEM_STATE_ACQUIRING);
}

/**
 * @test StartExposure rejects missing parameters
 */
TEST_F(CommandServiceTestFixture, StartExposure_MissingParameters_ReturnsValidationError) {
    // Arrange: Create request without parameters
    StartExposureRequest request;
    StartExposureResponse response;
    ServerContext context;

    // Act: Call StartExposure
    Status status = service_->StartExposure(&context, &request, &response);

    // Assert: Response indicates error
    EXPECT_TRUE(status.ok());  // gRPC status OK, but response contains error
    EXPECT_FALSE(response.success());
    EXPECT_EQ(response.error().code(), ERROR_CODE_INVALID_ARGUMENT);
    EXPECT_FALSE(response.error().message().empty());
}

/**
 * @test StartExposure rejects invalid kV (too low)
 */
TEST_F(CommandServiceTestFixture, StartExposure_KVTooLow_ReturnsValidationError) {
    // Arrange: Create request with kV below minimum
    StartExposureRequest request;
    request.mutable_parameters()->set_kv(10.0f);  // Below MIN_KV (20.0)
    request.mutable_parameters()->set_mas(100.0f);
    request.mutable_parameters()->set_detector_id(1);
    request.mutable_parameters()->set_transfer_mode(IMAGE_TRANSFER_MODE_FULL_QUALITY);

    StartExposureResponse response;
    ServerContext context;

    // Act: Call StartExposure
    Status status = service_->StartExposure(&context, &request, &response);

    // Assert: Response indicates validation error
    EXPECT_TRUE(status.ok());
    EXPECT_FALSE(response.success());
    EXPECT_EQ(response.error().code(), ERROR_CODE_INVALID_ARGUMENT);
}

/**
 * @test StartExposure rejects invalid kV (too high)
 */
TEST_F(CommandServiceTestFixture, StartExposure_KVTooHigh_ReturnsValidationError) {
    // Arrange: Create request with kV above maximum
    StartExposureRequest request;
    request.mutable_parameters()->set_kv(200.0f);  // Above MAX_KV (150.0)
    request.mutable_parameters()->set_mas(100.0f);
    request.mutable_parameters()->set_detector_id(1);
    request.mutable_parameters()->set_transfer_mode(IMAGE_TRANSFER_MODE_FULL_QUALITY);

    StartExposureResponse response;
    ServerContext context;

    // Act: Call StartExposure
    Status status = service_->StartExposure(&context, &request, &response);

    // Assert: Response indicates validation error
    EXPECT_TRUE(status.ok());
    EXPECT_FALSE(response.success());
    EXPECT_EQ(response.error().code(), ERROR_CODE_INVALID_ARGUMENT);
}

/**
 * @test StartExposure rejects invalid mAs (too low)
 */
TEST_F(CommandServiceTestFixture, StartExposure_MAsTooLow_ReturnsValidationError) {
    // Arrange: Create request with mAs below minimum
    StartExposureRequest request;
    request.mutable_parameters()->set_kv(120.0f);
    request.mutable_parameters()->set_mas(0.05f);  // Below MIN_MAS (0.1)
    request.mutable_parameters()->set_detector_id(1);
    request.mutable_parameters()->set_transfer_mode(IMAGE_TRANSFER_MODE_FULL_QUALITY);

    StartExposureResponse response;
    ServerContext context;

    // Act: Call StartExposure
    Status status = service_->StartExposure(&context, &request, &response);

    // Assert: Response indicates validation error
    EXPECT_TRUE(status.ok());
    EXPECT_FALSE(response.success());
    EXPECT_EQ(response.error().code(), ERROR_CODE_INVALID_ARGUMENT);
}

/**
 * @test StartExposure rejects invalid detector_id
 */
TEST_F(CommandServiceTestFixture, StartExposure_InvalidDetectorId_ReturnsValidationError) {
    // Arrange: Create request with invalid detector_id
    StartExposureRequest request;
    request.mutable_parameters()->set_kv(120.0f);
    request.mutable_parameters()->set_mas(100.0f);
    request.mutable_parameters()->set_detector_id(0);  // Invalid (must be >= 1)
    request.mutable_parameters()->set_transfer_mode(IMAGE_TRANSFER_MODE_FULL_QUALITY);

    StartExposureResponse response;
    ServerContext context;

    // Act: Call StartExposure
    Status status = service_->StartExposure(&context, &request, &response);

    // Assert: Response indicates validation error
    EXPECT_TRUE(status.ok());
    EXPECT_FALSE(response.success());
    EXPECT_EQ(response.error().code(), ERROR_CODE_INVALID_ARGUMENT);
}

/**
 * @test StartExposure rejects unspecified transfer mode
 */
TEST_F(CommandServiceTestFixture, StartExposure_UnspecifiedTransferMode_ReturnsValidationError) {
    // Arrange: Create request with unspecified transfer mode
    StartExposureRequest request;
    request.mutable_parameters()->set_kv(120.0f);
    request.mutable_parameters()->set_mas(100.0f);
    request.mutable_parameters()->set_detector_id(1);
    request.mutable_parameters()->set_transfer_mode(IMAGE_TRANSFER_MODE_UNSPECIFIED);

    StartExposureResponse response;
    ServerContext context;

    // Act: Call StartExposure
    Status status = service_->StartExposure(&context, &request, &response);

    // Assert: Response indicates validation error
    EXPECT_TRUE(status.ok());
    EXPECT_FALSE(response.success());
    EXPECT_EQ(response.error().code(), ERROR_CODE_INVALID_ARGUMENT);
}

/**
 * @test StartExposure rejects when system not ready
 */
TEST_F(CommandServiceTestFixture, StartExposure_SystemNotReady_ReturnsHardwareNotReadyError) {
    // Arrange: Set system to FAULT state
    service_->SetSystemState(SystemState::SYSTEM_STATE_FAULT);

    StartExposureRequest request;
    request.mutable_parameters()->set_kv(120.0f);
    request.mutable_parameters()->set_mas(100.0f);
    request.mutable_parameters()->set_detector_id(1);
    request.mutable_parameters()->set_transfer_mode(IMAGE_TRANSFER_MODE_FULL_QUALITY);

    StartExposureResponse response;
    ServerContext context;

    // Act: Call StartExposure
    Status status = service_->StartExposure(&context, &request, &response);

    // Assert: Response indicates hardware not ready
    EXPECT_TRUE(status.ok());
    EXPECT_FALSE(response.success());
    EXPECT_EQ(response.error().code(), ERROR_CODE_HARDWARE_NOT_READY);
    EXPECT_FALSE(response.error().message().empty());
}

/**
 * @test StartExposure generates unique acquisition IDs
 */
TEST_F(CommandServiceTestFixture, StartExposure_GeneratesUniqueAcquisitionIds) {
    // Arrange: Create two valid requests
    StartExposureRequest request1;
    request1.mutable_parameters()->set_kv(120.0f);
    request1.mutable_parameters()->set_mas(100.0f);
    request1.mutable_parameters()->set_detector_id(1);
    request1.mutable_parameters()->set_transfer_mode(IMAGE_TRANSFER_MODE_FULL_QUALITY);

    StartExposureRequest request2;
    request2.mutable_parameters()->set_kv(120.0f);
    request2.mutable_parameters()->set_mas(100.0f);
    request2.mutable_parameters()->set_detector_id(1);
    request2.mutable_parameters()->set_transfer_mode(IMAGE_TRANSFER_MODE_FULL_QUALITY);

    StartExposureResponse response1, response2;
    ServerContext context1, context2;

    // Reset to READY state after first call
    service_->SetSystemState(SystemState::SYSTEM_STATE_READY);

    // Act: Call StartExposure twice
    Status status1 = service_->StartExposure(&context1, &request1, &response1);
    service_->SetSystemState(SystemState::SYSTEM_STATE_READY);
    Status status2 = service_->StartExposure(&context2, &request2, &response2);

    // Assert: Acquisition IDs are different
    EXPECT_TRUE(status1.ok());
    EXPECT_TRUE(status2.ok());
    EXPECT_NE(response1.acquisition_id(), response2.acquisition_id());
}

/**
 * @test AbortExposure cancels in-progress acquisition
 * FR-IPC-04a: Abort in-progress exposure
 */
TEST_F(CommandServiceTestFixture, AbortExposure_ValidAcquisitionId_ReturnsSuccess) {
    // Arrange: Create request with valid acquisition_id
    AbortExposureRequest request;
    request.set_acquisition_id(12345);

    AbortExposureResponse response;
    ServerContext context;

    // Act: Call AbortExposure
    Status status = service_->AbortExposure(&context, &request, &response);

    // Assert: Response indicates success
    EXPECT_TRUE(status.ok());
    EXPECT_TRUE(response.success());
    EXPECT_EQ(response.error().code(), ERROR_CODE_OK);
}

/**
 * @test SetCollimator validates and applies position
 * FR-IPC-04a: Move collimator blades to position
 */
TEST_F(CommandServiceTestFixture, SetCollimator_ValidPosition_AppliesPosition) {
    // Arrange: Create request with valid collimator position
    SetCollimatorRequest request;
    request.mutable_position()->set_left_mm(10.0f);
    request.mutable_position()->set_right_mm(10.0f);
    request.mutable_position()->set_top_mm(10.0f);
    request.mutable_position()->set_bottom_mm(10.0f);

    SetCollimatorResponse response;
    ServerContext context;

    // Act: Call SetCollimator
    Status status = service_->SetCollimator(&context, &request, &response);

    // Assert: Response indicates success
    EXPECT_TRUE(status.ok());
    EXPECT_TRUE(response.success());
    EXPECT_EQ(response.error().code(), ERROR_CODE_OK);

    // Assert: actual_position matches requested position
    EXPECT_FLOAT_EQ(response.actual_position().left_mm(), 10.0f);
    EXPECT_FLOAT_EQ(response.actual_position().right_mm(), 10.0f);
    EXPECT_FLOAT_EQ(response.actual_position().top_mm(), 10.0f);
    EXPECT_FLOAT_EQ(response.actual_position().bottom_mm(), 10.0f);
}

/**
 * @test SetCollimator rejects missing position
 */
TEST_F(CommandServiceTestFixture, SetCollimator_MissingPosition_ReturnsValidationError) {
    // Arrange: Create request without position
    SetCollimatorRequest request;
    SetCollimatorResponse response;
    ServerContext context;

    // Act: Call SetCollimator
    Status status = service_->SetCollimator(&context, &request, &response);

    // Assert: Response indicates validation error
    EXPECT_TRUE(status.ok());
    EXPECT_FALSE(response.success());
    EXPECT_EQ(response.error().code(), ERROR_CODE_INVALID_ARGUMENT);
}

/**
 * @test SetCollimator rejects negative position
 */
TEST_F(CommandServiceTestFixture, SetCollimator_NegativePosition_ReturnsValidationError) {
    // Arrange: Create request with negative position
    SetCollimatorRequest request;
    request.mutable_position()->set_left_mm(-10.0f);  // Negative
    request.mutable_position()->set_right_mm(10.0f);
    request.mutable_position()->set_top_mm(10.0f);
    request.mutable_position()->set_bottom_mm(10.0f);

    SetCollimatorResponse response;
    ServerContext context;

    // Act: Call SetCollimator
    Status status = service_->SetCollimator(&context, &request, &response);

    // Assert: Response indicates validation error
    EXPECT_TRUE(status.ok());
    EXPECT_FALSE(response.success());
    EXPECT_EQ(response.error().code(), ERROR_CODE_INVALID_ARGUMENT);
}

/**
 * @test SetCollimator rejects position exceeding maximum
 */
TEST_F(CommandServiceTestFixture, SetCollimator_PositionExceedsMax_ReturnsValidationError) {
    // Arrange: Create request with position > MAX_COLLIMATOR_OPENING_MM (300.0)
    SetCollimatorRequest request;
    request.mutable_position()->set_left_mm(350.0f);  // Exceeds max
    request.mutable_position()->set_right_mm(10.0f);
    request.mutable_position()->set_top_mm(10.0f);
    request.mutable_position()->set_bottom_mm(10.0f);

    SetCollimatorResponse response;
    ServerContext context;

    // Act: Call SetCollimator
    Status status = service_->SetCollimator(&context, &request, &response);

    // Assert: Response indicates validation error
    EXPECT_TRUE(status.ok());
    EXPECT_FALSE(response.success());
    EXPECT_EQ(response.error().code(), ERROR_CODE_INVALID_ARGUMENT);
}

/**
 * @test RunCalibration accepts DARK_FIELD mode
 * FR-IPC-04a: Execute calibration routine
 */
TEST_F(CommandServiceTestFixture, RunCalibration_DarkFieldMode_ReturnsSuccess) {
    // Arrange: Create request with DARK_FIELD mode
    RunCalibrationRequest request;
    request.set_mode(CALIBRATION_MODE_DARK_FIELD);

    RunCalibrationResponse response;
    ServerContext context;

    // Act: Call RunCalibration
    Status status = service_->RunCalibration(&context, &request, &response);

    // Assert: Response indicates success
    EXPECT_TRUE(status.ok());
    EXPECT_TRUE(response.success());
    EXPECT_EQ(response.error().code(), ERROR_CODE_OK);
}

/**
 * @test RunCalibration accepts FLAT_FIELD mode
 */
TEST_F(CommandServiceTestFixture, RunCalibration_FlatFieldMode_ReturnsSuccess) {
    // Arrange: Create request with FLAT_FIELD mode
    RunCalibrationRequest request;
    request.set_mode(CALIBRATION_MODE_FLAT_FIELD);

    RunCalibrationResponse response;
    ServerContext context;

    // Act: Call RunCalibration
    Status status = service_->RunCalibration(&context, &request, &response);

    // Assert: Response indicates success
    EXPECT_TRUE(status.ok());
    EXPECT_TRUE(response.success());
    EXPECT_EQ(response.error().code(), ERROR_CODE_OK);
}

/**
 * @test RunCalibration accepts GAIN mode
 */
TEST_F(CommandServiceTestFixture, RunCalibration_GainMode_ReturnsSuccess) {
    // Arrange: Create request with GAIN mode
    RunCalibrationRequest request;
    request.set_mode(CALIBRATION_MODE_GAIN);

    RunCalibrationResponse response;
    ServerContext context;

    // Act: Call RunCalibration
    Status status = service_->RunCalibration(&context, &request, &response);

    // Assert: Response indicates success
    EXPECT_TRUE(status.ok());
    EXPECT_TRUE(response.success());
    EXPECT_EQ(response.error().code(), ERROR_CODE_OK);
}

/**
 * @test RunCalibration rejects unspecified mode
 */
TEST_F(CommandServiceTestFixture, RunCalibration_UnspecifiedMode_ReturnsValidationError) {
    // Arrange: Create request with unspecified mode
    RunCalibrationRequest request;
    request.set_mode(CALIBRATION_MODE_UNSPECIFIED);

    RunCalibrationResponse response;
    ServerContext context;

    // Act: Call RunCalibration
    Status status = service_->RunCalibration(&context, &request, &response);

    // Assert: Response indicates validation error
    EXPECT_TRUE(status.ok());
    EXPECT_FALSE(response.success());
    EXPECT_EQ(response.error().code(), ERROR_CODE_INVALID_ARGUMENT);
}

/**
 * @test GetSystemState returns current system state
 * FR-IPC-04a: Return current system state
 */
TEST_F(CommandServiceTestFixture, GetSystemState_ReturnsCurrentState) {
    // Arrange: Set system to READY state
    service_->SetSystemState(SystemState::SYSTEM_STATE_READY);

    GetSystemStateRequest request;
    GetSystemStateResponse response;
    ServerContext context;

    // Act: Call GetSystemState
    Status status = service_->GetSystemState(&context, &request, &response);

    // Assert: Response contains valid SystemState
    EXPECT_TRUE(status.ok());
    EXPECT_EQ(response.state(), SystemState::SYSTEM_STATE_READY);
    EXPECT_GT(response.response_timestamp().microseconds_since_start(), 0u);
}

/**
 * @test GetSystemState returns ACQUIRING state
 */
TEST_F(CommandServiceTestFixture, GetSystemState_AcquiringState_ReturnsAcquiring) {
    // Arrange: Set system to ACQUIRING state
    service_->SetSystemState(SystemState::SYSTEM_STATE_ACQUIRING);

    GetSystemStateRequest request;
    GetSystemStateResponse response;
    ServerContext context;

    // Act: Call GetSystemState
    Status status = service_->GetSystemState(&context, &request, &response);

    // Assert: Response contains ACQUIRING state
    EXPECT_TRUE(status.ok());
    EXPECT_EQ(response.state(), SystemState::SYSTEM_STATE_ACQUIRING);
}

/**
 * @test GetSystemState returns CALIBRATING state
 */
TEST_F(CommandServiceTestFixture, GetSystemState_CalibratingState_ReturnsCalibrating) {
    // Arrange: Set system to CALIBRATING state
    service_->SetSystemState(SystemState::SYSTEM_STATE_CALIBRATING);

    GetSystemStateRequest request;
    GetSystemStateResponse response;
    ServerContext context;

    // Act: Call GetSystemState
    Status status = service_->GetSystemState(&context, &request, &response);

    // Assert: Response contains CALIBRATING state
    EXPECT_TRUE(status.ok());
    EXPECT_EQ(response.state(), SystemState::SYSTEM_STATE_CALIBRATING);
}

/**
 * @test GetSystemState returns FAULT state
 */
TEST_F(CommandServiceTestFixture, GetSystemState_FaultState_ReturnsFault) {
    // Arrange: Set system to FAULT state
    service_->SetSystemState(SystemState::SYSTEM_STATE_FAULT);

    GetSystemStateRequest request;
    GetSystemStateResponse response;
    ServerContext context;

    // Act: Call GetSystemState
    Status status = service_->GetSystemState(&context, &request, &response);

    // Assert: Response contains FAULT state
    EXPECT_TRUE(status.ok());
    EXPECT_EQ(response.state(), SystemState::SYSTEM_STATE_FAULT);
}

} // namespace hnvue::test
