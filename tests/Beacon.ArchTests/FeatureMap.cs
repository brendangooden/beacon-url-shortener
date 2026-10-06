using System.Reflection;

namespace Beacon.ArchTests;

/// <summary>One cross-feature reference: <see cref="From"/> (in feature A) uses <see cref="To"/> (in feature B).</summary>
public sealed record FeatureViolation(string FromFeature, Type From, string ToFeature, Type To)
{
    public override string ToString() => $"{FromFeature}: {From.FullName} -> {ToFeature}: {To.FullName}";
}

/// <summary>
/// Maps a type to its feature slice by namespace. The feature is the first namespace segment after
/// <c>{root}.</c>, compared as a whole segment — so <c>Features.Workspaces</c> and a sibling
/// <c>Features.WorkspaceShares</c> are two features, never one (a raw prefix check would merge them).
/// </summary>
public sealed class FeatureMap(string featuresRoot)
{
    private readonly string _prefix = featuresRoot + ".";

    public string? FeatureOf(Type type)
    {
        var ns = type.Namespace;
        if (ns is null || !ns.StartsWith(_prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = ns[_prefix.Length..];
        var dot = rest.IndexOf('.', StringComparison.Ordinal);
        return dot < 0 ? rest : rest[..dot];
    }

    public IReadOnlySet<string> FeaturesIn(Assembly assembly) =>
        assembly.GetTypes().Select(FeatureOf).OfType<string>().ToHashSet(StringComparer.Ordinal);

    public IReadOnlyList<FeatureViolation> Violations(Assembly assembly)
    {
        var violations = new List<FeatureViolation>();
        foreach (var type in assembly.GetTypes())
        {
            if (FeatureOf(type) is not { } from)
            {
                continue;
            }

            foreach (var dependency in TypeDependencies.Of(type))
            {
                if (FeatureOf(dependency) is { } to && !string.Equals(from, to, StringComparison.Ordinal))
                {
                    violations.Add(new FeatureViolation(from, type, to, dependency));
                }
            }
        }

        return violations;
    }
}
