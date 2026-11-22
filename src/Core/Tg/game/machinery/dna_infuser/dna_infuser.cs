// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/dna_infuser/dna_infuser.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3b, D327): the DNA infuser: a dead creature or a valid sample in its slot, a subject in its chamber, the
// infusion with its needles, scream and shake, the organ it grows, the error report of a rejected or too complex sample, and the
// tiers it unlocks. The var block is generated (D111).

// tg: code/game/machinery/dna_infuser/dna_infuser.dm:6
public partial class obj_machinery_dna_infuser : obj_machinery
{
    /// how long it takes to infuse
    // tg: code/game/machinery/dna_infuser/dna_infuser.dm:2  #define INFUSING_TIME 4 SECONDS (file-local, #undef at :232)
    private const float INFUSING_TIME = 4 * SECONDS;
    /// we throw in a scream along the way.
    // tg: code/game/machinery/dna_infuser/dna_infuser.dm:4  #define SCREAM_TIME 3 SECONDS (file-local, #undef at :233)
    private const float SCREAM_TIME = 3 * SECONDS;

    // tg: code/game/machinery/dna_infuser/dna_infuser.dm:28
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        occupant_typecache = typecacheof(typeof(mob_living_carbon_human)) as DmList;
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/dna_infuser.dm:32
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        //dump_inventory_contents called by parent, emptying infusing_from
        infusing_into = null;
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/dna_infuser.dm:37
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (occupant == null)
            Dm.append(ref dot, span_notice(I18n.T("Requires []." , span_bold(I18n.T("a subject")))));
        else
            Dm.append(ref dot, span_notice(I18n.T("\"[]\" is inside the infusion chamber.", span_bold(occupant.name))));
        if (infusing_from == null)
            Dm.append(ref dot, span_notice(I18n.T("Missing [].", span_bold(I18n.T("an infusion source")))));
        else
            Dm.append(ref dot, span_notice(I18n.T("[] is in the infusion slot.", span_bold(infusing_from.name))));
        Dm.append(ref dot, span_notice(I18n.T("To operate: Obtain dead creature. Depending on size, drag or drop into the infuser slot.")));
        Dm.append(ref dot, span_notice(I18n.T("Subject enters the chamber, someone activates the machine. Voila! One of your organs has... changed!")));
        Dm.append(ref dot, span_notice(I18n.T("Alt-click to eject the infusion source, if one is inside.")));
        if (max_tier_allowed < DNA_INFUSER_MAX_TIER)
            Dm.append(ref dot, span_boldnotice(I18n.T("Right now, the DNA Infuser can only infuse Tier [] entries.", max_tier_allowed)));
        else
            Dm.append(ref dot, span_boldnotice(I18n.T("Maximum tier unlocked. All DNA entries are possible.")));
        Dm.append(ref dot, span_notice(I18n.T("Examine further for more information.")));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/dna_infuser.dm:56
    public override async DmTask<object> examine_more(mob user = null)
    {
        object dot = await base.examine_more(user);
        Dm.append(ref dot, span_notice(I18n.T("If you infuse a Tier [] entry until it unlocks the bonus, it will upgrade the maximum tier and allow more complicated infusions.", DNA_MUTANT_TIER_ONE)));
        Dm.append(ref dot, span_notice(I18n.T("The maximum level it can reach is Tier [].", DNA_INFUSER_MAX_TIER)));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/dna_infuser.dm:61
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        if (user == occupant)
        {
            await toggle_open(user);
            return null;
        }
        if (Dm.truthy(infusing))
        {
            balloon_alert(user, I18n.T("not while it's on!"));
            return null;
        }
        if (occupant != null && infusing_from != null)
        {
            if (!Dm.truthy(occupant.can_infuse(user)))
            {
                playsound(this, "sound/machines/scanner/scanbuzz.ogg", 35, vary: TRUE);
                return null;
            }
            balloon_alert(user, I18n.T("starting DNA infusion..."));
            await start_infuse();
            return null;
        }
        await toggle_open(user);
        return null;
    }

