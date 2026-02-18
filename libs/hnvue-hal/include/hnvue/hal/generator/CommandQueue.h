/**
 * @file CommandQueue.h
 * @brief Thread-safe FIFO queue for HVG commands with timeout and retry
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Command infrastructure
 * SPDX-License-Identifier: MIT
 */

#ifndef HNUE_HAL_COMMAND_QUEUE_H
#define HNUE_HAL_COMMAND_QUEUE_H

#include <queue>
#include <mutex>
#include <condition_variable>
#include <chrono>
#include <functional>
#include <atomic>

#include "hnvue/hal/HalTypes.h"
#include <deque>

namespace hnvue::hal {

/**
 * @brief HVG command types
 */
enum class CommandType : int32_t {
    CMD_SET_EXPOSURE_PARAMS = 1,
    CMD_START_EXPOSURE = 2,
    CMD_ABORT_EXPOSURE = 3,
    CMD_GET_STATUS = 4,
    CMD_GET_CAPABILITIES = 5
};

/**
 * @brief HVG command wrapper with metadata
 */
struct HvgCommand {
    CommandType type;
    std::function<HvgResponse()> execute;  // Command execution handler
    uint32_t retry_count = 0;              // Current retry count
    int64_t timestamp_us = 0;              // Command creation timestamp

    // Priority: abort commands have highest priority
    int priority() const {
        return (type == CommandType::CMD_ABORT_EXPOSURE) ? 100 : 0;
    }

    // For priority_queue ordering (lower priority value = higher priority)
    bool operator<(const HvgCommand& other) const {
        if (priority() != other.priority()) {
            return priority() < other.priority();  // Higher priority first
        }
        return timestamp_us > other.timestamp_us;  // Earlier commands first
    }
};

/**
 * @brief Thread-safe command queue for HVG operations
 *
 * Features:
 * - FIFO ordering with priority promotion for abort commands
 * - Configurable max depth
 * - Command timeout
 * - Retry logic (max 3 retries by default)
 * - Thread-safe operations (SPSC or MPSC with mutex)
 *
 * Performance: FR-HAL-05 requires 50ms round-trip, FR-HAL-06 requires
 * <50ms alarm delivery latency.
 */
class CommandQueue {
public:
    /**
     * @brief Construct command queue with configuration
     * @param max_depth Maximum queue depth (default 16)
     * @param timeout_ms Command timeout in milliseconds (default 500)
     * @param max_retries Maximum retry attempts (default 3)
     */
    explicit CommandQueue(
        size_t max_depth = 16,
        uint32_t timeout_ms = 500,
        uint32_t max_retries = 3
    );

    /**
     * @brief Destructor
     */
    ~CommandQueue();

    // Disable copy
    CommandQueue(const CommandQueue&) = delete;
    CommandQueue& operator=(const CommandQueue&) = delete;

    // Enable move
    CommandQueue(CommandQueue&&) noexcept;
    CommandQueue& operator=(CommandQueue&&) noexcept;

    // =========================================================================
    // Queue Operations
    // =========================================================================

    /**
     * @brief Push command to queue
     * @param command Command to push
     * @return true if command was queued, false if queue is full
     *
     * Thread-safe: Can be called from multiple threads.
     * Abort commands get priority promotion.
     */
    bool Push(const HvgCommand& command);

    /**
     * @brief Push command to queue (rvalue overload)
     * @param command Command to push
     * @return true if command was queued, false if queue is full
     */
    bool Push(HvgCommand&& command);

    /**
     * @brief Try to pop command from queue (non-blocking)
     * @param[out] command Output parameter for popped command
     * @return true if command was popped, false if queue is empty
     *
     * Thread-safe: Can be called from multiple threads.
     */
    bool TryPop(HvgCommand& command);

    /**
     * @brief Wait for command with timeout
     * @param[out] command Output parameter for popped command
     * @param timeout_ms Timeout in milliseconds
     * @return true if command was popped, false if timeout occurred
     *
     * Thread-safe: Can be called from multiple threads.
     */
    bool WaitPop(HvgCommand& command, uint32_t timeout_ms);

    /**
     * @brief Get current queue size
     * @return Current number of commands in queue
     *
     * Thread-safe: Can be called from multiple threads.
     */
    size_t Size() const;

    /**
     * @brief Check if queue is empty
     * @return true if queue is empty
     *
     * Thread-safe: Can be called from multiple threads.
     */
    bool Empty() const;

    /**
     * @brief Check if queue is full
     * @return true if queue is at capacity
     *
     * Thread-safe: Can be called from multiple threads.
     */
    bool Full() const;

    /**
     * @brief Clear all commands from queue
     *
     * Thread-safe: Can be called from multiple threads.
     */
    void Clear();

    /**
     * @brief Retry a failed command
     * @param command Command to retry
     * @return true if command was re-queued, false if max retries exceeded
     *
     * Thread-safe: Can be called from multiple threads.
     */
    bool Retry(const HvgCommand& command);

    // =========================================================================
    // Configuration Accessors
    // =========================================================================

    /**
     * @brief Get maximum queue depth
     * @return Maximum queue capacity
     */
    size_t GetMaxDepth() const { return max_depth_; }

    /**
     * @brief Get command timeout
     * @return Timeout in milliseconds
     */
    uint32_t GetTimeout() const { return timeout_ms_; }

    /**
     * @brief Get maximum retry count
     * @return Maximum retry attempts
     */
    uint32_t GetMaxRetries() const { return max_retries_; }

private:
    // Queue storage
    mutable std::mutex mutex_;
    std::condition_variable cond_var_;

    // Use std::deque for FIFO with priority queue semantics
    std::deque<HvgCommand> queue_;

    // Configuration
    size_t max_depth_;
    uint32_t timeout_ms_;
    uint32_t max_retries_;

    // Statistics
    std::atomic<uint64_t> push_count_;
    std::atomic<uint64_t> pop_count_;
    std::atomic<uint64_t> timeout_count_;
};

} // namespace hnvue::hal

#endif // HNUE_HAL_COMMAND_QUEUE_H
