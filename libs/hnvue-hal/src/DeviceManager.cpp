/**
 * @file DeviceManager.cpp
 * @brief Device Manager implementation
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class C - SAFETY CRITICAL: Device lifecycle management
 * SPDX-License-Identifier: MIT
 */

#include "hnvue/hal/DeviceManager.h"

#include "hnvue/hal/aec/AecController.h"
#include "hnvue/hal/generator/GeneratorSimulator.h"
#include "hnvue/hal/plugin/DetectorPluginLoader.h"

#include <fstream>
#include <regex>
#include <sstream>
#include <stdexcept>

namespace hnvue::hal {

// =============================================================================
// Construction / Destruction
// =============================================================================

DeviceManager::DeviceManager()
    : initialized_(false)
{
}

DeviceManager::~DeviceManager() {
    Shutdown();
}

// =============================================================================
// Initialization
// =============================================================================

bool DeviceManager::Initialize(const std::string& config_path) {
    if (initialized_) {
        ReportError(HalError::HAL_ERR_STATE, "DeviceManager already initialized");
        return false;
    }

    // Load and parse configuration
    if (!LoadConfiguration(config_path)) {
        return false;
    }

    // Configuration loaded successfully
    // Note: Actual device initialization would happen here
    // with dependency-respecting order

    initialized_ = true;
    return true;
}

bool DeviceManager::IsInitialized() const {
    return initialized_;
}

// =============================================================================
// Typed Device Accessors
// =============================================================================

IGenerator* DeviceManager::GetGenerator() {
    return generator_.get();
}

IDetector* DeviceManager::GetDetector() {
    return detector_.get();
}

ICollimator* DeviceManager::GetCollimator() {
    return collimator_.get();
}

IPatientTable* DeviceManager::GetPatientTable() {
    return patient_table_.get();
}

IAEC* DeviceManager::GetAEC() {
    return aec_.get();
}

IDoseMonitor* DeviceManager::GetDoseMonitor() {
    return dose_monitor_.get();
}

ISafetyInterlock* DeviceManager::GetSafetyInterlock() {
    return safety_interlock_.get();
}

// =============================================================================
// Shutdown
// =============================================================================

void DeviceManager::Shutdown() {
    if (!initialized_) {
        return;  // Already shut down or never initialized
    }

    // FR-HAL-03: Shutdown in reverse initialization order

    // 7. Patient Table (last initialized)
    patient_table_.reset();

    // 6. Collimator
    collimator_.reset();

    // 5. Safety Interlock
    safety_interlock_.reset();

    // 4. Dose Monitor
    dose_monitor_.reset();

    // 3. AEC Controller
    aec_.reset();

    // 2. Detector
    detector_.reset();

    // 1. Generator (first initialized)
    generator_.reset();

    initialized_ = false;
}

// =============================================================================
// Error Handling
// =============================================================================

void DeviceManager::RegisterErrorHandler(ErrorHandler handler) {
    error_handler_ = std::move(handler);
}

void DeviceManager::ReportError(HalError error, const std::string& message) {
    if (error_handler_) {
        try {
            error_handler_(error, message);
        } catch (...) {
            // Ignore exceptions in error handler to prevent recursive errors
        }
    }
}

// =============================================================================
// Private Methods
// =============================================================================

bool DeviceManager::LoadConfiguration(const std::string& config_path) {
    // Read configuration file
    std::ifstream file(config_path);
    if (!file.is_open()) {
        ReportError(HalError::HAL_ERR_PARAM, "Cannot open config file: " + config_path);
        return false;
    }

    std::string content((std::istreambuf_iterator<char>(file)),
                        std::istreambuf_iterator<char>());
    file.close();

    // Simple JSON parsing (for production, use proper JSON library)
    // This is a minimal implementation for the GREEN phase

    try {
        // Initialize generator (required)
        std::string gen_type = ExtractJsonString(content, "\"type\"", "simulator");
        std::string gen_port = ExtractJsonString(content, "\"port\"", "COM1");
        int gen_baud = ExtractJsonInt(content, "\"baud_rate\"", 115200);

        if (!InitializeGenerator(gen_type, gen_port, gen_baud)) {
            return false;
        }

        // Initialize AEC (required)
        std::string aec_mode_str = ExtractJsonString(content, "\"mode\"", "AEC_MANUAL");
        AecMode aec_mode = (aec_mode_str == "AEC_AUTO") ? AecMode::AEC_AUTO : AecMode::AEC_MANUAL;
        float aec_threshold = ExtractJsonFloat(content, "\"threshold_percent\"", 50.0f);

        if (!InitializeAEC(aec_mode, aec_threshold)) {
            return false;
        }

        // Detector is optional (enabled flag check)
        bool detector_enabled = ExtractJsonBool(content, "\"enabled\"", false);
        std::string detector_plugin = ExtractJsonString(content, "\"plugin_path\"", "");

        if (detector_enabled && !detector_plugin.empty()) {
            InitializeDetector(detector_plugin, detector_enabled);
        }

        return true;

    } catch (const std::exception& e) {
        ReportError(HalError::HAL_ERR_PARAM, std::string("JSON parse error: ") + e.what());
        return false;
    }
}

bool DeviceManager::InitializeGenerator(const std::string& type, const std::string& port, int baud_rate) {
    // FR-HAL-02: Create appropriate generator implementation
    if (type == "simulator") {
        generator_ = std::make_unique<GeneratorSimulator>();
        // Configure simulator with port and baud rate (if needed)
        return true;
    }

    // Other generator types (RS232, Ethernet) would be implemented here
    ReportError(HalError::HAL_ERR_NOT_SUPPORTED, "Unknown generator type: " + type);
    return false;
}

bool DeviceManager::InitializeDetector(const std::string& plugin_path, bool enabled) {
    if (!enabled) {
        return true;  // Detector optional
    }

    // FR-HAL-01: Load detector plugin
    // This would use DetectorPluginLoader in production
    // For GREEN phase, we skip plugin loading
    return true;
}

bool DeviceManager::InitializeAEC(AecMode mode, float threshold) {
    // FR-HAL-07: Create AEC controller with generator integration
    aec_ = std::make_unique<AecController>(generator_.get());

    // Configure AEC with settings from config
    if (!aec_->SetMode(mode)) {
        ReportError(HalError::HAL_ERR_PARAM, "Failed to set AEC mode");
        return false;
    }

    if (!aec_->SetThreshold(threshold)) {
        ReportError(HalError::HAL_ERR_PARAM, "Failed to set AEC threshold");
        return false;
    }

    return true;
}

// =============================================================================
// Simple JSON Parsing Helpers (for GREEN phase)
// =============================================================================

std::string DeviceManager::ExtractJsonString(const std::string& json, const std::string& key, const std::string& default_value) {
    // Simple regex-based extraction for GREEN phase
    // Production should use proper JSON library
    std::string pattern = key + "\\s*:\\s*\"([^\"]*)\"";
    std::regex regex(pattern);
    std::smatch match;

    if (std::regex_search(json, match, regex) && match.size() > 1) {
        return match[1].str();
    }

    return default_value;
}

int DeviceManager::ExtractJsonInt(const std::string& json, const std::string& key, int default_value) {
    std::string pattern = key + "\\s*:\\s*(\\d+)";
    std::regex regex(pattern);
    std::smatch match;

    if (std::regex_search(json, match, regex) && match.size() > 1) {
        return std::stoi(match[1].str());
    }

    return default_value;
}

float DeviceManager::ExtractJsonFloat(const std::string& json, const std::string& key, float default_value) {
    std::string pattern = key + "\\s*:\\s*([\\d.]+)";
    std::regex regex(pattern);
    std::smatch match;

    if (std::regex_search(json, match, regex) && match.size() > 1) {
        return std::stof(match[1].str());
    }

    return default_value;
}

bool DeviceManager::ExtractJsonBool(const std::string& json, const std::string& key, bool default_value) {
    std::string pattern = key + "\\s*:\\s*(true|false)";
    std::regex regex(pattern);
    std::smatch match;

    if (std::regex_search(json, match, regex) && match.size() > 1) {
        return match[1].str() == "true";
    }

    return default_value;
}

} // namespace hnvue::hal
