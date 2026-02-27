/**
 * @file ImageServiceImpl.cpp
 * @brief Implementation of ImageService (Core Engine -> GUI image streaming)
 * SPEC-IPC-001 Section 4.2.3: ImageService with server-streaming
 */

#include "hnvue/ipc/ImageServiceImpl.h"

namespace hnvue::ipc {

ImageServiceImpl::ImageServiceImpl(
    std::shared_ptr<spdlog::logger> logger,
    size_t chunk_size_bytes)
    : logger_(logger)
    , chunk_size_bytes_(chunk_size_bytes) {
    logger_->info("ImageServiceImpl initialized (chunk_size: {} bytes)", chunk_size_bytes_);
}

grpc::Status ImageServiceImpl::SubscribeImageStream(
    grpc::ServerContext* context,
    const ImageStreamRequest* request,
    grpc::ServerWriter<ImageChunk>* writer) {

    uint64_t filter_id = request->acquisition_id_filter();
    auto mode = request->preferred_mode();

    logger_->info("SubscribeImageStream: filter_id={}, mode={}",
                  filter_id, static_cast<int>(mode));

    // Stream images that match the filter
    // This is a blocking call that streams images as they become available

    std::unique_lock<std::mutex> lock(queue_mutex_);

    while (!context->IsCancelled()) {
        // Wait for images to arrive in queue
        queue_cv_.wait(lock, [this, &context] {
            return !image_queue_.empty() || context->IsCancelled();
        });

        if (context->IsCancelled()) {
            logger_->debug("SubscribeImageStream: client disconnected");
            break;
        }

        // Process queued images
        while (!image_queue_.empty()) {
            ImageBuffer buffer = std::move(image_queue_.front());
            image_queue_.pop();

            // Apply filter
            if (filter_id != 0 && buffer.acquisition_id != filter_id) {
                continue;  // Skip if not matching filter
            }

            // Apply mode preference
            if (mode != hnvue::ipc::protobuf::IMAGE_TRANSFER_MODE_UNSPECIFIED &&
                mode != hnvue::ipc::protobuf::IMAGE_TRANSFER_MODE_PREVIEW &&
                mode != buffer.transfer_mode) {
                // Downsample if requesting preview but have full quality
                if (mode == hnvue::ipc::protobuf::IMAGE_TRANSFER_MODE_PREVIEW &&
                    buffer.transfer_mode == hnvue::ipc::protobuf::IMAGE_TRANSFER_MODE_FULL_QUALITY) {
                    buffer = DownsampleImage(buffer, 4);  // Downsample by 4x
                }
            }

            // Release lock during streaming to avoid blocking queue
            lock.unlock();

            // Stream the image
            bool success = StreamImageChunks(buffer, writer);

            lock.lock();

            if (!success) {
                logger_->warn("SubscribeImageStream: error streaming acquisition_id={}",
                             buffer.acquisition_id);
                // Continue to next image
            }
        }
    }

    logger_->info("SubscribeImageStream: ending stream");
    return grpc::Status::OK;
}

void ImageServiceImpl::QueueImage(const ImageBuffer& buffer) {
    if (!buffer.is_valid) {
        logger_->warn("QueueImage: invalid buffer ignored");
        return;
    }

    {
        std::lock_guard<std::mutex> lock(queue_mutex_);
        image_queue_.push(buffer);
    }

    queue_cv_.notify_one();
    logger_->debug("QueueImage: acquisition_id={}, size={}x{} queued",
                   buffer.acquisition_id, buffer.width, buffer.height);
}

size_t ImageServiceImpl::GetQueueSize() const {
    std::lock_guard<std::mutex> lock(queue_mutex_);
    return image_queue_.size();
}

void ImageServiceImpl::ClearQueue() {
    std::lock_guard<std::mutex> lock(queue_mutex_);
    while (!image_queue_.empty()) {
        image_queue_.pop();
    }
    logger_->info("ClearQueue: all images cleared");
}

bool ImageServiceImpl::StreamImageChunks(
    const ImageBuffer& buffer,
    grpc::ServerWriter<ImageChunk>* writer) {

    uint32_t chunk_count = CalculateChunkCount(buffer);
    logger_->debug("StreamImageChunks: acquisition_id={}, total_chunks={}",
                   buffer.acquisition_id, chunk_count);

    // Send metadata chunk first
    ImageChunk metadata_chunk;
    CreateMetadataChunk(buffer, &metadata_chunk);
    if (!writer->Write(metadata_chunk)) {
        logger_->warn("StreamImageChunks: failed to write metadata chunk");
        return false;
    }

    // Send pixel data chunks
    size_t pixel_offset = 0;
    size_t total_pixels = buffer.width * buffer.height;
    size_t bytes_per_pixel = buffer.bits_per_pixel / 8;

    for (uint32_t chunk_num = 0; chunk_num < chunk_count; ++chunk_num) {
        ImageChunk chunk;
        bool is_last = CreatePixelDataChunk(buffer, chunk_num, chunk_count, &chunk);

        if (!writer->Write(chunk)) {
            logger_->warn("StreamImageChunks: failed to write chunk {}", chunk_num);
            return false;
        }

        if (is_last) {
            break;
        }
    }

    logger_->debug("StreamImageChunks: acquisition_id={} completed",
                   buffer.acquisition_id);
    return true;
}

void ImageServiceImpl::CreateMetadataChunk(
    const ImageBuffer& buffer,
    ImageChunk* chunk) const {

    chunk->set_acquisition_id(buffer.acquisition_id);
    chunk->set_sequence_number(0);
    chunk->set_is_last_chunk(false);

    // Create metadata
    auto* metadata = chunk->mutable_metadata();
    metadata->set_width_pixels(buffer.width);
    metadata->set_height_pixels(buffer.height);
    metadata->set_bits_per_pixel(buffer.bits_per_pixel);
    metadata->set_pixel_pitch_mm(buffer.pixel_pitch_mm);
    metadata->set_transfer_mode(buffer.transfer_mode);
    metadata->mutable_acquisition_timestamp()->set_microseconds_since_start(0);
    metadata->set_kv_actual(buffer.kv_actual);
    metadata->set_mas_actual(buffer.mas_actual);
    metadata->set_detector_id(buffer.detector_id);
}

bool ImageServiceImpl::CreatePixelDataChunk(
    const ImageBuffer& buffer,
    uint32_t chunk_number,
    uint32_t total_chunks,
    ImageChunk* chunk) const {

    chunk->set_acquisition_id(buffer.acquisition_id);
    chunk->set_sequence_number(chunk_number);
    chunk->set_total_chunks(total_chunks);

    // Calculate chunk data range
    size_t total_bytes = buffer.pixel_data.size();
    size_t bytes_per_chunk = (total_bytes + total_chunks - 1) / total_chunks;  // Round up
    size_t offset = chunk_number * bytes_per_chunk;
    size_t chunk_bytes = std::min(bytes_per_chunk, total_bytes - offset);

    // Set pixel data
    chunk->set_pixel_data(buffer.pixel_data.data() + offset, chunk_bytes);

    // Mark last chunk
    bool is_last = (chunk_number == total_chunks - 1);
    chunk->set_is_last_chunk(is_last);

    return is_last;
}

uint32_t ImageServiceImpl::CalculateChunkCount(const ImageBuffer& buffer) const {
    size_t total_bytes = buffer.pixel_data.size();
    uint32_t chunk_count = static_cast<uint32_t>((total_bytes + chunk_size_bytes_ - 1) /
                                                  chunk_size_bytes_);
    return std::max(1u, chunk_count);
}

void ImageServiceImpl::CreateErrorChunk(
    uint64_t acquisition_id,
    const std::string& error_message,
    ImageChunk* chunk) const {

    chunk->set_acquisition_id(acquisition_id);
    chunk->set_is_last_chunk(true);
    chunk->set_sequence_number(0);

    auto* error = chunk->mutable_error();
    error->set_code(ErrorCode::ERROR_CODE_INTERNAL);
    error->set_message(error_message);
}

ImageBuffer ImageServiceImpl::DownsampleImage(const ImageBuffer& buffer, int scale_factor) const {
    // TODO: Implement actual downsampling
    // For now, return a copy with modified metadata
    ImageBuffer downsampled = buffer;

    downsampled.width = buffer.width / scale_factor;
    downsampled.height = buffer.height / scale_factor;
    downsampled.transfer_mode = ImageTransferMode::IMAGE_TRANSFER_MODE_PREVIEW;

    // TODO: Perform actual pixel downsampling (e.g., using OpenCV)

    return downsampled;
}

} // namespace hnvue::ipc
