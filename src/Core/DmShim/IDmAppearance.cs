namespace Anomaly.Core.DmShim;

// Owner: the M2 contracts step (complete, frozen; docs/ai/specs/m2-ownership.md). Implemented by the /atom engine part
// (src/Core/Tg/Builtin/atom.builtin.cs), by image and mutable_appearance (this assembly) and by M2 P3's overlay snapshot.

/// <summary>
/// The visible state of anything BYOND draws (BYOND ref "appearance var (atom)": every atom and image has an
/// appearance): the vars the server's appearance interning reads (M2 P5, <c>docs/ai/specs/replication.md</c>) and the
/// client composes (M2 P7). An atom, an <see cref="image"/>, a <see cref="mutable_appearance"/> and an overlay snapshot
/// (M2 P3: the immutable copy BYOND keeps in <c>overlays</c> and <c>underlays</c>, "a static snapshot of the object is
/// taken at the time when the overlay is created", BYOND ref <c>/atom/var/overlays</c>) all implement it.
/// <para>
/// Read-only: reading changes nothing and fires no sink, so a reader outside the world (the replicator, after
/// <c>World.Step</c>) may use it. The overlay lists hold <see cref="IDmAppearance"/> values or, for an item an engine
/// path stored unconverted, text (an icon state of the parent's icon) or a resource (an icon).
/// </para>
/// </summary>
public interface IDmAppearance
{
    /// <summary>BYOND <c>icon</c>: a resource (<see cref="DmResource"/>), text where tg passes text, or null (an overlay then uses its parent's icon).</summary>
    object icon { get; }

    /// <summary>BYOND <c>icon_state</c>.</summary>
    string icon_state { get; }

    /// <summary>BYOND <c>dir</c>.</summary>
    float dir { get; }

    /// <summary>BYOND <c>layer</c> (<c>FLOAT_LAYER</c> and other negative values float above the parent, ordered among themselves).</summary>
    float layer { get; }

    /// <summary>BYOND <c>plane</c> (<c>FLOAT_PLANE</c>: the parent's plane).</summary>
    float plane { get; }

    /// <summary>BYOND <c>alpha</c>, 0 to 255.</summary>
    float alpha { get; }

    /// <summary>BYOND <c>color</c>: a colour text (<c>"#rrggbb"</c>, <c>"#rrggbbaa"</c>), a colour matrix (a list), or null.</summary>
    object color { get; }

    /// <summary>BYOND <c>pixel_x</c>.</summary>
    float pixel_x { get; }

    /// <summary>BYOND <c>pixel_y</c>.</summary>
    float pixel_y { get; }

    /// <summary>BYOND <c>pixel_w</c>.</summary>
    float pixel_w { get; }

    /// <summary>BYOND <c>pixel_z</c>.</summary>
    float pixel_z { get; }

    /// <summary>BYOND <c>transform</c>: a <see cref="matrix"/> or null (no transformation).</summary>
    object transform { get; }

    /// <summary>BYOND <c>appearance_flags</c> (<c>RESET_COLOR</c>, <c>RESET_ALPHA</c>, <c>RESET_TRANSFORM</c>, <c>KEEP_TOGETHER</c>, <c>KEEP_APART</c>, ...).</summary>
    float appearance_flags { get; }

    /// <summary>BYOND <c>blend_mode</c> (<c>BLEND_DEFAULT</c> 0: the parent's, for an overlay).</summary>
    float blend_mode { get; }

    /// <summary>BYOND <c>invisibility</c>.</summary>
    float invisibility { get; }

    /// <summary>BYOND <c>maptext</c>: text drawn on the atom (a small HTML subset), null or empty for none. The HUD draws it (protocol version 18); the world's maptext is M9.</summary>
    string maptext { get; }

    /// <summary>BYOND <c>maptext_width</c> in pixels (default 32, one icon width).</summary>
    float maptext_width { get; }

    /// <summary>BYOND <c>maptext_height</c> in pixels (default 32).</summary>
    float maptext_height { get; }

    /// <summary>BYOND <c>maptext_x</c>: the text box's offset from the icon's lower left, in pixels.</summary>
    float maptext_x { get; }

    /// <summary>BYOND <c>maptext_y</c>.</summary>
    float maptext_y { get; }

    /// <summary>BYOND <c>filters</c>: null for none, else a filters list of <see cref="dm_filter"/> (M6 slice 7, D155; protocol version 25).</summary>
    object filters { get; }

    /// <summary>BYOND <c>render_target</c>: the name a filter's <c>render_source</c> takes this picture by; one starting with <c>*</c> is not drawn itself (M6 slice 7).</summary>
    object render_target { get; }

    /// <summary>BYOND <c>overlays</c>, never null (empty when there are none).</summary>
    DmList overlays { get; }

    /// <summary>BYOND <c>underlays</c>, never null.</summary>
    DmList underlays { get; }
}
