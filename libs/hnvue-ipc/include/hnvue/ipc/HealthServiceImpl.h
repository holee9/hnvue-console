/**
 * @file HealthServiceImpl.h
 * @brief Implementation of HealthService (Core Engine -> GUI health monitoring)
 * SPEC-IPC-001 Section 4.2.4: HealthService with server-streaming
 *
 * This service provides continuous health and status monitoring:
 * - Heartbeat events at 1Hz (configurable)
 * - Hardware status changes
 * - Fault events
 * - System state changes
 */

#ifndef HNVE_IPC_HEALTH_SERVICE_IMPL_H
#define HNVE_IPC_HEALTH_SERVICE_IMPL_H

#include <grpcpp/grpcpp.h>
#include <memory>
#include <atomic>
#include <mutex>
#include <vector>
#include <queue>
#include <condition_variable>
#include <spdlog/spdlog.h>

// Generated protobuf headers
#include "hnvue_health.grpc.pb.h"
#include "hnvue_health.pb.h"

namespace hnvue::ipc {

using hnvue::ipc::protobuf::HealthService;
using hnvue::ipc::protobuf::HealthSubscribeRequest;
using hnvue::ipc::protobuf::HealthEvent;
using hnvue::ipc::protobuf::HealthEventType;
using hnvue::ipc::protobuf::HeartbeatPayload;
using hnvue::ipc::protobuf::HardwareStatusPayload;
using hnvue::ipc::protobuf::FaultPayload;
using hnvue::ipc::protobuf::SystemStateChangePayload;
using hnvue::ipc::protobuf::HardwareComponentStatus;
using hnvue::ipc::protobuf::FaultSeverity;
using hnvue::ipc::protobuf::SystemState;

/**
 * @struct HardwareComponent
 * @brief Internal representation of a monitored hardware component
 */
struct HardwareComponent {
    uint32_t component_id;
    std::string component_name;
    HardwareComponentStatus current_status;
    std::string detail;

    HardwareComponent() : component_id(0), current_status(HardwareComponentStatus::HARDWARE_STATUS_UNSPECIFIED) {}
};

/**
 * @class HealthServiceImpl
 * @brief gRPC service implementation for health monitoring
 *
 * Thread safety: Uses atomic variables and mutex protection for state.
 *
 * SPEC-IPC-001 Section 4.2.4:
 * - Server-streaming RPC for event delivery
 * - Event types: HEARTBEAT, HARDWARE_STATUS, FAULT, STATE_CHANGE
 * - Event type filtering supported
 * - 1Hz heartbeat default interval
 *
 * SPEC-IPC-001 Section 4.3.3:
 * - Heartbeat every 1000ms (configurable)
 * - Client detects disconnect after 3000ms without heartbeat
 */
class HealthServiceImpl final : public HealthService::Service {
public:
    /**
     * @brief Construct HealthService implementation
     * @param logger Logger instance
     * @param heartbeat_interval_ms Heartbeat interval in milliseconds
     */
    explicit HealthServiceImpl(
        std::shared_ptr<spdlog::logger> logger = spdlog::default_logger(),
        uint32_t heartbeat_interval_ms = 1000
    );

    ~HealthServiceImpl() override;

    // Non-copyable, non-movable
    HealthServiceImpl(const HealthServiceImpl&) = delete;
    HealthServiceImpl& operator=(const HealthServiceImpl&) = delete;

    /**
     * @brief Subscribe to health and status events
     *
     * This is a server-streaming RPC. The Core Engine pushes events
     * to the GUI as they occur.
     *
     * SPEC-IPC-001 Section 4.2.4:
     * - Empty event_type_filter means subscribe to all
     * - Heartbeat sent at configured interval
     * - State changes sent immediately
     *
     * @param context gRPC server context (supports cancellation)
     * @param request Event type filter
     * @param writer Server writer for streaming events
     * @return gRPC status code
     */
    grpc::Status SubscribeHealth(
        grpc::ServerContext* context,
        const HealthSubscribeRequest* request,
        grpc::ServerWriter<HealthEvent>* writer) override;

