// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/scanners/gas_analyzer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice T3 (D164): the whole file (its suicide_act: gaps DV2). The var block is generated. Leaves:
// the barometer reads SSweather (gaps E2a); the chat icons (icon2html) are the 3D client's
// chat (M9); the anomaly's analyzer_act is the anomaly slice's (its type is the supermatter slice's). The window is tg's
// GasAnalyzer interface (godot/scripts/Shared/Tgui/Interfaces/GasAnalyzer.cs) with the atmos handbook of __HELPERS/atmospherics.cs.

// tg: code/game/objects/items/devices/scanners/gas_analyzer.dm:1
public partial class obj_item_analyzer : obj_item
{
    // tg: code/game/objects/items/devices/scanners/gas_analyzer.dm:36
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_TOOL_ATOM_ACTED_PRIMARY(tool_behaviour), nameof(on_analyze));

        if (type != typeof(obj_item_analyzer))
            return dot;
        // tg: code/game/objects/items/devices/scanners/gas_analyzer.dm:41  var/static/list/slapcraft_recipe_list (a type-level static, rules 10.6)
        Waiting.NoSleep(AddElement(typeof(datum_element_slapcrafting), TgState.Current.obj_item_analyzer__slapcraft_recipe_list));
        return dot;
    }

    // tg: code/game/objects/items/devices/scanners/gas_analyzer.dm:48
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_mercury), 5), Dm.kv(typeof(datum_reagent_iron), 5), Dm.kv(typeof(datum_reagent_silicon), 5));
    }

    // tg: code/game/objects/items/devices/scanners/gas_analyzer.dm:51
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        ADD_TRAIT(user, TRAIT_DETECT_STORM, CLOTHING_TRAIT);
    }

    // tg: code/game/objects/items/devices/scanners/gas_analyzer.dm:55
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.dropped(user, silent, __extra);
        REMOVE_TRAIT(user, TRAIT_DETECT_STORM, CLOTHING_TRAIT);
        return dot;
    }

    // tg: code/game/objects/items/devices/scanners/gas_analyzer.dm:59
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Right-click [] to open the gas reference.", this)));
        Dm.append(ref dot, span_notice(I18n.T("Alt-click [] to activate the barometer function.", this)));
        return dot;
    }

    // tg: code/game/objects/items/devices/scanners/gas_analyzer.dm:64  (gaps DV2)
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] begins to analyze []self with []! The display shows that [] dead!", user, user.p_them(), this, user.p_theyre())));
        return BRUTELOSS;
    }

    // tg: code/game/objects/items/devices/scanners/gas_analyzer.dm:68  Barometer output for measuring when the next storm happens
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(cooldown))
        {
            to_chat(user, span_warning(I18n.T("[]'s barometer function is preparing itself.", this)));
            return CLICK_ACTION_BLOCKING;
        }

        turf T = get_turf(user);
        if (T == null)
            return CLICK_ACTION_BLOCKING;

        playsound(this, Dm.resource("sound/effects/pop.ogg"), 100);
        area user_area = T.loc as area;
        datum_weather ongoing_weather = null;

        if (!Dm.truthy(user_area.outdoors))
        {
            to_chat(user, span_warning(I18n.T("[]'s barometer function won't work indoors!", this)));
            return CLICK_ACTION_BLOCKING;
        }

        foreach (datum_weather W in Dm.each_cast<datum_weather>(SSweather.processing))
        {
            if (Dm.truthy(Dm.bitand(W.weather_flags, WEATHER_BAROMETER)) && Dm.@in(T.z, W.impacted_z_levels) && W.area_type == user_area.type && !(W.stage == END_STAGE))
            {
                ongoing_weather = W;
                break;
            }
        }

        if (ongoing_weather != null)
        {
            if ((ongoing_weather.stage == MAIN_STAGE) || (ongoing_weather.stage == WIND_DOWN_STAGE))
            {
                to_chat(user, span_warning(ongoing_weather.stage == MAIN_STAGE
                    ? I18n.T("[]'s barometer function can't trace anything while the storm is already here!", this)
                    : I18n.T("[]'s barometer function can't trace anything while the storm is winding down.", this)));
                return CLICK_ACTION_BLOCKING;
            }

            to_chat(user, span_notice(I18n.T("The next [] will hit in [].", ongoing_weather.name, butchertime(ongoing_weather.next_hit_time - world.time))));
            if (!Dm.truthy(Dm.bitand(ongoing_weather.weather_flags, FUNCTIONAL_WEATHER)))
                to_chat(user, span_warning(I18n.T("[]'s barometer function says that the next storm will breeze on by.", this)));
        }
        else
        {
            object next_hit = SSweather.next_hit_by_zlevel[Dm.text("[]", T.z)];
            float @fixed = Dm.truthy(next_hit) ? Dm.num(timeleft(next_hit)) : -1;
            if (@fixed < 0)
                to_chat(user, span_warning(I18n.T("[]'s barometer function was unable to trace any weather patterns.", this)));
            else
                to_chat(user, span_warning(I18n.T("[]'s barometer function says a storm will land in approximately [].", this, butchertime(@fixed))));
        }
        cooldown = TRUE;
        _addtimer(Dm.@new<datum_callback>(this, nameof(ping)), cooldown_time, file: "code/game/objects/items/devices/scanners/gas_analyzer.dm", line: 109);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/devices/scanners/gas_analyzer.dm:111
    public virtual void ping()
    {
        if (isliving(loc))
        {
            mob_living L = loc as mob_living;
            to_chat(L, span_notice(I18n.T("[]'s barometer function is ready!", this)));
        }
        playsound(this, Dm.resource("sound/machines/click.ogg"), 100);
        cooldown = FALSE;
    }

    /// Applies the barometer inaccuracy to the gas reading.
    // tg: code/game/objects/items/devices/scanners/gas_analyzer.dm:119
    public virtual object butchertime(object amount = null)
    {
        if (!Dm.truthy(amount))
            return null;
        if (Dm.truthy(barometer_accuracy))
        {
            float inaccurate = round(Dm.num(barometer_accuracy) * Dm.div(1, 3));
            if (prob(50))
                amount = Dm.sub(amount, inaccurate);
            if (prob(50))
                amount = Dm.add(amount, inaccurate);
        }
        return DisplayTimeText(max(1, Dm.num(amount)));
    }

    // tg: code/game/objects/items/devices/scanners/gas_analyzer.dm:129
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "GasAnalyzer", "Gas Analyzer");
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/items/devices/scanners/gas_analyzer.dm:134
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        return return_atmos_handbooks();
    }

    // tg: code/game/objects/items/devices/scanners/gas_analyzer.dm:137
    public override async DmTask<object> ui_data(mob user = null)
    {
        obj_item last_scanned_real = last_scanned?.resolve() as obj_item;
        if (!QDELETED(last_scanned_real) && Dm.truthy(can_see(user, last_scanned_real, ranged_scan_distance)))
            collect_scan_info(last_scanned_real); // updates last_gasmix_data as long as we're in range

        return Dm.list(
            Dm.kv("gasmixes", last_gasmix_data)
        );
    }

    /// Checks if we can use the analyzer at all
    // tg: code/game/objects/items/devices/scanners/gas_analyzer.dm:147
    public virtual bool can_use(mob user = null)
    {
        if (!Dm.truthy(user.can_read(this)))
            return false;
        // Logical, but it contains "tutorial information", so we should allow it.
        // if(user.is_blind())
        // 	return FALSE
        return true;
    }

    // tg: code/game/objects/items/devices/scanners/gas_analyzer.dm:155
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        return can_use(user) ? await base.ui_status(user, state) : UI_CLOSE;
    }

    // tg: code/game/objects/items/devices/scanners/gas_analyzer.dm:158
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        scan_atom(get_turf(this), user as mob_living);
        return TRUE;
    }

    // tg: code/game/objects/items/devices/scanners/gas_analyzer.dm:162
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        await ui_interact(user);
        return TRUE;
    }

    // tg: code/game/objects/items/devices/scanners/gas_analyzer.dm:166
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (istype(interacting_with, typeof(obj_effect_anomaly)) && Dm.truthy(can_see(user, interacting_with, ranged_scan_distance)))
        {
            obj_effect_anomaly ranged_anomaly = interacting_with as obj_effect_anomaly;
            ranged_anomaly.analyzer_act(user, this);
            return ITEM_INTERACT_SUCCESS;
        }

        return await interact_with_atom(interacting_with, user, modifiers);
    }

    // tg: code/game/objects/items/devices/scanners/gas_analyzer.dm:174
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!HAS_TRAIT(interacting_with, TRAIT_COMBAT_MODE_SKIP_INTERACTION) && Dm.truthy(can_see(user, interacting_with, ranged_scan_distance)))
            scan_atom(Dm.truthy(interacting_with.return_analyzable_air()) ? interacting_with : get_turf(interacting_with), user);
        return NONE; // Non-blocking
    }

    // tg: code/game/objects/items/devices/scanners/gas_analyzer.dm:180
    public virtual void scan_atom(atom target = null, mob_living user = null)
    {
        if (!can_use(user))
            return;

        atmos_scan(user, target, silent: FALSE);
        collect_scan_info(target);
    }

    // tg: code/game/objects/items/devices/scanners/gas_analyzer.dm:187
    public virtual void collect_scan_info(atom target = null)
    {
        object mixture = target.return_analyzable_air();
        if (!Dm.truthy(mixture))
            return;

        DmList airs = islist(mixture) ? (DmList)mixture : Dm.list(mixture);
        DmList new_gasmix_data = Dm.list();
        foreach (datum_gas_mixture air in Dm.each_cast<datum_gas_mixture>(airs))
        {
            string mix_name = Dm.str(capitalize(LOWER_TEXT(target.name)));
            if (airs.len != 1) //not a unary gas mixture
                mix_name += Dm.text(" - Node []", airs.Find(air));
            Dm.append(new_gasmix_data, Dm.list(gas_mixture_parser(air, mix_name)));
        }
        last_gasmix_data = new_gasmix_data;
        last_scanned = WEAKREF(target) as datum_weakref;
    }

    /// Called when our analyzer is used on something
    // tg: code/game/objects/items/devices/scanners/gas_analyzer.dm:203
    [SignalHandler]
    public virtual void on_analyze(datum source = null, atom target = null)
    {
        collect_scan_info(target);
    }
}

