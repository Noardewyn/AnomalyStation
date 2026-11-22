// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/food/processable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): slicing, flattening and the other tool processing of food. The var block is hand-written:
// tg's `var/atom/result_atom_type` holds the typepath the element is given (rules 3.2 V3), which the generator would type as an atom.

// If an item has the processable item, it can be processed into another item with a specific tool. This adds generic behaviour for those actions to make it easier to set-up generically.
// tg: code/datums/elements/food/processable.dm:2
public partial class datum_element_processable : datum_element
{
    ///The type of atom this creates when the processing recipe is used.
    // tg: code/datums/elements/food/processable.dm:6
    public Type result_atom_type;

    // tg: code/datums/elements/food/processable.dm:2 (the type's own var defaults: a hand-written var block, types-converter.md 3.2)
    public datum_element_processable()
    {
        element_flags = ELEMENT_BESPOKE;
        argument_hash_start_idx = 2;
    }

    // tg: code/datums/elements/food/processable.dm:20
    [DmParams("target", "tool_behaviour", "result_atom_type", "amount_created", "time_to_process", "table_required", "screentip_verb", "sound_to_play")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object tool_behaviour = Dm.arg(__extra, 0);
        Type result_atom_type = Dm.arg(__extra, 1) as Type;
        object amount_created = Dm.arg(__extra, 2) ?? 3;
        object time_to_process = Dm.arg(__extra, 3) ?? 2 * SECONDS;
        object table_required = Dm.arg(__extra, 4) ?? FALSE;
        object screentip_verb = Dm.arg(__extra, 5) ?? "Process";
        object sound_to_play = Dm.arg(__extra, 6);
        object dot = await base.Attach(target, __extra);
        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;

        this.tool_behaviour = tool_behaviour;
        this.amount_created = amount_created;
        this.time_to_process = time_to_process;
        this.result_atom_type = result_atom_type;
        this.table_required = table_required;
        this.screentip_verb = screentip_verb;
        this.sound_to_play = sound_to_play;

        atom atom_target = (atom)target;
        Dm.combine(ref atom_target.flags_1, HAS_CONTEXTUAL_SCREENTIPS_1);

        RegisterSignal(atom_target, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM, nameof(on_requesting_context_from_item));
        RegisterSignal(target, COMSIG_ATOM_TOOL_ACT(tool_behaviour), nameof(try_process));
        RegisterSignal(target, COMSIG_ATOM_EXAMINE, nameof(OnExamine));

        // PERFORM_ALL_TESTS(focus_only/check_materials_when_processed) is FALSE in production (rules 1.7)
        if (!Dm.truthy(FALSE) || !Dm.truthy(atom_target.custom_materials))
            return dot;

        atom_movable prototype = Dm.@new<atom_movable>();
        await prototype.set_custom_materials(atom_target.custom_materials, 1 / Dm.num(amount_created));
        atom_movable result = Dm.@new(result_atom_type) as atom_movable;
        if (!Dm.truthy(prototype.compare_materials(result)))
        {
            string warning = Dm.text("custom_materials of [] when processed compared to just spawned don't match", result.type);
            object what_it_should_be = prototype.transcribe_materials_list();
            //compose a text string containing the syntax and paths to use for editing the custom_materials var
            if (Dm.truthy(result.custom_materials))
                what_it_should_be = Dm.add(what_it_should_be, " (you can round values a bit)");
            stack_trace(Dm.text("[]. should be: custom_materials = [] (you can round values a bit).", warning, what_it_should_be), "code/datums/elements/food/processable.dm", 52);
        }
        qdel(prototype);
        qdel(result);
        return dot;
    }