    // tg: code/game/machinery/dna_infuser/dna_infuser.dm:77
    public virtual async DmTask start_infuse()
    {
        mob_living_carbon_human human_occupant = occupant as mob_living_carbon_human;
        infusing = TRUE;
        visible_message(span_notice(I18n.T("[] hums to life, beginning the infusion process!", this)));

        infusing_into = infusing_from.get_infusion_entry();
        string fail_title = "";
        string fail_explanation = "";
        if (infusing_into is datum_infuser_entry_fly)
        {
            fail_title = I18n.T("Unknown DNA");
            fail_explanation = I18n.T("Unknown DNA. Consult the \"DNA infusion book\".");
        }
        if (Dm.num(infusing_into.tier) > max_tier_allowed)
        {
            infusing_into = GLOB.infuser_entries[typeof(datum_infuser_entry_fly)] as datum_infuser_entry;
            fail_title = I18n.T("Overcomplexity");
            fail_explanation = I18n.T("DNA too complicated to infuse. The machine needs to infuse simpler DNA first.");
        }
        playsound(this, "sound/machines/blender.ogg", 50, vary: TRUE);
        to_chat(human_occupant, span_danger(I18n.T("Little needles repeatedly prick you!")));
        await human_occupant.take_overall_damage(10);
        human_occupant.add_mob_memory(typeof(datum_memory_dna_infusion), Dm.named("protagonist", human_occupant), Dm.named("deuteragonist", infusing_from), Dm.named("mutantlike", infusing_into.infusion_desc));
        Shake(duration: INFUSING_TIME);
        _addtimer(Dm.@new<datum_callback>(human_occupant, nameof(mob.emote), "scream"), INFUSING_TIME - 1 * SECONDS, file: "code/game/machinery/dna_infuser/dna_infuser.dm", line: 97);
        _addtimer(Dm.@new<datum_callback>(this, nameof(end_infuse), fail_explanation, fail_title), INFUSING_TIME, file: "code/game/machinery/dna_infuser/dna_infuser.dm", line: 98);
        update_appearance();
    }

    // tg: code/game/machinery/dna_infuser/dna_infuser.dm:101
    public virtual async DmTask end_infuse(object fail_explanation = null, object fail_title = null)
    {
        mob_living_carbon_human human_occupant = occupant as mob_living_carbon_human;
        if (Dm.truthy(await human_occupant.infuse_organ(infusing_into, infusing_from)))
        {
            check_tier_progression(human_occupant);
            to_chat(occupant, span_danger(I18n.T("You feel yourself becoming more... []?", infusing_into.infusion_desc)));
        }
        infusing = FALSE;
        infusing_into = null;
        QDEL_NULL(ref infusing_from);
        playsound(this, "sound/machines/microwave/microwave-end.ogg", 100, vary: FALSE);
        if (Dm.truthy(fail_explanation))
        {
            playsound(this, "sound/machines/printer.ogg", 100, TRUE);
            visible_message(span_notice(I18n.T("[] prints an error report.", this)));
            obj_item_paper printed_paper = Dm.@new<obj_item_paper>(loc);
            printed_paper.name = I18n.T("error report - '[]'", fail_title);
            printed_paper.add_raw_text(fail_explanation);
            printed_paper.update_appearance();
        }
        await toggle_open();
        update_appearance();
    }

    /// checks to see if the machine should progress a new tier.
    // tg: code/game/machinery/dna_infuser/dna_infuser.dm:122
    public virtual void check_tier_progression(mob_living_carbon_human target = null)
    {
        if (max_tier_allowed != DNA_INFUSER_MAX_TIER
            && Dm.eq(infusing_into.tier, max_tier_allowed)
            && Dm.truthy(target.has_status_effect(infusing_into.status_effect_type)))
        {
            max_tier_allowed++;
            playsound(this, "sound/machines/ding.ogg", 50, TRUE);
            visible_message(span_notice(I18n.T("[] dings as it records the results of the full infusion.", this)));
        }
    }

