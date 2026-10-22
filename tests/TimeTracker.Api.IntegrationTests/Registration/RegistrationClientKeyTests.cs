using System.Net;

using TimeTracker.Api.Registration;

namespace TimeTracker.Api.IntegrationTests.Registration;

/// <summary>Clave de partición del límite del registro: IPv4 exacta e IPv6 agrupada por prefijo /64.</summary>
public class RegistrationClientKeyTests
{
    private static string Key(string address) => RegistrationClientKey.For(IPAddress.Parse(address));

    [Fact]
    public void AnIPv4Address_IsKeptExact()
    {
        Assert.Equal("203.0.113.7", Key("203.0.113.7"));
        Assert.NotEqual(Key("203.0.113.7"), Key("203.0.113.8"));
    }

    [Fact]
    public void IPv6AddressesOfTheSame64Prefix_ShareTheKey()
    {
        Assert.Equal(Key("2001:db8:aaaa:bbbb::1"), Key("2001:db8:aaaa:bbbb:ffff:ffff:ffff:ffff"));
    }

    [Fact]
    public void IPv6AddressesOfDifferent64Prefixes_HaveDifferentKeys()
    {
        Assert.NotEqual(Key("2001:db8:aaaa:bbbb::1"), Key("2001:db8:aaaa:bbbc::1"));
    }

    [Fact]
    public void AnIPv4MappedIPv6Address_UsesTheIPv4Key()
    {
        Assert.Equal(Key("203.0.113.7"), Key("::ffff:203.0.113.7"));
    }

    [Fact]
    public void WithoutAnAddress_ThereIsOneSharedKey()
    {
        Assert.Equal(RegistrationClientKey.NoAddress, RegistrationClientKey.For(null));
    }

    [Fact]
    public void AnIPv6KeyNeverCollidesWithAnIPv4Key()
    {
        Assert.NotEqual(Key("::1"), Key("0.0.0.1"));
    }
}
