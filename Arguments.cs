using System.Dynamic;
using System.Net;
using Kaenx.Konnect.Addresses;

namespace KnxFileTransferClient;

internal class Arguments{
    
    private static List<Argument> arguments = new List<Argument> {
        new("delay", "Delay", Argument.ArgumentType.Int, 0),
        new("pkg", "Package Size", Argument.ArgumentType.Int, 128),
        new("force", "Force", Argument.ArgumentType.Bool, false),
        new("routing", "Routing", Argument.ArgumentType.Bool, false),
        new("verbose", "Verbose", Argument.ArgumentType.Bool, false),
        new("pa", "Physical Address", Argument.ArgumentType.String, "1.1.255"),
        new("port", "Port", Argument.ArgumentType.Int, 3671),
        new("gw", "Gateway IP", Argument.ArgumentType.String, "192.168.178.2"),
        new("ga", "Gateway PA", Argument.ArgumentType.String, "1.1.0"),
        new("gs", "Routing Source Address", Argument.ArgumentType.String, "0.0.1"),
        new("config", "Konfigurationsname", Argument.ArgumentType.String, ""),
        new("no-input", "Keine Aufforderung für manuelle Eingaben", Argument.ArgumentType.Bool, false)
    };

    public UnicastAddress? PhysicalAddress { get; } = null;
    public string Interface { get; } = "";
    public string Source { get; } = "";
    public string Target { get; } = "";
    public string Command { get; } = "";
    private static readonly List<string> toSave = new() { "routing", "pa", "port", "gw", "ga", "gs", "pkg", "delay" };

    public Arguments() { }

    public Arguments(string[] args, bool isOpen = false)
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

            if(!Get<bool>("no-input"))
            {
                Console.WriteLine("Werte in Klammern sind default");
                Console.WriteLine("Bei leerer Eingabe wird default übernommen");
                bool changed = GetInputArg("routing", "Verbindung über Routing? [j/n]", "(j|n|True|False)");
                if(Get<bool>("routing"))
                {
                    if(changed)
                        Set("gw", "224.0.23.12");

                    GetInputArg("gw", "IP-Adresse des Routers", @"((25[0-5]|(2[0-4]|1\d|[1-9]|)\d)\.?\b){4}");
                    GetInputArg("ga", "PA des Routers", @"^(1[0-5]|[0-9])\.(1[0-5]|[0-9])\.(25[0-5]|2[0-4][0-9]|1[0-9]{2}|[0-9]{1,2})$");
                } else {
                    GetInputArg("gw", "IP-Adresse des Routers", @"((25[0-5]|(2[0-4]|1\d|[1-9]|)\d)\.?\b){4}");
                }
                GetInputArg("pa", "PA des Update-Geräts", @"^(1[0-5]|[0-9])\.(1[0-5]|[0-9])\.(25[0-5]|2[0-4][0-9]|1[0-9]{2}|[0-9]{1,2})$");
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

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"Verwende als source address {Get<string>("gs")}");
            Console.ResetColor();

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

    private void LoadArgs(string configName, string[] args)
    {
        string path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KnxFileTransferClient");
        if(!Directory.Exists(path))
            Directory.CreateDirectory(path);

        string toLoad = CheckConfigFile(path, configName);
        List<Argument> loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Argument>>(File.ReadAllText(toLoad));
        foreach(Argument arg in loaded)
        {
            Argument x = GetRaw(arg.Name);
            x.Value = arg.Value;
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