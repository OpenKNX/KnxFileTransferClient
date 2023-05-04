using System.Text;
using Kaenx.Konnect.Messages.Response;

namespace KnxFtpClient;


class Program
{    
    private static Kaenx.Konnect.Connections.KnxIpTunneling? conn = null;
    private static Kaenx.Konnect.Classes.BusDevice? device = null;

    private enum FtpCommands
    {
        Format,
        Exists,
        Rename,
        FileUpload = 40,
        FileDownload,
        FileDelete,
        DirList = 80,
        DirCreate,
        DirDelete
    }

    static async Task<int> Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        var version = typeof(Program).Assembly.GetName().Version;
        var versionString = "";
        if (version != null) 
            versionString = string.Format(" {0}.{1}.{2}", version.Major, version.Minor, version.Build);
        Console.WriteLine("Willkommen zum KnxFtpClient{0}!!", versionString);
        Console.WriteLine();
        Console.ResetColor();

        if(args.Length == 0 || args[0] == "help")
            return help();

        Arguments arguments = new Arguments(args);
        
        
        var startTime = DateTime.Now;
        int code = -2;

        try
        {
            //conn = new Kaenx.Konnect.Connections.KnxIpTunneling(arguments.Interface, arguments.Get("port"));
            //await conn.Connect();
            Console.WriteLine("Info:  Verbindung zum Bus hergestellt");
            //device = new Kaenx.Konnect.Classes.BusDevice(arguments.PhysicalAddress, conn);
            //await device.Connect();
            Console.WriteLine($"Info:  Verbindung zum KNX-Gerät {args[1]} hergestellt");

            bool isOpen = false;
            do
            {
                if(isOpen)
                {
                    Console.WriteLine("Info:  Neuen Befehl eingeben:");
                    string[] args2 = Console.ReadLine().Split(" ");
                    arguments = new Arguments(args2, true);
                }

                switch(arguments.Command)
                {
                    case "help":
                        code = help();
                        break;
                    case "format":
                        code = await format(arguments);
                        break;
                    case "exists":
                        code = await exists(arguments);
                        break;
                    case "rename":
                        code = await rename(arguments);
                        break;
                    case "upload":
                        code = await upload(arguments);
                        break;
                    case "download":
                        code = await download(arguments);
                        break;
                    case "list":
                        code = await list(arguments);
                        break;
                    case "mkdir":
                        code = await mkdir(arguments);
                        break;
                    case "rmdir":
                        code = await rmdir(arguments);
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
        } catch(FtpException ex)
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
        Console.WriteLine($"KnxFtpClient <Command> <IP-Address> <PhysicalAddress> <Source?> <Target?> (--port={args.Get("port")} --delay={args.Get("delay")} --pkg={args.Get("pkg")} --errors={args.Get("errors")})");
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

    private static async Task<int> format(Arguments args)
    {
        Console.WriteLine("Info:  Dateisystem wird formatiert");
        MsgFunctionPropertyStateRes res = await device.InvokeFunctionProperty(159, (byte)FtpCommands.Format, null, true);

        if(res.Data[0] == 0x00)
        {
            Console.WriteLine("Info:  Dateisystem wurde erfolgreich formatiert");
            return 0;
        }
        
        throw new FtpException(res.Data[0]);
    }
    
    private static async Task<int> exists(Arguments args)
    {
        Console.WriteLine("Info:  Exists - " + args.Path1);
        byte[] buffer = UTF8Encoding.UTF8.GetBytes(args.Path1 + char.MinValue);
        MsgFunctionPropertyStateRes res = await device.InvokeFunctionProperty(159, (byte)FtpCommands.Exists, buffer, true);

        if(res.Data[0] == 0x00)
        {
            if(res.Data[1] == 0x00)
                Console.WriteLine("Info:  Existiert nicht");
            else
                Console.WriteLine("Info:  Existiert");
            return 0;
        }
        
        throw new FtpException(res.Data[0]);
    }

    private static async Task<int> rename(Arguments args)
    {
        Console.WriteLine("Info:  Umbenennen - " + args.Path1 + " in " + args.Path2);
        List<byte> data = new List<byte>();
        data.AddRange(UTF8Encoding.UTF8.GetBytes(args.Path1 + char.MinValue));
        data.AddRange(UTF8Encoding.UTF8.GetBytes(args.Path2 + char.MinValue));
        MsgFunctionPropertyStateRes res = await device.InvokeFunctionProperty(159, (byte)FtpCommands.Exists, data.ToArray(), true);

        if(res.Data[0] == 0x00)
        {
            Console.WriteLine("Info:  Umbenennen erfolgreich");
            return 0;
        }
        
        throw new FtpException(res.Data[0]);
    }

    private static async Task<int> upload(Arguments args)
    {
        short sequence = 0;
        Console.WriteLine("Info:  Datei runterladen - " + args.Path1 + " in " + args.Path2);
        List<byte> data = new List<byte>();
        data.AddRange(BitConverter.GetBytes(sequence));
        data.Add((byte)(args.Get("pkg")));
        data.AddRange(UTF8Encoding.UTF8.GetBytes(args.Path2 + char.MinValue));
        MsgFunctionPropertyStateRes res = await device.InvokeFunctionProperty(159, (byte)FtpCommands.FileUpload, data.ToArray(), true);
        sequence++;

        FileStream file = File.Open(args.Path1, FileMode.Open);

        while(true)
        {
            if(res.Data[0] != 0x00)
                throw new FtpException(res.Data[0]);

            byte[] buffer = new byte[args.Get("pkg") - 5];
            int readed = file.Read(buffer, 0, args.Get("pkg") - 5);

            data.Clear();
            data.AddRange(BitConverter.GetBytes(sequence));
            data.AddRange(buffer);

            res = await device.InvokeFunctionProperty(159, (byte)FtpCommands.FileDownload, data.ToArray(), true);
            sequence++;
        }
    }

    private static async Task<int> download(Arguments args)
    {
        short sequence = 0;
        Console.WriteLine("Info:  Datei runterladen - in " + args.Path1 + " von " + args.Path2);
        List<byte> data = new List<byte>();
        data.AddRange(BitConverter.GetBytes(sequence));
        data.Add((byte)(args.Get("pkg")));
        data.AddRange(UTF8Encoding.UTF8.GetBytes(args.Path2 + char.MinValue));
        MsgFunctionPropertyStateRes res = await device.InvokeFunctionProperty(159, (byte)FtpCommands.FileDownload, data.ToArray(), true);
        sequence++;

        FileStream file = File.Open(args.Path1, FileMode.OpenOrCreate);


        while(true)
        {
            if(res.Data[0] != 0x00)
                throw new FtpException(res.Data[0]);

            /*if(res.Data.Length - 5 == 0)
            {
                file.Flush();
                file.Close();
                file.Dispose();
                return 0;
            }*/

            file.Write(res.Data, 3, res.Data.Length - 5);

            if(res.Data.Length - 5 < args.Get("pkg"))
            {
                file.Flush();
                file.Close();
                file.Dispose();
                return 0;
            }


            res = await device.InvokeFunctionProperty(159, (byte)FtpCommands.FileDownload, BitConverter.GetBytes(sequence), true);
            sequence++;
        }
    }

    private static async Task<int> list(Arguments args)
    {
        Console.WriteLine("Info:  Ordner auflisten - " + args.Path1);
        byte[] data = UTF8Encoding.UTF8.GetBytes(args.Path1 + char.MinValue);
        MsgFunctionPropertyStateRes res = await device.InvokeFunctionProperty(159, (byte)FtpCommands.DirList, data, true);

        while(true)
        {
            if(res.Data[0] != 0x00)
                throw new FtpException(res.Data[0]);

            switch(res.Data[1])
            {
                case 0x00:
                    Console.WriteLine("       Keine weiteren Einträge");
                    return 0;
                    
                case 0x01:
                    Console.WriteLine("        - Datei  " + UTF8Encoding.UTF8.GetString(res.Data.Skip(2).ToArray()));
                    break;
                    
                case 0x02:
                    Console.WriteLine("        - Ordner " + UTF8Encoding.UTF8.GetString(res.Data.Skip(2).ToArray()));
                    break;
            }

            
            res = await device.InvokeFunctionProperty(159, (byte)FtpCommands.DirList, null, true);
        }
    }
    
    private static async Task<int> mkdir(Arguments args)
    {
        Console.WriteLine("Info:  Ordner erstellen - " + args.Path1);
        byte[] buffer = UTF8Encoding.UTF8.GetBytes(args.Path1 + char.MinValue);
        MsgFunctionPropertyStateRes res = await device.InvokeFunctionProperty(159, (byte)FtpCommands.DirCreate, buffer, true);

        if(res.Data[0] == 0x00)
        {
            Console.WriteLine("Info:  Ordner erfolgreich erstellt");
            return 0;
        }
        
        throw new FtpException(res.Data[0]);
    }
    
    private static async Task<int> rmdir(Arguments args)
    {
        Console.WriteLine("Info:  Ordner löschen - " + args.Path1);
        byte[] buffer = UTF8Encoding.UTF8.GetBytes(args.Path1 + char.MinValue);
        MsgFunctionPropertyStateRes res = await device.InvokeFunctionProperty(159, (byte)FtpCommands.DirDelete, buffer, true);

        if(res.Data[0] == 0x00)
        {
            Console.WriteLine("Info:  Ordner erfolgreich gelöscht");
            return 0;
        }
        
        throw new FtpException(res.Data[0]);
    }
}