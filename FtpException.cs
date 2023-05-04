namespace KnxFtpClient;

internal class FtpException : Exception
{
    public int ErrorCode { get; }
    private static Dictionary<int, string> Messages = new Dictionary<int, string>() {
        { 0x01, "LittleFS.begin() failed" },
        { 0x02, "LittleFS.format() failed" },
        { 0x03, "LittleFS not initialized" },
        //reserve
        { 0x41, "File already open" },
        { 0x42, "File can't be opened" },
        { 0x43, "File not opened" },
        { 0x44, "File can't be deleted" },
        { 0x45, "File can't be renamed" },
        { 0x46, "File can't seek position" },
        //reserve
        { 0x81, "Dir already open" },
        { 0x82, "Dir can't be opened" },
        { 0x83, "Dir not opened" },
        { 0x84, "Dir can't be deleted" },
        { 0x85, "Dir can't be created" },
        { 0x86, "Dir has no more files" }
    };

    public FtpException(int code) : base(Messages[code])
    {
        ErrorCode = code;
    }

    public FtpException(string message, int code)
        : base(message)
    {
        ErrorCode = code;
    }

    public FtpException(string message, Exception inner, int code)
        : base(message, inner)
    {
        ErrorCode = code;
    }
}