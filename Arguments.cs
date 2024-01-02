using System.Dynamic;
using System.Net;
using Kaenx.Konnect.Addresses;

namespace KnxFileTransferClient;

internal class Arguments{
    
    private static List<Argument> arguments = new List<Argument> {
        new("delay", "Delay", Argument.ArgumentType.Int, 0),
        new("pkg", "Package Size", Argument.ArgumentType.Int, 128),
        new("force", "Force", Argument.ArgumentType.Bool, false),
        new("connect", "Verbindung", Argument.ArgumentType.Enum, Argument.ConnectionType.Search),
        new("verbose", "Verbose", Argument.ArgumentType.Bool, false),
        new("pa", "Physical Address", Argument.ArgumentType.String, "1.1.255", true) { Question = "PA des Geräts", Regex = @"^(1[0-5]|[0-9])\.(1[0-5]|[0-9])\.(25[0-5]|2[0-4][0-9]|1[0-9]{2}|[0-9]{1,2})$"},
        new("port", "Port", Argument.ArgumentType.Int, 3671),
        new("gw", "Gateway IP", Argument.ArgumentType.String, "192.168.178.2", true),
        new("ga", "Gateway PA", Argument.ArgumentType.String, "1.1.0", true),
        new("gs", "Routing Source Address", Argument.ArgumentType.String, "0.0.1", true),
        new("config", "Konfigurationsname", Argument.ArgumentType.String, ""),
        new("interactive", "All arguments need to be set by user", Argument.ArgumentType.Bool, false)
    };

    public UnicastAddress? PhysicalAddress { get; private set; } = null;
    public string Interface { get; private set; } = "";
    public string Source { get; private set; } = "";
    public string Target { get; private set; } = "";
    public string Command { get; private set; } = "";
    public bool IsRouting { get; private set; } = false;
    private static readonly List<string> toSave = new() { "connect", "port", "gw", "ga", "gs", "pkg", "delay" };

    public async Task Init(string[] args, bool isOpen = false)
    {
        List<string> argL = new(args);
        string configName = GetConfigArg(args);
        LoadArgs(configName, args);
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
                Source = argL[1];
            
            if(argL.Count > 2)
                Target = argL[2];
        } else {
            Console.WriteLine("Werte in Klammern sind default");
            Console.WriteLine("Bei leerer Eingabe wird default übernommen");
            
            if(Get<bool>("interactive"))
                foreach(Argument arg in arguments)
                    arg.WasSet = false;

            if(!GetRaw("pa").WasSet)
                GetInputArg("pa", "PA des Update-Geräts", @"^(1[0-5]|[0-9])\.(1[0-5]|[0-9])\.(25[0-5]|2[0-4][0-9]|1[0-9]{2}|[0-9]{1,2})$");

            if(!GetRaw("connect").WasSet)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("(Auto|Search|Tunneling|Routing)");
                Console.ResetColor();
                GetInputArg("connect", "Verbindungstyp: ", @"(Auto|Search|Tunneling|Routing)");
            }

            switch(Get<Argument.ConnectionType>("connect"))
            {
                case Argument.ConnectionType.Auto:
                    await SearchGateways(true);
                    break;

                case Argument.ConnectionType.Search:
                    await SearchGateways();
                    break;

                case Argument.ConnectionType.Tunneling:
                    if(!GetRaw("gw").WasSet)
                        GetInputArg("gw", "IP-Adresse der Schnittstelle", @"((25[0-5]|(2[0-4]|1\d|[1-9]|)\d)\.?\b){4}");
                    break;

                case Argument.ConnectionType.Routing:
                    if(!GetRaw("gw").WasSet)
                        GetInputArg("gw", "IP-Adresse des Routers", @"((25[0-5]|(2[0-4]|1\d|[1-9]|)\d)\.?\b){4}");
                    if(!GetRaw("ga").WasSet)
                        GetInputArg("ga", "PA des Routers", @"^(1[0-5]|[0-9])\.(1[0-5]|[0-9])\.(25[0-5]|2[0-4][0-9]|1[0-9]{2}|[0-9]{1,2})$");
                    break;
            }
            
