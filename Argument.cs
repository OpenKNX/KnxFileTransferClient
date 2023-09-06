

namespace KnxFileTransferClient;

internal class Argument
{
    public enum ArgumentType
    {
        Int,
        String,
        Bool
    };

    public string Name { get; set; }
    public string Display { get; set; }
    public object Value { get; set; }
    public ArgumentType Type { get; set; }
    public bool Required { get; set; } = false;
    public bool IsDefault { get; set; } = true;

    public Argument(string name, string display, ArgumentType type, object defaultValue, bool required)
    {
        Name = name;
        Display = display;
        Type = type;
        Value = defaultValue;
        Required = required;
    }
}