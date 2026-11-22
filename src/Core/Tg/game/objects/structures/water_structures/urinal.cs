// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/water_structures/urinal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps H1: the whole file (the urinal, its drain enclosure with the urinal cake, the slam, its frame; the urinal cake's squish). The
// var blocks are generated (D111).

// tg: code/game/objects/structures/water_structures/urinal.dm:1
public partial class obj_structure_urinal : obj_structure
{
    // tg: code/game/objects/structures/water_structures/urinal.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload))
        {
            hidden_item = Dm.@new<obj_item_food_urinalcake>(this);
            Waiting.NoSleep(find_and_mount_on_atom());
        }
        return dot;
    }

    // tg: code/game/objects/structures/water_structures/urinal.dm:21
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == hidden_item)
            hidden_item = null;
        return dot;
    }

    // tg: code/game/objects/structures/water_structures/urinal.dm:26
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        var living = user as mob_living;

        if (living.pulling != null && isliving(living.pulling))
        {
            var grabbed_mob = (mob_living)living.pulling;
            if (living.grab_state >= GRAB_AGGRESSIVE)
            {
                if (grabbed_mob.loc != get_turf(this))
                {
                    to_chat(living, span_notice(I18n.T("[] needs to be on [].", grabbed_mob.name, this)));
                    return dot;
                }
                living.changeNext_move(CLICK_CD_MELEE);
                living.visible_message(span_danger(I18n.T("[] slams [] into []!", living, grabbed_mob, this)), span_danger(I18n.T("You slam [] into []!", grabbed_mob, this)));
                await grabbed_mob.emote("scream");
                await grabbed_mob.adjust_brute_loss(8);
            }
            else
                to_chat(living, span_warning(I18n.T("You need a tighter grip!")));
            return dot;
        }

        if (Dm.truthy(exposed))
        {
            if (hidden_item != null)
            {
                to_chat(living, span_notice(I18n.T("You fish [] out of the drain enclosure.", hidden_item)));
                await living.put_in_hands(hidden_item);
            }
            else
                to_chat(living, span_warning(I18n.T("There is nothing in the drain holder!")));
            return dot;
        }
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/game/objects/structures/water_structures/urinal.dm:54
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(exposed))
            return NONE;

        if (hidden_item != null)
        {
            to_chat(user, span_warning(I18n.T("There is already something in the drain enclosure!")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (tool.w_class > WEIGHT_CLASS_TINY)
        {
            to_chat(user, span_warning(I18n.T("[] is too large for the drain enclosure.", tool)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
        {
            to_chat(user, span_warning(I18n.T("[] is stuck to your hand, you cannot put it in the drain enclosure!", tool)));
            return ITEM_INTERACT_BLOCKING;
        }

        hidden_item = tool;
        to_chat(user, span_notice(I18n.T("You place [] into the drain enclosure.", tool)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/water_structures/urinal.dm:74
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(await base.screwdriver_act(user, tool)))
            return TRUE;
        to_chat(user, span_notice(Dm.truthy(exposed) ? I18n.T("You start to screw the cap back into place...") : I18n.T("You start to unscrew the cap to the drain protector...")));
        playsound(loc, Dm.resource("sound/effects/stonedoor_openclose.ogg"), 50, TRUE);
        if (Dm.truthy(await tool.use_tool(this, user, 20)))
        {
            user.visible_message(span_notice(Dm.truthy(exposed) ? I18n.T("[] screws the cap back into place!", user) : I18n.T("[] unscrew the cap to the drain protector!", user)),
                span_notice(Dm.truthy(exposed) ? I18n.T("You screw the cap back into place!") : I18n.T("You unscrew the cap on the drain!")),
                span_hear(I18n.T("You hear metal and squishing noises.")));
            exposed = Dm.truthy(exposed) ? FALSE : TRUE;
        }
        return TRUE;
    }

    // tg: code/game/objects/structures/water_structures/urinal.dm:86
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        tool.play_tool_sound(user);
        await deconstruct(TRUE);
        balloon_alert(user, "removed urinal");
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/water_structures/urinal.dm:92
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        Dm.@new<obj_item_wallframe_urinal>(loc);
        if (hidden_item != null)
            await hidden_item.forceMove(drop_location() as atom);
    }
}

// tg: code/game/objects/structures/water_structures/urinal.dm:104
public partial class obj_item_food_urinalcake : obj_item_food
{
    // tg: code/game/objects/structures/water_structures/urinal.dm:117
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        user.visible_message(span_notice(I18n.T("[] squishes []!", user, this)), span_notice(I18n.T("You squish [].", this)), I18n.T("<i>You hear a squish.</i>"));
        icon_state = "urinalcake_squish";
        // VARSET_CALLBACK(src, icon_state, "urinalcake"): ___callbackvarset is not declared (vars is not translated, rules 10.7), so the
        // timer calls icon_state__varset (as powernet.cs's flickering__varset)
        _addtimer(Dm.@new<datum_callback>(this, nameof(icon_state__varset), "urinalcake"), 0.8f * SECONDS, file: "code/game/objects/structures/water_structures/urinal.dm", line: 120);
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/__HELPERS/varset_callback.dm:3  VARSET_CALLBACK(src, icon_state, value)
    public virtual void icon_state__varset(object value = null) => icon_state = value as string;
}
