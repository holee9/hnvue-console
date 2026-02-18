/**
 * @file DmaRingBuffer.cpp
 * @brief DMA Ring Buffer implementation
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - DMA Ring Buffer Implementation
 * Implements FR-HAL-09: DMA Ring Buffer Management
 * Implements NFR-HAL-01: Data Transfer Latency (<= 100ms)
 *
 * SPDX-License-Identifier: MIT
 */

#include "hnvue/hal/DmaRingBuffer.h"
#include <stdexcept>
#include <mutex>
#include <condition_variable>
#include <cstring>
#include <atomic>

namespace hnvue::hal {

// =============================================================================
// Implementation Class (PIMPL)
// =============================================================================

/**
 * @brief Internal implementation of DmaRingBuffer
 *
 * Uses lock-free SPSC queue for single producer single consumer scenarios.
 * For BLOCK_PRODUCER policy, uses condition variable for blocking behavior.
 */
class DmaRingBufferImpl {
public:
    DmaRingBufferImpl(size_t depth, size_t frame_size, OverwritePolicy policy)
        : depth_(depth)
        , frame_size_(frame_size)
        , policy_(policy)
        , buffer_data_(depth * frame_size)
        , write_index_(0)
        , read_index_(0)
        , sequence_counter_(0)
        , frame_count_(0)
    {
        if (depth == 0) {
            throw std::invalid_argument("Buffer depth must be greater than 0");
        }
        if (frame_size == 0) {
            throw std::invalid_argument("Frame size must be greater than 0");
        }
    }

    ~DmaRingBufferImpl() = default;

    // Non-copyable, non-movable for simplicity
    DmaRingBufferImpl(const DmaRingBufferImpl&) = delete;
    DmaRingBufferImpl& operator=(const DmaRingBufferImpl&) = delete;

    bool WriteFrame(const void* data, size_t size, uint64_t& sequence_out) {
        if (size != frame_size_) {
            return false;
        }

        std::unique_lock<std::mutex> lock(mutex_);

        // Wait for space if BLOCK_PRODUCER policy and buffer is full
        if (policy_ == OverwritePolicy::BLOCK_PRODUCER) {
            write_cv_.wait(lock, [this]() { return frame_count_ < depth_; });
        }

        // Calculate write position
        size_t write_pos = write_index_ % depth_;
        uint8_t* write_ptr = buffer_data_.data() + (write_pos * frame_size_);

        // Copy frame data
        std::memcpy(write_ptr, data, frame_size_);

        // Assign sequence number
        sequence_out = sequence_counter_++;
        sequence_numbers_[write_pos] = sequence_out;

        // Handle DROP_OLDEST: if full, advance read_index to drop oldest
        if (frame_count_ == depth_ && policy_ == OverwritePolicy::DROP_OLDEST) {
            read_index_++;  // Drop oldest frame
            // frame_count_ remains at depth_
        } else if (frame_count_ < depth_) {
            frame_count_++;
        }

        // Advance write index
        write_index_++;

        // Invoke callback if registered
        if (callback_) {
            // Unlock before callback to avoid deadlock
            lock.unlock();
            callback_(write_ptr, frame_size_, sequence_out);
        }

        // Notify waiting reader (for BLOCK_PRODUCER scenario)
        read_cv_.notify_one();

        return true;
    }

    bool ReadFrame(void* buffer_out, size_t& size_out, uint64_t& sequence_out) {
        std::unique_lock<std::mutex> lock(mutex_);

        if (frame_count_ == 0) {
            return false;  // Buffer empty
        }

        // Calculate read position
        size_t read_pos = read_index_ % depth_;
        const uint8_t* read_ptr = buffer_data_.data() + (read_pos * frame_size_);

        // Copy frame data to output buffer
        std::memcpy(buffer_out, read_ptr, frame_size_);

        // Get sequence number
        sequence_out = sequence_numbers_[read_pos];
        size_out = frame_size_;

        // Advance read index and decrement count
        read_index_++;
        frame_count_--;

        // Notify waiting writer (for BLOCK_PRODUCER scenario)
        write_cv_.notify_one();

        return true;
    }

