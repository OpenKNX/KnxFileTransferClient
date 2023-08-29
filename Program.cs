using System.Text;
using Kaenx.Konnect.Messages.Response;
using KnxFileTransferClient.Lib;

namespace KnxFileTransferClient;


class Program
{    
    private static Kaenx.Konnect.Connections.KnxIpTunneling? conn = null;
    private static Kaenx.Konnect.Classes.BusDevice? device = null;
    private static FileTransferClient? client = null;

    static async Task<int> Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        var version = typeof(Program).Assembly.GetName().Version;
        var versionString = "";
        if (version != null) 
            versionString = string.Format(" {0}.{1}.{2}", version.Major, version.Minor, version.Build);
        Console.WriteLine("Willkommen zum KnxFileTransferClient{0}!!", versionString);
        Console.WriteLine();
        Console.ResetColor();

        if(args.Length == 0 || args[0] == "help")
            return help();

        Arguments arguments = new Arguments(args);
        
        
        var startTime = DateTime.Now;
        int code = -2;

        try
        {
            conn = new Kaenx.Konnect.Connections.KnxIpTunneling(arguments.Interface, arguments.Get("port"));
            await conn.Connect();
            Console.WriteLine("Info:  Verbindung zum Bus hergestellt");
            device = new Kaenx.Konnect.Classes.BusDevice(arguments.PhysicalAddress, conn);
            await device.Connect();
            Console.WriteLine($"Info:  Verbindung zum KNX-Gerät {args[1]} hergestellt");
            client = new FileTransferClient(device);

            bool isOpen = false;
            do
            {
                if(isOpen)
                {
                    await device.Disconnect();
                    Console.WriteLine("Info:  Neuen Befehl eingeben:");
                    string[] args2 = Console.ReadLine().Split(" ");
                    arguments = new Arguments(args2, true);
                    await device.Connect();
                }

                switch(arguments.Command)
                {
                    case "help":
                        code = help();
                        break;
                    case "format":
                        await format(arguments);
                        break;
                    case "exists":
                        await exists(arguments);
                        break;
                    case "rename":
                        await rename(arguments);
                        break;
                    case "upload":
                        await upload(arguments);
                        break;
                    case "download":
                        await download(arguments);
                        break;
                    case "delete":
                        await delete(arguments);
                        break;
                    case "list":
                        await list(arguments);
                        break;
                    case "mkdir":
                        await mkdir(arguments);
                        break;
                    case "rmdir":
                        await rmdir(arguments);
                        break;
                    case "open":
                    {
                        Console.WriteLine("Info:  Verbindung wurde geöffnet");
                        isOpen = true;
                        break;
                    }
                    case "close":
                    {
                        code = 0;
                        isOpen = false;
                        break;
                    }
                }
            } while(isOpen);
        } catch(FileTransferException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: " + ex.Message);
            Console.ResetColor();
            return ex.ErrorCode;
        } catch(Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: " + ex.Message);
            Console.ResetColor();
            return -1;
        }

        if(device != null)
            await device.Disconnect();
        if(conn != null)
            await conn.Disconnect();


        return code;
    }

    private static int help()
    {
        Arguments args = new Arguments();
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"KnxFileTransferClient <Command> <IP-Address> <PhysicalAddress> <Source?> <Target?> (--port={args.Get("port")} --delay={args.Get("delay")} --pkg={args.Get("pkg")} --errors={args.Get("errors")})");
        Console.ResetColor();
        Console.WriteLine($"In Session:");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"<Command> <Source?> <Target?> (--port={args.Get("port")} --delay={args.Get("delay")} --pkg={args.Get("pkg")} --errors={args.Get("errors")})");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("Command:         Command to execute");
        Console.WriteLine("                 format/exists/rename/upload/download/list/mkdir/rmdir/open/close");
        Console.WriteLine("IP-Address:      IP of the KNX-IP-interface");
        Console.WriteLine("PhysicalAddress: Address of the KNX-Device (1.2.120)");
        Console.WriteLine("Source*:         Path to the file on the host");
        Console.WriteLine("Target**:        Path to the file on the knx device");
        Console.WriteLine($"Port:            Optional - Port of the KNX-IP-interface ({args.Get("port")})");
        Console.WriteLine($"Delay:           Optional - Delay after each telegram ({args.Get("delay")} ms)");
        Console.WriteLine($"Package (pkg):   Optional - data size to transfer in one telegram ({args.Get("pkg")} bytes)");
        Console.WriteLine($"Errors:          Optional - Max count of errors before abort update ({args.Get("errors")})");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("*  only at command upload/download");
        Console.WriteLine("** only at command exists/rename/upload/download/list/mkdir/rmdir");
        Console.WriteLine();
        Console.WriteLine("Open  = Session Start");
        Console.WriteLine("Close = Session End");
        Console.ResetColor();
        return 0;
    }

    private static async Task format(Arguments args)
    {
        Console.WriteLine("Info:  Dateisystem wird formatiert");
        await client.Format();
        Console.WriteLine("Info:  Dateisystem erfolgreich formatiert");
    }
    
    private static async Task exists(Arguments args)
    {
        Console.WriteLine("Info:  Exists - " + args.Path1);
        bool exists = await client.Exists(args.Path1);
        Console.WriteLine("Info:  Existiert" + (exists ? "":" nicht"));
    }

    private static async Task rename(Arguments args)
    {
        Console.WriteLine("Info:  Umbenennen - " + args.Path1 + " in " + args.Path2);
        await client.Rename(args.Path1, args.Path2);
        Console.WriteLine("Info:  Umbenennen erfolgreich");
    }

    private static async Task upload(Arguments args)
    {
        Console.WriteLine("Info:  Datei hochladen - von " + args.Path1 + " in " + args.Path2);
        await client.FileUpload(args.Path1, args.Path2, args.Get("pkg"));
        Console.WriteLine("Info:  Datei hochladen abgeschlossen");
    }

    private static async Task download(Arguments args)
    {
        Console.WriteLine("Info:  Datei runterladen - in " + args.Path1 + " von " + args.Path2);
        await client.FileUpload(args.Path1, args.Path2, args.Get("pkg"));
        Console.WriteLine("Info:  Datei runterladen abgeschlossen");
    }

    private static async Task delete(Arguments args)
    {
        Console.WriteLine("Info:  Datei löschen - " + args.Path1);
        await client.FileDelete(args.Path1);
        Console.WriteLine("Info:  Datei erfolgreich gelöscht");
    }

    private static async Task list(Arguments args)
    {
        Console.WriteLine("Info:  Ordner auflisten - " + args.Path1);
        List<FileTransferPath> list = await client.List(args.Path1);
        foreach(FileTransferPath path in list)
            Console.WriteLine($"        - {(path.IsFile ? "Datei ":"Ordner")} {path.Name}");
    }
    
    private static async Task mkdir(Arguments args)
    {
        Console.WriteLine("Info:  Ordner erstellen - " + args.Path1);
        await client.DirCreate(args.Path1);
        Console.WriteLine("Info:  Ordner erfolgreich erstellt");
    }
    
    private static async Task rmdir(Arguments args)
    {
        Console.WriteLine("Info:  Ordner löschen - " + args.Path1);
        await client.DirDelete(args.Path1);
        Console.WriteLine("Info:  Ordner erfolgreich gelöscht");
    }
}