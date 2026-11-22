// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/slime/defense.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

public partial class mob_living_basic_slime
{
    // tg: code/modules/mob/living/basic/slime/defense.dm:1
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        adjust_power_level(-SLIME_MAX_POWER); // oh no, the power!
        return dot;
    }

    ///Handles a slime being attacked by hand: a chance to wrestle it off
    // tg: code/modules/mob/living/basic/slime/defense.dm:8
    [SignalHandler]
    public virtual void on_attack_hand(mob_living_basic_slime defender_slime = null, mob_living attacker = null)
    {
        if (buckled == null)
            return;

        if (buckled == attacker ? prob(60) : prob(30)) //its easier to remove the slime from yourself
        {
            attacker.visible_message(span_warning(Dm.text("[] attempts to wrestle \\the [] off [] !", attacker, defender_slime.name, buckled == attacker ? "" : buckled)),
            span_danger(Dm.text("[] to wrestle \\the [] off []!", buckled == attacker ? "You attempt" : Dm.text("[] attempts", attacker), defender_slime.name, buckled == attacker ? "" : buckled)));
            playsound(loc, Dm.resource("sound/items/weapons/punchmiss.ogg"), 25, TRUE, -1);
            return;
        }

        attacker.visible_message(span_warning(Dm.text("[] manages to wrestle \\the [] off!", attacker, defender_slime.name)), span_notice(Dm.text("You manage to wrestle \\the [] off!", defender_slime.name)));
        playsound(loc, Dm.resource("sound/items/weapons/shove.ogg"), 50, TRUE, -1);

        defender_slime.discipline_slime();
    }

    // tg: code/modules/mob/living/basic/slime/defense.dm:24
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_stack_sheet_mineral_plasma plasma && !IS_UNCONSCIOUS_OR_CRIT(this))
        {
            await use_sheet(plasma, user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(await check_item_passthrough(tool, user)))
            return ITEM_INTERACT_SUCCESS;

        try_discipline_slime(tool);

        if (tool is not obj_item_storage_bag_xeno xeno_bag)
            return await base.item_interaction(user, tool, modifiers, __extra);

        await use_xeno_bag(xeno_bag, user);
        return ITEM_INTERACT_SUCCESS;
    }

    ///Checks if an item passes through the slime
    // tg: code/modules/mob/living/basic/slime/defense.dm:39
    public virtual async DmTask<object> check_item_passthrough(obj_item attacking_item = null, mob_living user = null)
    {
        if (attacking_item.force <= 0)
            return FALSE;

        if (!prob(25))
            return FALSE;

        await user.do_attack_animation(this);
        user.changeNext_move(CLICK_CD_MELEE);
        to_chat(user, span_danger(Dm.text("[] passes right through []!", attacking_item, this)));
        return TRUE;
    }

    ///Attacking a slime with something heavy can discipline it
    // tg: code/modules/mob/living/basic/slime/defense.dm:50
    public virtual void try_discipline_slime(obj_item attacking_item = null)
    {
        if (attacking_item.force < 3)
            return;

        float force_effect = attacking_item.force * (Dm.eq(life_stage, SLIME_LIFE_STAGE_BABY) ? 2 : 1);
        if (prob(10 + force_effect))
            discipline_slime();
    }

    ///Feeding a plasma sheet to a slime makes it your friend
    // tg: code/modules/mob/living/basic/slime/defense.dm:58
    public virtual async DmTask use_sheet(obj_item_stack_sheet_mineral_plasma delicious_sheet = null, mob_living user = null)
    {
        await befriend(user);
        to_chat(user, span_notice("You feed the slime the plasma. It chirps happily."));
        await delicious_sheet.use(1);
        Dm.@new<obj_effect_temp_visual_heart>(loc);
    }

    ///Used to mutate a slime with an extract from a bag
    // tg: code/modules/mob/living/basic/slime/defense.dm:65
    public virtual async DmTask use_xeno_bag(obj_item_storage_bag_xeno xeno_bag = null, mob_living user = null)
    {
        if (!Dm.truthy(crossbreed_modification))
        {
            to_chat(user, span_warning("The slime is not currently being mutated."));
            return;
        }
        bool has_output = false; //Have we outputted text?
        bool has_found = false; //Have we found an extract to be added?
        foreach (obj_item_slime_extract extract in Dm.each<obj_item_slime_extract>(xeno_bag.contents))
        {
            if (Dm.eq(extract.crossbreed_modification, crossbreed_modification))
            {
                (xeno_bag.atom_storage as datum_storage).attempt_remove(extract, get_turf(this), silent: TRUE);
                qdel(extract);
                applied_crossbreed_amount++;
                has_found = true;
            }
            if (applied_crossbreed_amount >= SLIME_EXTRACT_CROSSING_REQUIRED)
            {
                to_chat(user, span_notice("You feed the slime as many of the extracts from the bag as you can, and it mutates!"));
                playsound(this, Dm.resource("sound/effects/blob/attackblob.ogg"), 50, TRUE);
                spawn_corecross();
                has_output = true;
                break;
            }
        }

        if (has_output)
            return;
        if (!has_found)
            to_chat(user, span_warning("There are no extracts in the bag that this slime will accept!"));
        else
        {
            to_chat(user, span_notice("You feed the slime some extracts from the bag."));
            playsound(this, Dm.resource("sound/effects/blob/attackblob.ogg"), 50, TRUE);
        }
    }

    ///Used to make the slime take damage from water
    // tg: code/modules/mob/living/basic/slime/defense.dm:90
    public virtual async DmTask apply_water()
    {
        await adjust_brute_loss(rand(15, 20));
        discipline_slime();
    }

    ///Disciplines a slime, makes it let go, clears targets and calms it
    // tg: code/modules/mob/living/basic/slime/defense.dm:95
    public virtual void discipline_slime()
    {
        stop_feeding(TRUE);
        datum_ai_controller controller = ai_controller as datum_ai_controller;
        if (Dm.eq(life_stage, SLIME_LIFE_STAGE_BABY) && prob(80))
        {
            controller?.clear_blackboard_key(BB_CURRENT_TARGET);
            controller?.clear_blackboard_key(BB_CURRENT_HUNTING_TARGET);
        }
        if (prob(10))
            controller?.set_blackboard_key(BB_SLIME_RABID, FALSE);
    }
}
