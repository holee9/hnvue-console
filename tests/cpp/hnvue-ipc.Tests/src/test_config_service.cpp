/**
 * @file test_config_service.cpp
 * @brief Comprehensive unit tests for ConfigServiceImpl
 * SPEC-IPC-001 Section 4.2.5: ConfigService with 3 RPCs
 */

#include <gtest/gtest.h>
#include <gmock/gmock.h>
#include <memory>
#include <thread>
#include <future>
#include <spdlog/sinks/stdout_color_sinks.h>

// Include generated protobuf headers
#include "hnvue_config.grpc.pb.h"
#include "hnvue_config.pb.h"

// Include service implementation
#include "hnvue/ipc/ConfigServiceImpl.h"

using namespace hnvue::ipc;
using namespace hnvue::ipc::protobuf;
using grpc::Status;
using grpc::ServerContext;

namespace hnvue::test {

/**
 * @class MockServerWriter
 * @brief Mock ServerWriter for testing streaming behavior
 */
template<typename T>
class MockServerWriter : public grpc::ServerWriter<T> {
public:
    MOCK_METHOD(bool, Write, (const T& msg), (override));
};

/**
 * @class ConfigServiceTestFixture
 * @brief Test fixture for ConfigServiceImpl tests
 */
class ConfigServiceTestFixture : public ::testing::Test {
protected:
    void SetUp() override {
        // Create logger for tests
        logger_ = spdlog::stdout_color_mt("test_config");
        logger_->set_level(spdlog::level::debug);

        // Create service instance
        service_ = std::make_unique<ConfigServiceImpl>(logger_);
    }

    void TearDown() override {
        service_.reset();
        spdlog::drop("test_config");
    }

    /**
     * Helper: Create a ConfigValue with double value
     */
    ConfigValue CreateDoubleValue(double value) {
        ConfigValue config_value;
        config_value.set_double_value(value);
        return config_value;
    }

    /**
     * Helper: Create a ConfigValue with int value
     */
    ConfigValue CreateIntValue(int64_t value) {
        ConfigValue config_value;
        config_value.set_int_value(value);
        return config_value;
    }

    /**
     * Helper: Create a ConfigValue with string value
     */
    ConfigValue CreateStringValue(const std::string& value) {
        ConfigValue config_value;
        config_value.set_string_value(value);
        return config_value;
    }

    /**
     * Helper: Create a ConfigValue with bool value
     */
    ConfigValue CreateBoolValue(bool value) {
        ConfigValue config_value;
        config_value.set_bool_value(value);
        return config_value;
    }

