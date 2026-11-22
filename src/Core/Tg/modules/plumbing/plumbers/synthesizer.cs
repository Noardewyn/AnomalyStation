// Translated from tgstation (snapshot 1d99bd7f): code/modules/plumbing/plumbers/synthesizer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E15 (plumbing): the whole file. The var blocks are generated (D111); the var/static lists are per-world state
// (rules 10.6).

///A single machine that produces a single chem. Can be placed in unison with others through plumbing to create chemical factories
// tg: code/modules/plumbing/plumbers/synthesizer.dm:2
public partial class obj_machinery_plumbing_synthesizer : obj_machinery_plumbing
{
    ///I track them here because I have no idea how I'd make tgui loop like that
    // tg: code/modules/plumbing/plumbers/synthesizer.dm:11  var/static/list/possible_amounts (rules 10.6)
    public static ref DmList possible_amounts => ref TgState.Current.obj_machinery_plumbing_synthesizer__possible_amounts;
    ///straight up copied from chem dispenser. Being a subtype would be extremely tedious and making it global would restrict potential subtypes using different dispensable_reagents
    // tg: code/modules/plumbing/plumbers/synthesizer.dm:15  var/static/list/default_reagents (rules 10.6)
    public static ref DmList default_reagents => ref TgState.Current.obj_machinery_plumbing_synthesizer__default_reagents;

    // tg: code/modules/plumbing/plumbers/synthesizer.dm:45
    [DmParams("mapload", "layer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object layer = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_simple_supply), layer));
        dispensable_reagents = default_reagents;
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/synthesizer.dm:50
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(is_operational) || reagent_id == null || !Dm.truthy(amount))
            return null;

        //otherwise we get leftovers, and we need this to be precise
        if (((datum_reagents)reagents).total_volume >= amount)
            return null;
        Waiting.NoSleep(((datum_reagents)reagents).add_reagent(reagent_id, amount));

        Waiting.NoSleep(use_energy(active_power_usage * Dm.num(seconds_per_tick)));
        return null;
    }

    // tg: code/modules/plumbing/plumbers/synthesizer.dm:61
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ChemSynthesizer", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/plumbing/plumbers/synthesizer.dm:67
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList dot = await base.ui_static_data(user) as DmList;
        dot["possible_amounts"] = possible_amounts;
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/synthesizer.dm:71
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = Dm.list();
        dot["amount"] = amount;

        float is_hallucinating = FALSE;
        if (isliving(user))
        {
            mob_living living_user = (mob_living)user;
            is_hallucinating = Dm.truthy(living_user.has_status_effect(typeof(datum_status_effect_hallucination))) ? TRUE : FALSE;
        }
        DmList chemicals = Dm.list();

        foreach (object reagentID in Dm.each(dispensable_reagents))
        {
            // var/datum/reagent/reagent = GLOB.chemical_reagents_list[reagentID]: GAP(lead): the list is not built (the holder's
            // add_reagent GAP, holder.cs); the type's prototype (its initial values, read only) stands in for the entry, and "no
            // entry" is the list's own test: not a valid /datum/reagent subtype.
            Type reagent_path = reagentID as Type;
            datum_reagent reagent = ispath(reagent_path, typeof(datum_reagent)) && reagent_path != typeof(datum_reagent) && !Dm.@in(reagent_path, get_abstract_types()) ? Dm.initial(reagent_path, (datum_reagent p) => p) : null;
            if (reagent != null)
            {
                object chemname = reagent.name;
                if (Dm.truthy(is_hallucinating) && prob(5))
                    chemname = Dm.text("[]", pick_list_replacements("hallucination.json", "chemicals"));
                chemicals = Dm.add(chemicals, Dm.list(Dm.list(Dm.kv("title", chemname), Dm.kv("id", reagent.name)))) as DmList;
            }
        }
        dot["chemicals"] = chemicals;

        dot["current_reagent"] = reagent_id != null ? Dm.initial(reagent_id, (datum_reagent r) => r.name) : null;
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/synthesizer.dm:92
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "amount":
            {
                float? new_amount = text2num(@params["target"]);
                if (Dm.@in(new_amount, possible_amounts))
                {
                    amount = new_amount;
                    dot = TRUE;
                }
                break;
            }

