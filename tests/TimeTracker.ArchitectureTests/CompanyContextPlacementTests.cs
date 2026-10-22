using System.Reflection;

namespace TimeTracker.ArchitectureTests;

/// <summary>
/// V-15 (DD-7, spec solution-architecture «Puerto en Application, adaptador en Api»): el puerto <c>ICompanyContext</c> vive en
/// Application sin dependencias externas; su único adaptador vive en Api; Infrastructure no conoce <c>HttpContext</c>.
/// </summary>
public class CompanyContextPlacementTests
{
    private const string PortName = "TimeTracker.Application.Identity.ICompanyContext";

    [Fact]
    public void Port_LivesInApplicationAndExposesANullableCompany()
    {
        var port = Assembly.Load("TimeTracker.Application").GetType(PortName);

        Assert.NotNull(port);
        Assert.True(port.IsInterface);
        var property = Assert.Single(port.GetProperties());
        Assert.Equal(("CompanyId", typeof(Guid?)), (property.Name, property.PropertyType));
    }

    [Fact]
    public void CurrentUser_ExposesTheCompanyAsANonNullableGuid()
    {
        var property = Assembly.Load("TimeTracker.Application").GetType("TimeTracker.Application.Identity.ICurrentUser")!.GetProperty("CompanyId");

        Assert.Equal(typeof(Guid), property?.PropertyType);
    }

    [Theory]
    [InlineData("TimeTracker.Domain")]
    [InlineData("TimeTracker.Application")]
    [InlineData("TimeTracker.Infrastructure")]
    public void Adapter_IsNotImplementedOutsideApi(string assemblyName)
    {
        var port = Assembly.Load("TimeTracker.Application").GetType(PortName)!;

        var implementers = Assembly.Load(assemblyName).GetTypes().Where(type => type is { IsClass: true } && port.IsAssignableFrom(type)).Select(type => type.FullName).ToList();

        Assert.True(implementers.Count == 0, $"{assemblyName} implementa ICompanyContext (el adaptador vive solo en Api): {string.Join(", ", implementers)}");
    }

    [Fact]
    public void Api_ImplementsThePortOnlyInItsSecurityNamespace()
    {
        var port = Assembly.Load("TimeTracker.Application").GetType(PortName)!;

        var implementers = Assembly.Load("TimeTracker.Api").GetTypes().Where(type => type is { IsClass: true } && port.IsAssignableFrom(type)).ToList();

        var adapter = Assert.Single(implementers);
        Assert.Equal("TimeTracker.Api.Security.AmbientCompanyContext", adapter.FullName);
    }

    [Fact]
    public void Infrastructure_DoesNotReferenceAspNetCore()
    {
        var illegal = Assembly.Load("TimeTracker.Infrastructure").GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal))
            .ToList();

        Assert.True(illegal.Count == 0, $"Infrastructure referencia ASP.NET Core (sin HttpContext, DD-7): {string.Join(", ", illegal)}");
    }

    [Fact]
    public void Application_StillHasNoDirectPackageReferences()
    {
        var project = File.ReadAllText(Path.Combine(RepositoryRoot.Path, "src", "TimeTracker.Application", "TimeTracker.Application.csproj"));

        Assert.DoesNotContain("PackageReference", project, StringComparison.Ordinal);
    }
}
