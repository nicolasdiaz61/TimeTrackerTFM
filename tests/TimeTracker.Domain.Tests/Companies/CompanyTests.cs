using TimeTracker.Domain.Companies;

namespace TimeTracker.Domain.Tests.Companies;

public class CompanyTests
{
    private static readonly Guid CompanyId = Guid.Parse("0197a1b2-3c3d-7e4f-8a5b-6c7d8e9f0a3d");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 21, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DefaultId_IsTheFixedConstantOfTheMigration()
    {
        Assert.Equal(Guid.Parse("00000000-0000-7000-8000-000000000001"), Company.DefaultId);
    }

    [Fact]
    public void Create_WithValidData_SetsPropertiesAndStartsActiveWithoutPlatformAssistant()
    {
        var company = Company.Create(CompanyId, "  Acme   S.L. ", CreatedAt);

        Assert.Equal(CompanyId, company.Id);
        Assert.Equal("Acme   S.L.", company.Name);
        Assert.Equal("acme s.l.", company.NameNormalized);
        Assert.Equal(CompanyStatus.Active, company.Status);
        Assert.False(company.UsesPlatformAssistant);
        Assert.Equal(CreatedAt, company.CreatedAt);
    }

    [Fact]
    public void Create_WithPlatformAssistantFlag_KeepsIt()
    {
        var company = Company.Create(Company.DefaultId, Company.DefaultName, CreatedAt, usesPlatformAssistant: true);

        Assert.True(company.UsesPlatformAssistant);
        Assert.Equal("Demo", company.Name);
        Assert.Equal("demo", company.NameNormalized);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void Create_WithNameOfAllowedLength_IsValid(int length)
    {
        var company = Company.Create(CompanyId, new string('a', length), CreatedAt);

        Assert.Equal(length, company.Name.Length);
    }

    [Fact]
    public void Create_CountsTheLengthAfterTrimming()
    {
        var company = Company.Create(CompanyId, "  " + new string('a', 100) + "  ", CreatedAt);

        Assert.Equal(100, company.Name.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankName_Throws(string name)
    {
        Assert.ThrowsAny<ArgumentException>(() => Company.Create(CompanyId, name, CreatedAt));
    }

    [Fact]
    public void Create_WithNameOf101Characters_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => Company.Create(CompanyId, new string('a', 101), CreatedAt));
    }

    [Fact]
    public void Create_WithEmptyId_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => Company.Create(Guid.Empty, "Acme", CreatedAt));
    }

    [Fact]
    public void Create_WithCreationInstantThatIsNotUtc_Throws()
    {
        var local = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.FromHours(2));

        Assert.ThrowsAny<ArgumentException>(() => Company.Create(CompanyId, "Acme", local));
    }

    [Theory]
    [InlineData("  Acme   S.L. ", "acme s.l.")]
    [InlineData("Demo", "demo")]
    [InlineData("Ácme Norte", "ácme norte")]
    [InlineData("ACME\t\tNorte\n Sur", "acme norte sur")]
    public void NormalizeName_TrimsCollapsesSpacesAndLowersWithoutFoldingAccents(string name, string expected)
    {
        Assert.Equal(expected, Company.NormalizeName(name));
    }

    [Fact]
    public void NormalizeName_KeepsAccentsDistinctFromThePlainLetter()
    {
        Assert.NotEqual(Company.NormalizeName("Acme Norte"), Company.NormalizeName("Ácme Norte"));
    }

    [Fact]
    public void NormalizeName_UsesInvariantCulture()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");
        try
        {
            Assert.Equal("acme institute", Company.NormalizeName("ACME INSTITUTE"));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void CompanyStatus_HasExactlyActiveAndSuspended()
    {
        Assert.Equal(["Active", "Suspended"], Enum.GetNames<CompanyStatus>());
    }
}
