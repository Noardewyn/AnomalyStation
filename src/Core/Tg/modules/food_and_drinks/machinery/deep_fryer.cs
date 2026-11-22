// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/machinery/deep_fryer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): the deep fryer. The var block is generated (D111); the static deepfry_blacklisted_items
// is a TgState field (rules 10.6). Fishing in the fryer (the fishing spot and the fish-safe storage) waits for fishing.

// tg: code/modules/food_and_drinks/machinery/deep_fryer.dm:2
public partial class datum_controller_global_vars
{
    /// Global typecache of things which should never be fried.
    // tg: code/modules/food_and_drinks/machinery/deep_fryer.dm:2  GLOBAL_LIST_INIT(oilfry_blacklisted_items, typecacheof(...))
    public DmList oilfry_blacklisted_items;

    // tg: code/modules/food_and_drinks/machinery/deep_fryer.dm:2  (GLOBAL_MANAGED(oilfry_blacklisted_items, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaloilfry_blacklisted_items()
    {
        oilfry_blacklisted_items = typecacheof(Dm.list(
            typeof(obj_item_bodybag_bluespace),
            typeof(obj_item_delivery),
            typeof(obj_item_his_grace),
            typeof(obj_item_mod_control),
            typeof(obj_item_reagent_containers_condiment),
            typeof(obj_item_reagent_containers_cup),
            typeof(obj_item_reagent_containers_syringe),
            typeof(obj_item_reagent_containers_hypospray_medipen), //letting medipens become edible opens them to being injected/drained with IV drip & saltshakers
            typeof(obj_item_slimecrossbeaker_autoinjector) //same as medipen
        )) as DmList;
        Dm.append(ref gvars_datum_init_order, "oilfry_blacklisted_items");
    }
}

// tg: code/modules/food_and_drinks/machinery/deep_fryer.dm:14
public partial class obj_machinery_deepfryer : obj_machinery
{
    /// Static typecache of things we can't fry.
    // tg: code/modules/food_and_drinks/machinery/deep_fryer.dm:48  var/static/list/deepfry_blacklisted_items (rules 10.6)
    public static DmList deepfry_blacklisted_items => TgState.Current.obj_machinery_deepfryer__deepfry_blacklisted_items ??= typecacheof(Dm.list(
        typeof(obj_item_screwdriver),
        typeof(obj_item_crowbar),
        typeof(obj_item_wrench),
        typeof(obj_item_wirecutters),
        typeof(obj_item_multitool),
        typeof(obj_item_weldingtool)
    )) as DmList;

