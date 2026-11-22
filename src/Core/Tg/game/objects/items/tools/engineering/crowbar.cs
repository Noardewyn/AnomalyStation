// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/tools/engineering/crowbar.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the base crowbar's and the old crowbar's Initialize (M4a P4a); suicide_act is a live-partial stub (M10);
// the hammer and the mech removal crowbar come after the core version; the jaws of life are gaps J5's.

// tg: code/game/objects/items/tools/engineering/crowbar.dm:1
public partial class obj_item_crowbar : obj_item
{
    // tg: code/game/objects/items/tools/engineering/crowbar.dm:33
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/game/objects/items/tools/engineering/crowbar.dm:35", "Initialize: AddElement(/datum/element/falling_hazard, ...), the crowbar falling on a head (falls, M5)");
        return dot;
    }

    // tg: code/game/objects/items/tools/engineering/crowbar.dm:37  suicide_act: live-partial (M10).
}

// tg: code/game/objects/items/tools/engineering/crowbar.dm:104
public partial class obj_item_crowbar_large_old : obj_item_crowbar_large
{
    // tg: code/game/objects/items/tools/engineering/crowbar.dm:110
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (prob(50))
            icon_state = "crowbar_powergame";
        return dot;
    }
}

// M4a contracts: the roots of the later tools' procs (Dm.NotTranslated with their milestone).

// tg: code/game/objects/items/tools/engineering/crowbar.dm:116  (gaps J5, D298)
public partial class obj_item_crowbar_power : obj_item_crowbar
{
    // tg: code/game/objects/items/tools/engineering/crowbar.dm:161
    public override object get_all_tool_behaviours() => Dm.list(first_tool_behavior, second_tool_behavior);

