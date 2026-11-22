// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/atmosscan.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI2, D326: on the cyborg's tablet), its window NtosGasAnalyzer. The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/atmosscan.dm:6
public partial class datum_computer_file_program_atmosscan : datum_computer_file_program
{
    /// Scan the turf where the computer is on.
    private const string ATMOZPHERE_SCAN_ENV = "env";
    /// Scan the objects that the tablet clicks.
    private const string ATMOZPHERE_SCAN_CLICK = "click";

    /// Secondary attack self.
    // tg: code/modules/modular_computers/file_system/programs/atmosscan.dm:22
    [SignalHandler]
    public virtual object turf_analyze(datum source = null, mob user = null)
    {
        if (!Dm.eq(atmozphere_mode, ATMOZPHERE_SCAN_CLICK))
            return null;
        atmos_scan(user: user, target: get_turf(computer), silent: FALSE);
        on_analyze(source: source, target: get_turf(computer));
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    /// Keep this in sync with its tool based counterpart [/obj/proc/analyzer_act] and [/atom/proc/tool_act]
    // tg: code/modules/modular_computers/file_system/programs/atmosscan.dm:31
    public override async DmTask<object> tap(atom tapped_atom = null, mob_living user = null, DmList modifiers = null)
    {
        if (!Dm.eq(atmozphere_mode, ATMOZPHERE_SCAN_CLICK))
            return FALSE;
        if (!Dm.truthy(atmos_scan(user, tapped_atom)))
            return FALSE;
        on_analyze(computer, tapped_atom);
        return TRUE;
    }

    /// Updates our gasmix data if on click mode.
    // tg: code/modules/modular_computers/file_system/programs/atmosscan.dm:40
    public virtual object on_analyze(datum source = null, atom target = null)
    {
        object mixture = target.return_analyzable_air();
        if (!Dm.truthy(mixture))
            return FALSE;
        DmList airs = islist(mixture) ? mixture as DmList : Dm.list(mixture);
        DmList new_gasmix_data = Dm.list();
        foreach (datum_gas_mixture air in Dm.each_cast<datum_gas_mixture>(airs))
        {
            string mix_name = Dm.str(capitalize(LOWER_TEXT(target.name)));
            if (airs.len != 1) //not a unary gas mixture
                mix_name += Dm.text(" - Node []", airs.Find(air));
            Dm.append(ref new_gasmix_data, Dm.list(gas_mixture_parser(air, mix_name)));
        }
        last_gasmix_data = new_gasmix_data;
        return null;
    }

    // tg: code/modules/modular_computers/file_system/programs/atmosscan.dm:53
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        return return_atmos_handbooks();
    }

    // tg: code/modules/modular_computers/file_system/programs/atmosscan.dm:56
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        turf turf = get_turf(computer);
        data["atmozphereMode"] = atmozphere_mode;
        data["clickAtmozphereCompatible"] = Dm.bitand(computer.hardware_flag, PROGRAM_PDA);
        switch (atmozphere_mode as string) //Null air wont cause errors, don't worry.
        {
            case ATMOZPHERE_SCAN_ENV:
                datum_gas_mixture air = turf != null ? await turf.return_air() as datum_gas_mixture : null;
                data["gasmixes"] = Dm.list(gas_mixture_parser(air, "Location Reading"));
                break;
            case ATMOZPHERE_SCAN_CLICK:
                data["gasmixes"] = last_gasmix_data;
                break;
        }
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/atmosscan.dm:69
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        switch (action as string)
        {
            case "scantoggle":
                if (Dm.eq(atmozphere_mode, ATMOZPHERE_SCAN_CLICK))
                {
                    atmozphere_mode = ATMOZPHERE_SCAN_ENV;
                    UnregisterSignal(computer, COMSIG_ITEM_ATTACK_SELF_SECONDARY);
                    return TRUE;
                }
                if (!Dm.truthy(Dm.bitand(computer.hardware_flag, PROGRAM_PDA)))
                {
                    await computer.say(I18n.T("Device incompatible for scanning objects!"));
                    return FALSE;
                }
                atmozphere_mode = ATMOZPHERE_SCAN_CLICK;
                RegisterSignal(computer, COMSIG_ITEM_ATTACK_SELF_SECONDARY, nameof(turf_analyze));
                turf turf = get_turf(computer);
                last_gasmix_data = Dm.list(gas_mixture_parser(turf != null ? await turf.return_air() as datum_gas_mixture : null, "Location Reading"));
                return TRUE;
        }
        return dot;
    }
}
