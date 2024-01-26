using System.IO.Compression;

namespace KnxFileTransferClient;

public class Converter
{
    public static uint KNX_EXTENSION_TYPE = 0x584E4B;
    private static uint UF2_MAGIC_START0 = 0x0A324655;
    private static uint UF2_MAGIC_START1 = 0x9E5D5157;
    private static uint UF2_MAGIC_END = 0x0AB16F30;

    public static List<Tag> GetTags(string path)
    {
        List<Tag> output = new();
        Block block;
        int counter = 0;
        do
        {
            block = ParseBlock(path, counter);

            if(block.IsValid && !block.FlagNotMainFlash)
            {
                if(block.Tags.Count > 0)
                {
                    output.AddRange(block.Tags);
                }
            }
            counter++;
        } while(block.Sequence < block.BlockCount -1);

        return output;
    }

    public static byte[] ToBin(string path)
    {
        using(MemoryStream ms = new MemoryStream())
        {
            int counter = 0;
            long addr = -1;
            Block block;
            do
            {
                block = ParseBlock(path, counter);

                if(block.IsValid && !block.FlagNotMainFlash)
                {
                    if(addr == -1)
                        addr = block.Address;

                    var padding = block.Address - addr;
                    if (padding < 0) {
                        throw new DataMisalignedException(string.Format("Blockreihenfolge falsch an Position {0}", addr));
                    }
                    if (padding > 10 * 1024 * 1024) {
                        throw new DataMisalignedException(string.Format("Mehr als 10M zum Auffüllen (padding) benötigt an Position {0}", addr));
                    }
                    if (padding % 4 != 0) {
                        throw new DataMisalignedException(string.Format("Adresse zum Auffüllen (padding) nicht an einer Wortgrenze ausgerichtet an Position {0}", addr));
                    }
                    while (padding > 0) {
                        padding -= 4;
                        ms.Write(BitConverter.GetBytes(0), 0, 4);
                    }
                    addr += block.Size;

                    ms.Write(block.Data, 0, block.Data.Length);
                }
                else
                    Console.WriteLine($"Conv:  Block an Position {counter} ignoriert; Falsche 'magic number'!");

                counter++;
            } while(block.Sequence < block.BlockCount -1);

            ms.Flush();
            return ms.ToArray();
        }
    }

    private static Block ParseBlock(string path, int block)
    {
        List<uint> data = new List<uint>();
        byte[] file = System.IO.File.ReadAllBytes(path);
        file = file.Skip(block * 512).Take(512).ToArray();
        byte[] buffer = new byte[4];

        for(int i = 0; i < 9; i++)
        {
            int addr = i * 4;

            if(i == 8)
                addr = 508;

            for(int x = 0; x < 4; x++)
                buffer[x] = file[addr + x];

            uint num = BitConverter.ToUInt32(buffer);
            data.Add(num);
        }

        Block output = new Block();

        if(data[0] == UF2_MAGIC_START0 && data[1] == UF2_MAGIC_START1 && data[8] == UF2_MAGIC_END)
        {
            output.IsValid = true;
            output.Address = data[3];
            output.Size = data[4];
            output.FlagNotMainFlash = (data[2] & 0x00000001) != 0;
            output.FlagFileContainer = (data[2] & 0x00001000) != 0;
            output.FlagFamilyId = (data[2] & 0x00002000) != 0;
            output.FlagMD5 = (data[2] & 0x00004000) != 0;
            output.FlagExtensionTags = (data[2] & 0x00008000) != 0;

            output.DataLength = data[4];
            output.Sequence = data[5];
            output.BlockCount = data[6];
            output.Info = data[7];

            output.Data = file.Skip(32).Take((int)output.DataLength).ToArray();

            if(output.BlockCount - 1 == output.Sequence)
            {
                int xcounter = (int)output.DataLength - 1;
                while(output.Data[xcounter] == 0x00)
                {
                    xcounter--;
                }
                output.Data = output.Data.Take(xcounter+1).ToArray();
            }

            if(output.FlagExtensionTags)
            {
                uint addr = 32 + output.DataLength;
                while(file[addr] != 0 && file[addr+1] != 0)
                {
                    Tag tag = new()
                    {
                        Size = file[addr],
                        Type = (uint)(file[addr + 1] | file[addr + 2] << 8 | file[addr + 3] << 16)
                    };
                    tag.Data = file.Skip((int)addr+4).Take((int)tag.Size - 4).ToArray();
                    output.Tags.Add(tag);

                    uint padding = (4 - (tag.Size % 4)) % 4;
                    addr += tag.Size + padding;
                }   
            }
        }

        return output;
    }

    public static void ToGZip(string path)
    {
        using FileStream originalFileStream = File.Open(path, FileMode.Open);
        using FileStream compressedFileStream = File.Create(path + ".gz");
        using var compressor = new GZipStream(compressedFileStream, CompressionMode.Compress);
        originalFileStream.CopyTo(compressor);
    }
}

public class Block
{
    public bool IsValid { get; set; }

    public bool FlagNotMainFlash { get; set; }
    public bool FlagFileContainer { get; set; }
    public bool FlagFamilyId { get; set; }
    public bool FlagMD5 { get; set; }
    public bool FlagExtensionTags { get; set; }

    public uint Address { get; set; }
    public uint Size { get; set; }
    public uint BlockCount { get; set; }
    public uint DataLength { get; set; }
    public uint Sequence { get; set; }

    //FileSize or FamilyId or Zero
    public uint Info { get; set; }

    public byte[] Data { get; set; } = new byte[0];

    public List<Tag> Tags { get; set; } = new List<Tag>();

    public string FamilyName
    {
        get {
            if(FamilyNames.ContainsKey(Info))
                return FamilyNames[Info];
            return "Unknown";
        }
    }

    private Dictionary<uint, string> FamilyNames = new Dictionary<uint, string>() {
        {0x68ed2b88, "SAMD21"},
        {0xe48bff56, "RP2040"}
    };
}

public class Tag
{
    public uint Size { get; set; }
    public uint Type { get; set; }
    public byte[] Data { get; set; } = new byte[0];
}

public enum TagTypes
{
    FirmwareVersion = 0x9fc7bc,
    Description = 0x650d9d,
    PageSize = 0x0be9f7,
    SHA2 = 0xb46db0,
    DeviceTypeIdentifier = 0xc8a729
}