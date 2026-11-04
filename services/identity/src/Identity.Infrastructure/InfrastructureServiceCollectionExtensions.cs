using Identity.Application.Tenancy;
using Identity.Application.Users;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Persistence.Entities;
using Identity.Infrastructure.Persistence.Interceptors;
using Identity.Infrastructure.Security;
using Identity.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextSetter>(provider => provider.GetRequiredService<TenantContext>());

        services.AddDbContext<IdentityDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString("Identity")
                ?? throw new InvalidOperationException("Missing required setting 'ConnectionStrings:Identity'.");
            var tenantContext = provider.GetRequiredService<ITenantContext>();

            options.UseNpgsql(connectionString);
            options.AddInterceptors(
                new TenantConnectionInterceptor(tenantContext),
                new TenantWriteInterceptor(tenantContext));
        });

        services.AddOpenIddict().AddCore(core =>
        {
            core.UseEntityFrameworkCore()
                .UseDbContext<IdentityDbContext>()
                .ReplaceDefaultEntities<TenantApplication, TenantAuthorization, PlatformScope, TenantToken, Guid>();

            // The cache is in-process and its keys have no tenant, so it would bypass isolation.
            core.DisableEntityCaching();
        });

        services.AddScoped<IClientDirectory, ClientDirectory>();
        services.AddScoped<ILocalUserRepository, LocalUserRepository>();
        services.AddScoped<ValidateCredentialsHandler>();
        services.AddSingleton<IPasswordVerifier, Pbkdf2PasswordVerifier>();
        services.AddHostedService<DevelopmentSeeder>();

        return services;
    }
}
