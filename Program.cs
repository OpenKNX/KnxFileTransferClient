using System.Diagnostics;
using System.Text;
using System;
using System.Management.Automation;
using System.Reflection;
using Kaenx.Konnect;
using Kaenx.Konnect.Addresses;
using KnxFileTransferClient.Lib;
using System.Net.NetworkInformation;
using System.IO.Hashing;
using Kaenx.Konnect.Connections;

namespace KnxFileTransferClient;

class Program
{    
    static void PrintOpenKNXHeader(string addCustomText = "", ConsoleColor customTextColor = ConsoleColor.Green)
    {
        Console.WriteLine();
        Console.Write("Open ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("■");
        Console.ResetColor();
        string unicodeString = $"{(char)0x252C}{(char)0x2500}{(char)0x2500}{(char)0x2500}{(char)0x2500}{(char)0x2534} ";
        Console.Write($"{unicodeString} ");
        if (!string.IsNullOrEmpty(addCustomText)) {
            Console.ForegroundColor = customTextColor;
            Console.WriteLine($"{addCustomText}");
            Console.ResetColor();
        }
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("■");
        Console.ResetColor();
        Console.WriteLine(" KNX");
        Console.WriteLine();
    }
    private static Kaenx.Konnect.Connections.IpKnxConnection? conn = null;
    private static Kaenx.Konnect.Classes.BusDevice? device = null;
    private static bool verbose = false;
    private static int useMaxAPDU = 15;
    private static bool remoteCanUseResume = false;
    private static bool remoteResumeTimeout = false;

    static async Task<int> Main(string[] args)
    {
        AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
        {
            Console.WriteLine("ProcessExit – Verbindung wird geschlossen...");
            Finish().Wait();
        };
        Console.CancelKeyPress += (sender, e) =>
        {
            // Console.WriteLine("CTRL+C erkannt – Verbindung wird geschlossen...");
            // Finish().Wait();
            Environment.Exit(0);
        };

        PrintOpenKNXHeader("KnxFileTransferClient");

        //Print the client version of the client and the lib
        Console.ForegroundColor = ConsoleColor.DarkGray;
        System.Version? clientVersion = typeof(Program).Assembly.GetName().Version;
        if (clientVersion != null)
        {
            if(clientVersion.Revision != 0)
                Console.WriteLine($"Version Client:     {clientVersion.Major}.{clientVersion.Minor}.{clientVersion.Build}.{clientVersion.Revision}");
            else
                Console.WriteLine($"Version Client:     {clientVersion.Major}.{clientVersion.Minor}.{clientVersion.Build}");
        }
        // Get the custom library attributes
        System.Version? libVersion = typeof(KnxFileTransferClient.Lib.FileTransferClient).Assembly.GetName().Version;
        if (libVersion != null)
        {
            if(libVersion.Revision != 0)
                Console.WriteLine($"Version Client.Lib: {libVersion.Major}.{libVersion.Minor}.{libVersion.Build}.{libVersion.Revision}");
            else
                Console.WriteLine($"Version Client.Lib: {libVersion.Major}.{libVersion.Minor}.{libVersion.Build}");
        }
        Console.ResetColor();
        Arguments arguments = new Arguments();
        try{
            await arguments.Init(args);
        } catch(Exception ex)
        {
            PrintError(ex, arguments.Get<bool>("verbose"));
            await Finish();
            return -1;
        }
        if(arguments.Command == "version") return 0; // The version is requested, so exit with 0
        if(arguments.Command == "help")
            return help();
        verbose = arguments.Get<bool>("verbose");

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
                conn = KnxFactory.CreateRouting(UnicastAddress.FromString(arguments.Get<string>("gs")), arguments.Interface, arguments.Get<int>("port"));
            else
                conn = KnxFactory.CreateTunnelingUdp(arguments.Interface, arguments.Get<int>("port"));

            try
                {
                    await conn.Connect();
                }
                catch (Exception ex)
                {
                    throw new Exception("Die Schnittstelle ist nicht erreichbar.", ex);
                }
            Console.WriteLine("Info:  Verbindung zum Bus hergestellt");
            if(arguments.IsRouting) {
                Console.WriteLine("Info:  Verwendete Source-PA ist " + conn.GetLocalAddress() ?? "unbekannt");
                Console.WriteLine($"Info:  Router MaxAPDU: {conn.GetMaxApduLength()}");
            }
            else {
                Console.WriteLine("Info:  PA der Schnittstelle ist " + conn.GetLocalAddress() ?? "unbekannt");
                // Console.WriteLine($"Info:  Schnittstelle MaxAPDU: {conn.MaxFrameLength}");
            }
            
            if(arguments.PhysicalAddress == null)
                throw new Exception("Keine PA angegeben");

            device = new Kaenx.Konnect.Classes.BusDevice(arguments.PhysicalAddress, conn);
            try {
                await device.ConnectIndividual();
                if(arguments.GetWasSet("device-timeout"))
                    device.SetTimeout(arguments.Get<int>("device-timeout"));
            } catch(Exception ex) {
                throw new Exception($"Das Zielgerät {arguments.PhysicalAddress} ist nicht erreichbar.", ex);
            }
            Console.WriteLine($"Info:  Verbindung zum KNX-Gerät {arguments.Get<string>("pa")} hergestellt");
            useMaxAPDU = 255;
            // TODO get real MaxFrameLength from connection
            // if(conn.MaxFrameLength < useMaxAPDU)
            //     useMaxAPDU = conn.MaxFrameLength;
            if(device.MaxFrameLength < useMaxAPDU)
                useMaxAPDU = device.MaxFrameLength;
            device.SetMaxFrameLength(useMaxAPDU);
            Console.WriteLine($"Info:  Gerät MaxAPDU: {device.MaxFrameLength}");
            Console.WriteLine($"Info:  Verwende MaxAPDU: {useMaxAPDU}");
            if(arguments.Get<int>("pkg") > (useMaxAPDU - 3)) {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("WARN:  Package ist größer als MaxAPDU");
                Console.WriteLine($"WARN:  Package wird geändert auf {useMaxAPDU-3}");
                Console.ResetColor();
                arguments.Set("pkg", useMaxAPDU-3);
            }
            Console.WriteLine($"Info:  Verwende Package: {arguments.Get<int>("pkg")}");
            // Set the MaxAPDU that we calculated
            device.MaxFrameLength = useMaxAPDU;

            FileTransferClient client = new FileTransferClient(device);
            client.ProcessChanged += ProcessChanged;
            client.OnError += OnError;
            client.PrintInfo += PrintInfo;
            try {
                SemanticVersion remoteVersion = await client.CheckVersion();
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"Version Remote:     {remoteVersion}");
                remoteCanUseResume = remoteVersion >= new SemanticVersion(0, 1, 3);
                remoteResumeTimeout = remoteVersion == new SemanticVersion(0, 1, 3);
                Console.ResetColor();

            } catch {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Version Remote:     Unbekannt");
                Console.ResetColor();
            }
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
                    try
                    {
                        await arguments.Init(args2, true);
                        await device.ConnectIndividual(true);
                        // Set the MaxAPDU that we calculated
                        device.MaxFrameLength = useMaxAPDU;
                    } catch(Exception ex)
                    {
                        PrintError(ex, arguments.Get<bool>("verbose"));
                        isOpen = false;
                        continue;
                    }
                }