    // tg: code/game/objects/items/tools/engineering/crowbar.dm:164
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_transforming),
            Dm.named("force_on", force),
            Dm.named("throwforce_on", throwforce),
            Dm.named("hitsound_on", hitsound),
            Dm.named("w_class_on", w_class),
            Dm.named("clumsy_check", FALSE),
            Dm.named("inhand_icon_change", FALSE)));
        RegisterSignal(this, COMSIG_TRANSFORMING_ON_TRANSFORM, nameof(on_transform));
        RegisterSignal(this, COMSIG_TOOL_FORCE_OPEN_AIRLOCK, nameof(on_force_open));
        return dot;
    }

    // tg: code/game/objects/items/tools/engineering/crowbar.dm:177
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        dot.Add(I18n.T("It's fitted with a [] head.", Dm.eq(tool_behaviour, first_tool_behavior) ? inactive_text : active_text));
        return dot;
    }

    /*
     * Signal proc for [COMSIG_TRANSFORMING_ON_TRANSFORM].
     *
     * Toggles between crowbar and wirecutters and gives feedback to the user.
     */
    // tg: code/game/objects/items/tools/engineering/crowbar.dm:186
    [SignalHandler]
    public virtual object on_transform(obj_item source = null, mob user = null, object active = null)
    {
        tool_behaviour = Dm.truthy(active) ? second_tool_behavior : first_tool_behavior;
        if (user != null)
            balloon_alert(user, I18n.T("attached []", Dm.eq(tool_behaviour, first_tool_behavior) ? inactive_text : active_text));
        playsound(this, Dm.resource("sound/items/tools/change_jaws.ogg"), 50, TRUE);
        if (!Dm.eq(tool_behaviour, TOOL_WIRECUTTER))
            RemoveElement(typeof(datum_element_cuffsnapping), snap_time_weak_handcuffs, snap_time_strong_handcuffs);
        else
            Waiting.NoSleep(AddElement(typeof(datum_element_cuffsnapping), snap_time_weak_handcuffs, snap_time_strong_handcuffs));
        return COMPONENT_NO_DEFAULT_MESSAGE;
    }

    /*
     * Signal proc for [COMSIG_TOOL_FORCE_OPEN_AIRLOCK].
     *
     * Determines if our jaws of life is restricted from opening some doors, and whether or not we need to alert over the radio whenever they are used to
     * pry open a door. Useful if you want to restrict jaws of life in some fashion.
     */
    // tg: code/game/objects/items/tools/engineering/crowbar.dm:207
    [SignalHandler]
    public virtual object on_force_open(obj_item source = null, mob user = null, obj_machinery_door_airlock target = null)
    {
        DmList collective_access = Dm.list();
        collective_access = Dm.add(collective_access, target.req_access) as DmList;
        collective_access = Dm.add(collective_access, target.req_one_access) as DmList;

        if (Dm.truthy(limit_jaws_access))
        {
            foreach (object possible_blacklisted_access in Dm.each(collective_access))
            {
                if (Dm.@in(possible_blacklisted_access, blacklisted_access))
                {
                    playsound(loc, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, FALSE);
                    user.balloon_alert(user, I18n.T("cannot pry open!"));
                    return COMPONENT_TOOL_DO_NOT_ALLOW_FORCE_OPEN;
                }
            }
        }

        if (Dm.truthy(radio_alert) && (alert_cooldown <= world.time))
        {
            // tg: `if(!collective_access)` never holds, a list is true (kept)
            foreach (object possible_public_access in Dm.each(collective_access)) //Return if the door has otherwise unimportant access
            {
                if (Dm.@in(possible_public_access, ignored_access))
                    return COMPONENT_TOOL_ALLOW_FORCE_OPEN;
            }

            Waiting.Detach(sound_the_alarms(user, target));
            alert_cooldown = world.time + (alert_cooldown_time);
        }
        return COMPONENT_TOOL_ALLOW_FORCE_OPEN;
    }

    ///Our alert for our jaws of life.
    // tg: code/game/objects/items/tools/engineering/crowbar.dm:236
    public virtual async DmTask sound_the_alarms(mob user = null, obj_machinery_door_airlock target = null)
    {
        await aas_config_announce(typeof(datum_aas_config_entry_jaws_entry_alert), Dm.list(
            Dm.kv("PERSON", user.name),
            Dm.kv("LOCATION", get_area_name(target)),
            Dm.kv("TOOL", name)), this, Dm.list(RADIO_CHANNEL_SECURITY), RADIO_CHANNEL_SECURITY);
    }

    // tg: code/game/objects/items/tools/engineering/crowbar.dm:252
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (Dm.eq(tool_behaviour, TOOL_CROWBAR))
        {
            user.visible_message(span_suicide(I18n.T("[] is putting [] head in [], it looks like [] trying to commit suicide!", user, user.p_their(), this, user.p_theyre())));
            playsound(loc, Dm.resource("sound/items/tools/jaws_pry.ogg"), 50, TRUE, -1);
        }
        else
        {
            user.visible_message(span_suicide(I18n.T("[] is wrapping \\the [] around [] neck. It looks like [] trying to rip [] head off!", user, this, user.p_their(), user.p_theyre(), user.p_their())));
            playsound(loc, Dm.resource("sound/items/tools/jaws_cut.ogg"), 50, TRUE, -1);
            if (iscarbon(user))
            {
                mob_living_carbon suicide_victim = user as mob_living_carbon;
                obj_item_bodypart target_bodypart = suicide_victim.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart;
                if (target_bodypart != null)
                {
                    await target_bodypart.drop_limb();
                    playsound(loc, SFX_DESECRATION, 50, TRUE, -1);
                }
            }
        }
        return BRUTELOSS;
    }
}

// tg: code/game/objects/items/tools/engineering/crowbar.dm:382
public partial class obj_item_crowbar_mechremoval : obj_item_crowbar
{
    // tg: code/game/objects/items/tools/engineering/crowbar.dm:382
    public virtual async DmTask empty_mech(obj_vehicle_sealed_mecha mech = null, mob user = null)
    {
        Dm.NotTranslated("code/game/objects/items/tools/engineering/crowbar.dm:382", "empty_mech: mechs (after the core version)");
    }

    // tg: code/game/objects/items/tools/engineering/crowbar.dm:411
    public virtual object extra_checks(obj_vehicle_sealed_mecha mech = null, object mech_dir = null, obj_item_mecha_parts_mecha_equipment_sleeper mech_sleeper = null)
    {
        Dm.NotTranslated("code/game/objects/items/tools/engineering/crowbar.dm:411", "extra_checks: mechs (after the core version)");
        return default;
    }
}