public static partial class GlobalProcs
{
    /**
     * Outputs a message to the user describing the target's gasmixes.
     *
     * Gets called by analyzer_act, which in turn is called by tool_act.
     * Also used in other chat-based gas scans.
     */
    // tg: code/game/objects/items/devices/scanners/gas_analyzer.dm:213
    public static object atmos_scan(mob user = null, atom target = null, float? silent = null)
    {
        silent ??= FALSE;
        object mixture = target.return_analyzable_air();
        if (!Dm.truthy(mixture))
            return FALSE;

        // tg: :218  var/icon = target (icon2html's argument)
        DmList message = Dm.list();
        if (!Dm.truthy(silent) && isliving(user))
        {
            playsound(user, SFX_INDUSTRIAL_SCAN, 20, TRUE, -2, TRUE, FALSE);
            // [icon2html(icon, viewers(user))] and [icon2html(icon, user)] before the target: the target's picture in the chat line
            Dm.Deferred("code/game/objects/items/devices/scanners/gas_analyzer.dm:222", "atmos_scan: icon2html(icon, ...), the target's icon in the chat line (chat icons, M10+)");
            user.visible_message(span_notice(I18n.T("[] uses the analyzer on [].", user, target)), span_notice(I18n.T("You use the analyzer on [].", target)));
        }
        Dm.append(message, span_boldnotice(I18n.T("Results of analysis of [].", target)));