                try
                {
                    switch(arguments.Command)
                    {
                        case "help":
                            code = help();
                            break;
                        case "format":
                            await format(arguments, client);
                            break;
                        case "exists":
                            await exists(arguments, client);
                            break;
                        case "rename":
                            await rename(arguments, client);
                            break;
                        case "upload":
                            await upload(arguments, client);
                            break;
                        case "download":
                            await download(arguments, client);
                            break;
                        case "delete":
                            await delete(arguments, client);
                            break;
                        case "list":
                            await list(arguments, client);
                            break;
                        case "mkdir":
                            await mkdir(arguments, client);
                            break;
                        case "rmdir":
                            await rmdir(arguments, client);
                            break;
                        case "info":
                            await info(arguments, client);
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
                            await update(arguments, client);
                            break;
                        default:
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Unbekanntes Kommando: " + arguments.Command);
                            Console.ResetColor();
                            break;
                        }
                    }
                } catch(Exception ex) {
                    if(isOpen)
                    {
                        PrintError(ex, arguments.Get<bool>("verbose"));
                    } else {
                        throw new Exception(ex.Message, ex);
                    }
                }
            } while(isOpen);
        } catch(FileTransferException ex)
        {
            PrintError(ex, arguments.Get<bool>("verbose"));
            await Finish();
            return ex.ErrorCode;
        } catch(Exception ex)
        {
            PrintError(ex, arguments.Get<bool>("verbose"));
            await Finish();
            return -1;
        }

        await Finish();
        return code;
    }

    private static async Task Finish()
    {
        if (device != null)
        {
            await device.Disconnect();
            device = null;
        }
        if (conn != null)
        {
            await conn.Disconnect();
            conn = null;
        }
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
                Console.Write("Progress: [                    ]    % -     B/s - 00m:00s left");
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
            TimeSpan t = TimeSpan.FromSeconds(timeLeft);
            Console.Write(string.Format("{0:D2}m:{1:D2}s left", (int)Math.Floor(t.TotalMinutes), t.Seconds));

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

            TimeSpan t = TimeSpan.FromSeconds(timeLeft);
            Console.Write(string.Format("{0:D2}m:{1:D2}s left", (int)Math.Floor(t.TotalMinutes), t.Seconds));
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
        Console.WriteLine($"Error ({errorCounting++:D2}) [{DateTime.Now}]: " + ex.Message);
        if(verbose)
            Console.WriteLine(ex.StackTrace);
        Console.ResetColor();
        firstDraw = true;
    }

    private static void PrintInfo(string info)
    {
        if(canFancy && !firstDraw)
        {
            Console.SetCursorPosition(62, Console.CursorTop);
            Console.WriteLine();
        }
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write("Info:  " + info);
        for(int i = (7+info.Length); i < 64; i++)
            Console.Write(" ");
        Console.WriteLine();
        Console.ResetColor();
        firstDraw = true;
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
        Console.WriteLine("*  nur bei Befehl upload/download/exists/rename/delete/fwupdate/mkdir");
        Console.WriteLine("** nur bei Befehl upload/download/rename");
        Console.WriteLine();
        Console.WriteLine("Open  = Session Öffnen");
        Console.WriteLine("Close = Session Beenden");
        Console.ResetColor();
        return 0;
    }

    private static async Task format(Arguments args, FileTransferClient client)
    {
        Console.WriteLine("Info:  Dateisystem wird formatiert");
        await client.Format();
        Console.WriteLine("Info:  Dateisystem erfolgreich formatiert");
    }
    
    private static async Task exists(Arguments args, FileTransferClient client)
    {
        if(string.IsNullOrEmpty(args.Source))
            throw new Exception("Kein Pfad angegeben");

        if(!args.Source.StartsWith("/"))
            throw new Exception("Pfadangaben auf dem Zielgerät müssen absolut angegeben werden (zB /ordner/datei.txt)");
            
        Console.WriteLine("Info:  Exists - " + args.Source);
        bool exists = await client.Exists(args.Source, args.Get<bool>("force"));
        Console.WriteLine("Info:  Existiert" + (exists ? "":" nicht"));
    }

    private static async Task rename(Arguments args, FileTransferClient client)
    {
        if(string.IsNullOrEmpty(args.Source))
            throw new Exception("Kein Source-Pfad angegeben");
            
        if(string.IsNullOrEmpty(args.Target))
            throw new Exception("Kein Ziel-Pfad angegeben");
            
        Console.WriteLine("Info:  Umbenennen - " + args.Source + " in " + args.Target);
        await client.Rename(args.Source, args.Target, args.Get<bool>("force"));
        Console.WriteLine("Info:  Umbenennen erfolgreich");
    }

    private static async Task upload(Arguments args, FileTransferClient client)
    {
        if(string.IsNullOrEmpty(args.Source))
            throw new Exception("Kein Source-Pfad angegeben");
            
        if(string.IsNullOrEmpty(args.Target))
            throw new Exception("Kein Ziel-Pfad angegeben");
            
        if(!args.Target.StartsWith("/"))
            throw new Exception("Pfadangaben auf dem Zielgerät müssen absolut angegeben werden (zB /ordner/datei.txt)");

        if(device == null)
            throw new Exception("Kein Gerät verbunden");
            
        Console.WriteLine("Info:  Datei hochladen - von " + args.Source + " in " + args.Target);

        if(await client.Exists(args.Target, args.Get<bool>("force")))
        {
            Console.WriteLine("       Die Datei existiert bereits.");
            Console.Write("       Datei löschen? (J/N): ");  // Yes/No or Ja/Nein but not Ja/Yes nor J/Y!
            await device.Disconnect();
            ConsoleKeyInfo input = Console.ReadKey();
            if(input.Key != ConsoleKey.J && input.Key != ConsoleKey.Y)
            {
                await device.ConnectIndividual(true);
                // Set the MaxAPDU that we calculated
                device.MaxFrameLength = useMaxAPDU;
                Console.WriteLine("");
                Console.WriteLine("Info:  Datei wird nicht gelöscht");
            } else {
                await device.ConnectIndividual(true);
                // Set the MaxAPDU that we calculated
                device.MaxFrameLength = useMaxAPDU;
                await client.FileDelete(args.Target, args.Get<bool>("force"));
                Console.WriteLine("");
                Console.WriteLine("Info:  Datei wurde gelöscht");
            }
        }

        short start_sequence = 1;

        if(args.Get<bool>("no-resume"))
        {
            Console.WriteLine("Info:  Keine Wiederaufnahme");
        } else {
            start_sequence = await GetFileStartSequence(client, args.Source, args.Target, args.Get<int>("pkg"), true, args.Get<bool>("force"));
        }

        if(start_sequence > 0)
            await client.FileUpload(args.Source, args.Target, args.Get<int>("pkg"), start_sequence, args.Get<bool>("force"));
        Console.WriteLine($"Info:  Datei hochladen abgeschlossen");
    }

    private static async Task download(Arguments args, FileTransferClient client)
    {
        if(string.IsNullOrEmpty(args.Source))
            throw new Exception("Kein Source-Pfad angegeben");
            
        if(string.IsNullOrEmpty(args.Target))
            throw new Exception("Kein Ziel-Pfad angegeben");
            
        if(!args.Source.StartsWith("/"))
            throw new Exception("Pfadangaben auf dem Zielgerät müssen absolut angegeben werden (zB /ordner/datei.txt)");
            
        Console.WriteLine("Info:  Datei runterladen - von " + args.Source + " in " + args.Target);
        await client.FileDownload(args.Source, args.Target, args.Get<int>("pkg"), args.Get<bool>("force"));
        Console.WriteLine("Info:  Datei runterladen abgeschlossen");
    }

    private static async Task delete(Arguments args, FileTransferClient client)
    {
        if(string.IsNullOrEmpty(args.Source))
            throw new Exception("Kein Pfad angegeben");
            
        if(!args.Source.StartsWith("/"))
            throw new Exception("Pfadangaben auf dem Zielgerät müssen absolut angegeben werden (zB /ordner/datei.txt)");

        Console.WriteLine("Info:  Datei löschen - " + args.Source);
        await client.FileDelete(args.Source, args.Get<bool>("force"));
        Console.WriteLine("Info:  Datei erfolgreich gelöscht");
    }

    private static async Task list(Arguments args, FileTransferClient client)
    {
        if(string.IsNullOrEmpty(args.Source))
            throw new Exception("Kein Pfad angegeben");
            
        if(!args.Source.StartsWith("/"))
            throw new Exception("Pfadangaben auf dem Zielgerät müssen absolut angegeben werden (zB /ordner/datei.txt)");

        Console.WriteLine("Info:  Ordner auflisten - " + args.Source);
        List<FileTransferPath> list = await client.List(args.Source, args.Get<bool>("force"));
        string root = args.Source;
        if(!root.EndsWith("/"))
            root += "/";
        foreach(FileTransferPath path in list)
            Console.WriteLine($"        - {(path.IsFile ? "Datei ":"Ordner")} {root}{path.Name}");
    }
    
    private static async Task mkdir(Arguments args, FileTransferClient client)
    {
        if(string.IsNullOrEmpty(args.Source))
            throw new Exception("Kein Pfad angegeben");

        if(!args.Source.StartsWith("/"))
            throw new Exception("Pfadangaben auf dem Zielgerät müssen absolut angegeben werden (zB /ordner)");

        Console.WriteLine("Info:  Ordner erstellen - " + args.Source);
        await client.DirCreate(args.Source, args.Get<bool>("force"));
        Console.WriteLine("Info:  Ordner erfolgreich erstellt");
    }
    
    private static async Task rmdir(Arguments args, FileTransferClient client)
    {
        if(string.IsNullOrEmpty(args.Source))
            throw new Exception("Kein Pfad angegeben");

        if(!args.Source.StartsWith("/"))
            throw new Exception("Pfadangaben auf dem Zielgerät müssen absolut angegeben werden (zB /ordner)");

        Console.WriteLine("Info:  Ordner löschen - " + args.Source);
        await client.DirDelete(args.Source, args.Get<bool>("force"));
        Console.WriteLine("Info:  Ordner erfolgreich gelöscht");
    }

    private static async Task info(Arguments args, FileTransferClient client)
    {
        if (string.IsNullOrEmpty(args.Source))
            throw new Exception("Kein Pfad angegeben");

        if (!args.Source.StartsWith("/"))
            throw new Exception("Pfadangaben auf dem Zielgerät müssen absolut angegeben werden (zB /ordner)");

        Console.WriteLine("Info:  Datei-Informationen - " + args.Source);
        Lib.FileInfo info = await client.FileInfo(args.Source, args.Get<bool>("force"));
        Console.WriteLine($"        - Size: {info.Size}");
        Console.WriteLine($"        - CRC:  {info.GetCrc()}");
    }

    private static async Task update(Arguments args, FileTransferClient client)
    {
        if (string.IsNullOrEmpty(args.Source))
            throw new Exception("Kein Pfad angegeben");

        if (!File.Exists(args.Source))
            throw new Exception("Das Programm kann die angegebene Firmware nicht finden");

        if (device == null)
            throw new Exception("Kein Gerät verbunden");

        string extension = args.Source.Substring(args.Source.LastIndexOf("."));
        switch (extension)
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

        if (extension == ".uf2")
        {
            if (!args.Get<bool>("force"))
            {
                List<Tag> tags = Converter.GetTags(args.Source);
                Tag? infoTag = tags.SingleOrDefault(t => t.Type == Converter.KNX_EXTENSION_TYPE);

                if (infoTag != null)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"Version UF2:    0x{infoTag.Data[0] << 8 | infoTag.Data[1]:X4} {infoTag.Data[2] >> 4}.{infoTag.Data[2] & 0xF}.{infoTag.Data[3]}");
                    Console.ResetColor();
                    // if(!device.IsConnected())
                    //     await device.ConnectIndividual(true);
                    uint deviceOpenKnxId, deviceAppNumber, deviceAppVersion, deviceAppRevision = 0;

                    try
                    {
                        byte[] res = await device.PropertyRead(0, 78);
                        if (res.Length == 6)
                        {
                            deviceOpenKnxId = res[2];
                            deviceAppNumber = res[3];
                            deviceAppVersion = res[4];

                            res = await device.PropertyRead(0, 25);
                            if (res.Length == 2)
                            {
                                deviceAppRevision = (uint)(res[0] >> 3);
                            }
                            else
                            {
                                throw new Exception("PropertyResponse für Version war ungültig");
                            }

                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            Console.WriteLine($"Version Device: 0x{deviceOpenKnxId << 8 | deviceAppNumber:X4} {deviceAppVersion >> 4}.{deviceAppVersion & 0xF}.{deviceAppRevision}");
                            Console.ResetColor();
                        }
                        else
                        {
                            throw new Exception("PropertyResponse für HardwareType war ungültig");
                        }

                        if (!CheckApplication(infoTag, deviceOpenKnxId, deviceAppNumber, deviceAppVersion, deviceAppRevision))
                            return;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error beim Lesen der Version. Die Kompatibilität wird nicht geprüft!");
                        if (args.Get<bool>("verbose"))
                        {
                            Console.WriteLine(ex.Message);
                            Console.WriteLine(ex.StackTrace);
                        }
                    }

                    //Konvertieren und abfrage können länger dauern
                    //await device.Disconnect();
                }
                else
                {
                    Console.WriteLine("Info:  UF2 enthält keine Angaben zur Version!");
                }
            }
            else
            {
                Console.WriteLine("Info:  Firmware wird übertragen, egal welche Version auf dem Gerät ist.");
                Console.WriteLine("       Du musst sicher sein, dass die Hardware die Firmware unterstützt, die hochgeladen wird!");
            }
        }

        using (MemoryStream stream = new MemoryStream())
        {
            Console.WriteLine($"File:       Passe Firmware für Übertragung an...");
            long origsize = FileHandler.GetBytes(stream, args.Source); //, args.Get("force") == 1, deviceOpenKnxId, deviceAppNumber, deviceAppVersion, deviceAppRevision);
            Console.WriteLine($"Size:       {origsize} Bytes\t({origsize / 1024} kB) original");
            if (origsize != stream.Length)
            {
                Console.WriteLine($"Size:       {stream.Length} Bytes\t({stream.Length / 1024} kB) komprimiert");
            }
            Console.WriteLine();
            Console.WriteLine();

            byte[] initdata = BitConverter.GetBytes(stream.Length);
            if (!device.IsConnected())
            {
                await device.ConnectIndividual(true);
                // Set the MaxAPDU that we calculated
                device.MaxFrameLength = useMaxAPDU;
            }

            try
            {
                // sequence 0 is open file etc.
                short start_sequence = 1;
                if (args.Get<bool>("no-resume") == false && remoteCanUseResume)
                    start_sequence = await GetFileStartSequence(client, args.Source, "/fw.bin", args.Get<int>("pkg"), false, args.Get<bool>("force"));
                else
                    Console.WriteLine("Info:  Keine Wiederaufnahme");

                if (start_sequence > 0)
                    await client.FileUpload("/fw.bin", stream, args.Get<int>("pkg"), start_sequence, args.Get<bool>("force"));
            }
            catch (Exception ex)
            {
                PrintError(ex, args.Get<bool>("verbose"));
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Upload fehlgeschlagen. Breche Update ab                        ");
                Console.ResetColor();
                return;
            }

            Console.WriteLine("Info:  Dateiübertragung abgeschlossen                          ");
            Console.WriteLine("Info:  Übertragene Firmware wird geprüft...                    ");

            Lib.FileInfo fileInfo = await client.FileInfo("/fw.bin", args.Get<bool>("force"));
            Console.WriteLine($"Info:  CRC der übertragene Firmware: {fileInfo.GetCrc()}");
            byte[] file = FileHandler.GetBytes(args.Source);

            CRCTool crc = new();
            crc.Init(CRCTool.CRCCode.CRC32);
            ulong crc32 = crc.CalculateCRC(file);
            byte[] x = BitConverter.GetBytes(crc32).Take(4).Reverse().ToArray();
            string crc32str = BitConverter.ToString(x).Replace("-", "");
            Console.WriteLine($"Info:  CRC der lokalen Firmware:     {crc32str}");

            if (fileInfo.GetCrc() != crc32str)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: CRC stimmt nicht überein. Breche Update ab.               ");
                Console.ResetColor();
                return;
            }

            Console.WriteLine("Info:  Gerät wird neu gestartet                                ");

            try
            {
                await device.InvokeFunctionProperty(159, 101, System.Text.UTF8Encoding.UTF8.GetBytes("/fw.bin" + char.MinValue));
            } catch {
                // Das Gerät wird neu gestartet und die Verbindung geht verloren
            }
        }
    }
    
    private static void PrintError(Exception ex, bool verbose)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error [{DateTime.Now}]: {ex.Message}");
        if (verbose)
        {
            Console.WriteLine(ex.StackTrace);
            if (ex.InnerException != null)
                Console.WriteLine("Inner: " + ex.InnerException.Message + "\r\n" + ex.InnerException.StackTrace);
        }
        Console.ResetColor();
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
                Console.WriteLine("Conv:  Die Applikationrevision auf dem Gerät ist größer als der Firmware.", deviceAppRevision, appRevision);
                Console.WriteLine("       Das führt zu einem Downgrade!");
                return Continue();
            }
        } else if (appVersion < deviceAppVersion) {
            Console.WriteLine("Conv:  Die Applikationsversion auf dem Gerät ist größer als der Firmware.", deviceAppVersion, appVersion);
            Console.WriteLine("       Das führt zu einem Downgrade!");
            Console.WriteLine("       Das Gerät muss mit der ETS neu programmiert werden (die PA bleibt erhalten).");
            return Continue();
        }

        return true;
    }
    
    private static bool Continue()
    {
        Console.Write("Conv:  Update trotzdem durchführen? (j/n) ");
        var key = Console.ReadKey(false);
        Console.WriteLine();
        if (key.KeyChar == 'J' || key.KeyChar == 'j') {
            Console.WriteLine("Conv:  Update wird ausgeführt!");
            return true;
        }
        return false;
    }

    private static async Task<short> GetFileStartSequence(FileTransferClient client, string source, string target, int length, bool isGZipped, bool force)
    {
        try
        {
            bool canResume = await client.CheckFeature(FileTransferClient.FtmFeatures.Resume);
            if(!canResume)
            {
                Console.WriteLine("Info:  Wiederaufnahme von Gerät nicht unterstützt");
                return 1;
            }

            Lib.FileInfo info = await client.FileInfo(target, force);
            if(info.Size == 0)
            {
                if(remoteResumeTimeout)
                {
                    Console.WriteLine("Info:  Datei ist leer (Remote Resume Timeout)");
                    Console.WriteLine("Info:  Warte 30s");
                    await Task.Delay(30000);
                    info = await client.FileInfo(target, force);
                    if(info.Size == 0)
                    {
                        Console.WriteLine("Info:  Datei ist immer noch leer");
                        return 1;
                    }
                } else
                {
                    Console.WriteLine("Info:  Datei ist leer");
                    return 1;
                }
            }
            byte[] file = FileHandler.GetBytes(source);

            SemanticVersion version = await client.CheckVersion();
            int packageSize = length - 6;
            if (version <= new SemanticVersion(0, 1, 4))
                packageSize = length - 3;

            CRCTool crc = new();
            crc.Init(CRCTool.CRCCode.CRC32);
            ulong crc32 = crc.CalculateCRC(file.Take(info.Size).ToArray());
            byte[] x = BitConverter.GetBytes(crc32).Take(4).Reverse().ToArray();
            string crc32str = BitConverter.ToString(x).Replace("-", "");
            Console.WriteLine($"Info:  Dateiinfos CRC32 Lokal={crc32str} Remote={info.GetCrc()}");

            if(info.GetCrc() == crc32str)
            {
                Console.WriteLine("Info:  Datei ist identisch");

                short start_sequence = (short)Math.Floor(info.Size / (packageSize * 1.0));
                if(file.Length == info.Size)
                {
                    Console.WriteLine("Info:  Datei ist vollständig");
                    return -1;
                }

                int start_byte = (start_sequence * packageSize) + 1;
                int start_perc = (int)((double)start_byte / file.Length * 100);
                int needed_sequences = (int)Math.Ceiling((double)file.Length / packageSize) - 1;
                Console.WriteLine($"Info:  Starte bei {start_byte}/{file.Length} Bytes ({start_perc}%) [{start_sequence}/{needed_sequences} Sequenzen]");
                start_sequence++; // sequence starts at 1, 0 is open file etc.
                return start_sequence;
            } else {
                Console.WriteLine("Info:  Datei ist nicht identisch");
                return 1;
            }
        }
        catch
        {
            Console.WriteLine("Info:  Dateiinfos konnten nicht abgerufen werden");
        }
        return 1;
    }
}