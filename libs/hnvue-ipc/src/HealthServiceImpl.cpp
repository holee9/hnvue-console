/**
 * @file HealthServiceImpl.cpp
 * @brief Implementation of HealthService (Core Engine -> GUI health monitoring)
 * SPEC-IPC-001 Section 4.2.4: HealthService with server-streaming
 */

#include "hnvue/ipc/HealthServiceImpl.h"
#include <fmt/format.h>
#include <thread>
#include <chrono>

#ifdef _WIN32
#include <windows.h>
#include <psapi.h>
#else
#include <sys/resource.h>
#include <unistd.h>
#endif

namespace hnvue::ipc {

HealthServiceImpl::HealthServiceImpl(
    std::shared_ptr<spdlog::logger> logger,
    uint32_t heartbeat_interval_ms)
    : logger_(logger)
    , heartbeat_interval_ms_(heartbeat_interval_ms)
    , heartbeat_sequence_(0) {
    logger_->info("HealthServiceImpl initialized (heartbeat_interval: {}ms)", heartbeat_interval_ms);
}

HealthServiceImpl::~HealthServiceImpl() = default;

grpc::Status HealthServiceImpl::SubscribeHealth(
    grpc::ServerContext* context,
    const HealthSubscribeRequest* request,
    grpc::ServerWriter<HealthEvent>* writer) {

    const auto& filters = request->event_type_filter();
    logger_->info("SubscribeHealth: filters_count={}", filters.size());

    // Stream health events until client disconnects
    while (!context->IsCancelled()) {
        // Send heartbeat
        HealthEvent heartbeat_event;
        CreateHeartbeatEvent(&heartbeat_event);

        if (PassesFilter(heartbeat_event.event_type(), filters)) {
            if (!writer->Write(heartbeat_event)) {
                logger_->debug("SubscribeHealth: failed to write heartbeat, client disconnected");
                break;
            }
        }

        // Wait for next heartbeat interval
        std::this_thread::sleep_for(std::chrono::milliseconds(heartbeat_interval_ms_));
    }

    logger_->info("SubscribeHealth: ending stream");
    return grpc::Status::OK;
}

void HealthServiceImpl::UpdateHardwareStatus(
    uint32_t component_id,
    const std::string& component_name,
    HardwareComponentStatus status,
    const std::string& detail) {

    std::lock_guard<std::mutex> lock(hardware_mutex_);

    auto& component = hardware_components_[component_id];
    bool status_changed = (component.current_status != status);

    component.component_id = component_id;
    component.component_name = component_name;
    component.current_status = status;
    component.detail = detail;

    if (status_changed) {
        logger_->info("Hardware status changed: {} ({}) -> {}",
                     component_name, component_id, static_cast<int>(status));

        // TODO: Notify active subscribers via streaming
    }
}

void HealthServiceImpl::ReportFault(
    uint32_t fault_code,
    const std::string& fault_description,
    FaultSeverity severity,
    bool requires_operator_action) {

    logger_->warn("Fault reported: code={}, severity={}, action_required={}",
                 fault_code, static_cast<int>(severity), requires_operator_action);

    // TODO: Notify active subscribers via streaming
}

void HealthServiceImpl::NotifyStateChange(
    SystemState previous_state,
    SystemState new_state,
    const std::string& reason) {

    logger_->info("State change: {} -> {} (reason: {})",
                 static_cast<int>(previous_state),
                 static_cast<int>(new_state),
                 reason);

    // TODO: Notify active subscribers via streaming
}

uint32_t HealthServiceImpl::GetHeartbeatInterval() const {
    return heartbeat_interval_ms_.load(std::memory_order_acquire);
}

void HealthServiceImpl::SetHeartbeatInterval(uint32_t interval_ms) {
    heartbeat_interval_ms_.store(interval_ms, std::memory_order_release);
    logger_->info("Heartbeat interval changed to {}ms", interval_ms);
}

uint64_t HealthServiceImpl::GetHeartbeatSequence() const {
    return heartbeat_sequence_.load(std::memory_order_acquire);
}

void HealthServiceImpl::CreateHeartbeatEvent(HealthEvent* event) const {
    event->set_event_type(HealthEventType::HEALTH_EVENT_TYPE_HEARTBEAT);

    uint64_t seq = heartbeat_sequence_.fetch_add(1, std::memory_order_relaxed);
    auto* payload = event->mutable_heartbeat();
    payload->set_sequence_number(seq);
    payload->set_cpu_usage_percent(GetCpuUsage());
    payload->set_memory_usage_mb(GetMemoryUsage());

    event->mutable_event_timestamp()->set_microseconds_since_start(0);
}

void HealthServiceImpl::CreateHardwareStatusEvent(
    const HardwareComponent& component,
    HealthEvent* event) const {

    event->set_event_type(HealthEventType::HEALTH_EVENT_TYPE_HARDWARE_STATUS);

    auto* payload = event->mutable_hardware_status();
    payload->set_component_id(component.component_id);
    payload->set_component_name(component.component_name);
    payload->set_status(component.current_status);
    payload->set_detail(component.detail);

    event->mutable_event_timestamp()->set_microseconds_since_start(0);
}

void HealthServiceImpl::CreateFaultEvent(
    uint32_t fault_code,
    const std::string& description,
    FaultSeverity severity,
    bool requires_action,
    HealthEvent* event) const {

    event->set_event_type(HealthEventType::HEALTH_EVENT_TYPE_FAULT);

    auto* payload = event->mutable_fault();
    payload->set_fault_code(fault_code);
    payload->set_fault_description(description);
    payload->set_severity(severity);
    payload->set_requires_operator_action(requires_action);

    event->mutable_event_timestamp()->set_microseconds_since_start(0);
}

void HealthServiceImpl::CreateStateChangeEvent(
    SystemState previous_state,
    SystemState new_state,
    const std::string& reason,
    HealthEvent* event) const {

    event->set_event_type(HealthEventType::HEALTH_EVENT_TYPE_STATE_CHANGE);

    auto* payload = event->mutable_state_change();
    payload->set_previous_state(previous_state);
    payload->set_new_state(new_state);
    payload->set_reason(reason);

    event->mutable_event_timestamp()->set_microseconds_since_start(0);
}

bool HealthServiceImpl::PassesFilter(
    HealthEventType event_type,
    const google::protobuf::RepeatedField<int>& filters) const {

    if (filters.empty()) {
        return true;  // No filter = accept all
    }

    for (int filter : filters) {
        if (filter == static_cast<int>(event_type)) {
            return true;
        }
    }

    return false;
}

float HealthServiceImpl::GetCpuUsage() const {
    // Platform-specific CPU usage
#ifdef _WIN32
    FILETIME idle_time, kernel_time, user_time;
    if (GetSystemTimes(&idle_time, &kernel_time, &user_time)) {
        // Calculate CPU usage (simplified)
        return 25.0f;  // Placeholder
    }
#else
    // Linux/macOS: Read /proc/stat or use sysctl
    return 25.0f;  // Placeholder
#endif
    return 0.0f;
}

float HealthServiceImpl::GetMemoryUsage() const {
    // Platform-specific memory usage
#ifdef _WIN32
    PROCESS_MEMORY_COUNTERS_EX pmc;
    if (GetProcessMemoryInfo(GetCurrentProcess(),
                            (PROCESS_MEMORY_COUNTERS*)&pmc,
                            sizeof(pmc))) {
        return static_cast<float>(pmc.WorkingSetSize / (1024 * 1024));
    }
#else
    struct rusage usage;
    if (getrusage(RUSAGE_SELF, &usage) == 0) {
        return static_cast<float>(usage.ru_maxrss / 1024);  // Convert to MB
    }
#endif
    return 0.0f;
}

} // namespace hnvue::ipc