            if(!GetWasSet("gs"))
            {
                string[] addrP = Get<string>("ga").Split(".");
                int bl = int.Parse(addrP[0]);
                int hl = int.Parse(addrP[1]);
                int ta = 255;

                if(hl == 0)
                    bl--;
                else
                    hl--;
                Set("gs", $"{bl}.{hl}.{ta}");
            }

            if(IsRouting)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"Verwende als source address {Get<string>("gs")}");
                Console.ResetColor();
            }

            SaveArgs(configName);

            Interface = Get<string>("gw");
            PhysicalAddress = UnicastAddress.FromString(Get<string>("pa"));

            if(argL.Count > 1)
                Source = argL[1];
            
            if(argL.Count > 2)
                Target = argL[2];

            Console.WriteLine();
        }
    }

    private async Task SearchGateways(bool isAuto = false)
    {
        List<Connection> gateways = new();
        Kaenx.Konnect.Connections.KnxIpTunneling tunnel = new ("224.0.23.12", 3671, true);
        int counter = 1;
        tunnel.OnSearchResponse += (Kaenx.Konnect.Messages.Response.MsgSearchRes message) => {
            if(message.SupportedServiceFamilies.Any(s => s.ServiceFamilyType == ServiceFamilyTypes.Tunneling))
            {
                Console.WriteLine($"{counter++:D2} Tunneling -> {message.Endpoint.ToString()} \t({message.PhAddr}) [{message.FriendlyName}]");
                gateways.Add(new() { IPAddress = message.Endpoint, FriendlyName = message.FriendlyName, PhysicalAddress = message.PhAddr});
            }
            if(message.SupportedServiceFamilies.Any(s => s.ServiceFamilyType == ServiceFamilyTypes.Routing))
            {
                Console.WriteLine($"{counter++:D2} Routing   -> {message.Multicast.ToString()} \t({message.PhAddr}) [{message.FriendlyName}]");
                gateways.Add(new() { IsRouting = true, IPAddress = message.Multicast, FriendlyName = message.FriendlyName, PhysicalAddress = message.PhAddr});
            }
        };
        await tunnel.Send(new Kaenx.Konnect.Messages.Request.MsgSearchReq(), true);

        await Task.Delay(4000);
        Console.WriteLine($"Es wurden {gateways.Count} Gateways gefunden");

        string phaddr = Get<string>("pa");
        phaddr = phaddr.Substring(0, phaddr.LastIndexOf('.'));
        if(isAuto)
        {
            Connection conn = gateways.FirstOrDefault(g => g.PhysicalAddress.ToString().StartsWith(phaddr));
            if(conn == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Es konnte keine verbindung automatisch gefunden werden.");
                Console.ResetColor();
                isAuto = false;
            } else {
                Set("gw", conn.IPAddress.Address.ToString());
                Set("ga", conn.PhysicalAddress.ToString());
                Set("port", conn.IPAddress.Port.ToString());
                IsRouting = conn.IsRouting;
            }
        }
        
        if(!isAuto)
        {
            int selected = 0;
            do
            {
                Console.Write("Gateway Auswählen (Index): ");
                string input = Console.ReadLine();
                if(!int.TryParse(input, out selected))
                    selected = 0;
            } while(selected < 1 || selected > gateways.Count);
            Connection conn = gateways[selected-1];
            if(!conn.PhysicalAddress.ToString().StartsWith(phaddr))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Die Verbindung funktioniert möglicherweise nicht, da die Linien unterschiedlich sind.");
                Console.ResetColor();
            }
            Set("gw", conn.IPAddress.Address.ToString());
            Set("ga", conn.PhysicalAddress.ToString());
            Set("port", conn.IPAddress.Port.ToString());
            IsRouting = conn.IsRouting;
        }
    }

    private void LoadArgs(string configName, string[] args)
    {
        string path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KnxFileTransferClient");
        if(!Directory.Exists(path))
            Directory.CreateDirectory(path);

        string toLoad = CheckConfigFile(path, configName);
        List<Argument> loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Argument>>(File.ReadAllText(toLoad));
        foreach(Argument arg in loaded)
        {
            try{
                Argument x = GetRaw(arg.Name);
                x.Value = arg.Value;
            } catch {}
        }
    }

    private void SaveArgs(string configName)
    {
        string path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KnxFileTransferClient");
        List<Argument> argsDef = new();

        foreach(Argument arg in arguments)
            if(toSave.Contains(arg.Name))
                argsDef.Add(arg);

        string def = Newtonsoft.Json.JsonConvert.SerializeObject(argsDef);
        File.WriteAllText(Path.Combine(path, configName), def);
    }

    private string CheckConfigFile(string path, string configName)
    {
        if(!File.Exists(Path.Combine(path, configName)))
        {
            if(configName != "default")
            {
                configName = "default";
                return CheckConfigFile(path, configName);
            } else {
                List<Argument> argsDef = new();

                foreach(Argument arg in arguments)
                    if(toSave.Contains(arg.Name))
                        argsDef.Add(arg);

                string def = Newtonsoft.Json.JsonConvert.SerializeObject(argsDef);
                File.WriteAllText(Path.Combine(path, configName), def);
            }
        }
        return Path.Combine(path, configName);
    }

    private string GetConfigArg(string[] args)
    {
        string? argument = args.FirstOrDefault(a => a == "--config");
        if(argument == null) return "default";
        
        int index = Array.IndexOf(args, "--config");
        return args[index+1];
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
                arg.WasSet = true;
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

    private bool GetInputArg(string name, string input, string regex)
    {
        Argument arg = GetRaw(name);
        string? answer;

        do {
            Console.Write(input + " ");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            switch(arg.Type)
            {
                case Argument.ArgumentType.Bool:
                    Console.Write("(" + (Get<bool>(name) ? "j" : "n") +"): ");
                    break;

                case Argument.ArgumentType.Int:
                    Console.Write($"({arg.Value}): ");
                    break;

                case Argument.ArgumentType.String:
                    Console.Write($"({arg.Value}): ");
                    break;
            }
            Console.ResetColor();

            answer = Console.ReadLine();
            if(string.IsNullOrEmpty(answer))
                answer = arg.Value.ToString();
        } while(!CheckInput(answer, regex));


        bool response = false;

        switch(arg.Type)
        {
            case Argument.ArgumentType.Bool:
            {
                bool temp = answer == "j" || answer == "True";
                response = temp != Get<bool>(name);
                arg.Value = temp;
                break;
            }

            case Argument.ArgumentType.Int:
            {
                int temp = int.Parse(answer ?? "0");
                response = temp != Get<int>(name);
                arg.Value = temp;
                break;
            }

            case Argument.ArgumentType.String:
                response = answer != Get<string>(name);
                arg.Value = answer ?? "";
                break;

            case Argument.ArgumentType.Enum:
                arg.Value = Enum.Parse<Argument.ConnectionType>(answer);
                break;
        }

        return response;
    }

    private bool GetWasSet(string name)
    {
        Argument? arg = arguments.SingleOrDefault(a => a.Name == name);
        if(arg == null)
            throw new Exception("Kein Argument mit dem Namen gefunden: " + name);
        return arg.WasSet;
    }

    private bool CheckInput(string? answer, string regex)
    {
        if(string.IsNullOrEmpty(answer)) return false;

        System.Text.RegularExpressions.Regex reg = new(regex);
        if(!reg.IsMatch(answer)) return false;

        return true;
    }

    public List<Argument> GetArguments()
    {
        return arguments;
    }

    private Argument GetRaw(string name)
    {
        Argument? arg = arguments.SingleOrDefault(a => a.Name == name);
        if(arg == null)
            throw new Exception("Kein Argument mit dem Namen gefunden: " + name);
        return arg;
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