    // tg: code/game/machinery/dna_infuser/dna_infuser.dm:133
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        //out of order
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER | BROKEN)))
        {
            icon_state = Dm.str(base_icon_state);
            return await base.update_icon_state(__extra);
        }
        //maintenance
        if (Dm.truthy(Dm.bitand(machine_stat, MAINT)) || Dm.truthy(panel_open))
        {
            icon_state = Dm.text("[]_panel", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        //actively running
        if (Dm.truthy(infusing))
        {
            icon_state = Dm.text("[]_on", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        //open or not
        icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(state_open) ? "_open" : null);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/dna_infuser/dna_infuser.dm:151
    public virtual async DmTask toggle_open(mob user = null)
    {
        if (Dm.truthy(panel_open))
        {
            if (user != null)
                balloon_alert(user, I18n.T("close panel first!"));
            return;
        }
        if (Dm.truthy(state_open))
        {
            await close_machine();
            return;
        }
        else if (Dm.truthy(infusing))
        {
            if (user != null)
                balloon_alert(user, I18n.T("not while it's on!"));
            return;
        }
        await open_machine(drop: FALSE);
        //we set drop to false to manually call it with an allowlist
        await dump_inventory_contents(Dm.list(occupant));
    }

    // tg: code/game/machinery/dna_infuser/dna_infuser.dm:166
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        return Dm.truthy(infusing) ? NONE : default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/dna_infuser/dna_infuser.dm:169
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return Dm.truthy(infusing) ? NONE : await default_pry_open(user, tool, deconstruct_on_fail: TRUE);
    }

    // tg: code/game/machinery/dna_infuser/dna_infuser.dm:172
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;
        // if the machine already has a infusion target, or the target is not valid then no adding.
        if (!is_valid_infusion(tool, user))
            return await base.item_interaction(user, tool, modifiers, __extra); //research scanning is so back
        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
        {
            to_chat(user, span_warning(I18n.T("[] is stuck to your hand!", tool)));
            return ITEM_INTERACT_BLOCKING;
        }
        infusing_from = tool;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/dna_infuser/dna_infuser.dm:184
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(user))
        {
            if (message_cooldown <= world.time) // COOLDOWN_FINISHED, inline (rules 11.1 (a))
            {
                message_cooldown = world.time + 4 * SECONDS; // COOLDOWN_START, inline
                to_chat(user, span_warning(I18n.T("[]'s door won't budge!", this)));
            }
            return null;
        }
        if (Dm.truthy(infusing))
        {
            if (message_cooldown <= world.time) // COOLDOWN_FINISHED, inline (rules 11.1 (a))
            {
                message_cooldown = world.time + 4 * SECONDS; // COOLDOWN_START, inline
                to_chat(user, span_danger(I18n.T("[]'s door won't budge while all the needles are infusing you!", this)));
            }
            return null;
        }
        await open_machine(drop: FALSE);
        //we set drop to false to manually call it with an allowlist
        await dump_inventory_contents(Dm.list(occupant));
        return null;
    }

    // mostly good for dead mobs like corpses (drag to add).
    // tg: code/game/machinery/dna_infuser/dna_infuser.dm:201
    public override async DmTask<object> mouse_drop_receive(atom dropped = null, mob user = null, object @params = null)
    {
        // if the machine is closed, already has a infusion target, or the target is not valid then no mouse drop.
        if (!is_valid_infusion(dropped as atom_movable, user))
            return null;
        infusing_from = dropped as atom_movable;
        await infusing_from.forceMove(this);
        return null;
    }

    /// Verify that the given infusion source/mob is a dead creature.
    // tg: code/game/machinery/dna_infuser/dna_infuser.dm:209
    public virtual bool is_valid_infusion(atom_movable target = null, mob user = null)
    {
        if (infusing_from != null)
        {
            balloon_alert(user, I18n.T("empty the machine first!"));
            return false;
        }
        if (isliving(target))
        {
            mob_living living_target = target as mob_living;
            if (!Dm.eq(living_target.stat, DEAD))
            {
                balloon_alert(user, I18n.T("only dead creatures!"));
                return false;
            }
        }
        else if (!HAS_TRAIT(target, TRAIT_VALID_DNA_INFUSION))
        {
            balloon_alert(user, I18n.T("only creatures!"));
            return false;
        }
        return true;
    }

    // tg: code/game/machinery/dna_infuser/dna_infuser.dm:222
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(infusing))
        {
            balloon_alert(user, I18n.T("not while it's on!"));
            return CLICK_ACTION_BLOCKING;
        }
        if (infusing_from == null)
        {
            balloon_alert(user, I18n.T("no sample to eject!"));
            return CLICK_ACTION_BLOCKING;
        }
        balloon_alert(user, I18n.T("ejected sample"));
        await infusing_from.forceMove(get_turf(this));
        infusing_from = null;
        return CLICK_ACTION_SUCCESS;
    }
}
