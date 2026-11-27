// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/porta_turret/portable_turret_cover.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4d, D316); the AI's and the cyborgs' clicks since silicons SI2; the hulk's punch, which it hands to the
// turret, with the MetaStation leftovers. The var block is generated (D111).

/************************
* PORTABLE TURRET COVER *
************************/

// tg: code/game/machinery/porta_turret/portable_turret_cover.dm:5
public partial class obj_machinery_porta_turret_cover : obj_machinery
{
    // tg: code/game/machinery/porta_turret/portable_turret_cover.dm:15
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(parent_turret))
        {
            parent_turret.cover = null;
            parent_turret.RemoveInvisibility(type);
            parent_turret = null;
        }
        return base.Destroy(force);
    }

    //The below code is pretty much just recoded from the initial turret object. It's necessary but uncommented because it's exactly the same!
    //>necessary
    //I'm not fixing it because i'm fucking bored of this code already, but someone should just reroute these to the parent turret's procs.

    // tg: code/game/machinery/porta_turret/portable_turret_cover.dm:32
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        return await base.attack_hand(user, modifiers, __extra) is var dot && Dm.truthy(dot) ? dot : await parent_turret.attack_hand(user, modifiers);
    }

    // tg: code/game/machinery/porta_turret/portable_turret_cover.dm:35
    public override async DmTask<object> attack_ghost(mob_dead_observer user = null)
    {
        return await base.attack_ghost(user) is var dot && Dm.truthy(dot) ? dot : await parent_turret.attack_ghost(user);
    }

    // tg: code/game/machinery/porta_turret/portable_turret_cover.dm:38
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(parent_turret.locked))
        {
            user.balloon_alert(user, I18n.T("controls locked"));
            return ITEM_INTERACT_BLOCKING;
        }

        ((obj_item_multitool)tool).set_buffer(parent_turret);
        balloon_alert(user, I18n.T("saved to multitool buffer"));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/porta_turret/portable_turret_cover.dm:47
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(parent_turret.on) || Dm.truthy(parent_turret.raised))
            return NONE;

        if (Dm.truthy(parent_turret.anchored))
        {
            await parent_turret.set_anchored(FALSE);
            to_chat(user, span_notice(I18n.T("You unsecure the exterior bolts on the turret.")));
            parent_turret.SetInvisibility(INVISIBILITY_NONE, id: parent_turret.type, priority: INVISIBILITY_PRIORITY_TURRET_COVER);
            parent_turret.update_appearance();
            qdel(this);
            return ITEM_INTERACT_SUCCESS;
        }

        await parent_turret.set_anchored(TRUE);
        to_chat(user, span_notice(I18n.T("You secure the exterior bolts on the turret.")));
        parent_turret.SetInvisibility(INVISIBILITY_MAXIMUM, id: parent_turret.type, priority: INVISIBILITY_PRIORITY_TURRET_COVER);
        parent_turret.update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/porta_turret/portable_turret_cover.dm:66
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(tool.GetID()))
            return NONE;
        if (!Dm.truthy(await parent_turret.allowed__proc(user)))
        {
            to_chat(user, span_notice(I18n.T("Access denied.")));
            return ITEM_INTERACT_BLOCKING;
        }
        parent_turret.locked = Dm.truthy(parent_turret.locked) ? FALSE : TRUE;
        to_chat(user, span_notice(I18n.T("Controls are now [].", Dm.truthy(parent_turret.locked) ? "locked" : "unlocked")));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/porta_turret/portable_turret_cover.dm:76
    public override async DmTask<object> attacked_by(obj_item attacking_item = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        return await parent_turret.attacked_by(attacking_item, user, modifiers);
    }

    // tg: code/game/machinery/porta_turret/portable_turret_cover.dm:79
    public override async DmTask<object> attack_alien(mob_living_carbon user = null, DmList modifiers = null)
    {
        await parent_turret.attack_alien(user, modifiers);
        return null;
    }

    // tg: code/game/machinery/porta_turret/portable_turret_cover.dm:82
    public override async DmTask<object> attack_animal(mob user = null, DmList modifiers = null)
    {
        await parent_turret.attack_animal(user, modifiers);
        return null;
    }

    // tg: code/game/machinery/porta_turret/portable_turret_cover.dm:85
    public override async DmTask<object> attack_hulk(mob_living_carbon_human user = null, params object[] __extra)
    {
        return await parent_turret.attack_hulk(user);
    }

    // tg: code/game/machinery/porta_turret/portable_turret_cover.dm:88
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(parent_turret.obj_flags, EMAGGED)))
            return FALSE;

        balloon_alert(user, I18n.T("threat assessment circuits shorted"));
        audible_message(span_hear(I18n.T("[] hums oddly...", parent_turret)));
        Dm.combine(ref parent_turret.obj_flags, EMAGGED);
        parent_turret.on = FALSE;
        _addtimer(Dm.@new<datum_callback>(parent_turret, nameof(obj_machinery_porta_turret.varset_on_true)), 4 * SECONDS, file: "code/game/machinery/porta_turret/portable_turret_cover.dm", line: 97);
        return TRUE;
    }
}

// tg: code/game/machinery/porta_turret/portable_turret.dm:27
public partial class obj_machinery_porta_turret
{
    // tg: portable_turret_cover.dm:97  VARSET_CALLBACK(parent_turret, on, TRUE)
    public virtual void varset_on_true() => on = TRUE;
}

// The AI's and cyborgs' clicks (silicons SI2, D326)
// tg: code/game/machinery/porta_turret/portable_turret_cover.dm:27
public partial class obj_machinery_porta_turret_cover : obj_machinery
{
    // tg: code/game/machinery/porta_turret/portable_turret_cover.dm:27
    public override async DmTask<object> attack_ai(mob user = null)
    {
        object result = await base.attack_ai(user);
        return Dm.truthy(result) ? result : await parent_turret.attack_ai(user);
    }

    // tg: code/game/machinery/porta_turret/portable_turret_cover.dm:30
    public override async DmTask<object> attack_robot(mob user = null, object modifiers = null)
    {
        object result = await base.attack_robot(user);
        return Dm.truthy(result) ? result : await parent_turret.attack_robot(user);
    }
}
