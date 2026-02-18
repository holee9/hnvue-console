/**
 * @file ConfigServiceImpl.h
 * @brief Implementation of ConfigService (configuration synchronization)
 * SPEC-IPC-001 Section 4.2.5: ConfigService with 3 RPCs
 *
 * This service manages configuration parameters between GUI and Core Engine:
 * - GetConfiguration: Read current configuration
 * - SetConfiguration: Update configuration with validation
 * - SubscribeConfigChanges: Stream configuration change notifications
 */

#ifndef HNVE_IPC_CONFIG_SERVICE_IMPL_H
#define HNVE_IPC_CONFIG_SERVICE_IMPL_H

#include <grpcpp/grpcpp.h>
#include <memory>
#include <string>
#include <unordered_map>
#include <mutex>
#include <functional>
#include <spdlog/spdlog.h>

// Generated protobuf headers
#include "hnvue_config.grpc.pb.h"
#include "hnvue_config.pb.h"

namespace hnvue::ipc {

using hnvue::ipc::protobuf::ConfigService;
using hnvue::ipc::protobuf::GetConfigRequest;
using hnvue::ipc::protobuf::GetConfigResponse;
using hnvue::ipc::protobuf::SetConfigRequest;
using hnvue::ipc::protobuf::SetConfigResponse;
using hnvue::ipc::protobuf::ConfigChangeSubscribeRequest;
using hnvue::ipc::protobuf::ConfigChangeEvent;
using hnvue::ipc::protobuf::ConfigValue;
using hnvue::ipc::protobuf::ConfigChangeSource;
using hnvue::ipc::protobuf::IpcError;
using hnvue::ipc::protobuf::ErrorCode;

/**
 * @brief Configuration parameter validator function type
 *
 * Validators are called during SetConfiguration to verify parameter values.
 *
 * @param key Parameter key
 * @param value Parameter value to validate
 * @return true if valid, false otherwise
 */
using ConfigValidator = std::function<bool(const std::string& key, const ConfigValue& value)>;

/**
 * @brief Configuration change callback function type
 *
 * Called when a configuration parameter changes.
 *
 * @param key Parameter key
 * @param old_value Previous value
 * @param new_value New value
 * @param source Change source (GUI, CORE, STARTUP)
 */
using ConfigChangeCallback = std::function<void(
    const std::string& key,
    const ConfigValue& old_value,
    const ConfigValue& new_value,
    ConfigChangeSource source
)>;

/**
 * @class ConfigServiceImpl
 * @brief gRPC service implementation for configuration management
 *
 * Thread safety: Uses mutex protection for configuration storage.
 *
 * SPEC-IPC-001 Section 4.2.5:
 * - GetConfiguration: Read all or specific parameters
 * - SetConfiguration: Write with validation
 * - SubscribeConfigChanges: Server-streaming notifications
 *
 * SPEC-IPC-001 Section 4.3.3:
 * - Initial sync on connect: GUI calls GetConfiguration on connect
 */
class ConfigServiceImpl final : public ConfigService::Service {
public:
    /**
     * @brief Construct ConfigService implementation
     * @param logger Logger instance
     */
    explicit ConfigServiceImpl(std::shared_ptr<spdlog::logger> logger = spdlog::default_logger());

    ~ConfigServiceImpl() override;

    // Non-copyable, non-movable
    ConfigServiceImpl(const ConfigServiceImpl&) = delete;
    ConfigServiceImpl& operator=(const ConfigServiceImpl&) = delete;

    /**
     * @brief Read configuration parameters
     *
     * Returns requested parameters. Empty key list returns all parameters.
     *
     * SPEC-IPC-001 Section 4.2.5:
     * - Empty parameter_keys returns all
     * - Returns map<string, ConfigValue>
     *
     * @param context gRPC server context
     * @param request Request with parameter keys (empty = all)
     * @param response Response with parameters map
     * @return gRPC status code
     */
    grpc::Status GetConfiguration(
        grpc::ServerContext* context,
        const GetConfigRequest* request,
        GetConfigResponse* response) override;

