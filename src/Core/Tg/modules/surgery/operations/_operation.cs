// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/_operation.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. M8 slice D had only the manual cauterization of one's own limb. Starting an operation
// (perform_surgery: the available operations of the tool on the patient's limb, tg's radial of them), GLOB.operations with its
// unlocked and locked operations, the operation datum (availability, the tool's quality, the speed and failure chance from the
// table, the drunkenness and the patient's state, the do-after loop with its blood on the surgeon's gloves, the success and failure
// with their sounds, messages, pain and mood) and the basic, limb and organ bases. The debug verb's output and the speed mods are
// translated as tg has them. Slots retyped (Appendix A): perform_surgery's and get_available_operations' potential_tool and
// get_tool_quality's tool are object (tg passes an item or IMPLEMENT_HAND).

public partial class mob_living : mob
{
    /**
     * Attempts to perform a surgery with whatever tool is passed
     *
     * * src - the surgeon
     * * operating_on - the main thing being operated on, usually a mob, but can be a detached limb
     * * potential_tool - the tool being used for the operation (can be null / IMPLEMENT_HAND)
     * * intentionally_fail - if TRUE, forces the operation to fail (for testing purposes)
     *
     * Returns an ITEM_INTERACT_* flag
     */
    // tg: code/modules/surgery/operations/_operation.dm:11
    public virtual async DmTask<object> perform_surgery(atom_movable operating_on = null, object potential_tool = null, float? intentionally_fail = null, object operating_zone = null)
    {
        potential_tool ??= IMPLEMENT_HAND;
        intentionally_fail ??= FALSE;
        operating_zone ??= zone_selected;
        if (Dm.truthy(DOING_INTERACTION(this, HAS_TRAIT(this, TRAIT_HIPPOCRATIC_OATH) ? operating_on : DOAFTER_SOURCE_SURGERY)))
        {
            operating_on.balloon_alert(this, I18n.T("already performing surgery!"));
            return ITEM_INTERACT_BLOCKING;
        }

        // allow cyborgs to use "hands"
        if (istype(potential_tool, typeof(obj_item_borg_cyborghug)))
            potential_tool = IMPLEMENT_HAND;

        DmList operations = (DmList)await get_available_operations(operating_on, potential_tool, operating_zone);

        // we failed to undertake any operations?
        if (!Dm.truthy(length(operations)))
        {
            if (!isitem(potential_tool))
                return NONE;
            obj_item realtool = (obj_item)potential_tool;
            // try self-cauterization if applicable
            if (this == operating_on)
            {
                object manual_cauterization = await try_manual_cauterize(realtool);
                if (Dm.truthy(Dm.bitand(manual_cauterization, ITEM_INTERACT_ANY_BLOCKER)))
                    return manual_cauterization;
            }
            // for surgical tools specifically, we have some special handling
            if (!Dm.truthy(Dm.bitand(realtool.item_flags, SURGICAL_TOOL)))
                return NONE;
            // if the targeted limb isn't prepped for surgery, i suppose we can allow an attack
            obj_item_bodypart operating__1 = null;
            if (isbodypart(operating_on))
                operating__1 = (obj_item_bodypart)operating_on;
            else if (isliving(operating_on))
            {
                mob_living patient = (mob_living)operating_on;
                operating__1 = patient.get_bodypart((string)operating_zone) as obj_item_bodypart;
            }
            if (operating__1 != null && !HAS_TRAIT(operating__1, TRAIT_READY_TO_OPERATE))
                return NONE;
            // at this point we can be relatively sure they messed up so let's give a feedback message...
            if (isliving(operating_on))
            {
                mob_living patient = (mob_living)operating_on;
                if (!Dm.truthy(patient.is_location_accessible(operating_zone, IGNORED_OPERATION_CLOTHING_SLOTS)))
                    patient.balloon_alert(this, I18n.T("operation site is obstructed!"));
                else if (!IS_LYING_OR_CANNOT_LIE(patient))
                    patient.balloon_alert(this, I18n.T("not lying down!"));
                else
                    patient.balloon_alert(this, I18n.T("nothing to do with []!", realtool.name));
            }
            else
                operating__1.balloon_alert(this, I18n.T("nothing to do with []!", realtool.name));
            //  ...then, block attacking. prevents the surgeon from viciously stabbing the patient on a mistake
            return ITEM_INTERACT_BLOCKING;
        }

        DmList radial_operations = Dm.list();
        foreach (object radial_slice in Dm.each(operations))
            radial_operations[radial_slice] = radial_slice; // weird but makes it easier to index later

        sortTim(radial_operations, "/proc/" + nameof(GlobalProcs.cmp_name_asc));

        object picked = await show_radial_menu(
            user: this,
            anchor: operating_on,
            choices: radial_operations,
            require_near: TRUE,
            autopick_single_option: TRUE,
            radius: 56,
            custom_check: Dm.@new<datum_callback>(this, nameof(surgery_check), potential_tool, operating_on));
        if (picked == null)
            return ITEM_INTERACT_BLOCKING;

        DmList picked_entry = (DmList)operations[picked];
        datum_surgery_operation picked_op = (datum_surgery_operation)picked_entry[1];
        atom_movable operating = (atom_movable)picked_entry[2];
        DmList op_info = (DmList)picked_entry[3];
        op_info[OPERATION_TARGET_ZONE] = operating_zone;
        op_info[OPERATION_FORCE_FAIL] = intentionally_fail;

        return await picked_op.try_perform(operating, this, potential_tool, op_info);
    }

    /**
     * Returns a list of all surgery operations the mob can currently perform on the target with the potential tool
     *
     * * src - the surgeon
     * * operating_on - the mob being operated on, or possibly a detached limb
     * * potential_tool - the tool being used for the operation (can be null / IMPLEMENT_HAND)
     * * operating_zone - the body zone being operated on
     *
     * Returns a list where the keys are radial menu slices and the values are lists of:
     * * [0] - the operation datum
     * * [1] - the atom being operated on
     * * [2] - a list of option-specific info
     */
    // tg: code/modules/surgery/operations/_operation.dm:93
    public virtual async DmTask<object> get_available_operations(atom_movable operating_on = null, object potential_tool = null, object operating_zone = null)
    {
        potential_tool ??= IMPLEMENT_HAND;
        operating_zone ??= zone_selected;
        // List of typepaths of operations we *can* do
        DmList possible_operations = GLOB.operations.unlocked.Copy();
        // Signals can add operation types to the list to unlock special ones
        // SEND_SIGNAL(src, COMSIG_LIVING_OPERATING_ON, operating_on, possible_operations) (rules 11.1 (e))
        if (Dm.truthy(this._listen_lookup?[COMSIG_LIVING_OPERATING_ON]))
            this._SendSignal(COMSIG_LIVING_OPERATING_ON, Dm.list(this, operating_on, possible_operations));
        // SEND_SIGNAL(operating_on, COMSIG_ATOM_BEING_OPERATED_ON, src, possible_operations)
        if (Dm.truthy(operating_on._listen_lookup?[COMSIG_ATOM_BEING_OPERATED_ON]))
            operating_on._SendSignal(COMSIG_ATOM_BEING_OPERATED_ON, Dm.list(operating_on, this, possible_operations));

        DmList operations = Dm.list();
        foreach (datum_surgery_operation operation in Dm.each_cast<datum_surgery_operation>(GLOB.operations.get_instances_from(possible_operations)))
        {
            atom_movable operate_on = operation.get_operation_target(operating_on, operating_zone) as atom_movable;
            if (!Dm.truthy(operation.check_availability(isliving(operating_on) ? (mob_living)operating_on : null, operate_on, this, potential_tool, operating_zone)))
                continue;
            object potential_options = await operation.get_radial_options(operate_on, potential_tool as obj_item, operating_zone);
            if (!islist(potential_options))
                potential_options = Dm.list(potential_options);
            foreach (datum_radial_menu_choice radial_slice in Dm.each_cast<datum_radial_menu_choice>(potential_options))
            {
                if (Dm.truthy(operations[radial_slice]))
                {
                    stack_trace(Dm.text("Duplicate radial surgery option '[]' detected for operation '[]'.", radial_slice.name, operation.type), "code/modules/surgery/operations/_operation.dm", 108);
                    continue;
                }
                object option_specific_info = ((DmList)potential_options)[radial_slice] is var or__1 && Dm.truthy(or__1) ? or__1 : Dm.list(Dm.kv(Dm.text("[]", OPERATION_ACTION), "default"));
                operations[radial_slice] = Dm.list(operation, operate_on, option_specific_info);
            }
        }

        return operations;
    }

    /// Callback for checking if the surgery radial can be kept open
    // tg: code/modules/surgery/operations/_operation.dm:117
    public virtual object surgery_check(object tool = null, atom_movable operating_on = null)
    {
        obj_item holding = get_active_held_item() as obj_item;

        if (Dm.eq(tool, IMPLEMENT_HAND))
            return Dm.num(holding == null || istype(holding, typeof(obj_item_borg_cyborghug))); // still holding nothing (or "hands")

        if (QDELETED(holding))
            return FALSE; // i dunno, a stack item? not our problem

        return Dm.num(Dm.eq(tool, holding.get_proxy_attacker_for(operating_on, this))); // tool (or its proxy) is still being held
    }

