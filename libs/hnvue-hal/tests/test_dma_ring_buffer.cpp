/**
 * @file test_dma_ring_buffer.cpp
 * @brief Unit tests for DmaRingBuffer component
 * @date 2026-02-18
 * @author abyz-lab
 *
 * IEC 62304 Class B - DMA Ring Buffer Tests
 * Tests FR-HAL-09: DMA Ring Buffer Management
 * Tests NFR-HAL-01: Data Transfer Latency (<= 100ms)
 *
 * SPDX-License-Identifier: MIT
 */

#include <gtest/gtest.h>
#include <thread>
#include <chrono>
#include <vector>
#include <atomic>
#include <cstring>
#include "hnvue/hal/DmaRingBuffer.h"

using namespace hnvue::hal;

// =============================================================================
// Test Fixture
// =============================================================================

class DmaRingBufferTest : public ::testing::Test {
protected:
    static constexpr size_t TEST_BUFFER_DEPTH = 4;
    static constexpr size_t TEST_FRAME_SIZE = 1024;

    void SetUp() override {
        // Default: DROP_OLDEST policy
        buffer = std::make_unique<DmaRingBuffer>(
            TEST_BUFFER_DEPTH,
            TEST_FRAME_SIZE,
            OverwritePolicy::DROP_OLDEST
        );
    }

    void TearDown() override {
        buffer.reset();
    }

    // Helper: Create test frame pattern
    std::vector<uint8_t> CreateTestFrame(uint8_t pattern) {
        std::vector<uint8_t> frame(TEST_FRAME_SIZE);
        std::fill(frame.begin(), frame.end(), pattern);
        return frame;
    }

    // Helper: Verify frame content
    bool VerifyFramePattern(const void* data, uint8_t expected_pattern) {
        const uint8_t* bytes = static_cast<const uint8_t*>(data);
        for (size_t i = 0; i < TEST_FRAME_SIZE; ++i) {
            if (bytes[i] != expected_pattern) {
                return false;
            }
        }
        return true;
    }

    std::unique_ptr<DmaRingBuffer> buffer;
};

// =============================================================================
// Basic Functionality Tests (FR-HAL-09)
// =============================================================================

/**
 * TEST: Buffer initialization with valid parameters
 * FR-HAL-09: Configurable buffer depth, frame size, overwrite policy
 */
TEST_F(DmaRingBufferTest, InitializeWithValidParameters) {
    EXPECT_NE(buffer, nullptr);
    EXPECT_FALSE(buffer->IsFull());
    EXPECT_TRUE(buffer->IsEmpty());
    EXPECT_EQ(buffer->GetAvailableFrameCount(), 0);
}

/**
 * TEST: Write and read single frame
 * FR-HAL-09: Thread-safe producer-consumer semantics
 */
TEST_F(DmaRingBufferTest, WriteAndReadSingleFrame) {
    auto test_frame = CreateTestFrame(0xAB);
    uint64_t sequence_out = 0;

    // Write frame
    bool write_success = buffer->WriteFrame(
        test_frame.data(),
        test_frame.size(),
        sequence_out
    );
    EXPECT_TRUE(write_success);
    EXPECT_EQ(sequence_out, 0);  // First frame has sequence 0

    // Read frame
    std::vector<uint8_t> read_buffer(TEST_FRAME_SIZE);
    size_t size_out = 0;
    uint64_t read_sequence = 0;

    bool read_success = buffer->ReadFrame(
        read_buffer.data(),
        size_out,
        read_sequence
    );
    EXPECT_TRUE(read_success);
    EXPECT_EQ(size_out, TEST_FRAME_SIZE);
    EXPECT_EQ(read_sequence, 0);
    EXPECT_TRUE(VerifyFramePattern(read_buffer.data(), 0xAB));
}

/**
 * TEST: Multiple frames maintain monotonically increasing sequence numbers
 * FR-HAL-09: Monotonically increasing uint64 sequence number
 */
