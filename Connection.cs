using Kaenx.Konnect.Addresses;
using System.Net;
using System.Net.NetworkInformation;

public class Connection
{
    public bool IsRouting { get; set; } = false;
    public string FriendlyName { get; set; } = "";
    public IPEndPoint IPAddress { get; set; }
    public UnicastAddress PhysicalAddress { get; set; } = UnicastAddress.FromString("0.0.0");
    public int Version { get; set; } = 1;

    public Connection(IPEndPoint ip)
    {
        IPAddress = ip;
    }
}