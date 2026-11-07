using Xunit;

namespace TSCC_WASD.Tests;

/// <summary>
/// AppLog.Directory is one static shared by the whole process. Test classes that repoint it must not run
/// in parallel with each other, or one test's log lines land in (or vanish with) another test's folder.
/// </summary>
[CollectionDefinition(Name)]
public sealed class AppLogCollection
{
    public const string Name = "AppLog";
}
