// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/ai_core/_core.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the /atom root of transfer_ai with the APC; the AI core, the networked latejoin core and the AI core board silicons
// SI4, D326). The screen's emissive glow is DV12's. The var blocks are generated (D111).

// tg: code/game/machinery/ai_core/_core.dm:3
public partial class obj_structure_ai_core : obj_structure
{
    // #define AI_CORE_BRAIN(X) X.braintype == "Android" ? "brain" : "MMI"
    internal static string AI_CORE_BRAIN(obj_item_brain_processor X) => Dm.eq(X.braintype, "Android") ? "brain" : "MMI";

    // tg: code/game/machinery/ai_core/_core.dm:19
    [DmParams("mapload", "state", "core_mmi")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object state = __extra.Length > 0 ? __extra[0] : this.state;
        obj_item_brain_processor core_mmi = Dm.arg<obj_item_brain_processor>(__extra, 1);
        object dot = base.Initialize(mapload, __extra);
        if (core_mmi != null && Dm.num(state) < CORE_STATE_CABLED)
        {
            stack_trace("supplied a core_mmi as constructor argument, but core state wouldn't have accepted it!", "code/game/machinery/ai_core/_core.dm", 22);
            state = CORE_STATE_FINISHED; // just in case...
        }
        this.state = Dm.num(state);
        if (Dm.num(state) >= CORE_STATE_CIRCUIT)
            circuit = Dm.@new<obj_item_circuitboard_aicore>(this);
        if (Dm.num(state) >= CORE_STATE_CABLED)
        {
            core_mmi ??= Dm.@new<obj_item_brain_processor_organic>(this, Dm.@new<obj_item_organ_brain>());
            Waiting.NoSleep(core_mmi.forceMove(this));
            this.core_mmi = core_mmi;
            Waiting.NoSleep(set_anchored(TRUE));
        }

        update_appearance(UPDATE_ICON_STATE);
        return dot;
    }

    // tg: code/game/machinery/ai_core/_core.dm:35
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        cut_overlays();