    /// src attempts to cauterize themselves to reset their surgery state. Basically a manual form of the real "close skin" operation
    // tg: code/modules/surgery/operations/_operation.dm:129
    public virtual async DmTask<object> try_manual_cauterize(obj_item tool = null)
    {
        object cauterize_zone = deprecise_zone(zone_selected);
        obj_item_bodypart limb = get_bodypart((string)cauterize_zone) as obj_item_bodypart;
        if (!Dm.truthy(manual_cauterize_check(tool, limb)))
            return NONE;
        if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(this, this)))
            return ITEM_INTERACT_BLOCKING;

        visible_message(
            span_notice(I18n.T("[] attempts to close [] own [] with []...", this, p_their(), limb.plaintext_zone, tool)),
            span_notice(I18n.T("You attempt to close your own [] with []...", limb.plaintext_zone, tool)),
            span_hear(I18n.T("You hear [] sounds.", Dm.truthy(tool?.get_temperature()) ? I18n.T("singeing") : I18n.T("stitching"))),
            vision_distance: 5,
            visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
        playsound(this, istype(tool, typeof(obj_item_stack_medical_suture)) ? SFX_SUTURE_BEGIN : Dm.resource("sound/items/handling/surgery/cautery1.ogg"), 50, TRUE);
        if (!Dm.truthy(await do_after(
            this,
            Dm.num(Dm.initial(typeof(datum_surgery_operation_limb_close_skin), (datum_surgery_operation_limb_close_skin o) => o.time)) * 2 * Dm.num(tool.toolspeed),
            target: this,
            extra_checks: Dm.@new<datum_callback>(this, nameof(manual_cauterize_check), tool, limb))))
            return ITEM_INTERACT_BLOCKING;

        visible_message(
            span_notice(I18n.T("[] closes [] own [] with [].", this, p_their(), limb.plaintext_zone, tool)),
            span_notice(I18n.T("You close your own [] with [].", limb.plaintext_zone, tool)),
            span_hear(I18n.T("You hear [] sounds.", Dm.truthy(tool?.get_temperature()) ? I18n.T("singeing") : I18n.T("stitching"))),
            vision_distance: 5,
            visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
        playsound(this, istype(tool, typeof(obj_item_stack_medical_suture)) ? SFX_SUTURE_END : Dm.resource("sound/items/handling/surgery/cautery2.ogg"), 50, TRUE);
        await limb.remove_surgical_state(ALL_SURGERY_STATES_UNSET_ON_CLOSE);
        if (tool is obj_item_stack_medical_suture suture_tool)
            await suture_tool.use(1);
        else
            await limb.receive_damage(burn: 5, wound_bonus: CANT_WOUND, damage_source: tool);
        return ITEM_INTERACT_SUCCESS;
    }

    /// Callback for checking if the cauterization do-after can continue
    // tg: code/modules/surgery/operations/_operation.dm:175
    public virtual object manual_cauterize_check(obj_item tool = null, obj_item_bodypart limb = null)
    {
        // PRIVATE_PROC(TRUE)

        if (QDELETED(limb) || limb.owner != this)
            return FALSE;

        if (QDELETED(tool))
            return FALSE;
        else if (tool is obj_item_stack_medical_suture suture_tool)
        {
            if (Dm.num(suture_tool.amount) <= 0)
                return FALSE;
        }
        else if (!Dm.eq(tool.tool_behaviour, TOOL_CAUTERY))
        {
            if (Dm.num(tool.get_temperature()) <= FIRE_MINIMUM_TEMPERATURE_TO_EXIST)
                return FALSE;
        }

        // we need to have a surgery state worth closing
        float states_to_check = ALL_SURGERY_STATES_UNSET_ON_CLOSE;
        if (!Dm.truthy(LIMB_HAS_BONES(limb)))
            states_to_check = Dm.bitand(states_to_check, Dm.bitnot(BONELESS_SURGERY_STATES));
        if (!Dm.truthy(LIMB_HAS_VESSELS(limb)))
            states_to_check = Dm.bitand(states_to_check, Dm.bitnot(VESSELLESS_SURGERY_STATES));
        if (!Dm.truthy(LIMB_HAS_SKIN(limb)))
            states_to_check = Dm.bitand(states_to_check, Dm.bitnot(SKINLESS_SURGERY_STATES));
        if (!Dm.truthy(states_to_check) || !Dm.truthy(LIMB_HAS_ANY_SURGERY_STATE(limb, states_to_check)))
            return FALSE;

        // skin has to be open or cut to do anything (we can't have a negative state without also having skin open anyways)
        if (!Dm.truthy(LIMB_HAS_ANY_SURGERY_STATE(limb, ALL_SURGERY_SKIN_STATES)))
            return FALSE;

        return TRUE;
    }

    /// Debug proc to print all surgeries available to whoever called the proc
    // tg: code/modules/surgery/operations/_operation.dm:209
    public virtual async DmTask debug_get_all_available_surgeries()
    {
        mob_living surgeon = usr as mob_living;
        if (!isliving(surgeon))
            return;

        DmList operations = (DmList)await surgeon.get_available_operations(this, surgeon.get_active_held_item());
        if (!Dm.truthy(length(operations)))
        {
            to_chat(surgeon, boxed_message(span_info(I18n.T("No available surgeries."))));
            return;
        }

        DmList operations_info = Dm.list();
        foreach (object radial_slice in Dm.each(operations))
        {
            datum_surgery_operation operation = (datum_surgery_operation)((DmList)operations[radial_slice])[1];
            atom_movable operating_on = (atom_movable)((DmList)operations[radial_slice])[2];
            Dm.append(ref operations_info, Dm.text("[]: [] on []", radial_slice, operation.name, operating_on));
        }

        to_chat(surgeon, boxed_message(span_info(I18n.T("Available surgeries:<br><hr>[]", jointext(operations_info, "<br>")))));
    }

    /// Takes a target zone and returns a list of readable surgery states for that zone.
    /// Example output may be list("Skin is cut", "Blood vessels are unclamped", "Bone is sawed")
    // tg: code/modules/surgery/operations/_operation.dm:229
    public virtual object get_surgery_state_as_list(object target_zone = null)
    {
        DmList state = Dm.list();
        if (Dm.truthy(has_limbs))
        {
            obj_item_bodypart part = get_bodypart((string)target_zone) as obj_item_bodypart;
            if (part == null)
                return Dm.list(I18n.T("Bodypart missing"));

            if (HAS_TRAIT(part, TRAIT_READY_TO_OPERATE))
                Dm.append(ref state, I18n.T("Ready for surgery"));
            if (!Dm.truthy(is_location_accessible(target_zone, IGNORED_OPERATION_CLOTHING_SLOTS)))
                Dm.append(ref state, I18n.T("Bodypart is obstructed by clothing"));

            float part_state = Dm.truthy(part?.surgery_state) ? part.surgery_state : NONE;

            if (!Dm.truthy(LIMB_HAS_BONES(part)))
            {
                part_state = Dm.bitand(part_state, Dm.bitnot(BONELESS_SURGERY_STATES));
                Dm.append(ref state, I18n.T("Bodypart lacks bones (counts as [])", jointext(bitfield_to_list(BONELESS_SURGERY_STATES, SURGERY_STATE_READABLE()), ", ")));
            }
            if (!Dm.truthy(LIMB_HAS_VESSELS(part)))
            {
                part_state = Dm.bitand(part_state, Dm.bitnot(VESSELLESS_SURGERY_STATES));
                Dm.append(ref state, I18n.T("Bodypart lacks blood vessels (counts as [])", jointext(bitfield_to_list(VESSELLESS_SURGERY_STATES, SURGERY_STATE_READABLE()), ", ")));
            }
            if (!Dm.truthy(LIMB_HAS_SKIN(part)))
            {
                part_state = Dm.bitand(part_state, Dm.bitnot(SKINLESS_SURGERY_STATES));
                Dm.append(ref state, I18n.T("Bodypart lacks skin (counts as [])", jointext(bitfield_to_list(SKINLESS_SURGERY_STATES, SURGERY_STATE_READABLE()), ", ")));
            }

            Dm.append(ref state, bitfield_to_list(part_state, SURGERY_STATE_READABLE()));
        }
        else
        {
            if (HAS_TRAIT(this, TRAIT_READY_TO_OPERATE))
                Dm.append(ref state, I18n.T("Ready for surgery"));

            datum_status_effect_basic_surgery_state state_holder = has_status_effect(typeof(datum_status_effect_basic_surgery_state)) as datum_status_effect_basic_surgery_state;
            Dm.append(ref state, bitfield_to_list(state_holder?.surgery_state, SURGERY_STATE_READABLE()));
        }

        return state;
    }

    /**
     * Adds a speed modifier to this mob
     *
     * * id - id of the modifier, string
     * * amount - the multiplier to apply to surgery speed.
     * This is multiplicative with other modifiers.
     * * duration - how long the modifier should last in deciseconds.
     * If null, it will be permanent until removed.
     */
    // tg: code/modules/surgery/operations/_operation.dm:274
    public virtual void add_surgery_speed_mod(object id = null, object amount = null, object duration = null)
    {
        if (!(id != null))
            throw Dm.CRASH(assertion_message("code/modules/surgery/operations/_operation.dm", 275, "!isnull(id)", "Surgery speed mod ID cannot be null"), "code/modules/surgery/operations/_operation.dm", 275);
        if (!isnum(amount))
            throw Dm.CRASH(assertion_message("code/modules/surgery/operations/_operation.dm", 276, "isnum(amount)", "Surgery speed mod amount must be a number"), "code/modules/surgery/operations/_operation.dm", 276);
        if (!(isnum(duration) || duration == null))
            throw Dm.CRASH(assertion_message("code/modules/surgery/operations/_operation.dm", 277, "isnum(duration) || isnull(duration)", "Surgery speed mod duration must be a number or null"), "code/modules/surgery/operations/_operation.dm", 277);

        object existing = LAZYACCESS(mob_surgery_speed_mods, id);
        if (Dm.eq(existing, amount))
            return;

        if (isnum(existing))
        {
            if (Dm.num(amount) > 1 && Dm.num(existing) > 1)
            {
                // both are speed decreases, take the better one
                LAZYSET(ref mob_surgery_speed_mods, id, max(amount, existing));
            }
            else if (Dm.num(amount) < 1 && Dm.num(existing) < 1)
            {
                // both are speed increases, take the better one
                LAZYSET(ref mob_surgery_speed_mods, id, min(amount, existing));
            }
            else
            {
                // one of each, just multiply them
                LAZYSET(ref mob_surgery_speed_mods, id, Dm.num(amount) * Dm.num(existing));
            }
        }
        else
            LAZYSET(ref mob_surgery_speed_mods, id, amount);

        if (isnum(duration))
            _addtimer(Dm.@new<datum_callback>(this, nameof(remove_surgery_speed_mod), id), Dm.num(duration), Dm.bitor(Dm.bitor(Dm.bitor(TIMER_DELETE_ME, TIMER_UNIQUE), TIMER_OVERRIDE), TIMER_NO_HASH_WAIT), file: "code/modules/surgery/operations/_operation.dm", line: 298);
    }

    /**
     * Removes a speed modifier from this mob
     *
     * * id - id of the modifier to remove, string
     */
    // tg: code/modules/surgery/operations/_operation.dm:305
    public virtual void remove_surgery_speed_mod(object id = null)
    {
        LAZYREMOVE(ref mob_surgery_speed_mods, id);
    }
}

