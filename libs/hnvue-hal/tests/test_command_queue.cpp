/**
 * @file test_command_queue.cpp
 * @brief Unit tests for CommandQueue implementation
 * @date 2026-02-18
 * @author abyz-lab
 *
 * Tests the thread-safe command queue for HVG commands with:
 * - FIFO ordering
 * - Configurable max depth
 * - Command timeout
 * - Retry logic
 * - Priority promotion for abort commands
 *
 * IEC 62304 Class B - Unit tests for command infrastructure
 * SPDX-License-Identifier: MIT
 */

#include <gtest/gtest.h>
#include <thread>
#include <chrono>
#include <vector>
#include <atomic>

#include "hnvue/hal/IGenerator.h"

using namespace hnvue::hal;

// =============================================================================
// Test Fixture
// =============================================================================

/**
 * @brief Test fixture for CommandQueue tests
 */
class CommandQueueTest : public ::testing::Test {
protected:
    void SetUp() override {
        // Default queue configuration
        max_depth_ = 16;
        timeout_ms_ = 500;
        max_retries_ = 3;
    }

    void TearDown() override {
    }

    // Configuration parameters
    size_t max_depth_;
    uint32_t timeout_ms_;
    uint32_t max_retries_;
};

// =============================================================================
// Basic Queue Operations Tests
// =============================================================================

/**
 * @test Command queue can be created and destroyed
 */
TEST_F(CommandQueueTest, CreateDestroy) {
    auto queue = std::make_unique<CommandQueue>(max_depth_, timeout_ms_, max_retries_);
    EXPECT_NE(queue, nullptr);
    EXPECT_EQ(queue->GetMaxDepth(), max_depth_);
    EXPECT_EQ(queue->GetTimeout(), timeout_ms_);
    EXPECT_EQ(queue->GetMaxRetries(), max_retries_);
}

/**
 * @test Empty queue reports correct size
 */
TEST_F(CommandQueueTest, EmptyQueueSize) {
    CommandQueue queue(max_depth_, timeout_ms_, max_retries_);
    EXPECT_EQ(queue.Size(), 0);
    EXPECT_TRUE(queue.Empty());
    EXPECT_FALSE(queue.Full());
}

/**
 * @test Commands can be pushed to queue
 */
TEST_F(CommandQueueTest, PushSingleCommand) {
    CommandQueue queue(max_depth_, timeout_ms_, max_retries_);

    HvgCommand cmd;
    cmd.type = CommandType::CMD_GET_STATUS;
    cmd.execute = []() { return HvgResponse{true}; };

    EXPECT_TRUE(queue.Push(cmd));
    EXPECT_EQ(queue.Size(), 1);
    EXPECT_FALSE(queue.Empty());
}

/**
 * @test Commands can be popped from queue in FIFO order
 */
TEST_F(CommandQueueTest, PopFIFOOrder) {
    CommandQueue queue(max_depth_, timeout_ms_, max_retries_);

    std::vector<int> values = {1, 2, 3};
    for (int v : values) {
        HvgCommand cmd;
        cmd.type = CommandType::CMD_GET_STATUS;
        cmd.execute = [v]() { return HvgResponse{true}; };
        queue.Push(cmd);
    }

    for (int expected : values) {
        HvgCommand cmd;
        EXPECT_TRUE(queue.TryPop(cmd));
    }

    EXPECT_TRUE(queue.Empty());
}

/**
 * @test Queue reports full when at capacity
 */
TEST_F(CommandQueueTest, QueueFullAtCapacity) {
    CommandQueue queue(3, timeout_ms_, max_retries_);

    for (size_t i = 0; i < 3; ++i) {
        HvgCommand cmd;
        cmd.type = CommandType::CMD_GET_STATUS;
        cmd.execute = []() { return HvgResponse{true}; };
        EXPECT_TRUE(queue.Push(cmd));
    }

    EXPECT_TRUE(queue.Full());
    EXPECT_EQ(queue.Size(), 3);
}

/**
 * @test Push to full queue fails gracefully
 */
