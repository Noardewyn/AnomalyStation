// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/bounties/reagent.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4c (D298): the whole file, the reagent and pill bounties. The reagent bounty declares wanted_reagent as object (the soup bounty
// stores a soup's typepath where the others store a reagent, rules 3.2), so its var block is hand-written (DV409); the others are
// generated.

// tg: code/modules/cargo/bounties/reagent.dm:1
public partial class datum_bounty_reagent : datum_bounty
{
    // tg: code/modules/cargo/bounties/reagent.dm:2
    public float required_volume = 10;
    // tg: code/modules/cargo/bounties/reagent.dm:3
    public float shipped_volume = 0;
    // tg: code/modules/cargo/bounties/reagent.dm:4  object (rules 3.2): a reagent, or the soup bounty's typepath
    public object wanted_reagent;

    /// DEVIATION(DV409): tg reads `wanted_reagent.type`, a var of the soup bounty's typepath; the type of the reagent, or the typepath itself
    protected Type WantedType => wanted_reagent is datum_reagent reagent ? reagent.GetType() : wanted_reagent as Type;

    // tg: code/modules/cargo/bounties/reagent.dm:6
    public override object can_claim() => shipped_volume >= required_volume ? TRUE : FALSE;

    // tg: code/modules/cargo/bounties/reagent.dm:9
    public override object print_required() => Dm.text("[]/[] u", shipped_volume, required_volume);

    // tg: code/modules/cargo/bounties/reagent.dm:12
    public override object applies_to(obj shipped = null)
    {
        if (!is_reagent_container(shipped))
            return FALSE;
        if (shipped.reagents == null || !Dm.truthy(((datum_reagents)shipped.reagents).has_reagent(WantedType)))
            return FALSE;
        if (Dm.truthy(Dm.bitand(shipped.flags_1, HOLOGRAM_1)))
            return FALSE;
        return shipped_volume < required_volume ? TRUE : FALSE;
    }

    // tg: code/modules/cargo/bounties/reagent.dm:21
    public override async DmTask<object> ship(obj shipped = null)
    {
        if (!Dm.truthy(applies_to(shipped)))
            return FALSE;
        shipped_volume += Dm.num(((datum_reagents)shipped.reagents).get_reagent_amount(WantedType));
        if (shipped_volume > required_volume)
            shipped_volume = required_volume;
        return TRUE;
    }

    // tg: code/modules/cargo/bounties/reagent.dm:29
    public override object contribution_amount(obj shipped = null) => ((datum_reagents)shipped.reagents).get_reagent_amount(WantedType);

    // tg: code/modules/cargo/bounties/reagent.dm:32
    public override object get_total() => shipped_volume;

    // tg: code/modules/cargo/bounties/reagent.dm:35
    public override object get_max() => required_volume;

    // tg: the four reagent bounties' New: a random reagent of their list, its name, the description and the reward (rules 4.13)
    protected void pick_reagent(DmList possible_reagents, string description_format, float reward_step, int max_steps)
    {
        var reagent_type = (Type)pick(possible_reagents);
        wanted_reagent = Dm.@new(reagent_type);
        name = ((datum_reagent)wanted_reagent).name;
        description = I18n.T(description_format, name);
        reward = Dm.num(reward) + rand(0, max_steps) * reward_step;
    }
}

