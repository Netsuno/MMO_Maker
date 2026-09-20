using Frog.Application.Identity;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Repositories.Auth;
using Frog.Persistence.PostgreSql.Repositories.Ops;

namespace Frog.OpsCli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || args.Any(a => a is "-h" or "--help"))
            {
                PrintHelp();
                return args.Length == 0 ? 1 : 0;
            }

            var connection = ReadConnectionString(args);
            if (string.IsNullOrWhiteSpace(connection))
            {
                Console.Error.WriteLine("FROG_POSTGRES_CONNECTION_STRING or --connection-string is required.");
                return 2;
            }

            using var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(connection)));
            var commands = new OperatorAccountCommands(
                new PostgresAccountRepository(gate),
                new PostgresAuthSessionRepository(gate),
                new PostgresOperatorDirectory(gate),
                new PostgresAccountSanctionStore(gate));

            return await DispatchAsync(commands, args).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static async Task<int> DispatchAsync(OperatorAccountCommands commands, string[] args)
    {
        var verb = args[0].ToLowerInvariant();
        switch (verb)
        {
            case "create":
                Require(args, 3, "create <username> <password>");
                var created = await commands.CreateAccountAsync(args[1], args[2]).ConfigureAwait(false);
                if (created.Status != AccountCreateStatus.Created)
                {
                    Console.Error.WriteLine("create failed: " + created.Status);
                    return 1;
                }

                Console.WriteLine("created " + args[1] + " id=" + created.AccountId);
                Console.WriteLine("operator=false (invite/create n'accorde jamais GM)");
                return 0;

            case "reset-password":
                Require(args, 2, "reset-password <username> [--password <new>]");
                var newPass = Option(args, "--password") ?? OperatorAccountCommands.GeneratePassword();
                if (!await commands.ResetPasswordAsync(args[1], newPass).ConfigureAwait(false))
                {
                    Console.Error.WriteLine("reset-password failed (compte introuvable ou mot de passe invalide).");
                    return 1;
                }

                Console.WriteLine("password-reset " + args[1]);
                if (Option(args, "--password") is null)
                {
                    Console.WriteLine("new-password " + newPass);
                }

                return 0;

            case "session-revoke":
                Require(args, 2, "session-revoke <username>");
                if (!await commands.RevokeSessionsAsync(args[1]).ConfigureAwait(false))
                {
                    Console.Error.WriteLine("session-revoke failed (compte introuvable ou aucune session).");
                    return 1;
                }

                Console.WriteLine("sessions-revoked " + args[1]);
                return 0;

            case "operator":
                Require(args, 3, "operator grant|revoke <username> [--by <actor>]");
                var action = args[1].ToLowerInvariant();
                if (action == "grant")
                {
                    var by = Option(args, "--by") ?? "opscli";
                    var grant = await commands.GrantOperatorAsync(args[2], by).ConfigureAwait(false);
                    if (grant.Status != OperatorGrantStatus.Granted)
                    {
                        Console.Error.WriteLine("operator grant failed: " + grant.Status);
                        return 1;
                    }

                    Console.WriteLine("operator-granted " + args[2]);
                    return 0;
                }

                if (action == "revoke")
                {
                    if (!await commands.RevokeOperatorAsync(args[2]).ConfigureAwait(false))
                    {
                        Console.Error.WriteLine("operator revoke failed.");
                        return 1;
                    }

                    Console.WriteLine("operator-revoked " + args[2]);
                    return 0;
                }

                Console.Error.WriteLine("operator action must be grant or revoke.");
                return 1;

            case "sanction":
                Require(args, 3, "sanction mute|unmute|ban|unban <username> --actor <ops> [--reason <text>]");
                var kindCmd = args[1].ToLowerInvariant();
                var actor = Option(args, "--actor")
                    ?? throw new InvalidOperationException("--actor is required for sanctions.");
                var reason = Option(args, "--reason") ?? "opscli";
                switch (kindCmd)
                {
                    case "mute":
                        await commands.ApplySanctionAsync(args[2], actor, SanctionKinds.Mute, reason)
                            .ConfigureAwait(false);
                        break;
                    case "ban":
                        await commands.ApplySanctionAsync(args[2], actor, SanctionKinds.Ban, reason)
                            .ConfigureAwait(false);
                        break;
                    case "unmute":
                        if (!await commands.LiftSanctionAsync(args[2], actor, SanctionKinds.Mute, reason)
                                .ConfigureAwait(false))
                        {
                            Console.Error.WriteLine("unmute failed.");
                            return 1;
                        }

                        break;
                    case "unban":
                        if (!await commands.LiftSanctionAsync(args[2], actor, SanctionKinds.Ban, reason)
                                .ConfigureAwait(false))
                        {
                            Console.Error.WriteLine("unban failed.");
                            return 1;
                        }

                        break;
                    default:
                        Console.Error.WriteLine("sanction kind must be mute|unmute|ban|unban.");
                        return 1;
                }

                Console.WriteLine("sanction " + kindCmd + " " + args[2]);
                return 0;

            default:
                Console.Error.WriteLine("unknown command: " + verb);
                PrintHelp();
                return 1;
        }
    }

    private static string? ReadConnectionString(string[] args)
        => Option(args, "--connection-string")
           ?? Environment.GetEnvironmentVariable("FROG_POSTGRES_CONNECTION_STRING");

    private static string? Option(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static void Require(string[] args, int positional, string usage)
    {
        if (args.Length < positional)
        {
            throw new InvalidOperationException("usage: " + usage);
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            """
            Frog.OpsCli — comptes / sessions / opérateurs / sanctions (hors TCP).
            Connection: FROG_POSTGRES_CONNECTION_STRING or --connection-string <cs>

            create <username> <password>
            reset-password <username> [--password <new>]
            session-revoke <username>
            operator grant <username> [--by <actor>]
            operator revoke <username>
            sanction mute|unmute|ban|unban <username> --actor <ops-username> [--reason <text>]

            Create n'accorde jamais GM. Bêta: Registration:Mode=ProvisionedOnly.
            InviteOnly: jalon (pas de jetons) — TCP register refusé.

            Maintenance (hors TCP, pas de bump protocole):
              Maintenance:Enabled=true  ou  FROG_MAINTENANCE=1  ou  FROG_MAINTENANCE_FILE
              AllowOperators=true : un compte déjà grant via `operator grant` peut encore se logger.
            """);
    }
}
