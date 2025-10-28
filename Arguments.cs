using System.Dynamic;
using System.Net;
using System.Net.NetworkInformation;
using Kaenx.Konnect;
using Kaenx.Konnect.Addresses;
using Kaenx.Konnect.Connections;
using Kaenx.Konnect.Telegram.Contents;
using Kaenx.Konnect.Telegram.IP;
using Kaenx.Konnect.Telegram.IP.DIB;

namespace KnxFileTransferClient;

internal class Arguments{
    private static List<Argument> arguments = new List<Argument> {
        new("delay", "Verzögerung", Argument.ArgumentType.Int, 0),
        new("pkg", "Telegrammgröße", Argument.ArgumentType.Int, 128),
        new("force", "Erzwingen", Argument.ArgumentType.Bool, false),
        new("connect", "Verbindung", Argument.ArgumentType.Enum, Argument.ConnectionType.search),
        new("verbose", "Verbose", Argument.ArgumentType.Bool, false),
        new("pa", "Physikalische Adresse", Argument.ArgumentType.String, "1.1.255", true),
        new("port", "Port", Argument.ArgumentType.Int, 3671),
        new("gw", "Gateway IP", Argument.ArgumentType.String, "192.168.178.2", true),
        new("ga", "Gateway PA", Argument.ArgumentType.String, "1.1.0", true),
        new("gs", "Routing Source Address", Argument.ArgumentType.String, "0.0.1", true),
        new("config", "Konfigurationsname", Argument.ArgumentType.String, "default"),
        new("interactive", "Alle Argumente müssen vom Benutzer eingegeben werden", Argument.ArgumentType.Bool, false),
        new("no-resume", "Vorhandene Dateien werden immer komplett neu übertragen", Argument.ArgumentType.Bool, false),
        new("device-timeout", "Timeout in dem das Gerät eine Antwort schicken muss", Argument.ArgumentType.Int, 4000)
    };

    public UnicastAddress? PhysicalAddress { get; private set; } = null;
    public string Interface { get; private set; } = "";
    public string Source { get; private set; } = "";
    public string Target { get; private set; } = "";
    public string Command { get; private set; } = "";
    public bool IsRouting { get; private set; } = false;
    private static readonly List<string> toSave = new() { "connect", "pa", "port", "gw", "ga", "gs" };

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

        if(Command == "close" || Command == "help" || Command == "version")
            return;

        if(Command == "upload" || Command == "fwupdate")
        {
            if(!System.IO.File.Exists(argL[1]))
                throw new Exception($"Quelldatei existiert nicht. ({argL[1]})");
        }

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
                GetInputArg("pa", "PA des Geräts", @"^(1[0-5]|[0-9])\.(1[0-5]|[0-9])\.(25[0-5]|2[0-4][0-9]|1[0-9]{2}|[0-9]{1,2})$");

