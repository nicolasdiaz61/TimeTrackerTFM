using TimeTracker.Api.Security;
using TimeTracker.Application.Identity;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Api.IntegrationTests.Security;

public class AmbientCompanyContextTests
{
    private static readonly Guid CompanyA = Guid.Parse("0197a1b2-aaaa-7e4f-8a5b-6c7d8e9f0001");
    private static readonly Guid CompanyB = Guid.Parse("0197a1b2-bbbb-7e4f-8a5b-6c7d8e9f0002");

    [Fact]
    public void CompanyId_BeforeAnySet_IsNullAndDoesNotThrow()
        => Assert.Null(((ICompanyContext)new AmbientCompanyContext()).CompanyId);

    [Fact]
    public void Set_FixesTheCompanyForBothPorts()
    {
        var ambient = new AmbientCompanyContext();

        ((ICompanyContextSetter)ambient).Set(CompanyA);

        Assert.Equal(CompanyA, ((ICompanyContext)ambient).CompanyId);
    }

    [Fact]
    public void Set_AgainWithTheSameCompany_IsAccepted()
    {
        var ambient = new AmbientCompanyContext();
        ambient.Set(CompanyA);

        ambient.Set(CompanyA);

        Assert.Equal(CompanyA, ambient.CompanyId);
    }

    [Fact]
    public void Set_AgainWithAnotherCompany_ThrowsAndKeepsTheFirst()
    {
        var ambient = new AmbientCompanyContext();
        ambient.Set(CompanyA);

        var exception = Assert.Throws<InvalidOperationException>(() => ambient.Set(CompanyB));

        Assert.Equal(CompanyA, ambient.CompanyId);
        Assert.DoesNotContain(CompanyA.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(CompanyB.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Set_WithAnEmptyCompany_ThrowsAndLeavesNoCompany()
    {
        var ambient = new AmbientCompanyContext();

        Assert.Throws<ArgumentException>(() => ambient.Set(Guid.Empty));

        Assert.Null(ambient.CompanyId);
    }
}
