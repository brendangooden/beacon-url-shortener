// Known-shape fixtures that prove the architecture checks can fail. Each namespace is a "feature"
// under the fixture root; the walker must flag exactly the couplings described on each type.

namespace Beacon.ArchTests.Fixtures.Features.Beta
{
    public sealed record BetaDto(string Value);

    public static class BetaHelper
    {
        public static string Shout(string value) => value.ToUpperInvariant();
    }
}

namespace Beacon.ArchTests.Fixtures.Features.Alpha
{
    /// <summary>Couples to Beta only inside a method body (a static call) — invisible to a signature-only check.</summary>
    public static class AlphaService
    {
        public static string Run(string value) => Beta.BetaHelper.Shout(value);
    }
}

namespace Beacon.ArchTests.Fixtures.Features.Gamma
{
    /// <summary>Couples to Beta only inside a lambda (compiled onto a nested closure type).</summary>
    public static class GammaEndpoints
    {
        public static Func<string, object> Handler() => value => new Beta.BetaDto(value);
    }
}

namespace Beacon.ArchTests.Fixtures.Features.Workspace
{
    public sealed class WorkspaceThing
    {
        public int Size { get; init; }
    }
}

namespace Beacon.ArchTests.Fixtures.Features.WorkspaceShares
{
    /// <summary>A sibling feature whose name starts with "Workspace" — must be its own feature.</summary>
    public sealed class ShareThing
    {
        public int Count { get; init; }
    }
}
