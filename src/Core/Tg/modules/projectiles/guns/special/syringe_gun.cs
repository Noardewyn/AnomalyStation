// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/special/syringe_gun.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: M8 slice C (D190): the syringe gun on M7x's gun core (loading and unloading syringes, the low power mode that
// embeds them, its overlay), the rapid and syndicate ones (var blocks) and the blowgun; the DNA injector gun (syndicate G4a,
// D316). The fantasy bonuses: gaps GU1 (the fantasy component that calls them is not ported). The var blocks are generated.

// tg: code/modules/projectiles/guns/special/syringe_gun.dm:1
public partial class obj_item_gun_syringe : obj_item_gun
{
    // tg: code/modules/projectiles/guns/special/syringe_gun.dm:33
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        chambered = Dm.@new<obj_item_ammo_casing_syringegun>(this);
        Waiting.NoSleep(recharge_newshot());
        return dot;
    }

    // tg: code/modules/projectiles/guns/special/syringe_gun.dm:38
    public override async DmTask apply_fantasy_bonuses(object bonus = null)
    {
        await base.apply_fantasy_bonuses(bonus);
        max_syringes = Dm.num(modify_fantasy_variable("max_syringes", max_syringes, bonus, minimum: 1));
    }

    // tg: code/modules/projectiles/guns/special/syringe_gun.dm:42
    public override async DmTask remove_fantasy_bonuses(object bonus = null)
    {
        max_syringes = Dm.num(reset_fantasy_variable("max_syringes", max_syringes));
        await base.remove_fantasy_bonuses(bonus);
    }

    // tg: code/modules/projectiles/guns/special/syringe_gun.dm:46
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (Dm.@in(gone, syringes))
            Dm.remove(ref syringes, gone);
        return dot;
    }

    // tg: code/modules/projectiles/guns/special/syringe_gun.dm:51
    public override async DmTask recharge_newshot(params object[] __extra)
    {
        if (!Dm.truthy(syringes.len))
            return;
        chambered.newshot();
        await base.recharge_newshot(__extra);
    }

    // tg: code/modules/projectiles/guns/special/syringe_gun.dm:57
    public override object can_shoot()
    {
        return syringes.len;
    }

    // tg: code/modules/projectiles/guns/special/syringe_gun.dm:60
    public override async DmTask handle_chamber(float? empty_chamber = null, float? from_firing = null, float? chamber_next_round = null)
    {
        if (chambered != null && chambered.loaded_projectile == null) //we just fired
            await recharge_newshot();
        update_appearance();
    }

    // tg: code/modules/projectiles/guns/special/syringe_gun.dm:65
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Can hold [] syringe\\s. Has [] syringe\\s remaining.", max_syringes, syringes.len)));
        if (Dm.truthy(low_power))
            Dm.append(ref dot, span_notice(I18n.T("Its pressure regulator is set to low power mode, making sure that syringes shot will embed and slowly bleed their reagents into their target.")));
        else
            Dm.append(ref dot, span_notice(I18n.T("Its pressure regulator is cranked to the max, instantly injecting the reagents at the cost of breaking the syringes fired.")));
        Dm.append(ref dot, span_notice(I18n.T("Right-click [] in-hand to switch it to [] power.", this, Dm.truthy(low_power) ? I18n.T("full") : I18n.T("low"))));
        return dot;
    }

    // tg: code/modules/projectiles/guns/special/syringe_gun.dm:74
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(syringes.len))
        {
            balloon_alert(user, I18n.T("it's empty!"));
            return FALSE;
        }

        obj_item_reagent_containers_syringe syringe = syringes[syringes.len] as obj_item_reagent_containers_syringe;

        if (syringe == null)
            return FALSE;
        await user.put_in_hands(syringe);

        syringes.Remove(syringe);
        balloon_alert(user, I18n.T("[] unloaded", syringe.name));
        update_appearance();
        return TRUE;
    }

    // tg: code/modules/projectiles/guns/special/syringe_gun.dm:90
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        object dot = await base.attack_self_secondary(user, modifiers);
        if (Dm.truthy(dot))
            return dot;

        low_power = Dm.not(low_power);
        if (Dm.truthy(low_power))
        {
            balloon_alert(user, I18n.T("enabled low power mode"));
            to_chat(user, span_notice(I18n.T("You carefully lower the pressure regulator setting, ensuring that fired syringes embed in your target.")));
        }
        else
        {
            balloon_alert(user, I18n.T("enabled high power mode"));
            to_chat(user, span_notice(I18n.T("You crank the pressure regulator to the max, making sure that fired syringes inject their contents instantly.")));
        }
        playsound(user, Dm.resource("sound/machines/click.ogg"), 75, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/projectiles/guns/special/syringe_gun.dm:106
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_reagent_containers_syringe)))
            return NONE;
        return await attempt_insert_syringe(user, (obj_item_reagent_containers_syringe)tool);
    }

    // tg: code/modules/projectiles/guns/special/syringe_gun.dm:111
    public virtual async DmTask<object> attempt_insert_syringe(mob_living user = null, obj_item_reagent_containers_syringe syringe = null)
    {
        if (istype(syringe, typeof(obj_item_reagent_containers_syringe_bluespace)))
        {
            balloon_alert(user, I18n.T("[] is too big!", syringe.name));
            return ITEM_INTERACT_BLOCKING;
        }
        if (syringes.len >= max_syringes)
        {
            balloon_alert(user, I18n.T("it's full!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.transferItemToLoc(syringe, this)))
            return ITEM_INTERACT_BLOCKING;

        balloon_alert(user, I18n.T("[] loaded", syringe.name));
        Dm.append(ref syringes, syringe);
        await recharge_newshot();
        update_appearance();
        playsound(this, load_sound, 40);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/projectiles/guns/special/syringe_gun.dm:129
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (!Dm.truthy(has_syringe_overlay))
            return dot;
        float syringe_count = syringes.len;
        Dm.append(ref dot, Dm.text("[]_[]", Dm.initial(this, (obj_item_gun_syringe p) => p.icon_state), Dm.truthy(syringe_count) ? clamp(syringe_count, 1, Dm.num(Dm.initial(this, (obj_item_gun_syringe p) => p.max_syringes))) : "empty"));
        return dot;
    }
}

// tg: code/modules/projectiles/guns/special/syringe_gun.dm:213
public partial class obj_item_gun_syringe_blowgun : obj_item_gun_syringe
{
    // tg: code/modules/projectiles/guns/special/syringe_gun.dm:235
    public override async DmTask<object> process_fire(atom target = null, mob_living user = null, float? message = null, object @params = null, string zone_override = null, float? bonus_spread = null)
    {
        object dot = await base.process_fire(target, user, message, @params, zone_override, bonus_spread);
        if (!Dm.truthy(dot))
            return dot;
        visible_message(span_danger(I18n.T("[] shoots the blowgun!", user)));

        await user.adjust_stamina_loss(20, updating_stamina: FALSE);
        await user.adjust_oxy_loss(20);
        return dot;
    }
}

// tg: code/modules/projectiles/guns/special/syringe_gun.dm:174  (syndicate G4a, D316)
public partial class obj_item_gun_syringe_dna : obj_item_gun_syringe
{
    // tg: code/modules/projectiles/guns/special/syringe_gun.dm:190
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        chambered = Dm.@new<obj_item_ammo_casing_dnainjector>(this);
        return dot;
    }

    // tg: code/modules/projectiles/guns/special/syringe_gun.dm:194
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_dnainjector D)
        {
            if (Dm.truthy(D.used))
            {
                balloon_alert(user, I18n.T("[] is used up!", D.name));
                return ITEM_INTERACT_BLOCKING;
            }
            if (syringes.len < max_syringes)
            {
                if (!Dm.truthy(await user.transferItemToLoc(D, this)))
                    return ITEM_INTERACT_BLOCKING;
                balloon_alert(user, I18n.T("[] loaded", D.name));
                Dm.append(ref syringes, D);
                await recharge_newshot();
                update_appearance();
                playsound(loc, load_sound, 40);
                return ITEM_INTERACT_SUCCESS;
            }
            balloon_alert(user, I18n.T("it's already full!"));
            return ITEM_INTERACT_BLOCKING;
        }
        return NONE;
    }
}
