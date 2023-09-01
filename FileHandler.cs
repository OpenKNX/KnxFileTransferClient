using System.IO.Compression;

namespace KnxFileTransferClient;


internal class FileHandler
{
    public static long GetBytes(MemoryStream result, string path)
    {
        string extension = path.Substring(path.LastIndexOf("."));
        switch(extension)
        {
            case ".bin":
            {
                int size = 0;
                using(GZipStream compressionStream = new GZipStream(result, CompressionMode.Compress, true))
                {
                    using(FileStream fs = System.IO.File.Open(path, FileMode.Open))
                    {
                        size = (int)fs.Length;
                        fs.CopyTo(compressionStream);
                        fs.Flush();
                    }
                }
                result.Position = 0;
                return size;
            }

            case ".gz":
            {
                result.Write(System.IO.File.ReadAllBytes(path));
                result.Position = 0;
                return result.Length;
            }

            case ".uf2":
            {
                int size = 0;
                using(GZipStream compressionStream = new GZipStream(result, CompressionMode.Compress, true))
                {
                    byte[] data = Converter.ToBin(path);
                    size = data.Length;
                    foreach(byte d in data)
                        compressionStream.WriteByte(d);
                    compressionStream.Flush();
                }
                result.Position = 0;
                return size;
            }
        }

        throw new Exception("Nicht unterstütztes Dateiformat: " + extension);
    }

    public static byte[] GetBytes(string path)
    {
        using(MemoryStream ms = new MemoryStream())
        {
            GetBytes(ms, path);
            return ms.ToArray();
        }
    }
}