    /**
     * @brief Update configuration parameters
     *
     * Validates and applies configuration changes.
     * Returns list of rejected keys if validation fails.
     *
     * SPEC-IPC-001 Section 4.2.5:
     * - Validates all parameters before applying
     * - Returns rejected_keys for invalid parameters
     * - Triggers change notifications after applying
     *
     * @param context gRPC server context
     * @param request Map of parameters to set
     * @param response Success status, applied parameters, rejected keys
     * @return gRPC status code
     */
    grpc::Status SetConfiguration(
        grpc::ServerContext* context,
        const SetConfigRequest* request,
        SetConfigResponse* response) override;

    /**
     * @brief Subscribe to configuration change notifications
     *
     * Server-streaming RPC that sends events when parameters change.
     *
     * SPEC-IPC-001 Section 4.2.5:
     * - Empty parameter_keys subscribes to all changes
     * - Events contain key, old_value, new_value, source
     *
     * @param context gRPC server context (supports cancellation)
     * @param request Parameter key filter (empty = all)
     * @param writer Server writer for streaming change events
     * @return gRPC status code
     */
    grpc::Status SubscribeConfigChanges(
        grpc::ServerContext* context,
        const ConfigChangeSubscribeRequest* request,
        grpc::ServerWriter<ConfigChangeEvent>* writer) override;

    /**
     * @brief Set a configuration parameter (internal API)
     *
     * Called by Core Engine internally. Triggers change notifications
     * with source = CONFIG_CHANGE_SOURCE_CORE.
     *
     * @param key Parameter key
     * @param value New value
     * @return true if set successfully, false otherwise
     */
    bool SetParameter(const std::string& key, const ConfigValue& value);

    /**
     * @brief Get a configuration parameter (internal API)
     * @param key Parameter key
     * @param value Output value
     * @return true if parameter exists, false otherwise
     */
    bool GetParameter(const std::string& key, ConfigValue& value) const;

    /**
     * @brief Register a parameter validator
     *
     * Validators are called during SetConfiguration to verify values.
     *
     * @param key Parameter key (or "*" for wildcard)
     * @param validator Validator function
     */
    void RegisterValidator(const std::string& key, ConfigValidator validator);

    /**
     * @brief Register a change callback
     *
     * Callbacks are invoked when parameters change.
     *
     * @param callback Callback function
     */
    void RegisterChangeCallback(ConfigChangeCallback callback);

    /**
     * @brief Load default configuration values
     *
     * Called during server startup to populate initial config.
     * Sets source = CONFIG_CHANGE_SOURCE_STARTUP.
     */
    void LoadDefaults();

private:
    std::shared_ptr<spdlog::logger> logger_;

    // Configuration storage (thread-safe)
    mutable std::mutex config_mutex_;
    std::unordered_map<std::string, ConfigValue> config_values_;

    // Parameter validators
    std::unordered_map<std::string, ConfigValidator> validators_;

    // Change callbacks
    std::vector<ConfigChangeCallback> change_callbacks_;

    /**
     * @brief Validate a configuration parameter
     * @param key Parameter key
     * @param value Value to validate
     * @return true if valid, false otherwise
     */
    bool ValidateParameter(const std::string& key, const ConfigValue& value) const;

    /**
     * @brief Apply configuration changes
     *
     * Applies valid parameters and triggers change notifications.
     *
     * @param parameters Parameters to apply
     * @param applied_parameters Output: successfully applied parameters
     * @param rejected_keys Output: keys that failed validation
     * @param source Change source
     */
    void ApplyChanges(
        const google::protobuf::Map<std::string, ConfigValue>& parameters,
        google::protobuf::Map<std::string, ConfigValue>* applied_parameters,
        std::vector<std::string>* rejected_keys,
        ConfigChangeSource source);

    /**
     * @brief Trigger change callbacks
     * @param key Parameter key
     * @param old_value Previous value
     * @param new_value New value
     * @param source Change source
     */
    void TriggerChangeCallbacks(
        const std::string& key,
        const ConfigValue& old_value,
        const ConfigValue& new_value,
        ConfigChangeSource source);

    /**
     * @brief Setup default validators
     *
     * Registers validators for common parameter types.
     */
    void SetupDefaultValidators();

    /**
     * @brief Load default configuration values
     *
     * Sets reasonable defaults for known parameters.
     */
    void LoadDefaultValues();
};

} // namespace hnvue::ipc

#endif // HNVE_IPC_CONFIG_SERVICE_IMPL_H