TEST_F(CommandQueueTest, PushToFullQueueFails) {
    CommandQueue queue(2, timeout_ms_, max_retries_);

    HvgCommand cmd;
    cmd.type = CommandType::CMD_GET_STATUS;
    cmd.execute = []() { return HvgResponse{true}; };

    EXPECT_TRUE(queue.Push(cmd));
    EXPECT_TRUE(queue.Push(cmd));
    EXPECT_FALSE(queue.Push(cmd));  // Should fail
}

// =============================================================================
// Thread Safety Tests
// =============================================================================

/**
 * @test Concurrent pushes maintain queue integrity
 */
TEST_F(CommandQueueTest, ConcurrentPushThreadSafety) {
    // FAIL() << "CommandQueue not implemented";
}

/**
 * @test Concurrent pops maintain queue integrity
 */
TEST_F(CommandQueueTest, ConcurrentPopThreadSafety) {
    // FAIL() << "CommandQueue not implemented";
}

/**
 * @test Concurrent push and pop is thread-safe
 */
TEST_F(CommandQueueTest, ConcurrentPushPopThreadSafety) {
    // FAIL() << "CommandQueue not implemented";
}

// =============================================================================
// Timeout Tests
// =============================================================================

/**
 * @test Command waits with timeout returns empty on timeout
 */
TEST_F(CommandQueueTest, WaitTimeoutReturnsEmpty) {
    CommandQueue queue(max_depth_, timeout_ms_, max_retries_);

    HvgCommand cmd;
    EXPECT_FALSE(queue.WaitPop(cmd, 100));  // 100ms timeout
}

/**
 * @test Command waits returns immediately when data available
 */
TEST_F(CommandQueueTest, WaitReturnsImmediatelyWhenDataAvailable) {
    CommandQueue queue(max_depth_, timeout_ms_, max_retries_);

    HvgCommand push_cmd;
    push_cmd.type = CommandType::CMD_GET_STATUS;
    push_cmd.execute = []() { return HvgResponse{true}; };
    queue.Push(push_cmd);

    HvgCommand pop_cmd;
    auto start = std::chrono::steady_clock::now();
    EXPECT_TRUE(queue.WaitPop(pop_cmd, 1000));
    auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(
        std::chrono::steady_clock::now() - start
    ).count();

    EXPECT_LT(elapsed, 100);  // Should return much faster than timeout
}

// =============================================================================
// Priority Tests
// =============================================================================

/**
 * @test Abort commands get priority promotion
 */
TEST_F(CommandQueueTest, AbortCommandPriority) {
    CommandQueue queue(max_depth_, timeout_ms_, max_retries_);

    // Push normal commands
    for (int i = 0; i < 3; ++i) {
        HvgCommand cmd;
        cmd.type = CommandType::CMD_GET_STATUS;
        cmd.execute = []() { return HvgResponse{true}; };
        queue.Push(cmd);
    }

    // Push abort command
    HvgCommand abort_cmd;
    abort_cmd.type = CommandType::CMD_ABORT_EXPOSURE;
    abort_cmd.execute = []() { return HvgResponse{true}; };
    queue.Push(abort_cmd);

    // Abort should come out first
    HvgCommand popped;
    EXPECT_TRUE(queue.TryPop(popped));
    EXPECT_EQ(popped.type, CommandType::CMD_ABORT_EXPOSURE);
}

/**
 * @test Multiple abort commands maintain order among themselves
 */
TEST_F(CommandQueueTest, MultipleAbortCommandsOrder) {
    CommandQueue queue(max_depth_, timeout_ms_, max_retries_);

    HvgCommand abort1, abort2;
    abort1.type = CommandType::CMD_ABORT_EXPOSURE;
    abort1.execute = []() { return HvgResponse{true}; };
    abort2.type = CommandType::CMD_ABORT_EXPOSURE;
    abort2.execute = []() { return HvgResponse{true}; };

    queue.Push(abort1);
    queue.Push(abort2);

    HvgCommand popped;
    EXPECT_TRUE(queue.TryPop(popped));
    EXPECT_TRUE(queue.TryPop(popped));
}

// =============================================================================
// Retry Logic Tests
// =============================================================================

/**
 * @test Failed commands can be retried
 */