public partial class datum_controller_global_vars
{
    // tg: code/modules/surgery/operations/_operation.dm:308  GLOBAL_DATUM_INIT(operations, /datum/operation_holder, new)
    public datum_operation_holder operations;

    // tg: code/modules/surgery/operations/_operation.dm:308  (GLOBAL_MANAGED(operations, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaloperations()
    {
        operations = Dm.@new<datum_operation_holder>();
        Dm.append(ref gvars_datum_init_order, "operations");
    }
}

/// Singleton containing all surgery operation, as well as some helpers for organizing them
// tg: code/modules/surgery/operations/_operation.dm:311
public partial class datum_operation_holder : datum
{
    // tg: code/modules/surgery/operations/_operation.dm:320
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        operations_by_typepath = Dm.list();
        unlocked = Dm.list();
        locked = Dm.list();

        foreach (Type operation_type in Dm.each_cast<Type>(valid_subtypesof(typeof(datum_surgery_operation))))
        {
            datum_surgery_operation operation = (datum_surgery_operation)Dm.@new(operation_type);
            if (operation.name == null)
                stack_trace(Dm.text("Surgery operation '[]' is missing a name!", operation_type), "code/modules/surgery/operations/_operation.dm", 328);

            operations_by_typepath[operation_type] = operation;
            if (Dm.truthy(Dm.bitand(operation.operation_flags, OPERATION_LOCKED)))
                Dm.append(ref locked, operation_type);
            else
                Dm.append(ref unlocked, operation_type);
        }
    }

    /// Takes in a list of operation typepaths and returns their singleton instances. Optionally can filter out replaced surgeries and by certain operation flags.
    // tg: code/modules/surgery/operations/_operation.dm:336
    public virtual object get_instances_from(DmList typepaths = null, float? filter_replaced = null)
    {
        filter_replaced ??= TRUE;
        DmList result = Dm.list();
        foreach (object operation_type in Dm.each(typepaths))
        {
            datum_surgery_operation operation = operations_by_typepath[operation_type] as datum_surgery_operation;
            if (operation == null)
                continue;
            if (Dm.truthy(filter_replaced) && Dm.truthy(is_replaced(operation, typepaths)))
                continue;
            Dm.append(ref result, operation);
        }
        return result;
    }

    /// Check if the passed operation has been replaced by a typepath in the provided operation pool
    // tg: code/modules/surgery/operations/_operation.dm:348
    public virtual object is_replaced(datum_surgery_operation operation = null, DmList operation_pool = null)
    {
        if (operation.replaced_by == null || !Dm.truthy(length(operation_pool)))
            return FALSE;
        if (operation.replaced_by == operation.type)
            return FALSE;
        if (Dm.@in(operation.replaced_by, operation_pool))
            return TRUE;
        // recursively check if the operation that replaces us is itself replaced
        datum_surgery_operation next_highest_operation = operations_by_typepath[operation.replaced_by] as datum_surgery_operation;
        if (next_highest_operation == null)
            return FALSE;
        return is_replaced(next_highest_operation, operation_pool);
    }
}

/**
 * ## Surgery operation datum
 *
 * A singleton datum which represents a surgical operation that can be performed on a mob.
 *
 * Surgery operations can be something simple, like moving between surgery states (tend wounds, clamp vessels),
 * or more complex, like outright replacing limbs or organs. As such the datum is very flexible.
 *
 * See also [/datum/surgery_operation/basic], which is a bit more complex to use
 * but allows for operations to target any mob type, rather than only those with limbs or organs.
 */
// tg: code/modules/surgery/operations/_operation.dm:389
public partial class datum_surgery_operation : datum
{
    /**
     * Checks to see if this operation can be performed
     * This is the main entry point for checking availability
     */
    // tg: code/modules/surgery/operations/_operation.dm:470
    public virtual object check_availability(mob_living patient = null, atom_movable operating_on = null, mob_living surgeon = null, object tool = null, object operated_zone = null)
    {
        // SHOULD_NOT_OVERRIDE(TRUE) SHOULD_NOT_SLEEP(TRUE) SHOULD_BE_PURE(TRUE)

        if (operating_on == null)
            return FALSE;

        if (Dm.num(get_tool_quality(tool)) <= 0)
            return FALSE;

        if (!Dm.truthy(is_available(operating_on, operated_zone)))
            return FALSE;

        if (patient == null)
        {
            if (Dm.truthy(Dm.bitand(operation_flags, OPERATION_NO_PATIENT_REQUIRED)))
                return snowflake_check_availability(operating_on, surgeon, tool, operated_zone);
            return FALSE;
        }

        if (!Dm.truthy(Dm.bitand(operation_flags, OPERATION_STANDING_ALLOWED)) && !IS_LYING_OR_CANNOT_LIE(patient))
            return FALSE;

        if (!Dm.truthy(Dm.bitand(operation_flags, OPERATION_SELF_OPERABLE)) && patient == surgeon && !HAS_TRAIT(surgeon, TRAIT_SELF_SURGERY))
            return FALSE;

        return snowflake_check_availability(operating_on, surgeon, tool, operated_zone);
    }

    /**
     * Snowflake checks for surgeries which need many interconnected conditions to be met
     */
    // tg: code/modules/surgery/operations/_operation.dm:500
    public virtual object snowflake_check_availability(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, object operated_zone = null)
    {
        // PROTECTED_PROC(TRUE)
        return TRUE;
    }

    /**
     * Returns the quality of the passed tool for this operation
     * Quality directly affects the time taken to perform the operation
     *
     * 0 = unusable
     * 1 = standard quality
     */
    // tg: code/modules/surgery/operations/_operation.dm:511
    public virtual object get_tool_quality(object tool = null)
    {
        tool ??= IMPLEMENT_HAND;
        // PROTECTED_PROC(TRUE)
        if (!Dm.truthy(length(implements)))
            return 1;
        if (!isitem(tool))
            return implements[tool];
        if (!Dm.truthy(tool_check((obj_item)tool)))
            return 0;

        obj_item realtool = (obj_item)tool;
        object quality = implements[realtool.tool_behaviour] is var or__1 && Dm.truthy(or__1) ? or__1
            : (is_type_in_list(realtool, implements, zebra: TRUE, return_first_match: TRUE) is var or__2 && Dm.truthy(or__2) ? or__2 : 0);
        return Dm.num(realtool.toolspeed) * Dm.num(quality);
    }

    /**
     * Return a radial slice, a list of radial slices, or an assoc list of radial slice to operation info
     *
     * By default it returns a single option with the operation name and description,
     * but you can override this proc to return multiple options for one operation, like selecting which organ to operate on.
     */
    // tg: code/modules/surgery/operations/_operation.dm:529
    public virtual async DmTask<object> get_radial_options(atom_movable operating_on = null, obj_item tool = null, object operating_zone = null)
    {
        if (!Dm.truthy(main_option))
        {
            main_option = Dm.@new<datum_radial_menu_choice>();
            main_option.image = await get_default_radial_image();
            main_option.name = name;
            main_option.info = desc;
        }

        return main_option;
    }

    /**
     * Checks to see if this operation can be performed on the provided target
     */
    // tg: code/modules/surgery/operations/_operation.dm:541
    public virtual object is_available(atom_movable operating_on = null, object operated_zone = null)
    {
        // PROTECTED_PROC(TRUE) SHOULD_CALL_PARENT(TRUE)

        if (Dm.truthy(all_surgery_states_required) && !Dm.truthy(has_surgery_state(operating_on, all_surgery_states_required)))
            return FALSE;

        if (Dm.truthy(any_surgery_states_required) && !Dm.truthy(has_any_surgery_state(operating_on, any_surgery_states_required)))
            return FALSE;

        if (Dm.truthy(any_surgery_states_blocked) && Dm.truthy(has_any_surgery_state(operating_on, any_surgery_states_blocked)))
            return FALSE;

        mob_living patient = get_patient(operating_on) as mob_living;
        if (patient == null)
        {
            if (!Dm.truthy(Dm.bitand(operation_flags, OPERATION_NO_PATIENT_REQUIRED)))
                return FALSE;
            return state_check(operating_on);
        }

        if (!Dm.truthy(state_check(operating_on)))
            return FALSE;

        if (!Dm.truthy(Dm.bitand(operation_flags, OPERATION_IGNORE_CLOTHES)) && !Dm.truthy(patient.is_location_accessible(operated_zone, IGNORED_OPERATION_CLOTHING_SLOTS)))
            return FALSE;

        return TRUE;
    }

    /// Check if the movable being operated on has all the passed surgery states
    // tg: code/modules/surgery/operations/_operation.dm:569
    public virtual object has_surgery_state(atom_movable operating_on = null, object state = null)
    {
        // PROTECTED_PROC(TRUE)
        return FALSE;
    }

    /// Check if the movable being operated on has any of the passed surgery states
    // tg: code/modules/surgery/operations/_operation.dm:574
    public virtual object has_any_surgery_state(atom_movable operating_on = null, object state = null)
    {
        // PROTECTED_PROC(TRUE)
        return FALSE;
    }

    /**
     * Any operation specific state checks, such as checking for traits or more complex state requirements
     */
    // tg: code/modules/surgery/operations/_operation.dm:581
    public virtual object state_check(atom_movable operating_on = null)
    {
        // PROTECTED_PROC(TRUE)
        return TRUE;
    }

