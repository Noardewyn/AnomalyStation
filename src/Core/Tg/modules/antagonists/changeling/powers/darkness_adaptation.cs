// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/changeling/powers/darkness_adaptation.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: Darkness Adaptation and its status effect (the skin fading and darkening with the turf's light,
// the eyes' lighting cutoff and flash protection following it, the shimmer on examine). The var blocks are generated (D111).

// tg: code/modules/antagonists/changeling/powers/darkness_adaptation.dm:1
public partial class datum_action_changeling_darkness_adaptation : datum_action_changeling
{
    // tg: code/modules/antagonists/changeling/powers/darkness_adaptation.dm:14
    [DmParams("cling")]
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null) //SHOULD always be human, because req_human = TRUE
    {
        mob_living_carbon_human cling = user as mob_living_carbon_human;
        if (Dm.truthy(cling.has_status_effect(typeof(datum_status_effect_darkness_adapted))))
        {
            disable_ability(cling);
            cling.changeNext_move(CLICK_CD_MELEE * 0.5f);
            chemical_cost = Dm.initial(this, (datum_action_changeling_darkness_adaptation p) => p.chemical_cost);
            return FALSE;
        }

        await base.sting_action(user, target);
        await enable_ability(cling);
        chemical_cost = 0;
        return TRUE;
    }

    // tg: code/modules/antagonists/changeling/powers/darkness_adaptation.dm:26
    public override async DmTask Remove(mob cling = null)
    {
        disable_ability(cling as mob_living_carbon_human);
        await base.Remove(cling);
    }

    // tg: code/modules/antagonists/changeling/powers/darkness_adaptation.dm:30
    public virtual async DmTask enable_ability(mob_living_carbon_human cling = null)
    {
        if (!Dm.truthy(await cling.apply_status_effect(typeof(datum_status_effect_darkness_adapted))))
            return;

        cling.visible_message(
            span_warning(I18n.T("[]'s skin suddenly starts shifting and shimmering!", cling)),
            span_notice(I18n.T("We shift our skin to adapt to the darkness.")));
        datum_antagonist_changeling changeling_data = cling.mind?.has_antag_datum(typeof(datum_antagonist_changeling)) as datum_antagonist_changeling;
        changeling_data?.chem_recharge_slowdown -= recharge_slowdown; //Slows down chem regeneration
    }

    // tg: code/modules/antagonists/changeling/powers/darkness_adaptation.dm:41
    public virtual void disable_ability(mob_living_carbon_human cling = null)
    {
        if (!Dm.truthy(cling.remove_status_effect(typeof(datum_status_effect_darkness_adapted))))
            return;

        cling.visible_message(
            span_warning(I18n.T("[]'s skin goes from shimmering to normal.", cling)),
            span_notice(I18n.T("We stop adapting our skin to the darkness.")));
        datum_antagonist_changeling changeling_data = cling.mind?.has_antag_datum(typeof(datum_antagonist_changeling)) as datum_antagonist_changeling;
        changeling_data?.chem_recharge_slowdown += recharge_slowdown;
    }
}