// tg: code/modules/cargo/bounties/reagent.dm:39
public partial class datum_bounty_reagent_simple_drink : datum_bounty_reagent
{
    // tg: code/modules/cargo/bounties/reagent.dm:43
    public override async DmTask New(params object[] __extra)
    {
        // Don't worry about making this comprehensive. It doesn't matter if some drinks are skipped.
        DmList possible_reagents = Dm.list(
            typeof(datum_reagent_consumable_ethanol_antifreeze),
            typeof(datum_reagent_consumable_ethanol_andalusia),
            typeof(datum_reagent_consumable_tea_arnold_palmer),
            typeof(datum_reagent_consumable_ethanol_b52),
            typeof(datum_reagent_consumable_ethanol_bananahonk),
            typeof(datum_reagent_consumable_ethanol_beepsky_smash),
            typeof(datum_reagent_consumable_ethanol_between_the_sheets),
            typeof(datum_reagent_consumable_ethanol_bilk),
            typeof(datum_reagent_consumable_ethanol_black_russian),
            typeof(datum_reagent_consumable_ethanol_bloody_mary),
            typeof(datum_reagent_consumable_ethanol_brave_bull),
            typeof(datum_reagent_consumable_ethanol_martini),
            typeof(datum_reagent_consumable_ethanol_cuba_libre),
            typeof(datum_reagent_consumable_ethanol_eggnog),
            typeof(datum_reagent_consumable_ethanol_erikasurprise),
            typeof(datum_reagent_consumable_ethanol_ginfizz),
            typeof(datum_reagent_consumable_ethanol_gintonic),
            typeof(datum_reagent_consumable_ethanol_grappa),
            typeof(datum_reagent_consumable_ethanol_grog),
            typeof(datum_reagent_consumable_ethanol_hooch),
            typeof(datum_reagent_consumable_ethanol_iced_beer),
            typeof(datum_reagent_consumable_ethanol_irishcarbomb),
            typeof(datum_reagent_consumable_ethanol_manhattan),
            typeof(datum_reagent_consumable_ethanol_margarita),
            typeof(datum_reagent_consumable_ethanol_gargle_blaster),
            typeof(datum_reagent_consumable_ethanol_rum_coke),
            typeof(datum_reagent_consumable_ethanol_screwdrivercocktail),
            typeof(datum_reagent_consumable_ethanol_snowwhite),
            typeof(datum_reagent_consumable_soy_latte),
            typeof(datum_reagent_consumable_cafe_latte),
            typeof(datum_reagent_consumable_ethanol_syndicatebomb),
            typeof(datum_reagent_consumable_ethanol_tequila_sunrise),
            typeof(datum_reagent_consumable_ethanol_manly_dorf),
            typeof(datum_reagent_consumable_ethanol_thirteenloko),
            typeof(datum_reagent_consumable_triple_citrus),
            typeof(datum_reagent_consumable_ethanol_vodkamartini),
            typeof(datum_reagent_consumable_ethanol_whiskeysoda),
            typeof(datum_reagent_consumable_ethanol_beer_green),
            typeof(datum_reagent_consumable_ethanol_demonsblood),
            typeof(datum_reagent_consumable_ethanol_crevice_spike),
            typeof(datum_reagent_consumable_ethanol_singulo),
            typeof(datum_reagent_consumable_ethanol_whiskey_sour),
            typeof(datum_reagent_consumable_ethanol_boston_sour),
            typeof(datum_reagent_consumable_ethanol_old_fashioned),
            typeof(datum_reagent_consumable_ethanol_sazerac),
            typeof(datum_reagent_consumable_ethanol_hot_toddy),
            typeof(datum_reagent_consumable_ethanol_daiquiri),
            typeof(datum_reagent_consumable_ethanol_blue_blazer),
            typeof(datum_reagent_consumable_ethanol_flip_cocktail),
            typeof(datum_reagent_consumable_ethanol_bitters_soda),
            typeof(datum_reagent_consumable_ethanol_star),
            typeof(datum_reagent_consumable_ethanol_brandy_crusta),
            typeof(datum_reagent_consumable_ethanol_casino),
            typeof(datum_reagent_consumable_ethanol_mary_pickford));

        pick_reagent(possible_reagents, "CentCom is thirsty! Send a shipment of [] to CentCom to quench the company's thirst.", 500, 2);
    }
}

