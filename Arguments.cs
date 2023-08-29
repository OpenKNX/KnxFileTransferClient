using System.Net;
using Kaenx.Konnect.Addresses;

namespace KnxFileTransferClient;

internal class Arguments{
    
    private static Dictionary<string, int> arguments = new Dictionary<string, int> {
        {"port", 3671},
        {"delay", 0},
        {"pkg", 128},
        {"errors", 3},
        {"force", 0},
        {"routing", 0},
    };

    public UnicastAddress? PhysicalAddress { get; } = null;
    public string Interface { get; } = "";
    public string Path1 { get; } = "";
    public string Path2 { get; } = "";
    public string Command { get; } = "";

    public Arguments() { }

    public Arguments(string[] args, bool isOpen = false)
    {
        Command = args[0];
        
        foreach (string arg in args.Where(a => a.StartsWith("--")))
        {
            string[] sp = arg.Split("=");
            string name = sp[0].Substring(2);
            if (sp.Length == 1)
            {
                arguments[name] = 1;
            }
            else
            {
                if (!arguments.ContainsKey(name))
                    throw new Exception("Unbekanntes Argument: " + name);
                    
                arguments[name] = int.Parse(sp[1]);
            }
        }

        if(Command == "open")
        {
            if(arguments["routing"] == 1)
            {
                if(args[1].Split(".").Length == 4)
                {
                    Interface = args[1];
                    PhysicalAddress = UnicastAddress.FromString(args[2]);
                } else {
                    Interface = "224.0.23.12";
                    PhysicalAddress = UnicastAddress.FromString(args[1]);
                }
            } else {
                Interface = args[1];
                PhysicalAddress = UnicastAddress.FromString(args[2]);
            }
            return;
        }

        if(Command == "close" || Command == "help")
            return;

        if(isOpen)
        {
            Path1 = args[1];
            if(args.Length > 2 && !args[2].StartsWith("--"))
                Path2 = args[2];
        } else {
            if(arguments["routing"] == 1)
            {
                if(args[1].Split(".").Length == 4)
                {
                    Interface = args[1];
                    PhysicalAddress = UnicastAddress.FromString(args[2]);
                    Path1 = args[3];
                    if(args.Length > 4 && !args[4].StartsWith("--"))
                        Path2 = args[4];
                } else {
                    Interface = "224.0.23.12";
                    PhysicalAddress = UnicastAddress.FromString(args[1]);
                    Path1 = args[2];
                    if(args.Length > 4 && !args[3].StartsWith("--"))
                        Path2 = args[3];
                }
            } else {
                Interface = args[1];
                PhysicalAddress = UnicastAddress.FromString(args[2]);
                Path1 = args[3];
                if(args.Length > 4 && !args[4].StartsWith("--"))
                    Path2 = args[4];
            }
        }
    }

    public int Get(string name)
    {
        if(!arguments.ContainsKey(name))
            throw new Exception("Unbekanntes Argument: " + name);

        return arguments[name];
    }
}