using System.Globalization;

using TimeTracker.Domain.Users;

namespace TimeTracker.Domain.Tests;

public class UserAccountTests
{
    private static readonly Guid CompanyId = Guid.Parse("0197a1b2-3c3d-7e4f-8a5b-6c7d8e9f0a3d");
    private static readonly Guid UserId = Guid.Parse("0197a1b2-1c3d-7e4f-8a5b-6c7d8e9f0a1b");
    private static readonly Guid ManagerId = Guid.Parse("0197a1b2-2c3d-7e4f-8a5b-6c7d8e9f0a2c");
    private const string Email = "luis.empleado@example.test";
    private const string DisplayName = "Luis Empleado";
    private const string PasswordHash = "hash-de-prueba";

    [Fact]
    public void UserRole_HasExactlyEmployeeAndManager()
    {
        Assert.Equal(["Employee", "Manager"], Enum.GetNames<UserRole>());
    }

    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        var user = UserAccount.Create(UserId, CompanyId, Email, DisplayName, PasswordHash, UserRole.Manager, ManagerId);

        Assert.Equal(UserId, user.Id);
        Assert.Equal(CompanyId, user.CompanyId);
        Assert.False(user.MustChangePassword);
        Assert.Equal(Email, user.Email);
        Assert.Equal(DisplayName, user.DisplayName);
        Assert.Equal(PasswordHash, user.PasswordHash);
        Assert.Equal(UserRole.Manager, user.Role);
        Assert.Equal(ManagerId, user.ManagerId);
        Assert.Equal(254, UserAccount.MaxEmailLength);
        Assert.Equal(100, UserAccount.MaxDisplayNameLength);
    }

    [Fact]
    public void Create_NormalizesEmailToLowerAndTrims()
    {
        var user = UserAccount.Create(UserId, CompanyId, "  Marta.Responsable@Example.TEST ", DisplayName, PasswordHash, UserRole.Manager, null);

        Assert.Equal("marta.responsable@example.test", user.Email);
    }

    [Fact]
    public void NormalizeEmail_LowersInvariant()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
        try
        {
            // Con cultura turca, ToLower() convertiría la «I» en «ı».
            Assert.Equal("luis.empleado@example.test", UserAccount.NormalizeEmail("LUIS.EMPLEADO@EXAMPLE.TEST"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Create_TrimsDisplayName()
    {
        var user = UserAccount.Create(UserId, CompanyId, Email, "  Marta Ejemplo  ", PasswordHash, UserRole.Manager, null);

        Assert.Equal("Marta Ejemplo", user.DisplayName);
    }

    [Fact]
    public void Create_WithEmptyId_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => UserAccount.Create(Guid.Empty, CompanyId, Email, DisplayName, PasswordHash, UserRole.Employee, null));
    }

    public static TheoryData<string> InvalidEmails =>
    [
        "",
        "   ",
        "sin-arroba",
        new string('a', UserAccount.MaxEmailLength - "@example.test".Length + 1) + "@example.test",
    ];

    [Theory]
    [MemberData(nameof(InvalidEmails))]
    public void Create_WithBlankOrMalformedOrTooLongEmail_Throws(string email)
    {
        Assert.ThrowsAny<ArgumentException>(() => UserAccount.Create(UserId, CompanyId, email, DisplayName, PasswordHash, UserRole.Employee, null));
    }

    [Fact]
    public void Create_WithEmailOf254Characters_IsValid()
    {
        var email = new string('a', UserAccount.MaxEmailLength - "@example.test".Length) + "@example.test";

        var user = UserAccount.Create(UserId, CompanyId, email, DisplayName, PasswordHash, UserRole.Employee, null);

        Assert.Equal(254, user.Email.Length);
    }

    public static TheoryData<string> InvalidDisplayNames =>
    [
        "",
        "   ",
        new string('n', UserAccount.MaxDisplayNameLength + 1),
    ];

    [Theory]
    [MemberData(nameof(InvalidDisplayNames))]
    public void Create_WithBlankOrTooLongDisplayName_Throws(string displayName)
    {
        Assert.ThrowsAny<ArgumentException>(() => UserAccount.Create(UserId, CompanyId, Email, displayName, PasswordHash, UserRole.Employee, null));
    }

    [Fact]
    public void Create_WithDisplayNameOf100Characters_IsValid()
    {
        var name = new string('n', UserAccount.MaxDisplayNameLength);

        var user = UserAccount.Create(UserId, CompanyId, Email, name, PasswordHash, UserRole.Employee, null);

        Assert.Equal(name, user.DisplayName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankPasswordHash_Throws(string passwordHash)
    {
        Assert.ThrowsAny<ArgumentException>(() => UserAccount.Create(UserId, CompanyId, Email, DisplayName, passwordHash, UserRole.Employee, null));
    }

    [Fact]
    public void Create_WithUndefinedRole_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => UserAccount.Create(UserId, CompanyId, Email, DisplayName, PasswordHash, (UserRole)99, null));
    }

    [Fact]
    public void Create_WithManagerIdEqualToId_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => UserAccount.Create(UserId, CompanyId, Email, DisplayName, PasswordHash, UserRole.Employee, UserId));
    }

    [Fact]
    public void Create_WithEmptyManagerId_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => UserAccount.Create(UserId, CompanyId, Email, DisplayName, PasswordHash, UserRole.Employee, Guid.Empty));
    }

    [Fact]
    public void Create_WithNullManagerId_CreatesUserWithoutManager()
    {
        var user = UserAccount.Create(UserId, CompanyId, Email, DisplayName, PasswordHash, UserRole.Manager, null);

        Assert.Null(user.ManagerId);
    }

    [Fact]
    public void Create_WithManagerId_KeepsIt()
    {
        var user = UserAccount.Create(UserId, CompanyId, Email, DisplayName, PasswordHash, UserRole.Employee, ManagerId);

        Assert.Equal(ManagerId, user.ManagerId);
    }

    [Fact]
    public void Create_WithEmptyCompanyId_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => UserAccount.Create(UserId, Guid.Empty, Email, DisplayName, PasswordHash, UserRole.Employee, null));
    }

    [Fact]
    public void Create_WithTemporaryPasswordMark_SetsMustChangePassword()
    {
        var user = UserAccount.Create(UserId, CompanyId, Email, DisplayName, PasswordHash, UserRole.Employee, ManagerId, mustChangePassword: true);

        Assert.True(user.MustChangePassword);
    }

    [Fact]
    public void ChangePassword_ReplacesTheHashAndClearsTheMark()
    {
        var user = UserAccount.Create(UserId, CompanyId, Email, DisplayName, PasswordHash, UserRole.Employee, ManagerId, mustChangePassword: true);

        user.ChangePassword("hash-nuevo");

        Assert.Equal("hash-nuevo", user.PasswordHash);
        Assert.False(user.MustChangePassword);
    }

    [Fact]
    public void RegenerateTemporaryPassword_ReplacesTheHashAndKeepsTheMark()
    {
        var user = UserAccount.Create(UserId, CompanyId, Email, DisplayName, PasswordHash, UserRole.Employee, ManagerId, mustChangePassword: true);

        user.RegenerateTemporaryPassword("hash-temporal-nuevo");

        Assert.Equal("hash-temporal-nuevo", user.PasswordHash);
        Assert.True(user.MustChangePassword);
    }

    [Fact]
    public void RegenerateTemporaryPassword_WithoutPreviousMark_LeavesItAsItWas()
    {
        // La regla «solo antes del primer acceso» la aplica el caso de uso, no el dominio.
        var user = UserAccount.Create(UserId, CompanyId, Email, DisplayName, PasswordHash, UserRole.Employee, ManagerId);

        user.RegenerateTemporaryPassword("hash-temporal-nuevo");

        Assert.Equal("hash-temporal-nuevo", user.PasswordHash);
        Assert.False(user.MustChangePassword);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ChangePassword_WithBlankHash_ThrowsAndLeavesTheUserUnchanged(string hash)
    {
        var user = UserAccount.Create(UserId, CompanyId, Email, DisplayName, PasswordHash, UserRole.Employee, ManagerId, mustChangePassword: true);

        Assert.ThrowsAny<ArgumentException>(() => user.ChangePassword(hash));

        Assert.Equal(PasswordHash, user.PasswordHash);
        Assert.True(user.MustChangePassword);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RegenerateTemporaryPassword_WithBlankHash_ThrowsAndLeavesTheUserUnchanged(string hash)
    {
        var user = UserAccount.Create(UserId, CompanyId, Email, DisplayName, PasswordHash, UserRole.Employee, ManagerId, mustChangePassword: true);

        Assert.ThrowsAny<ArgumentException>(() => user.RegenerateTemporaryPassword(hash));

        Assert.Equal(PasswordHash, user.PasswordHash);
        Assert.True(user.MustChangePassword);
    }
}
