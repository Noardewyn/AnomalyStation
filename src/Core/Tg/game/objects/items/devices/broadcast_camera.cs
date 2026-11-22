// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/broadcast_camera.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// Gaps TL1: the whole file. The broadcast camera streams what its holder sees to the entertainment monitors (a camera inside
// the holder on its own networks, broadcast through telescreen.dm's procs) and its audio on the entertainment frequency (a
// microphone radio), with its red light while live; the cargo's camera gets a network of its own. The var blocks are generated.

using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Unique broadcast camera given to the first Curator
// Only one should exist ideally, if other types are created they must have different camera_networks
// Broadcasts its surroundings to entertainment monitors and its audio to entertainment radio channel
// tg: code/game/objects/items/devices/broadcast_camera.dm:4
public partial class obj_item_broadcast_camera : obj_item
{
    // tg: code/game/objects/items/devices/broadcast_camera.dm:39
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(AddElement(typeof(datum_element_empprotection), EMP_PROTECT_ALL));
        return dot;
    }

    // tg: code/game/objects/items/devices/broadcast_camera.dm:44
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref internal_radio);
        QDEL_NULL(ref internal_camera);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/devices/broadcast_camera.dm:49
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", base_icon_state, active);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/devices/broadcast_camera.dm:53
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        active = Dm.num(!Dm.truthy(active));
        if (Dm.truthy(active))
            await on_activating();
        else
            await on_deactivating();
        return dot;
    }

    // tg: code/game/objects/items/devices/broadcast_camera.dm:61
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        object dot = await base.attack_self_secondary(user, modifiers);
        broadcast_name = Dm.str(await tgui_input_text(user: user, title: I18n.T("Broadcast Name"), message: I18n.T("What will be the name of your broadcast?"), @default: Dm.text("[]", broadcast_name), max_length: MAX_CHARTER_LEN));
        return dot;
    }

    // tg: code/game/objects/items/devices/broadcast_camera.dm:65
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Broadcast name is <b>[]</b>", broadcast_name)));
        Dm.append(ref dot, span_notice(I18n.T("The microphone is <b>[]</b>", Dm.truthy(active_microphone) ? I18n.T("On") : I18n.T("Off"))));
        return dot;
    }

    // tg: code/game/objects/items/devices/broadcast_camera.dm:70
    public override void on_enter_storage(datum_storage master_storage = null)
    {
        base.on_enter_storage(master_storage);
        if (Dm.truthy(active))
            Waiting.NoSleep(on_deactivating());
    }

    // tg: code/game/objects/items/devices/broadcast_camera.dm:75
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        if (Dm.truthy(active))
            await on_deactivating();
        return dot;
    }

    /// When activating the camera
    // tg: code/game/objects/items/devices/broadcast_camera.dm:81
    public virtual async DmTask on_activating()
    {
        if (!iscarbon(loc))
            return;
        active = TRUE;
        await update_icon_state();
        /// The carbon who wielded the camera, allegedly
        var wielding_carbon = (mob_living_carbon)loc;

        // INTERNAL CAMERA
        internal_camera = Dm.@new<obj_machinery_camera>(wielding_carbon); // Cameras for some reason do not work inside of obj's
        internal_camera.internal_light = FALSE;
        internal_camera.view_range = camera_range;
        internal_camera.network = camera_networks;
        internal_camera.c_tag = Dm.text("LIVE: []", broadcast_name);
        start_broadcasting_network(camera_networks, I18n.T("[] is now LIVE!", broadcast_name));

        // INTERNAL RADIO
        internal_radio = Dm.@new<obj_item_radio_entertainment_microphone>(this);
        /// Sets the state of the microphone
        set_microphone_state();

        await set_light_on(TRUE);
        playsound(source: this, soundin: Dm.resource("sound/machines/terminal/terminal_processing.ogg"), vol: 20, vary: FALSE, ignore_walls: FALSE);
        balloon_alert_to_viewers(I18n.T("live!"));
    }

    /// When deactivating the camera
    // tg: code/game/objects/items/devices/broadcast_camera.dm:107
    public virtual async DmTask on_deactivating()
    {
        active = FALSE;
        await update_icon_state();
        QDEL_NULL(ref internal_camera);
        QDEL_NULL(ref internal_radio);

        stop_broadcasting_network(camera_networks);

        await set_light_on(FALSE);
        playsound(source: this, soundin: Dm.resource("sound/machines/terminal/terminal_prompt_deny.ogg"), vol: 20, vary: FALSE, ignore_walls: FALSE);
        balloon_alert_to_viewers(I18n.T("offline"));
    }

    // tg: code/game/objects/items/devices/broadcast_camera.dm:119
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        active_microphone = Dm.num(!Dm.truthy(active_microphone));

        /// Text popup for letting the user know that the microphone has changed state
        balloon_alert(user, Dm.truthy(active_microphone) ? I18n.T("microphone activated") : I18n.T("microphone deactivated"));

        ///If the radio exists as an object, set its state accordingly
        if (Dm.truthy(active))
            set_microphone_state();

        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/devices/broadcast_camera.dm:131
    public virtual void set_microphone_state()
    {
        internal_radio.set_broadcasting(active_microphone);
    }
}

// Orderable from cargo
// tg: code/game/objects/items/devices/broadcast_camera.dm:135
public partial class obj_item_broadcast_camera_cargo : obj_item_broadcast_camera
{
    // tg: code/game/objects/items/devices/broadcast_camera.dm:141
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // Gives each cargo camera a unique network id
        camera_networks = Dm.list(Dm.text("cargo_camera_id_[]", TgState.Current.obj_item_broadcast_camera_cargo__cargo_camera_network_id++));
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/devices/broadcast_camera.dm:144  var/static/cargo_camera_network_id = 0
    internal float obj_item_broadcast_camera_cargo__cargo_camera_network_id;
}
