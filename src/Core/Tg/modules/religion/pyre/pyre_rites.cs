// Translated from tgstation (snapshot 1d99bd7f): code/modules/religion/pyre/pyre_rites.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the pyre god's rites (unmelting protection, the burning offering, the immortal candles, the blazing star). The var
// blocks are generated (D111).

// tg: code/modules/religion/pyre/pyre_rites.dm:9 (the proc at :2 is declared before the type, rules 1.9)
public partial class datum_religion_rites_fireproof : datum_religion_rites
{
    ///apply a bunch of fire immunity effect to clothing
    // tg: code/modules/religion/pyre/pyre_rites.dm:3
    public virtual void apply_fireproof(obj_item_clothing fireproofed = null)
    {
        fireproofed.name = I18n.T("unmelting []", fireproofed.name);
        fireproofed.max_heat_protection_temperature = FIRE_IMMUNITY_MAX_TEMP_PROTECT;
        fireproofed.heat_protection = chosen_clothing.body_parts_covered;
        Dm.combine(ref fireproofed.resistance_flags, FIRE_PROOF);
    }

    // tg: code/modules/religion/pyre/pyre_rites.dm:21
    public override async DmTask<object> perform_rite(mob_living user = null, atom religious_tool = null)
    {
        foreach (obj_item_clothing apparel in Dm.each<obj_item_clothing>(get_turf(religious_tool)))
        {
            if (Dm.num(apparel.max_heat_protection_temperature) >= FIRE_IMMUNITY_MAX_TEMP_PROTECT)
                continue; //we ignore anything that is already fireproof
            chosen_clothing = apparel; //the apparel has been chosen by our lord and savior
            return await base.perform_rite(user, religious_tool);
        }
        return FALSE;
    }

    // tg: code/modules/religion/pyre/pyre_rites.dm:29
    public override async DmTask<object> invoke_effect(mob_living user = null, atom religious_tool = null)
    {
        await base.invoke_effect(user, religious_tool);
        if (!QDELETED(chosen_clothing) && get_turf(religious_tool) == chosen_clothing.loc) //check if the same clothing is still there
        {
            if (chosen_clothing is obj_item_clothing_suit_hooded)
            {
                foreach (obj_item_clothing_head integrated_helmet in Dm.each<obj_item_clothing_head>(chosen_clothing.contents)) //check if the clothing has a hood/helmet integrated and fireproof it if there is one.
                    apply_fireproof(integrated_helmet);
            }
            apply_fireproof(chosen_clothing);
            playsound(get_turf(religious_tool), Dm.resource("sound/effects/magic/fireball.ogg"), 50, TRUE);
            chosen_clothing = null; //our lord and savior no longer cares about this apparel
            return TRUE;
        }
        chosen_clothing = null;
        to_chat(user, span_warning(I18n.T("The clothing that was chosen for the rite is no longer on the altar!")));
        return FALSE;
    }
}

// tg: code/modules/religion/pyre/pyre_rites.dm:44
public partial class datum_religion_rites_burning_sacrifice : datum_religion_rites
{
    // tg: code/modules/religion/pyre/pyre_rites.dm:56
    public override async DmTask<object> perform_rite(mob_living user = null, atom religious_tool = null)
    {
        if (!ismovable(religious_tool))
        {
            to_chat(user, span_warning(I18n.T("This rite requires a religious device that individuals can be buckled to.")));
            return FALSE;
        }
        atom_movable movable_reltool = religious_tool as atom_movable;
        if (movable_reltool == null)
            return FALSE;
        if (!Dm.truthy(LAZYLEN(movable_reltool.buckled_mobs)))
        {
            to_chat(user, span_warning(I18n.T("Nothing is buckled to the altar!")));
            return FALSE;
        }
        foreach (object corpse in Dm.each(movable_reltool.buckled_mobs))
        {
            if (!iscarbon(corpse))// only works with carbon corpse since most normal mobs can't be set on fire.
            {
                to_chat(user, span_warning(I18n.T("Only carbon lifeforms can be properly burned for the sacrifice!")));
                return FALSE;
            }
            chosen_sacrifice = corpse as mob_living_carbon;
            if (!Dm.eq(chosen_sacrifice.stat, DEAD))
            {
                to_chat(user, span_warning(I18n.T("You can only sacrifice dead bodies, this one is still alive!")));
                return FALSE;
            }
            if (!Dm.truthy(chosen_sacrifice.on_fire) && !HAS_TRAIT_FROM(chosen_sacrifice, TRAIT_HUSK, BURN))
            {
                to_chat(user, span_warning(I18n.T("This corpse needs to be on fire or husked to be sacrificed!")));
                return FALSE;
            }
            return await base.perform_rite(user, religious_tool);
        }
        return null;
    }