// tg: code/modules/cargo/bounties/reagent.dm:108
public partial class datum_bounty_reagent_complex_drink : datum_bounty_reagent
{
    // tg: code/modules/cargo/bounties/reagent.dm:112
    public override async DmTask New(params object[] __extra)
    {
        // Don't worry about making this comprehensive. It doesn't matter if some drinks are skipped.
        DmList possible_reagents = Dm.list(
            typeof(datum_reagent_consumable_ethanol_atomicbomb),
            typeof(datum_reagent_consumable_ethanol_bacchus_blessing),
            typeof(datum_reagent_consumable_ethanol_bastion_bourbon),
            typeof(datum_reagent_consumable_ethanol_booger),
            typeof(datum_reagent_consumable_ethanol_hippies_delight),
            typeof(datum_reagent_consumable_ethanol_drunkenblumpkin),
            typeof(datum_reagent_consumable_ethanol_fetching_fizz),
            typeof(datum_reagent_consumable_ethanol_goldschlager),
            typeof(datum_reagent_consumable_ethanol_manhattan_proj),
            typeof(datum_reagent_consumable_ethanol_narsour),
            typeof(datum_reagent_consumable_ethanol_neurotoxin),
            typeof(datum_reagent_consumable_ethanol_patron),
            typeof(datum_reagent_consumable_ethanol_quadruple_sec),
            typeof(datum_reagent_consumable_bluecherryshake),
            typeof(datum_reagent_consumable_doctor_delight),
            typeof(datum_reagent_consumable_ethanol_silencer),
            typeof(datum_reagent_consumable_ethanol_peppermint_patty),
            typeof(datum_reagent_consumable_ethanol_aloe),
            typeof(datum_reagent_consumable_pumpkin_latte),
            typeof(datum_reagent_consumable_ethanol_ramos_gin_fizz),
            typeof(datum_reagent_consumable_ethanol_sangria),
            typeof(datum_reagent_consumable_ethanol_tizirian_sour),
            typeof(datum_reagent_consumable_ethanol_suffering_bastard),
            typeof(datum_reagent_consumable_ethanol_pousse_cafe),
            typeof(datum_reagent_consumable_ethanol_improved_whiskey),
            typeof(datum_reagent_consumable_ethanol_vieux_carre));

        pick_reagent(possible_reagents, "CentCom is offering a reward for talented mixologists. Ship a container of [] to claim the prize.", 500, 4);
    }
}

// tg: code/modules/cargo/bounties/reagent.dm:149
public partial class datum_bounty_reagent_chemical_simple : datum_bounty_reagent
{
    // tg: code/modules/cargo/bounties/reagent.dm:154
    public override async DmTask New(params object[] __extra)
    {
        // Chemicals that can be mixed by a single skilled Chemist.
        DmList possible_reagents = Dm.list(
            typeof(datum_reagent_medicine_leporazine),
            typeof(datum_reagent_medicine_mine_salve),
            typeof(datum_reagent_medicine_c2_convermol),
            typeof(datum_reagent_medicine_ephedrine),
            typeof(datum_reagent_medicine_diphenhydramine),
            typeof(datum_reagent_drug_space_drugs),
            typeof(datum_reagent_drug_blastoff),
            typeof(datum_reagent_gunpowder),
            typeof(datum_reagent_napalm),
            typeof(datum_reagent_firefighting_foam),
            typeof(datum_reagent_consumable_mayonnaise),
            typeof(datum_reagent_toxin_itching_powder),
            typeof(datum_reagent_toxin_cyanide),
            typeof(datum_reagent_toxin_heparin),
            typeof(datum_reagent_medicine_pen_acid),
            typeof(datum_reagent_medicine_atropine),
            typeof(datum_reagent_drug_aranesp),
            typeof(datum_reagent_drug_krokodil),
            typeof(datum_reagent_drug_methamphetamine),
            typeof(datum_reagent_teslium),
            typeof(datum_reagent_toxin_anacea),
            typeof(datum_reagent_pax));

        pick_reagent(possible_reagents, "CentCom is in desperate need of the chemical []. Ship a container of it to be rewarded.", 500, 4); //4000 to 6000 credits
    }
}