    /**
     * Checks to see if the provided tool is valid for this operation
     * You can override this to add more specific checks, such as checking sharpness
     */
    // tg: code/modules/surgery/operations/_operation.dm:589
    public virtual object tool_check(obj_item tool = null)
    {
        // PROTECTED_PROC(TRUE)
        return TRUE;
    }

    /**
     * Returns the name of whatever tool is recommended for this operation, such as "hemostat"
     */
    // tg: code/modules/surgery/operations/_operation.dm:596
    public virtual object get_recommended_tool()
    {
        if (!Dm.truthy(length(implements)))
            return null;
        object recommendation = implements[1];
        if (istext(recommendation))
            return recommendation; // handles tools or IMPLEMENT_HAND
        if (Dm.eq(recommendation, typeof(obj_item)))
            return get_any_tool();
        if (ispath(recommendation, typeof(obj_item)))
        {
            Type tool = (Type)recommendation;
            return Dm.initial(tool, (obj_item o) => o.name);
        }
        return null;
    }

    /**
     * For surgery operations that can be performed with any item, this explains what kind of item is needed
     */
    // tg: code/modules/surgery/operations/_operation.dm:612
    public virtual object get_any_tool()
    {
        return I18n.T("Any item");
    }

    /**
     * Return a list of lists of strings indicating the various requirements for this operation
     */
    // tg: code/modules/surgery/operations/_operation.dm:618
    public virtual object get_requirements()
    {
        // SHOULD_NOT_OVERRIDE(TRUE)
        return Dm.list(
            all_required_strings(),
            any_required_strings(),
            any_optional_strings(),
            all_blocked_strings());
    }

    /// Returns a list of strings indicating requirements for this operation
    /// "All requirements" are formatted as "All of the following must be true:"
    // tg: code/modules/surgery/operations/_operation.dm:629
    public virtual object all_required_strings()
    {
        // SHOULD_CALL_PARENT(TRUE)
        object dot = bitfield_to_list(all_surgery_states_required, SURGERY_STATE_GUIDES(I18n.T("must")));
        if (!Dm.truthy(Dm.bitand(operation_flags, OPERATION_STANDING_ALLOWED)))
            Dm.append(ref dot, I18n.T("the patient must be lying down"));
        return dot;
    }

    /// Returns a list of strings indicating any of the requirements for this operation
    /// "Any requirements" are formatted as "At least one of the following must be true:"
    // tg: code/modules/surgery/operations/_operation.dm:637
    public virtual object any_required_strings()
    {
        // SHOULD_CALL_PARENT(TRUE)
        object dot = Dm.list();
        // grouped states are filtered down to make it more readable
        float parsed_any_flags = any_surgery_states_required;
        if (Dm.bitand(parsed_any_flags, ALL_SURGERY_BONE_STATES) == ALL_SURGERY_BONE_STATES)
        {
            parsed_any_flags = Dm.bitand(parsed_any_flags, Dm.bitnot(ALL_SURGERY_BONE_STATES));
            Dm.append(ref dot, I18n.T("the bone must be sawed or drilled"));
        }
        if (Dm.bitand(parsed_any_flags, ALL_SURGERY_SKIN_STATES) == ALL_SURGERY_SKIN_STATES)
        {
            parsed_any_flags = Dm.bitand(parsed_any_flags, Dm.bitnot(ALL_SURGERY_SKIN_STATES));
            Dm.append(ref dot, I18n.T("the skin must be cut or opened"));
        }
        if (Dm.bitand(parsed_any_flags, ALL_SURGERY_VESSEL_STATES) == ALL_SURGERY_VESSEL_STATES)
        {
            parsed_any_flags = Dm.bitand(parsed_any_flags, Dm.bitnot(ALL_SURGERY_VESSEL_STATES));
            Dm.append(ref dot, I18n.T("the blood vessels must be clamped or unclamped")); // weird phrasing but whatever
        }

        Dm.append(ref dot, bitfield_to_list(parsed_any_flags, SURGERY_STATE_GUIDES(I18n.T("must"))));
        return dot;
    }

    /// Returns a list of strings indicating optional conditions for this operation
    /// "Optional conditions" are formatted as "Additionally, any of the following may be true:"
    // tg: code/modules/surgery/operations/_operation.dm:656
    public virtual object any_optional_strings()
    {
        // SHOULD_CALL_PARENT(TRUE)
        object dot = Dm.list();
        if (Dm.truthy(Dm.bitand(operation_flags, OPERATION_SELF_OPERABLE)))
            Dm.append(ref dot, I18n.T("a surgeon may perform this on themselves"));
        return dot;
    }

    /// Returns a list of strings indicating blocked states for this operation
    /// "Blocked requirements" are formatted as "However, none of the following may be true:"
    // tg: code/modules/surgery/operations/_operation.dm:664
    public virtual object all_blocked_strings()
    {
        // SHOULD_CALL_PARENT(TRUE)
        object dot = Dm.list();
        // grouped states are filtered down to make it more readable
        float parsed_blocked_flags = any_surgery_states_blocked;
        if (Dm.bitand(parsed_blocked_flags, ALL_SURGERY_BONE_STATES) == ALL_SURGERY_BONE_STATES)
        {
            parsed_blocked_flags = Dm.bitand(parsed_blocked_flags, Dm.bitnot(ALL_SURGERY_BONE_STATES));
            Dm.append(ref dot, I18n.T("the bone must be intact"));
        }
        if (Dm.bitand(parsed_blocked_flags, ALL_SURGERY_SKIN_STATES) == ALL_SURGERY_SKIN_STATES)
        {
            parsed_blocked_flags = Dm.bitand(parsed_blocked_flags, Dm.bitnot(ALL_SURGERY_SKIN_STATES));
            Dm.append(ref dot, I18n.T("the skin must be intact"));
        }
        if (Dm.bitand(parsed_blocked_flags, ALL_SURGERY_VESSEL_STATES) == ALL_SURGERY_VESSEL_STATES)
        {
            parsed_blocked_flags = Dm.bitand(parsed_blocked_flags, Dm.bitnot(ALL_SURGERY_VESSEL_STATES));
            Dm.append(ref dot, I18n.T("the blood vessels must be intact"));
        }

        Dm.append(ref dot, bitfield_to_list(parsed_blocked_flags, SURGERY_STATE_GUIDES(I18n.T("must not"))));
        if (!Dm.truthy(Dm.bitand(operation_flags, OPERATION_IGNORE_CLOTHES)))
            Dm.append(ref dot, I18n.T("the operation site must not be obstructed by clothing"));
        return dot;
    }

    /**
     * Returns what icon this surgery uses by default on the radial wheel if it does not implement its own radial options
     *
     * Also used when generating icons for the wiki
     */
    // tg: code/modules/surgery/operations/_operation.dm:688
    public virtual async DmTask<object> get_default_radial_image()
    {
        return image(icon: Dm.resource("icons/effects/random_spawners.dmi"), icon_state: "questionmark");
    }

    /// Helper to get a generic limb radial image based on body zone
    // tg: code/modules/surgery/operations/_operation.dm:692
    public virtual async DmTask<object> get_generic_limb_radial_image(object body_zone = null)
    {
        // SHOULD_NOT_OVERRIDE(TRUE) PROTECTED_PROC(TRUE)

        if (Dm.eq(body_zone, BODY_ZONE_HEAD) || Dm.eq(body_zone, BODY_ZONE_CHEST) || Dm.eq(body_zone, BODY_ZONE_PRECISE_EYES) || Dm.eq(body_zone, BODY_ZONE_PRECISE_MOUTH))
            return image(icon: Dm.resource("icons/obj/medical/surgery_ui.dmi"), icon_state: Dm.text("surgery_[]", body_zone));
        if (Dm.eq(body_zone, BODY_ZONE_L_ARM) || Dm.eq(body_zone, BODY_ZONE_R_ARM))
            return image(icon: Dm.resource("icons/obj/medical/surgery_ui.dmi"), icon_state: "surgery_arms");
        if (Dm.eq(body_zone, BODY_ZONE_L_LEG) || Dm.eq(body_zone, BODY_ZONE_R_LEG))
            return image(icon: Dm.resource("icons/obj/medical/surgery_ui.dmi"), icon_state: "surgery_legs");
        return await get_default_radial_image();
    }

    /**
     * Helper for constructing overlays to apply to a radial image
     *
     * Input can be
     * * - An atom typepath
     * * - An atom instance
     * * - Another image
     *
     * Returns a list of images
     */
    // tg: code/modules/surgery/operations/_operation.dm:714
    public virtual object add_radial_overlays(object overlay_icons = null)
    {
        // SHOULD_NOT_OVERRIDE(TRUE) PROTECTED_PROC(TRUE)

        if (!islist(overlay_icons))
            overlay_icons = Dm.list(overlay_icons);

        DmList created_list = Dm.list();
        foreach (object input in Dm.each(overlay_icons))
        {
            image created = input is image input_image ? input_image : image(input);
            created.layer = FLOAT_LAYER;
            created.plane = FLOAT_PLANE;
            created.pixel_w = 0;
            created.pixel_x = 0;
            created.pixel_y = 0;
            created.pixel_z = 0;
            Dm.append(ref created_list, created);
        }

        return created_list;
    }

