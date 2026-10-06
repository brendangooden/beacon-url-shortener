using Microsoft.EntityFrameworkCore;
using Beacon.Api.Domain;
using Beacon.Api.Infrastructure.Auth;
using Beacon.Api.Infrastructure.Persistence;

namespace Beacon.Api.Features.Me;

public sealed record MeResponse(string Oid, string Email, string Name, bool IsGlobalAdmin);

/// <summary>
/// Identity + first-login provisioning. On the first call for a user we reconcile any pending shares
/// that were granted by email (keyed provisionally on the email) to the now-known oid, and ensure the
/// user has a personal Workspace.
/// </summary>
public sealed class MeService(AppDbContext db, CurrentUser user)
{
    private const string PersonalWorkspaceName = "My Links";

    public async Task<MeResponse> GetAsync(CancellationToken ct)
    {
        var oid = user.RequireOid();
        var email = user.Email;
        var name = user.Name;

        await UpsertDirectoryAsync(oid, email, name, ct);
        await ReconcilePendingGrantsAsync(oid, email, name, ct);
        await EnsurePersonalWorkspaceAsync(oid, ct);

        return new MeResponse(oid, email, name, user.IsGlobalAdmin);
    }

    // Keep a directory row per signed-in user so oids can be shown as human-readable names.
    private async Task UpsertDirectoryAsync(string oid, string email, string name, CancellationToken ct)
    {
        var row = await db.Users.AsTracking().FirstOrDefaultAsync(u => u.Oid == oid, ct);
        if (row is null)
        {
            db.Users.Add(AppUser.Create(oid, email, name));
        }
        else
        {
            row.Seen(email, name);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task ReconcilePendingGrantsAsync(string oid, string email, string name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var provisionalKey = email.ToLowerInvariant();
        var pending = await db.ResourceGrants.AsTracking()
            .Where(g => g.GranteeOid == provisionalKey)
            .ToListAsync(ct);

        if (pending.Count == 0)
        {
            return;
        }

        foreach (var grant in pending)
        {
            var realExists = await db.ResourceGrants.AnyAsync(
                g => g.ResourceType == grant.ResourceType && g.ResourceId == grant.ResourceId && g.GranteeOid == oid, ct);
            if (realExists)
            {
                db.ResourceGrants.Remove(grant); // already a real grant — drop the placeholder
            }
            else
            {
                grant.Reconcile(oid, email, name);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task EnsurePersonalWorkspaceAsync(string oid, CancellationToken ct)
    {
        var hasPersonal = await db.Workspaces.AnyAsync(w => w.CreatedByOid == oid && w.IsPersonal, ct);
        if (hasPersonal)
        {
            return;
        }

        db.Workspaces.Add(Workspace.Create(PersonalWorkspaceName, oid, isPersonal: true));
        await db.SaveChangesAsync(ct);
    }
}

public static class MeEndpoints
{
    public static void MapMeEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/me", (MeService svc, CancellationToken ct) => svc.GetAsync(ct)).WithTags("Me");
    }
}

public static class MeServiceExtensions
{
    public static IServiceCollection AddMe(this IServiceCollection services) =>
        services.AddScoped<MeService>();
}
