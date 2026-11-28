using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Owner: M4a P7 (client interaction). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// What a click or a hover names (interaction.md 2): an entity or a screen entry by id, or a turf by its cell, with tg's pixel inside the clicked icon.
/// </summary>
/// <param name="Kind">The kind of target; <see cref="TargetKind.None"/> for nothing.</param>
/// <param name="Id">The entity or screen entry id.</param>
/// <param name="X">A turf's x.</param>
/// <param name="Y">A turf's y.</param>
/// <param name="Z">A turf's z.</param>
/// <param name="IconX">tg's icon-x, 1 to 32 (0: none).</param>
/// <param name="IconY">tg's icon-y, 1 to 32 (0: none).</param>
/// <param name="Aim">The body zone under the point of a mob's picture (DV30), None otherwise.</param>
/// <param name="Picture">An entity pick's unclamped picture pixel (<see cref="PickHit.Picture"/>), for the client's zone lookup only; never sent.</param>
public readonly record struct ClickTarget(TargetKind Kind, long Id, int X, int Y, int Z, byte IconX = 0, byte IconY = 0, AimZone Aim = AimZone.None, (int X, int Y)? Picture = null)
{
    /// <summary>No target.</summary>
    public static ClickTarget None => default;

    /// <summary>True when there is a target.</summary>
    public bool IsSet => Kind != TargetKind.None;

    /// <summary>The target a pick names (for an entity or a screen entry the id, for a cell the cell).</summary>
    public static ClickTarget Of(in PickHit hit)
    {
        switch (hit.Kind)
        {
            case TargetKind.Entity:
            case TargetKind.Screen:
                return new ClickTarget(hit.Kind, hit.Id, 0, 0, 0, hit.IconX, hit.IconY, Picture: hit.Picture);
            case TargetKind.Ceiling:
            case TargetKind.Turf:
                return new ClickTarget(hit.Kind, 0, hit.X, hit.Y, hit.Z, hit.IconX, hit.IconY);
            default:
                return None;
        }
    }

    /// <summary>The same thing (the pixel does not count): a hover is sent when this changes.</summary>
    public bool SameThing(in ClickTarget other)
        => Kind == other.Kind && Id == other.Id && X == other.X && Y == other.Y && Z == other.Z;
}
