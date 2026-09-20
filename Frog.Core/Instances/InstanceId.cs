namespace Frog.Core.Instances;

/// <summary>Identifiant d'une instance runtime (run donjon / raid). <see cref="Empty"/> = overworld.</summary>
public readonly record struct InstanceId(Guid Value)
{
    public static InstanceId Empty { get; } = new(Guid.Empty);

    public static InstanceId New() => new(Guid.NewGuid());

    public bool IsEmpty => Value == Guid.Empty;

    public override string ToString() => Value.ToString("D");
}
