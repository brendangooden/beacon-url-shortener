using Bogus;
using Beacon.Api.Features.Folders;

namespace Beacon.TestInfrastructure.Fakers;

/// <summary>A <see cref="CreateFolderRequest"/> whose name starts with the test's key.</summary>
public sealed class FolderRequestFaker : Faker<CreateFolderRequest>
{
    public FolderRequestFaker(TestKey key)
    {
        CustomInstantiator(f => new CreateFolderRequest(key.Name(f.Commerce.ProductName())));
    }
}
