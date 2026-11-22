// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/dryable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): drying on the drying rack. The var block is generated (D111); finish_drying's static
// dried_color is a TgState field (rules 10.6).

// If an item has this element, it can be dried on a drying rack.
// tg: code/datums/elements/dryable.dm:2
public partial class datum_element_dryable : datum_element
{
    // tg: code/datums/elements/dryable.dm:8
    [DmParams("target", "dry_result")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dry_result = Dm.arg(__extra, 0);
        object dot = await base.Attach(target, __extra);
        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;
        this.dry_result = dry_result;

        RegisterSignal(target, COMSIG_ITEM_FINISH_DRYING, nameof(finish_drying));
        ADD_TRAIT(target, TRAIT_DRYABLE, ELEMENT_TRAIT(type));

        atom atom_target = (atom)target;
        // PERFORM_ALL_TESTS(focus_only/check_materials_when_processed) is FALSE in production (rules 1.7)
        if (!Dm.truthy(FALSE) || !Dm.truthy(atom_target.custom_materials) || !Dm.truthy(dry_result) || isstack(atom_target))
            return dot;

        atom result = Dm.@new((Type)dry_result) as atom;
        if (!Dm.truthy(atom_target.compare_materials(result)))
        {
            string warning = Dm.text("custom_materials of [] when dried compared to just spawned don't match", result.type);
            object what_it_should_be = atom_target.transcribe_materials_list();
            stack_trace(Dm.text("[]. should be: custom_materials = [].", warning, what_it_should_be), "code/datums/elements/dryable.dm", 25);
        }
        qdel(result);
        return dot;
    }

    // tg: code/datums/elements/dryable.dm:29
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, COMSIG_ITEM_FINISH_DRYING);
        REMOVE_TRAIT(source, TRAIT_DRYABLE, ELEMENT_TRAIT(type));
        return dot;
    }

    // tg: code/datums/elements/dryable.dm:36  var/static/list/dried_color (rules 10.6)
    private static ref object finish_drying__dried_color => ref TgState.Current.datum_element_dryable__finish_drying__dried_color;

    // tg: code/datums/elements/dryable.dm:34
    [SignalHandler]
    public virtual void finish_drying(atom source = null, datum_weakref drying_user = null)
    {
        if (finish_drying__dried_color == null)
            finish_drying__dried_color = color_transition_filter(COLOR_DRIED_TAN);
        atom dried_atom = source;
        if (Dm.eq(dry_result, dried_atom.type))//if the dried type is the same as our currrent state, don't bother creating a whole new item, just re-color it.
        {
            atom_movable resulting_atom__1 = (atom_movable)dried_atom;
            Waiting.NoSleep(resulting_atom__1.add_atom_colour(finish_drying__dried_color, FIXED_COLOUR_PRIORITY));
            apply_dried_status(resulting_atom__1, drying_user, source);
            return;
        }
        else if (isstack(source)) //Check if its a sheet
        {
            obj_item_stack itemstack = (obj_item_stack)dried_atom;
            for (float i = 1, i__end = Dm.num(itemstack.amount); i <= i__end; i++)
            {
                atom_movable resulting_atom__2 = Dm.@new((Type)dry_result, source.loc) as atom_movable;
                apply_dried_status(resulting_atom__2, drying_user);
            }
            qdel(source);
            return;
        }

        obj_item_food resulting_atom = Dm.@new((Type)dry_result, source.loc) as obj_item_food;
        if (source is obj_item_food && ispath(dry_result, typeof(obj_item_food)))
        {
            obj_item_food source_food = (obj_item_food)source;
            Waiting.NoSleep(((datum_reagents)resulting_atom.reagents).clear_reagents());
            Waiting.NoSleep(((datum_reagents)source_food.reagents).trans_to(resulting_atom, ((datum_reagents)source_food.reagents).total_volume));
        }
        Waiting.NoSleep(resulting_atom.set_custom_materials(source.custom_materials));
        apply_dried_status(resulting_atom, drying_user, source);

        qdel(source);
    }

    // tg: code/datums/elements/dryable.dm:63
    public virtual void apply_dried_status(atom target = null, datum_weakref drying_user = null, atom source = null)
    {
        ADD_TRAIT(target, TRAIT_DRIED, ELEMENT_TRAIT(type));
        datum_mind user_mind = drying_user?.resolve() as datum_mind;
        if (drying_user != null && target is obj_item_food)
            ADD_TRAIT(target, TRAIT_HANDMADE, REF(user_mind));

        // The stack branch of finish_drying passes no source: DM's runtime there ends only this proc and the loop goes on (rules 9.1),
        // so the null source sends nothing here.
        if (Dm.truthy(source?._listen_lookup?[COMSIG_ITEM_DRIED]))
            source._SendSignal(COMSIG_ITEM_DRIED, Dm.list(source, target, drying_user));
    }
}

public sealed partial class TgState
{
    // tg: code/datums/elements/dryable.dm:36  finish_drying's var/static/list/dried_color
    internal object datum_element_dryable__finish_drying__dried_color;
}
