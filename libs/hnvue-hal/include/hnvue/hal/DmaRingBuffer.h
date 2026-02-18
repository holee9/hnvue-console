/**
 * @file DmaRingBuffer.h
 * @brief DMA Ring Buffer for high-throughput detector frame data transfer
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - DMA Ring Buffer
 * Implements FR-HAL-09: DMA Ring Buffer Management
 * Implements NFR-HAL-01: Data Transfer Latency (<= 100ms)
 *
 * SPDX-License-Identifier: MIT
 */

#ifndef HNUE_HAL_DMA_RING_BUFFER_H
#define HNUE_HAL_DMA_RING_BUFFER_H

#include <cstdint>
#include <functional>
#include <memory>

namespace hnvue::hal {

// =============================================================================
// Forward Declarations
// =============================================================================

class DmaRingBufferImpl;

// =============================================================================
// Enumerations
// =============================================================================

/**
 * @brief Overwrite policy for ring buffer when full
 *
 * Determines behavior when producer attempts to write to full buffer.
 */
enum class OverwritePolicy : uint32_t {
    DROP_OLDEST = 0,      ///< Drop oldest frame to make space (non-blocking)
    BLOCK_PRODUCER = 1    ///< Block producer thread until space available
};

// =============================================================================
// DmaRingBuffer Class
// =============================================================================

/**
 * @brief Lock-free SPSC circular buffer for detector frame DMA
 *
 * Manages a pre-allocated circular buffer in user-space memory for
 * high-throughput detector frame data transfer from detector plugins
 * to consumer callbacks.
 *
 * Key Features:
 * - Thread-safe single-producer single-consumer (SPSC) semantics
 * - Monotonically increasing frame sequence numbers
 * - No heap allocation during operation (pre-allocated at construction)
 * - Configurable overwrite policy (DROP_OLDEST or BLOCK_PRODUCER)
 * - Non-blocking read operations
 *
 * Thread Safety:
 * - Safe for concurrent use by one producer thread and one consumer thread
 * - Not safe for multiple producers or multiple consumers without external sync
 *
 * Performance (NFR-HAL-01):
 * - Frame DMA to callback latency: <= 100 ms under normal conditions
 *
 * IEC 62304 Class B: Software failure may cause loss of diagnostic data
 * but not direct physical harm to patients.
 *
 * @see FrameCallback for frame delivery mechanism
 */
class DmaRingBuffer {
public:
    // ------------------------------------------------------------------------
    // Public Types
    // ------------------------------------------------------------------------

    /**
     * @brief Frame-available callback signature
     *
     * Registered callbacks are invoked when a frame is written to the buffer.
     * Callback receives non-owning view of frame data (valid only during callback).
     *
     * @param data Pointer to frame data (valid only during callback)
     * @param size Frame size in bytes
     * @param sequence Monotonically increasing frame sequence number
     */
    using FrameCallback = std::function<void(const void* data, size_t size, uint64_t sequence)>;

    // ------------------------------------------------------------------------
    // Constructor / Destructor
    // ------------------------------------------------------------------------

    /**
     * @brief Construct DMA ring buffer with specified parameters
     *
     * Pre-allocates buffer memory of size (depth * frame_size) bytes.
     * No heap allocation occurs during operation after construction.
     *
     * @param depth Number of frames the buffer can hold (must be > 0)
     * @param frame_size Size of each frame in bytes (must be > 0)
     * @param policy Overwrite policy when buffer is full
     * @throws std::invalid_argument if depth or frame_size is 0
     * @throws std::bad_alloc if buffer allocation fails
     */
    DmaRingBuffer(size_t depth, size_t frame_size, OverwritePolicy policy);

    /**
     * @brief Destructor - releases buffer memory
     */
    ~DmaRingBuffer();

    // Prevent copying (buffer owns unique resources)
    DmaRingBuffer(const DmaRingBuffer&) = delete;
    DmaRingBuffer& operator=(const DmaRingBuffer&) = delete;

    // Allow moving
    DmaRingBuffer(DmaRingBuffer&&) noexcept;
    DmaRingBuffer& operator=(DmaRingBuffer&&) noexcept;

    // ------------------------------------------------------------------------
    // Producer Interface (Detector Plugin Thread)
    // ------------------------------------------------------------------------