    // tg: code/modules/religion/pyre/pyre_rites.dm:79
    public override async DmTask<object> invoke_effect(mob_living user = null, atom religious_tool = null)
    {
        await base.invoke_effect(user, religious_tool);
        if (!Dm.@in(chosen_sacrifice, ((atom_movable)religious_tool).buckled_mobs)) //checks one last time if the right corpse is still buckled
        {
            to_chat(user, span_warning(I18n.T("The right sacrifice is no longer on the altar!")));
            chosen_sacrifice = null;
            return FALSE;
        }
        if (!Dm.truthy(chosen_sacrifice.on_fire) && !HAS_TRAIT_FROM(chosen_sacrifice, TRAIT_HUSK, BURN))
        {
            to_chat(user, span_warning(I18n.T("The sacrifice has to be on fire or husked to finish the end of the rite!")));
            chosen_sacrifice = null;
            return FALSE;
        }
        if (!Dm.eq(chosen_sacrifice.stat, DEAD))
        {
            to_chat(user, span_warning(I18n.T("The sacrifice has to stay dead for the rite to work!")));
            chosen_sacrifice = null;
            return FALSE;
        }
        float favor_gained = 100 + round(Dm.num(chosen_sacrifice.get_fire_loss()));
        GLOB.religious_sect.adjust_favor(favor_gained, user);
        to_chat(user, span_notice(I18n.T("[] absorbs the charred corpse and any trace of fire with it. [] rewards you with [] favor.", GLOB.deity, GLOB.deity, favor_gained)));
        await chosen_sacrifice.dust(force: TRUE);
        playsound(get_turf(religious_tool), Dm.resource("sound/effects/supermatter.ogg"), 50, TRUE);
        chosen_sacrifice = null;
        return TRUE;
    }
}

// tg: code/modules/religion/pyre/pyre_rites.dm:101
public partial class datum_religion_rites_infinite_candle : datum_religion_rites
{
    // tg: code/modules/religion/pyre/pyre_rites.dm:108
    public override async DmTask<object> invoke_effect(mob_living user = null, atom religious_tool = null)
    {
        await base.invoke_effect(user, religious_tool);
        turf altar_turf = get_turf(religious_tool) as turf;
        for (float i = 1, i__end = 5; i <= i__end; i++)
            Dm.@new<obj_item_flashlight_flare_candle_infinite>(altar_turf);
        playsound(altar_turf, Dm.resource("sound/effects/magic/fireball.ogg"), 50, TRUE);
        return TRUE;
    }
}

// tg: code/modules/religion/pyre/pyre_rites.dm:116
public partial class datum_religion_rites_blazing_star : datum_religion_rites
{
    // tg: code/modules/religion/pyre/pyre_rites.dm:130
    public override async DmTask<object> perform_rite(mob_living user = null, atom religious_tool = null)
    {
        foreach (obj_item_ammo_casing_arrow_holy can_enchant in Dm.each<obj_item_ammo_casing_arrow_holy>(get_turf(religious_tool)))
        {
            if (can_enchant is obj_item_ammo_casing_arrow_holy_blazing)
                continue;
            enchant_target = can_enchant;
            return await base.perform_rite(user, religious_tool);
        }
        to_chat(user, span_warning(I18n.T("You need to place a holy arrow on [] to do this!", religious_tool)));
        return FALSE;
    }

    // tg: code/modules/religion/pyre/pyre_rites.dm:139
    public override async DmTask<object> invoke_effect(mob_living user = null, atom religious_tool = null)
    {
        await base.invoke_effect(user, religious_tool);
        obj_item_ammo_casing_arrow_holy enchanting = enchant_target;
        turf tool_turf = get_turf(religious_tool);
        enchant_target = null;
        if (QDELETED(enchanting) || !(tool_turf == enchanting.loc)) //check if the arrow is still there
        {
            to_chat(user, span_warning(I18n.T("Your target left the altar!")));
            return FALSE;
        }
        enchanting.visible_message(span_notice(I18n.T("[] is blessed by holy fire!", enchant_target)));
        playsound(tool_turf, Dm.resource("sound/effects/pray.ogg"), 50, TRUE);
        Dm.@new<obj_item_ammo_casing_arrow_holy_blazing>(tool_turf);
        qdel(enchanting);
        return TRUE;
    }
}
