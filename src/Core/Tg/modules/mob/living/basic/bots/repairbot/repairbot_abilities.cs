// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/repairbot/repairbot_abilities.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

// tg: code/modules/mob/living/basic/bots/repairbot/repairbot_abilities.dm:3
public partial class datum_action_cooldown_mob_cooldown_bot_build_girder : datum_action_cooldown_mob_cooldown_bot
{
    private const string BUILDING_WALL_ABILITY = "building wall ability";

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_abilities.dm:11
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        object dot = await base.IsAvailable(feedback);
        if (!Dm.truthy(dot))
            return FALSE;
        obj_item_stack_rods our_rods = Dm.locate<obj_item_stack_rods>(owner);
        if (our_rods == null || Dm.num(our_rods.amount) < 2)
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_abilities.dm:20
    public override async DmTask<object> Activate(atom target = null)
    {
        if (Dm.truthy(DOING_INTERACTION(owner, BUILDING_WALL_ABILITY)))
            return TRUE;
        if (!isopenturf(target) || Dm.truthy(isgroundlessturf(target)))
        {
            owner.balloon_alert(owner, "cant build here!");
            return TRUE;
        }
        obj_item_stack_rods our_rods = Dm.locate<obj_item_stack_rods>(owner);
        turf turf_target = (turf)target;
        if (Dm.truthy(await turf_target.is_blocked_turf()))
        {
            owner.balloon_alert(owner, "blocked!");
            return TRUE;
        }
        obj_effect_constructing_effect effect = Dm.@new<obj_effect_constructing_effect>(turf_target, 3 * SECONDS);

        if (!Dm.truthy(await do_after(owner, 3 * SECONDS, target: turf_target, interaction_key: BUILDING_WALL_ABILITY)) || turf_target == null || Dm.truthy(await turf_target.is_blocked_turf()))
        {
            qdel(effect);
            return TRUE;
        }

        playsound(turf_target, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        Dm.@new<obj_structure_girder>(turf_target);
        await our_rods.use(2);
        StartCooldown();
        qdel(effect);
        return TRUE;
    }
}

// tg: code/modules/mob/living/basic/bots/repairbot/repairbot_abilities.dm:47
public partial class datum_action_repairbot_resources : datum_action
{
    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_abilities.dm:54
    private static DmList eject_blacklist => TgState.Current.datum_action_repairbot_resources__eject_blacklist ??= typecacheof(Dm.list(typeof(obj_item_stack_rods))) as DmList;

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_abilities.dm:58
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        await ui_interact(owner);
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_abilities.dm:64
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "RepairbotResources");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_abilities.dm:70
    public override object ui_state(mob user = null)
    {
        return GLOB.always_state;
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_abilities.dm:73
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        DmList stacks = Dm.list();
        data["stacks"] = stacks;
        foreach (obj_item_stack managed_stack in Dm.each<obj_item_stack>(user.contents))
        {
            Dm.append(ref stacks, Dm.list(Dm.list(
                Dm.kv("stack_reference", REF(managed_stack)),
                Dm.kv("stack_name", managed_stack.name),
                Dm.kv("stack_amount", managed_stack.amount),
                Dm.kv("stack_maximum_amount", managed_stack.max_amount),
                Dm.kv("stack_icon", managed_stack.icon),
                Dm.kv("stack_icon_state", managed_stack.icon_state))));
        }

        return data;
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_abilities.dm:88
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["repairbot_icon"] = Dm.resource("icons/ui/repairbotmanagement/repairbot_smile.dmi");
        data["repairbot_icon_state"] = "repairbot_smile";
        return data;
    }

    // tg: code/modules/mob/living/basic/bots/repairbot/repairbot_abilities.dm:95
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (action as string)
        {
            case "eject":
                atom_movable my_sheet = Dm.locate_ref(@params["item_reference"]) as atom_movable;
                if (my_sheet != null && !Dm.@in(my_sheet, owner.contents))
                    my_sheet = null;
                if (my_sheet == null)
                    return null;
                if (Dm.truthy(is_type_in_typecache(my_sheet, eject_blacklist)))
                {
                    to_chat(owner, span_warning(Dm.text("You're unable to eject []!", my_sheet)));
                    return null;
                }

                await my_sheet.forceMove(owner.drop_location() as atom);
                break;
        }
        return null;
    }
}

// rules 10.6: the static vars above, made on first use
public sealed partial class TgState
{
    internal DmList datum_action_repairbot_resources__eject_blacklist;
}
