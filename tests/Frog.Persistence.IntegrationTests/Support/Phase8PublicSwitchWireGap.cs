namespace Frog.Persistence.IntegrationTests.Support;

/// <summary>
/// C10 STOP for character switches. There is no public packet that exposes switch
/// id/value after <c>SetSwitch</c>. Do not reintroduce mid-scenario
/// <c>GetSwitchAsync</c>, and do not treat ShowText as persistence proof.
/// </summary>
internal static class Phase8PublicSwitchWireGap
{
    /// <summary>CE command <c>SetSwitch</c> target; no page is conditioned on this id.</summary>
    public const string CommonEventSwitchId = Phase8PostgresContentSeed.CommonEventSwitchId;

    /// <summary>Wait-resume <c>SetSwitch</c> target; resume emits no InteractResult.</summary>
    public const string WaitSwitchId = Phase8PostgresContentSeed.WaitSwitchId;

    /// <summary>
    /// Server packet gap: <see cref="Frog.Core.Enums.PacketId"/> ends at
    /// <c>AcquireProfessionResult = 76</c>. Needed before CE/wait switches can be
    /// asserted on the public path.
    /// </summary>
    public const string RequiredPacketForServerEngineer =
        "WorldSwitchSnapshot or CharacterFlags push (new PacketId after 76) listing " +
        "this character's switch id/value after SetSwitch, including common-event " +
        "execution and wait-resume. Wait resume must emit that snapshot (or InteractResult) " +
        "when remaining commands finish.";

    public const string CommonEventConditionedContentGap =
        "phase8_common_fired has no second map-event page gated on CharacterSwitch; " +
        "ShowText 'Common event fired' runs in the same command list as SetSwitch and " +
        "does not uniquely prove the switch persisted.";
}
