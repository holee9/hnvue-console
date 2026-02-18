/**
 * @file ImageServiceImpl.h
 * @brief Implementation of ImageService (Core Engine -> GUI image streaming)
 * SPEC-IPC-001 Section 4.2.3: ImageService with server-streaming
 *
 * This service handles streaming of 16-bit grayscale X-ray images:
 * - Splits large images into chunks for streaming
 * - Supports PREVIEW and FULL_QUALITY transfer modes
 * - Sends metadata in first chunk
 * - Handles transfer errors with error chunks
 */

#ifndef HNVE_IPC_IMAGE_SERVICE_IMPL_H
#define HNVE_IPC_IMAGE_SERVICE_IMPL_H

#include <grpcpp/grpcpp.h>
#include <memory>
#include <mutex>
#include <unordered_map>
#include <queue>
#include <condition_variable>
#include <spdlog/spdlog.h>

// Generated protobuf headers
#include "hnvue_image.grpc.pb.h"
#include "hnvue_image.pb.h"

namespace hnvue::ipc {

using hnvue::ipc::protobuf::ImageService;
using hnvue::ipc::protobuf::ImageStreamRequest;
using hnvue::ipc::protobuf::ImageChunk;
using hnvue::ipc::protobuf::ImageMetadata;
using hnvue::ipc::protobuf::ImageTransferMode;

/**
 * @struct ImageBuffer
 * @brief Internal representation of an acquired image
 */
struct ImageBuffer {
    uint64_t acquisition_id;
    uint32_t width;
    uint32_t height;
    uint32_t bits_per_pixel;
    float pixel_pitch_mm;
    float kv_actual;
    float mas_actual;
    uint32_t detector_id;
    std::vector<uint8_t> pixel_data;  // Raw 16-bit grayscale pixels
    ImageTransferMode transfer_mode;
    bool is_valid;

    ImageBuffer() : acquisition_id(0), width(0), height(0), bits_per_pixel(16),
                    pixel_pitch_mm(0.0f), kv_actual(0.0f), mas_actual(0.0f),
                    detector_id(0), transfer_mode(ImageTransferMode::IMAGE_TRANSFER_MODE_UNSPECIFIED),
                    is_valid(false) {}
};

/**
 * @class ImageServiceImpl
 * @brief gRPC service implementation for image streaming
 *
 * Thread safety: Manages internal image buffer queue with mutex protection.
 *
 * SPEC-IPC-001 Section 4.2.3:
 * - Server-streaming RPC for chunk delivery
 * - First chunk contains ImageMetadata
 * - Chunks have sequential sequence_number
 * - Last chunk has is_last_chunk == true
 * - Error chunks have non-zero error field
 *
 * NFR-IPC-001: Full 9MP image transfer < 50ms
 */
class ImageServiceImpl final : public ImageService::Service {
public:
    /**
     * @brief Construct ImageService implementation
     * @param logger Logger instance
     * @param chunk_size_bytes Target chunk size (default: 256KB)
     */
    explicit ImageServiceImpl(
        std::shared_ptr<spdlog::logger> logger = spdlog::default_logger(),
        size_t chunk_size_bytes = 256 * 1024
    );

    ~ImageServiceImpl() override = default;

    // Non-copyable, non-movable
    ImageServiceImpl(const ImageServiceImpl&) = delete;
    ImageServiceImpl& operator=(const ImageServiceImpl&) = delete;

    /**
     * @brief Stream image data for an acquisition
     *
     * This is a server-streaming RPC. The Core Engine pushes chunks
     * to the GUI as they become available.
     *
     * SPEC-IPC-001 Section 4.2.3:
     * - acquisition_id_filter == 0 means subscribe to all
     * - Chunk streaming until is_last_chunk == true
     * - First chunk contains metadata
     *
     * @param context gRPC server context (supports cancellation)
     * @param request Subscription filter and preferred mode
     * @param writer Server writer for streaming chunks
     * @return gRPC status code
     */
    grpc::Status SubscribeImageStream(
        grpc::ServerContext* context,
        const ImageStreamRequest* request,
        grpc::ServerWriter<ImageChunk>* writer) override;

    /**
     * @brief Add an image to the streaming queue
     *
     * Called by the acquisition subsystem when an image is ready.
     *
     * @param buffer Image data to stream
     */
    void QueueImage(const ImageBuffer& buffer);

    /**
     * @brief Get the number of images waiting to be streamed
     * @return Queue size
     */
    size_t GetQueueSize() const;

    /**
     * @brief Clear all queued images (e.g., on shutdown)
     */
    void ClearQueue();

private:
    std::shared_ptr<spdlog::logger> logger_;
    size_t chunk_size_bytes_;

    // Image buffer queue (thread-safe)
    mutable std::mutex queue_mutex_;
    std::condition_variable queue_cv_;
    std::queue<ImageBuffer> image_queue_;

    /**
     * @brief Split image into chunks for streaming
     * @param buffer Image to chunk
     * @param writer Output writer
     * @return true if all chunks sent successfully, false on error
     */
    bool StreamImageChunks(
        const ImageBuffer& buffer,
        grpc::ServerWriter<ImageChunk>* writer);

    /**
     * @brief Create metadata chunk (first chunk)
     * @param buffer Source image buffer
     * @param chunk Output chunk to populate
     */
    void CreateMetadataChunk(
        const ImageBuffer& buffer,
        ImageChunk* chunk) const;

    /**
     * @brief Create pixel data chunk
     * @param buffer Source image buffer
     * @param chunk_number Which chunk this is (0-indexed)
     * @param total_chunks Total number of chunks
     * @param chunk Output chunk to populate
     * @return true if this is the last chunk, false otherwise
     */
    bool CreatePixelDataChunk(
        const ImageBuffer& buffer,
        uint32_t chunk_number,
        uint32_t total_chunks,
        ImageChunk* chunk) const;

    /**
     * @brief Calculate number of chunks for an image
     * @param buffer Image to calculate for
     * @return Number of chunks needed
     */
    uint32_t CalculateChunkCount(const ImageBuffer& buffer) const;

    /**
     * @brief Create error chunk for failed transfer
     * @param acquisition_id Associated acquisition
     * @param error_message Error description
     * @param chunk Output chunk to populate
     */
    void CreateErrorChunk(
        uint64_t acquisition_id,
        const std::string& error_message,
        ImageChunk* chunk) const;

    /**
     * @brief Downsample image for preview mode
     * @param buffer Original image
     * @param scale_factor Downsampling factor (e.g., 4 for 1/4 resolution)
     * @return Downsampled image buffer
     */
    ImageBuffer DownsampleImage(const ImageBuffer& buffer, int scale_factor) const;
};

} // namespace hnvue::ipc

#endif // HNVE_IPC_IMAGE_SERVICE_IMPL_H