// tg: code/modules/cargo/bounties/reagent.dm:187
public partial class datum_bounty_reagent_chemical_complex : datum_bounty_reagent
{
    // tg: code/modules/cargo/bounties/reagent.dm:192
    public override async DmTask New(params object[] __extra)
    {
        // Reagents that require interaction with multiple departments or are a pain to mix. Lower required_volume since acquiring 30u of some is unrealistic
        DmList possible_reagents = Dm.list(
            typeof(datum_reagent_medicine_pyroxadone),
            typeof(datum_reagent_medicine_rezadone),
            typeof(datum_reagent_medicine_regen_jelly),
            typeof(datum_reagent_drug_bath_salts),
            typeof(datum_reagent_hair_dye),
            typeof(datum_reagent_consumable_honey),
            typeof(datum_reagent_consumable_frostoil),
            typeof(datum_reagent_toxin_slimejelly),
            typeof(datum_reagent_teslium_energized_jelly),
            typeof(datum_reagent_toxin_mimesbane),
            typeof(datum_reagent_medicine_strange_reagent),
            typeof(datum_reagent_nitroglycerin),
            typeof(datum_reagent_medicine_rezadone),
            typeof(datum_reagent_toxin_zombiepowder),
            typeof(datum_reagent_toxin_ghoulpowder),
            typeof(datum_reagent_mulligan));

        pick_reagent(possible_reagents, "CentCom is paying premium for the chemical []. Ship a container of it to be rewarded.", 750, 5); //6000 to 9750 credits
    }
}

// tg: code/modules/cargo/bounties/reagent.dm:219
public partial class datum_bounty_pill : datum_bounty
{
    // tg: code/modules/cargo/bounties/reagent.dm:229
    public override object can_claim() => shipped_ammount >= required_ammount ? TRUE : FALSE;

    // tg: code/modules/cargo/bounties/reagent.dm:232
    public override object applies_to(obj shipped = null)
    {
        if (shipped is not obj_item_reagent_containers_applicator_pill)
            return FALSE;
        if (Dm.num(((datum_reagents)shipped?.reagents)?.get_reagent_amount(wanted_reagent.GetType())) >= wanted_vol)
            return TRUE;
        return FALSE;
    }

    // tg: code/modules/cargo/bounties/reagent.dm:239
    public override async DmTask<object> ship(obj shipped = null)
    {
        if (!Dm.truthy(applies_to(shipped)))
            return FALSE;
        shipped_ammount += 1;
        if (shipped_ammount > required_ammount)
            shipped_ammount = required_ammount;
        return TRUE;
    }
}

// tg: code/modules/cargo/bounties/reagent.dm:247
public partial class datum_bounty_pill_simple_pill : datum_bounty_pill
{
    // tg: code/modules/cargo/bounties/reagent.dm:251
    public override async DmTask New(params object[] __extra)
    {
        //reagent that are possible to be chem factory'd
        DmList possible_reagents = Dm.list(
            typeof(datum_reagent_medicine_spaceacillin),
            typeof(datum_reagent_medicine_c2_synthflesh),
            typeof(datum_reagent_medicine_pen_acid),
            typeof(datum_reagent_medicine_atropine),
            typeof(datum_reagent_medicine_cryoxadone),
            typeof(datum_reagent_medicine_salbutamol),
            typeof(datum_reagent_medicine_c2_hercuri),
            typeof(datum_reagent_medicine_c2_probital),
            typeof(datum_reagent_drug_methamphetamine),
            typeof(datum_reagent_nitrous_oxide),
            typeof(datum_reagent_barbers_aid),
            typeof(datum_reagent_pax),
            typeof(datum_reagent_flash_powder),
            typeof(datum_reagent_phlogiston),
            typeof(datum_reagent_firefighting_foam));

        var reagent_type = (Type)pick(possible_reagents);
        wanted_reagent = Dm.@new(reagent_type) as datum_reagent;
        name = I18n.T("[] pills", wanted_reagent.name);
        required_ammount += rand(1, 60);
        wanted_vol += rand(1, 20);
        description = I18n.T("CentCom requires [] of [] containing at least [] each. Ship a container of it to be rewarded.", required_ammount, name, wanted_vol);
        reward = Dm.num(reward) + rand(1, 5) * (CARGO_CRATE_VALUE * 6);
    }
}
