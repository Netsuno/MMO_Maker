namespace Frog.LoadHarness;

public sealed class LoadHarnessOptions
{
    public string Scenario { get; init; } = "mixed";
    public int Sessions { get; init; } = 25;
    public int HoldMilliseconds { get; init; } = 3000;
    public int ChatBurst { get; init; } = 12;
    public int MoveBurst { get; init; } = 80;
    public string? Host { get; init; }
    public int Port { get; init; }
    public bool SelfHost { get; init; } = true;
    public string? JsonOut { get; init; }
    public int ConnectTimeoutMs { get; init; } = 15_000;
    public int MaxParallelAuth { get; init; } = 8;

    public static LoadHarnessOptions Parse(string[] args)
    {
        var scenario = "mixed";
        var sessions = 25;
        var holdMs = 3000;
        var chatBurst = 12;
        var moveBurst = 80;
        string? host = null;
        var port = 0;
        var selfHost = true;
        string? jsonOut = null;
        var connectTimeoutMs = 15_000;
        var maxParallelAuth = 8;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--scenario":
                    scenario = Require(args, ref i, "--scenario").ToLowerInvariant();
                    break;
                case "--sessions":
                    sessions = int.Parse(Require(args, ref i, "--sessions"));
                    break;
                case "--hold-ms":
                    holdMs = int.Parse(Require(args, ref i, "--hold-ms"));
                    break;
                case "--chat-burst":
                    chatBurst = int.Parse(Require(args, ref i, "--chat-burst"));
                    break;
                case "--move-burst":
                    moveBurst = int.Parse(Require(args, ref i, "--move-burst"));
                    break;
                case "--host":
                    host = Require(args, ref i, "--host");
                    selfHost = false;
                    break;
                case "--port":
                    port = int.Parse(Require(args, ref i, "--port"));
                    break;
                case "--self-host":
                    selfHost = true;
                    break;
                case "--json-out":
                    jsonOut = Require(args, ref i, "--json-out");
                    break;
                case "--connect-timeout-ms":
                    connectTimeoutMs = int.Parse(Require(args, ref i, "--connect-timeout-ms"));
                    break;
                case "--max-parallel-auth":
                    maxParallelAuth = int.Parse(Require(args, ref i, "--max-parallel-auth"));
                    break;
                case "-h":
                case "--help":
                    throw new LoadHarnessHelpException();
                default:
                    throw new ArgumentException("unknown argument: " + args[i]);
            }
        }

        if (sessions is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(sessions), "sessions must be 1–500");
        }

        if (!selfHost && (string.IsNullOrWhiteSpace(host) || port is <= 0 or > 65535))
        {
            throw new ArgumentException("--host and --port are required when not --self-host");
        }

        return new LoadHarnessOptions
        {
            Scenario = scenario,
            Sessions = sessions,
            HoldMilliseconds = holdMs,
            ChatBurst = chatBurst,
            MoveBurst = moveBurst,
            Host = host,
            Port = port,
            SelfHost = selfHost,
            JsonOut = jsonOut,
            ConnectTimeoutMs = connectTimeoutMs,
            MaxParallelAuth = Math.Max(1, maxParallelAuth),
        };
    }

    private static string Require(string[] args, ref int i, string name)
    {
        if (i + 1 >= args.Length)
        {
            throw new ArgumentException(name + " requires a value");
        }

        return args[++i];
    }
}

public sealed class LoadHarnessHelpException : Exception
{
    public LoadHarnessHelpException()
        : base("usage")
    {
    }
}
