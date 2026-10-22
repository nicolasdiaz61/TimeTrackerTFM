using System.Net;
using System.Net.Sockets;

namespace TimeTracker.Api.Registration;

/// <summary>
/// Clave de partición del límite por IP del registro. IPv4 va exacta; IPv6 se agrupa por prefijo /64, la unidad que un proveedor
/// entrega a un solo cliente: rotar direcciones dentro de ese /64 no da cupo nuevo. Una IPv6 con IPv4 incrustada (::ffff:a.b.c.d)
/// usa la clave de su IPv4.
/// </summary>
public static class RegistrationClientKey
{
    /// <summary>Sin dirección remota (servidor en memoria de las pruebas) todo cae en una partición única.</summary>
    public const string NoAddress = "sin-ip";

    private const int PrefixBytes = 8;

    public static string For(IPAddress? address)
    {
        if (address is null)
        {
            return NoAddress;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return address.AddressFamily == AddressFamily.InterNetworkV6
            ? $"v6:{Convert.ToHexString(address.GetAddressBytes().AsSpan(0, PrefixBytes))}/64"
            : address.ToString();
    }
}
