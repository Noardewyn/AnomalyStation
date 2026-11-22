// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/frozen.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10x closing: the whole file (an object on a turf that freezing water vapor freezes, freeze_turf; reached by the M4b scenario's
// decompressed room).

// tg: code/datums/elements/frozen.dm:1
public partial class datum_controller_global_vars
{
    // tg: code/datums/elements/frozen.dm:1  GLOBAL_LIST_INIT(freon_color_matrix, list("#2E5E69", "#60A2A8", "#A1AFB1", rgb(0,0,0)))
    public DmList freon_color_matrix;

    // tg: code/datums/elements/frozen.dm:1  (GLOBAL_MANAGED, __DEFINES/__globals.dm:5)
    public virtual void InitGlobalfreon_color_matrix()
    {
        freon_color_matrix = Dm.list("#2E5E69", "#60A2A8", "#A1AFB1", rgb(0, 0, 0));
        Dm.append(ref gvars_datum_init_order, "freon_color_matrix");
    }
}

///simple element to handle frozen obj's
// tg: code/datums/elements/frozen.dm:4
public partial class datum_element_frozen : datum_element
{
    // tg: code/datums/elements/frozen.dm:6
    [DmParams("target")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isobj(target))
            return ELEMENT_INCOMPATIBLE;

        obj target_obj = (obj)target;
        if (Dm.truthy(Dm.bitand(target_obj.resistance_flags, FREEZE_PROOF)))
            return ELEMENT_INCOMPATIBLE;

        if (HAS_TRAIT(target_obj, TRAIT_FROZEN))
            return ELEMENT_INCOMPATIBLE;

        ADD_TRAIT(target_obj, TRAIT_FROZEN, ELEMENT_TRAIT(type));
        target_obj.name = Dm.text("frozen []", target_obj.name);
        await target_obj.add_atom_colour(GLOB.freon_color_matrix, TEMPORARY_COLOUR_PRIORITY);
        target_obj.alpha -= 25;

        if (isorgan(target))
        {
            obj_item_organ organ = (obj_item_organ)target;
            organ.organ_flags = Dm.bitor(organ.organ_flags, ORGAN_FROZEN);
        }
        else if (isbodypart(target))
        {
            foreach (obj_item_organ organ in Dm.each<obj_item_organ>(target_obj.contents))
                organ.organ_flags = Dm.bitor(organ.organ_flags, ORGAN_FROZEN);
        }

        RegisterSignal(target, COMSIG_MOVABLE_MOVED, nameof(on_moved));
        RegisterSignal(target, COMSIG_MOVABLE_THROW_LANDED, nameof(shatter_on_landed));
        RegisterSignal(target, COMSIG_MOVABLE_IMPACT, nameof(shatter_on_throw));
        RegisterSignal(target, COMSIG_OBJ_UNFREEZE, nameof(on_unfreeze));
        return dot;
    }

    // tg: code/datums/elements/frozen.dm:35
    [DmParams("source")]
    public override object Detach(datum source = null, params object[] __extra)
    {
        obj obj_source = (obj)source;
        REMOVE_TRAIT(obj_source, TRAIT_FROZEN, ELEMENT_TRAIT(type));
        UnregisterSignal(obj_source, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_MOVABLE_THROW_LANDED, COMSIG_MOVABLE_IMPACT, COMSIG_OBJ_UNFREEZE));
        obj_source.name = replacetext(obj_source.name, "frozen ", "");
        Waiting.NoSleep(obj_source.remove_atom_colour(TEMPORARY_COLOUR_PRIORITY, GLOB.freon_color_matrix));
        obj_source.alpha += 25;

        if (isorgan(source))
        {
            obj_item_organ organ = (obj_item_organ)source;
            organ.organ_flags = Dm.bitand(organ.organ_flags, ~ORGAN_FROZEN);
        }
        else if (isbodypart(source))
        {
            foreach (obj_item_organ organ in Dm.each<obj_item_organ>(obj_source.contents))
                organ.organ_flags = Dm.bitand(organ.organ_flags, ~ORGAN_FROZEN);
        }

        return base.Detach(source, __extra);
    }

    ///signal handler for COMSIG_OBJ_UNFREEZE that forces us to detach from the target
    // tg: code/datums/elements/frozen.dm:52
    [SignalHandler]
    public virtual void on_unfreeze(datum source = null)
    {
        Detach(source);
    }

    // tg: code/datums/elements/frozen.dm:56
    [SignalHandler]
    public virtual void shatter_on_throw(datum source = null, atom hit_atom = null, datum_thrownthing throwing_datum = null, object caught = null)
    {
        if (!Dm.truthy(caught))
            shatter_on_landed(source, throwing_datum);
    }

    ///signal handler that shatters our target after impacting after a throw.
    // tg: code/datums/elements/frozen.dm:62
    [SignalHandler]
    public virtual void shatter_on_landed(datum target = null, datum_thrownthing throwingdatum = null)
    {
        obj obj_target = (obj)target;
        // thrower is a weakref (throwing.dm:61), so ismob() is always false in tg too; the log names what it resolves to
        if (ismob(throwingdatum.thrower))
            log_combat(throwingdatum.thrower.resolve() as atom, target as atom, "shattered", addition: Dm.text("from being thrown due to [] being frozen.", target));
        else
            log_combat(throwingdatum.thrower?.resolve() as atom, target as atom, "launched", addition: "shattering it due to being frozen.");
        obj_target.visible_message(span_danger(I18n.T("[] shatters into a million pieces!", obj_target)));
        obj_target.obj_flags = Dm.bitor(obj_target.obj_flags, NO_DEBRIS_AFTER_DECONSTRUCTION); // disable item spawning
        Waiting.NoSleep(obj_target.deconstruct(FALSE)); // call pre-deletion specialized code -- internals release gas etc
    }

    /// signal handler for COMSIG_MOVABLE_MOVED that unfreezes our target if it moves onto an open turf thats hotter than
    /// our melting temperature.
    // tg: code/datums/elements/frozen.dm:76
    [SignalHandler]
    public virtual void on_moved(datum target = null)
    {
        atom_movable movable_target = (atom_movable)target;

        if (Dm.truthy(movable_target.throwing))
            return;

        if (!isopenturf(movable_target.loc))
            return;

        turf_open turf_loc = (turf_open)movable_target.loc;
        if (turf_loc.air?.temperature >= T0C) //unfreezes target
            Detach(target);
    }
}
