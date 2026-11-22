// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/table_clock.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DV2 (D333): the file whole. The table clock ticks (tg's clock loop), tells the station time and the shift's length when
// examined, breaks (its "_broken" picture, the loop stopped) when hit hard or thrown, and is fixed by hand in ten seconds, at most
// three times. The var block is generated (D111).

// tg: code/game/objects/items/devices/table_clock.dm:4
public partial class obj_item_table_clock : obj_item
{
    ///Maximum amount of times a clock can be repaired until it's destroyed beyond repair.
    private const int MAX_CLOCK_REPAIRS = 2; // tg: table_clock.dm:2

    // tg: code/game/objects/items/devices/table_clock.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        soundloop = Dm.@new<datum_looping_sound_clock>(this, TRUE);
        Waiting.NoSleep(AddElement(typeof(datum_element_beauty), 200));
        return dot;
    }

    // tg: code/game/objects/items/devices/table_clock.dm:25
    public override object Destroy(float? force = null)
    {
        soundloop.stop();
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/devices/table_clock.dm:29
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(broken))
            Dm.append(ref dot, span_info(I18n.T("It appears to be currently broken. You can use it in-hand to repair it.")));
        else
        {
            Dm.append(ref dot, span_info(I18n.T("The current NST (local) time is: [].", server_timestamp(ic_time: TRUE, twelve_hour_clock: user.client?.prefs?.read_preference(typeof(datum_preference_toggle_twelve_hour))))));
            if (Dm.truthy(user.is_literate()))
                Dm.append(ref dot, span_info(I18n.T("That means it is currently [] into the shift.", round_timestamp())));
        }
        return dot;
    }

    // tg: code/game/objects/items/devices/table_clock.dm:38
    public override async DmTask<object> attackby(obj_item attacking_item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = await base.attackby(attacking_item, user, modifiers, attack_modifiers);
        if (attacking_item.force < 5 || Dm.truthy(broken))
            return dot;
        if (Dm.truthy(break_clock(break_sound: Dm.resource("sound/effects/magic/clockwork/ark_activation.ogg"))))
        {
            user.visible_message(
                span_warning(I18n.T("[] smashes \\the [] so hard it stops breaking!", user, this)),
                span_bolddanger(I18n.T("I can't stand this stupid machine anymore! Shut up already!")),
                span_notice(I18n.T("You hear repeated smashing!")));
        }
        return dot;
    }

    // tg: code/game/objects/items/devices/table_clock.dm:49
    public override async DmTask<object> throw_at(atom target = null, object range = null, object speed = null, atom thrower = null, float? spin = null, float? diagonals_first = null, datum_callback callback = null, float? force = null, float? gentle = null, object quickstart = null, Type throw_datum_typepath = null, params object[] __extra)
    {
        throw_datum_typepath ??= typeof(datum_thrownthing);
        object dot = await base.throw_at(target, range, speed, thrower, spin, diagonals_first, callback, force, gentle, quickstart, throw_datum_typepath, __extra);
        if (!Dm.truthy(dot))
            return dot;
        break_clock(break_sound: Dm.resource("sound/effects/footstep/glass_step.ogg"));
        return dot;
    }

    // tg: code/game/objects/items/devices/table_clock.dm:55
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        object dot = await base.interact(user, __extra);
        if (!Dm.truthy(broken))
        {
            to_chat(user, span_warning(I18n.T("Touch the clock? And risk breaking it? Are you crazy??")));
            return dot;
        }
        if (Dm.num(times_broken) > MAX_CLOCK_REPAIRS)
        {
            user.balloon_alert(user, I18n.T("clock unrepairable!"));
            return dot;
        }
        user.balloon_alert(user, I18n.T("fixing clock..."));
        if (!Dm.truthy(await do_after(user, 10 * SECONDS, this)))
            return dot;
        user.balloon_alert(user, I18n.T("clock repaired!"));
        broken = FALSE;
        soundloop.start();
        update_appearance(UPDATE_ICON);
        return dot;
    }

    // tg: code/game/objects/items/devices/table_clock.dm:71
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(broken) ? "_broken" : null);
        return await base.update_icon_state(__extra);
    }

    /**
     * Breaks the clock, turning off the soundloop.
     * Returns TRUE if it successfully breaks, FALSE otherwise.
     */
    // tg: code/game/objects/items/devices/table_clock.dm:79
    public virtual object break_clock(object break_sound = null)
    {
        if (Dm.truthy(broken))
            return FALSE;

        broken = TRUE;
        soundloop.stop();
        playsound(this, break_sound, 40, FALSE);
        times_broken = Dm.num(times_broken) + 1;
        update_appearance(UPDATE_ICON);
        return TRUE;
    }
}
