// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/earthcracker.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign G2 (D316): the E-2 and E-1 Earthcrackers whole: armed in hand onto the floor, activated, they drive a
// weakpoint into the hull (on a mining level they break the rocks around instead). The screentip is a live-partial row.

/**
 * The E-2 Earthcracker, a traitor device that creates intentional weakpoints on the station after use, which can be triggered via explosions.
 * See weakpoint.dm for more specifics on their effects.
 */
// tg: code/game/objects/items/devices/earthcracker.dm:9
public partial class obj_item_earthcracker : obj_item
{
    // tg: code/game/objects/items/devices/earthcracker.dm:1
    private const string EARTHCRACKER_READY = "ready";
    private const string EARTHCRACKER_ACTIVE = "active";
    private const string EARTHCRACKER_SPENT = "spent";

    // tg: code/game/objects/items/devices/earthcracker.dm:23
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (weakpoint_type == null)
            throw Dm.CRASH("An earthcracker spawned without a designated weakpoint!", "code/game/objects/items/devices/earthcracker.dm", 26);
        register_context();
        return dot;
    }

    // tg: code/game/objects/items/devices/earthcracker.dm:29
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (status == EARTHCRACKER_READY)
            await handle_arming(user);
        return dot;
    }

    // tg: code/game/objects/items/devices/earthcracker.dm:34
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (!Dm.truthy(anchored))
            return NONE;
        if (status == EARTHCRACKER_ACTIVE)
        {
            if (Dm.truthy(activation_timer))
                return FALSE;
            bool response = Dm.eq(await tgui_alert(user, I18n.T("Activate the earthcracker?"), I18n.T("Activate?"), Dm.list("Yes", "No")), "Yes");
            if (!response)
                return FALSE;
            if (!Dm.truthy(await user.Adjacent(this)))
                return FALSE;
            flick(Dm.text("[]-active", base_icon_state), this);
            if (Dm.truthy(is_mining_level(z)))
            {
                activation_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(mining_act), user), 0.8f * SECONDS, file: "code/game/objects/items/devices/earthcracker.dm", line: 49);
                return TRUE;
            }
            activation_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(strike_the_earth)), 0.8f * SECONDS, file: "code/game/objects/items/devices/earthcracker.dm", line: 51);
            return TRUE;
        }
        else if (status == EARTHCRACKER_SPENT)
        {
            balloon_alert(user, I18n.T("used up!"));
            return FALSE;
        }
        return dot;
    }

    // tg: code/game/objects/items/devices/earthcracker.dm:57
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (status == EARTHCRACKER_READY)
            icon_state = Dm.text("[]", base_icon_state);
        else if (status == EARTHCRACKER_ACTIVE)
            icon_state = Dm.text("[]-armed", base_icon_state);
        else if (status == EARTHCRACKER_SPENT)
            icon_state = Dm.text("[]-spent", base_icon_state);
        return dot;
    }

    // tg: code/game/objects/items/devices/earthcracker.dm:67
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(anchored) && status == EARTHCRACKER_SPENT)
        {
            balloon_alert(user, I18n.T("it falls apart"));
            animate(this, 0.6f * SECONDS, Dm.named("alpha", 0), Dm.named("easing", Dm.bitor(CIRCULAR_EASING, EASE_IN)));
            _addtimer(Dm.@new<datum_callback>(this, nameof(post_break)), 0.6f * SECONDS, file: "code/game/objects/items/devices/earthcracker.dm", line: 71);
            return ITEM_INTERACT_SUCCESS;
        }
        if (!Dm.truthy(anchored) && status == EARTHCRACKER_READY)
        {
            balloon_alert(user, I18n.T("arm in hands first!"));
            return ITEM_INTERACT_SUCCESS;
        }
        if (Dm.truthy(anchored) && status == EARTHCRACKER_ACTIVE)
        {
            balloon_alert(user, I18n.T("unfastening from the floor..."));
            if (!Dm.truthy(await tool.use_tool(this, user, 8 * SECONDS, volume: 50)))
                return ITEM_INTERACT_FAILURE;
            anchored = FALSE;
            status = EARTHCRACKER_READY;
            update_appearance(UPDATE_ICON);
            return NONE;
        }
        return null;
    }

    // tg: code/game/objects/items/devices/earthcracker.dm:94
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (status == EARTHCRACKER_SPENT)
            dot.Add(span_warning(I18n.T("This device is toast. You could disassemble the remains using a [].", EXAMINE_HINT(I18n.T("Wrench")))));
        else
            dot.Add(span_info(I18n.T("This device can be unanchored using a [].", EXAMINE_HINT(I18n.T("Wrench")))));
        return dot;
    }

    // tg: code/game/objects/items/devices/earthcracker.dm:101
    public virtual async DmTask<object> handle_arming(mob user = null)
    {
        turf arm_location = get_turf(user);
        if (arm_location == null)
            return FALSE;
        if (isgroundlessturf(arm_location))
        {
            balloon_alert(user, I18n.T("can't deploy here"));
            return FALSE;
        }

        if (status == EARTHCRACKER_SPENT)
        {
            balloon_alert(user, I18n.T("used up!"));
            return FALSE;
        }
        balloon_alert(user, I18n.T("arming..."));
        if (!Dm.truthy(await do_after(user, 3 * SECONDS, this)))
        {
            balloon_alert(user, I18n.T("failed to arm"));
            return FALSE;
        }

        await forceMove(arm_location);
        anchored = TRUE;
        flick(Dm.text("[]-arm", base_icon_state), this);
        playsound(this, Dm.resource("sound/items/barcodebeep.ogg"), 50, FALSE);
        status = EARTHCRACKER_ACTIVE;
        update_appearance(UPDATE_ICON);
        return TRUE;
    }

    /// The fun part. We spawn a huge weakpoint here.
    // tg: code/game/objects/items/devices/earthcracker.dm:126
    public virtual void strike_the_earth()
    {
        if (QDELETED(this))
            return;
        playsound(this, Dm.resource("sound/items/weapons/earthcracker_bang.mp3"), 75, FALSE, 3);
        turf cracked_hull = drop_location() as turf;
        Dm.@new(weakpoint_type, cracked_hull);
        handle_after_activation(cracked_hull);
    }

    /// Cleanup after an earthcracker is activated either for sabotage or mining.
    // tg: code/game/objects/items/devices/earthcracker.dm:135
    public virtual void handle_after_activation(turf cracked_hull = null)
    {
        do_sparks(2, FALSE, this);
        cracked_hull?.levelupdate();

        status = EARTHCRACKER_SPENT;
        update_appearance(UPDATE_ICON);
        particles = Dm.@new<particles_smoke_burning_small>();
        activation_timer = null;
    }

    /// Called after the earthcracker activates.
    // tg: code/game/objects/items/devices/earthcracker.dm:145
    public virtual async DmTask post_break()
    {
        await deconstruct(TRUE);
    }

    /// When this item is used on a mining Z, we perform an action that breaks all rocks in a radius around us, the same as starting an ore vent wave.
    // tg: code/game/objects/items/devices/earthcracker.dm:149
    public virtual async DmTask mining_act(mob user = null)
    {
        for (float i = 1; i <= 5; i++)
        {
            foreach (turf rock in Dm.each<turf>(oview(i, this))) // This collects a list of rings of turfs (in a growing radius of i) that we'll applying logic to "drill" below.
            {
                if (istype(rock, typeof(turf_closed_mineral)))
                {
                    if (prob(i * 15 - 25))
                        continue;
                    turf_closed_mineral drillable = (turf_closed_mineral)rock;
                    await drillable.gets_drilled(user);
                    if (prob(15))
                        Dm.@new<obj_effect_decal_cleanable_rubble>(rock);
                    continue;
                }

                if (istype(rock, typeof(turf_open_misc_asteroid)) && prob(10))
                {
                    Dm.@new<obj_effect_decal_cleanable_rubble>(rock);
                    continue;
                }
            }
            await sleep(0.6f * SECONDS);
        }
        handle_after_activation();
    }
}