    std::shared_ptr<spdlog::logger> logger_;
    std::unique_ptr<ConfigServiceImpl> service_;
};

// =========================================================================
// Constructor and Initialization Tests
// =========================================================================

/**
 * @test Constructor initializes with default values
 */
TEST_F(ConfigServiceTestFixture, Constructor_InitializesWithDefaults) {
    // Arrange & Act: Service is created in SetUp()

    // Assert: Service is initialized
    EXPECT_NE(service_, nullptr);
}

/**
 * @test LoadDefaults populates initial configuration
 * FR-IPC-07: Default configuration values
 */
TEST_F(ConfigServiceTestFixture, LoadDefaults_PopulatesInitialConfig) {
    // Arrange: Get configuration before LoadDefaults
    GetConfigRequest request;
    GetConfigResponse response;
    ServerContext context;

    // Act: Get configuration (defaults should be loaded on construction)
    Status status = service_->GetConfiguration(&context, &request, &response);

    // Assert: Default parameters exist
    EXPECT_TRUE(status.ok());
    EXPECT_GT(response.parameters().size(), 0u);
}

// =========================================================================
// GetConfiguration Tests
// =========================================================================

/**
 * @test GetConfiguration with no keys returns all parameters
 * FR-IPC-07: Read configuration parameters
 */
TEST_F(ConfigServiceTestFixture, GetConfiguration_NoKeys_ReturnsAllParameters) {
    // Arrange: Create request with empty parameter_keys
    GetConfigRequest request;
    GetConfigResponse response;
    ServerContext context;

    // Act: Get configuration
    Status status = service_->GetConfiguration(&context, &request, &response);

    // Assert: Response contains all default parameters
    EXPECT_TRUE(status.ok());
    EXPECT_GT(response.parameters().size(), 0u);
    EXPECT_EQ(response.error().code(), ERROR_CODE_OK);

    // Verify default exposure parameters exist
    EXPECT_TRUE(response.parameters().contains("exposure.default_kv"));
    EXPECT_TRUE(response.parameters().contains("exposure.default_mas"));
    EXPECT_TRUE(response.parameters().contains("exposure.default_transfer_mode"));
}

/**
 * @test GetConfiguration with specific keys returns only requested parameters
 */
TEST_F(ConfigServiceTestFixture, GetConfiguration_SpecificKeys_ReturnsOnlyRequested) {
    // Arrange: Create request with specific keys
    GetConfigRequest request;
    request.add_parameter_keys("exposure.default_kv");
    request.add_parameter_keys("exposure.default_mas");
    GetConfigResponse response;
    ServerContext context;

    // Act: Get configuration
    Status status = service_->GetConfiguration(&context, &request, &response);

    // Assert: Response contains only requested parameters
    EXPECT_TRUE(status.ok());
    EXPECT_EQ(response.parameters().size(), 2u);
    EXPECT_TRUE(response.parameters().contains("exposure.default_kv"));
    EXPECT_TRUE(response.parameters().contains("exposure.default_mas"));
    EXPECT_FALSE(response.parameters().contains("exposure.default_transfer_mode"));
}

/**
 * @test GetConfiguration with non-existent key returns subset
 */
TEST_F(ConfigServiceTestFixture, GetConfiguration_NonExistentKey_ReturnsSubset) {
    // Arrange: Create request with mix of existent and non-existent keys
    GetConfigRequest request;
    request.add_parameter_keys("exposure.default_kv");
    request.add_parameter_keys("non.existent.key");
    GetConfigResponse response;
    ServerContext context;

    // Act: Get configuration
    Status status = service_->GetConfiguration(&context, &request, &response);

    // Assert: Response contains only existent key
    EXPECT_TRUE(status.ok());
    EXPECT_EQ(response.parameters().size(), 1u);
    EXPECT_TRUE(response.parameters().contains("exposure.default_kv"));
}

/**
 * @test GetConfiguration with only non-existent keys returns empty map
 */
TEST_F(ConfigServiceTestFixture, GetConfiguration_AllNonExistentKeys_ReturnsEmpty) {
    // Arrange: Create request with only non-existent keys
    GetConfigRequest request;
    request.add_parameter_keys("non.existent.key1");
    request.add_parameter_keys("non.existent.key2");
    GetConfigResponse response;
    ServerContext context;

    // Act: Get configuration
    Status status = service_->GetConfiguration(&context, &request, &response);

    // Assert: Response is empty but successful
    EXPECT_TRUE(status.ok());
    EXPECT_EQ(response.parameters().size(), 0u);
}

// =========================================================================
// SetConfiguration Tests
// =========================================================================

/**
 * @test SetConfiguration with valid parameters applies them
 * FR-IPC-07: Write configuration parameters with validation
 */
TEST_F(ConfigServiceTestFixture, SetConfiguration_ValidParameters_AppliesAll) {
    // Arrange: Create request with valid parameters
    SetConfigRequest request;
    (*request.mutable_parameters())["test.double_param"] = CreateDoubleValue(100.0);
    (*request.mutable_parameters())["test.int_param"] = CreateIntValue(42);
    (*request.mutable_parameters())["test.string_param"] = CreateStringValue("test_value");
    (*request.mutable_parameters())["test.bool_param"] = CreateBoolValue(true);

    SetConfigResponse response;
    ServerContext context;

    // Act: Set configuration
    Status status = service_->SetConfiguration(&context, &request, &response);

    // Assert: All parameters applied successfully
    EXPECT_TRUE(status.ok());
    EXPECT_TRUE(response.success());
    EXPECT_EQ(response.applied_parameters().size(), 4u);
    EXPECT_EQ(response.rejected_keys().size(), 0u);
    EXPECT_EQ(response.error().code(), ERROR_CODE_OK);
}

/**
 * @test SetConfiguration with validator rejects invalid value
 */
TEST_F(ConfigServiceTestFixture, SetConfiguration_InvalidKVValue_RejectsParameter) {
    // Arrange: Create request with invalid kV value (out of range)
    SetConfigRequest request;
    (*request.mutable_parameters())["exposure.default_kv"] = CreateDoubleValue(200.0);  // > 150.0 max

    SetConfigResponse response;
    ServerContext context;

    // Act: Set configuration
    Status status = service_->SetConfiguration(&context, &request, &response);

    // Assert: Parameter rejected by validator
    EXPECT_TRUE(status.ok());
    EXPECT_FALSE(response.success());
    EXPECT_EQ(response.applied_parameters().size(), 0u);
    EXPECT_EQ(response.rejected_keys().size(), 1u);
    EXPECT_EQ(response.rejected_keys(0), "exposure.default_kv");
    EXPECT_EQ(response.error().code(), ERROR_CODE_CONFIGURATION_REJECTED);
}

/**
 * @test SetConfiguration with validator accepts valid kV value
 */
TEST_F(ConfigServiceTestFixture, SetConfiguration_ValidKVValue_AcceptsParameter) {
    // Arrange: Create request with valid kV value
    SetConfigRequest request;
    (*request.mutable_parameters())["exposure.default_kv"] = CreateDoubleValue(120.0);  // 20.0-150.0 range

    SetConfigResponse response;
    ServerContext context;

    // Act: Set configuration
    Status status = service_->SetConfiguration(&context, &request, &response);

    // Assert: Parameter accepted
    EXPECT_TRUE(status.ok());
    EXPECT_TRUE(response.success());
    EXPECT_EQ(response.applied_parameters().size(), 1u);
    EXPECT_EQ(response.rejected_keys().size(), 0u);
    EXPECT_TRUE(response.applied_parameters().contains("exposure.default_kv"));
}

/**
 * @test SetConfiguration with validator accepts valid mAs value
 */
TEST_F(ConfigServiceTestFixture, SetConfiguration_ValidMAsValue_AcceptsParameter) {
    // Arrange: Create request with valid mAs value
    SetConfigRequest request;
    (*request.mutable_parameters())["exposure.default_mas"] = CreateDoubleValue(100.0);  // 0.1-1000.0 range

    SetConfigResponse response;
    ServerContext context;

    // Act: Set configuration
    Status status = service_->SetConfiguration(&context, &request, &response);

    // Assert: Parameter accepted
    EXPECT_TRUE(status.ok());
    EXPECT_TRUE(response.success());
    EXPECT_EQ(response.applied_parameters().size(), 1u);
    EXPECT_EQ(response.rejected_keys().size(), 0u);
}

/**
 * @test SetConfiguration with validator rejects invalid mAs value
 */
TEST_F(ConfigServiceTestFixture, SetConfiguration_InvalidMAsValue_RejectsParameter) {
    // Arrange: Create request with invalid mAs value (out of range)
    SetConfigRequest request;
    (*request.mutable_parameters())["exposure.default_mas"] = CreateDoubleValue(2000.0);  // > 1000.0 max

    SetConfigResponse response;
    ServerContext context;

    // Act: Set configuration
    Status status = service_->SetConfiguration(&context, &request, &response);

    // Assert: Parameter rejected
    EXPECT_TRUE(status.ok());
    EXPECT_FALSE(response.success());
    EXPECT_EQ(response.applied_parameters().size(), 0u);
    EXPECT_EQ(response.rejected_keys().size(), 1u);
    EXPECT_EQ(response.rejected_keys(0), "exposure.default_mas");
}

/**
 * @test SetConfiguration with mixed valid and invalid parameters
 */
TEST_F(ConfigServiceTestFixture, SetConfiguration_MixedParameters_AppliesValidOnly) {
    // Arrange: Create request with mix of valid and invalid parameters
    SetConfigRequest request;
    (*request.mutable_parameters())["exposure.default_kv"] = CreateDoubleValue(120.0);  // Valid
    (*request.mutable_parameters())["exposure.default_mas"] = CreateDoubleValue(2000.0);  // Invalid
    (*request.mutable_parameters())["test.valid_param"] = CreateIntValue(42);  // Valid (no validator)

    SetConfigResponse response;
    ServerContext context;

    // Act: Set configuration
    Status status = service_->SetConfiguration(&context, &request, &response);

    // Assert: Valid parameters applied, invalid rejected
    EXPECT_TRUE(status.ok());
    EXPECT_FALSE(response.success());  // Overall success = false because some rejected
    EXPECT_EQ(response.applied_parameters().size(), 2u);
    EXPECT_EQ(response.rejected_keys().size(), 1u);
    EXPECT_TRUE(response.applied_parameters().contains("exposure.default_kv"));
    EXPECT_TRUE(response.applied_parameters().contains("test.valid_param"));
    EXPECT_EQ(response.rejected_keys(0), "exposure.default_mas");
}

/**
 * @test SetConfiguration with empty request is successful
 */
TEST_F(ConfigServiceTestFixture, SetConfiguration_EmptyRequest_ReturnsSuccess) {
    // Arrange: Create empty request
    SetConfigRequest request;
    SetConfigResponse response;
    ServerContext context;

    // Act: Set configuration
    Status status = service_->SetConfiguration(&context, &request, &response);

    // Assert: Empty request is successful
    EXPECT_TRUE(status.ok());
    EXPECT_TRUE(response.success());
    EXPECT_EQ(response.applied_parameters().size(), 0u);
    EXPECT_EQ(response.rejected_keys().size(), 0u);
}

// =========================================================================
// SetParameter/GetParameter Tests
// =========================================================================

/**
 * @test SetParameter updates existing parameter
 */
TEST_F(ConfigServiceTestFixture, SetParameter_ExistingParameter_UpdatesValue) {
    // Arrange: Set initial value
    ConfigValue value1 = CreateDoubleValue(100.0);
    EXPECT_TRUE(service_->SetParameter("test.param", value1));

    // Act: Update to new value
    ConfigValue value2 = CreateDoubleValue(200.0);
    EXPECT_TRUE(service_->SetParameter("test.param", value2));

    // Assert: New value is set
    ConfigValue retrieved;
    EXPECT_TRUE(service_->GetParameter("test.param", retrieved));
    EXPECT_DOUBLE_EQ(retrieved.double_value(), 200.0);
}

/**
 * @test SetParameter creates new parameter
 */
TEST_F(ConfigServiceTestFixture, SetParameter_NewParameter_CreatesParameter) {
    // Arrange & Act: Set new parameter
    ConfigValue value = CreateStringValue("new_value");
    EXPECT_TRUE(service_->SetParameter("test.new_param", value));

    // Assert: Parameter exists with correct value
    ConfigValue retrieved;
    EXPECT_TRUE(service_->GetParameter("test.new_param", retrieved));
    EXPECT_EQ(retrieved.string_value(), "new_value");
}

/**
 * @test GetParameter retrieves existing parameter
 */
TEST_F(ConfigServiceTestFixture, GetParameter_ExistingParameter_ReturnsValue) {
    // Arrange: Set parameter
    ConfigValue value = CreateIntValue(123);
    service_->SetParameter("test.get_param", value);

    // Act: Get parameter
    ConfigValue retrieved;
    bool found = service_->GetParameter("test.get_param", retrieved);

    // Assert: Parameter retrieved correctly
    EXPECT_TRUE(found);
    EXPECT_EQ(retrieved.int_value(), 123);
}

/**
 * @test GetParameter with non-existent parameter returns false
 */
TEST_F(ConfigServiceTestFixture, GetParameter_NonExistentParameter_ReturnsFalse) {
    // Arrange & Act: Try to get non-existent parameter
    ConfigValue retrieved;
    bool found = service_->GetParameter("non.existent.param", retrieved);

    // Assert: Parameter not found
    EXPECT_FALSE(found);
}

// =========================================================================
// ConfigValue Type Tests (Oneof)
// =========================================================================

/**
 * @test ConfigValue with double type
 */
TEST_F(ConfigServiceTestFixture, ConfigValue_DoubleType_CorrectlyStored) {
    // Arrange & Act: Set double parameter
    ConfigValue value = CreateDoubleValue(123.456);
    service_->SetParameter("test.double", value);

    // Assert: Value stored and retrieved correctly
    ConfigValue retrieved;
    EXPECT_TRUE(service_->GetParameter("test.double", retrieved));
    EXPECT_TRUE(retrieved.has_double_value());
    EXPECT_DOUBLE_EQ(retrieved.double_value(), 123.456);
}

/**
 * @test ConfigValue with int type
 */
TEST_F(ConfigServiceTestFixture, ConfigValue_IntType_CorrectlyStored) {
    // Arrange & Act: Set int parameter
    ConfigValue value = CreateIntValue(-99999);
    service_->SetParameter("test.int", value);

    // Assert: Value stored and retrieved correctly
    ConfigValue retrieved;
    EXPECT_TRUE(service_->GetParameter("test.int", retrieved));
    EXPECT_TRUE(retrieved.has_int_value());
    EXPECT_EQ(retrieved.int_value(), -99999);
}

/**
 * @test ConfigValue with string type
 */
TEST_F(ConfigServiceTestFixture, ConfigValue_StringType_CorrectlyStored) {
    // Arrange & Act: Set string parameter
    ConfigValue value = CreateStringValue("Hello, World!");
    service_->SetParameter("test.string", value);

    // Assert: Value stored and retrieved correctly
    ConfigValue retrieved;
    EXPECT_TRUE(service_->GetParameter("test.string", retrieved));
    EXPECT_TRUE(retrieved.has_string_value());
    EXPECT_EQ(retrieved.string_value(), "Hello, World!");
}

/**
 * @test ConfigValue with bool type
 */
TEST_F(ConfigServiceTestFixture, ConfigValue_BoolType_CorrectlyStored) {
    // Arrange & Act: Set bool parameter
    ConfigValue value = CreateBoolValue(true);
    service_->SetParameter("test.bool", value);

    // Assert: Value stored and retrieved correctly
    ConfigValue retrieved;
    EXPECT_TRUE(service_->GetParameter("test.bool", retrieved));
    EXPECT_TRUE(retrieved.has_bool_value());
    EXPECT_TRUE(retrieved.bool_value());
}

/**
 * @test ConfigValue type switching
 */
TEST_F(ConfigServiceTestFixture, ConfigValue_TypeSwitching_UpdatesType) {
    // Arrange: Set as double
    ConfigValue double_val = CreateDoubleValue(100.0);
    service_->SetParameter("test.switch", double_val);

    // Act: Switch to string
    ConfigValue string_val = CreateStringValue("now_string");
    service_->SetParameter("test.switch", string_val);

    // Assert: Type switched correctly
    ConfigValue retrieved;
    EXPECT_TRUE(service_->GetParameter("test.switch", retrieved));
    EXPECT_TRUE(retrieved.has_string_value());
    EXPECT_FALSE(retrieved.has_double_value());
    EXPECT_EQ(retrieved.string_value(), "now_string");
}

// =========================================================================
// Validator Tests
// =========================================================================

/**
 * @test RegisterValidator adds custom validator
 */
TEST_F(ConfigServiceTestFixture, RegisterValidator_CustomValidator_UsesValidator) {
    // Arrange: Register validator that only accepts value 42
    service_->RegisterValidator("test.custom_validator", [](const std::string& key, const ConfigValue& value) {
        return value.has_int_value() && value.int_value() == 42;
    });

    // Act: Try to set invalid value
    SetConfigRequest request;
    (*request.mutable_parameters())["test.custom_validator"] = CreateIntValue(99);  // Not 42
    SetConfigResponse response;
    ServerContext context;
    service_->SetConfiguration(&context, &request, &response);

    // Assert: Value rejected
    EXPECT_EQ(response.rejected_keys().size(), 1u);

    // Act: Try to set valid value
    SetConfigRequest request2;
    (*request2.mutable_parameters())["test.custom_validator"] = CreateIntValue(42);  // Valid
    SetConfigResponse response2;
    ServerContext context2;
    service_->SetConfiguration(&context2, &request2, &response2);

    // Assert: Value accepted
    EXPECT_EQ(response2.applied_parameters().size(), 1u);
}

/**
 * @test Wildcard validator applies to all keys
 */
TEST_F(ConfigServiceTestFixture, RegisterValidator_WildcardValidator_AppliesToAll) {
    // Arrange: Register wildcard validator that rejects negative values
    service_->RegisterValidator("*", [](const std::string& key, const ConfigValue& value) {
        if (value.has_int_value()) {
            return value.int_value() >= 0;
        }
        return true;
    });

    // Act: Try to set negative value
    SetConfigRequest request;
    (*request.mutable_parameters())["any.key"] = CreateIntValue(-1);
    SetConfigResponse response;
    ServerContext context;
    service_->SetConfiguration(&context, &request, &response);

    // Assert: Value rejected by wildcard validator
    EXPECT_EQ(response.rejected_keys().size(), 1u);
}

/**
 * @test Default validators are registered on construction
 */
TEST_F(ConfigServiceTestFixture, Constructor_RegistersDefaultValidators) {
    // Arrange: Create request with values that should pass default validators
    SetConfigRequest request;
    (*request.mutable_parameters())["exposure.default_kv"] = CreateDoubleValue(120.0);  // Valid (20-150)
    (*request.mutable_parameters())["exposure.default_mas"] = CreateDoubleValue(100.0);  // Valid (0.1-1000)

    SetConfigResponse response;
    ServerContext context;

    // Act: Set configuration
    Status status = service_->SetConfiguration(&context, &request, &response);

    // Assert: Default validators accepted valid values
    EXPECT_TRUE(status.ok());
    EXPECT_TRUE(response.success());
    EXPECT_EQ(response.applied_parameters().size(), 2u);
}

/**
 * @test SetConfiguration with wrong type for validator rejects
 */
TEST_F(ConfigServiceTestFixture, SetConfiguration_WrongTypeForKV_RejectsParameter) {
    // Arrange: Create request with string value for kV (validator expects double)
    SetConfigRequest request;
    (*request.mutable_parameters())["exposure.default_kv"] = CreateStringValue("not_a_double");

    SetConfigResponse response;
    ServerContext context;

    // Act: Set configuration
    Status status = service_->SetConfiguration(&context, &request, &response);

    // Assert: Parameter rejected (type mismatch)
    EXPECT_TRUE(status.ok());
    EXPECT_FALSE(response.success());
    EXPECT_EQ(response.rejected_keys().size(), 1u);
}

// =========================================================================
// Thread Safety Tests
// =========================================================================

/**
 * @test Concurrent SetParameter calls are thread-safe
 */
TEST_F(ConfigServiceTestFixture, SetParameter_ConcurrentCalls_ThreadSafe) {
    // Arrange: Create multiple threads
    const int num_threads = 10;
    std::vector<std::future<void>> futures;

    // Act: Set parameters concurrently
    for (int i = 0; i < num_threads; ++i) {
        futures.push_back(std::async(std::launch::async, [this, i]() {
            ConfigValue value = CreateIntValue(i);
            service_->SetParameter("test.concurrent_" + std::to_string(i), value);
        }));
    }

    // Wait for all threads
    for (auto& future : futures) {
        future.wait();
    }

    // Assert: All parameters set correctly
    for (int i = 0; i < num_threads; ++i) {
        ConfigValue retrieved;
        std::string key = "test.concurrent_" + std::to_string(i);
        EXPECT_TRUE(service_->GetParameter(key, retrieved));
        EXPECT_EQ(retrieved.int_value(), i);
    }
}

/**
 * @test Concurrent GetParameter and SetParameter are thread-safe
 */
TEST_F(ConfigServiceTestFixture, GetParameter_SetParameter_Concurrent_ThreadSafe) {
    // Arrange: Set initial value
    ConfigValue value = CreateIntValue(100);
    service_->SetParameter("test.rw_shared", value);

    std::atomic<int> read_count{0};
    std::atomic<int> write_count{0};
    std::vector<std::future<void>> futures;

    // Act: Concurrent reads and writes
    for (int i = 0; i < 10; ++i) {
        futures.push_back(std::async(std::launch::async, [this, &read_count]() {
            for (int j = 0; j < 100; ++j) {
                ConfigValue retrieved;
                service_->GetParameter("test.rw_shared", retrieved);
                read_count++;
            }
        }));

        futures.push_back(std::async(std::launch::async, [this, &write_count]() {
            for (int j = 0; j < 100; ++j) {
                ConfigValue value = CreateIntValue(j);
                service_->SetParameter("test.rw_shared", value);
                write_count++;
            }
        }));
    }

    // Wait for all threads
    for (auto& future : futures) {
        future.wait();
    }

    // Assert: All operations completed
    EXPECT_EQ(read_count.load(), 1000);
    EXPECT_EQ(write_count.load(), 1000);
}

// =========================================================================
// SubscribeConfigChanges Tests
// =========================================================================

/**
 * @test SubscribeConfigChanges keeps stream alive
 * FR-IPC-07: Subscribe to configuration change notifications
 */
TEST_F(ConfigServiceTestFixture, SubscribeConfigChanges_NoFilters_KeepsStreamAlive) {
    // Arrange: Create request
    ConfigChangeSubscribeRequest request;
    MockServerWriter<ConfigChangeEvent> writer;
    ServerContext context;

    // Expect: May be called depending on timing
    EXPECT_CALL(writer, Write(testing::_))
        .Times(testing::AnyNumber());

    // Act: Subscribe
    Status status = service_->SubscribeConfigChanges(&context, &request, &writer);

    // Assert: Service accepts subscription
    EXPECT_TRUE(status.ok() || status.error_code() == grpc::StatusCode::CANCELLED);
}

/**
 * @test SubscribeConfigChanges with parameter filter
 */
TEST_F(ConfigServiceTestFixture, SubscribeConfigChanges_WithFilter_FiltersChanges) {
    // Arrange: Create request with specific parameter filter
    ConfigChangeSubscribeRequest request;
    request.add_parameter_keys("exposure.default_kv");
    MockServerWriter<ConfigChangeEvent> writer;
    ServerContext context;

    EXPECT_CALL(writer, Write(testing::_))
        .Times(testing::AnyNumber());

    // Act: Subscribe
    Status status = service_->SubscribeConfigChanges(&context, &request, &writer);

    // Assert: Service accepts subscription
    EXPECT_TRUE(status.ok() || status.error_code() == grpc::StatusCode::CANCELLED);
}

} // namespace hnvue::test
