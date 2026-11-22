// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/crates/bins.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST3 (D333): the file whole, the trash bin: its fill drawn (empty, some, full), a trash bag filled from it with the lid's flick
// and sounds, the push broom's garbage swept into it. Its emissive overlay is DV12's, as every emissive in the port: the client draws
// lamps from the 3D model. The var block is generated (D111).

// tg: code/game/objects/structures/crates_lockers/crates/bins.dm:1
public partial class obj_structure_closet_crate_bin : obj_structure_closet_crate
{
    // tg: code/game/objects/structures/crates_lockers/crates/bins.dm:17
    public override void LateInitialize(params object[] __extra)
    {
        base.LateInitialize(__extra);
        update_appearance(UPDATE_ICON);
        DmList loc_connections = TgState.Current.obj_structure_closet_crate_bin__LateInitialize__loc_connections ??= Dm.list(
            Dm.kv(COMSIG_TURF_RECEIVE_SWEEPED_ITEMS, nameof(ready_for_trash)));
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), loc_connections));
    }

    // tg: code/game/objects/structures/crates_lockers/crates/bins.dm:25
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList ?? Dm.list();
        // DEVIATION(DV12): . += emissive_appearance(icon, base_icon_state + "_empty", src, alpha = src.alpha), the bin's glow
        if (contents.Count == 0)
        {
            Dm.append(dot, (string)base_icon_state + "_empty");
            return dot;
        }
        if (contents.Count >= storage_capacity)
        {
            Dm.append(dot, (string)base_icon_state + "_full");
            return dot;
        }
        Dm.append(dot, (string)base_icon_state + "_some");
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/crates/bins.dm:36
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!(tool is obj_item_storage_bag_trash) || !Dm.truthy(opened))
            return await base.item_interaction(user, tool, modifiers, __extra);
        obj_item_storage_bag_trash garbage_bag = (obj_item_storage_bag_trash)tool;
        to_chat(user, span_notice(I18n.T("You fill the bag.")));
        foreach (obj_item garbage in Dm.each<obj_item>(contents.Copy()))
            garbage_bag.atom_storage?.attempt_insert(garbage, user, TRUE);
        do_animate();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/crates_lockers/crates/bins.dm:46
    public virtual void do_animate()
    {
        playsound(loc, open_sound, 15, TRUE, -3);
        flick((string)base_icon_state + "_animate", this);
        _addtimer(Dm.@new<datum_callback>(this, nameof(do_close)), 1.1f * SECONDS, file: "code/game/objects/structures/crates_lockers/crates/bins.dm", line: 49);
    }

    // tg: code/game/objects/structures/crates_lockers/crates/bins.dm:51
    public virtual void do_close()
    {
        playsound(loc, close_sound, 15, TRUE, -3);
        update_appearance();
    }

    ///Called when a push broom is trying to sweep items onto the turf this object is standing on. Garbage will be moved inside.
    // tg: code/game/objects/structures/crates_lockers/crates/bins.dm:56
    [SignalHandler]
    public virtual void ready_for_trash(datum source = null, obj_item_pushbroom broom = null, mob user = null, DmList items_to_sweep = null)
    {
        if (!Dm.truthy(items_to_sweep) || !Dm.truthy(opened))
            return;

        foreach (obj_item garbage in Dm.each<obj_item>(items_to_sweep))
            Waiting.NoSleep(garbage.forceMove(loc));

        items_to_sweep.Cut();

        to_chat(user, span_notice(I18n.T("You sweep the pile of garbage into [].", this)));
        playsound(broom.loc, Dm.resource("sound/items/weapons/thudswoosh.ogg"), 30, TRUE, -1);
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/structures/crates_lockers/crates/bins.dm:20  /obj/structure/closet/crate/bin/LateInitialize's var/static/list/loc_connections
    internal DmList obj_structure_closet_crate_bin__LateInitialize__loc_connections;
}