            if(!GetRaw("connect").WasSet)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("(Auto|Search|Tunneling|Routing)");
                Console.ResetColor();
                GetInputArg("connect", "Verbindungstyp", @"(auto|search|tunneling|routing|a|s|t|r)", "search");
            }

            switch(Get<Argument.ConnectionType>("connect"))
            {
                case Argument.ConnectionType.auto:
                    if(await SearchGateways(true) == false)
                        throw new Exception("Keine Verbindung gefunden");
                    break;

                case Argument.ConnectionType.search:
                    if(await SearchGateways() == false)
                        throw new Exception("Keine verbindung gefunden");
                    break;

                case Argument.ConnectionType.tunneling:
                    if(!GetRaw("gw").WasSet)
                        GetInputArg("gw", "IP-Adresse der Schnittstelle", @"((25[0-5]|(2[0-4]|1\d|[1-9]|)\d)\.?\b){4}");
                    if(!GetRaw("port").WasSet)
                        GetInputArg("port", "IP-Port der Schnittstelle", @"[0-9]{1,6}");
                    break;

                case Argument.ConnectionType.routing:
                    if(!GetRaw("gw").WasSet)
                        GetInputArg("gw", "Multicast Adresse", @"((25[0-5]|(2[0-4]|1\d|[1-9]|)\d)\.?\b){4}");
                    if(!GetRaw("ga").WasSet)
                        GetInputArg("ga", "PA des Routers", @"^(1[0-5]|[0-9])\.(1[0-5]|[0-9])\.(25[0-5]|2[0-4][0-9]|1[0-9]{2}|[0-9]{1,2})$");
                    IsRouting = true;
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

    private async Task<bool> SearchGateways(bool isAuto = false)
    {
        List<Connection> gateways = new();
        HashSet<string> uniquePhysicalAddresses = new HashSet<string>();
        //Kaenx.Konnect.Connections.KnxIpSearch search = new();
        int counter = 1;
        object lockObject = new object(); // Object for synchronization

        IpKnxConnection _conn = KnxFactory.CreateTunnelingUdp(new(IPAddress.Parse("224.0.23.12"), 3671));

        _conn.OnReceivedService += (IpTelegram message) =>
        {
            lock(lockObject)
            {
                if (message.ServiceIdentifier != Kaenx.Konnect.Enums.ServiceIdentifiers.SearchResponse)
                    return;
                SearchResponse? response = message as SearchResponse;
                if (response == null)
                    return;

                HpaiContent? hpai = response.GetEndpoint();
                DeviceInfo? deviceInfo = response.GetDeviceInfo();
                SupportedServiceFamilies? svcFamilies = response.GetSupportedServiceFamilies();
                if (hpai == null || deviceInfo == null || svcFamilies == null)
                    return;

                if(deviceInfo.Medium != Kaenx.Konnect.Enums.KnxMediums.TP1)
                    return;

                if (uniquePhysicalAddresses.Add(deviceInfo.UnicastAddress.ToString()))
                {
                    if (svcFamilies.GetServiceFamilyVersion(Kaenx.Konnect.Enums.ServiceFamilies.Tunneling) > 0)
                    {
                        int tunnelingVersion = svcFamilies.GetServiceFamilyVersion(Kaenx.Konnect.Enums.ServiceFamilies.Tunneling);
                        Console.WriteLine($"{counter,2} Tunneling v{tunnelingVersion} -> {hpai.Endpoint,-20} ({deviceInfo.UnicastAddress,-9}) [{deviceInfo.FriendlyName}]");
                        Connection conn = new(hpai.Endpoint)
                        {
                            FriendlyName = deviceInfo.FriendlyName,
                            PhysicalAddress = deviceInfo.UnicastAddress,
                            Version = tunnelingVersion
                        };
                        gateways.Add(conn);
                        counter++;
                    }

                    if (svcFamilies.GetServiceFamilyVersion(Kaenx.Konnect.Enums.ServiceFamilies.Routing) > 0)
                    {
                        int routingVersion = svcFamilies.GetServiceFamilyVersion(Kaenx.Konnect.Enums.ServiceFamilies.Routing);
                        Console.WriteLine($"{counter,2} Routing   v{routingVersion} -> {hpai.Endpoint,-20} ({deviceInfo.UnicastAddress,-9}) [{deviceInfo.FriendlyName}]");
                        Connection conn = new(hpai.Endpoint)
                        {
                            IsRouting = true,
                            FriendlyName = deviceInfo.FriendlyName,
                            PhysicalAddress = deviceInfo.UnicastAddress,
                            Version = routingVersion
                        };
                        gateways.Add(conn);
                        counter++;
                    }
                }
            }
        };

        SearchRequest req = new SearchRequest(_conn.GetLocalEndpoint());
        await _conn.SendAsync(req);

        await Task.Delay(500);
        _conn.Dispose();

        Console.WriteLine($"Es wurden {gateways.Count} Gateways gefunden");
        if(gateways.Count == 0)
            return false;

        string phaddr = Get<string>("pa");
        phaddr = phaddr.Substring(0, phaddr.LastIndexOf('.'));
        if(isAuto)
        {
            Connection? conn = gateways.FirstOrDefault(g => g.PhysicalAddress.ToString().StartsWith(phaddr));
            if(conn == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Es konnte keine Verbindung automatisch gefunden werden.");
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
                string input = Console.ReadLine() ?? "";
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
        return true;
    }

    private void LoadArgs(string configName, string[] args)
    {
        string path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenKNX", "KnxFileTransferClient");
        if(!Directory.Exists(path))
            Directory.CreateDirectory(path);

        string toLoad = CheckConfigFile(path, configName);
        List<Argument> loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Argument>>(File.ReadAllText(toLoad)) ?? [];
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
        string path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenKNX", "KnxFileTransferClient");
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

                    case Argument.ArgumentType.Enum:
                    {
                        arg.Value = (Argument.ConnectionType)Enum.Parse<Argument.ConnectionType>(argL[index + 1].ToLower());
                        break;
                    }
                }
            }

            argL.RemoveAt(index);
            if(arg?.Type != Argument.ArgumentType.Bool)
                argL.RemoveAt(index);
        }
    }

    private bool GetInputArg(string name, string input, string regex, string defaultVal = "")
    {
        Argument arg = GetRaw(name);
        string? answer;

        do {
            Console.Write(input);
            if(!string.IsNullOrEmpty(defaultVal))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($" ({defaultVal})");
                Console.ResetColor();
            }
            Console.Write(": ");
            
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

            answer = Console.ReadLine()?.ToLower();
            if(string.IsNullOrEmpty(answer) && !string.IsNullOrEmpty(defaultVal))
                answer = defaultVal;
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
                Argument.ConnectionType connectionType;
                if(!Enum.TryParse<Argument.ConnectionType>(answer, out connectionType))
                {
                    foreach(string ename in Enum.GetNames<Argument.ConnectionType>())
                    {
                        if(answer != null && ename.StartsWith(answer))
                        {
                            connectionType = Enum.Parse<Argument.ConnectionType>(ename);
                            break;
                        }
                    }
                }
                arg.Value = connectionType;
                break;
        }

        return response;
    }

    public bool GetWasSet(string name)
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