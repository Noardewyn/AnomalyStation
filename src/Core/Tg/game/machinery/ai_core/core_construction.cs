// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/ai_core/core_construction.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI4, D326): building and taking apart an AI core.

// tg: code/game/machinery/ai_core/core_construction.dm:5
public partial class obj_structure_ai_core : obj_structure
{
    // #define UPDATE_STATE(new_state) state = new_state; update_appearance(UPDATE_ICON_STATE)
    private void UPDATE_STATE(float new_state)
    {
        state = new_state;
        update_appearance(UPDATE_ICON_STATE);
    }

    // #define CHECK_STATE_CALLBACK(maintained_state) CALLBACK(src, PROC_REF(check_state), maintained_state)
    private datum_callback CHECK_STATE_CALLBACK(float maintained_state) => Dm.@new<datum_callback>(this, nameof(check_state), maintained_state);

    // tg: code/game/machinery/ai_core/core_construction.dm:5
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (state != CORE_STATE_EMPTY)
        {
            balloon_alert(user, "frame has to be empty!");
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(tool.tool_start_check(user, 1)))
            return ITEM_INTERACT_BLOCKING;

        if (!Dm.truthy(await tool.use_tool(this, user, 2 * SECONDS, 1, 50, CHECK_STATE_CALLBACK(CORE_STATE_EMPTY))))
            return ITEM_INTERACT_BLOCKING;

        await deconstruct(TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/ai_core/core_construction.dm:18
    public override object can_be_unfasten_wrench(mob user = null, object silent = null)
    {
        if (state >= CORE_STATE_FINISHED)
        {
            if (!Dm.truthy(silent))
                balloon_alert(user, "can't be unanchored while operational!");
            return FAILED_UNFASTEN;
        }

        return base.can_be_unfasten_wrench(user, silent);
    }

    // tg: code/game/machinery/ai_core/core_construction.dm:26
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object result = await default_unfasten_wrench(user, tool);
        if (Dm.eq(result, FAILED_UNFASTEN))
            return ITEM_INTERACT_BLOCKING;
        if (Dm.eq(result, SUCCESSFUL_UNFASTEN))
            return ITEM_INTERACT_SUCCESS;

        return NONE;
    }