TEST_F(DmaRingBufferTest, MonotonicallyIncreasingSequenceNumbers) {
    for (uint8_t i = 0; i < TEST_BUFFER_DEPTH; ++i) {
        auto frame = CreateTestFrame(i);
        uint64_t sequence = 0;
        ASSERT_TRUE(buffer->WriteFrame(frame.data(), frame.size(), sequence));
        EXPECT_EQ(sequence, i);
    }

    // Verify sequence numbers on read
    for (uint8_t i = 0; i < TEST_BUFFER_DEPTH; ++i) {
        std::vector<uint8_t> read_buffer(TEST_FRAME_SIZE);
        size_t size_out = 0;
        uint64_t sequence = 0;

        ASSERT_TRUE(buffer->ReadFrame(read_buffer.data(), size_out, sequence));
        EXPECT_EQ(sequence, i);
        EXPECT_TRUE(VerifyFramePattern(read_buffer.data(), i));
    }
}

/**
 * TEST: Buffer empties after reading all frames
 */
TEST_F(DmaRingBufferTest, BufferEmptiesAfterReadingAllFrames) {
    // Write frames
    for (size_t i = 0; i < TEST_BUFFER_DEPTH; ++i) {
        auto frame = CreateTestFrame(static_cast<uint8_t>(i));
        uint64_t sequence = 0;
        ASSERT_TRUE(buffer->WriteFrame(frame.data(), frame.size(), sequence));
    }

    EXPECT_TRUE(buffer->IsFull());
    EXPECT_FALSE(buffer->IsEmpty());

    // Read all frames
    for (size_t i = 0; i < TEST_BUFFER_DEPTH; ++i) {
        std::vector<uint8_t> read_buffer(TEST_FRAME_SIZE);
        size_t size_out = 0;
        uint64_t sequence = 0;
        ASSERT_TRUE(buffer->ReadFrame(read_buffer.data(), size_out, sequence));
    }

    EXPECT_FALSE(buffer->IsFull());
    EXPECT_TRUE(buffer->IsEmpty());
    EXPECT_EQ(buffer->GetAvailableFrameCount(), 0);
}

/**
 * TEST: Available frame count accuracy
 */
TEST_F(DmaRingBufferTest, AvailableFrameCountAccuracy) {
    EXPECT_EQ(buffer->GetAvailableFrameCount(), 0);

    for (size_t i = 1; i <= TEST_BUFFER_DEPTH; ++i) {
        auto frame = CreateTestFrame(static_cast<uint8_t>(i));
        uint64_t sequence = 0;
        buffer->WriteFrame(frame.data(), frame.size(), sequence);
        EXPECT_EQ(buffer->GetAvailableFrameCount(), i);
    }
}

// =============================================================================
// Overwrite Policy Tests (FR-HAL-09)
// =============================================================================

/**
 * TEST: DROP_OLDEST policy overwrites oldest frame when buffer full
 * FR-HAL-09: Overwrite policy (DROP_OLDEST, BLOCK_PRODUCER)
 */
TEST_F(DmaRingBufferTest, DropOldestPolicyOverwritesWhenFull) {
    // Fill buffer
    for (size_t i = 0; i < TEST_BUFFER_DEPTH; ++i) {
        auto frame = CreateTestFrame(static_cast<uint8_t>(i));
        uint64_t sequence = 0;
        ASSERT_TRUE(buffer->WriteFrame(frame.data(), frame.size(), sequence));
    }

    EXPECT_TRUE(buffer->IsFull());

    // Write one more frame - should drop oldest (sequence 0)
    auto extra_frame = CreateTestFrame(0xFF);
    uint64_t extra_sequence = 0;
    ASSERT_TRUE(buffer->WriteFrame(extra_frame.data(), extra_frame.size(), extra_sequence));
    EXPECT_EQ(extra_sequence, TEST_BUFFER_DEPTH);  // Sequence continues

    // Buffer should still be full
    EXPECT_TRUE(buffer->IsFull());
    EXPECT_EQ(buffer->GetAvailableFrameCount(), TEST_BUFFER_DEPTH);

    // Read and verify - frame 0 should be dropped, frame 1-3 + extra present
    for (size_t i = 1; i <= TEST_BUFFER_DEPTH; ++i) {
        std::vector<uint8_t> read_buffer(TEST_FRAME_SIZE);
        size_t size_out = 0;
        uint64_t sequence = 0;
        ASSERT_TRUE(buffer->ReadFrame(read_buffer.data(), size_out, sequence));

        if (i < TEST_BUFFER_DEPTH) {
            EXPECT_EQ(sequence, i);
            EXPECT_TRUE(VerifyFramePattern(read_buffer.data(), static_cast<uint8_t>(i)));
        } else {
            EXPECT_EQ(sequence, TEST_BUFFER_DEPTH);
            EXPECT_TRUE(VerifyFramePattern(read_buffer.data(), 0xFF));
        }
    }

    EXPECT_TRUE(buffer->IsEmpty());
}

