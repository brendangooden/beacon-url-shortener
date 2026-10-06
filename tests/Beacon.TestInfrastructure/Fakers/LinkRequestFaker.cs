using Bogus;
using Beacon.Api.Features.Links;

namespace Beacon.TestInfrastructure.Fakers;

/// <summary>
/// A valid <see cref="CreateLinkRequest"/>: an https Destination, a title stamped with the test's
/// key, and no vanity code (the server generates one). Override fields with <c>with</c>.
/// </summary>
public sealed class LinkRequestFaker : Faker<CreateLinkRequest>
{
    public LinkRequestFaker(TestKey key)
    {
        CustomInstantiator(f => new CreateLinkRequest(
            Destination: $"https://{f.Internet.DomainName()}/{f.Internet.DomainWord()}/{f.Random.AlphaNumeric(8)}",
            Code: null,
            FolderId: null,
            Title: key.Name(f.Lorem.Word()),
            Notes: f.Lorem.Sentence(),
            Tags: [f.Commerce.Department(), f.Hacker.Noun()],
            ExpiresOnUtc: null));
    }
}