    /**
     * Collates all time modifiers for this operation and returns the final modifier
     */
    // tg: code/modules/surgery/operations/_operation.dm:738
    public virtual object get_time_modifiers(atom_movable operating_on = null, mob_living surgeon = null, object tool = null)
    {
        // PROTECTED_PROC(TRUE)
        float total_mod = 1.0f;
        total_mod *= get_tool_quality(tool) is var or__1 && Dm.truthy(or__1) ? Dm.num(or__1) : 1.0f;
        // Ignore alllll the penalties (but also all the bonuses)
        if (!HAS_TRAIT(surgeon, TRAIT_IGNORE_SURGERY_MODIFIERS))
        {
            mob_living patient = get_patient(operating_on) as mob_living;
            total_mod *= Dm.num(get_surgeon_surgery_speed_mod(patient, surgeon, tool));
            if (patient != null) // Some surgeries can lack patients
            {
                total_mod *= Dm.num(get_location_modifier(get_turf(patient), patient, surgeon, tool));
                total_mod *= Dm.num(get_mob_surgery_speed_mod(patient, surgeon, tool));
            }
            // Using TRAIT_SELF_SURGERY on a surgery which doesn't normally allow self surgery imparts a penalty
            if (operating_on == surgeon && HAS_TRAIT(surgeon, TRAIT_SELF_SURGERY) && !Dm.truthy(Dm.bitand(operation_flags, OPERATION_SELF_OPERABLE)))
                total_mod *= 1.5f;
        }
        return round(total_mod, 0.01f);
    }

    /// Returns a time modifier based on the mob's status
    // tg: code/modules/surgery/operations/_operation.dm:755
    public virtual object get_mob_surgery_speed_mod(mob_living patient = null, mob_living surgeon = null, object tool = null)
    {
        // PROTECTED_PROC(TRUE)
        float basemod = 1.0f;
        foreach ((object mod_id, object mod_amt) in Dm.pairs(patient.mob_surgery_speed_mods))
            basemod *= Dm.num(mod_amt);
        if (HAS_TRAIT(patient, TRAIT_SURGICALLY_ANALYZED))
            basemod *= 0.8f;
        if (HAS_TRAIT(patient, TRAIT_ANALGESIA))
            basemod *= 0.8f;
        return basemod;
    }

    /// Returns a time modifier based on the surgeon's status
    // tg: code/modules/surgery/operations/_operation.dm:767
    public virtual object get_surgeon_surgery_speed_mod(mob_living patient = null, mob_living surgeon = null, object tool = null)
    {
        // PROTECTED_PROC(TRUE)
        float basemod = 1.0f;
        if (Dm.truthy(Dm.bitand(operation_flags, OPERATION_MORBID)) && HAS_MIND_TRAIT(surgeon, TRAIT_MORBID) && isitem(tool))
        {
            obj_item realtool = (obj_item)tool;
            if (Dm.truthy(Dm.bitand(realtool.item_flags, CRUEL_IMPLEMENT)))
                basemod *= 0.7f;
        }

        float drunkness = Dm.num(surgeon.get_drunk_amount());
        // being drunk gives upwards of a 12x speed penalty - unless you land in the ballmer peak
        if (drunkness >= BALLMER_PEAK_LOW_END && drunkness <= BALLMER_PEAK_HIGH_END)
            basemod *= 0.8f;
        else
            basemod *= 1 + Dm.num(round(Dm.pow(drunkness, 1.5f) / 90, 0.1f));

        return basemod;
    }

    // tg: code/modules/surgery/operations/_operation.dm:785  var/static/list/modifiers (rules 10.6: a TgState field)
    private static ref DmList get_location_modifier__modifiers => ref TgState.Current.datum_surgery_operation__get_location_modifier__modifiers;

    /// Gets the surgery speed modifier for a given mob, based off what sort of table/bed/whatever is on their turf.
    // tg: code/modules/surgery/operations/_operation.dm:785
    public virtual object get_location_modifier(turf operation_turf = null, mob_living patient = null, mob_living surgeon = null, object tool = null)
    {
        // PROTECTED_PROC(TRUE)
        // Technically this IS a typecache, just not the usual kind :3
        // The order of the modifiers matter, latter entries override earlier ones
        get_location_modifier__modifiers ??= (DmList)zebra_typecacheof(Dm.list(
            Dm.kv(typeof(obj_structure_table), 1.25f),
            Dm.kv(typeof(obj_structure_table_optable), 1.0f),
            Dm.kv(typeof(obj_structure_table_optable_abductor), 0.85f),
            Dm.kv(typeof(obj_machinery_stasis), 1.15f),
            Dm.kv(typeof(obj_structure_bed), 1.5f),
            Dm.kv(typeof(obj_structure_kitchenspike), 1.5f)));
        float mod = 2.0f;
        foreach (obj thingy in Dm.each<obj>(operation_turf))
            mod = Dm.num(min(mod, get_location_modifier__modifiers[thingy.type] is var or__1 && Dm.truthy(or__1) ? or__1 : 2.0f));
        return mod;
    }

    /**
     * Gets what movable is being operated on by a surgeon during this operation
     * Determines what gets passed into the try_perform() proc
     * If null is returned, the operation cannot be performed
     *
     * * operating_on - The mob or detached bodypart being operated on
     * * body_zone - The body zone being operated on
     *
     * Returns the atom/movable being operated on
     */
    // tg: code/modules/surgery/operations/_operation.dm:812
    public virtual object get_operation_target(atom_movable operating_on = null, object body_zone = null)
    {
        return operating_on;
    }

    /**
     * Called by operating computers to hint that this surgery could come next given the target's current state
     */
    // tg: code/modules/surgery/operations/_operation.dm:818
    public virtual object show_as_next_step(atom_movable potential_target = null, object operated_zone = null)
    {
        atom_movable operate_on = get_operation_target(potential_target, operated_zone) as atom_movable;
        return Dm.num(operate_on != null && Dm.truthy(is_available(operate_on, operated_zone)));
    }

    /**
     * The actual chain of performing the operation
     *
     * * operating_on - The atom being operated on, probably a bodypart or occasionally a mob directly
     * * surgeon - The mob performing the operation
     * * tool - The tool being used to perform the operation. CAN BE A STRING, ie, IMPLEMENT_HAND, be careful
     * * operation_args - Additional arguments passed into the operation. Contains largely niche info that only certain operations care about or can be accessed through other means
     *
     * Returns an item interaction flag - intended to be invoked from the interaction chain
     */
    // tg: code/modules/surgery/operations/_operation.dm:833
    public virtual async DmTask<object> try_perform(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        operation_args ??= Dm.list();
        // SHOULD_NOT_OVERRIDE(TRUE)
        mob_living patient = get_patient(operating_on) as mob_living;

        if (!Dm.truthy(check_availability(patient, operating_on, surgeon, tool, operation_args[OPERATION_TARGET_ZONE])))
            return ITEM_INTERACT_BLOCKING;

        if (isitem(tool))
        {
            obj_item realtool = (obj_item)tool;
            // SEND_SIGNAL(realtool, COMSIG_ITEM_USED_IN_SURGERY, src, operating_on, surgeon) (rules 11.1 (e))
            object tool_return = !Dm.truthy(realtool._listen_lookup?[COMSIG_ITEM_USED_IN_SURGERY]) ? (object)NONE : realtool._SendSignal(COMSIG_ITEM_USED_IN_SURGERY, Dm.list(realtool, this, operating_on, surgeon));
            if (Dm.truthy(Dm.bitand(tool_return, ITEM_INTERACT_ANY_BLOCKER)))
                return tool_return;
        }

        if (!Dm.truthy(await start_operation(operating_on, surgeon, tool, operation_args)))
            return ITEM_INTERACT_BLOCKING;

        bool was_sleeping = false;
        object result = NONE;

        if (patient != null)
        {
            was_sleeping = IS_UNCONSCIOUS_AND_ALIVE(patient);
            await update_surgery_mood(patient, SURGERY_STATE_STARTED);
            // SEND_SIGNAL(patient, COMSIG_ATOM_SURGERY_STARTED, src, operating_on, tool)
            if (Dm.truthy(patient._listen_lookup?[COMSIG_ATOM_SURGERY_STARTED]))
                patient._SendSignal(COMSIG_ATOM_SURGERY_STARTED, Dm.list(patient, this, operating_on, tool));
        }

        do
        {
            operation_args[OPERATION_SPEED] = get_time_modifiers(operating_on, surgeon, tool);

            if (!Dm.truthy(await do_after(
                user: surgeon,
                // Actual delay is capped - think of the excess time as being added to failure chance instead
                delay: Dm.num(time) * Dm.num(min(operation_args[OPERATION_SPEED], SURGERY_MODIFIER_FAILURE_THRESHOLD)),
                target: (object)patient ?? operating_on,
                extra_checks: Dm.@new<datum_callback>(this, nameof(operate_check), patient, operating_on, surgeon, tool, operation_args),
                // You can only operate on one mob at a time without a hippocratic oath
                interaction_key: HAS_TRAIT(surgeon, TRAIT_HIPPOCRATIC_OATH) ? ((object)patient ?? operating_on) : DOAFTER_SOURCE_SURGERY)))
            {
                result = Dm.bitor(result, ITEM_INTERACT_BLOCKING);
                if (patient != null)
                    await update_surgery_mood(patient, SURGERY_STATE_FAILURE);
                break;
            }

            DmList patient_dna = get_patient_blood_dna(patient, operating_on) as DmList;
            if (Dm.truthy(patient_dna))
            {
                if (ishuman(surgeon))
                {
                    mob_living_carbon_human surgeon_human = (mob_living_carbon_human)surgeon;
                    await surgeon_human.add_blood_DNA_to_items(patient_dna, ITEM_SLOT_GLOVES);
                }
                else
                    await surgeon.add_blood_DNA(patient_dna);
            }

            // This will annoy doctors immensely
            // if(isitem(tool))
            // 	var/obj/item/realtool = tool
            // 	realtool.add_mob_blood(patient)

            // We modify speed modifier here AFTER the do after to increase failure chances, that's intentional
            // Think of it as modifying "effective time" rather than "real time". Failure chance goes up but the time it took is unchanged

            // Using TRAIT_SELF_SURGERY on a surgery which doesn't normally allow self surgery imparts a flat penalty
            // (On top of the 1.5x real time surgery modifier, an effective time modifier of 3x under standard conditions)
            if (patient == surgeon && HAS_TRAIT(surgeon, TRAIT_SELF_SURGERY) && !Dm.truthy(Dm.bitand(operation_flags, OPERATION_SELF_OPERABLE)))
                operation_args[OPERATION_SPEED] = Dm.num(operation_args[OPERATION_SPEED]) + 1.5f;

            // Otherwise if we have TRAIT_IGNORE_SURGERY_MODIFIERS we cannot possibly fail, unless we specifically allow failure
            if (HAS_TRAIT(surgeon, TRAIT_IGNORE_SURGERY_MODIFIERS) && !Dm.truthy(Dm.bitand(operation_flags, OPERATION_ALWAYS_FAILABLE)))
                operation_args[OPERATION_SPEED] = 0;

            if (Dm.truthy(operation_args[OPERATION_FORCE_FAIL]) || prob(clamp(GET_FAILURE_CHANCE(Dm.num(time), Dm.num(operation_args[OPERATION_SPEED])), 0, 99)))
            {
                failure(operating_on, surgeon, tool, operation_args);
                result = Dm.bitor(result, ITEM_INTERACT_FAILURE);
                if (patient != null)
                    await update_surgery_mood(patient, SURGERY_STATE_FAILURE);
            }
            else
            {
                success(operating_on, surgeon, tool, operation_args);
                result = Dm.bitor(result, ITEM_INTERACT_SUCCESS);
                if (patient != null)
                    await update_surgery_mood(patient, SURGERY_STATE_SUCCESS);
            }

            if (isstack(tool))
            {
                obj_item_stack tool_stack = (obj_item_stack)tool;
                await tool_stack.use(1);
            }
        }
        while (Dm.truthy(Dm.bitand(operation_flags, OPERATION_LOOPING)) && Dm.truthy(can_loop(patient, operating_on, surgeon, tool, operation_args)));

        if (patient == null)
            return result;

        // SEND_SIGNAL(patient, COMSIG_ATOM_SURGERY_FINISHED, src, operating_on, tool)
        if (Dm.truthy(patient._listen_lookup?[COMSIG_ATOM_SURGERY_FINISHED]))
            patient._SendSignal(COMSIG_ATOM_SURGERY_FINISHED, Dm.list(patient, this, operating_on, tool));
        if (Dm.eq(patient.stat, DEAD) && was_sleeping)
            Dm.Deferred("code/modules/surgery/operations/_operation.dm:921", "try_perform: surgeon.client?.give_award(/datum/award/achievement/jobs/sandman, surgeon) (achievements, M10+)");

        return result;
    }