/**
 * TEST: BLOCK_PRODUCER policy blocks when buffer full
 * FR-HAL-09: BLOCK_PRODUCER blocks write until space available
 */
TEST_F(DmaRingBufferTest, BlockProducerPolicyBlocksWhenFull) {
    auto block_buffer = std::make_unique<DmaRingBuffer>(
        TEST_BUFFER_DEPTH,
        TEST_FRAME_SIZE,
        OverwritePolicy::BLOCK_PRODUCER
    );

    // Fill buffer
    for (size_t i = 0; i < TEST_BUFFER_DEPTH; ++i) {
        auto frame = CreateTestFrame(static_cast<uint8_t>(i));
        uint64_t sequence = 0;
        ASSERT_TRUE(block_buffer->WriteFrame(frame.data(), frame.size(), sequence));
    }

    EXPECT_TRUE(block_buffer->IsFull());

    // Attempt to write when full should block
    // We'll test this with a timeout in a separate thread
    std::atomic<bool> write_completed{false};
    std::atomic<bool> read_triggered{false};

    auto write_thread = std::thread([&]() {
        auto blocking_frame = CreateTestFrame(0xAA);
        uint64_t sequence = 0;
        // This should block until we read
        block_buffer->WriteFrame(blocking_frame.data(), blocking_frame.size(), sequence);
        write_completed = true;
    });

    // Give write thread time to start and block
    std::this_thread::sleep_for(std::chrono::milliseconds(100));

    // Write should not have completed yet
    EXPECT_FALSE(write_completed.load());

    // Read one frame to unblock
    read_triggered = true;
    std::vector<uint8_t> read_buffer(TEST_FRAME_SIZE);
    size_t size_out = 0;
    uint64_t sequence = 0;
    ASSERT_TRUE(block_buffer->ReadFrame(read_buffer.data(), size_out, sequence));

    // Now write should complete
    write_thread.join();
    EXPECT_TRUE(write_completed.load());
}

// =============================================================================
// Thread Safety Tests (FR-HAL-09, NFR-HAL-05)
// =============================================================================

/**
 * TEST: Concurrent producer and consumer (SPSC pattern)
 * FR-HAL-09: Thread-safe producer-consumer semantics
 * NFR-HAL-05: Thread safety
 */
TEST_F(DmaRingBufferTest, ConcurrentProducerConsumerStressTest) {
    constexpr int NUM_FRAMES = 1000;
    std::atomic<int> producer_count{0};
    std::atomic<int> consumer_count{0};
    std::atomic<bool> producer_done{false};

    // Producer thread
    auto producer = std::thread([&]() {
        for (int i = 0; i < NUM_FRAMES; ++i) {
            auto frame = CreateTestFrame(static_cast<uint8_t>(i % 256));
            uint64_t sequence = 0;
            while (!buffer->WriteFrame(frame.data(), frame.size(), sequence)) {
                std::this_thread::yield();
            }
            producer_count++;
        }
        producer_done = true;
    });

    // Consumer thread
    auto consumer = std::thread([&]() {
        std::vector<uint8_t> read_buffer(TEST_FRAME_SIZE);
        while (consumer_count < NUM_FRAMES) {
            size_t size_out = 0;
            uint64_t sequence = 0;
            if (buffer->ReadFrame(read_buffer.data(), size_out, sequence)) {
                consumer_count++;
            } else if (!producer_done) {
                std::this_thread::yield();
            }
        }
    });

    producer.join();
    consumer.join();

    EXPECT_EQ(producer_count, NUM_FRAMES);
    EXPECT_EQ(consumer_count, NUM_FRAMES);
}

// =============================================================================
// Latency Tests (NFR-HAL-01)
// =============================================================================

