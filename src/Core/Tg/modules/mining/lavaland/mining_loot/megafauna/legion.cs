// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/lavaland/mining_loot/megafauna/legion.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

// Staff of storms

// tg: code/modules/mining/lavaland/mining_loot/megafauna/legion.dm:3
public partial class obj_item_storm_staff : obj_item
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/legion.dm:23  var/static/list/excluded_areas = list(/area/space)
    public static ref DmList excluded_areas => ref TgState.Current.obj_item_storm_staff__excluded_areas;

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/legion.dm:26
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("It has [] charges remaining.", thunder_charges)));
        Dm.append(ref dot, span_notice(I18n.T("Use it in hand to dispel storms.")));
        Dm.append(ref dot, span_notice(I18n.T("Use it on targets to summon thunderbolts from the sky.")));
        Dm.append(ref dot, span_notice(I18n.T("The thunderbolts are boosted if in an area with weather effects.")));
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/legion.dm:33
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        area user_area = get_area(user) as area;
        turf user_turf = get_turf(user);
        if (user_area == null || user_turf == null || Dm.truthy(is_type_in_list(user_area, excluded_areas)))
        {
            to_chat(user, span_warning(I18n.T("Something is preventing you from using the staff here.")));
            return null;
        }
        datum_weather affected_weather = null;
        foreach (datum_weather weather in Dm.each_cast<datum_weather>(SSweather.processing))
        {
            if (Dm.@in(user_turf.z, weather.impacted_z_levels) && ispath(user_area.type, weather.area_type))
            {
                affected_weather = weather;
                break;
            }
        }
        if (affected_weather == null)
            return null;
        if (affected_weather.stage == END_STAGE)
        {
            balloon_alert(user, I18n.T("already ended!"));
            return null;
        }
        if (affected_weather.stage == WIND_DOWN_STAGE)
        {
            balloon_alert(user, I18n.T("already ending!"));
            return null;
        }
        balloon_alert(user, I18n.T("you hold the staff up..."));
        if (!Dm.truthy(await do_after(user, 3 * SECONDS, this)))
        {
            balloon_alert(user, I18n.T("interrupted!"));
            return null;
        }
        user.visible_message(span_warning(I18n.T("[] holds [] skywards as an orange beam travels into the sky!", user, this)),
            span_notice(I18n.T("You hold [] skyward, dispelling the storm!", this)));
        playsound(user, Dm.resource("sound/effects/magic/staff_change.ogg"), 200, FALSE);
        object old_color = user.color;
        user.color = Dm.list(340f / 255, 240f / 255, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0);
        object old_transform = user.transform;
        user.transform = Dm.mul(user.transform, 1.2f);
        animate(user, Dm.named("color", old_color), Dm.named("transform", old_transform), Dm.named("time", 1 * SECONDS));
        affected_weather.wind_down();
        user.log_message(Dm.text("has dispelled a storm at [].", AREACOORD(user_turf)), LOG_GAME);
        return null;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/legion.dm:67
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        await DmTask.CompletedTask;
        return Dm.truthy(thunder_blast(interacting_with, user)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/legion.dm:70
    public override async DmTask afterattack(atom target = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        await DmTask.CompletedTask;
        thunder_blast(target, user);
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/legion.dm:73
    public virtual object thunder_blast(atom target = null, mob user = null)
    {
        if (!Dm.truthy(thunder_charges))
        {
            balloon_alert(user, I18n.T("needs to charge!"));
            return FALSE;
        }
        turf target_turf = get_turf(target);
        area target_area = get_area(target) as area;
        if (target_turf == null || target_area == null || Dm.truthy(is_type_in_list(target_area, excluded_areas)))
        {
            balloon_alert(user, I18n.T("can't bolt here!"));
            return FALSE;
        }
        if (Dm.@in(target_turf, targeted_turfs))
        {
            balloon_alert(user, I18n.T("already targeted!"));
            return FALSE;
        }
        if (HAS_TRAIT(user, TRAIT_PACIFISM))
        {
            balloon_alert(user, I18n.T("you don't want to harm!"));
            return FALSE;
        }
        bool power_boosted = false;
        foreach (datum_weather weather in Dm.each_cast<datum_weather>(SSweather.processing))
        {
            if (weather.stage != MAIN_STAGE)
                continue;
            if (Dm.@in(target_turf.z, weather.impacted_z_levels) && ispath(target_area.type, weather.area_type))
            {
                power_boosted = true;
                break;
            }
        }
        playsound(this, Dm.resource("sound/effects/magic/lightningshock.ogg"), 10, TRUE, extrarange: SILENCED_SOUND_EXTRARANGE, falloff_distance: 0);
        Dm.append(ref targeted_turfs, target_turf);
        balloon_alert(user, I18n.T("you aim at []...", target_turf));
        Dm.@new<obj_effect_temp_visual_telegraphing_circle>(target_turf);
        _addtimer(Dm.@new<datum_callback>(this, nameof(throw_thunderbolt), target_turf, power_boosted ? TRUE : FALSE), 1.5f * SECONDS, file: "code/modules/mining/lavaland/mining_loot/megafauna/legion.dm", line: 99);
        thunder_charges--;
        _addtimer(Dm.@new<datum_callback>(this, nameof(recharge)), thunder_charge_time, file: "code/modules/mining/lavaland/mining_loot/megafauna/legion.dm", line: 101);
        user.log_message(Dm.text("fired the staff of storms at [].", AREACOORD(target_turf)), LOG_ATTACK);
        return TRUE;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/legion.dm:105
    public virtual void recharge(mob user = null)
    {
        thunder_charges = min(thunder_charges + 1, max_thunder_charges);
        playsound(this, Dm.resource("sound/effects/magic/charge.ogg"), 10, TRUE, extrarange: SILENCED_SOUND_EXTRARANGE, falloff_distance: 0);
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/legion.dm:109
    public virtual async DmTask throw_thunderbolt(turf target = null, object boosted = null)
    {
        Dm.remove(ref targeted_turfs, target);
        Dm.@new<obj_effect_temp_visual_thunderbolt>(target);
        DmList affected_turfs = Dm.list(target);
        if (Dm.truthy(boosted))
        {
            foreach (object direction in Dm.each(GLOB.alldirs))
            {
                turf turf_to_add = get_step(target, Dm.num(direction));
                if (turf_to_add == null)
                    continue;
                Dm.append(ref affected_turfs, turf_to_add);
            }
        }
        foreach (turf turf in Dm.each_cast<turf>(affected_turfs))
        {
            Dm.@new<obj_effect_temp_visual_electricity>(turf);
            foreach (mob_living hit_mob in Dm.each<mob_living>(turf))
            {
                to_chat(hit_mob, span_userdanger(I18n.T("You've been struck by lightning!")));
                await hit_mob.electrocute_act(15 * (isanimal_or_basicmob(hit_mob) ? 3 : 1) * (turf == target ? 2 : 1) * (Dm.truthy(boosted) ? 2 : 1), this, flags: Dm.bitor(SHOCK_TESLA, SHOCK_NOSTUN));
            }

            foreach (obj hit_thing in Dm.each<obj>(turf))
                await hit_thing.take_damage(20, BURN, ENERGY, FALSE);
        }
        playsound(target, Dm.resource("sound/effects/magic/lightningbolt.ogg"), 100, TRUE);
        target.visible_message(span_danger(I18n.T("A thunderbolt strikes []!", target)));
        await explosion(target, light_impact_range: Dm.truthy(boosted) ? 1 : 0, flame_range: Dm.truthy(boosted) ? 2 : 1, silent: TRUE);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/legion.dm:23
    internal DmList obj_item_storm_staff__excluded_areas = Dm.list(typeof(area_space));
}
