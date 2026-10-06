using System.Reflection;
using Shouldly;
using Beacon.Api.Features.Links;

namespace Beacon.ArchTests;

/// <summary>
/// Rule 1 — feature isolation: a type under <c>Features/&lt;A&gt;/</c> must not use anything from
/// <c>Features/&lt;B&gt;/</c>. Shared needs go through <c>Common/</c> (or <c>Infrastructure/</c>).
/// </summary>
public sealed class FeatureIsolationTests
{
    private const string ApiFeaturesRoot = "Beacon.Api.Features";
    private const string FixtureFeaturesRoot = "Beacon.ArchTests.Fixtures.Features";

    /// <summary>Admin, Analytics, Branding, Folders, Links, Me, Redirect, Sharing, Workspaces.
    /// A floor, so a broken namespace scan can never pass by finding nothing.</summary>
    private const int MinExpectedFeatures = 9;

    private static readonly Assembly ApiAssembly = typeof(LinkService).Assembly;

    [Test]
    public void Api_has_the_expected_feature_slices()
    {
        var features = new FeatureMap(ApiFeaturesRoot).FeaturesIn(ApiAssembly);

        features.Count.ShouldBeGreaterThanOrEqualTo(MinExpectedFeatures, $"Found: {string.Join(", ", features)}");
    }

    [Test]
    public void No_feature_depends_on_another_feature()
    {
        var violations = new FeatureMap(ApiFeaturesRoot).Violations(ApiAssembly);

        violations.ShouldBeEmpty(
            "Cross-feature references found (move the shared type to Common/):" + Environment.NewLine +
            string.Join(Environment.NewLine, violations.Select(v => v.ToString()).Distinct(StringComparer.Ordinal)));
    }

    // ---- The checker itself: prove it can fail, so the real check above is not vacuous. ----

    [Test]
    public void Walker_flags_a_coupling_hidden_in_a_method_body()
    {
        var violations = new FeatureMap(FixtureFeaturesRoot).Violations(typeof(FeatureIsolationTests).Assembly);

        violations.ShouldContain(v => v.FromFeature == "Alpha" && v.ToFeature == "Beta");
    }

    [Test]
    public void Walker_flags_a_coupling_hidden_in_a_lambda()
    {
        var violations = new FeatureMap(FixtureFeaturesRoot).Violations(typeof(FeatureIsolationTests).Assembly);

        violations.ShouldContain(v => v.FromFeature == "Gamma" && v.ToFeature == "Beta");
    }

    [Test]
    public void Walker_flags_only_the_known_couplings()
    {
        var violations = new FeatureMap(FixtureFeaturesRoot).Violations(typeof(FeatureIsolationTests).Assembly);

        violations.Select(v => $"{v.FromFeature}->{v.ToFeature}").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .ShouldBe(["Alpha->Beta", "Gamma->Beta"]);
    }

    [Test]
    public void Feature_boundary_is_a_whole_namespace_segment_not_a_prefix()
    {
        var map = new FeatureMap(FixtureFeaturesRoot);

        map.FeatureOf(typeof(Fixtures.Features.Workspace.WorkspaceThing)).ShouldBe("Workspace");
        map.FeatureOf(typeof(Fixtures.Features.WorkspaceShares.ShareThing)).ShouldBe("WorkspaceShares");
        map.FeatureOf(typeof(FeatureIsolationTests)).ShouldBeNull();
    }
}
