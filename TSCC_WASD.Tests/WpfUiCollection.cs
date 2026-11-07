using Xunit;

namespace TSCC_WASD.Tests;

/// <summary>
/// Tests that build WPF-UI windows and pages on their own STA threads. Loading WPF-UI's controls on several threads at
/// once can deadlock inside their static initialisers (one thread then waits forever), so these classes run one at a time.
/// The app itself has a single UI thread and is not affected.
/// </summary>
[CollectionDefinition(Name)]
public sealed class WpfUiCollection
{
    public const string Name = "WpfUi";
}