/// Makes the user harder to see in the dark (and makes the user see in the dark easier)
// tg: code/modules/antagonists/changeling/powers/darkness_adaptation.dm:54
public partial class datum_status_effect_darkness_adapted : datum_status_effect
{
    // tg: code/modules/antagonists/changeling/powers/darkness_adaptation.dm:66
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        RegisterSignal(owner, COMSIG_MOVABLE_MOVED, nameof(on_move));
        RegisterSignal(owner, COMSIG_CARBON_GAIN_ORGAN, nameof(eye_implanted));
        RegisterSignal(owner, COMSIG_CARBON_LOSE_ORGAN, nameof(eye_removed));
        RegisterSignal(owner, COMSIG_ATOM_EXAMINE, nameof(examine_mob));
        update_eye_status();
        await update_invis();
        return TRUE;
    }

    // tg: code/modules/antagonists/changeling/powers/darkness_adaptation.dm:75
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, Dm.list(
            COMSIG_CARBON_GAIN_ORGAN,
            COMSIG_CARBON_LOSE_ORGAN,
            COMSIG_MOVABLE_MOVED,
            COMSIG_ATOM_EXAMINE));
        if (last_eye_strength > 0)
            nerf_eyes(owner.get_organ_by_type(typeof(obj_item_organ_eyes)) as obj_item_organ_eyes);
        if (last_alpha < 255)
            await nerf_invis();
        return null;
    }

    // tg: code/modules/antagonists/changeling/powers/darkness_adaptation.dm:87
    [SignalHandler]
    public virtual void examine_mob(datum source = null, mob user = null, DmList examine_list = null)
    {
        if (last_alpha > dark_color_threshold)
            Dm.append(ref examine_list, span_warning(I18n.T("[] skin is shimmering unnaturally in the light.", owner.p_Their())));
    }

    // tg: code/modules/antagonists/changeling/powers/darkness_adaptation.dm:93
    public virtual object get_darkness()
    {
        turf owner_turf = get_turf(owner);
        return (owner_turf is turf ? owner_turf.get_lumcount() : 1);
    }

    // tg: code/modules/antagonists/changeling/powers/darkness_adaptation.dm:97
    public virtual object get_alpha()
    {
        return clamp(65 + ((Dm.num(get_darkness()) - LIGHTING_TILE_IS_DARK) * 255), 65, 255);
    }

    // tg: code/modules/antagonists/changeling/powers/darkness_adaptation.dm:100
    public virtual object get_eye_strength()
    {
        return clamp(LIGHTING_CUTOFF_MEDIUM - ((Dm.num(get_darkness()) - LIGHTING_TILE_IS_DARK) * LIGHTING_CUTOFF_MEDIUM + 5), LIGHTING_CUTOFF_REAL_LOW, LIGHTING_CUTOFF_MEDIUM + 5);
    }

    // tg: code/modules/antagonists/changeling/powers/darkness_adaptation.dm:103
    public virtual void update_eye_status(obj_item_organ_eyes eyes = null)
    {
        eyes ??= owner.get_organ_by_type(typeof(obj_item_organ_eyes)) as obj_item_organ_eyes;
        if (!(eyes is obj_item_organ_eyes))
        {
            last_eye_strength = 0;
            return;
        }
        object new_eye_strength = get_eye_strength();
        if (Dm.eq(last_eye_strength, new_eye_strength))
            return;
        buff_eyes(eyes, new_eye_strength);
        last_eye_strength = Dm.num(new_eye_strength);
    }

    // tg: code/modules/antagonists/changeling/powers/darkness_adaptation.dm:113
    public virtual void buff_eyes(obj_item_organ_eyes eyes = null, object new_strength = null)
    {
        new_strength ??= get_eye_strength();
        eyes.lighting_cutoff = new_strength;
        if (Dm.ge(new_strength, LIGHTING_CUTOFF_MEDIUM))
            eyes.flash_protect = max(eyes.flash_protect += 1, FLASH_PROTECTION_WELDER);
        else if (last_eye_strength >= LIGHTING_CUTOFF_MEDIUM)
            eyes.flash_protect = max(eyes.flash_protect -= 1, FLASH_PROTECTION_HYPER_SENSITIVE);
        owner.update_sight();
    }

    // tg: code/modules/antagonists/changeling/powers/darkness_adaptation.dm:121
    public virtual void nerf_eyes(obj_item_organ_eyes eyes = null)
    {
        eyes.lighting_cutoff = Dm.initial(eyes, (obj_item_organ_eyes p) => p.lighting_cutoff);
        if (last_eye_strength >= LIGHTING_CUTOFF_MEDIUM)
            eyes.flash_protect = max(eyes.flash_protect -= 1, FLASH_PROTECTION_HYPER_SENSITIVE);
        owner.update_sight();
    }

    // tg: code/modules/antagonists/changeling/powers/darkness_adaptation.dm:127
    public virtual async DmTask update_invis()
    {
        object new_alpha = get_alpha();
        if (Dm.eq(last_alpha, new_alpha))
            return;
        animate(owner, Dm.named("alpha", new_alpha), Dm.named("time", 0.5f * SECONDS), Dm.named("flags", ANIMATION_PARALLEL));
        if (Dm.le(new_alpha, dark_color_threshold))
            await owner.add_atom_colour(COLOR_DARK, TEMPORARY_COLOUR_PRIORITY);
        else
            await owner.remove_atom_colour(TEMPORARY_COLOUR_PRIORITY, COLOR_DARK);
        last_alpha = Dm.num(new_alpha);
    }

    // tg: code/modules/antagonists/changeling/powers/darkness_adaptation.dm:138
    public virtual async DmTask nerf_invis()
    {
        animate(owner, Dm.named("alpha", 255), Dm.named("time", 1 * SECONDS));
        await owner.remove_atom_colour(TEMPORARY_COLOUR_PRIORITY, COLOR_DARK);
    }

    // tg: code/modules/antagonists/changeling/powers/darkness_adaptation.dm:142
    [SignalHandler]
    public virtual void on_move(params object[] __extra)
    {
        Waiting.NoSleep(update_invis());
        update_eye_status();
        skip_tick_update = world.time + 0.5f * SECONDS; // COOLDOWN_START(src, skip_tick_update, 0.5 SECONDS) (__DEFINES/cooldowns.dm), inline (rules 11.1 (a))
    }

    // tg: code/modules/antagonists/changeling/powers/darkness_adaptation.dm:149
    [SignalHandler]
    public virtual void eye_implanted(mob_living source = null, obj_item_organ gained = null, object special = null)
    {
        if (gained is obj_item_organ_eyes)
            update_eye_status(gained as obj_item_organ_eyes);
    }

    // tg: code/modules/antagonists/changeling/powers/darkness_adaptation.dm:155
    [SignalHandler]
    public virtual void eye_removed(mob_living source = null, obj_item_organ removed = null, object special = null)
    {
        if (removed is obj_item_organ_eyes && last_eye_strength > 0)
        {
            nerf_eyes(removed as obj_item_organ_eyes);
            last_eye_strength = 0;
        }
    }

    // tg: code/modules/antagonists/changeling/powers/darkness_adaptation.dm:162
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (!(skip_tick_update <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
            return null;
        update_eye_status();
        await update_invis();
        return null;
    }
}