        DmList airs = islist(mixture) ? (DmList)mixture : Dm.list(mixture);
        foreach (datum_gas_mixture air in Dm.each_cast<datum_gas_mixture>(airs))
        {
            string mix_name = Dm.str(capitalize(LOWER_TEXT(target.name)));
            if (airs.len > 1) //not a unary gas mixture
            {
                float mix_number = airs.Find(air);
                Dm.append(message, span_boldnotice(I18n.T("Node []", mix_number)));
                mix_name += Dm.text(" - Node []", mix_number);
            }

            float total_moles = Dm.num(air.total_moles());
            float pressure = Dm.num(air.return_pressure());
            float volume = Dm.num(air.return_volume()); //could just do mixture.volume... but safety, I guess?
            float temperature = Dm.num(air.return_temperature());
            object heat_capacity = air.heat_capacity();
            object thermal_energy = air.thermal_energy();

            if (total_moles > 0)
            {
                Dm.append(message, span_notice(I18n.T("Moles: [] mol", round(total_moles, 0.01f))));

                DmList cached_gas_name = (DmList)GLOB.meta_gas_info[META_GAS_NAME]; // GAS_META[META_GAS_NAME]
                foreach ((object id, object amount) in Dm.pairs(air.moles))
                {
                    float gas_concentration = Dm.num(Dm.div(amount, total_moles));
                    Dm.append(message, span_notice(I18n.T("[]: [] mol ([] %)", cached_gas_name[id], round(Dm.num(amount), 0.01f), round(gas_concentration * 100, 0.01f))));
                }
                Dm.append(message, span_notice(I18n.T("Temperature: [] &deg;C ([] K)", round(temperature - T0C, 0.01f), round(temperature, 0.01f))));
                Dm.append(message, span_notice(I18n.T("Volume: [] L", volume)));
                Dm.append(message, span_notice(I18n.T("Pressure: [] kPa", round(pressure, 0.01f))));
                Dm.append(message, span_notice(I18n.T("Heat Capacity: [] / K", display_energy(heat_capacity))));
                Dm.append(message, span_notice(I18n.T("Thermal Energy: []", display_energy(thermal_energy))));
            }
            else
            {
                Dm.append(message, airs.len > 1 ? span_notice(I18n.T("This node is empty!")) : span_notice(I18n.T("[] is empty!", target)));
                Dm.append(message, span_notice(I18n.T("Volume: [] L", volume))); // don't want to change the order volume appears in, suck it
            }
        }

        // we let the join apply newlines so we do need handholding
        // boxed_message(jointext(message, "\n")) (__DEFINES/chat.dm:48), inline
        to_chat(user, "<div class='boxed_message'>" + jointext(message, "\n") + "</div>", type: MESSAGE_TYPE_INFO);
        return TRUE;
    }
}

// tg: code/game/objects/items/devices/scanners/gas_analyzer.dm:260
public partial class obj_item_analyzer_ranged : obj_item_analyzer
{
    // tg: code/game/objects/items/devices/scanners/gas_analyzer.dm:268
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_mercury), 5), Dm.kv(typeof(datum_reagent_iron), 5), Dm.kv(typeof(datum_reagent_silicon), 5));
    }
}