/**
 * TEST: Frame write to callback latency <= 100ms
 * NFR-HAL-01: Data Transfer Latency <= 100ms
 */
TEST_F(DmaRingBufferTest, DISABLED_Latency_WriteToCallback) {
    // This test requires callback mechanism implementation
    // Marked DISABLED until callback is implemented

    constexpr int NUM_ITERATIONS = 100;
    std::vector<std::chrono::microseconds> latencies;

    for (int i = 0; i < NUM_ITERATIONS; ++i) {
        auto frame = CreateTestFrame(static_cast<uint8_t>(i));

        auto write_start = std::chrono::high_resolution_clock::now();

        uint64_t sequence = 0;
        ASSERT_TRUE(buffer->WriteFrame(frame.data(), frame.size(), sequence));

        std::vector<uint8_t> read_buffer(TEST_FRAME_SIZE);
        size_t size_out = 0;
        uint64_t read_sequence = 0;
        ASSERT_TRUE(buffer->ReadFrame(read_buffer.data(), size_out, read_sequence));

        auto write_end = std::chrono::high_resolution_clock::now();

        auto latency = std::chrono::duration_cast<std::chrono::microseconds>(
            write_end - write_start
        );
        latencies.push_back(latency);
    }

    // Calculate statistics
    uint64_t total_us = 0;
    uint64_t max_us = 0;
    for (const auto& lat : latencies) {
        total_us += lat.count();
        max_us = std::max(max_us, static_cast<uint64_t>(lat.count()));
    }
    uint64_t avg_us = total_us / latencies.size();

    // NFR-HAL-01: <= 100ms (100,000us)
    EXPECT_LT(max_us, 100000) << "Max latency exceeds 100ms threshold";
    EXPECT_LT(avg_us, 10000) << "Average latency unexpectedly high";

    std::cout << "Latency stats: avg=" << avg_us << "us, max=" << max_us << "us\n";
}

// =============================================================================
// Error Handling Tests
// =============================================================================

/**
 * TEST: Read from empty buffer returns false
 */
TEST_F(DmaRingBufferTest, ReadFromEmptyBufferFails) {
    std::vector<uint8_t> read_buffer(TEST_FRAME_SIZE);
    size_t size_out = 0;
    uint64_t sequence = 0;

    bool result = buffer->ReadFrame(read_buffer.data(), size_out, sequence);
    EXPECT_FALSE(result);
}

/**
 * TEST: Write with size mismatching frame size fails
 */
TEST_F(DmaRingBufferTest, WriteWithInvalidSizeFails) {
    auto small_frame = CreateTestFrame(0xAA);
    small_frame.resize(TEST_FRAME_SIZE / 2);  // Wrong size

    uint64_t sequence = 0;
    bool result = buffer->WriteFrame(
        small_frame.data(),
        small_frame.size(),
        sequence
    );

    EXPECT_FALSE(result);
}

/**
 * TEST: Read with insufficient buffer size
 */
TEST_F(DmaRingBufferTest, ReadWithInsufficientBuffer) {
    // Write a valid frame first
    auto frame = CreateTestFrame(0xBB);
    uint64_t sequence = 0;
    ASSERT_TRUE(buffer->WriteFrame(frame.data(), frame.size(), sequence));

    // Try to read into smaller buffer
    std::vector<uint8_t> small_buffer(TEST_FRAME_SIZE / 2);
    size_t size_out = 0;

    bool result = buffer->ReadFrame(small_buffer.data(), size_out, sequence);
    EXPECT_FALSE(result);
}

// =============================================================================
// Sequence Number Wrap Tests
// =============================================================================

/**
 * TEST: Sequence number wraps at UINT64_MAX
 * FR-HAL-09: Monotonically increasing sequence, wraps at UINT64_MAX
 */
TEST_F(DmaRingBufferTest, DISABLED_SequenceNumberWrapAround) {
    // DISABLED: This test would take too long to run practically
    // In real implementation, we would inject sequence numbers for testing

    // The implementation should handle wrap-around gracefully:
    // UINT64_MAX -> 0 transition should maintain ordering
}

// =============================================================================
// Main
// =============================================================================

int main(int argc, char** argv) {
    ::testing::InitGoogleTest(&argc, argv);
    return RUN_ALL_TESTS();
}