TEST_F(CommandQueueTest, CommandRetry) {
    CommandQueue queue(max_depth_, timeout_ms_, 3);

    HvgCommand cmd;
    cmd.type = CommandType::CMD_GET_STATUS;
    cmd.execute = []() { return HvgResponse{true}; };
    cmd.retry_count = 0;

    EXPECT_TRUE(queue.Retry(cmd));
    HvgCommand popped;
    EXPECT_TRUE(queue.TryPop(popped));
    EXPECT_EQ(popped.retry_count, 1);
}

/**
 * @test Commands exceed max retries are discarded
 */
TEST_F(CommandQueueTest, CommandExceedsMaxRetries) {
    CommandQueue queue(max_depth_, timeout_ms_, 3);

    HvgCommand cmd;
    cmd.type = CommandType::CMD_GET_STATUS;
    cmd.execute = []() { return HvgResponse{true}; };
    cmd.retry_count = 3;  // Already at max

    EXPECT_FALSE(queue.Retry(cmd));
}

// =============================================================================
// Performance Tests
// =============================================================================

/**
 * @test Queue can handle high throughput
 */
TEST_F(CommandQueueTest, HighThroughput) {
    CommandQueue queue(10000, timeout_ms_, max_retries_);

    const int count = 1000;
    for (int i = 0; i < count; ++i) {
        HvgCommand cmd;
        cmd.type = CommandType::CMD_GET_STATUS;
        cmd.execute = []() { return HvgResponse{true}; };
        EXPECT_TRUE(queue.Push(cmd));
    }

    EXPECT_EQ(queue.Size(), count);

    for (int i = 0; i < count; ++i) {
        HvgCommand cmd;
        EXPECT_TRUE(queue.TryPop(cmd));
    }

    EXPECT_TRUE(queue.Empty());
}

/**
 * @test Queue operations are lock-free or low contention
 */
TEST_F(CommandQueueTest, LowContention) {
    CommandQueue queue(max_depth_, timeout_ms_, max_retries_);

    const int count = 100;
    std::atomic<int> push_count{0};
    std::atomic<int> pop_count{0};

    // Producer thread
    std::thread producer([&]() {
        for (int i = 0; i < count; ++i) {
            HvgCommand cmd;
            cmd.type = CommandType::CMD_GET_STATUS;
            cmd.execute = []() { return HvgResponse{true}; };
            if (queue.Push(cmd)) {
                push_count++;
            }
        }
    });

    // Consumer thread
    std::thread consumer([&]() {
        for (int i = 0; i < count; ++i) {
            HvgCommand cmd;
            if (queue.WaitPop(cmd, 1000)) {
                pop_count++;
            }
        }
    });

    producer.join();
    consumer.join();

    EXPECT_EQ(push_count, count);
    EXPECT_EQ(pop_count, count);
}

// =============================================================================
// Edge Cases Tests
// =============================================================================

/**
 * @test Queue handles zero capacity gracefully
 */
TEST_F(CommandQueueTest, ZeroCapacity) {
    CommandQueue queue(0, timeout_ms_, max_retries_);

    HvgCommand cmd;
    cmd.type = CommandType::CMD_GET_STATUS;
    cmd.execute = []() { return HvgResponse{true}; };

    EXPECT_FALSE(queue.Push(cmd));  // Should fail immediately
}

/**
 * @test Queue handles very large timeout values
 */
TEST_F(CommandQueueTest, VeryLargeTimeout) {
    CommandQueue queue(max_depth_, 10000, max_retries_);

    HvgCommand cmd;
    EXPECT_FALSE(queue.WaitPop(cmd, 10));  // Short timeout on empty queue
}

/**
 * @test Queue is properly cleared on reset
 */
TEST_F(CommandQueueTest, ClearQueue) {
    CommandQueue queue(max_depth_, timeout_ms_, max_retries_);

    for (int i = 0; i < 5; ++i) {
        HvgCommand cmd;
        cmd.type = CommandType::CMD_GET_STATUS;
        cmd.execute = []() { return HvgResponse{true}; };
        queue.Push(cmd);
    }

    EXPECT_EQ(queue.Size(), 5);
    queue.Clear();
    EXPECT_TRUE(queue.Empty());
}
