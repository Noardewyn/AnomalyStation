// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/pet_collar.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

// tg: code/datums/elements/pet_collar.dm:2
public partial class datum_element_wears_collar : datum_element
{
    // tg: code/datums/elements/pet_collar.dm:11
    [DmParams("target", "collar_icon", "collar_resting_icon_state", "collar_icon_state")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object collar_icon = Dm.arg(__extra, 0) ?? Dm.resource("icons/mob/simple/pets.dmi");
        object collar_resting_icon_state = Dm.arg(__extra, 1) ?? FALSE;
        object collar_icon_state = Dm.arg(__extra, 2);
        object dot = await base.Attach(target, __extra);

        if (!isliving(target))
            return ELEMENT_INCOMPATIBLE;

        this.collar_icon = collar_icon;
        this.collar_icon_state = collar_icon_state;
        this.collar_resting_icon_state = collar_resting_icon_state;

        RegisterSignal(target, COMSIG_ATOM_ATTACKBY, nameof(attach_collar));
        RegisterSignal(target, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(on_overlays_updated));
        RegisterSignal(target, COMSIG_ATOM_EXITED, nameof(on_content_exit));
        RegisterSignal(target, COMSIG_ATOM_ENTERED, nameof(on_content_enter));
        RegisterSignal(target, COMSIG_LIVING_RESTING, nameof(on_rest));
        RegisterSignal(target, COMSIG_MOB_STATCHANGE, nameof(on_stat_change));
        return dot;
    }

    // tg: code/datums/elements/pet_collar.dm:28
    public override object Detach(datum target = null, params object[] __extra)
    {
        object dot = base.Detach(target, __extra);
        UnregisterSignal(target, Dm.list(COMSIG_ATOM_ATTACKBY, COMSIG_ATOM_UPDATE_OVERLAYS, COMSIG_ATOM_EXITED, COMSIG_ATOM_ENTERED, COMSIG_LIVING_RESTING, COMSIG_MOB_STATCHANGE));
        return dot;
    }

    // tg: code/datums/elements/pet_collar.dm:39
    [SignalHandler]
    public virtual void on_stat_change(mob_living source = null)
    {
        if (Dm.truthy(collar_icon_state))
            Waiting.NoSleep(source.update_icon(UPDATE_OVERLAYS));
    }

    // tg: code/datums/elements/pet_collar.dm:45
    [SignalHandler]
    public virtual void on_content_exit(mob_living source = null, atom moved = null)
    {
        if (moved is not obj_item_clothing_neck_petcollar)
            return;
        Waiting.NoSleep(source.fully_replace_character_name(null, initial(source, "name")));
        if (Dm.truthy(collar_icon_state))
            source.update_appearance();
    }

    // tg: code/datums/elements/pet_collar.dm:54
    [SignalHandler]
    public virtual void on_content_enter(mob_living source = null, atom arrived = null)
    {
        if (arrived is not obj_item_clothing_neck_petcollar new_collar || !Dm.truthy(new_collar.tagname))
            return;

        Waiting.NoSleep(source.fully_replace_character_name(null, Dm.text("\\proper []", new_collar.tagname)));
        if (Dm.truthy(collar_icon_state))
            source.update_appearance();
    }

    // tg: code/datums/elements/pet_collar.dm:64
    [SignalHandler]
    public virtual object attach_collar(atom source = null, atom_movable attacking_item = null, atom user = null, object @params = null)
    {
        if (attacking_item is not obj_item_clothing_neck_petcollar)
            return NONE;
        if (Dm.locate<obj_item_clothing_neck_petcollar>(source) != null)
        {
            user.balloon_alert(source as mob, "already wearing a collar!");
            return NONE;
        }
        Waiting.NoSleep(attacking_item.forceMove(source));
        return COMPONENT_NO_AFTERATTACK;
    }

    // tg: code/datums/elements/pet_collar.dm:75
    [SignalHandler]
    public virtual void on_overlays_updated(mob_living source = null, DmList overlays = null)
    {
        if (Dm.locate<obj_item_clothing_neck_petcollar>(source) == null)
            return;

        string icon_tag = "";

        if (IS_DEAD_OR_FAKING(source))
            icon_tag = "_dead";
        else if (Dm.truthy(collar_resting_icon_state) && Dm.truthy(source.resting))
            icon_tag = "_rest";

        Dm.append(ref overlays, mutable_appearance(collar_icon, Dm.text("[][]collar", collar_icon_state, icon_tag)));
        Dm.append(ref overlays, mutable_appearance(collar_icon, Dm.text("[][]tag", collar_icon_state, icon_tag)));
    }

    // tg: code/datums/elements/pet_collar.dm:92
    [SignalHandler]
    public virtual void on_rest(atom_movable source = null)
    {
        Waiting.NoSleep(source.update_icon(UPDATE_OVERLAYS));
    }
}
