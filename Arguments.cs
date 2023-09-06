using System.Dynamic;
using System.Net;
using Kaenx.Konnect.Addresses;

namespace KnxFileTransferClient;

internal class Arguments{
    
    private static List<Argument> arguments = new List<Argument> {
        new("delay", "Delay", Argument.ArgumentType.Int, 0, false),
        new("pkg", "Package Size", Argument.ArgumentType.Int, 128, false),
        new("force", "Force", Argument.ArgumentType.Bool, false, false),
        new("routing", "Routing", Argument.ArgumentType.Bool, false, false),
        new("verbose", "Verbose", Argument.ArgumentType.Bool, false, false),
        new("pa", "Physical Address", Argument.ArgumentType.String, "", true),
        new("port", "Port", Argument.ArgumentType.Int, 3671, false),
        new("gw", "Gateway IP", Argument.ArgumentType.String, "", true),
        new("ga", "Gateway PA", Argument.ArgumentType.String, "", false),
        new("save", "Speichere aktuelle Konfiguration", Argument.ArgumentType.Bool, false, false),
        new("config", "Konfigurationsname", Argument.ArgumentType.String, "", false)
    };

    public UnicastAddress? PhysicalAddress { get; } = null;
    public string Interface { get; } = "";
    public string Path1 { get; } = "";
    public string Path2 { get; } = "";
    public string Command { get; } = "";

    public Arguments() { }

    public Arguments(string[] args, bool isOpen = false)
    {
        List<string> argL = new(args);
        ParseArgs(argL);
        
        if(argL.Count > 0)
            Command = argL[0];
        else
            Command = "help";

        if(Command == "close" || Command == "help")
            return;

        if(isOpen)
        {
            if(argL.Count > 1)
                Path1 = argL[1];
            
            if(argL.Count > 2)
                Path2 = argL[2];
        } else {
            if(Get<bool>("routing"))
            {
                if(string.IsNullOrEmpty(Get<string>("gw")))
                    Set("gw", "224.0.23.12");

                if(string.IsNullOrEmpty(Get<string>("ga")))
                {
                    string addrX = Get<string>("pa");
                    if(addrX == "")
                        Set("ga", "0.0.1");
                    else {
                        string[] addrP = addrX.Split(".");
                        int bl = int.Parse(addrP[0]);
                        int hl = int.Parse(addrP[1]);
                        int ta = 255;

                        if(hl == 0)
                            bl--;
                        else
                            hl--;

                        Set("ga", $"{bl}.{hl}.{ta}");
                    }
                }
            }

            if(!string.IsNullOrEmpty(Get<string>("config")))
            {
                string configName = Get<string>("config");
                string path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KnxFileTransferClient");
                if(!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                
                if(Get<bool>("save"))
                {
                    GetRequiredArguments();
                    string toSave = $"--gw {Get<string>("gw")} --port {Get<int>("port")} --pkg {Get<int>("pkg")}";
                    if(!string.IsNullOrEmpty(Get<string>("ga")))
                        toSave += $" --ga {Get<string>("ga")}";
                    if(Get<bool>("routing"))
                        toSave += " --routing";

                    File.WriteAllText(Path.Combine(path, configName), toSave);
                } else {
                    string configContent = File.ReadAllText(Path.Combine(path, configName));
                    ParseArgs(new(configContent.Split(" ")));
                    GetRequiredArguments();
                }
            } else {
                GetRequiredArguments();
            }

            Interface = Get<string>("gw");
            PhysicalAddress = UnicastAddress.FromString(Get<string>("pa"));

            if(argL.Count > 1)
                Path1 = argL[1];
            
            if(argL.Count > 2)
                Path2 = argL[2];
        }
    }

    private void GetRequiredArguments()
    {
        foreach(Argument arg in arguments.Where(a => a.Required))
        {
            switch(arg.Type)
            {
                case Argument.ArgumentType.String:
                {
                    if(string.IsNullOrEmpty(ConvertTo<string>(arg.Value)))
                        GetRequiredArgument(arg);
                    break;
                }

                default:
                    throw new Exception("Unbekannter Argumenttyp: " + arg.Type.ToString());
            }
        }
    }

    private void ParseArgs(List<string> argL)
    {
        while(argL.Any(a => a.StartsWith("--")))
        {
            string argstr = argL.First(a => a.StartsWith("--")).Substring(2);
            int index = argL.IndexOf("--" + argstr);
            Argument? arg = arguments.SingleOrDefault(a => a.Name == argstr);

            if(arg == null)
            {
                Console.WriteLine("Unbekanntes Argument: " + argstr);
            } else {
                switch(arg.Type)
                {
                    case Argument.ArgumentType.Int:
                    {
                        arg.Value = int.Parse(argL[index + 1]);
                        break;
                    }

                    case Argument.ArgumentType.Bool:
                    {
                        arg.Value = true;
                        break;
                    }

                    case Argument.ArgumentType.String:
                    {
                        arg.Value = argL[index + 1];
                        break;
                    }
                }
            }

            argL.RemoveAt(index);
            if(arg?.Type != Argument.ArgumentType.Bool)
                argL.RemoveAt(index);
        }
    }

    private void GetRequiredArgument(Argument arg)
    {
        switch(arg.Name)
        {
            case "gw":
            {
                Console.Write("Bitte IP des Gateways eingeben: ");
                string input = Console.ReadLine() ?? "";
                arg.Value = input;
                break;
            }
            
            case "pa":
            {
                Console.Write("Bitte PA des Zielgerätes eingeben: ");
                string input = Console.ReadLine() ?? "";
                arg.Value = input;
                break;
            }

            default:
                throw new Exception("Unbekanntes Argument: " + arg.Name);
        }
    }

    public List<Argument> GetArguments()
    {
        return arguments;
    }

    public void Set(string name, object value)
    {
        Argument? arg = arguments.SingleOrDefault(a => a.Name == name);
        if(arg == null)
            throw new Exception("Kein Argument mit dem Namen gefunden: " + name);
        arg.Value = value;
    }

    public T Get<T>(string name)
    {
        Argument? arg = arguments.SingleOrDefault(a => a.Name == name);
        if(arg == null)
            throw new Exception("Kein Argument mit dem Namen gefunden: " + name);

        return ConvertTo<T>(arg.Value);
    }

    private T ConvertTo<T>(object value)
    {
        return (T)Convert.ChangeType(value, typeof(T));
    }
}