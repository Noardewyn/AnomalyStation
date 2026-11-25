namespace Anomaly.Protocol;

// Protocol version 95 (DV586; docs/ai/specs/protocol.md section 90). In the private block's version 43 flags byte:
// - bit 7 (128): the own mob's melee click cooldown differs from tg's CLICK_CD_MELEE: [varuint milliseconds] after the view areas.
//   The client swings its held thing and whooshes on a click only once that long has passed since its last swing, as the server
//   takes no blow before it (tg's next_move).

public sealed partial class OwnBlock
{
    /// <summary>tg's <c>CLICK_CD_MELEE</c>, 8 deciseconds: the cooldown a block names without bit 7.</summary>
    public const int DefaultMeleeCooldownMs = 800;

    /// <summary>The longest cooldown a block may name (a minute).</summary>
    public const int MaxMeleeCooldownMs = 60_000;

    /// <summary>
    /// Version 95: the own mob's click cooldown after a melee blow with its active hand now, milliseconds: the held thing's
    /// <c>attack_speed</c> (an empty hand's <c>CLICK_CD_MELEE</c>) with the mob's <c>next_move</c> modifiers, as tg's changeNext_move takes it.
    /// </summary>
    public int MeleeCooldownMs { get; set; } = DefaultMeleeCooldownMs;
}
