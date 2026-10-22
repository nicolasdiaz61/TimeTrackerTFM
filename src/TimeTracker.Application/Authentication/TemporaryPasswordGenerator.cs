using System.Security.Cryptography;

namespace TimeTracker.Application.Authentication;

/// <summary>
/// Genera la contraseña temporal con <see cref="RandomNumberGenerator"/> (DD-21): 16 caracteres de un alfabeto de 57 símbolos sin
/// ambigüedades (sin 0, O, 1, l ni I), unos 93 bits de entropía. Cumple <see cref="PasswordPolicy"/> por construcción.
/// </summary>
public sealed class TemporaryPasswordGenerator : ITemporaryPasswordGenerator
{
    public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

    public const int Length = 16;

    // GetInt32 descarta los valores que sesgarían el reparto: cada símbolo es equiprobable.
    public string Generate() => string.Create(Length, Alphabet, static (buffer, alphabet) =>
    {
        for (var index = 0; index < buffer.Length; index++)
        {
            buffer[index] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        }
    });
}
