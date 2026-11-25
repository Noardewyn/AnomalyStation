// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/nightmare/nightmare_equipment.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP3b (D349): the whole file: the nightmare heart's armblade, which snuffs lights out. Only a nightmare antagonist's blade gets the
// critical strike after a jaunt (tg checks the antagonist datum; the datum itself is not translated, D333). The var block is generated (D111).

/**
 * An armblade that instantly snuffs out lights
 */
// tg: code/modules/antagonists/nightmare/nightmare_equipment.dm:5
public partial class obj_item_light_eater : obj_item
{
    // tg: code/modules/antagonists/nightmare/nightmare_equipment.dm:27
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NODROP, HAND_REPLACEMENT_TRAIT);
        Waiting.NoSleep(AddComponent(typeof(datum_component_butchering),
            Dm.named("speed", 8 * SECONDS),
            Dm.named("effectiveness", 70)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_light_eater)));
        return dot;
    }

    // tg: code/modules/antagonists/nightmare/nightmare_equipment.dm:35
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        initial ??= FALSE;
        await base.equipped__proc(user, slot, initial);
        if (user?.mind?.has_antag_datum(typeof(datum_antagonist_nightmare)) == null)
            return;
        RegisterSignal(user, COMSIG_MOB_ENTER_JAUNT, nameof(prepare_crit_timer));
        RegisterSignal(user, COMSIG_MOB_AFTER_EXIT_JAUNT, nameof(stop_crit_timer));
    }

    // tg: code/modules/antagonists/nightmare/nightmare_equipment.dm:42
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        silent ??= FALSE;
        object dot = await base.dropped(user, silent, __extra);
        if (user?.mind?.has_antag_datum(typeof(datum_antagonist_nightmare)) == null)
            return dot;
        UnregisterSignal(user, COMSIG_MOB_ENTER_JAUNT);
        UnregisterSignal(user, COMSIG_MOB_AFTER_EXIT_JAUNT);
        remove_crit();
        return dot;
    }

    // tg: code/modules/antagonists/nightmare/nightmare_equipment.dm:50
    public override async DmTask<object> attack__proc(mob_living target = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        object dot = await base.attack__proc(target, user, modifiers, attack_modifiers, __extra);
        if (!Dm.truthy(has_crit))
            return dot;
        playsound(target, Dm.resource("sound/effects/wounds/crackandbleed.ogg"), 100, TRUE);
        if (Dm.eq(target.stat, DEAD))
        {
            user.visible_message(span_warning(I18n.T("[] gores [] with []!", user, target, this)), span_warning(I18n.T("You gore [] with [], which doesn't accomplish much, but it does make you feel a little better.", target, this)));
        }
        else if (!HAS_TRAIT(target, TRAIT_HULK) && (iscarbon(target) || issilicon(target)))
        {
            user.visible_message(span_boldwarning(I18n.T("[] gores [] with [], bringing them to a halt!", user, target, this)), span_userdanger(I18n.T("You gore [] with [], bringing them to a halt!", target, this)));
            await target.Paralyze(issilicon(target) ? 2 * SECONDS : 1 * SECONDS);
        }
        else
        {
            user.visible_message(span_boldwarning(I18n.T("[] gores [] with [], ripping into them!", user, target, this)), span_userdanger(I18n.T("You gore [] with [], ripping into them!", target, this)));
            await target.apply_damage(damage: force);
        }
        remove_crit();
        return dot;
    }

    // tg: code/modules/antagonists/nightmare/nightmare_equipment.dm:67
    [SignalHandler]
    public virtual void prepare_crit_timer()
    {
        crit_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(add_crit)), 7 * SECONDS, Dm.bitor(TIMER_DELETE_ME, TIMER_STOPPABLE), file: "code/modules/antagonists/nightmare/nightmare_equipment.dm", line: 68);
    }

    // tg: code/modules/antagonists/nightmare/nightmare_equipment.dm:70
    [SignalHandler]
    public virtual void stop_crit_timer()
    {
        deltimer(crit_timer);
    }

    // tg: code/modules/antagonists/nightmare/nightmare_equipment.dm:73
    public virtual void add_crit()
    {
        if (Dm.truthy(has_crit))
            return;
        has_crit = TRUE;
        add_filter("crit_glow", 3, Dm.list(Dm.kv("type", "outline"), Dm.kv("color", COLOR_CARP_RIFT_RED), Dm.kv("size", 5)));
        if (ismob(loc))
            ((atom)loc).balloon_alert((mob)loc, I18n.T("critical strike ready"));
    }

    // tg: code/modules/antagonists/nightmare/nightmare_equipment.dm:81
    public virtual void remove_crit()
    {
        if (!Dm.truthy(has_crit))
            return;
        has_crit = FALSE;
        remove_filter("crit_glow");
        stop_crit_timer();
    }
}
