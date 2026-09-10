namespace Frog.Persistence.IntegrationTests.Support;

/// <summary>
/// Character switch ids asserted on the public <c>WorldSwitchSnapshot</c> packet after
/// <c>SetSwitch</c>. Do not reintroduce mid-scenario <c>GetSwitchAsync</c>, and do not
/// treat ShowText as persistence proof.
/// </summary>
internal static class Phase8PublicSwitchWireGap
{
    /// <summary>
    /// Nested child CE <c>SetSwitch</c> target (parent CE only shows text + calls child).
    /// No page is conditioned on this id.
    /// </summary>
    public const string CommonEventSwitchId = Phase8PostgresContentSeed.CommonEventSwitchId;

    /// <summary>J5-FIX-07 missing-CE caller <c>SetSwitch</c> before the unresolved call; probe page is gated on this id.</summary>
    public const string MissingCommonEventSwitchId = Phase8PostgresContentSeed.MissingCommonEventSwitchId;

    /// <summary>J5-FIX-08 cycle CE A <c>SetSwitch</c> before calling B; probe page is gated on this id.</summary>
    public const string CycleCommonEventSwitchId = Phase8PostgresContentSeed.CycleCommonEventSwitchId;

    /// <summary>Wait-resume <c>SetSwitch</c> target; resume emits <see cref="Frog.Core.Enums.PacketId.WorldSwitchSnapshot"/>.</summary>
    public const string WaitSwitchId = Phase8PostgresContentSeed.WaitSwitchId;

    /// <summary>
    /// Public packet: <see cref="Frog.Core.Enums.PacketId.WorldSwitchSnapshot"/> = 77
    /// listing this character's switch id/value after SetSwitch, including common-event
    /// execution and wait-resume.
    /// </summary>
    public const string RequiredPacketForServerEngineer =
        "WorldSwitchSnapshot or CharacterFlags push (new PacketId after 76) listing " +
        "this character's switch id/value after SetSwitch, including common-event " +
        "execution and wait-resume. Wait resume must emit that snapshot (or InteractResult) " +
        "when remaining commands finish.";

    public const string CommonEventConditionedContentGap =
        "phase8_common_fired is set only by the nested child CE; parent ShowText " +
        "'Common event fired' is not persistence proof. WorldSwitchSnapshot after " +
        "interact is the public-wire proof that CE A→CE B applied SetSwitch.";

    public const string MissingAndCycleRejectGap =
        "Missing CE ref and CE→CE cycle must fail on InteractResult (plan error) " +
        "with no WorldSwitchSnapshot for the probe switch, and the conditioned probe " +
        "page must stay on the unset text. Do not use mid-scenario SQL.";
}
