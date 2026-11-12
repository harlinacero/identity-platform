using Identity.Application.Tenancy;
using Identity.Application.Users;
using Identity.Domain.Users;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Identity.Infrastructure.Seeding;

/// <summary>Creates test data in Development and Testing only. No setting can enable it elsewhere.</summary>
public sealed class DevelopmentSeeder(
    IServiceProvider services,
    IConfiguration configuration,
    IHostEnvironment environment) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
        {
            return;
        }

        var options = configuration.GetSection("Seed").Get<SeedOptions>() ?? new SeedOptions();
        options.Validate();

        await SeedTenantAsync(SeedData.TenantAId, SeedData.ClientAId, options.TenantA.UserPassword!, cancellationToken);
        await SeedTenantAsync(SeedData.TenantBId, SeedData.ClientBId, options.TenantB.UserPassword!, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedTenantAsync(Guid tenantId, string clientId, string password, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        provider.GetRequiredService<ITenantContextSetter>().Set(tenantId);
        var context = provider.GetRequiredService<IdentityDbContext>();

        if (!await context.ClientDirectory.AnyAsync(entry => entry.ClientId == clientId, cancellationToken))
        {
            context.ClientDirectory.Add(new ClientDirectoryEntry { ClientId = clientId, TenantId = tenantId });
        }

        if (await provider.GetRequiredService<ILocalUserRepository>().FindByUsernameAsync(SeedData.Username, cancellationToken) is null)
        {
            var hash = provider.GetRequiredService<IPasswordVerifier>().Hash(password);
            context.LocalUsers.Add(LocalUser.Create(SeedData.Username, hash));
        }

        await context.SaveChangesAsync(cancellationToken);

        var applications = provider.GetRequiredService<IOpenIddictApplicationManager>();
        if (await applications.FindByClientIdAsync(clientId, cancellationToken) is null)
        {
            await applications.CreateAsync(CreatePublicClient(clientId), cancellationToken);
        }
    }

    private static OpenIddictApplicationDescriptor CreatePublicClient(string clientId) => new()
    {
        ClientId = clientId,
        ClientType = ClientTypes.Public,
        ConsentType = ConsentTypes.Implicit,
        DisplayName = clientId,
        RedirectUris = { new Uri(SeedData.RedirectUri) },
        Permissions =
        {
            Permissions.Endpoints.Authorization,
            Permissions.Endpoints.Token,
            Permissions.GrantTypes.AuthorizationCode,
            Permissions.ResponseTypes.Code,
            Permissions.Scopes.Profile,
        },
        Requirements = { Requirements.Features.ProofKeyForCodeExchange },
    };
}
