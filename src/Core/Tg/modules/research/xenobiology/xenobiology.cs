// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/xenobiology.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (syndicate G3c, D316): the slime potion base and the intelligence potions (sentience, the syndicate one and the
// dangerous pony potion of the uplink's pony box). The slime extracts, the other potions and the xenobiology console wait for
// the xenobiology lab. The var blocks are generated (D111).

// tg: code/modules/research/xenobiology/xenobiology.dm:682
public partial class obj_item_slimepotion : obj_item
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:688
    public override DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (is_reagent_container(interacting_with))
        {
            to_chat(user, span_warning(I18n.T("You cannot transfer [] to []! It appears the potion must be given directly to a slime or other object to absorb.", this, interacting_with)));
            return DmTask<object>.FromResult(ITEM_INTERACT_BLOCKING);
        }
        return DmTask<object>.FromResult(NONE);
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:736
public partial class obj_item_slimepotion_sentience : obj_item_slimepotion
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:746
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(I18n.T("Alt-click to set potion offer reason. []", Dm.truthy(potion_reason) ? I18n.T("Current reason: []", span_warning(Dm.str(potion_reason))) : null)));
        return dot;
    }

    // tg: code/modules/research/xenobiology/xenobiology.dm:750
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        // register_context(): the context tips (D264)
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/research/xenobiology/xenobiology.dm:754  add_context: the context tips (D264)

    // tg: code/modules/research/xenobiology/xenobiology.dm:758
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        potion_reason = await tgui_input_text(user, I18n.T("Enter reason for offering potion"), I18n.T("Intelligence Potion"), potion_reason, max_length: MAX_MESSAGE_LEN, multiline: TRUE);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/research/xenobiology/xenobiology.dm:762
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (((int)Dm.num(dot) & ITEM_INTERACT_ANY_BLOCKER) != 0)
            return dot;
        if (!isliving(interacting_with))
            return NONE;
        mob_living dumb_mob = (mob_living)interacting_with;
        if (Dm.truthy(being_used))
            return ITEM_INTERACT_BLOCKING;
        if (Dm.truthy(dumb_mob.ckey)) //only works on animals that aren't player controlled
        {
            balloon_alert(user, I18n.T("already sentient!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (IS_UNCONSCIOUS_OR_CRIT(dumb_mob))
        {
            balloon_alert(user, I18n.T("it's dead!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(dumb_mob.compare_sentience_type(sentience_type))) // Will also return false if not a basic or simple mob, which are the only two we want anyway
        {
            balloon_alert(user, I18n.T("invalid creature!"));
            return ITEM_INTERACT_BLOCKING;
        }
        balloon_alert(user, I18n.T("offering..."));
        being_used = TRUE;
        mob chosen_one = await SSpolling.poll_ghosts_for_target(
            question: I18n.T("[] is offering [] an intelligence potion![]", span_danger(user.name), span_notice(dumb_mob.name), Dm.truthy(potion_reason) ? I18n.T(" Reason: []", span_boldnotice(Dm.str(potion_reason))) : ""),
            check_jobban: ROLE_SENTIENCE,
            poll_time: 20 * SECONDS,
            checked_target: dumb_mob,
            ignore_category: POLL_IGNORE_SENTIENCE_POTION,
            alert_pic: dumb_mob,
            role_name_text: "intelligence potion",
            chat_text_border_icon: this) as mob;
        await on_poll_concluded(user, dumb_mob, chosen_one as mob_dead_observer);
        return ITEM_INTERACT_SUCCESS;
    }

    /// Assign the chosen ghost to the mob
    // tg: code/modules/research/xenobiology/xenobiology.dm:796
    public virtual async DmTask on_poll_concluded(mob user = null, mob_living dumb_mob = null, mob_dead_observer ghost = null)
    {
        if (ghost == null)
        {
            balloon_alert(user, I18n.T("try again later!"));
            being_used = FALSE;
            return;
        }

        dumb_mob.PossessByPlayer(ghost.key);
        await dumb_mob.mind.enslave_mind_to_creator(user as mob_living);
        dumb_mob.SEND_SIGNAL(COMSIG_SIMPLEMOB_SENTIENCEPOTION, Dm.list(dumb_mob, user));

        // if(isanimal(dumb_mob)) smart_animal.sentience_act(): the simple animals are not ported, and their compare_sentience_type
        // is not live, so a simple animal never gets here (the potion calls it "invalid creature!" above)
        if (isanimal(dumb_mob))
            Dm.Deferred("code/modules/research/xenobiology/xenobiology.dm:806", "on_poll_concluded: smart_animal.sentience_act() (the simple animals are not ported)");

        await dumb_mob.mind.add_antag_datum(typeof(datum_antagonist_sentient_creature));
        balloon_alert(user, I18n.T("success"));
        await after_success(user as mob_living, dumb_mob);
        qdel(this);
    }

    // tg: code/modules/research/xenobiology/xenobiology.dm:815
    public virtual DmTask after_success(mob_living user = null, mob_living smart_mob = null) => DmTask.CompletedTask;
}

// tg: code/modules/research/xenobiology/xenobiology.dm:818
public partial class obj_item_slimepotion_sentience_nuclear : obj_item_slimepotion_sentience
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:822
    public override async DmTask after_success(mob_living user = null, mob_living smart_mob = null)
    {
        obj_item_implant_radio_syndicate imp = Dm.@new<obj_item_implant_radio_syndicate>(this);
        await imp.implant(smart_mob, user);
        await smart_mob.AddComponent(typeof(datum_component_simple_access), Dm.list(ACCESS_SYNDICATE, ACCESS_MAINT_TUNNELS));
        // var/obj/item/implant/implanter = SSwardrobe.provide_type(/obj/item/implant/tacmap/nuclear/cayenne, src);
        // implanter.implant(src, null, TRUE): the nuclear operatives' tactical map implant (N1); tg implants it into the potion itself
        Dm.Deferred("code/modules/research/xenobiology/xenobiology.dm:826", "after_success: the tactical map implant /obj/item/implant/tacmap/nuclear/cayenne (the nuclear operatives' minimap, N1)");
    }
}
