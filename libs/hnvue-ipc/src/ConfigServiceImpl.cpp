/**
 * @file ConfigServiceImpl.cpp
 * @brief Implementation of ConfigService (configuration synchronization)
 * SPEC-IPC-001 Section 4.2.5: ConfigService with 3 RPCs
 */

#include "hnvue/ipc/ConfigServiceImpl.h"
#include <thread>
#include <chrono>

namespace hnvue::ipc {

ConfigServiceImpl::ConfigServiceImpl(std::shared_ptr<spdlog::logger> logger)
    : logger_(logger) {
    SetupDefaultValidators();
    LoadDefaults();
    logger_->info("ConfigServiceImpl initialized");
}

ConfigServiceImpl::~ConfigServiceImpl() = default;

grpc::Status ConfigServiceImpl::GetConfiguration(
    grpc::ServerContext* context,
    const GetConfigRequest* request,
    GetConfigResponse* response) {

    const auto& requested_keys = request->parameter_keys();
    logger_->debug("GetConfiguration: requested_keys={}", requested_keys.size());

    std::lock_guard<std::mutex> lock(config_mutex_);

    if (requested_keys.empty()) {
        // Return all parameters
        for (const auto& [key, value] : config_values_) {
            (*response->mutable_parameters())[key] = value;
        }
        logger_->debug("GetConfiguration: returned all {} parameters",
                      config_values_.size());
    } else {
        // Return only requested parameters
        for (const auto& key : requested_keys) {
            auto it = config_values_.find(key);
            if (it != config_values_.end()) {
                (*response->mutable_parameters())[key] = it->second;
            }
        }
        logger_->debug("GetConfiguration: returned {} parameters",
                      response->parameters().size());
    }

    response->mutable_error()->set_code(ErrorCode::ERROR_CODE_OK);
    response->mutable_response_timestamp()->set_microseconds_since_start(0);

    return grpc::Status::OK;
}

grpc::Status ConfigServiceImpl::SetConfiguration(
    grpc::ServerContext* context,
    const SetConfigRequest* request,
    SetConfigResponse* response) {

    const auto& parameters = request->parameters();
    logger_->info("SetConfiguration: {} parameters", parameters.size());

    std::lock_guard<std::mutex> lock(config_mutex_);

    google::protobuf::Map<std::string, ConfigValue> applied_parameters;
    std::vector<std::string> rejected_keys;

    // Validate and apply each parameter
    for (const auto& [key, value] : parameters) {
        if (ValidateParameter(key, value)) {
            // Store old value for callback
            ConfigValue old_value;
            bool had_old_value = false;
            auto it = config_values_.find(key);
            if (it != config_values_.end()) {
                old_value = it->second;
                had_old_value = true;
            }

            // Apply new value
            config_values_[key] = value;
            (*response->mutable_applied_parameters())[key] = value;

            // Trigger change callbacks
            if (had_old_value) {
                TriggerChangeCallbacks(key, old_value, value,
                                      ConfigChangeSource::CONFIG_CHANGE_SOURCE_GUI);
            }

            logger_->debug("SetConfiguration: {} applied", key);
        } else {
            rejected_keys.push_back(key);
            logger_->warn("SetConfiguration: {} rejected (validation failed)", key);
        }
    }

    // Populate response
    response->set_success(rejected_keys.empty());
    for (const auto& key : rejected_keys) {
        response->add_rejected_keys(key);
    }

    if (rejected_keys.empty()) {
        response->mutable_error()->set_code(ErrorCode::ERROR_CODE_OK);
    } else {
        response->mutable_error()->set_code(ErrorCode::ERROR_CODE_CONFIGURATION_REJECTED);
        response->mutable_error()->set_message("Some parameters failed validation");
    }

    response->mutable_response_timestamp()->set_microseconds_since_start(0);

    return grpc::Status::OK;
}

grpc::Status ConfigServiceImpl::SubscribeConfigChanges(
    grpc::ServerContext* context,
    const ConfigChangeSubscribeRequest* request,
    grpc::ServerWriter<ConfigChangeEvent>* writer) {

    const auto& filters = request->parameter_keys();
    logger_->info("SubscribeConfigChanges: filters_count={}", filters.size());

    // TODO: Implement streaming of configuration changes
    // For now, this is a placeholder that keeps the stream alive

    while (!context->IsCancelled()) {
        std::this_thread::sleep_for(std::chrono::milliseconds(100));
    }

    logger_->info("SubscribeConfigChanges: ending stream");
    return grpc::Status::OK;
}

bool ConfigServiceImpl::SetParameter(const std::string& key, const ConfigValue& value) {
    std::lock_guard<std::mutex> lock(config_mutex_);

    ConfigValue old_value;
    bool had_old_value = false;
    auto it = config_values_.find(key);
    if (it != config_values_.end()) {
        old_value = it->second;
        had_old_value = true;
    }

    config_values_[key] = value;

    if (had_old_value) {
        TriggerChangeCallbacks(key, old_value, value,
                              ConfigChangeSource::CONFIG_CHANGE_SOURCE_CORE);
    }

    logger_->debug("SetParameter: {} (internal)", key);
    return true;
}

bool ConfigServiceImpl::GetParameter(const std::string& key, ConfigValue& value) const {
    std::lock_guard<std::mutex> lock(config_mutex_);

    auto it = config_values_.find(key);
    if (it != config_values_.end()) {
        value = it->second;
        return true;
    }
    return false;
}

void ConfigServiceImpl::RegisterValidator(const std::string& key, ConfigValidator validator) {
    validators_[key] = std::move(validator);
    logger_->debug("Registered validator for key: {}", key);
}

void ConfigServiceImpl::RegisterChangeCallback(ConfigChangeCallback callback) {
    change_callbacks_.push_back(std::move(callback));
    logger_->debug("Registered change callback (total: {})", change_callbacks_.size());
}

void ConfigServiceImpl::LoadDefaults() {
    LoadDefaultValues();
    logger_->info("Loaded default configuration values");
}

bool ConfigServiceImpl::ValidateParameter(const std::string& key, const ConfigValue& value) const {
    // Check for specific validator
    auto it = validators_.find(key);
    if (it != validators_.end()) {
        return it->second(key, value);
    }

    // Check for wildcard validator
    auto wildcard_it = validators_.find("*");
    if (wildcard_it != validators_.end()) {
        return wildcard_it->second(key, value);
    }

    // No validator: accept all values
    return true;
}

void ConfigServiceImpl::ApplyChanges(
    const google::protobuf::Map<std::string, ConfigValue>& parameters,
    google::protobuf::Map<std::string, ConfigValue>* applied_parameters,
    std::vector<std::string>* rejected_keys,
    ConfigChangeSource source) {

    for (const auto& [key, value] : parameters) {
        if (ValidateParameter(key, value)) {
            ConfigValue old_value;
            bool had_old_value = false;
            auto it = config_values_.find(key);
            if (it != config_values_.end()) {
                old_value = it->second;
                had_old_value = true;
            }

            config_values_[key] = value;
            (*applied_parameters)[key] = value;

            if (had_old_value) {
                TriggerChangeCallbacks(key, old_value, value, source);
            }
        } else {
            rejected_keys->push_back(key);
        }
    }
}

void ConfigServiceImpl::TriggerChangeCallbacks(
    const std::string& key,
    const ConfigValue& old_value,
    const ConfigValue& new_value,
    ConfigChangeSource source) {

    for (const auto& callback : change_callbacks_) {
        callback(key, old_value, new_value, source);
    }
}

void ConfigServiceImpl::SetupDefaultValidators() {
    // Register validator for exposure kV range
    validators_["exposure.default_kv"] = [](const std::string& key, const ConfigValue& value) {
        if (!value.has_double_value()) return false;
        float kv = static_cast<float>(value.double_value());
        return kv >= 20.0f && kv <= 150.0f;
    };

    // Register validator for exposure mAs range
    validators_["exposure.default_mas"] = [](const std::string& key, const ConfigValue& value) {
        if (!value.has_double_value()) return false;
        float mas = static_cast<float>(value.double_value());
        return mas >= 0.1f && mas <= 1000.0f;
    };

    logger_->debug("Default validators registered");
}

void ConfigServiceImpl::LoadDefaultValues() {
    // Exposure defaults
    ConfigValue kv;
    kv.set_double_value(120.0);
    config_values_["exposure.default_kv"] = kv;

    ConfigValue mas;
    mas.set_double_value(100.0);
    config_values_["exposure.default_mas"] = mas;

    ConfigValue transfer_mode;
    transfer_mode.set_int_value(static_cast<int64_t>(
        ImageTransferMode::IMAGE_TRANSFER_MODE_FULL_QUALITY));
    config_values_["exposure.default_transfer_mode"] = transfer_mode;

    // Collimator defaults
    ConfigValue max_opening;
    max_opening.set_double_value(300.0);
    config_values_["collimator.max_opening_mm"] = max_opening;

    // Heartbeat interval
    ConfigValue heartbeat;
    heartbeat.set_int_value(1000);
    config_values_["health.heartbeat_interval_ms"] = heartbeat;

    logger_->debug("Default configuration values loaded");
}

} // namespace hnvue::ipc
