// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/aquarium_content.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: what lets a fish or a prop be put in an aquarium and shown in it (the fish carries it from its Initialize): its
// visual, an /obj/effect/aquarium in the aquarium's vis_contents, which the fish or the prop draws and animates. The var blocks are
// generated (D111).

/// Allows movables to be inserted/displayed in aquariums.
// tg: code/datums/components/aquarium_content.dm:2
public partial class datum_component_aquarium_content : datum_component
{
    // tg: code/datums/components/aquarium_content.dm:25
    [DmParams("animation_update_signals")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object animation_update_signals = Dm.arg(__extra, 0);
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;

        this.animation_update_signals = islist(animation_update_signals) ? (DmList)animation_update_signals : Dm.list(animation_update_signals);

        ADD_TRAIT(parent, TRAIT_AQUARIUM_CONTENT, REF(this));
        RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(enter_aquarium));

        //If component is added to something already in aquarium at the time initialize it properly.
        atom_movable movable_parent = parent as atom_movable;
        if (movable_parent.loc != null && HAS_TRAIT(movable_parent.loc, TRAIT_IS_AQUARIUM))
            on_inserted(movable_parent.loc as atom_movable);
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/datums/components/aquarium_content.dm:39
    public override object Destroy(float? force = null)
    {
        atom_movable movable = parent as atom_movable;
        if (movable.loc != null && HAS_TRAIT(movable.loc, TRAIT_IS_AQUARIUM))
            remove_from_aquarium(movable.loc as atom_movable);
        QDEL_NULL(ref vc_obj);
        REMOVE_TRAIT(parent, TRAIT_AQUARIUM_CONTENT, REF(this));
        return base.Destroy(force);
    }

    // tg: code/datums/components/aquarium_content.dm:47
    [SignalHandler]
    public virtual void enter_aquarium(datum source = null, object OldLoc = null, object Dir = null, object Forced = null)
    {
        atom_movable movable_parent = parent as atom_movable;
        if (HAS_TRAIT(movable_parent.loc, TRAIT_IS_AQUARIUM))
            on_inserted(movable_parent.loc as atom_movable);
    }

    // tg: code/datums/components/aquarium_content.dm:53
    public virtual void on_inserted(atom_movable aquarium = null)
    {
        RegisterSignal(aquarium, COMSIG_ATOM_EXITED, nameof(on_removed));
        RegisterSignal(aquarium, COMSIG_AQUARIUM_FLUID_CHANGED, nameof(on_fluid_changed));
        RegisterSignals(aquarium, animation_update_signals, nameof(animation_update_signal_proc));

        if (Dm.truthy(processing))
            START_PROCESSING(SSobj, this);

        //If we don't have vc object yet build it
        if (vc_obj == null)
            generate_base_vc(aquarium);

        //Set default position and layer
        set_vc_base_position();
        generate_animation(reset: TRUE);

        //Finally add it to to objects vis_contents
        Dm.combine(aquarium.vis_contents, vc_obj);
    }

    // tg: code/datums/components/aquarium_content.dm:72
    [SignalHandler]
    public virtual void on_fluid_changed(datum source = null, object new_fluid_type = null)
    {
        vc_obj.fluid_type = new_fluid_type;
        generate_animation();
    }

    ///Called when one of the signals in the 'animation_update_signals' is sent
    // tg: code/datums/components/aquarium_content.dm:78
    public virtual void animation_update_signal_proc(datum source = null)
    {
        generate_animation();
    }

    ///Sends a signal to the parent to get them to update the aquarium animation of the visual object
    // tg: code/datums/components/aquarium_content.dm:82
    public virtual void generate_animation(float? reset = null)
    {
        reset ??= FALSE;
        atom_movable movable = parent as atom_movable;
        if (Dm.truthy(movable._listen_lookup?[COMSIG_AQUARIUM_CONTENT_DO_ANIMATION]))
            movable._SendSignal(COMSIG_AQUARIUM_CONTENT_DO_ANIMATION, Dm.list(movable, Dm.truthy(reset) ? null : current_animation, vc_obj));
    }

    /// Generates common visual object, propeties that don't depend on aquarium surface
    // tg: code/datums/components/aquarium_content.dm:87
    public virtual void generate_base_vc(atom_movable aquarium = null)
    {
        vc_obj = Dm.@new<obj_effect_aquarium>();
        vc_obj.vis_flags = Dm.bitor(vc_obj.vis_flags, Dm.bitor(VIS_INHERIT_ID, VIS_INHERIT_PLANE)); //plane so it shows properly in containers on inventory ui for handheld cases
        update_vc_color(parent as atom_movable);
        RegisterSignal(parent, COMSIG_ATOM_COLOR_UPDATED, nameof(update_vc_color));
        if (Dm.truthy(parent._listen_lookup?[COMSIG_AQUARIUM_CONTENT_GENERATE_APPEARANCE]))
            parent._SendSignal(COMSIG_AQUARIUM_CONTENT_GENERATE_APPEARANCE, Dm.list(parent, vc_obj, aquarium));
    }

    // tg: code/datums/components/aquarium_content.dm:94
    [SignalHandler]
    public virtual void update_vc_color(atom_movable source = null)
    {
        object color_to_use = source.color;
        if (!Dm.truthy(color_to_use) && isfish(source)) //Small bit of snowflake code for those fish overlays that are greyscale.
        {
            obj_item_fish fish = source as obj_item_fish;
            color_to_use = fish.aquarium_vc_color;
        }
        vc_obj.color = color_to_use;
    }

    // tg: code/datums/components/aquarium_content.dm:102
    public virtual void set_vc_base_position()
    {
        atom_movable movable = parent as atom_movable;
        if (Dm.truthy(movable.loc._listen_lookup?[COMSIG_AQUARIUM_SET_VISUAL]))
            movable.loc._SendSignal(COMSIG_AQUARIUM_SET_VISUAL, Dm.list(movable.loc, vc_obj)); //set the necessary layer as well as the pixel bounds first
        if (Dm.truthy(movable._listen_lookup?[COMSIG_AQUARIUM_CONTENT_RANDOMIZE_POSITION]))
            movable._SendSignal(COMSIG_AQUARIUM_CONTENT_RANDOMIZE_POSITION, Dm.list(movable, movable.loc, vc_obj));
    }

    // tg: code/datums/components/aquarium_content.dm:107
    [SignalHandler]
    public virtual void on_removed(atom_movable aquarium = null, atom_movable gone = null, object direction = null)
    {
        if (parent != gone)
            return;
        remove_from_aquarium(aquarium);
    }

    // tg: code/datums/components/aquarium_content.dm:113
    public virtual void remove_from_aquarium(atom_movable aquarium = null)
    {
        UnregisterSignal(aquarium, Dm.add(Dm.list(COMSIG_AQUARIUM_FLUID_CHANGED, COMSIG_ATOM_EXITED), animation_update_signals));
        if (Dm.truthy(aquarium._listen_lookup?[COMSIG_AQUARIUM_REMOVE_VISUAL]))
            aquarium._SendSignal(COMSIG_AQUARIUM_REMOVE_VISUAL, Dm.list(aquarium, vc_obj));
    }
}