    // tg: code/modules/surgery/operations/_operation.dm:925
    public virtual object get_patient_blood_dna(mob_living patient = null, atom_movable operating_on = null)
    {
        if (patient != null)
            return patient.get_blood_dna_list();

        if (isbodypart(operating_on))
        {
            obj_item_bodypart limb = (obj_item_bodypart)operating_on;
            return limb.blood_dna_info?.Copy();
        }

        if (isorgan(operating_on))
        {
            obj_item_organ guts = (obj_item_organ)operating_on;
            return guts.blood_dna_info?.Copy();
        }
        return null;
    }

    /// Called after an operation to check if it can be repeated/looped
    // tg: code/modules/surgery/operations/_operation.dm:938
    public virtual object can_loop(mob_living patient = null, atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        // PROTECTED_PROC(TRUE)
        return operate_check(patient, operating_on, surgeon, tool, operation_args);
    }

    /// Called during the do-after to check if the operation can continue
    // tg: code/modules/surgery/operations/_operation.dm:943
    public virtual object operate_check(mob_living patient = null, atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        // PROTECTED_PROC(TRUE)

        if (isstack(tool))
        {
            obj_item_stack tool_stack = (obj_item_stack)tool;
            if (Dm.num(tool_stack.amount) <= 0)
                return FALSE;
        }

        if (!Dm.truthy(surgeon.surgery_check(tool, Dm.truthy(Dm.bitand(operation_flags, OPERATION_NO_PATIENT_REQUIRED)) ? patient : ((atom_movable)patient ?? operating_on))))
            return FALSE;

        if (!Dm.truthy(check_availability(patient, operating_on, surgeon, tool, operation_args[OPERATION_TARGET_ZONE])))
            return FALSE;

        return TRUE;
    }

