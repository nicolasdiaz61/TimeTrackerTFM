using System.Security.Cryptography;
using System.Text;

namespace TimeTracker.Application.Companies;

/// <summary>
/// Compara el código recibido con cada código configurado en tiempo constante (DD-19): se resume con SHA-256, de modo que las dos
/// partes miden lo mismo, y se compara con <see cref="CryptographicOperations.FixedTimeEquals"/> recorriendo siempre todos los
/// códigos, sin cortocircuito por longitud ni por posición del primer carácter distinto. El código no se consume.
/// </summary>
public sealed class InviteCodeValidator : IInviteCodeValidator
{
    /// <summary>Longitud máxima de un código (y de un candidato): lo que no cabe no puede coincidir con ninguno.</summary>
    public const int MaximumLength = 128;

    private readonly byte[][] _codeHashes;

    public InviteCodeValidator(IEnumerable<string> codes)
        => _codeHashes = [.. codes.Select(code => code.Trim()).Where(code => code.Length > 0).Select(Hash)];

    public bool IsValid(string? candidate)
    {
        var trimmed = candidate?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaximumLength)
        {
            return false;
        }

        return MatchesAny(Hash(trimmed), _codeHashes, (left, right) => CryptographicOperations.FixedTimeEquals(left, right));
    }

    /// <summary>
    /// Recorre todos los resúmenes y acumula el acierto con un OR a nivel de bits: nunca sale antes de tiempo. La comparación se
    /// inyecta para poder comprobar que se visita cada código.
    /// </summary>
    public static bool MatchesAny(byte[] candidateHash, IReadOnlyList<byte[]> codeHashes, Func<byte[], byte[], bool> equals)
    {
        var matched = false;
        foreach (var codeHash in codeHashes)
        {
            matched |= equals(candidateHash, codeHash);
        }

        return matched;
    }

    private static byte[] Hash(string code) => SHA256.HashData(Encoding.UTF8.GetBytes(code));
}
