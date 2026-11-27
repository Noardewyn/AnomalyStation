// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/recipes/slime_extracts.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: every slime extract's reaction. The reactions that only make reagents are var-only subtypes and
// live with their base. The var blocks are generated (D111).

// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:2
public partial class datum_chemical_reaction_slime : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:7
    public override async DmTask<object> pre_reaction_other_checks(datum_reagents holder = null)
    {
        obj_item_slime_extract extract = holder.my_atom as obj_item_slime_extract;
        if (!(extract is obj_item_slime_extract))
            return FALSE;

        return Dm.num(extract.extract_uses > 0);
    }

    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:14
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        use_slime_core(holder);
        return null;
    }

    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:17
    public virtual void use_slime_core(datum_reagents holder = null)
    {
        SSblackbox.record_feedback("tally", "slime_cores_used", 1, "type");
        if (Dm.truthy(deletes_extract))
            delete_extract(holder);
    }

    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:22
    public virtual void delete_extract(datum_reagents holder = null)
    {
        obj_item_slime_extract extract = holder.my_atom as obj_item_slime_extract;
        if (extract != null && extract.extract_uses <= 0 && !Dm.truthy(length(results))) //if the slime doesn't output chemicals
            qdel(extract);
    }
}

//Grey
// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:28
public partial class datum_chemical_reaction_slime_slimespawn : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:32
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        mob_living_basic_slime spawning_slime = Dm.@new<mob_living_basic_slime>(get_turf(holder.my_atom), typeof(datum_slime_type_grey));
        spawning_slime.visible_message(span_danger(I18n.T("Infused with plasma, the core begins to quiver and grow, and a new baby slime emerges from it!")));
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:42
public partial class datum_chemical_reaction_slime_slimemonkey : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:46
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        for (float i = 1; i <= 3; i++)
            Dm.@new<obj_item_food_monkeycube>(get_turf(holder.my_atom));
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

//Metal
// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:83
public partial class datum_chemical_reaction_slime_slimemetal : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:87
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf location = get_turf(holder.my_atom);
        Dm.@new<obj_item_stack_sheet_plasteel>(location, 5);
        Dm.@new<obj_item_stack_sheet_iron>(location, 15);
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:93
public partial class datum_chemical_reaction_slime_slimeglass : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:97
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf location = get_turf(holder.my_atom);
        Dm.@new<obj_item_stack_sheet_rglass>(location, 5);
        Dm.@new<obj_item_stack_sheet_glass>(location, 15);
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

//Gold
// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:104
public partial class datum_chemical_reaction_slime_slimemobspawn : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:110
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf T = get_turf(holder.my_atom);
        summon_mobs(holder, T);
        obj_item_slime_extract M = holder.my_atom as obj_item_slime_extract;
        deltimer(M.qdel_timer);
        await base.on_reaction(holder, reaction, created_volume, __extra);
        M.qdel_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(delete_extract), holder), 55, TIMER_STOPPABLE, file: "code/modules/reagents/chemistry/recipes/slime_extracts.dm", line: 116);
        return null;
    }

    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:118
    public virtual void summon_mobs(datum_reagents holder = null, turf T = null)
    {
        T.visible_message(span_danger(I18n.T("The slime extract begins to vibrate violently!")));
        _addtimer(Dm.@new<datum_callback>(this, nameof(chemical_mob_spawn), holder, 4, "Gold Slime", HOSTILE_SPAWN), 5 * SECONDS, file: "code/modules/reagents/chemistry/recipes/slime_extracts.dm", line: 120);
    }
}

// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:122
public partial class datum_chemical_reaction_slime_slimemobspawn_lesser : datum_chemical_reaction_slime_slimemobspawn
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:125
    public override void summon_mobs(datum_reagents holder = null, turf T = null)
    {
        T.visible_message(span_danger(I18n.T("The slime extract begins to vibrate violently!")));
        _addtimer(Dm.@new<datum_callback>(this, nameof(chemical_mob_spawn), holder, 3, "Lesser Gold Slime", HOSTILE_SPAWN, FACTION_NEUTRAL), 5 * SECONDS, file: "code/modules/reagents/chemistry/recipes/slime_extracts.dm", line: 127);
    }
}

// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:129
public partial class datum_chemical_reaction_slime_slimemobspawn_friendly : datum_chemical_reaction_slime_slimemobspawn
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:132
    public override void summon_mobs(datum_reagents holder = null, turf T = null)
    {
        T.visible_message(span_danger(I18n.T("The slime extract begins to vibrate adorably!")));
        _addtimer(Dm.@new<datum_callback>(this, nameof(chemical_mob_spawn), holder, 1, "Friendly Gold Slime", FRIENDLY_SPAWN, FACTION_NEUTRAL), 5 * SECONDS, file: "code/modules/reagents/chemistry/recipes/slime_extracts.dm", line: 134);
    }
}

// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:136
public partial class datum_chemical_reaction_slime_slimemobspawn_spider : datum_chemical_reaction_slime_slimemobspawn
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:140
    public override void summon_mobs(datum_reagents holder = null, turf T = null)
    {
        T.visible_message(span_danger(I18n.T("The slime extract begins to vibrate crikey-ingly!")));
        _addtimer(Dm.@new<datum_callback>(this, nameof(chemical_mob_spawn), holder, 3, "Traitor Spider Slime", typeof(mob_living_basic_spider_giant_midwife), FACTION_NEUTRAL, FALSE), 5 * SECONDS, file: "code/modules/reagents/chemistry/recipes/slime_extracts.dm", line: 142);
    }
}

//Silver
// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:146
public partial class datum_chemical_reaction_slime_slimebork : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:150
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        //BORK BORK BORK
        turf T = get_turf(holder.my_atom);

        playsound(T, "sound/effects/phasein.ogg", 100, TRUE);

        foreach (mob_living_carbon C in Dm.each<mob_living_carbon>(viewers(T, null)))
            await C.flash_act();

        for (float i = 1, i_end = 4 + rand(1, 2); i <= i_end; i++)
        {
            object chosen = getbork();
            obj_item food_item = Dm.@new((Type)chosen, T) as obj_item;
            ADD_TRAIT(food_item, TRAIT_FOOD_SILVER, INNATE_TRAIT);
            if (prob(5))//Fry it!
                await food_item.AddElement(typeof(datum_element_fried_item), rand(15, 60) * SECONDS);
            if (prob(5))//Grill it!
                await food_item.AddElement(typeof(datum_element_grilled_item), rand(30 * SECONDS, 100 * SECONDS));
            if (prob(50))
            {
                for (float j = 1, j_end = rand(1, 3); j <= j_end; j++)
                    await step(food_item, Dm.num(pick(NORTH, SOUTH, EAST, WEST)));
            }
        }
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }

    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:172
    public virtual object getbork()
    {
        return get_random_food();
    }
}

// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:175
public partial class datum_chemical_reaction_slime_slimebork_drinks : datum_chemical_reaction_slime_slimebork
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:178
    public override object getbork()
    {
        return get_random_drink();
    }
}

//Blue
// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:187
public partial class datum_chemical_reaction_slime_slimestabilizer : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:191
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        Dm.@new<obj_item_slimepotion_slime_stabilizer>(get_turf(holder.my_atom));
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:195
public partial class datum_chemical_reaction_slime_slimefoam : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:199
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        // tg passes the notification ("[src] spews out foam!") third, into create_foam's result_type (__HELPERS/reagents.dm:79):
        // no one is told, and each foam tile fails to make the text into an object when it dissolves; the port passes neither,
        // which shows the same: the foam, no message and no result
        await holder.create_foam(typeof(datum_effect_system_fluid_spread_foam), 80, null, null, log: TRUE);
        return null;
    }
}

//Dark Blue
// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:203
public partial class datum_chemical_reaction_slime_slimefreeze : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:208
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf T = get_turf(holder.my_atom);
        T.visible_message(span_danger(I18n.T("The slime extract starts to feel extremely cold!")));
        _addtimer(Dm.@new<datum_callback>(this, nameof(freeze), holder), 5 * SECONDS, file: "code/modules/reagents/chemistry/recipes/slime_extracts.dm", line: 211);
        obj_item_slime_extract M = holder.my_atom as obj_item_slime_extract;
        deltimer(M.qdel_timer);
        await base.on_reaction(holder, reaction, created_volume, __extra);
        M.qdel_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(delete_extract), holder), 55, TIMER_STOPPABLE, file: "code/modules/reagents/chemistry/recipes/slime_extracts.dm", line: 215);
        return null;
    }

    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:217
    public virtual void freeze(datum_reagents holder = null)
    {
        if (holder?.my_atom != null)
        {
            turf_open T = get_turf(holder.my_atom) as turf_open;
            if (T is turf_open)
            {
                Type gastype = typeof(datum_gas_nitrogen);
                T.atmos_spawn_air(Dm.text("[]=50;[]", Dm.initial(gastype, (datum_gas g) => g.id), TURF_TEMPERATURE(2.7f)));
            }
        }
    }
}

// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:224
public partial class datum_chemical_reaction_slime_slimefireproof : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:228
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        Dm.@new<obj_item_slimepotion_fireproof>(get_turf(holder.my_atom));
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

//Orange
// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:238
public partial class datum_chemical_reaction_slime_slimefire : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:243
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf T = get_turf(holder.my_atom);
        T.visible_message(span_danger(I18n.T("The slime extract begins to vibrate adorably!")));
        _addtimer(Dm.@new<datum_callback>(this, nameof(slime_burn), holder), 5 * SECONDS, file: "code/modules/reagents/chemistry/recipes/slime_extracts.dm", line: 246);
        obj_item_slime_extract M = holder.my_atom as obj_item_slime_extract;
        deltimer(M.qdel_timer);
        await base.on_reaction(holder, reaction, created_volume, __extra);
        M.qdel_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(delete_extract), holder), 55, TIMER_STOPPABLE, file: "code/modules/reagents/chemistry/recipes/slime_extracts.dm", line: 250);
        return null;
    }

    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:252
    public virtual void slime_burn(datum_reagents holder = null)
    {
        if (holder?.my_atom != null)
        {
            turf_open T = get_turf(holder.my_atom) as turf_open;
            if (T is turf_open)
                T.atmos_spawn_air(Dm.text("[]=50;[]", GAS_PLASMA, TURF_TEMPERATURE(1000)));
        }
    }
}

//Yellow
// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:265
public partial class datum_chemical_reaction_slime_slimeoverload : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:270
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf turf = get_turf(holder.my_atom);
        object lastkey = holder.my_atom.fingerprintslast;
        await empulse(get_turf(holder.my_atom), 3, 5, emp_source: this);
        if (Dm.truthy(lastkey))
        {
            mob toucher = get_mob_by_key(lastkey);
            toucher.log_message(Dm.text("triggered EMP reaction at [].", AREACOORD(turf)), LOG_GAME, log_globally: FALSE);
        }
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:279
public partial class datum_chemical_reaction_slime_slimecell : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:283  on_reaction(datum/reagents/holder, created_volume): tg's
    // second parameter is named created_volume but takes the slot's reaction argument; the body reads neither
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        Dm.@new<obj_item_stock_parts_power_store_cell_emproof_slime>(get_turf(holder.my_atom));
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:287
public partial class datum_chemical_reaction_slime_slimeglow : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:291
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf T = get_turf(holder.my_atom);
        T.visible_message(span_danger(I18n.T("The slime begins to emit a soft light. Squeezing it will cause it to grow brightly.")));
        Dm.@new<obj_item_flashlight_slime>(T);
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

//Purple
// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:298
public partial class datum_chemical_reaction_slime_slimepsteroid : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:302
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        Dm.@new<obj_item_slimepotion_slime_steroid>(get_turf(holder.my_atom));
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

//Dark Purple
// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:312
public partial class datum_chemical_reaction_slime_slimeplasma : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:316
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        Dm.@new<obj_item_stack_sheet_mineral_plasma>(get_turf(holder.my_atom), 3);
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

//Red
// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:321
public partial class datum_chemical_reaction_slime_slimemutator : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:325
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        Dm.@new<obj_item_slimepotion_slime_mutator>(get_turf(holder.my_atom));
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:329
public partial class datum_chemical_reaction_slime_slimebloodlust : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:334
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        foreach (mob_living_basic_slime slime in Dm.each<mob_living_basic_slime>(viewers(get_turf(holder.my_atom), null)))
        {
            if (Dm.truthy(slime.hunger_disabled)) //Undoes docility, but doesn't make rabid.
            {
                slime.visible_message(span_danger(I18n.T("[] forgets its training, becoming wild once again!", slime)));
                slime.set_default_behaviour();
                slime.update_name();
                continue;
            }
            slime.set_enraged_behaviour();
            slime.visible_message(span_danger(I18n.T("The [] is driven into a frenzy!", slime)));
        }
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:345
public partial class datum_chemical_reaction_slime_slimespeed : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:349
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        Dm.@new<obj_item_slimepotion_speed>(get_turf(holder.my_atom));
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

//Pink
// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:354
public partial class datum_chemical_reaction_slime_docility : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:358
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        Dm.@new<obj_item_slimepotion_slime_docility>(get_turf(holder.my_atom));
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:362
public partial class datum_chemical_reaction_slime_gender : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:366
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        Dm.@new<obj_item_slimepotion_genderchange>(get_turf(holder.my_atom));
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