        if (state != CORE_STATE_FINISHED)
        {
            icon_state = Dm.text("[][]", base_icon_state, state);
            if (state == CORE_STATE_CABLED && core_mmi != null)
                icon_state += "b";
        }
        else
        {
            icon_state = "ai-core";

            mutable_appearance screen = mutable_appearance(icon, "ai-empty") as mutable_appearance;
            screen.layer = FLOAT_LAYER;
            screen.appearance_flags = Dm.bitor(RESET_COLOR, KEEP_APART);

            add_overlay(screen);

            // DEVIATION(DV12): add_overlay(emissive_appearance(icon, "ai-empty", src, alpha = 255))

            await set_light(0.2f, 0.2f, LIGHT_COLOR_FAINT_CYAN);
        }

        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/ai_core/_core.dm:57
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == circuit)
        {
            circuit = null;
            if (state != CORE_STATE_GLASSED && state != CORE_STATE_FINISHED)
            {
                state = CORE_STATE_EMPTY;
                update_appearance();
            }
        }
        if (gone == core_mmi)
        {
            core_mmi = null;
            update_appearance();
        }
        return dot;
    }

    // tg: code/game/machinery/ai_core/_core.dm:68
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        if (state >= CORE_STATE_GLASSED)
            Dm.@new<obj_item_stack_sheet_rglass>(drop_location(), 2);
        if (state >= CORE_STATE_CABLED)
            Dm.@new<obj_item_stack_cable_coil>(drop_location(), 5);
        if (core_mmi != null)
            await core_mmi.forceMove(drop_location() as atom);
        if (circuit != null)
            await circuit.forceMove(drop_location() as atom);
        Dm.@new<obj_item_stack_sheet_plasteel>(drop_location(), 4);
    }

    // tg: code/game/machinery/ai_core/_core.dm:76
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref circuit);
        QDEL_NULL(ref core_mmi);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/ai_core/_core.dm:81
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(I18n.T("It has some <b>bolts</b> that look [].", Dm.truthy(anchored) ? "tightened" : "loosened")));
        Dm.append(ref dot, span_notice(I18n.T("Save this core to a multitool's buffer and upload it to a module rack to automatically link it on first boot. Otherwise, it will link[] to the first rack found.", Dm.truthy(is_station_level(z)) ? " to the station's core rack or" : "")));

        switch ((int)state)
        {
            case CORE_STATE_EMPTY:
                Dm.append(ref dot, span_notice(I18n.T("There is a <b>slot</b> for a circuit board, the frame can be <b>melted</b> down.")));
                break;
            case CORE_STATE_CIRCUIT:
                Dm.append(ref dot, span_notice(I18n.T("The circuit board can be <b>screwed</b> into place or <b>pried</b> out.")));
                break;
            case CORE_STATE_SCREWED:
                Dm.append(ref dot, span_notice(I18n.T("The frame can be <b>wired</b>, the circuit board can be <b>unfastened</b>.")));
                break;
            case CORE_STATE_CABLED:
                if (core_mmi != null)
                    Dm.append(ref dot, span_notice(I18n.T("There is a <b>slot</b> for a reinforced glass panel, the [] could be <b>pried</b> out.", AI_CORE_BRAIN(core_mmi))));
                else
                    Dm.append(ref dot, span_notice(I18n.T("There are wires which could be hooked up to an <b>MMI or positronic brain</b>, or <b>cut</b>.")));
                break;
            case CORE_STATE_GLASSED:
                Dm.append(ref dot, span_notice(I18n.T("The monitor []can be <b>screwed</b> in, the panel can be <b>pried</b> out.", (core_mmi?.brainmob?.mind != null && !suicide_check()) ? "and neural interface " : "")));
                break;
            case CORE_STATE_FINISHED:
                Dm.append(ref dot, span_notice(I18n.T("The monitor's connection can be <b>cut</b>[]", (core_mmi?.brainmob?.mind != null && !suicide_check()) ? " the neural interface can be <b>screwed</b> in." : ".")));
                break;
        }
        return dot;
    }

    // tg: code/game/machinery/ai_core/_core.dm:104
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (state < CORE_STATE_FINISHED)
            return await construction_item_interaction(user, tool, modifiers);

        return NONE;
    }

    /// Exists to be used for callbacks.
    // tg: code/game/machinery/ai_core/_core.dm:111
    public virtual bool check_state(float state_to_check = 0)
    {
        return state == state_to_check;
    }

    // tg: code/game/machinery/ai_core/_core.dm:172
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        obj_item_multitool multitool = tool as obj_item_multitool;
        tool.play_tool_sound(this, 50);
        multitool.set_buffer(this);
        balloon_alert(user, "core saved to buffer");
        await DmTask.CompletedTask;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/ai_core/_core.dm:178
    public virtual async DmTask<mob_living_silicon_ai> ai_structure_to_mob()
    {
        mob_living_brain the_brainmob = core_mmi.brainmob;
        if (the_brainmob.mind == null || suicide_check())
            return null;
        await the_brainmob.mind.remove_antags_for_borging();
        // SSblackbox.record_feedback("amount", "ais_created", 1): the round statistics (SSblackbox is an inert skeleton, M10)

        mob_living_silicon_ai ai_mob = Dm.@new<mob_living_silicon_ai>(loc, the_brainmob, core_mmi.laws, default_link_ref?.resolve());

        ai_mob.posibrain_inside = Dm.eq(core_mmi.braintype, "Android") ? TRUE : FALSE;
        deadchat_broadcast(Dm.text(" has been brought online at <b>[]</b>.", get_area_name(ai_mob, format_text: TRUE)), span_name(Dm.text("[]", ai_mob)), follow_target: ai_mob, message_type: DEADCHAT_ANNOUNCEMENT);
        qdel(this);
        return ai_mob;
    }

    /// Quick proc to call to see if the brainmob inside of us has suicided. Returns TRUE if we have, FALSE in any other scenario.
    // tg: code/game/machinery/ai_core/_core.dm:194
    public virtual bool suicide_check()
    {
        if (core_mmi == null || core_mmi.brainmob == null)
            return false;
        return HAS_TRAIT(core_mmi.brainmob, TRAIT_SUICIDED);
    }

    // tg: code/game/machinery/ai_core/_core.dm:210
    public override async DmTask<object> transfer_ai(object interaction = null, mob user = null, mob_living_silicon_ai AI = null, obj_item_aicard card = null)
    {
        if (state != CORE_STATE_FINISHED || !Dm.truthy(await base.transfer_ai(interaction, user, AI, card)))
            return null;
        if (core_mmi != null && core_mmi.brainmob != null)
        {
            if (core_mmi.brainmob.mind != null)
            {
                to_chat(user, span_warning(I18n.T("[] already contains an active mind!", this)));
                return null;
            }
            else if (suicide_check())
            {
                to_chat(user, span_warning(I18n.T("[] installed in [] is completely useless!", AI_CORE_BRAIN(core_mmi), this)));
                return null;
            }
        }
        //Transferring a carded AI to a core.
        if (Dm.eq(interaction, AI_TRANS_FROM_CARD))
        {
            AI.set_control_disabled(FALSE);
            AI.radio_enabled = TRUE;
            await AI.forceMove(loc); // to replace the terminal.
            to_chat(AI, span_notice(I18n.T("You have been uploaded to a stationary terminal. Remote device connection restored.")));
            to_chat(user, Dm.text("[]: ", span_boldnotice(I18n.T("Transfer successful"))) + I18n.T("[] ([].exe) installed and executed successfully. Local copy has been removed.", AI.name, rand(1000, 9999)));
            card.AI = null;
            AI.battery = circuit.battery;
            AI.posibrain_inside = (core_mmi == null || Dm.eq(core_mmi.braintype, "Android")) ? TRUE : FALSE;
            qdel(this);
        }
        else //If for some reason you use an empty card on an empty AI terminal.
            to_chat(user, span_alert(I18n.T("There is no AI loaded on this terminal.")));
        return null;
    }
}

