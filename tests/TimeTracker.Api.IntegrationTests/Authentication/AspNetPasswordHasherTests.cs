using TimeTracker.Api.Authentication;

namespace TimeTracker.Api.IntegrationTests.Authentication;

public class AspNetPasswordHasherTests
{
    private const string Password = "clave-de-prueba-123";
    private const string OtherPassword = "otra-clave-456";

    // PBKDF2 es lento a propósito: un único adaptador (singleton, como en producción) para todas las pruebas.
    private static readonly AspNetPasswordHasher Hasher = new();

    [Fact]
    public void Hash_DoesNotContainThePasswordAndVerifies()
    {
        var hash = Hasher.Hash(Password);

        Assert.False(string.IsNullOrWhiteSpace(hash));
        Assert.DoesNotContain(Password, hash, StringComparison.Ordinal);
        Assert.True(Hasher.Verify(hash, Password));
    }

    [Fact]
    public void Verify_WithWrongPassword_ReturnsFalseWithoutThrowing()
        => Assert.False(Hasher.Verify(Hasher.Hash(Password), OtherPassword));

    [Fact]
    public void Hash_TwiceSamePassword_DiffersAndBothVerify()
    {
        var first = Hasher.Hash(Password);
        var second = Hasher.Hash(Password);

        Assert.NotEqual(first, second);
        Assert.True(Hasher.Verify(first, Password));
        Assert.True(Hasher.Verify(second, Password));
    }

    [Fact]
    public void Verify_WithNullHash_ReturnsFalse()
        => Assert.False(Hasher.Verify(null, Password));

    [Theory]
    [InlineData("no-es-un-hash")]
    [InlineData("AAAA")]
    [InlineData("")]
    [InlineData("   ")]
    public void Verify_WithUnreadableHash_ReturnsFalseWithoutThrowing(string hash)
        => Assert.False(Hasher.Verify(hash, Password));

    [Fact]
    public void Hash_UsesFormatV3()
    {
        var bytes = Convert.FromBase64String(Hasher.Hash(Password));

        // Formato V3 de PasswordHasher: marcador 0x01, PRF (uint BE), iteraciones (uint BE), longitud de sal (uint BE).
        Assert.Equal(0x01, bytes[0]);
        var iterations = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(5, 4));
        Assert.True(iterations >= 100_000, $"Iteraciones PBKDF2 insuficientes: {iterations}.");
    }
}
