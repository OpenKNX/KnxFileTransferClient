using System.Reflection;
using KnxFileTransferClient;

try
{
    var options = new Arguments();
    Check(options.Get<int>("delay") == 60, "Default upload delay must be 60 ms");
    foreach (int delay in new[] {0, 100})
    {
        var remaining = new List<string> {"--delay", delay.ToString()};
        typeof(Arguments).GetMethod("ParseArgs", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(options, new object[] {remaining});
        Check(options.Get<int>("delay") == delay, "Explicit delay was ignored");
        Check(remaining.Count == 0, "Delay argument was not consumed");
    }
    Console.WriteLine("PASS: CLI default 60 ms, explicit --delay 0 and --delay 100");
    return 0;
}
catch (Exception ex) {Console.Error.WriteLine("FAIL: " + ex.Message); return 1;}
static void Check(bool condition, string message) {if(!condition) throw new Exception(message);}