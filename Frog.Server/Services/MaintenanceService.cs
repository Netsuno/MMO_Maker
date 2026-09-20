using Frog.Application.Identity;
using Frog.Core.Distribution;
using Frog.Server.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Frog.Server.Services;

/// <summary>
/// Drapeau maintenance processus (config / env / fichier / override tests).
/// Pas un drain de sessions déjà connectées. Pas de bump protocole.
/// </summary>
public sealed class MaintenanceService(
    IOptionsMonitor<MaintenanceOptions> options,
    IOperatorDirectory operators,
    ILogger<MaintenanceService> logger)
{
    private readonly IOptionsMonitor<MaintenanceOptions> _options = options;
    private readonly IOperatorDirectory _operators = operators;
    private readonly ILogger<MaintenanceService> _logger = logger;
    private bool? _overrideEnabled;

    public bool IsEnabled
    {
        get
        {
            if (_overrideEnabled is bool forced)
            {
                return forced;
            }

            var current = _options.CurrentValue;
            if (TryReadFlagFile(current.FlagFile, out var fromFile))
            {
                return fromFile;
            }

            return current.Enabled;
        }
    }

    public bool AllowOperators => _options.CurrentValue.AllowOperators;

    public string WireMessage
    {
        get
        {
            var message = _options.CurrentValue.Message;
            return string.IsNullOrWhiteSpace(message)
                ? MaintenanceMessages.LoginRejected
                : message.Trim();
        }
    }

    /// <summary>Override in-process (tests / ops). <paramref name="enabled"/> null = revenir à config/env/fichier.</summary>
    public void SetEnabledOverride(bool? enabled)
    {
        _overrideEnabled = enabled;
        _logger.LogInformation("Maintenance override set to {State}.", enabled switch
        {
            true => "on",
            false => "off",
            null => "cleared",
        });
    }

    public async Task<bool> ShouldRejectLoginAsync(
        Guid? accountId,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return false;
        }

        if (AllowOperators
            && accountId is { } id
            && id != Guid.Empty
            && await _operators.IsOperatorAsync(id, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        return true;
    }

    internal static bool TryReadFlagFile(string? path, out bool enabled)
    {
        enabled = false;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            var first = File.ReadLines(path).FirstOrDefault()?.Trim() ?? string.Empty;
            if (first.Length == 0)
            {
                enabled = true;
                return true;
            }

            if (first.Equals("0", StringComparison.OrdinalIgnoreCase)
                || first.Equals("false", StringComparison.OrdinalIgnoreCase)
                || first.Equals("off", StringComparison.OrdinalIgnoreCase)
                || first.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                enabled = false;
                return true;
            }

            enabled = MaintenanceOptions.IsTruthyEnv(first) || first.Equals("enabled", StringComparison.OrdinalIgnoreCase);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