    bool IsEmpty() const {
        std::lock_guard<std::mutex> lock(mutex_);
        return frame_count_ == 0;
    }

    bool IsFull() const {
        std::lock_guard<std::mutex> lock(mutex_);
        return frame_count_ == depth_;
    }

    size_t GetAvailableFrameCount() const {
        std::lock_guard<std::mutex> lock(mutex_);
        return frame_count_;
    }

    void RegisterFrameCallback(DmaRingBuffer::FrameCallback callback) {
        std::lock_guard<std::mutex> lock(mutex_);
        callback_ = std::move(callback);
    }

    size_t GetDepth() const { return depth_; }
    size_t GetFrameSize() const { return frame_size_; }
    OverwritePolicy GetOverwritePolicy() const { return policy_; }

private:
    const size_t depth_;              ///< Buffer capacity in frames
    const size_t frame_size_;         ///< Size of each frame in bytes
    const OverwritePolicy policy_;    ///< Overwrite policy when full

    std::vector<uint8_t> buffer_data_;  ///< Pre-allocated buffer memory

    std::vector<uint64_t> sequence_numbers_;  ///< Sequence numbers for each slot

    std::atomic<uint64_t> write_index_;   ///< Current write position (monotonic)
    std::atomic<uint64_t> read_index_;    ///< Current read position (monotonic)

    std::atomic<uint64_t> sequence_counter_;  ///< Global sequence counter

    std::atomic<size_t> frame_count_;    ///< Current number of frames in buffer

    DmaRingBuffer::FrameCallback callback_;  ///< Frame-available callback

    mutable std::mutex mutex_;            ///< Protects shared state
    std::condition_variable write_cv_;    ///< CV for blocking producer
    std::condition_variable read_cv_;     ///< CV for notifications
};

// =============================================================================
// DmaRingBuffer Public Interface
// =============================================================================

DmaRingBuffer::DmaRingBuffer(size_t depth, size_t frame_size, OverwritePolicy policy)
    : impl_(std::make_unique<DmaRingBufferImpl>(depth, frame_size, policy)) {
}

DmaRingBuffer::~DmaRingBuffer() = default;

DmaRingBuffer::DmaRingBuffer(DmaRingBuffer&& other) noexcept
    : impl_(std::move(other.impl_)) {
}

DmaRingBuffer& DmaRingBuffer::operator=(DmaRingBuffer&& other) noexcept {
    if (this != &other) {
        impl_ = std::move(other.impl_);
    }
    return *this;
}

bool DmaRingBuffer::WriteFrame(const void* data, size_t size, uint64_t& sequence_out) {
    return impl_->WriteFrame(data, size, sequence_out);
}

bool DmaRingBuffer::ReadFrame(void* buffer_out, size_t& size_out, uint64_t& sequence_out) {
    return impl_->ReadFrame(buffer_out, size_out, sequence_out);
}

bool DmaRingBuffer::IsEmpty() const {
    return impl_->IsEmpty();
}

bool DmaRingBuffer::IsFull() const {
    return impl_->IsFull();
}

size_t DmaRingBuffer::GetAvailableFrameCount() const {
    return impl_->GetAvailableFrameCount();
}

void DmaRingBuffer::RegisterFrameCallback(FrameCallback callback) {
    impl_->RegisterFrameCallback(std::move(callback));
}

size_t DmaRingBuffer::GetDepth() const {
    return impl_->GetDepth();
}

size_t DmaRingBuffer::GetFrameSize() const {
    return impl_->GetFrameSize();
}

OverwritePolicy DmaRingBuffer::GetOverwritePolicy() const {
    return impl_->GetOverwritePolicy();
}

} // namespace hnvue::hal
