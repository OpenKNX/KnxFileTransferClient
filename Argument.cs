

namespace KnxFileTransferClient;

internal class Argument
{
    public enum ArgumentType
    {
        Int,
        String,
        Bool,
        Enum
    };

    public enum ConnectionType
    {
        Search,
        Auto,
        Tunneling,
        Routing
    }

    public string Name { get; set; }
    public string Display { get; set; }
    public string Question { get; set; } = "";
    public string Regex { get; set; } = "";
    public object Value { get; set; }
    public ArgumentType Type { get; set; }
    public bool WasSet { get; set; } = false;
    public bool Required { get; set; } = false;

    public Argument(string name, string display, ArgumentType type, object defaultValue, bool required = false)
    {
        Name = name;
        Display = display;
        Type = type;
        Value = defaultValue;
        Required = required;
    }
}