    // tg: code/datums/elements/food/processable.dm:56
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, Dm.list(COMSIG_ATOM_TOOL_ACT(tool_behaviour), COMSIG_ATOM_EXAMINE, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM));
        return dot;
    }

    // tg: code/datums/elements/food/processable.dm:60
    [SignalHandler]
    public virtual void try_process(datum source = null, mob_living user = null, obj_item I = null, DmList mutable_recipes = null)
    {
        if (Dm.truthy(table_required))
        {
            obj_item found_item = (obj_item)source;
            object found_location = found_item.loc;
            bool found_turf = isturf(found_location);
            obj_structure_table found_table = Dm.locate<obj_structure_table>(found_location);
            // DM parse: locate(tray) in (found_location || (locate(oven_tray) in found_location)); only the tray is looked for (rules 4.11).
            obj_item_storage_bag_tray found_tray = Dm.locate<obj_item_storage_bag_tray>(found_location is var or__1 && Dm.truthy(or__1) ? or__1 : (object)Dm.locate<obj_item_plate_oven_tray>(found_location));
            if (!found_turf && found_location is not obj_item_storage_bag_tray || found_turf && !(found_table != null || found_tray != null))
            {
                to_chat(user, span_notice(I18n.T("You cannot make [] here! You need a table or at least a tray.", Dm.initial(result_atom_type, (atom a) => a.name))));
                return;
            }
        }

        Dm.append(ref mutable_recipes, Dm.list(Dm.list(Dm.kv(TOOL_PROCESSING_RESULT, result_atom_type), Dm.kv(TOOL_PROCESSING_AMOUNT, amount_created), Dm.kv(TOOL_PROCESSING_TIME, time_to_process), Dm.kv(TOOL_PROCESSING_SOUND, sound_to_play))));
    }

    ///So people know what the frick they're doing without reading from a wiki page (I mean they will inevitably but i'm trying to help, ok?)
    // tg: code/datums/elements/food/processable.dm:76
    [SignalHandler]
    public virtual void OnExamine(atom source = null, mob user = null, DmList examine_list = null)
    {
        string result_name = Dm.initial(result_atom_type, (atom a) => a.name);
        object result_gender = Dm.initial(result_atom_type, (atom a) => a.gender);
        string tool_desc = tool_behaviour_name(tool_behaviour);

        // I admit, this is a lot of lines for very minor changes in the strings
        // but at least it's readable?
        if (Dm.num(amount_created) > 1)
        {
            if (Dm.eq(result_gender, PLURAL))
                Dm.append(ref examine_list, span_notice(I18n.T("It can be turned into [] [] with []!", amount_created, result_name, span_bold(tool_desc))));
            else
                Dm.append(ref examine_list, span_notice(I18n.T("It can be turned into [] [][] with []!", amount_created, result_name, plural_s(result_name), span_bold(tool_desc))));
        }
        else
        {
            if (Dm.eq(result_gender, PLURAL))
                Dm.append(ref examine_list, span_notice(I18n.T("It can be turned into some [] with []!", result_name, span_bold(tool_desc))));
            else
                Dm.append(ref examine_list, span_notice(I18n.T("It can be turned into \\a [] with []!", result_name, span_bold(tool_desc))));
        }
    }

    /**
     * Adds context sensitivy directly to the processable file for screentips
     * Arguments:
     * * source - refers to item that will display its screentip
     * * context - refers to, in this case, an item that can be proccessed into another item via add element proccessable
     * * held_item - refers to tool used by add element proccessable to process one item into another item
     * * user - refers to user who will see the screentip when the proper context and tool are there
     */
    // tg: code/datums/elements/food/processable.dm:105
    [SignalHandler]
    public virtual object on_requesting_context_from_item(datum source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (held_item == null)
            return NONE;

        if (!Dm.eq(held_item.tool_behaviour, tool_behaviour))
            return NONE;

        context[SCREENTIP_CONTEXT_LMB] = Dm.text("[] into []", screentip_verb, Dm.initial(result_atom_type, (atom a) => a.name));

        return CONTEXTUAL_SCREENTIP_SET;
    }
}