    /**
     * @brief Write frame data to ring buffer (producer thread)
     *
     * Writes frame data to the next available slot in the circular buffer.
     * Behavior depends on overwrite policy:
     * - DROP_OLDEST: Overwrites oldest frame if buffer full, always succeeds
     * - BLOCK_PRODUCER: Blocks until space available if buffer full
     *
     * On successful write, increments frame sequence number and invokes
     * registered callback (if any) before returning.
     *
     * Thread Safety: Safe to call from single producer thread concurrently
     * with consumer ReadFrame calls.
     *
     * @param data Pointer to frame data to write (must be valid for frame_size bytes)
     * @param size Size of frame data in bytes (must match configured frame_size)
     * @param sequence_out Output parameter receiving assigned sequence number
     * @return true if write succeeded, false if size doesn't match frame_size
     *
     * @pre data must point to valid memory of at least frame_size bytes
     * @post sequence_out contains monotonically increasing sequence number
     * @post callback invoked (if registered) on success
     */
    bool WriteFrame(const void* data, size_t size, uint64_t& sequence_out);

    // ------------------------------------------------------------------------
    // Consumer Interface (Callback Thread)
    // ------------------------------------------------------------------------

    /**
     * @brief Read frame data from ring buffer (consumer thread)
     *
     * Reads the oldest available frame from the buffer.
     * Non-blocking operation - returns false immediately if buffer empty.
     *
     * Caller must provide buffer of sufficient size (frame_size bytes).
     * Frame data is copied from ring buffer to caller's buffer.
     *
     * Thread Safety: Safe to call from single consumer thread concurrently
     * with producer WriteFrame calls.
     *
     * @param buffer_out Pointer to output buffer (must be valid for frame_size bytes)
     * @param size_out Output parameter receiving actual frame size (always frame_size)
     * @param sequence_out Output parameter receiving frame sequence number
     * @return true if frame read successfully, false if buffer empty
     *
     * @pre buffer_out must point to valid memory of at least frame_size bytes
     * @post On success, buffer_out contains frame data, size_out == frame_size
     * @post On success, sequence_out contains frame's sequence number
     */
    bool ReadFrame(void* buffer_out, size_t& size_out, uint64_t& sequence_out);

    // ------------------------------------------------------------------------
    // State Query
    // ------------------------------------------------------------------------

    /**
     * @brief Check if buffer is empty (no frames available for reading)
     *
     * @return true if buffer has no frames, false otherwise
     */
    bool IsEmpty() const;

    /**
     * @brief Check if buffer is full (no space for writing without overwrite/block)
     *
     * @return true if buffer is at capacity, false otherwise
     */
    bool IsFull() const;

    /**
     * @brief Get number of frames currently available for reading
     *
     * @return Number of frames in buffer (0 to depth)
     */
    size_t GetAvailableFrameCount() const;

    // ------------------------------------------------------------------------
    // Callback Registration
    // ------------------------------------------------------------------------

    /**
     * @brief Register frame-available callback
     *
     * Registers a callback function to be invoked whenever a frame is
     * successfully written to the buffer. Callback is invoked from the
     * producer thread during WriteFrame() execution.
     *
     * Callback receives non-owning view of frame data - data pointer is
     * only valid during callback execution. Copy data if needed beyond callback.
     *
     * Only one callback can be registered at a time. Registering a new
     * callback replaces any previously registered callback.
     *
     * Thread Safety: Safe to call at any time, but recommended to call
     * during initialization before concurrent operations begin.
     *
     * @param callback Function object to invoke on frame write (empty to unregister)
     *
     * @post callback is invoked on each successful WriteFrame()
     */
    void RegisterFrameCallback(FrameCallback callback);

    // ------------------------------------------------------------------------
    // Configuration Access
    // ------------------------------------------------------------------------

    /**
     * @brief Get configured buffer depth (capacity in frames)
     *
     * @return Buffer depth in frames
     */
    size_t GetDepth() const;

    /**
     * @brief Get configured frame size in bytes
     *
     * @return Frame size in bytes
     */
    size_t GetFrameSize() const;

    /**
     * @brief Get current overwrite policy
     *
     * @return Current OverwritePolicy
     */
    OverwritePolicy GetOverwritePolicy() const;

private:
    // PIMPL idiom for implementation hiding and ABI stability
    std::unique_ptr<DmaRingBufferImpl> impl_;
};

} // namespace hnvue::hal

#endif // HNUE_HAL_DMA_RING_BUFFER_H
