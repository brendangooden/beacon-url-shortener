using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Shouldly;
using Beacon.Api.Features.Links;

namespace Beacon.ArchTests;

/// <summary>
/// Endpoints live only in <c>Features/</c>. Two checks: every route registrar (an
/// <see cref="IEndpointRouteBuilder"/> extension method) sits in a feature slice, and no code
/// outside <c>Features/</c> maps a route itself — except the composition root's one liveness route.
/// </summary>
public sealed class EndpointPlacementTests
{
    private const string FeaturesNamespace = "Beacon.Api.Features";

    /// <summary>Me, Workspaces, Folders, Links, Analytics, Branding (admin + public), Sharing, Admin, Redirect.</summary>
    private const int MinExpectedRegistrars = 10;

    /// <summary><c>Program.cs</c> maps <c>/health</c> (always-on liveness) and nothing else.</summary>
    private const int MaxRoutesInCompositionRoot = 1;

    private const BindingFlags AllDeclared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private static readonly Assembly ApiAssembly = typeof(LinkService).Assembly;

    [Test]
    public void Every_route_registrar_lives_in_a_feature_slice()
    {
        var registrars = ApiAssembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(m => m.IsDefined(typeof(ExtensionAttribute), false))
            .Where(m => m.GetParameters() is [var first, ..] && first.ParameterType == typeof(IEndpointRouteBuilder))
            .ToList();

        registrars.Count.ShouldBeGreaterThanOrEqualTo(MinExpectedRegistrars);
        registrars.Where(m => !IsInFeatures(m.DeclaringType!))
            .Select(m => $"{m.DeclaringType!.FullName}.{m.Name}")
            .ShouldBeEmpty("Route registrars must live under Features/.");
    }

    [Test]
    public void No_code_outside_features_maps_routes()
    {
        var offenders = ApiAssembly.GetTypes()
            .Where(t => !IsInFeatures(t) && !IsCompositionRoot(t))
            .SelectMany(t => MapCallsIn(t).Select(call => $"{t.FullName} calls {call}"))
            .ToList();

        offenders.ShouldBeEmpty("Only Features/ may map routes.");
    }

    [Test]
    public void Composition_root_maps_only_the_liveness_route()
    {
        var routes = ApiAssembly.GetTypes().Where(IsCompositionRoot).SelectMany(MapCallsIn).ToList();

        routes.Count.ShouldBeLessThanOrEqualTo(MaxRoutesInCompositionRoot, string.Join(", ", routes));
    }

    [Test]
    public void Map_call_detection_sees_the_features_own_routes()
    {
        // Guards the detector: if it stops recognising Map* calls, the two checks above pass vacuously.
        ApiAssembly.GetTypes().Where(IsInFeatures).SelectMany(MapCallsIn).Count().ShouldBeGreaterThanOrEqualTo(MinExpectedRegistrars);
    }

    private static bool IsInFeatures(Type type) =>
        type.Namespace is { } ns && (ns == FeaturesNamespace || ns.StartsWith(FeaturesNamespace + ".", StringComparison.Ordinal));

    /// <summary>The top-level-statements <c>Program</c> and its compiler-generated nested types.</summary>
    private static bool IsCompositionRoot(Type type)
    {
        for (var t = type; t is not null; t = t.DeclaringType)
        {
            if (t.Namespace is null && t.Name == "Program")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Calls to the route-mapping extensions (MapGet/MapPost/.../MapFallback), not MapGroup.</summary>
    private static IEnumerable<string> MapCallsIn(Type type) =>
        type.GetMethods(AllDeclared).Cast<MethodBase>().Concat(type.GetConstructors(AllDeclared))
            .SelectMany(TypeDependencies.CalledMethods)
            .Where(m => m.DeclaringType == typeof(EndpointRouteBuilderExtensions) && m.Name != nameof(EndpointRouteBuilderExtensions.MapGroup))
            .Select(m => m.Name);
}
