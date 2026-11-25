// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/species_types/mushpeople.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Species package SP2b (D349): the whole file, the mushroomperson, its cap organ and the cap's bodypart overlay. The mushpunch martial art
// (datums/martial/mushpunch.cs) is taught on species gain. The var blocks are generated (D111). A dead mushroomperson's falling spores
// (the pollen particles around the corpse, a client visual) are left as a deferred leaf; the brown mushrooms that sprout are made.

// tg: code/modules/mob/living/carbon/human/species_types/mushpeople.dm:4
public partial class datum_species_mush : datum_species
{
    // tg: code/modules/mob/living/carbon/human/species_types/mushpeople.dm:41
    public override async DmTask on_species_gain(mob_living_carbon_human C = null, datum_species old_species = null, object pref_load = null, float? regenerate_icons = null, float? replace_missing = null)
    {
        await base.on_species_gain(C, old_species, pref_load, regenerate_icons, replace_missing);
        mush = Dm.@new<datum_martial_art_mushpunch>(this);
        mush.locked_to_use = TRUE;
        await mush.teach(C);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/mushpeople.dm:47
    public override async DmTask on_species_loss(mob_living_carbon_human C = null, datum_species new_species = null, object pref_load = null, params object[] __extra)
    {
        await base.on_species_loss(C, new_species, pref_load, __extra);
        QDEL_NULL(ref mush);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/mushpeople.dm:51
    public override object get_fixed_hair_color(mob_living_carbon for_mob = null)
    {
        return "#FF4B19"; //cap color, spot color uses eye color
    }
}

/// A mushpersons mushroom cap organ
// tg: code/modules/mob/living/carbon/human/species_types/mushpeople.dm:56
public partial class obj_item_organ_mushroom_cap : obj_item_organ
{
    // tg: code/modules/mob/living/carbon/human/species_types/mushpeople.dm:1
    private const float MUSHROOM_SPORE_COOLDOWN = 45 * SECONDS;

    // tg: code/modules/mob/living/carbon/human/species_types/mushpeople.dm:71
    [DmParams("organ_owner", "special", "movement_flags")]
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        RegisterSignal(organ_owner, COMSIG_MOB_STATCHANGE, nameof(on_stat_change));
        return dot;
    }

    /**
     * Checks to see if the owner of the mushroom cap has died, and if so, will randomly spore up the floors
     */
    // tg: code/modules/mob/living/carbon/human/species_types/mushpeople.dm:78
    [SignalHandler]
    public virtual void on_stat_change(mob_living victim = null, object new_stat = null)
    {
        if (!Dm.eq(new_stat, DEAD))
            return;

        if (QDELETED(victim))
            return; //can't leave spores if there's no shroom to spore

        victim.visible_message(span_notice(I18n.T("Spores start growing around [].", victim)), ignored_mobs: Dm.list(victim));
        // var/atom/particle_holder = victim.add_shared_particles(/particles/pollen/mushroom): the pollen particles are a client visual
        Dm.Deferred("code/modules/mob/living/carbon/human/species_types/mushpeople.dm:84", "on_stat_change: the pollen particles around the corpse (add_shared_particles, a client visual)");
        _addtimer(Dm.@new<datum_callback>(this, nameof(create_spores), victim), MUSHROOM_SPORE_COOLDOWN, file: "code/modules/mob/living/carbon/human/species_types/mushpeople.dm", line: 86);
    }

    /**
     * This proc cleans up mushroom particles and spawns a new patch of brown mushrooms like glowshrooms do.
     */
    // tg: code/modules/mob/living/carbon/human/species_types/mushpeople.dm:95
    public virtual void create_spores(mob_living victim = null)
    {
        if (QDELETED(victim))
            return; //can't leave more spores if there's no spores to spore

        victim.visible_message(span_notice(I18n.T("Mushrooms sprout up around [].", victim)), ignored_mobs: Dm.list(victim));
        Dm.@new<obj_structure_glowshroom_brownshroom>(get_turf(victim));
    }
}

/// Bodypart overlay for the mushroom cap organ
// tg: code/modules/mob/living/carbon/human/species_types/mushpeople.dm:105
public partial class datum_bodypart_overlay_mutant_mushroom_cap : datum_bodypart_overlay_mutant
{
    // tg: code/modules/mob/living/carbon/human/species_types/mushpeople.dm:110
    public override object can_draw_on_bodypart(obj_item_bodypart bodypart_owner = null, mob_living_carbon owner = null)
    {
        return Dm.truthy(base.can_draw_on_bodypart(bodypart_owner, owner)) && !Dm.truthy(Dm.bitand(bodypart_owner.owner?.obscured_slots, HIDEHAIR)) ? TRUE : FALSE;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/mushpeople.dm:113
    public override object override_color(obj_item_bodypart bodypart_owner = null)
    {
        //The mushroom cap is red by default (can still be dyed)
        return "#FF4B19";
    }
}
