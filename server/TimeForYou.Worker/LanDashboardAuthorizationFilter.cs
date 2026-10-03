using System.Net;
using System.Net.Sockets;
using Hangfire.Dashboard;

namespace TimeForYou.Worker;

/// <summary>
/// Lets loopback and private-network requests into the Hangfire dashboard. Hangfire's default only
/// allows localhost, which fails inside Docker (the host's requests arrive from the bridge gateway).
/// Unauthenticated on purpose, like the mock's admin endpoints — the LAN is the boundary.
/// </summary>
public sealed class LanDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) =>
        IsLan(context.GetHttpContext().Connection.RemoteIpAddress);

    public static bool IsLan(IPAddress? address)
    {
        if (address is null) return false;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return true;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return b[0] == 10
                || (b[0] == 172 && b[1] is >= 16 and <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 169 && b[1] == 254);
        }

        return address.AddressFamily == AddressFamily.InterNetworkV6
            && (address.IsIPv6LinkLocal || (address.GetAddressBytes()[0] & 0xFE) == 0xFC);
    }
}