    /**
     * Allows for any extra checks or setup when the operation starts
     * If you want user input before for an operation, do it here
     *
     * This proc can sleep, sanity checks are automatically performed after it completes
     *
     * Return FALSE to cancel the operation
     * Return TRUE to continue
     */
    // tg: code/modules/surgery/operations/_operation.dm:968
    public virtual async DmTask<object> pre_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        // PROTECTED_PROC(TRUE)
        return TRUE;
    }

    /// Used to display messages to the surgeon and patient
    // tg: code/modules/surgery/operations/_operation.dm:973
    public virtual void display_results(mob_living surgeon = null, mob_living target = null, object self_message = null, object detailed_message = null, object vague_message = null, float? target_detailed = null)
    {
        target_detailed ??= FALSE;
        // SHOULD_NOT_OVERRIDE(TRUE) PROTECTED_PROC(TRUE)

        if (!istext(self_message))
            throw Dm.CRASH(assertion_message("code/modules/surgery/operations/_operation.dm", 977, "istext(self_message)", Dm.text("[] operation display_results must have a self_message!", type)), "code/modules/surgery/operations/_operation.dm", 977);
        if (!istext(detailed_message))
            throw Dm.CRASH(assertion_message("code/modules/surgery/operations/_operation.dm", 978, "istext(detailed_message)", Dm.text("[] operation display_results must have a detailed_message!", type)), "code/modules/surgery/operations/_operation.dm", 978);
        if (!(istext(vague_message) || Dm.truthy(target_detailed)))
            throw Dm.CRASH(assertion_message("code/modules/surgery/operations/_operation.dm", 979, "istext(vague_message) || target_detailed", Dm.text("[] operation display_results must have either a vague_message or target_detailed = TRUE!", type)), "code/modules/surgery/operations/_operation.dm", 979);

        surgeon.visible_message(
            message: detailed_message,
            self_message: self_message,
            vision_distance: 1,
            ignored_mobs: Dm.truthy(target_detailed) ? null : target);

        if (Dm.truthy(target_detailed))
            return;

        if (target == null)
        {
            if (Dm.truthy(Dm.bitand(operation_flags, OPERATION_NO_PATIENT_REQUIRED)))
                return;
            throw Dm.CRASH(Dm.text("[] operation attempted to call display_results without a patient, despite requiring one!", type), "code/modules/surgery/operations/_operation.dm", 994);
        }

        object you_feel = pick(I18n.T("a brief pain"), I18n.T("your body tense up"), I18n.T("an unnerving sensation"));
        target.show_message(
            msg: vague_message is var or__1 && Dm.truthy(or__1) ? or__1 : (detailed_message is var or__2 && Dm.truthy(or__2) ? or__2 : span_notice(I18n.T("You feel [] as you are operated on.", you_feel))),
            type: MSG_VISUAL,
            alt_msg: span_notice(I18n.T("You feel [] as you are operated on.", you_feel)));
    }

    /// Display pain message to the target based on their traits and condition
    // tg: code/modules/surgery/operations/_operation.dm:1004
    public virtual async DmTask display_pain(mob_living target = null, object pain_message = null, float? mechanical_surgery = null)
    {
        mechanical_surgery ??= FALSE;
        // SHOULD_NOT_OVERRIDE(TRUE) PROTECTED_PROC(TRUE)

        if (!Dm.truthy(pain_message))
            return;

        if (target == null)
        {
            if (Dm.truthy(Dm.bitand(operation_flags, OPERATION_NO_PATIENT_REQUIRED)))
                return;
            throw Dm.CRASH(Dm.text("[] operation attempted to call display_pain without a patient, despite requiring one!", type), "code/modules/surgery/operations/_operation.dm", 1013);
        }

        // Determine how drunk our patient is
        float drunken_patient = Dm.num(target.get_drunk_amount());
        // Create a probability to ignore the pain based on drunkenness level
        float drunken_ignorance_probability = clamp(drunken_patient, 0, 90);

        if (IS_UNCONSCIOUS(target))
            return;
        if (HAS_TRAIT(target, TRAIT_ANALGESIA) || (Dm.truthy(drunken_patient) && prob(drunken_ignorance_probability)))
        {
            to_chat(target, span_notice(I18n.T("You feel a dull, numb sensation as your body is surgically operated on.")));
            return;
        }
        to_chat(target, span_userdanger((string)pain_message));
        if (prob(30) && !Dm.truthy(mechanical_surgery))
            await target.emote("scream");
    }

    /// Plays a sound for the operation based on the tool used
    // tg: code/modules/surgery/operations/_operation.dm:1031
    public virtual void play_operation_sound(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, object sound_or_sound_list = null)
    {
        // PROTECTED_PROC(TRUE)

        if (isitem(tool) && Dm.truthy(Dm.bitand(operation_flags, OPERATION_MECHANIC)))
        {
            obj_item realtool = (obj_item)tool;
            realtool.play_tool_sound(operating_on);
            return;
        }

        object sound_to_play;
        if (islist(sound_or_sound_list))
        {
            DmList sounds = (DmList)sound_or_sound_list;
            if (isitem(tool))
            {
                obj_item realtool = (obj_item)tool;
                sound_to_play = sounds[realtool.tool_behaviour] is var or__1 && Dm.truthy(or__1) ? or__1 : is_type_in_list(realtool, sounds, zebra: TRUE);
            }
            else
                sound_to_play = sounds[tool];
        }
        else
            sound_to_play = sound_or_sound_list;

        if (Dm.truthy(sound_to_play))
            playsound(surgeon, sound_to_play, 50, TRUE);
    }

    /// Helper for getting the mob who is ultimately being operated on, given the movable that is truly being operated on.
    /// For example in limb surgeries this would return the mob the limb is attached to.
    // tg: code/modules/surgery/operations/_operation.dm:1055
    public virtual object get_patient(atom_movable operating_on = null)
    {
        return operating_on;
    }

    /// Helper for getting an operating compupter the patient is linked to
    // tg: code/modules/surgery/operations/_operation.dm:1059
    public virtual object locate_operating_computer(atom_movable operating_on = null)
    {
        // SHOULD_NOT_OVERRIDE(TRUE)
        turf operating_turf = get_turf(operating_on);
        if (operating_turf == null)
            return null;

        obj_structure_table_optable operating_table = Dm.locate<obj_structure_table_optable>(operating_turf);
        obj_machinery_computer_operating operating_computer = operating_table?.computer as obj_machinery_computer_operating;

        if (operating_computer == null || Dm.truthy(Dm.bitand(operating_computer.machine_stat, Dm.bitor(NOPOWER, BROKEN))))
            return null;

        return operating_computer;
    }

    /// Updates a patient's mood based on the surgery state and their traits
    // tg: code/modules/surgery/operations/_operation.dm:1074
    public virtual async DmTask update_surgery_mood(mob_living patient = null, object surgery_state = null)
    {
        // PROTECTED_PROC(TRUE)
        if (!Dm.truthy(Dm.bitand(operation_flags, OPERATION_AFFECTS_MOOD)))
            return;

        // Create a probability to ignore the pain based on drunkenness level
        float drunk_ignore_prob = clamp(Dm.num(patient.get_drunk_amount()), 0, 90);

        if (HAS_TRAIT(patient, TRAIT_ANALGESIA) || prob(drunk_ignore_prob))
        {
            patient.clear_mood_event(SURGERY_MOOD_CATEGORY); //incase they gained the trait mid-surgery (or became drunk). has the added side effect that if someone has a bad surgical memory/mood and gets drunk & goes back to surgery, they'll forget they hated it, which is kinda funny imo.
            return;
        }
        if (IS_UNCONSCIOUS(patient))
        {
            datum_mood_event_surgery target_mood_event = patient.mob_mood?.mood_events[SURGERY_MOOD_CATEGORY] as datum_mood_event_surgery;
            if (target_mood_event == null || Dm.truthy(target_mood_event.surgery_completed)) //don't give sleeping mobs trauma. that said, if they fell asleep mid-surgery after already getting the bad mood, lets make sure they wake up to a (hopefully) happy memory.
                return;
        }
        object sw__1 = surgery_state;
        if (Dm.eq(sw__1, SURGERY_STATE_STARTED))
            await patient.add_mood_event(SURGERY_MOOD_CATEGORY, surgery_started_mood_event);
        else if (Dm.eq(sw__1, SURGERY_STATE_SUCCESS))
            await patient.add_mood_event(SURGERY_MOOD_CATEGORY, surgery_success_mood_event);
        else if (Dm.eq(sw__1, SURGERY_STATE_FAILURE))
            await patient.add_mood_event(SURGERY_MOOD_CATEGORY, surgery_failure_mood_event);
        else
            throw Dm.CRASH(Dm.text("passed invalid surgery_state, \"[]\".", surgery_state), "code/modules/surgery/operations/_operation.dm", 1097);
    }

    /**
     * Called when the operation initiates
     * Don't touch this proc, override on_preop() instead
     */
    // tg: code/modules/surgery/operations/_operation.dm:1103
    public virtual async DmTask<object> start_operation(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        // SHOULD_NOT_OVERRIDE(TRUE) PRIVATE_PROC(TRUE)

        float preop_time = world.time;
        mob_living patient = get_patient(operating_on) as mob_living;
        if (!Dm.truthy(await pre_preop(operating_on, surgeon, tool, operation_args)))
            return FALSE;
        // if pre_preop slept, sanity check that everything is still valid
        if (preop_time != world.time)
        {
            if (patient != get_patient(operating_on))
                return FALSE;
            if (!in_range((atom)patient ?? operating_on, surgeon))
                return FALSE;
            if (!Dm.truthy(operate_check(patient, operating_on, surgeon, tool, operation_args)))
                return FALSE;
        }

        play_operation_sound(operating_on, surgeon, tool, preop_sound);
        await on_preop(operating_on, surgeon, tool, operation_args);
        return TRUE;
    }

    /**
     * Used to customize behavior when the operation starts
     */
    // tg: code/modules/surgery/operations/_operation.dm:1127
    public virtual async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        // PROTECTED_PROC(TRUE)
        mob_living patient = get_patient(operating_on) as mob_living;
        atom_movable display_target = (atom_movable)patient ?? operating_on;
        display_results(
            surgeon,
            patient,
            span_notice(I18n.T("You begin to operate on []...", display_target)),
            span_notice(I18n.T("[] begins to operate on [].", surgeon, display_target)),
            span_notice(I18n.T("[] begins to operate on [].", surgeon, display_target)));
    }

    /**
     * Called when the operation is successful
     * Don't touch this proc, override on_success() instead
     */
    // tg: code/modules/surgery/operations/_operation.dm:1143
    public virtual void success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        // SHOULD_NOT_OVERRIDE(TRUE) PRIVATE_PROC(TRUE) SHOULD_NOT_SLEEP(TRUE)

        if (Dm.truthy(Dm.bitand(operation_flags, OPERATION_NOTABLE)))
        {
            SSblackbox.record_feedback("tally", "surgeries_completed", 1, type);
            surgeon.add_mob_memory(typeof(datum_memory_surgery), Dm.named("deuteragonist", get_patient(operating_on) is var or__1 && Dm.truthy(or__1) ? or__1 : operating_on), Dm.named("surgery_type", name));
        }

        // SEND_SIGNAL(surgeon, COMSIG_ATOM_SURGERY_SUCCESS, src, operating_on, tool)
        if (Dm.truthy(surgeon._listen_lookup?[COMSIG_ATOM_SURGERY_SUCCESS]))
            surgeon._SendSignal(COMSIG_ATOM_SURGERY_SUCCESS, Dm.list(surgeon, this, operating_on, tool));
        play_operation_sound(operating_on, surgeon, tool, success_sound);
        Waiting.NoSleep(on_success(operating_on, surgeon, tool, operation_args));
    }

    /**
     * Used to customize behavior when the operation is successful
     */
    // tg: code/modules/surgery/operations/_operation.dm:1159
    public virtual async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        // PROTECTED_PROC(TRUE)
        mob_living patient = get_patient(operating_on) as mob_living;

        display_results(
            surgeon,
            patient,
            span_notice(I18n.T("You succeed.")),
            span_notice(I18n.T("[] succeeds!", surgeon)),
            span_notice(I18n.T("[] finishes.", surgeon)));
        return null;
    }

    /**
     * Called when the operation fails
     * Don't touch this proc, override on_failure() instead
     */
    // tg: code/modules/surgery/operations/_operation.dm:1175
    public virtual void failure(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        // SHOULD_NOT_OVERRIDE(TRUE) PRIVATE_PROC(TRUE) SHOULD_NOT_SLEEP(TRUE)

        if (Dm.truthy(Dm.bitand(operation_flags, OPERATION_NOTABLE)))
            SSblackbox.record_feedback("tally", "surgeries_failed", 1, type);

        // SEND_SIGNAL(surgeon, COMSIG_ATOM_SURGERY_FAILED, src, operating_on, tool)
        if (Dm.truthy(surgeon._listen_lookup?[COMSIG_ATOM_SURGERY_FAILED]))
            surgeon._SendSignal(COMSIG_ATOM_SURGERY_FAILED, Dm.list(surgeon, this, operating_on, tool));
        play_operation_sound(operating_on, surgeon, tool, failure_sound);
        Waiting.NoSleep(on_failure(operating_on, surgeon, tool, operation_args));
    }

    /**
     * Used to customize behavior when the operation fails
     *
     * total_penalty_modifier is the final modifier applied to the time taken to perform the operation,
     * and it can be interpreted as how badly the operation was performed
     *
     * At its lowest, it will be just above 2.5 (the threshold for success), and can go up to infinity (theoretically)
     */
    // tg: code/modules/surgery/operations/_operation.dm:1195
    public virtual async DmTask on_failure(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        // PROTECTED_PROC(TRUE)
        mob_living patient = get_patient(operating_on) as mob_living;

        string screwedmessage = "";
        float sw__1 = Dm.num(operation_args[OPERATION_SPEED]);
        if (sw__1 >= 2.5f && sw__1 <= 3)
            screwedmessage = I18n.T(" You almost had it, though.");
        else if (sw__1 >= 3 && sw__1 <= 4)
            pass();
        else if (sw__1 >= 4 && sw__1 <= 5)
            screwedmessage = I18n.T(" This is hard to get right in these conditions...");
        else if (sw__1 >= 5)
            screwedmessage = I18n.T(" This is practically impossible in these conditions...");
        if (Dm.truthy(operation_args[OPERATION_FORCE_FAIL]))
            screwedmessage = I18n.T(" Intentionally.");

        display_results(
            surgeon,
            patient,
            span_warning(I18n.T("You screw up![]", screwedmessage)),
            span_warning(I18n.T("[] screws up!", surgeon)),
            span_notice(I18n.T("[] finishes.", surgeon)),
            TRUE); //By default the patient will notice if the wrong thing has been cut
    }
}

/**
 * Basic operations are a simple base type for surgeries that
 * 1. Target a specific zone on humans
 * 2. Work on non-humans
 *
 * Use this as a bsae if your surgery needs to work on everyone
 *
 * "operating_on" is the mob being operated on, be it carbon or non-carbon.
 * If the mob is carbon, we check the relevant bodypart for surgery states and traits. No bodypart, no operation.
 * If the mob is non-carbon, we just check the mob directly.
 */
// tg: code/modules/surgery/operations/_operation.dm:1233
public partial class datum_surgery_operation_basic : datum_surgery_operation
{
    // tg: code/modules/surgery/operations/_operation.dm:1241
    public override object all_required_strings()
    {
        object dot = Dm.list();
        if (Dm.truthy(required_biotype))
            Dm.append(ref dot, Dm.truthy(target_zone) ? I18n.T("operate on [] (target [])", parse_zone(target_zone), parse_zone(target_zone)) : I18n.T("operate on patient"));
        else if (Dm.truthy(target_zone))
            Dm.append(ref dot, I18n.T("operate on [] (target [])", parse_zone(target_zone), parse_zone(target_zone)));
        Dm.append(ref dot, base.all_required_strings());
        return dot;
    }

