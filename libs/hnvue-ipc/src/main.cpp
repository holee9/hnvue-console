/**
 * @file main.cpp
 * @brief Standalone IPC server executable for integration testing
 * SPEC-IPC-001: Standalone server for integration testing
 *
 * This executable provides a standalone gRPC server that can be used
 * for integration testing with the C# client.
 *
 * Usage:
 *   hnvue-ipc-server [--port=PORT] [--verbose]
 *
 * Default port: 50051
 */

#include "hnvue/ipc/IpcServer.h"
#include <spdlog/spdlog.h>
#include <spdlog/sinks/stdout_color_sinks.h>
#include <iostream>
#include <string>
#include <csignal>
#include <cstdlib>

namespace {

// Global server pointer for signal handler
hnvue::ipc::IpcServer* g_server = nullptr;
std::atomic<bool> g_shutdown_requested{false};

/**
 * @brief Signal handler for graceful shutdown
 */
void SignalHandler(int signal)
{
    if (g_server != nullptr && !g_shutdown_requested)
    {
        spdlog::info("Received signal {}, shutting down server...", signal);
        g_shutdown_requested = true;

        // Stop the server (will be called from main thread)
        // Note: This is not thread-safe for production use but acceptable for test server
    }
}

/**
 * @brief Print usage information
 */
void PrintUsage(const char* program_name)
{
    std::cout << "Usage: " << program_name << " [OPTIONS]\n"
              << "\n"
              << "Options:\n"
              << "  --port=PORT       Server port (default: 50051)\n"
              << "  --verbose         Enable verbose logging\n"
              << "  --help            Show this help message\n"
              << "\n"
              << "Example:\n"
              << "  " << program_name << " --port=50051 --verbose\n"
              << std::endl;
}

/**
 * @brief Parse command line arguments
 */
struct CommandLineArgs
{
    std::string server_address = "localhost:50051";
    bool verbose = false;
    bool show_help = false;

    static CommandLineArgs Parse(int argc, char* argv[])
    {
        CommandLineArgs args;

        for (int i = 1; i < argc; ++i)
        {
            std::string arg = argv[i];

            if (arg == "--help" || arg == "-h")
            {
                args.show_help = true;
                return args;
            }

            if (arg == "--verbose" || arg == "-v")
            {
                args.verbose = true;
                continue;
            }

            if (arg.find("--port=") == 0)
            {
                std::string port = arg.substr(7); // Skip "--port="
                int port_num = std::atoi(port.c_str());
                if (port_num > 0 && port_num < 65536)
                {
                    args.server_address = "localhost:" + std::to_string(port_num);
                }
                else
                {
                    std::cerr << "Error: Invalid port number: " << port << std::endl;
                    args.show_help = true;
                }
                continue;
            }

            // Unknown argument
            std::cerr << "Error: Unknown argument: " << arg << std::endl;
            args.show_help = true;
        }

        return args;
    }
};

} // anonymous namespace

/**
 * @brief Main entry point
 */
int main(int argc, char* argv[])
{
    // Parse command line arguments
    auto args = CommandLineArgs::Parse(argc, argv);

    if (args.show_help)
    {
        PrintUsage(argv[0]);
        return args.show_help && !args.server_address.empty() ? 1 : 0;
    }

    // Setup logging
    auto console_sink = std::make_shared<spdlog::sinks::stdout_color_sink_mt>();
    console_sink->set_level(args.verbose ? spdlog::level::debug : spdlog::level::info);
    console_sink->set_pattern("[%Y-%m-%d %H:%M:%S.%e] [%^%l%$] %v");

    auto logger = std::make_shared<spdlog::logger>("hnvue-ipc-server", spdlog::sinks_init_list{console_sink});
    logger->set_level(args.verbose ? spdlog::level::debug : spdlog::level::info);
    spdlog::register_logger(logger);
    spdlog::set_default_logger(logger);

    logger->info("HnVue IPC Server (Integration Test)");
    logger->info("Version {}.{}.{}",
        hnvue::ipc::IpcServer::kInterfaceVersionMajor,
        hnvue::ipc::IpcServer::kInterfaceVersionMinor,
        hnvue::ipc::IpcServer::kInterfaceVersionPatch);

    // Setup signal handlers for graceful shutdown
    std::signal(SIGINT, SignalHandler);
    std::signal(SIGTERM, SignalHandler);

#ifdef _WIN32
    std::signal(SIGBREAK, SignalHandler);
#endif

    // Create and start server
    hnvue::ipc::IpcServer server(args.server_address, logger);
    g_server = &server;

    if (!server.Start())
    {
        logger->error("Failed to start server");
        return 1;
    }

    logger->info("Server started successfully on {}", args.server_address);
    logger->info("Press Ctrl+C to stop");

    // Wait for shutdown signal
    while (!g_shutdown_requested)
    {
        std::this_thread::sleep_for(std::chrono::milliseconds(100));
    }

    // Stop server gracefully
    logger->info("Stopping server...");
    server.Stop(5000); // 5 second timeout

    g_server = nullptr;
    logger->info("Server stopped");

    return 0;
}