    // tg: code/game/machinery/ai_core/core_construction.dm:35
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        switch ((int)state)
        {
            case CORE_STATE_EMPTY:
                balloon_alert(user, "nothing to screw in there!");
                return ITEM_INTERACT_BLOCKING;
            case CORE_STATE_CIRCUIT:
                if (!Dm.truthy(await tool.use_tool(this, user, 0 * SECONDS, 0, 50, CHECK_STATE_CALLBACK(CORE_STATE_CIRCUIT))))
                    return ITEM_INTERACT_BLOCKING;
                balloon_alert(user, "board secured");
                UPDATE_STATE(CORE_STATE_SCREWED);
                return ITEM_INTERACT_SUCCESS;
            case CORE_STATE_SCREWED:
                if (!Dm.truthy(await tool.use_tool(this, user, 0 * SECONDS, 0, 50, CHECK_STATE_CALLBACK(CORE_STATE_SCREWED))))
                    return ITEM_INTERACT_BLOCKING;
                balloon_alert(user, "board unsecured");
                UPDATE_STATE(CORE_STATE_CIRCUIT);
                return ITEM_INTERACT_SUCCESS;
            case CORE_STATE_CABLED:
                balloon_alert(user, "can't reach the board!");
                return ITEM_INTERACT_BLOCKING;
            case CORE_STATE_GLASSED:
            {
                if (!Dm.truthy(anchored))
                {
                    balloon_alert(user, "isn't anchored!");
                    return ITEM_INTERACT_BLOCKING;
                }
                if (!Dm.truthy(await tool.use_tool(this, user, 0 * SECONDS, 0, 50, CHECK_STATE_CALLBACK(CORE_STATE_GLASSED))))
                    return ITEM_INTERACT_BLOCKING;
                if (suicide_check())
                {
                    balloon_alert(user, "processor is completely useless!");
                    return ITEM_INTERACT_BLOCKING;
                }

                atom_movable alert_source = this;
                if (core_mmi.brainmob?.mind != null)
                    alert_source = (atom_movable)await ai_structure_to_mob() ?? alert_source;
                else
                    UPDATE_STATE(CORE_STATE_FINISHED);
                alert_source.balloon_alert(user, Dm.text("connected monitor[]", core_mmi?.brainmob?.mind != null ? " and neural network" : ""));
                return ITEM_INTERACT_SUCCESS;
            }
            case CORE_STATE_FINISHED:
            {
                if (core_mmi?.brainmob?.mind == null || suicide_check())
                {
                    balloon_alert(user, "processor is inactive!");
                    return ITEM_INTERACT_BLOCKING;
                }

                if (!Dm.truthy(anchored))
                {
                    balloon_alert(user, "anchor it first!");
                    return ITEM_INTERACT_BLOCKING;
                }

                balloon_alert(user, "connecting neural network...");
                if (!Dm.truthy(await tool.use_tool(this, user, 10 * SECONDS, 0, 50, CHECK_STATE_CALLBACK(CORE_STATE_FINISHED))))
                    return ITEM_INTERACT_BLOCKING;

                atom_movable alert_source = await ai_structure_to_mob();
                if (alert_source == null)
                {
                    balloon_alert(user, "processor is inactive!");
                    return ITEM_INTERACT_BLOCKING;
                }

                alert_source.balloon_alert(user, "connected neural network");
                return ITEM_INTERACT_SUCCESS;
            }
        }
        return null;
    }

    // tg: code/game/machinery/ai_core/core_construction.dm:98
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        switch ((int)state)
        {
            case CORE_STATE_EMPTY:
                balloon_alert(user, "nothing to pry out!");
                return ITEM_INTERACT_BLOCKING;
            case CORE_STATE_CIRCUIT:
                if (!Dm.truthy(await tool.use_tool(this, user, 0 * SECONDS, 0, 50, CHECK_STATE_CALLBACK(CORE_STATE_CIRCUIT))))
                    return ITEM_INTERACT_BLOCKING;

                await circuit.forceMove(drop_location() as atom);
                UPDATE_STATE(CORE_STATE_EMPTY);
                return ITEM_INTERACT_SUCCESS;
            case CORE_STATE_SCREWED:
                balloon_alert(user, "won't budge!");
                return ITEM_INTERACT_BLOCKING;
            case CORE_STATE_CABLED:
                if (core_mmi == null)
                {
                    balloon_alert(user, "nothing to pry out!");
                    return ITEM_INTERACT_BLOCKING;
                }
                if (!Dm.truthy(await tool.use_tool(this, user, 0 * SECONDS, 0, 50, CHECK_STATE_CALLBACK(CORE_STATE_CABLED))) || core_mmi == null)
                    return ITEM_INTERACT_BLOCKING;

                await core_mmi.forceMove(drop_location() as atom);
                UPDATE_STATE(CORE_STATE_CABLED);
                return ITEM_INTERACT_SUCCESS;
            case CORE_STATE_GLASSED:
                if (!Dm.truthy(await tool.use_tool(this, user, 0 * SECONDS, 0, 50, CHECK_STATE_CALLBACK(CORE_STATE_GLASSED))))
                    return ITEM_INTERACT_BLOCKING;

                Dm.@new<obj_item_stack_sheet_rglass>(drop_location(), 2);
                UPDATE_STATE(CORE_STATE_CABLED);
                return ITEM_INTERACT_SUCCESS;
            case CORE_STATE_FINISHED:
                balloon_alert(user, "display is on!");
                return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }

    // tg: code/game/machinery/ai_core/core_construction.dm:137
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        if (state >= CORE_STATE_EMPTY && state <= CORE_STATE_CIRCUIT) // if(CORE_STATE_EMPTY to CORE_STATE_CIRCUIT)
        {
            balloon_alert(user, "nothing to cut!");
            return ITEM_INTERACT_BLOCKING;
        }
        switch ((int)state)
        {
            case CORE_STATE_CABLED:
                if (core_mmi != null)
                {
                    balloon_alert(user, Dm.text("[] in the way!", AI_CORE_BRAIN(core_mmi)));
                    return ITEM_INTERACT_BLOCKING;
                }

                if (!Dm.truthy(await tool.use_tool(this, user, 0 * SECONDS, 0, 50, CHECK_STATE_CALLBACK(CORE_STATE_CABLED))) || core_mmi != null)
                    return ITEM_INTERACT_BLOCKING;

                Dm.@new<obj_item_stack_cable_coil>(drop_location(), 5);
                UPDATE_STATE(CORE_STATE_SCREWED);
                return ITEM_INTERACT_SUCCESS;
            case CORE_STATE_GLASSED:
                balloon_alert(user, "nothing left to cut!");
                return ITEM_INTERACT_BLOCKING;
            case CORE_STATE_FINISHED:
                if (!Dm.truthy(await tool.use_tool(this, user, 0 * SECONDS, 0, 50, CHECK_STATE_CALLBACK(CORE_STATE_FINISHED))))
                    return ITEM_INTERACT_BLOCKING;

                UPDATE_STATE(CORE_STATE_GLASSED);
                return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }

    /// Handles the interaction chain the same as item_interaction. Exists to isolate construction behaviour from other item behaviour.
    // tg: code/game/machinery/ai_core/core_construction.dm:166
    public virtual async DmTask<object> construction_item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (tool is obj_item_circuitboard_aicore board)
            return Dm.truthy(await install_board(user, board)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;

        if (tool is obj_item_stack_cable_coil cable)
            return Dm.truthy(await add_cabling(user, cable)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;

        if (tool is obj_item_brain_processor mmi)
            return Dm.truthy(await install_mmi(user, mmi)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;
        if (tool is obj_item_stack_sheet_rglass glass)
            return Dm.truthy(await install_glass(user, glass)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;

        return NONE;
    }

    // tg: code/game/machinery/ai_core/core_construction.dm:180
    public virtual async DmTask<object> install_board(mob_living user = null, obj_item_circuitboard_aicore circuit = null)
    {
        if (state != CORE_STATE_EMPTY)
            return FALSE;
        if (!Dm.truthy(await user.transferItemToLoc(circuit, this)))
            return FALSE;

        playsound(this, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
        this.circuit = circuit;
        UPDATE_STATE(CORE_STATE_CIRCUIT);
        return TRUE;
    }

    // tg: code/game/machinery/ai_core/core_construction.dm:191
    public virtual async DmTask<object> add_cabling(mob_living user = null, obj_item_stack_cable_coil cable = null)
    {
        if (state != CORE_STATE_SCREWED)
            return FALSE;

        if (Dm.num(cable.get_amount()) < 5)
        {
            balloon_alert(user, Dm.text("not enough []!", Dm.initial(typeof(obj_item_stack_cable_coil), (atom a) => a.name)));
            return FALSE;
        }

        balloon_alert(user, "adding cable...");
        if (!Dm.truthy(await cable.use_tool(this, user, 2 * SECONDS, 5, 50, CHECK_STATE_CALLBACK(CORE_STATE_SCREWED))))
            return FALSE;

        UPDATE_STATE(CORE_STATE_CABLED);
        return TRUE;
    }

    // tg: code/game/machinery/ai_core/core_construction.dm:206
    public virtual async DmTask<object> install_mmi(mob_living user = null, obj_item_brain_processor mmi = null)
    {
        if (state != CORE_STATE_CABLED)
            return FALSE;

        if (!Dm.truthy(mmi.brain_check(user)))
        {
            bool wants_install = Dm.eq(await tgui_alert(user, Dm.text("This [] is inactive, would you like to make an inactive AI?", AI_CORE_BRAIN(mmi)), Dm.text("Installing AI []", AI_CORE_BRAIN(mmi)), Dm.list("Yes", "No")), "Yes");
            if (!wants_install)
                return FALSE;
            if (QDELETED(this) || QDELETED(user) || QDELETED(mmi) || !Dm.truthy(user.is_holding(mmi)) || !Dm.truthy(await Adjacent(user)))
                return FALSE;
            if (mmi.brainmob != null && HAS_TRAIT(mmi.brainmob, TRAIT_SUICIDED))
            {
                balloon_alert(user, Dm.text("[] is useless!", AI_CORE_BRAIN(mmi)));
                return FALSE;
            }
        }
        else
        {
            mob_living_brain mmi_brainmob = mmi.brainmob;
            if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_allow_ai))) || (mmi_brainmob != null && Dm.truthy(is_banned_from(mmi_brainmob.ckey, JOB_AI)))) /* CONFIG_GET(flag/allow_ai) */
            {
                if (!QDELETED(this) && !QDELETED(user) && !QDELETED(mmi) && Dm.truthy(user.is_holding(mmi)) && Dm.truthy(await Adjacent(user)))
                    balloon_alert(user, Dm.text("[] won't fit!", mmi));
                return FALSE;
            }
        }

        if (state != CORE_STATE_CABLED)
            return FALSE;
        if (!Dm.truthy(await user.transferItemToLoc(mmi, this)))
            return FALSE;

        core_mmi = mmi;
        UPDATE_STATE(CORE_STATE_CABLED);
        return TRUE;
    }

    // tg: code/game/machinery/ai_core/core_construction.dm:236
    public virtual async DmTask<object> install_glass(mob_living user = null, obj_item_stack_sheet_rglass glass = null)
    {
        if (state != CORE_STATE_CABLED)
            return FALSE;

        if (core_mmi == null)
        {
            balloon_alert(user, "needs a processor!");
            return FALSE;
        }
        if (Dm.num(glass.get_amount()) < 2)
        {
            balloon_alert(user, Dm.text("not enough []!", Dm.initial(typeof(obj_item_stack_sheet_rglass), (atom a) => a.name)));
            return FALSE;
        }

        if (!Dm.truthy(await glass.use_tool(this, user, 2 * SECONDS, 2, 50, CHECK_STATE_CALLBACK(CORE_STATE_CABLED))) || core_mmi == null)
            return FALSE;

        UPDATE_STATE(CORE_STATE_GLASSED);
        return TRUE;
    }
}