// tg: code/game/machinery/ai_core/_core.dm:117
public partial class obj_structure_ai_core_latejoin_inactive : obj_structure_ai_core
{
    // tg: code/game/machinery/ai_core/_core.dm:125
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref GLOB.latejoin_ai_cores, this);
        return dot;
    }

    // tg: code/game/machinery/ai_core/_core.dm:129
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.latejoin_ai_cores, this);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/ai_core/_core.dm:133
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_info(I18n.T("Its transmitter seems to be <b>[]</b>.", Dm.truthy(active) ? "on" : "off")));
        Dm.append(ref dot, span_notice(I18n.T("You could [] it by [] with a multitool.", Dm.truthy(active) ? "deactivate" : "activate", EXAMINE_HINT("right clicking"))));
        return dot;
    }

    // tg: code/game/machinery/ai_core/_core.dm:138
    public virtual bool is_available() //If people still manage to use this feature to spawn-kill AI latejoins ahelp them.
    {
        if (!Dm.truthy(available))
            return false;
        if (!Dm.truthy(safety_checks))
            return true;
        if (!Dm.truthy(active))
            return false;
        turf ai_turf = get_turf(this);
        area ai_area = get_area(this) as area;
        if (!Dm.truthy(Dm.bitand(ai_area.area_flags, BLOBS_ALLOWED)))
            return false;
        if (!Dm.truthy(ai_area.power_equip))
            return false;
        if (!Dm.truthy(SSmapping.level_trait(ai_turf.z, ZTRAIT_STATION)))
            return false;
        if (!isfloorturf(ai_turf))
        {
            if (Dm.locate<obj_structure_transport_linear>(ai_turf.contents) != null) // carveout for maps that have the AI core on a lift platform.
                return true;
            return false;
        }
        return true;
    }

    // tg: code/game/machinery/ai_core/_core.dm:158
    public override async DmTask<object> multitool_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(await tool.use_tool(this, user, 0 * SECONDS, 0, 50)))
            return ITEM_INTERACT_BLOCKING;

        active = Dm.truthy(active) ? FALSE : TRUE;
        balloon_alert(user, Dm.text("[] transmitters", Dm.truthy(active) ? "activated" : "deactivated"));
        return ITEM_INTERACT_SUCCESS;
    }
}

/*
This is a good place for AI-related object verbs so I'm sticking it here.
If adding stuff to this, don't forget that an AI need to cancel_camera() whenever it physically moves to a different location.
That prevents a few funky behaviors.
*/
//The type of interaction, the player performing the operation, the AI itself, and the card object, if any.

// tg: code/game/machinery/ai_core/_core.dm:202
public partial class atom : datum
{
    // tg: code/game/machinery/ai_core/_core.dm:202
    public virtual async DmTask<object> transfer_ai(object interaction = null, mob user = null, mob_living_silicon_ai AI = null, obj_item_aicard card = null)
    {
        await DmTask.CompletedTask;
        if (card is obj_item_aicard && Dm.truthy(card.flush))
        {
            to_chat(user, span_alert(I18n.T("ERROR: AI flush is in progress, cannot execute transfer protocol.")));
            return FALSE;
        }
        return TRUE;
    }
}

// tg: code/game/machinery/ai_core/_core.dm:233
public partial class obj_item_circuitboard_aicore : obj_item_circuitboard
{
    // tg: code/game/machinery/ai_core/_core.dm:238
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload) && HAS_TRAIT(SSstation, STATION_TRAIT_HUMAN_AI))
            return INITIALIZE_HINT_QDEL;
        return dot;
    }
}
