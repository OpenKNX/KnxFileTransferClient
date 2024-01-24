using System.Diagnostics;
using System.Text;
using System;
using System.Reflection;
using Kaenx.Konnect.Addresses;
using Kaenx.Konnect.Messages.Response;
using KnxFileTransferClient.Lib;

namespace KnxFileTransferClient;


class Program
{    
    static void PrintOpenKNXHeader(string addCustomText = null, ConsoleColor customTextColor = ConsoleColor.Green)
    {
        Console.WriteLine();
        Console.Write("Open ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("■");
        Console.ResetColor();
        string unicodeString = $"{(char)0x252C}{(char)0x2500}{(char)0x2500}{(char)0x2500}{(char)0x2500}{(char)0x2534} ";
        Console.Write($"{unicodeString} ");
        if (addCustomText != null) {
            if (customTextColor != null) {
                Console.ForegroundColor = customTextColor;
            }
            Console.WriteLine($"{addCustomText}");
            Console.ResetColor();
        }
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("■");
        Console.ResetColor();
        Console.WriteLine(" KNX");
        Console.WriteLine();
    }
    private static Kaenx.Konnect.Connections.IKnxConnection? conn = null;
    private static Kaenx.Konnect.Classes.BusDevice device = null;
    private static FileTransferClient client = null;
    private static Arguments arguments;


    static async Task<int> Main(string[] args)
    {
        //Console.ForegroundColor = ConsoleColor.Green;
        //Console.WriteLine("Willkommen zum KnxFileTransferClient!!");
        //Console.WriteLine();
        PrintOpenKNXHeader("KnxFileTransferClient");

        //Print the client version of the client and the lib
        Console.ForegroundColor = ConsoleColor.DarkGray;
        System.Version? clientVersion = typeof(Program).Assembly.GetName().Version;
        if(clientVersion != null) {
            Console.WriteLine($"Version Client:     {clientVersion.Major}.{clientVersion.Minor}.{clientVersion.Build}");
        }
        // Get the custom library attributes
        Assembly libAssembly = typeof(KnxFileTransferClient.Lib.FileTransferClient).Assembly;
        
        System.Version? libVersion = new Version(libAssembly.GetCustomAttributes<AssemblyMetadataAttribute>()
          .FirstOrDefault(attr => attr.Key == "LibVersion")?.Value);
        if (libAssembly != null && libVersion != null)
        {
          Console.WriteLine($"Version Client.Lib: {libVersion.Major}.{libVersion.Minor}.{libVersion.Build}");
        }
        Console.ResetColor();
        arguments = new Arguments();
        await arguments.Init(args);
        if(arguments.Command == "version") return 0; // The version is requested, so exit with 0
        if(arguments.Command == "help")
            return help();
        
        try { int top = Console.CursorTop; }
        catch { canFancy = false; }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"IP-Adresse: {arguments.Interface}" + (arguments.IsRouting ? " (Multicast)" : ""));
        Console.WriteLine($"IP-Port:    {arguments.Get<int>("port")}");
        Console.WriteLine($"PA:         {arguments.PhysicalAddress}");
        Console.WriteLine();
        Console.ResetColor();
        int code = -2;

        try
        {
            if(arguments.IsRouting)
                conn = new Kaenx.Konnect.Connections.KnxIpRouting(UnicastAddress.FromString(arguments.Get<string>("gs")), arguments.Interface, arguments.Get<int>("port"));
            else
                conn = new Kaenx.Konnect.Connections.KnxIpTunneling(arguments.Interface, arguments.Get<int>("port"));

            await conn.Connect();
            Console.WriteLine("Info:  Verbindung zum Bus hergestellt");
            device = new Kaenx.Konnect.Classes.BusDevice(arguments.PhysicalAddress, conn);
            await device.Connect();
            Console.WriteLine($"Info:  Verbindung zum KNX-Gerät {arguments.Get<string>("pa")} hergestellt");
            client = new FileTransferClient(device);
            client.ProcessChanged += ProcessChanged;
            client.OnError += OnError;
            client.PrintInfo += PrintInfo;
            string remoteVersion = await client.CheckVersion();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"Version Remote:     {remoteVersion}");
            Console.ResetColor();

            bool isOpen = false;
            do
            {
                if(isOpen)
                {
                    await device.Disconnect();
                    Console.WriteLine("Info:  Neuen Befehl eingeben:");
                    string args3 = Console.ReadLine() ?? "";
                    string[] args2 = args3.Split(" ");
                    arguments = new Arguments();
                    await arguments.Init(args2, true);
                    await device.Connect();
                }

                switch(arguments.Command)
                {
                    case "version":
                        code = 0;
                        break;
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
                    case "fwupdate":
                        await update(arguments);
                        break;
                    default:
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Unbekanntes Kommando: " + arguments.Command);
                        Console.ResetColor();
                        break;
                    }
                }
            } while(isOpen);
        } catch(FileTransferException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: " + ex.Message);
            if(arguments.Get<bool>("verbose"))
                Console.WriteLine(ex.StackTrace);
            Console.ResetColor();
            return ex.ErrorCode;
        } catch(Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: " + ex.Message);
            if(arguments.Get<bool>("verbose"))
                Console.WriteLine(ex.StackTrace);
            Console.ResetColor();
            return -1;
        }

        if(device != null)
            await device.Disconnect();
        if(conn != null)
            await conn.Disconnect();


        return code;
    }


    static bool firstSpeed = true;
    static bool firstDraw = true;
    static bool canFancy = true;
    static int errorCounting = 1;
    static int lastProgress = 0;
    private static void ProcessChanged(int progress, int speed, int timeLeft)
    {
        if(firstSpeed)
        {
            speed = 0;
            timeLeft = 0;
            firstSpeed = false;
        }
        if (firstDraw)
        {
            lastProgress = 0;
            firstDraw = false;

            if (canFancy)
                Console.Write("Progress: [                    ]    % -     B/s -      s left");
        }

        if (canFancy)
        {
            Console.SetCursorPosition(36 - progress.ToString().Length, Console.CursorTop);
            Console.Write(progress);

            Console.SetCursorPosition(11, Console.CursorTop);
            int currentProgress = ((int)Math.Floor(progress / 5.0));
            if(currentProgress > lastProgress)
                for (int i = 0; i < currentProgress; i++)
                    Console.Write("=");

            Console.SetCursorPosition(40, Console.CursorTop);
            for (int i = 0; i < 3 - speed.ToString().Length; i++)
                Console.Write(" ");
            Console.Write(speed);

            Console.SetCursorPosition(50, Console.CursorTop);
            for (int i = 0; i < 4 - timeLeft.ToString().Length; i++)
                Console.Write(" ");
            Console.Write(timeLeft);

            Console.SetCursorPosition(0, Console.CursorTop);
        }
        else
        {
            Console.Write("Progress: [");
            for (int i = 0; i < ((int)Math.Floor(progress / 5.0)); i++)
                Console.Write("=");
            for (int i = 0; i < 20 - ((int)Math.Floor(progress / 5.0)); i++)
                Console.Write(" ");
            Console.Write("] ");

            for (int i = 0; i < (3 - progress.ToString().Length); i++)
                Console.Write(" ");
            Console.Write(progress + "% - ");

            for (int i = 0; i < 3 - speed.ToString().Length; i++)
                Console.Write(" ");
            Console.Write(speed + " B/s - ");

            for (int i = 0; i < 4 - timeLeft.ToString().Length; i++)
                Console.Write(" ");
            Console.Write(timeLeft + " s left");
            Console.WriteLine();
        }
    }

    private static void OnError(Exception ex)
    {
        if(canFancy && !firstDraw)
        {
            Console.SetCursorPosition(62, Console.CursorTop);
            Console.WriteLine();
        }
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error ({errorCounting++:D2}): " + ex.Message);
        if(arguments.Get<bool>("verbose"))
            Console.WriteLine(ex.StackTrace);
        Console.ResetColor();
        firstDraw = true;
    }

    private static void PrintInfo(string info)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write("Info:  " + info);
        for(int i = (7+info.Length); i < 64; i++)
            Console.Write(" ");
        Console.WriteLine();
        Console.ResetColor();
    }

    private static int help()
    {
        Arguments args = new Arguments();
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"KnxFileTransferClient <Befehl> <Quelle?> <Ziel?>");

        Console.Write($"               ");
        foreach(Argument arg in args.GetArguments())
        {
            Console.Write($" --{arg.Name}");
            if(arg.Type != Argument.ArgumentType.Bool)
                Console.Write($" {arg.Value}");
        }
        Console.WriteLine();
        Console.ResetColor();
        Console.WriteLine($"In Session:");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"<Befehl> <Quelle?> <Ziel?>");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("Befehl:          Folgende Befehle sind vorhanden");
        Console.WriteLine("                 format/exists/rename/list/mkdir/rmdir/open/close");
        Console.WriteLine("                 upload/download/fwupdate/delete");
        Console.WriteLine("Quelle*:         Pfad zur Datei auf dem Host");
        Console.WriteLine("Ziel**:          Pfad zur Datei auf dem KNX-Gerät");

        int maxLength = 17;

        foreach(Argument arg in args.GetArguments())
        {
            Console.Write(arg.Name);
            for(int i = 0; i < maxLength-arg.Name.Length; i++)
                Console.Write(" ");

            Console.Write(arg.Display);

            if(arg.Type != Argument.ArgumentType.Bool)
                Console.Write($" (Default: {arg.Value})");
            Console.WriteLine();
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("*  nur bei Befehl upload/download");
        Console.WriteLine("** nur bei Befehl exists/rename/upload/download/list/mkdir/rmdir");
        Console.WriteLine();
        Console.WriteLine("Open  = Session Öffnen");
        Console.WriteLine("Close = Session Beenden");
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
        if(string.IsNullOrEmpty(args.Source))
            throw new Exception("Kein Pfad angegeben");

        if(!args.Source.StartsWith("/"))
            throw new Exception("Pfadangaben auf dem Zielgerät müssen absolut angegeben werden (zB /ordner/datei.txt)");
            
        Console.WriteLine("Info:  Exists - " + args.Source);
        bool exists = await client.Exists(args.Source);
        Console.WriteLine("Info:  Existiert" + (exists ? "":" nicht"));
    }

    private static async Task rename(Arguments args)
    {
        if(string.IsNullOrEmpty(args.Source))
            throw new Exception("Kein Source-Pfad angegeben");
            
        if(string.IsNullOrEmpty(args.Target))
            throw new Exception("Kein Ziel-Pfad angegeben");
            
        Console.WriteLine("Info:  Umbenennen - " + args.Source + " in " + args.Target);
        await client.Rename(args.Source, args.Target);
        Console.WriteLine("Info:  Umbenennen erfolgreich");
    }

    private static async Task upload(Arguments args)
    {
        if(string.IsNullOrEmpty(args.Source))
            throw new Exception("Kein Source-Pfad angegeben");
            
        if(string.IsNullOrEmpty(args.Target))
            throw new Exception("Kein Ziel-Pfad angegeben");
            
        if(!args.Target.StartsWith("/"))
            throw new Exception("Pfadangaben auf dem Zielgerät müssen absolut angegeben werden (zB /ordner/datei.txt)");
            
        Console.WriteLine("Info:  Datei hochladen - von " + args.Source + " in " + args.Target);

        if(await client.Exists(args.Target))
        {
            Console.WriteLine("       Die Datei existiert bereits.");
            Console.Write("       Datei löschen? (J/N): ");  // Yes/No or Ja/Nein but not Ja/Yes nor J/Y!
            await device.Disconnect();
            ConsoleKeyInfo input = Console.ReadKey();
            if(input.Key != ConsoleKey.J && input.Key != ConsoleKey.Y)
            {
                Console.WriteLine("");
                Console.WriteLine("Info:  Upload abgebrochen");
                return;
            }
            await device.Connect();
            await client.FileDelete(args.Target);
            Console.WriteLine("");
            Console.WriteLine("Info:  Datei wurde gelöscht");
        }

        await client.FileUpload(args.Source, args.Target, args.Get<int>("pkg"));
        Console.WriteLine($"Info:  Datei hochladen abgeschlossen");
    }

    private static async Task download(Arguments args)
    {
        if(string.IsNullOrEmpty(args.Source))
            throw new Exception("Kein Source-Pfad angegeben");
            
        if(string.IsNullOrEmpty(args.Target))
            throw new Exception("Kein Ziel-Pfad angegeben");
            
        if(!args.Source.StartsWith("/"))
            throw new Exception("Pfadangaben auf dem Zielgerät müssen absolut angegeben werden (zB /ordner/datei.txt)");
            
        Console.WriteLine("Info:  Datei runterladen - von " + args.Source + " in " + args.Target);
        await client.FileDownload(args.Source, args.Target, args.Get<int>("pkg"));
        Console.WriteLine("Info:  Datei runterladen abgeschlossen");
    }

    private static async Task delete(Arguments args)
    {
        if(string.IsNullOrEmpty(args.Source))
            throw new Exception("Kein Pfad angegeben");
            
        if(!args.Source.StartsWith("/"))
            throw new Exception("Pfadangaben auf dem Zielgerät müssen absolut angegeben werden (zB /ordner/datei.txt)");

        Console.WriteLine("Info:  Datei löschen - " + args.Source);
        await client.FileDelete(args.Source);
        Console.WriteLine("Info:  Datei erfolgreich gelöscht");
    }

    private static async Task list(Arguments args)
    {
        if(string.IsNullOrEmpty(args.Source))
            throw new Exception("Kein Pfad angegeben");
            
        if(!args.Source.StartsWith("/"))
            throw new Exception("Pfadangaben auf dem Zielgerät müssen absolut angegeben werden (zB /ordner/datei.txt)");

        Console.WriteLine("Info:  Ordner auflisten - " + args.Source);
        List<FileTransferPath> list = await client.List(args.Source);
        string root = args.Source;
        if(!root.EndsWith("/"))
            root += "/";
        foreach(FileTransferPath path in list)
            Console.WriteLine($"        - {(path.IsFile ? "Datei ":"Ordner")} {root}{path.Name}");
    }
    
    private static async Task mkdir(Arguments args)
    {
        if(string.IsNullOrEmpty(args.Source))
            throw new Exception("Kein Pfad angegeben");

        if(!args.Source.StartsWith("/"))
            throw new Exception("Pfadangaben auf dem Zielgerät müssen absolut angegeben werden (zB /ordner)");

        Console.WriteLine("Info:  Ordner erstellen - " + args.Source);
        await client.DirCreate(args.Source);
        Console.WriteLine("Info:  Ordner erfolgreich erstellt");
    }
    
    private static async Task rmdir(Arguments args)
    {
        if(string.IsNullOrEmpty(args.Source))
            throw new Exception("Kein Pfad angegeben");

        if(!args.Source.StartsWith("/"))
            throw new Exception("Pfadangaben auf dem Zielgerät müssen absolut angegeben werden (zB /ordner)");

        Console.WriteLine("Info:  Ordner löschen - " + args.Source);
        await client.DirDelete(args.Source);
        Console.WriteLine("Info:  Ordner erfolgreich gelöscht");
    }
    
    private static async Task update(Arguments args)
    {
        if(string.IsNullOrEmpty(args.Source))
            throw new Exception("Kein Pfad angegeben");

        if (!File.Exists(args.Source))
            throw new Exception("Das Programm kann die angegebene Firmware nicht finden");

        string extension = args.Source.Substring(args.Source.LastIndexOf("."));
        switch(extension)
        {
            case ".bin":
                Console.WriteLine("Info:  Bei diesem Dateiformat kann die Kompatibilität\r\n       zur Applikation nicht überprüft werden.");
                Console.WriteLine("Info:  (beta) Die Firmware wird komprimiert übertragen!");
                break;

            case ".gz":
                Console.WriteLine("Info:  Bei diesem Dateiformat kann die Kompatibilität\r\n       zur Applikation nicht überprüft werden.");
                break;

            case ".uf2":
                Console.WriteLine("Info:  Die Firmware wird komprimiert übertragen!");
                break;
        }


        if(extension == ".uf2")
        {
            if(!args.Get<bool>("force"))
            {
                List<Tag> tags = Converter.GetTags(args.Source);
                Tag? infoTag = tags.SingleOrDefault(t => t.Type == Converter.KNX_EXTENSION_TYPE);

                if(infoTag != null)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"Version UF2:    0x{infoTag.Data[0] << 8 | infoTag.Data[1]:X4} {infoTag.Data[2]>>4}.{infoTag.Data[2]&0xF}.{infoTag.Data[3]}");
                    Console.ResetColor();
                    await device.Connect();
                    uint deviceOpenKnxId, deviceAppNumber, deviceAppVersion, deviceAppRevision = 0;
                    
                    try
                    {
                        byte[] res = await device.PropertyRead(0, 78);
                        if (res.Length == 6) {
                            deviceOpenKnxId = res[2];
                            deviceAppNumber = res[3];
                            deviceAppVersion = res[4];

                            
                            res = await device.PropertyRead(0, 25);
                            if(res.Length == 2)
                            {
                                deviceAppRevision = (uint)(res[0] >> 3);
                            } else {
                                throw new Exception("PropertyResponse für Version war ungültig");
                            }

                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            Console.WriteLine($"Version Device: 0x{deviceOpenKnxId << 8 | deviceAppNumber:X4} {deviceAppVersion>>4}.{deviceAppVersion&0xF}.{deviceAppRevision}");
                            Console.ResetColor();
                        } else {
                            throw new Exception("PropertyResponse für HardwareType war ungültig");
                        }

                        if(!CheckApplication(infoTag, deviceOpenKnxId, deviceAppNumber, deviceAppVersion, deviceAppRevision))
                            return;

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error beim Lesen der Version. Die Kompatibilität wird nicht geprüft!");
                        if(args.Get<bool>("verbose"))
                        {
                            Console.WriteLine(ex.Message);
                            Console.WriteLine(ex.StackTrace);
                        }
                    }

                    //Konvertieren und abfrage können länger dauern
                    await device.Disconnect();
                } else {
                    Console.WriteLine("Info:  UF2 enthält keine Angaben zur Version!");
                }
            } else {
                Console.WriteLine("Info:  Firmware wird übertragen, egal welche Version auf dem Gerät ist.");
                Console.WriteLine("       Du musst sicher sein, dass die Hardware die Firmware unterstützt, die hochgeladen wird!");
            }
        }
        



        using(MemoryStream stream = new MemoryStream())
        {
            Console.WriteLine($"File:       Passe Firmware für Übertragung an...");
            long origsize = FileHandler.GetBytes(stream, args.Source); //, args.Get("force") == 1, deviceOpenKnxId, deviceAppNumber, deviceAppVersion, deviceAppRevision);
            await device.Connect();
            Console.WriteLine($"Size:       {origsize} Bytes\t({origsize / 1024} kB) original");
            if(origsize != stream.Length)
            {
                Console.WriteLine($"Size:       {stream.Length} Bytes\t({stream.Length / 1024} kB) komprimiert");
            }
            Console.WriteLine();
            Console.WriteLine();

            byte[] initdata = BitConverter.GetBytes(stream.Length);

            try{
                await client.FileUpload("/firmware.bin", stream, args.Get<int>("pkg"));
            } catch {
                Console.WriteLine("Upload fehlgeschlagen. Breche Update ab");
                return;
            }

            Console.WriteLine("Info:  Gerät wird neu gestartet                                ");
            await device.InvokeFunctionProperty(159, 101, System.Text.UTF8Encoding.UTF8.GetBytes("/firmware.bin" + char.MinValue));
        }
    }


    
    private static bool CheckApplication(Tag tag, uint deviceOpenKnxId,  uint deviceAppNumber, uint deviceAppVersion, uint deviceAppRevision)
    {
        uint openKnxId = tag.Data[0];
        uint appNumber = tag.Data[1];
        uint appVersion = tag.Data[2];
        uint appRevision = tag.Data[3];

        if(openKnxId != deviceOpenKnxId)
        {
            Console.WriteLine("Conv:  Die OpenKnxId auf dem Gerät ist {0:X2}, die der Firmware ist {1:X2}.", deviceOpenKnxId, openKnxId);
            Console.WriteLine("       Das führt zu einem neuen Gerät, die PA ist dann 15.15.255.");
            Console.WriteLine("       Es muss komplett über die ETS neu aufgesetzt werden!");
            Console.WriteLine("       Du musst sicher sein, dass die Hardware die Firmware unterstützt, die hochgeladen wird!");
            return Continue();
        } else if(appNumber != deviceAppNumber)
        {
            Console.WriteLine("Conv:  Die Applikationsnummer auf dem Gerät entspricht nicht der Firmware.", deviceAppNumber, appNumber);
            Console.WriteLine("       Das führt zu einem neuen Gerät, die PA ist dann 15.15.255.");
            Console.WriteLine("       Es muss komplett über die ETS neu aufgesetzt werden!");
            Console.WriteLine("       Du musst sicher sein, dass die Hardware die Firmware unterstützt, die hochgeladen wird!");
            return Continue();
        } else if (appVersion == deviceAppVersion) {
            if(appRevision == deviceAppRevision)
            {
                Console.WriteLine("Conv:  Die Applikationsversion auf dem Gerät und der Firmware ist identisch.", deviceAppVersion);
                return Continue();
            }
            if(appRevision < deviceAppRevision)
            {
                Console.WriteLine("Conv:  Die Applikationrevision auf dem Gerät größer als der Firmware.", deviceAppRevision, appRevision);
                Console.WriteLine("       Das führt zu einem Downgrade!");
                return Continue();
            }
        } else if (appVersion < deviceAppVersion) {
            Console.WriteLine("Conv:  Die Applikationsversion auf dem Gerät größer als der Firmware.", deviceAppVersion, appVersion);
            Console.WriteLine("       Das führt zu einem Downgrade!");
            Console.WriteLine("       Das Gerät muss mit der ETS neu programmiert werden (die PA bleibt erhalten).");
            return Continue();
        }

        return true;
    }
    
    private static bool Continue()
    {
        Console.Write("Comv:  Update trotzdem durchführen? (j/n) ");
        var key = Console.ReadKey(false);
        Console.WriteLine();
        if (key.KeyChar == 'J' || key.KeyChar == 'j') {
            Console.WriteLine("Conv:  Update wird fortgesetzt!");
            return true;
        }
        return false;
    }


}