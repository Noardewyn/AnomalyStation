// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/geiger_counter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 radiation slice: the whole file. The var block is generated (D111). Its clicks are /datum/component/geiger_sound's loop
// (datums/components/geiger_sound.cs); the chat icon of a scan (icon2html) is a deferred leaf (chat icons, M9).

//DISCLAIMER: I know nothing about how real-life Geiger counters work. This will not be realistic. ~Xhuis
// tg: code/game/objects/items/devices/geiger_counter.dm:1
public partial class obj_item_geiger_counter : obj_item
{
    // tg: code/game/objects/items/devices/geiger_counter.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        RegisterSignal(this, COMSIG_IN_RANGE_OF_IRRADIATION, nameof(on_pre_potential_irradiation));
        return dot;
    }

    // tg: code/game/objects/items/devices/geiger_counter.dm:24
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!Dm.truthy(scanning))
            return dot;
        Dm.append(ref dot, span_info(I18n.T("Alt-click it to clear stored radiation levels.")));
        object sw__1 = last_perceived_radiation_danger;
        if (Dm.eq(sw__1, null))
            Dm.append(ref dot, span_notice(I18n.T("Ambient radiation level count reports that all is well.")));
        else if (Dm.eq(sw__1, PERCEIVED_RADIATION_DANGER_LOW))
            Dm.append(ref dot, span_alert(I18n.T("Ambient radiation levels slightly above average.")));
        else if (Dm.eq(sw__1, PERCEIVED_RADIATION_DANGER_MEDIUM))
            Dm.append(ref dot, span_warning(I18n.T("Ambient radiation levels above average.")));
        else if (Dm.eq(sw__1, PERCEIVED_RADIATION_DANGER_HIGH))
            Dm.append(ref dot, span_danger(I18n.T("Ambient radiation levels highly above average.")));
        else if (Dm.eq(sw__1, PERCEIVED_RADIATION_DANGER_EXTREME))
            Dm.append(ref dot, span_suicide(I18n.T("Ambient radiation levels reaching critical level!")));
        return dot;
    }

    // tg: code/game/objects/items/devices/geiger_counter.dm:41
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (!Dm.truthy(scanning))
        {
            icon_state = "geiger_off";
            return await base.update_icon_state(__extra);
        }

        object sw__1 = last_perceived_radiation_danger;
        if (Dm.eq(sw__1, null))
            icon_state = "geiger_on_1";
        else if (Dm.eq(sw__1, PERCEIVED_RADIATION_DANGER_LOW))
            icon_state = "geiger_on_2";
        else if (Dm.eq(sw__1, PERCEIVED_RADIATION_DANGER_MEDIUM))
            icon_state = "geiger_on_3";
        else if (Dm.eq(sw__1, PERCEIVED_RADIATION_DANGER_HIGH))
            icon_state = "geiger_on_4";
        else if (Dm.eq(sw__1, PERCEIVED_RADIATION_DANGER_EXTREME))
            icon_state = "geiger_on_5";
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/devices/geiger_counter.dm:59
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        scanning = Dm.num(!Dm.truthy(scanning));

        if (Dm.truthy(scanning))
            await AddComponent(typeof(datum_component_geiger_sound));
        else
            qdel(GetComponent(typeof(datum_component_geiger_sound)));

        update_appearance(UPDATE_ICON);
        balloon_alert(user, I18n.T("switch []", Dm.truthy(scanning) ? I18n.T("on") : I18n.T("off")));
        return null;
    }

    // tg: code/game/objects/items/devices/geiger_counter.dm:70
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (HAS_TRAIT(interacting_with, TRAIT_COMBAT_MODE_SKIP_INTERACTION) && Dm.truthy(user.combat_mode)) // SHOULD_SKIP_INTERACTION (__DEFINES/tools.dm:62), inline
            return NONE;
        return await ranged_interact_with_atom(interacting_with, user, modifiers);
    }

    // tg: code/game/objects/items/devices/geiger_counter.dm:75
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!CAN_IRRADIATE(interacting_with))
            return NONE;

        user.visible_message(span_notice(I18n.T("[] scans [] with [].", user, interacting_with, this)), span_notice(I18n.T("You scan []'s radiation levels with []...", interacting_with, this)));
        _addtimer(Dm.@new<datum_callback>(this, nameof(scan), interacting_with, user), 20, TIMER_UNIQUE, file: "code/game/objects/items/devices/geiger_counter.dm", line: 80); // Let's not have spamming GetAllContents
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/devices/geiger_counter.dm:83
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        initial ??= FALSE;
        await base.equipped__proc(user, slot, initial);

        RegisterSignal(user, COMSIG_IN_RANGE_OF_IRRADIATION, nameof(on_pre_potential_irradiation));
    }

    // tg: code/game/objects/items/devices/geiger_counter.dm:88
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        silent ??= FALSE;
        object dot = await base.dropped(user, silent, __extra);

        UnregisterSignal(user, COMSIG_IN_RANGE_OF_IRRADIATION);
        return dot;
    }

    // tg: code/game/objects/items/devices/geiger_counter.dm:93
    [SignalHandler]
    public virtual void on_pre_potential_irradiation(datum source = null, datum_radiation_pulse_information pulse_information = null, object insulation_to_target = null)
    {
        // SIGNAL_HANDLER

        last_perceived_radiation_danger = get_perceived_radiation_danger(pulse_information, insulation_to_target);
        _addtimer(Dm.@new<datum_callback>(this, nameof(reset_perceived_danger)), TIME_WITHOUT_RADIATION_BEFORE_RESET, Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/game/objects/items/devices/geiger_counter.dm", line: 97);

        if (Dm.truthy(scanning))
            update_appearance(UPDATE_ICON);
    }

    // tg: code/game/objects/items/devices/geiger_counter.dm:102
    public virtual void reset_perceived_danger()
    {
        last_perceived_radiation_danger = null;
        if (Dm.truthy(scanning))
            update_appearance(UPDATE_ICON);
    }

    // tg: code/game/objects/items/devices/geiger_counter.dm:107
    public virtual void scan(atom target = null, mob user = null)
    {
        object result = !Dm.truthy(target._listen_lookup?[COMSIG_GEIGER_COUNTER_SCAN]) ? (object)NONE : target._SendSignal(COMSIG_GEIGER_COUNTER_SCAN, Dm.list(target, user, this));
        if (Dm.truthy(Dm.bitand(result, COMSIG_GEIGER_COUNTER_SCAN_SUCCESSFUL)))
            return;

        // [icon2html(src, user)] before the text: the counter's picture in the chat line
        Dm.Deferred("code/game/objects/items/devices/geiger_counter.dm:111", "scan: icon2html(src, user), the counter's icon in the chat line (chat icons, M10+)");
        to_chat(user, span_notice(I18n.T("[] is free of radioactive contamination.", isliving(target) ? I18n.T("Subject") : I18n.T("Target"))));
    }

    // tg: code/game/objects/items/devices/geiger_counter.dm:113
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!Dm.truthy(scanning))
        {
            to_chat(user, span_warning(I18n.T("[] must be on to reset its radiation level!", this)));
            return CLICK_ACTION_BLOCKING;
        }
        to_chat(user, span_notice(I18n.T("You flush []'s radiation counts, resetting it to normal.", this)));
        last_perceived_radiation_danger = null;
        update_appearance(UPDATE_ICON);
        return CLICK_ACTION_SUCCESS;
    }
}
