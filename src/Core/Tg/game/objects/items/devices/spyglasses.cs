// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/spyglasses.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps J4, D298; the remote view's popup map came with the popup windows, silicons SI4, DV387): the detective's spyglass
// kit. The glasses and the bug link, wear and pin, and the remote view, a popup window the client draws with a second 3D view of the
// bug's surroundings.

// tg: code/game/objects/items/devices/spyglasses.dm:2
public partial class obj_item_clothing_glasses_sunglasses_spy : obj_item_clothing_glasses_sunglasses
{
    // tg: code/game/objects/items/devices/spyglasses.dm:7
    public virtual void show_to_user(mob user = null)
    {
        client cool_guy = user?.client;
        if (cool_guy == null)
            return;
        if (linked_bug == null)
        {
            user.audible_message(span_warning(I18n.T("[] lets off a shrill beep!", this)));
            return;
        }
        if (Dm.truthy(cool_guy.screen_maps["spypopup_map"])) //alright, the popup this object uses is already IN use, so the window is open. no point in doing any other work here, so we're good.
            return;
        cool_guy.setup_popup("spypopup", 3, 3, 2, "S.P.Y");
        linked_bug.cam_screen.display_to(user);
        RegisterSignal(cool_guy, COMSIG_POPUP_CLEARED, nameof(on_screen_clear));

        linked_bug.update_view();
    }

    // tg: code/game/objects/items/devices/spyglasses.dm:22
    [SignalHandler]
    public virtual void on_screen_clear(client source = null, object window = null)
    {
        linked_bug.cam_screen.hide_from_client(source);
        UnregisterSignal(source, COMSIG_POPUP_CLEARED);
    }

    // tg: code/game/objects/items/devices/spyglasses.dm:27
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (!Dm.truthy(Dm.bitand(slot, ITEM_SLOT_EYES)))
            user.client?.close_popup("spypopup");
    }

    // tg: code/game/objects/items/devices/spyglasses.dm:32
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        user.client?.close_popup("spypopup");
        return dot;
    }

    // tg: code/game/objects/items/devices/spyglasses.dm:36
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        show_to_user(user);
        return null;
    }

    // tg: code/game/objects/items/devices/spyglasses.dm:39
    public override object Destroy(float? force = null)
    {
        if (linked_bug != null)
            linked_bug.linked_glasses = null;
        return base.Destroy(force);
    }
}

// tg: code/game/objects/items/devices/spyglasses.dm:48
public partial class obj_item_clothing_accessory_spy_bug : obj_item_clothing_accessory
{
    // tg: code/game/objects/items/devices/spyglasses.dm:62
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_pinnable_accessory)));
        tracker = Dm.@new<datum_movement_detector>(this, Dm.@new<datum_callback>(this, nameof(update_view)));
        cam_screen = Dm.@new<atom_movable_screen_map_view>();
        cam_screen.generate_view("spypopup_map");
        cam_screen.set_display(this);
        return dot;
    }

    // tg: code/game/objects/items/devices/spyglasses.dm:70
    public override object Destroy(float? force = null)
    {
        if (linked_glasses != null)
            linked_glasses.linked_bug = null;
        QDEL_NULL(ref cam_screen);
        QDEL_NULL(ref tracker);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/devices/spyglasses.dm:77
    public virtual void update_view() //this doesn't do anything too crazy, just updates the vis_contents of its screen obj
    {
        cam_screen.vis_contents.Cut();
        DmList shown = cam_screen.vis_contents; // a list: changed in place
        foreach (turf visible_turf in Dm.each<turf>(view(cam_range, get_turf(this)))) //fuck you usr
            Dm.append(ref shown, visible_turf);
    }
}