    /**
     * @brief Update hardware component status
     *
     * Called by hardware monitoring subsystem when status changes.
     * Triggers HARDWARE_STATUS event to all subscribers.
     *
     * @param component_id Component identifier
     * @param component_name Component name
     * @param status New status
     * @param detail Optional detail string
     */
    void UpdateHardwareStatus(
        uint32_t component_id,
        const std::string& component_name,
        HardwareComponentStatus status,
        const std::string& detail = "");

    /**
     * @brief Report a fault event
     *
     * Called when a fault occurs in the system.
     * Triggers FAULT event to all subscribers.
     *
     * @param fault_code Fault code
     * @param fault_description Human-readable description
     * @param severity Fault severity level
     * @param requires_operator_action Whether operator must intervene
     */
    void ReportFault(
        uint32_t fault_code,
        const std::string& fault_description,
        FaultSeverity severity,
        bool requires_operator_action);

    /**
     * @brief Notify system state change
     *
     * Called when system state transitions.
     * Triggers STATE_CHANGE event to all subscribers.
     *
     * @param previous_state Previous system state
     * @param new_state New system state
     * @param reason Reason for state change
     */
    void NotifyStateChange(
        SystemState previous_state,
        SystemState new_state,
        const std::string& reason);

    /**
     * @brief Get the heartbeat interval
     * @return Heartbeat interval in milliseconds
     */
    uint32_t GetHeartbeatInterval() const;

    /**
     * @brief Set the heartbeat interval
     * @param interval_ms New interval in milliseconds
     */
    void SetHeartbeatInterval(uint32_t interval_ms);

    /**
     * @brief Get current heartbeat sequence number
     * @return Current sequence number
     */
    uint64_t GetHeartbeatSequence() const;

private:
    std::shared_ptr<spdlog::logger> logger_;

    // Heartbeat configuration
    std::atomic<uint32_t> heartbeat_interval_ms_;
    std::atomic<uint64_t> heartbeat_sequence_;

    // Hardware component registry (thread-safe)
    mutable std::mutex hardware_mutex_;
    std::unordered_map<uint32_t, HardwareComponent> hardware_components_;

    /**
     * @brief Create heartbeat event
     * @param event Output event to populate
     */
    void CreateHeartbeatEvent(HealthEvent* event) const;

    /**
     * @brief Create hardware status event
     * @param component Component to report
     * @param event Output event to populate
     */
    void CreateHardwareStatusEvent(
        const HardwareComponent& component,
        HealthEvent* event) const;

    /**
     * @brief Create fault event
     * @param fault_code Fault code
     * @param description Description
     * @param severity Severity
     * @param requires_action Action required flag
     * @param event Output event to populate
     */
    void CreateFaultEvent(
        uint32_t fault_code,
        const std::string& description,
        FaultSeverity severity,
        bool requires_action,
        HealthEvent* event) const;

    /**
     * @brief Create state change event
     * @param previous_state Previous state
     * @param new_state New state
     * @param reason Reason for change
     * @param event Output event to populate
     */
    void CreateStateChangeEvent(
        SystemState previous_state,
        SystemState new_state,
        const std::string& reason,
        HealthEvent* event) const;

    /**
     * @brief Check if event type matches filter
     * @param event_type Event type to check
     * @param filters List of allowed event types (empty = all)
     * @return true if event type passes filter
     */
    bool PassesFilter(
        HealthEventType event_type,
        const google::protobuf::RepeatedField<int>& filters) const;

    /**
     * @brief Get current CPU usage percentage
     * @return CPU usage (0-100)
     *
     * Platform-specific implementation.
     */
    float GetCpuUsage() const;

    /**
     * @brief Get current memory usage in MB
     * @return Memory usage in MB
     *
     * Platform-specific implementation.
     */
    float GetMemoryUsage() const;
};

} // namespace hnvue::ipc

#endif // HNVE_IPC_HEALTH_SERVICE_IMPL_H