//Oil
// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:377
public partial class datum_chemical_reaction_slime_slimeexplosion : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:383
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf T = get_turf(holder.my_atom);
        object lastkey = holder.my_atom.fingerprintslast;
        string touch_msg = "N/A";
        if (Dm.truthy(lastkey))
        {
            mob toucher = get_mob_by_key(lastkey);
            touch_msg = Dm.text("[].", ADMIN_LOOKUPFLW(toucher));
            toucher.log_message(Dm.text("was the last to touch the slime which has exploded at [].", AREACOORD(T)), LOG_GAME, log_globally: FALSE);
        }
        message_admins(Dm.text("Slime Explosion reaction started at []. Last Fingerprint: []", ADMIN_VERBOSEJMP(T), touch_msg));
        log_game(Dm.text("Slime Explosion reaction started at []. Last Fingerprint: [].", AREACOORD(T), Dm.truthy(lastkey) ? lastkey : "N/A"));
        T.visible_message(span_danger(I18n.T("The slime extract begins to vibrate violently !")));
        _addtimer(Dm.@new<datum_callback>(this, nameof(boom), holder), 5 * SECONDS, file: "code/modules/reagents/chemistry/recipes/slime_extracts.dm", line: 394);
        obj_item_slime_extract M = holder.my_atom as obj_item_slime_extract;
        deltimer(M.qdel_timer);
        await base.on_reaction(holder, reaction, created_volume, __extra);
        M.qdel_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(delete_extract), holder), 55, TIMER_STOPPABLE, file: "code/modules/reagents/chemistry/recipes/slime_extracts.dm", line: 398);
        return null;
    }

    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:400
    public virtual async DmTask boom(datum_reagents holder = null)
    {
        if (holder?.my_atom != null)
            await explosion(holder.my_atom, devastation_range: 1, heavy_impact_range: 2, light_impact_range: 4, explosion_cause: this);
    }
}

//Light Pink
// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:411
public partial class datum_chemical_reaction_slime_slimepotion2 : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:415
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        Dm.@new<obj_item_slimepotion_sentience>(get_turf(holder.my_atom));
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:419
public partial class datum_chemical_reaction_slime_renaming : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:423
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        Dm.@new<obj_item_slimepotion_renaming>(holder.my_atom.drop_location());
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

//Adamantine
// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:429
public partial class datum_chemical_reaction_slime_adamantine : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:433
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        Dm.@new<obj_item_stack_sheet_mineral_adamantine>(get_turf(holder.my_atom));
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

//Bluespace
// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:438
public partial class datum_chemical_reaction_slime_slimefloor2 : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:442
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        Dm.@new<obj_item_stack_tile_bluespace>(get_turf(holder.my_atom), 25);
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:447
public partial class datum_chemical_reaction_slime_slimecrystal : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:451
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        obj_item_stack_ore_bluespace_crystal BC = Dm.@new<obj_item_stack_ore_bluespace_crystal>(get_turf(holder.my_atom));
        BC.visible_message(span_notice(I18n.T("\\The [] appears out of thin air!", BC)));
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:456
public partial class datum_chemical_reaction_slime_slimeradio : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:460
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        Dm.@new<obj_item_slimepotion_slimeradio>(get_turf(holder.my_atom));
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

//Cerulean
// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:465
public partial class datum_chemical_reaction_slime_slimepsteroid2 : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:469
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        Dm.@new<obj_item_slimepotion_enhancer>(get_turf(holder.my_atom));
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:473
public partial class datum_chemical_reaction_slime_slime_territory : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:477
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        Dm.@new<obj_item_blueprints_slime>(get_turf(holder.my_atom));
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

//Sepia
// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:482
public partial class datum_chemical_reaction_slime_slimestop : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:486
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        _addtimer(Dm.@new<datum_callback>(this, nameof(slime_stop), holder), 5 * SECONDS, file: "code/modules/reagents/chemistry/recipes/slime_extracts.dm", line: 487);
        return null;
    }

    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:489
    public virtual async DmTask slime_stop(datum_reagents holder = null)
    {
        obj_item_slime_extract_sepia extract = holder.my_atom as obj_item_slime_extract_sepia;
        turf T = get_turf(holder.my_atom);
        Dm.@new<obj_effect_timestop>(T, null, null, null);
        if (extract is obj_item_slime_extract_sepia)
        {
            if (extract.extract_uses > 0)
            {
                mob lastheld = get_mob_by_key(holder.my_atom.fingerprintslast);
                if (lastheld != null && !Dm.truthy(await lastheld.equip_to_slot_if_possible(extract, ITEM_SLOT_HANDS, disable_warning: TRUE)))
                    await extract.forceMove(get_turf(lastheld));
            }
        }
        use_slime_core(holder);
    }
}

// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:500
public partial class datum_chemical_reaction_slime_slimecamera : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:504
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        Dm.@new<obj_item_camera>(get_turf(holder.my_atom));
        Dm.@new<obj_item_camera_film>(get_turf(holder.my_atom));
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:509
public partial class datum_chemical_reaction_slime_slimefloor : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:513
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        Dm.@new<obj_item_stack_tile_sepia>(get_turf(holder.my_atom), 25);
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

//Pyrite
// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:518
public partial class datum_chemical_reaction_slime_slimepaint : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:522
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        object chosen = pick(subtypesof(typeof(obj_item_paint)));
        Dm.@new((Type)chosen, get_turf(holder.my_atom));
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:527
public partial class datum_chemical_reaction_slime_slimecrayon : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:531
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        object chosen = pick(difflist(subtypesof(typeof(obj_item_toy_crayon)), typesof(typeof(obj_item_toy_crayon_spraycan))));
        Dm.@new((Type)chosen, get_turf(holder.my_atom));
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

//Rainbow :o)
// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:537
public partial class datum_chemical_reaction_slime_slime_rng : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:541
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        if (Dm.num(created_volume) >= 5)
        {
            obj_item_grenade_clusterbuster_slime S = Dm.@new<obj_item_grenade_clusterbuster_slime>(get_turf(holder.my_atom));
            S.visible_message(span_danger(I18n.T("Infused with plasma, the core begins to expand uncontrollably!")));
            S.icon_state = Dm.text("[]_active", S.base_state);
            S.active = TRUE;
            _addtimer(Dm.@new<datum_callback>(S, nameof(obj_item_grenade.detonate)), rand(1.5f * SECONDS, 6 * SECONDS), file: "code/modules/reagents/chemistry/recipes/slime_extracts.dm", line: 547);
        }
        else
        {
            mob_living_basic_slime_random random_slime = Dm.@new<mob_living_basic_slime_random>(get_turf(holder.my_atom));
            random_slime.visible_message(span_danger(I18n.T("Infused with plasma, the core begins to quiver and grow, and a new baby slime emerges from it!")));
        }
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:553
public partial class datum_chemical_reaction_slime_slimebomb : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:557
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf T = get_turf(holder.my_atom);
        obj_item_grenade_clusterbuster_slime_volatile S = Dm.@new<obj_item_grenade_clusterbuster_slime_volatile>(T);
        S.visible_message(span_danger(I18n.T("Infused with slime jelly, the core begins to expand uncontrollably!")));
        S.icon_state = Dm.text("[]_active", S.base_state);
        S.active = TRUE;
        _addtimer(Dm.@new<datum_callback>(S, nameof(obj_item_grenade.detonate)), rand(1.5f * SECONDS, 6 * SECONDS), file: "code/modules/reagents/chemistry/recipes/slime_extracts.dm", line: 563);
        object lastkey = holder.my_atom.fingerprintslast;
        string touch_msg = "N/A";
        if (Dm.truthy(lastkey))
        {
            mob toucher = get_mob_by_key(lastkey);
            touch_msg = Dm.text("[].", ADMIN_LOOKUPFLW(toucher));
            toucher.log_message(Dm.text("was last to touch a Brorble Brorble that has been primed at [].", AREACOORD(T)), LOG_GAME, log_globally: FALSE);
        }
        message_admins(Dm.text("Brorble Brorble primed at []. Last Fingerprint: []", ADMIN_VERBOSEJMP(T), touch_msg));
        log_game(Dm.text("Brorble Brorble primed at []. Last Fingerprint: [].", AREACOORD(T), Dm.truthy(lastkey) ? lastkey : "N/A"));
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:574
public partial class datum_chemical_reaction_slime_slime_transfer : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:578
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        Dm.@new<obj_item_slimepotion_transference>(get_turf(holder.my_atom));
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

// tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:582
public partial class datum_chemical_reaction_slime_flight_potion : datum_chemical_reaction_slime
{
    // tg: code/modules/reagents/chemistry/recipes/slime_extracts.dm:586
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        Dm.@new<obj_item_reagent_containers_cup_bottle_potion_flight>(get_turf(holder.my_atom));
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}
