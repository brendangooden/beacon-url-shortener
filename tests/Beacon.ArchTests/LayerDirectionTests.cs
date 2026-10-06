using System.Reflection;
using Shouldly;
using Beacon.Api.Domain;

namespace Beacon.ArchTests;

/// <summary>
/// The minimal-layout analogue of "Core does not reference Api": the Domain model depends on
/// nothing app-specific except <c>Common</c> — never on a feature slice or on Infrastructure.
/// </summary>
public sealed class LayerDirectionTests
{
    private const string DomainNamespace = "Beacon.Api.Domain";
    private static readonly string[] Forbidden = ["Beacon.Api.Features", "Beacon.Api.Infrastructure"];
    private static readonly Assembly ApiAssembly = typeof(Link).Assembly;

    [Test]
    public void Domain_does_not_depend_on_features_or_infrastructure()
    {
        var domainTypes = ApiAssembly.GetTypes().Where(t => t.Namespace == DomainNamespace).ToList();
        domainTypes.ShouldContain(typeof(Link)); // not vacuous

        var offenders = domainTypes
            .SelectMany(t => TypeDependencies.Of(t).Select(d => (From: t, To: d)))
            .Where(x => x.To.Namespace is { } ns && Forbidden.Any(f => ns == f || ns.StartsWith(f + ".", StringComparison.Ordinal)))
            .Select(x => $"{x.From.FullName} -> {x.To.FullName}")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        offenders.ShouldBeEmpty();
    }
}