            case "select":
            {
                object new_reagent = GLOB.name2reagent[@params["reagent"]];
                if (Dm.@in(new_reagent, dispensable_reagents))
                {
                    reagent_id = new_reagent as Type;
                    dot = TRUE;
                }
                break;
            }
        }

        update_appearance();
        await ((datum_reagents)reagents).clear_reagents();
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/synthesizer.dm:113
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        mutable_appearance r_overlay = (mutable_appearance)mutable_appearance(icon, Dm.text("[]_overlay", icon_state));
        r_overlay.color = reagent_id != null ? Dm.initial(reagent_id, (datum_reagent r) => r.color) : COLOR_WHITE;
        Dm.append(ref dot, r_overlay);
        return dot;
    }
}

// tg: code/modules/plumbing/plumbers/synthesizer.dm:119
public partial class obj_machinery_plumbing_synthesizer_soda : obj_machinery_plumbing_synthesizer
{
    //Copied from soda dispenser
    // tg: code/modules/plumbing/plumbers/synthesizer.dm:125  var/static/list/soda_reagents (rules 10.6)
    public static ref DmList soda_reagents => ref TgState.Current.obj_machinery_plumbing_synthesizer_soda__soda_reagents;

    // tg: code/modules/plumbing/plumbers/synthesizer.dm:151
    [DmParams("mapload", "bolt", "layer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        dispensable_reagents = soda_reagents;
        return dot;
    }
}

// tg: code/modules/plumbing/plumbers/synthesizer.dm:156
public partial class obj_machinery_plumbing_synthesizer_beer : obj_machinery_plumbing_synthesizer
{
    //Copied from beer dispenser
    // tg: code/modules/plumbing/plumbers/synthesizer.dm:163  var/static/list/beer_reagents (rules 10.6)
    public static ref DmList beer_reagents => ref TgState.Current.obj_machinery_plumbing_synthesizer_beer__beer_reagents;

    // tg: code/modules/plumbing/plumbers/synthesizer.dm:188
    [DmParams("mapload", "bolt", "layer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        dispensable_reagents = beer_reagents;
        return dot;
    }
}

// tg: code/modules/plumbing/plumbers/synthesizer.dm:193
public partial class obj_machinery_plumbing_synthesizer_mining : obj_machinery_plumbing_synthesizer
{
    // tg: code/modules/plumbing/plumbers/synthesizer.dm:197  var/static/list/mining_chems (rules 10.6)
    public static ref DmList mining_chems => ref TgState.Current.obj_machinery_plumbing_synthesizer_mining__mining_chems;

