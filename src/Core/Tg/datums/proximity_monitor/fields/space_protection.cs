// Translated from tgstation (snapshot 1d99bd7f): code/datums/proximity_monitor/fields/space_protection.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DV2 (D333): the file whole, for the space furnace (devices/flashlight.dm's space bubble): a bubble four tiles wide, drawn with
// tg's "space_protection" field edges, that keeps the living inside it from the cold, the low pressure and the need to breathe. The
// var block is generated (D111).

//Bubble that grants space protection to those inside of it.
// tg: code/datums/proximity_monitor/fields/space_protection.dm:2
public partial class datum_proximity_monitor_advanced_bubble_space_protection : datum_proximity_monitor_advanced_bubble
{
    ///List of all traits that's given to mobs in the field, which is our "space proof" we grant.
    // tg: code/datums/proximity_monitor/fields/space_protection.dm:5
    public static DmList traits_to_give => TgState.Current.datum_proximity_monitor_advanced_bubble_space_protection__traits_to_give ??= Dm.list(
        TRAIT_RESISTCOLD,
        TRAIT_RESISTLOWPRESSURE,
        TRAIT_NOBREATH);

    // tg: code/datums/proximity_monitor/fields/space_protection.dm:11
    public override void setup_effect_directions()
    {
        effect_direction_images = Dm.list(
            Dm.kv(Dm.text("[]", SOUTH), image(Dm.resource("icons/effects/fields.dmi"), icon_state: "space_protection_south")),
            Dm.kv(Dm.text("[]", NORTH), image(Dm.resource("icons/effects/fields.dmi"), icon_state: "space_protection_north")),
            Dm.kv(Dm.text("[]", WEST), image(Dm.resource("icons/effects/fields.dmi"), icon_state: "space_protection_west")),
            Dm.kv(Dm.text("[]", EAST), image(Dm.resource("icons/effects/fields.dmi"), icon_state: "space_protection_east")),
            Dm.kv(Dm.text("[]", NORTHWEST), image(Dm.resource("icons/effects/fields.dmi"), icon_state: "space_protection_northwest")),
            Dm.kv(Dm.text("[]", SOUTHWEST), image(Dm.resource("icons/effects/fields.dmi"), icon_state: "space_protection_southwest")),
            Dm.kv(Dm.text("[]", NORTHEAST), image(Dm.resource("icons/effects/fields.dmi"), icon_state: "space_protection_northeast")),
            Dm.kv(Dm.text("[]", SOUTHEAST), image(Dm.resource("icons/effects/fields.dmi"), icon_state: "space_protection_southeast"))
        );
    }

    // tg: code/datums/proximity_monitor/fields/space_protection.dm:23
    public override void field_turf_crossed(atom_movable movable = null, turf old_location = null, turf new_location = null)
    {
        if (!isliving(movable) || HAS_TRAIT_FROM(movable, traits_to_give[1], REF(this)))
            return;
        give_space_immunity((mob_living)movable);
    }

    // tg: code/datums/proximity_monitor/fields/space_protection.dm:28
    public override void field_turf_uncrossed(atom_movable movable = null, turf old_location = null, turf new_location = null)
    {
        if (!isliving(movable) || get_dist(new_location, host) <= (Dm.truthy(edge_is_a_field) ? Dm.num(current_range) : Dm.num(current_range) - 1))
            return;
        remove_space_immunity((mob_living)movable);
    }

    // tg: code/datums/proximity_monitor/fields/space_protection.dm:33
    public override void setup_field_turf(turf target = null)
    {
        foreach (mob possible_mob in Dm.each<mob>(target))
            give_space_immunity(possible_mob as mob_living);
    }

    // tg: code/datums/proximity_monitor/fields/space_protection.dm:37
    public override void cleanup_field_turf(turf target = null)
    {
        foreach (mob possible_mob in Dm.each<mob>(target))
        {
            if (HAS_TRAIT_FROM(possible_mob, traits_to_give[1], REF(this)))
                remove_space_immunity(possible_mob as mob_living);
        }
    }

    ///a mob has entered our field, apply the space protection to them.
    // tg: code/datums/proximity_monitor/fields/space_protection.dm:43
    public virtual void give_space_immunity(mob_living new_immunne = null)
    {
        new_immunne.add_traits(traits_to_give, REF(this));
    }

    ///removing the effects after the mob has exited our field.
    // tg: code/datums/proximity_monitor/fields/space_protection.dm:47
    public virtual void remove_space_immunity(mob_living no_longer_immune = null)
    {
        no_longer_immune.remove_traits(traits_to_give, REF(this));
    }
}

public sealed partial class TgState
{
    // tg: code/datums/proximity_monitor/fields/space_protection.dm:5
    internal DmList datum_proximity_monitor_advanced_bubble_space_protection__traits_to_give;
}
