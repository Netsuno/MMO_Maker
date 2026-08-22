namespace Frog.Client
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();
            var options = ClientPlaytestCli.Parse(args);
            Application.Run(new MainShellForm(options));
        }
    }

    /// <summary>Arguments CLI playtest (jamais de chaîne PostgreSQL ; jeton jamais journalisé).</summary>
    internal sealed class ClientPlaytestOptions
    {
        public bool IsPlaytest { get; init; }
        public string Host { get; init; } = "127.0.0.1";
        public int Port { get; init; } = 6000;
        public string? CorrelationId { get; init; }
        /// <summary>Jeton éphémère — ne jamais AppendLog / Console avec cette valeur.</summary>
        public string? PlaytestToken { get; init; }
    }

    internal static class ClientPlaytestCli
    {
        public static ClientPlaytestOptions Parse(string[] args)
        {
            var isPlaytest = false;
            var host = "127.0.0.1";
            var port = 6000;
            string? correlation = null;
            string? token = null;
            for (var i = 0; i < args.Length; i++)
            {
                var a = args[i];
                if (string.Equals(a, "--playtest", StringComparison.OrdinalIgnoreCase))
                {
                    isPlaytest = true;
                }
                else if (string.Equals(a, "--host", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    host = args[++i];
                }
                else if (string.Equals(a, "--port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length
                         && int.TryParse(args[i + 1], out var p))
                {
                    port = p;
                    i++;
                }
                else if (string.Equals(a, "--correlation", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    correlation = args[++i];
                }
                else if (string.Equals(a, "--playtest-token", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    token = args[++i];
                }
            }

            if (string.IsNullOrEmpty(token))
            {
                token = Environment.GetEnvironmentVariable("FROG_PLAYTEST_AUTH_TOKEN");
            }

            return new ClientPlaytestOptions
            {
                IsPlaytest = isPlaytest,
                Host = host,
                Port = port,
                CorrelationId = correlation,
                PlaytestToken = token,
            };
        }
    }
}
