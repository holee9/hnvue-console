/**
 * @file test_directory_structure.cpp
 * @brief Specification tests for HnVue project repository structure
 * @date 2026-02-18
 */

#include <gtest/gtest.h>
#include <filesystem>
#include <fstream>

namespace fs = std::filesystem;

/**
 * @class DirectoryStructureTest
 * @brief Test suite verifying the canonical repository structure defined in SPEC-INFRA-001
 */
class DirectoryStructureTest : public ::testing::Test {
protected:
    fs::path project_root;

    void SetUp() override {
        // Determine project root from current executable path
        project_root = fs::current_path().parent_path().parent_path().parent_path();
    }

    /**
     * @brief Helper to check if a directory exists
     */
    bool dir_exists(const fs::path& path) {
        return fs::exists(path) && fs::is_directory(path);
    }

    /**
     * @brief Helper to check if a file exists
     */
    bool file_exists(const fs::path& path) {
        return fs::exists(path) && fs::is_regular_file(path);
    }
};

/**
 * @test Top-level configuration files exist
 * @description Verify all required root-level files are present
 */
TEST_F(DirectoryStructureTest, TopLevelConfigurationFilesExist) {
    // C++ build system files
    EXPECT_TRUE(file_exists(project_root / "CMakeLists.txt"))
        << "CMakeLists.txt must exist at repository root";
    EXPECT_TRUE(file_exists(project_root / "CMakePresets.json"))
        << "CMakePresets.json must exist for standardized builds";
    EXPECT_TRUE(file_exists(project_root / "vcpkg.json"))
        << "vcpkg.json must exist for C++ dependency management";

    // C# build system files
    EXPECT_TRUE(file_exists(project_root / "HnVue.sln"))
        << "HnVue.sln must exist as the C# solution file";
    EXPECT_TRUE(file_exists(project_root / "Directory.Build.props"))
        << "Directory.Build.props must exist for MSBuild shared properties";
    EXPECT_TRUE(file_exists(project_root / "Directory.Packages.props"))
        << "Directory.Packages.props must exist for central NuGet package management";

    // Version control
    EXPECT_TRUE(file_exists(project_root / ".gitignore"))
        << ".gitignore must be configured for the hybrid C++/C# project";
}

/**
 * @test Source directories exist
 * @description Verify all required source directories are present
 */
TEST_F(DirectoryStructureTest, SourceDirectoriesExist) {
    // Protobuf definitions
    EXPECT_TRUE(dir_exists(project_root / "proto"))
        << "proto/ directory must exist for shared protobuf definitions";

    // C++ libraries
    EXPECT_TRUE(dir_exists(project_root / "libs"))
        << "libs/ directory must exist for C++ libraries";
    EXPECT_TRUE(dir_exists(project_root / "libs" / "hnvue-infra"))
        << "libs/hnvue-infra/ must exist for infrastructure utilities";
    EXPECT_TRUE(dir_exists(project_root / "libs" / "hnvue-hal"))
        << "libs/hnvue-hal/ must exist for hardware abstraction layer";
    EXPECT_TRUE(dir_exists(project_root / "libs" / "hnvue-ipc"))
        << "libs/hnvue-ipc/ must exist for IPC server";
    EXPECT_TRUE(dir_exists(project_root / "libs" / "hnvue-imaging"))
        << "libs/hnvue-imaging/ must exist for image processing";

    // C# projects
    EXPECT_TRUE(dir_exists(project_root / "src"))
        << "src/ directory must exist for C# projects";
    EXPECT_TRUE(dir_exists(project_root / "src" / "HnVue.Ipc.Client"))
        << "src/HnVue.Ipc.Client/ must exist for gRPC client";
    EXPECT_TRUE(dir_exists(project_root / "src" / "HnVue.Dicom"))
        << "src/HnVue.Dicom/ must exist for DICOM services";
    EXPECT_TRUE(dir_exists(project_root / "src" / "HnVue.Dose"))
        << "src/HnVue.Dose/ must exist for dose management";
    EXPECT_TRUE(dir_exists(project_root / "src" / "HnVue.Workflow"))
        << "src/HnVue.Workflow/ must exist for acquisition workflow";
    EXPECT_TRUE(dir_exists(project_root / "src" / "HnVue.Console"))
        << "src/HnVue.Console/ must exist for WPF application entry point";
}

/**
 * @test Test directories exist
 * @description Verify all required test directories are present
 */
TEST_F(DirectoryStructureTest, TestDirectoriesExist) {
    EXPECT_TRUE(dir_exists(project_root / "tests"))
        << "tests/ directory must exist";

    EXPECT_TRUE(dir_exists(project_root / "tests" / "cpp"))
        << "tests/cpp/ must exist for C++ unit tests";
    EXPECT_TRUE(dir_exists(project_root / "tests" / "cpp" / "hnvue-infra.Tests"))
        << "tests/cpp/hnvue-infra.Tests/ must exist";
    EXPECT_TRUE(dir_exists(project_root / "tests" / "cpp" / "hnvue-hal.Tests"))
        << "tests/cpp/hnvue-hal.Tests/ must exist";
    EXPECT_TRUE(dir_exists(project_root / "tests" / "cpp" / "hnvue-ipc.Tests"))
        << "tests/cpp/hnvue-ipc.Tests/ must exist";
    EXPECT_TRUE(dir_exists(project_root / "tests" / "cpp" / "hnvue-imaging.Tests"))
        << "tests/cpp/hnvue-imaging.Tests/ must exist";

    EXPECT_TRUE(dir_exists(project_root / "tests" / "csharp"))
        << "tests/csharp/ must exist for C# unit tests";
    EXPECT_TRUE(dir_exists(project_root / "tests" / "csharp" / "HnVue.Dicom.Tests"))
        << "tests/csharp/HnVue.Dicom.Tests/ must exist";
    EXPECT_TRUE(dir_exists(project_root / "tests" / "csharp" / "HnVue.Dose.Tests"))
        << "tests/csharp/HnVue.Dose.Tests/ must exist";
    EXPECT_TRUE(dir_exists(project_root / "tests" / "csharp" / "HnVue.Workflow.Tests"))
        << "tests/csharp/HnVue.Workflow.Tests/ must exist";
    EXPECT_TRUE(dir_exists(project_root / "tests" / "csharp" / "HnVue.Ipc.Client.Tests"))
        << "tests/csharp/HnVue.Ipc.Client.Tests/ must exist";

    EXPECT_TRUE(dir_exists(project_root / "tests" / "integration"))
        << "tests/integration/ must exist for integration tests";
    EXPECT_TRUE(dir_exists(project_root / "tests" / "docker"))
        << "tests/docker/ must exist for Docker test configurations";
    EXPECT_TRUE(dir_exists(project_root / "tests" / "fixtures"))
        << "tests/fixtures/ must exist for test data";
    EXPECT_TRUE(dir_exists(project_root / "tests" / "fixtures" / "dicom"))
        << "tests/fixtures/dicom/ must exist for DICOM test datasets";
}