    // tg: code/modules/food_and_drinks/machinery/deep_fryer.dm:57
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(50, OPENCONTAINER);
        Waiting.NoSleep(((datum_reagents)reagents).add_reagent(typeof(datum_reagent_consumable_nutriment_fat_oil), 25));
        fry_loop = Dm.@new<datum_looping_sound_deep_fryer>(this, FALSE);
        RegisterSignal(this, COMSIG_COMPONENT_CLEAN_ACT, nameof(on_cleaned));
        // AddComponent(/datum/component/fishing_spot, GLOB.preset_fish_sources[/datum/fish_source/deepfryer]) and
        // AddElement(/datum/element/fish_safe_storage) //Prevents fryish and fritterish from dying inside the deepfryer.
        Dm.Deferred("code/modules/food_and_drinks/machinery/deep_fryer.dm:63", "Initialize: fishing in the deep fryer (the fishing spot and the fish-safe storage: fishing is not ported)");
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/deep_fryer.dm:66
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref fry_loop);
        QDEL_NULL(ref frying);
        return base.Destroy(force);
    }

    // tg: code/modules/food_and_drinks/machinery/deep_fryer.dm:71
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        // This handles nulling out frying via exited
        if (frying != null)
            await frying.forceMove(drop_location() as atom);
        return null;
    }

    // tg: code/modules/food_and_drinks/machinery/deep_fryer.dm:76
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        float oil_efficiency = 0;
        foreach (datum_stock_part_micro_laser laser in Dm.each<datum_stock_part_micro_laser>(component_parts))
            oil_efficiency += Dm.num(laser.tier);
        oil_use = Dm.initial(this, (obj_machinery_deepfryer f) => f.oil_use) - (oil_efficiency * 0.00475f);
        fry_speed = oil_efficiency;
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/deep_fryer.dm:84
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (grease_level >= 1)
            Dm.append(ref dot, "fryer_greasy");
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/deep_fryer.dm:89
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (frying != null)
            Dm.append(ref dot, I18n.T("You can make out \\a [] in the oil.", frying));
        if (Dm.truthy(in_range(user, this)) || isobserver(user))
            Dm.append(ref dot, span_notice(I18n.T("The status display reads: Frying at <b>[]%</b> speed.<br>Using <b>[]</b> units of oil per second.", fry_speed * 100, oil_use)));
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/deep_fryer.dm:96
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/food_and_drinks/machinery/deep_fryer.dm:101
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/food_and_drinks/machinery/deep_fryer.dm:104
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[]_[]", base_icon_state, frying != null ? "on" : Dm.truthy(panel_open) ? "open" : "off");
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/deep_fryer.dm:108
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_reagent_containers_applicator_pill)
        {
            if (!Dm.truthy(((datum_reagents)reagents).total_volume))
            {
                to_chat(user, span_warning(I18n.T("There's nothing to dissolve [] in!", tool)));
                return ITEM_INTERACT_BLOCKING;
            }
            user.visible_message(span_notice(I18n.T("[] drops [] into [].", user, tool, this)), span_notice(I18n.T("You dissolve [] in [].", tool, this)));
            await ((datum_reagents)tool.reagents).trans_to(this, ((datum_reagents)tool.reagents).total_volume, transferred_by: user);
            qdel(tool);
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(user.combat_mode))
            return ITEM_INTERACT_SKIP_TO_ATTACK; // allow a thwack

        if (Dm.truthy(tool.is_drainable()))
            return NONE; // pour it in

        if (!Dm.truthy(((datum_reagents)reagents).has_reagent(typeof(datum_reagent_consumable_nutriment_fat), check_subtypes: TRUE)))
        {
            to_chat(user, span_warning(I18n.T("[] has no fat or oil to fry with!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(Dm.bitand(tool.resistance_flags, INDESTRUCTIBLE)))
        {
            to_chat(user, span_warning(I18n.T("You don't feel it would be wise to fry []...", tool)));
            return ITEM_INTERACT_BLOCKING;
        }

        bool deepfry_blacklisted = Dm.truthy(is_type_in_typecache(tool, deepfry_blacklisted_items)) || Dm.truthy(is_type_in_typecache(tool, GLOB.oilfry_blacklisted_items));
        bool is_storage = tool.atom_storage != null;
        bool illegal_item = HAS_TRAIT(tool, TRAIT_NODROP) || Dm.truthy(Dm.bitand(tool.item_flags, ABSTRACT | DROPDEL | HAND_ITEM));
        if (deepfry_blacklisted || is_storage || illegal_item)
            return NONE;

        if (frying == null && Dm.truthy(await user.transferItemToLoc(tool, this)))
        {
            start_fry(tool, user);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/food_and_drinks/machinery/deep_fryer.dm:144
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        base.process(seconds_per_tick, __extra);
        datum_reagents reagents = (datum_reagents)this.reagents;
        datum_reagent_consumable_nutriment_fat frying_oil = reagents.has_reagent(typeof(datum_reagent_consumable_nutriment_fat), check_subtypes: TRUE) as datum_reagent_consumable_nutriment_fat;
        if (frying_oil == null)
            return null;
        reagents.chem_temp = frying_oil.fry_temperature;
        if (frying == null)
            return null;

        Waiting.NoSleep(reagents.trans_to(frying, oil_use * Dm.num(seconds_per_tick), multiplier: fry_speed * 3)); //Fried foods gain more of the reagent thanks to space magic
        grease_level += Dm.num(prob(grease_increase_chance)) * grease_Increase_amount;

        cook_time += fry_speed * Dm.num(seconds_per_tick) * SECONDS;
        if (cook_time >= FRYING_TIME_PERFECT && !Dm.truthy(frying_fried))
        {
            frying_fried = TRUE; //frying... frying... fried
            playsound(this.loc, Dm.resource("sound/machines/ding.ogg"), 50, TRUE);
            audible_message(span_notice(I18n.T("[] dings!", this)));
        }
        else if (cook_time >= FRYING_TIME_WARNING && !Dm.truthy(frying_burnt))
        {
            frying_burnt = TRUE;
            DmList anosmia_havers = Dm.list();
            foreach (mob smeller in Dm.each<mob>(get_hearers_in_view(DEFAULT_MESSAGE_RANGE, this)))
            {
                if (HAS_TRAIT(smeller, TRAIT_ANOSMIA))
                    Dm.append(ref anosmia_havers, smeller);
            }
            visible_message(span_warning(I18n.T("[] emits an acrid smell!", this)), ignored_mobs: anosmia_havers);
        }

        Waiting.NoSleep(use_energy(active_power_usage));
        return null;
    }

    // tg: code/modules/food_and_drinks/machinery/deep_fryer.dm:171
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == frying)
            await reset_frying();
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/deep_fryer.dm:176
    public virtual async DmTask reset_frying()
    {
        if (!QDELETED(frying))
            await frying.AddElement(typeof(datum_element_fried_item), cook_time);

        frying = null;
        frying_fried = FALSE;
        frying_burnt = FALSE;
        fry_loop.stop();
        cook_time = 0;
        update_appearance();
        flick("fryer_stop", this);
    }

    // tg: code/modules/food_and_drinks/machinery/deep_fryer.dm:188
    public virtual void start_fry(obj_item frying_item = null, mob user = null)
    {
        to_chat(user, span_notice(I18n.T("You put [] into [].", frying_item, this)));
        if (frying_item is obj_item_freeze_cube)
        {
            log_bomber(user, "put a freeze cube in a", this);
            visible_message(span_userdanger(I18n.T("[] starts glowing... Oh no...", this)));
            playsound(this, Dm.resource("sound/effects/pray_chaplain.ogg"), 100);
            add_filter("entropic_ray", 10, Dm.list(Dm.kv("type", "rays"), Dm.kv("size", 35), Dm.kv("color", COLOR_VIVID_YELLOW)));
            _addtimer(Dm.@new<datum_callback>(this, nameof(blow_up)), 5 * SECONDS, file: "code/modules/food_and_drinks/machinery/deep_fryer.dm", line: 195);
        }

        frying = frying_item;
        // Give them reagents to put frying oil in
        if (frying.reagents == null)
            frying.create_reagents(50, INJECTABLE);
        if (user.mind != null)
            ADD_TRAIT(frying, TRAIT_HANDMADE, REF(user.mind));
        if (Dm.truthy(frying._listen_lookup?[COMSIG_ITEM_ENTERED_FRYER]))
            frying._SendSignal(COMSIG_ITEM_ENTERED_FRYER, Dm.list(frying));

        update_appearance();
        flick("fryer_start", this);
        fry_loop.start();
    }

    // tg: code/modules/food_and_drinks/machinery/deep_fryer.dm:209
    public virtual async DmTask blow_up()
    {
        visible_message(span_userdanger(I18n.T("[] blows up from the entropic reaction!", this)));
        await explosion(this, devastation_range: 1, heavy_impact_range: 3, light_impact_range: 5, flame_range: 7);
        await deconstruct(FALSE);
    }

    // tg: code/modules/food_and_drinks/machinery/deep_fryer.dm:214
    public override async DmTask<object> attack_ai(mob user = null)
    {
        return null;
    }

    // tg: code/modules/food_and_drinks/machinery/deep_fryer.dm:217
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        datum_reagents reagents = (datum_reagents)this.reagents;
        if (frying != null)
        {
            to_chat(user, span_notice(I18n.T("You eject [] from [].", frying, this)));
            await frying.forceMove(drop_location() as atom);
            if (Dm.truthy(await Adjacent(user)) && !issilicon(user))
                await user.put_in_hands(frying);
            return null;
        }
        else if (user.pulling != null && iscarbon(user.pulling) && Dm.truthy(reagents.total_volume))
        {
            if (user.grab_state < GRAB_AGGRESSIVE)
            {
                to_chat(user, span_warning(I18n.T("You need a better grip to do that!")));
                return null;
            }
            mob_living_carbon dunking_target = (mob_living_carbon)user.pulling;
            log_combat(user, dunking_target, "dunked", null, Dm.text("into []", this));
            user.visible_message(span_danger(I18n.T("[] dunks []'s face in []!", user, dunking_target, this)));
            await reagents.expose(dunking_target, TOUCH);
            float bio_multiplier = Dm.num(dunking_target.getarmor(BODY_ZONE_HEAD, BIO)) * 0.01f;
            float target_temp = dunking_target.bodytemperature;
            float cold_multiplier = 1;
            if (target_temp < TCMB + 10) // a tiny bit of leeway
            {
                dunking_target.visible_message(span_userdanger(I18n.T("[] explodes from the entropic difference! Holy fuck!", dunking_target)));
                dunking_target.investigate_log(Dm.text("has been gibbed by entropic difference (being dunked into []).", this), INVESTIGATE_DEATHS);
                await dunking_target.gib(DROP_ALL_REMAINS);
                log_combat(user, dunking_target, "blew up", null, Dm.text("by dunking them into []", this));
                return null;
            }
            else if (target_temp < T0C)
                cold_multiplier += round(target_temp * 1.5f / T0C, 0.01f);
            await dunking_target.apply_damage(min(30 * bio_multiplier * cold_multiplier, reagents.total_volume), BURN, BODY_ZONE_HEAD);
            if (Dm.truthy(reagents.reagent_list)) //This can runtime if reagents has nothing in it.
                await reagents.remove_all(reagents.total_volume / 2);
            await dunking_target.Paralyze(60);
            user.changeNext_move(CLICK_CD_MELEE);
        }
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/food_and_drinks/machinery/deep_fryer.dm:252
    [SignalHandler]
    public virtual object on_cleaned(obj source_component = null, obj source = null)
    {
        object dot = NONE;

        grease_level = 0;
        update_appearance(UPDATE_OVERLAYS);
        Dm.combine(ref dot, COMPONENT_CLEANED | COMPONENT_CLEANED_GAIN_XP);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/food_and_drinks/machinery/deep_fryer.dm:48  /obj/machinery/deepfryer's var/static/list/deepfry_blacklisted_items
    internal DmList obj_machinery_deepfryer__deepfry_blacklisted_items;
}
