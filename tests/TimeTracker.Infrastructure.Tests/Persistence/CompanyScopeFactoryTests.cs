using Microsoft.Extensions.DependencyInjection;

using TimeTracker.Application.Identity;
using TimeTracker.Infrastructure.Persistence;
using TimeTracker.Infrastructure.Tests.Fakes;

namespace TimeTracker.Infrastructure.Tests.Persistence;

public sealed class CompanyScopeFactoryTests
{
    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddScoped<RecordingCompanyContext>();
        services.AddScoped<ICompanyContext>(provider => provider.GetRequiredService<RecordingCompanyContext>());
        services.AddScoped<ICompanyContextSetter>(provider => provider.GetRequiredService<RecordingCompanyContext>());
        services.AddSingleton<ICompanyScopeFactory, CompanyScopeFactory>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    [Fact]
    public async Task CreateScope_FixesTheCompanyOfTheNewScope()
    {
        await using var provider = Build();
        var companyId = Guid.Parse("0197a1b2-aaaa-7e4f-8a5b-6c7d8e9f0001");

        await using var scope = provider.GetRequiredService<ICompanyScopeFactory>().CreateScope(companyId);

        Assert.Equal(companyId, scope.ServiceProvider.GetRequiredService<ICompanyContext>().CompanyId);
    }

    [Fact]
    public async Task CreateScope_TwoScopesDoNotShareTheirCompany()
    {
        await using var provider = Build();
        var factory = provider.GetRequiredService<ICompanyScopeFactory>();
        var first = Guid.Parse("0197a1b2-aaaa-7e4f-8a5b-6c7d8e9f0001");
        var second = Guid.Parse("0197a1b2-bbbb-7e4f-8a5b-6c7d8e9f0002");

        await using var scopeA = factory.CreateScope(first);
        await using var scopeB = factory.CreateScope(second);

        Assert.Equal(first, scopeA.ServiceProvider.GetRequiredService<ICompanyContext>().CompanyId);
        Assert.Equal(second, scopeB.ServiceProvider.GetRequiredService<ICompanyContext>().CompanyId);
        await using var plain = provider.CreateAsyncScope();
        Assert.Null(plain.ServiceProvider.GetRequiredService<ICompanyContext>().CompanyId);
    }
}
