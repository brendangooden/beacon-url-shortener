using Bogus;
using Beacon.Api.Features.Workspaces;

namespace Beacon.TestInfrastructure.Fakers;

/// <summary>A <see cref="CreateWorkspaceRequest"/> whose name starts with the test's key.</summary>
public sealed class WorkspaceRequestFaker : Faker<CreateWorkspaceRequest>
{
    public WorkspaceRequestFaker(TestKey key)
    {
        CustomInstantiator(f => new CreateWorkspaceRequest(key.Name(f.Commerce.Department())));
    }
}