    // tg: code/modules/surgery/operations/_operation.dm:1249
    public override object all_blocked_strings()
    {
        object dot = base.all_blocked_strings();
        if (Dm.truthy(Dm.bitand(required_biotype, MOB_ROBOTIC)))
            Dm.append(ref dot, I18n.T("the patient must not be organic"));
        else if (Dm.truthy(required_biotype))
            Dm.append(ref dot, I18n.T("the patient must not be robotic"));
        return dot;
    }

    // tg: code/modules/surgery/operations/_operation.dm:1256
    public override object is_available(atom_movable operating_on = null, object operated_zone = null)
    {
        // SHOULD_NOT_OVERRIDE(TRUE)
        mob_living patient = (mob_living)operating_on;

        if (Dm.truthy(target_zone) && !Dm.eq(target_zone, operated_zone))
            return FALSE;
        if (!HAS_TRAIT(patient, TRAIT_READY_TO_OPERATE))
            return FALSE;
        if (Dm.truthy(required_biotype) && !Dm.truthy(Dm.bitand(patient.mob_biotypes, required_biotype)))
            return FALSE;
        if (!Dm.truthy(patient.has_limbs) || !Dm.truthy(target_zone))
            return base.is_available(operating_on, operated_zone);

        obj_item_bodypart carbon_part = patient.get_bodypart(target_zone) as obj_item_bodypart;
        if (carbon_part == null)
            return FALSE;
        if (!HAS_TRAIT(carbon_part, TRAIT_READY_TO_OPERATE))
            return FALSE;
        if (Dm.truthy(required_bodytype) && !Dm.truthy(Dm.bitand(carbon_part.bodytype, required_bodytype)))
            return FALSE;
        return base.is_available(operating_on, operated_zone);
    }

    // tg: code/modules/surgery/operations/_operation.dm:1277
    public override object has_surgery_state(atom_movable operating_on = null, object state = null)
    {
        mob_living patient = (mob_living)operating_on;
        obj_item_bodypart carbon_part = patient.get_bodypart(target_zone) as obj_item_bodypart;
        if (carbon_part == null) // non-carbon
        {
            datum_status_effect_basic_surgery_state state_holder = patient.has_status_effect(typeof(datum_status_effect_basic_surgery_state)) as datum_status_effect_basic_surgery_state;
            return Dm.num(HAS_SURGERY_STATE(Dm.num(state_holder?.surgery_state), Dm.bitand(Dm.num(state), Dm.bitor(SURGERY_BONE_SAWED, SURGERY_SKIN_OPEN)))); // these are the only states basic mobs support, update this if that changes
        }

        return Dm.num(LIMB_HAS_SURGERY_STATE(carbon_part, Dm.num(state)));
    }

    // tg: code/modules/surgery/operations/_operation.dm:1285
    public override object has_any_surgery_state(atom_movable operating_on = null, object state = null)
    {
        mob_living patient = (mob_living)operating_on;
        obj_item_bodypart carbon_part = patient.get_bodypart(target_zone) as obj_item_bodypart;
        if (carbon_part == null) // non-carbon
        {
            datum_status_effect_basic_surgery_state state_holder = patient.has_status_effect(typeof(datum_status_effect_basic_surgery_state)) as datum_status_effect_basic_surgery_state;
            return HAS_ANY_SURGERY_STATE(Dm.num(state_holder?.surgery_state), Dm.num(state));
        }

        return LIMB_HAS_ANY_SURGERY_STATE(carbon_part, Dm.num(state));
    }
}

/**
 * Limb opterations are a base focused on the limb the surgeon is targeting
 *
 * Use this if your surgery targets a specific limb on the mob
 *
 * "operating_on" is asserted to be a bodypart - the bodypart the surgeon is targeting.
 * If there is no bodypart, there's no operation.
 */
// tg: code/modules/surgery/operations/_operation.dm:1302
public partial class datum_surgery_operation_limb : datum_surgery_operation
{
    // tg: code/modules/surgery/operations/_operation.dm:1309
    public override object all_blocked_strings()
    {
        object dot = base.all_blocked_strings();
        if (Dm.truthy(Dm.bitand(Dm.num(required_bodytype), BODYTYPE_ROBOTIC)))
            Dm.append(ref dot, I18n.T("the limb must not be organic"));
        else if (Dm.truthy(Dm.bitand(Dm.num(required_bodytype), BODYTYPE_ORGANIC)))
            Dm.append(ref dot, I18n.T("the limb must not be cybernetic"));
        return dot;
    }

    // tg: code/modules/surgery/operations/_operation.dm:1316
    public override object get_operation_target(atom_movable operating_on = null, object body_zone = null)
    {
        if (isliving(operating_on))
        {
            mob_living patient = (mob_living)operating_on;
            return patient.get_bodypart((string)deprecise_zone(body_zone), allow_stumps);
        }
        if (!isbodypart(operating_on))
            return null;
        return operating_on;
    }

    // tg: code/modules/surgery/operations/_operation.dm:1324
    public override object is_available(atom_movable operating_on = null, object operated_zone = null)
    {
        // SHOULD_NOT_OVERRIDE(TRUE)
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;

        // targeting groin will redirect you to the chest
        if (!Dm.eq(limb.body_zone, deprecise_zone(operated_zone)))
            return FALSE;

        if (Dm.truthy(required_bodytype) && !Dm.truthy(Dm.bitand(limb.bodytype, Dm.num(required_bodytype))))
            return FALSE;

        return base.is_available(operating_on, operated_zone);
    }

    // tg: code/modules/surgery/operations/_operation.dm:1336
    public override object has_surgery_state(atom_movable operating_on = null, object state = null)
    {
        return Dm.num(LIMB_HAS_SURGERY_STATE(operating_on, Dm.num(state)));
    }

    // tg: code/modules/surgery/operations/_operation.dm:1339
    public override object has_any_surgery_state(atom_movable operating_on = null, object state = null)
    {
        return LIMB_HAS_ANY_SURGERY_STATE(operating_on, Dm.num(state));
    }

    // tg: code/modules/surgery/operations/_operation.dm:1342
    public override object get_patient(atom_movable operating_on = null)
    {
        return ((obj_item_bodypart)operating_on).owner;
    }
}

/**
 * Organ operations are a base focused on a specific organ typepath
 *
 * Use this if your surgery targets a specific organ type
 *
 * "operating_on" is asserted to be an organ of the type defined by target_type.
 * No organ of that type, no operation.
 */
// tg: code/modules/surgery/operations/_operation.dm:1354
public partial class datum_surgery_operation_organ : datum_surgery_operation
{
    // tg: code/modules/surgery/operations/_operation.dm:1360
    public override object all_required_strings()
    {
        object dot = Dm.list(I18n.T("operate on [] (target [])", Dm.initial(target_type, (obj_item_organ o) => o.name), Dm.initial(target_type, (obj_item_organ o) => o.zone)));
        Dm.append(ref dot, base.all_required_strings());
        return dot;
    }

    // tg: code/modules/surgery/operations/_operation.dm:1363
    public override object all_blocked_strings()
    {
        object dot = base.all_blocked_strings();
        if (Dm.truthy(Dm.bitand(required_organ_flag, BODYTYPE_ROBOTIC)))
            Dm.append(ref dot, I18n.T("the organ must not be organic"));
        else if (Dm.truthy(Dm.bitand(required_organ_flag, ORGAN_TYPE_FLAGS)))
            Dm.append(ref dot, I18n.T("the organ must not be cybernetic"));
        return dot;
    }

    // tg: code/modules/surgery/operations/_operation.dm:1370
    public override async DmTask<object> get_default_radial_image()
    {
        return await get_generic_limb_radial_image(Dm.initial(target_type, (obj_item_organ o) => o.zone));
    }

    // tg: code/modules/surgery/operations/_operation.dm:1373
    public override object get_operation_target(atom_movable operating_on = null, object body_zone = null)
    {
        if (isliving(operating_on))
        {
            mob_living patient = (mob_living)operating_on;
            return patient.get_organ_by_type(target_type);
        }

        if (!isbodypart(operating_on))
            return null;

        return Dm.locate_type(target_type, operating_on);
    }

    // tg: code/modules/surgery/operations/_operation.dm:1383
    public override object get_patient(atom_movable operating_on = null)
    {
        return ((obj_item_organ)operating_on).owner;
    }

    // tg: code/modules/surgery/operations/_operation.dm:1386
    public override object is_available(atom_movable operating_on = null, object operated_zone = null)
    {
        // SHOULD_NOT_OVERRIDE(TRUE)
        obj_item_organ organ = (obj_item_organ)operating_on;

        if (!Dm.eq(organ.zone, operated_zone)) // this check prevents eyes from showing up in head operations
            return FALSE;
        if (Dm.truthy(required_organ_flag) && !Dm.truthy(Dm.bitand(organ.organ_flags, required_organ_flag)))
            return FALSE;
        if (!HAS_TRAIT(organ.bodypart_owner, TRAIT_READY_TO_OPERATE))
            return FALSE;

        return base.is_available(operating_on, operated_zone);
    }

    // tg: code/modules/surgery/operations/_operation.dm:1398
    public override object has_surgery_state(atom_movable operating_on = null, object state = null)
    {
        return Dm.num(LIMB_HAS_SURGERY_STATE(((obj_item_organ)operating_on).bodypart_owner, Dm.num(state)));
    }

    // tg: code/modules/surgery/operations/_operation.dm:1401
    public override object has_any_surgery_state(atom_movable operating_on = null, object state = null)
    {
        return LIMB_HAS_ANY_SURGERY_STATE(((obj_item_organ)operating_on).bodypart_owner, Dm.num(state));
    }
}

public sealed partial class TgState
{
    // tg: code/modules/surgery/operations/_operation.dm:787  /datum/surgery_operation/proc/get_location_modifier var/static/list/modifiers
    internal DmList datum_surgery_operation__get_location_modifier__modifiers;
}