    // tg: code/modules/plumbing/plumbers/synthesizer.dm:209
    [DmParams("mapload", "layer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        dispensable_reagents = mining_chems;
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/plumbing/plumbers/synthesizer.dm:11
    internal DmList obj_machinery_plumbing_synthesizer__possible_amounts = Dm.list(0f, 1f, 2f, 3f, 4f, 5f);

    // tg: code/modules/plumbing/plumbers/synthesizer.dm:15
    internal DmList obj_machinery_plumbing_synthesizer__default_reagents = Dm.list(
        typeof(datum_reagent_aluminium),
        typeof(datum_reagent_bromine),
        typeof(datum_reagent_carbon),
        typeof(datum_reagent_chlorine),
        typeof(datum_reagent_copper),
        typeof(datum_reagent_consumable_ethanol),
        typeof(datum_reagent_fluorine),
        typeof(datum_reagent_hydrogen),
        typeof(datum_reagent_iodine),
        typeof(datum_reagent_iron),
        typeof(datum_reagent_lithium),
        typeof(datum_reagent_mercury),
        typeof(datum_reagent_nitrogen),
        typeof(datum_reagent_oxygen),
        typeof(datum_reagent_phosphorus),
        typeof(datum_reagent_potassium),
        typeof(datum_reagent_uranium_radium),
        typeof(datum_reagent_silicon),
        typeof(datum_reagent_sodium),
        typeof(datum_reagent_stable_plasma),
        typeof(datum_reagent_consumable_sugar),
        typeof(datum_reagent_sulfur),
        typeof(datum_reagent_toxin_acid),
        typeof(datum_reagent_water),
        typeof(datum_reagent_fuel)
    );

    // tg: code/modules/plumbing/plumbers/synthesizer.dm:125
    internal DmList obj_machinery_plumbing_synthesizer_soda__soda_reagents = Dm.list(
        typeof(datum_reagent_consumable_coffee),
        typeof(datum_reagent_consumable_space_cola),
        typeof(datum_reagent_consumable_cream),
        typeof(datum_reagent_consumable_dr_gibb),
        typeof(datum_reagent_consumable_grenadine),
        typeof(datum_reagent_consumable_ice),
        typeof(datum_reagent_consumable_icetea),
        typeof(datum_reagent_consumable_lemonjuice),
        typeof(datum_reagent_consumable_lemon_lime),
        typeof(datum_reagent_consumable_limejuice),
        typeof(datum_reagent_consumable_menthol),
        typeof(datum_reagent_consumable_orangejuice),
        typeof(datum_reagent_consumable_pineapplejuice),
        typeof(datum_reagent_consumable_pwr_game),
        typeof(datum_reagent_consumable_shamblers),
        typeof(datum_reagent_consumable_spacemountainwind),
        typeof(datum_reagent_consumable_sodawater),
        typeof(datum_reagent_consumable_space_up),
        typeof(datum_reagent_consumable_sugar),
        typeof(datum_reagent_consumable_tea),
        typeof(datum_reagent_consumable_tomatojuice),
        typeof(datum_reagent_consumable_tonic),
        typeof(datum_reagent_water)
    );

    // tg: code/modules/plumbing/plumbers/synthesizer.dm:163
    internal DmList obj_machinery_plumbing_synthesizer_beer__beer_reagents = Dm.list(
        typeof(datum_reagent_consumable_ethanol_absinthe),
        typeof(datum_reagent_consumable_ethanol_ale),
        typeof(datum_reagent_consumable_ethanol_applejack),
        typeof(datum_reagent_consumable_ethanol_beer),
        typeof(datum_reagent_consumable_ethanol_cognac),
        typeof(datum_reagent_consumable_ethanol_creme_de_cacao),
        typeof(datum_reagent_consumable_ethanol_creme_de_coconut),
        typeof(datum_reagent_consumable_ethanol_creme_de_menthe),
        typeof(datum_reagent_consumable_ethanol_curacao),
        typeof(datum_reagent_consumable_ethanol_gin),
        typeof(datum_reagent_consumable_ethanol_hcider),
        typeof(datum_reagent_consumable_ethanol_kahlua),
        typeof(datum_reagent_consumable_ethanol_beer_maltliquor),
        typeof(datum_reagent_consumable_ethanol_navy_rum),
        typeof(datum_reagent_consumable_ethanol_rum),
        typeof(datum_reagent_consumable_ethanol_sake),
        typeof(datum_reagent_consumable_ethanol_tequila),
        typeof(datum_reagent_consumable_ethanol_triple_sec),
        typeof(datum_reagent_consumable_ethanol_vermouth),
        typeof(datum_reagent_consumable_ethanol_vodka),
        typeof(datum_reagent_consumable_ethanol_whiskey),
        typeof(datum_reagent_consumable_ethanol_wine)
    );

    // tg: code/modules/plumbing/plumbers/synthesizer.dm:197
    internal DmList obj_machinery_plumbing_synthesizer_mining__mining_chems = Dm.list(
        typeof(datum_reagent_toxin_acid),
        typeof(datum_reagent_toxin_acid_fluacid),
        typeof(datum_reagent_toxin_acid_nitracid),
        typeof(datum_reagent_teslium),
        typeof(datum_reagent_fuel),
        typeof(datum_reagent_thermite),
        typeof(datum_reagent_gunpowder),
        typeof(datum_reagent_liquid_dark_matter),
        typeof(datum_reagent_toxin_acid_industrial_waste)
    );
}