/**
 * @test Documentation and script directories exist
 * @description Verify documentation and script directories are present
 */
TEST_F(DirectoryStructureTest, DocumentationAndScriptDirectoriesExist) {
    EXPECT_TRUE(dir_exists(project_root / "docs"))
        << "docs/ directory must exist for project documentation";
    EXPECT_TRUE(dir_exists(project_root / "docs" / "architecture"))
        << "docs/architecture/ must exist for architecture documentation";
    EXPECT_TRUE(dir_exists(project_root / "docs" / "api"))
        << "docs/api/ must exist for API documentation";

    EXPECT_TRUE(dir_exists(project_root / "scripts"))
        << "scripts/ directory must exist for build and automation scripts";
}

/**
 * @test CI/CD configuration exists
 * @description Verify Gitea Actions workflows are configured
 */
TEST_F(DirectoryStructureTest, CIConfigurationExists) {
    EXPECT_TRUE(dir_exists(project_root / ".gitea"))
        << ".gitea/ directory must exist for Gitea-specific configuration";
    EXPECT_TRUE(dir_exists(project_root / ".gitea" / "workflows"))
        << ".gitea/workflows/ must exist for CI/CD pipeline definitions";
    EXPECT_TRUE(file_exists(project_root / ".gitea" / "workflows" / "ci.yml"))
        << ".gitea/workflows/ci.yml must exist for main CI pipeline";
    EXPECT_TRUE(file_exists(project_root / ".gitea" / "workflows" / "release.yml"))
        << ".gitea/workflows/release.yml must exist for release pipeline";
}

/**
 * @test C++ library module structure
 * @description Verify each C++ library has the required subdirectories
 */
TEST_F(DirectoryStructureTest, CppLibraryModuleStructure) {
    std::vector<std::string> libraries = {
        "hnvue-infra", "hnvue-hal", "hnvue-ipc", "hnvue-imaging"
    };

    for (const auto& lib : libraries) {
        fs::path lib_path = project_root / "libs" / lib;
        EXPECT_TRUE(dir_exists(lib_path / "include"))
            << "libs/" << lib << "/include/ must exist for public headers";
        EXPECT_TRUE(dir_exists(lib_path / "src"))
            << "libs/" << lib << "/src/ must exist for implementation files";
        EXPECT_TRUE(file_exists(lib_path / "CMakeLists.txt"))
            << "libs/" << lib << "/CMakeLists.txt must exist";
    }
}

/**
 * @test C# project structure
 * @description Verify each C# project has the required .csproj file
 */
TEST_F(DirectoryStructureTest, CSharpProjectStructure) {
    std::vector<std::string> projects = {
        "HnVue.Ipc.Client", "HnVue.Dicom", "HnVue.Dose",
        "HnVue.Workflow", "HnVue.Console"
    };

    for (const auto& project : projects) {
        fs::path project_path = project_root / "src" / project;
        EXPECT_TRUE(file_exists(project_path / (project + ".csproj")))
            << "src/" << project << "/" << project << ".csproj must exist";
    }
}

/**
 * @test Build scripts exist
 * @description Verify all required build scripts are present
 */
TEST_F(DirectoryStructureTest, BuildScriptsExist) {
    EXPECT_TRUE(file_exists(project_root / "scripts" / "build-all.ps1"))
        << "scripts/build-all.ps1 must exist for unified build";
    EXPECT_TRUE(file_exists(project_root / "scripts" / "run-tests.ps1"))
        << "scripts/run-tests.ps1 must exist for local test execution";
    EXPECT_TRUE(file_exists(project_root / "scripts" / "generate-proto.ps1"))
        << "scripts/generate-proto.ps1 must exist for protobuf generation";
}

/**
 * @test Docker test configuration exists
 * @description Verify Orthanc Docker Compose configuration is present
 */
TEST_F(DirectoryStructureTest, DockerTestConfigurationExists) {
    EXPECT_TRUE(file_exists(project_root / "tests" / "docker" / "docker-compose.orthanc.yml"))
        << "tests/docker/docker-compose.orthanc.yml must exist for DICOM test environment";
}

/**
 * @test SOUP register exists
 * @description Verify IEC 62304 SOUP register is present
 */
TEST_F(DirectoryStructureTest, SOUPRegisterExists) {
    EXPECT_TRUE(file_exists(project_root / "docs" / "soup-register.md"))
        << "docs/soup-register.md must exist for IEC 62304 compliance";
}
