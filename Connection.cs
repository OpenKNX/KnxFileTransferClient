using Kaenx.Konnect.Addresses;
using System.Net;

public class Connection
{
    public bool IsRouting { get; set; } = false;
    public string FriendlyName { get; set; } = "";
    public IPEndPoint IPAddress { get; set; }
    public UnicastAddress PhysicalAddress { get; set; } = UnicastAddress.FromString("0.0.0");
}