/**
 * @file CommandQueue.cpp
 * @brief Thread-safe FIFO queue implementation for HVG commands
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - Command infrastructure
 * SPDX-License-Identifier: MIT
 */

#include "hnvue/hal/generator/CommandQueue.h"

#include <algorithm>

namespace hnvue::hal {

// =============================================================================
// Constructor/Destructor
// =============================================================================

CommandQueue::CommandQueue(size_t max_depth, uint32_t timeout_ms, uint32_t max_retries)
    : max_depth_(max_depth)
    , timeout_ms_(timeout_ms)
    , max_retries_(max_retries)
    , push_count_(0)
    , pop_count_(0)
    , timeout_count_(0)
{
}

CommandQueue::~CommandQueue() {
    std::lock_guard<std::mutex> lock(mutex_);
    queue_.clear();
}

CommandQueue::CommandQueue(CommandQueue&& other) noexcept {
    std::lock_guard<std::mutex> lock(other.mutex_);
    queue_ = std::move(other.queue_);
    max_depth_ = other.max_depth_;
    timeout_ms_ = other.timeout_ms_;
    max_retries_ = other.max_retries_;
    push_count_.store(other.push_count_.load());
    pop_count_.store(other.pop_count_.load());
    timeout_count_.store(other.timeout_count_.load());
}

CommandQueue& CommandQueue::operator=(CommandQueue&& other) noexcept {
    if (this != &other) {
        std::lock(mutex_, other.mutex_);
        std::lock_guard<std::mutex> lock1(mutex_, std::adopt_lock);
        std::lock_guard<std::mutex> lock2(other.mutex_, std::adopt_lock);

        queue_ = std::move(other.queue_);
        max_depth_ = other.max_depth_;
        timeout_ms_ = other.timeout_ms_;
        max_retries_ = other.max_retries_;
        push_count_.store(other.push_count_.load());
        pop_count_.store(other.pop_count_.load());
        timeout_count_.store(other.timeout_count_.load());
    }
    return *this;
}

// =============================================================================
// Queue Operations
// =============================================================================

bool CommandQueue::Push(const HvgCommand& command) {
    std::lock_guard<std::mutex> lock(mutex_);

    if (queue_.size() >= max_depth_) {
        return false;  // Queue is full
    }

    HvgCommand cmd = command;
    cmd.timestamp_us = std::chrono::duration_cast<std::chrono::microseconds>(
        std::chrono::steady_clock::now().time_since_epoch()
    ).count();

    // Priority insertion: abort commands go to front
    if (cmd.type == CommandType::CMD_ABORT_EXPOSURE) {
        queue_.push_front(cmd);
    } else {
        queue_.push_back(cmd);
    }

    push_count_++;
    cond_var_.notify_one();
    return true;
}

bool CommandQueue::Push(HvgCommand&& command) {
    std::lock_guard<std::mutex> lock(mutex_);

    if (queue_.size() >= max_depth_) {
        return false;  // Queue is full
    }

    command.timestamp_us = std::chrono::duration_cast<std::chrono::microseconds>(
        std::chrono::steady_clock::now().time_since_epoch()
    ).count();

    // Priority insertion: abort commands go to front
    if (command.type == CommandType::CMD_ABORT_EXPOSURE) {
        queue_.push_front(std::move(command));
    } else {
        queue_.push_back(std::move(command));
    }

    push_count_++;
    cond_var_.notify_one();
    return true;
}

bool CommandQueue::TryPop(HvgCommand& command) {
    std::lock_guard<std::mutex> lock(mutex_);

    if (queue_.empty()) {
        return false;
    }

    command = std::move(queue_.front());
    queue_.pop_front();
    pop_count_++;
    return true;
}

bool CommandQueue::WaitPop(HvgCommand& command, uint32_t timeout_ms) {
    std::unique_lock<std::mutex> lock(mutex_);

    auto predicate = [this] { return !queue_.empty(); };

    if (timeout_ms == 0) {
        // Wait indefinitely
        cond_var_.wait(lock, predicate);
    } else {
        if (!cond_var_.wait_for(lock, std::chrono::milliseconds(timeout_ms), predicate)) {
            timeout_count_++;
            return false;  // Timeout
        }
    }

    command = std::move(queue_.front());
    queue_.pop_front();
    pop_count_++;
    return true;
}

size_t CommandQueue::Size() const {
    std::lock_guard<std::mutex> lock(mutex_);
    return queue_.size();
}

bool CommandQueue::Empty() const {
    std::lock_guard<std::mutex> lock(mutex_);
    return queue_.empty();
}

bool CommandQueue::Full() const {
    std::lock_guard<std::mutex> lock(mutex_);
    return queue_.size() >= max_depth_;
}

void CommandQueue::Clear() {
    std::lock_guard<std::mutex> lock(mutex_);
    queue_.clear();
}

bool CommandQueue::Retry(const HvgCommand& command) {
    if (command.retry_count >= max_retries_) {
        return false;  // Max retries exceeded
    }

    HvgCommand retry_cmd = command;
    retry_cmd.retry_count++;

    return Push(std::move(retry_cmd));
}

} // namespace hnvue::hal
