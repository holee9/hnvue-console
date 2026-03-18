using Xunit;

namespace HnVue.Integration.Tests.Fixtures;

// ---------------------------------------------------------------------------
// xUnit collection definition – allows tests to share a single CoreEngineFixture.
// Tests that declare [Collection("CoreEngine")] receive the same fixture instance.
// ---------------------------------------------------------------------------

/// <summary>
/// xUnit collection that shares a single <see cref="CoreEngineFixture"/> across all member tests.
/// Declare <c>[Collection("CoreEngine")]</c> on test classes that need the shared fixture.
/// </summary>
[CollectionDefinition("CoreEngine")]
public sealed class CoreEngineCollection : ICollectionFixture<CoreEngineFixture>
{
    // Marker class — no code required.
}
