// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/xenobiology.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the extracts' activation by a luminescent jellyperson (`activate` of each colour: the jellypeople species is not
// ported; live-partial stubs). The slime potion base and the intelligence potions came first (syndicate G3c, D316: the sentience, the
// syndicate one and the dangerous pony potion of the uplink's pony box); xenobiology added the slime extracts, the other potions and
// the slime steroid. The crossbreeds that a slime's crossed core grows into are xenobiology's crossbreeding, not ported yet. The var
// blocks are generated (D111).

/// Slime Extracts ///

// tg: code/modules/research/xenobiology/xenobiology.dm:3
public partial class obj_item_slime_extract : obj_item
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:24
    public override async DmTask<object> grind_results()
    {
        return Dm.truthy(can_grind) ? Dm.list(Dm.kv(typeof(datum_reagent_toxin_slimejelly), 20)) : Dm.list();
    }

    // tg: code/modules/research/xenobiology/xenobiology.dm:27
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (extract_uses > 1)
            Dm.append(ref dot, I18n.T("It has [] uses remaining.", extract_uses));
        return dot;
    }

    // tg: code/modules/research/xenobiology/xenobiology.dm:32
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!(tool is obj_item_slimepotion_enhancer))
            return NONE;
        if (extract_uses >= 5 || Dm.truthy(recurring))
        {
            to_chat(user, span_warning(I18n.T("You cannot enhance this extract further!")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (tool is obj_item_slimepotion_enhancer_max)
        {
            to_chat(user, span_notice(I18n.T("You dump the maximizer on the slime extract. It can now be used a total of 5 times!")));
            extract_uses = 5;
        }
        else
        {
            to_chat(user, span_notice(I18n.T("You apply the enhancer to the slime extract. It may now be reused one more time.")));
            extract_uses++;
        }
        qdel(tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/research/xenobiology/xenobiology.dm:47
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(100, Dm.bitor(Dm.bitor(INJECTABLE, DRAWABLE), SEALED_CONTAINER));
        return dot;
    }

    /**
    * Effect when activated by a Luminescent.
    *
    * This proc is called whenever a Luminescent consumes a slime extract. Each one is separated into major and minor effects depending on the extract. Cooldown is measured in deciseconds.
    *
    * * arg1 - The mob absorbing the slime extract.
    * * arg2 - The valid species for the absorbtion. Should always be a Luminescent unless something very major has changed.
    * * arg3 - Whether or not the activation is major or minor. Major activations have large, complex effects, minor are simple.
    */
    // tg: code/modules/research/xenobiology/xenobiology.dm:60
    public virtual async DmTask<object> activate(mob_living_carbon_human user = null, datum_species_jelly_luminescent species = null, object activation_type = null)
    {
        to_chat(user, span_warning(I18n.T("Nothing happened... This slime extract cannot be activated this way.")));
        return FALSE;
    }

    /**
    * Core-crossing: Feeding adult slimes extracts to obtain a much more powerful, single extract.
    *
    * By using a valid core on a living adult slime, then feeding it nine more of the same type, you can mutate it into more useful items. Not every slime type has an implemented core cross.
    */
    // tg: code/modules/research/xenobiology/xenobiology.dm:69
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        mob_living_basic_slime target_slime = interacting_with as mob_living_basic_slime;
        if (!(target_slime is mob_living_basic_slime))
            return NONE;

        if (IS_UNCONSCIOUS_OR_CRIT(target_slime))
        {
            to_chat(user, span_warning(I18n.T("The slime is dead!")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (target_slime.life_stage != SLIME_LIFE_STAGE_ADULT)
        {
            to_chat(user, span_warning(I18n.T("The slime must be an adult to cross its core!")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(target_slime.crossbreed_modification) && !Dm.eq(target_slime.crossbreed_modification, crossbreed_modification))
        {
            to_chat(user, span_warning(I18n.T("The slime is already being crossed with a different extract!")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(target_slime.crossbreed_modification))
            target_slime.crossbreed_modification = crossbreed_modification;

        target_slime.applied_crossbreed_amount++;
        qdel(this);
        to_chat(user, span_notice(I18n.T("You feed the slime [], []", this, target_slime.applied_crossbreed_amount == 1 ? I18n.T("starting to mutate its core.") : I18n.T("further mutating its core."))));
        playsound(target_slime, "sound/effects/blob/attackblob.ogg", 50, TRUE);

        if (target_slime.applied_crossbreed_amount >= SLIME_EXTRACT_CROSSING_REQUIRED)
            target_slime.spawn_corecross();
        return ITEM_INTERACT_SUCCESS;
    }

    /**
    * Effect when activated by selfsustaining crossbreed or rainbow slime
    *
    * * arg1 - The reaction being triggered. If null, a random reaction is picked
    */
    // tg: code/modules/research/xenobiology/xenobiology.dm:101
    public virtual async DmTask auto_activate_reaction(datum_chemical_reaction_slime slime_reaction = null)
    {
        if (QDELETED(this))
            return;

        if (slime_reaction == null)
        {
            DmList slime_reactions = GLOB.slime_extract_auto_activate_reactions[type] as DmList;
            if (slime_reactions == null)
                return;
            slime_reaction = pick(slime_reactions) as datum_chemical_reaction_slime;
        }

        DmList required_reagents = slime_reaction.required_reagents;
        foreach (object chem in Dm.each(required_reagents))
            await ((datum_reagents)reagents).add_reagent(chem, required_reagents[chem]);
    }
}

/// An assoc list of slime extracts to their allowed recipes
// tg: code/modules/research/xenobiology/xenobiology.dm:116  GLOBAL_LIST_INIT(slime_extract_auto_activate_reactions, init_slime_auto_activate_reaction_list())
public partial class datum_controller_global_vars
{
    public DmList slime_extract_auto_activate_reactions;

    // tg: code/modules/research/xenobiology/xenobiology.dm:116  (GLOBAL_MANAGED(slime_extract_auto_activate_reactions, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalslime_extract_auto_activate_reactions()
    {
        slime_extract_auto_activate_reactions = init_slime_auto_activate_reaction_list();
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:118
    public static DmList init_slime_auto_activate_reaction_list()
    {
        DmList recipe_list = Dm.list();

        // Only reactions with these reagent requirements are allowed to auto_activate
        DmList auto_activate_reagent_whistlist = Dm.list(
            typeof(datum_reagent_toxin_plasma),
            typeof(datum_reagent_water),
            typeof(datum_reagent_blood),
            typeof(datum_reagent_water_holywater),
            typeof(datum_reagent_uranium),
            typeof(datum_reagent_uranium_radium),
            typeof(datum_reagent_toxin_slimejelly)
        );

        DmList slime_extract_paths = subtypesof(typeof(obj_item_slime_extract));
        foreach (Type slime_reaction in Dm.each_cast<Type>(subtypesof(typeof(datum_chemical_reaction_slime))))
        {
            Type recipe_extract_type = Dm.initial(slime_reaction, (datum_chemical_reaction_slime p) => p.required_container);
            if (!Dm.@in(recipe_extract_type, slime_extract_paths))
                continue;

            bool skip = false;
            // A var read off a typepath is its initial(); the reagent list is runtime-initialized and reads null there (as in
            // OpenDream), so tg's whitelist filters nothing and every extract reaction is kept (tg's behaviour, kept)
            foreach (object chem in Dm.each(Dm.initial(slime_reaction, (datum_chemical_reaction_slime p) => p.required_reagents)))
            {
                if (!Dm.@in(chem, auto_activate_reagent_whistlist))
                {
                    skip = true;
                    break;
                }
            }
            if (skip)
                continue;

            DmList recipes = recipe_list[recipe_extract_type] as DmList;
            if (!Dm.truthy(recipes))
            {
                recipes = Dm.list();
                recipe_list[recipe_extract_type] = recipes;
            }
            recipes.Add(Dm.@new(slime_reaction));
        }

        return recipe_list;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:541
public partial class obj_item_slime_extract_adamantine : obj_item_slime_extract
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:541
    public virtual void reset_armor(mob_living_carbon_human user = null)
    {
        REMOVE_TRAIT(user, TRAIT_ADAMANTINE_EXTRACT_ARMOR, ADAMANTINE_EXTRACT_TRAIT);
        user.damage_resistance -= 25;
    }
}

////Slime-derived potions///

/**
* #Slime potions
*
* Feed slimes potions either by hand or using the slime console.
*
* Slime potions either augment the slime's behavior, its extract output, or its intelligence. These all come either from extract effects or cross cores.
* A few of the more powerful ones can modify someone's equipment or gender.
* New ones should probably be accessible only through cross cores as all the normal core types already have uses. Rule of thumb is 'stronger effects go in cross cores'.
*/

// Gaps SP2c (D349): the slime extract's activate(), the hook of the luminescent's extract actions, and the 22 colour extracts'
// activate() overrides (the effects of the integrated extract). The rest of the extracts (grinding, reactions) wait for the
// xenobiology lab.
// tg: code/modules/research/xenobiology/xenobiology.dm:3
public partial class obj_item_slime_extract : obj_item
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:47
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(100, Dm.bitor(Dm.bitor(INJECTABLE, DRAWABLE), SEALED_CONTAINER));
        return dot;
    }

    /**
    * Effect when activated by a Luminescent.
    *
    * This proc is called whenever a Luminescent consumes a slime extract. Each one is separated into major and minor effects depending on the extract. Cooldown is measured in deciseconds.
    *
    * * arg1 - The mob absorbing the slime extract.
    * * arg2 - The valid species for the absorbtion. Should always be a Luminescent unless something very major has changed.
    * * arg3 - Whether or not the activation is major or minor. Major activations have large, complex effects, minor are simple.
    */
    // tg: code/modules/research/xenobiology/xenobiology.dm:60
    public virtual async DmTask<object> activate(mob_living_carbon_human user = null, datum_species_jelly_luminescent species = null, object activation_type = null)
    {
        to_chat(user, span_warning(I18n.T("Nothing happened... This slime extract cannot be activated this way.")));
        return FALSE;
    }
}


// tg: code/modules/research/xenobiology/xenobiology.dm:155
public partial class obj_item_slime_extract_grey : obj_item_slime_extract
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:160
    public override async DmTask<object> activate(mob_living_carbon_human user = null, datum_species_jelly_luminescent species = null, object activation_type = null)
    {
        object sw__1 = activation_type;
        if (Dm.eq(sw__1, SLIME_ACTIVATE_MINOR))
        {
            obj_item_food_monkeycube M = Dm.@new<obj_item_food_monkeycube>();
            if (!Dm.truthy(await user.put_in_active_hand(M)))
                await M.forceMove(user.drop_location() as atom);
            playsound(user, Dm.resource("sound/effects/splat.ogg"), 50, TRUE);
            to_chat(user, span_notice(I18n.T("You spit out a monkey cube.")));
            return 120;
        }
        else if (Dm.eq(sw__1, SLIME_ACTIVATE_MAJOR))
        {
            to_chat(user, span_notice(I18n.T("Your [] starts pulsing...", name)));
            if (Dm.truthy(await do_after(user, 4 * SECONDS, target: user)))
            {
                mob_living_basic_slime new_slime = Dm.@new<mob_living_basic_slime>(get_turf(user), typeof(datum_slime_type_grey));
                playsound(user, Dm.resource("sound/effects/splat.ogg"), 50, TRUE);
                to_chat(user, span_notice(I18n.T("You spit out [].", new_slime)));
                return 350;
            }
            else
                return 0;
        }
        return null;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:180
public partial class obj_item_slime_extract_gold : obj_item_slime_extract
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:186
    public override async DmTask<object> activate(mob_living_carbon_human user = null, datum_species_jelly_luminescent species = null, object activation_type = null)
    {
        object sw__1 = activation_type;
        if (Dm.eq(sw__1, SLIME_ACTIVATE_MINOR))
        {
            user.visible_message(span_warning(I18n.T("[] starts shaking!", user)), span_notice(I18n.T("Your [] starts pulsing gently...", name)));
            if (Dm.truthy(await do_after(user, 4 * SECONDS, target: user)))
            {
                mob_living spawned_mob = create_random_mob(user.drop_location(), FRIENDLY_SPAWN) as mob_living;
                spawned_mob.add_faction(FACTION_NEUTRAL);
                playsound(user, Dm.resource("sound/effects/splat.ogg"), 50, TRUE);
                user.visible_message(span_warning(I18n.T("[] spits out []!", user, spawned_mob)), span_notice(I18n.T("You spit out []!", spawned_mob)));
                return 300;
            }
        }
        else if (Dm.eq(sw__1, SLIME_ACTIVATE_MAJOR))
        {
            user.visible_message(span_warning(I18n.T("[] starts shaking violently!", user)), span_warning(I18n.T("Your [] starts pulsing violently...", name)));
            if (Dm.truthy(await do_after(user, 5 * SECONDS, target: user)))
            {
                mob_living spawned_mob__1 = create_random_mob(user.drop_location(), HOSTILE_SPAWN) as mob_living;
                if (!Dm.truthy(user.combat_mode))
                    spawned_mob__1.add_faction(FACTION_NEUTRAL);
                else
                    spawned_mob__1.add_faction(FACTION_SLIME);
                playsound(user, Dm.resource("sound/effects/splat.ogg"), 50, TRUE);
                user.visible_message(span_warning(I18n.T("[] spits out []!", user, spawned_mob__1)), span_warning(I18n.T("You spit out []!", spawned_mob__1)));
                return 600;
            }
        }
        return null;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:210
public partial class obj_item_slime_extract_silver : obj_item_slime_extract
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:216
    public override async DmTask<object> activate(mob_living_carbon_human user = null, datum_species_jelly_luminescent species = null, object activation_type = null)
    {
        object sw__1 = activation_type;
        if (Dm.eq(sw__1, SLIME_ACTIVATE_MINOR))
        {
            object food_type = get_random_food();
            obj_item_food food_item = Dm.@new((Type)food_type) as obj_item_food;
            ADD_TRAIT(food_item, TRAIT_FOOD_SILVER, INNATE_TRAIT);
            if (!Dm.truthy(await user.put_in_active_hand(food_item)))
                await food_item.forceMove(user.drop_location() as atom);
            playsound(user, Dm.resource("sound/effects/splat.ogg"), 50, TRUE);
            user.visible_message(span_warning(I18n.T("[] spits out []!", user, food_item)), span_notice(I18n.T("You spit out []!", food_item)));
            return 200;
        }
        else if (Dm.eq(sw__1, SLIME_ACTIVATE_MAJOR))
        {
            object drink_type = get_random_drink();
            obj_item O = Dm.@new((Type)drink_type) as obj_item;
            if (!Dm.truthy(await user.put_in_active_hand(O)))
                await O.forceMove(user.drop_location() as atom);
            playsound(user, Dm.resource("sound/effects/splat.ogg"), 50, TRUE);
            user.visible_message(span_warning(I18n.T("[] spits out []!", user, O)), span_notice(I18n.T("You spit out []!", O)));
            return 200;
        }
        return null;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:236
public partial class obj_item_slime_extract_metal : obj_item_slime_extract
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:241
    public override async DmTask<object> activate(mob_living_carbon_human user = null, datum_species_jelly_luminescent species = null, object activation_type = null)
    {
        object sw__1 = activation_type;
        if (Dm.eq(sw__1, SLIME_ACTIVATE_MINOR))
        {
            obj_item_stack_sheet_glass O = Dm.@new<obj_item_stack_sheet_glass>(null, 5);
            if (!Dm.truthy(await user.put_in_active_hand(O)))
                await O.forceMove(user.drop_location() as atom);
            playsound(user, Dm.resource("sound/effects/splat.ogg"), 50, TRUE);
            user.visible_message(span_warning(I18n.T("[] spits out []!", user, O)), span_notice(I18n.T("You spit out []!", O)));
            return 150;
        }
        else if (Dm.eq(sw__1, SLIME_ACTIVATE_MAJOR))
        {
            obj_item_stack_sheet_iron O__1 = Dm.@new<obj_item_stack_sheet_iron>(null, 5);
            if (!Dm.truthy(await user.put_in_active_hand(O__1)))
                await O__1.forceMove(user.drop_location() as atom);
            playsound(user, Dm.resource("sound/effects/splat.ogg"), 50, TRUE);
            user.visible_message(span_warning(I18n.T("[] spits out []!", user, O__1)), span_notice(I18n.T("You spit out []!", O__1)));
            return 200;
        }
        return null;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:259
public partial class obj_item_slime_extract_purple : obj_item_slime_extract
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:264
    public override async DmTask<object> activate(mob_living_carbon_human user = null, datum_species_jelly_luminescent species = null, object activation_type = null)
    {
        object sw__1 = activation_type;
        if (Dm.eq(sw__1, SLIME_ACTIVATE_MINOR))
        {
            await user.adjust_nutrition(50);
            user.adjust_blood_volume(50);
            to_chat(user, span_notice(I18n.T("You activate [], and your body is refilled with fresh slime jelly!", this)));
            return 150;
        }
        else if (Dm.eq(sw__1, SLIME_ACTIVATE_MAJOR))
        {
            to_chat(user, span_notice(I18n.T("You activate [], and it releases regenerative chemicals!", this)));
            await ((datum_reagents)user.reagents).add_reagent(typeof(datum_reagent_medicine_regen_jelly), 10);
            return 600;
        }
        return null;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:277
public partial class obj_item_slime_extract_darkpurple : obj_item_slime_extract
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:282
    public override async DmTask<object> activate(mob_living_carbon_human user = null, datum_species_jelly_luminescent species = null, object activation_type = null)
    {
        object sw__1 = activation_type;
        if (Dm.eq(sw__1, SLIME_ACTIVATE_MINOR))
        {
            obj_item_stack_sheet_mineral_plasma O = Dm.@new<obj_item_stack_sheet_mineral_plasma>(null, 1);
            if (!Dm.truthy(await user.put_in_active_hand(O)))
                await O.forceMove(user.drop_location() as atom);
            playsound(user, Dm.resource("sound/effects/splat.ogg"), 50, TRUE);
            user.visible_message(span_warning(I18n.T("[] spits out []!", user, O)), span_notice(I18n.T("You spit out []!", O)));
            return 150;
        }
        else if (Dm.eq(sw__1, SLIME_ACTIVATE_MAJOR))
        {
            turf_open T = get_turf(user) as turf_open;
            if (T != null)
                T.atmos_spawn_air(Dm.text("[]=20", GAS_PLASMA));
            to_chat(user, span_warning(I18n.T("You activate [], and a cloud of plasma bursts out of your skin!", this)));
            return 900;
        }
        return null;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:299
public partial class obj_item_slime_extract_orange : obj_item_slime_extract
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:304
    public override async DmTask<object> activate(mob_living_carbon_human user = null, datum_species_jelly_luminescent species = null, object activation_type = null)
    {
        object sw__1 = activation_type;
        if (Dm.eq(sw__1, SLIME_ACTIVATE_MINOR))
        {
            to_chat(user, span_notice(I18n.T("You activate []. You start feeling hot!", this)));
            await ((datum_reagents)user.reagents).add_reagent(typeof(datum_reagent_consumable_capsaicin), 10);
            return 150;
        }
        else if (Dm.eq(sw__1, SLIME_ACTIVATE_MAJOR))
        {
            await ((datum_reagents)user.reagents).add_reagent(typeof(datum_reagent_phosphorus), 5);//
            await ((datum_reagents)user.reagents).add_reagent(typeof(datum_reagent_potassium), 5); // = smoke, along with any reagents inside mr. slime
            await ((datum_reagents)user.reagents).add_reagent(typeof(datum_reagent_consumable_sugar), 5);     //
            to_chat(user, span_warning(I18n.T("You activate [], and a cloud of smoke bursts out of your skin!", this)));
            return 450;
        }
        return null;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:318
public partial class obj_item_slime_extract_yellow : obj_item_slime_extract
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:323
    public override async DmTask<object> activate(mob_living_carbon_human user = null, datum_species_jelly_luminescent species = null, object activation_type = null)
    {
        object sw__1 = activation_type;
        if (Dm.eq(sw__1, SLIME_ACTIVATE_MINOR))
        {
            if (species.glow_intensity != LUMINESCENT_DEFAULT_GLOW)
            {
                to_chat(user, span_warning(I18n.T("Your glow is already enhanced!")));
                return null;
            }
            species.update_glow(user, 5);
            _addtimer(Dm.@new<datum_callback>(species, nameof(datum_species_jelly_luminescent.update_glow), user, LUMINESCENT_DEFAULT_GLOW), 1 * MINUTES, file: "code/modules/research/xenobiology/xenobiology.dm", line: 330);
            to_chat(user, span_notice(I18n.T("You start glowing brighter.")));
        }
        else if (Dm.eq(sw__1, SLIME_ACTIVATE_MAJOR))
        {
            user.visible_message(span_warning(I18n.T("[]'s skin starts flashing intermittently...", user)), span_warning(I18n.T("Your skin starts flashing intermittently...")));
            if (Dm.truthy(await do_after(user, 2.5f * SECONDS, target: user)))
            {
                await empulse(user, 1, 2, emp_source: this);
                user.log_message(Dm.text("triggered EMP using [] in []", this, AREACOORD(this)), LOG_GAME);
                user.visible_message(span_warning(I18n.T("[]'s skin flashes!", user)), span_warning(I18n.T("Your skin flashes as you emit an electromagnetic pulse!")));
                return 600;
            }
        }
        return null;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:341
public partial class obj_item_slime_extract_red : obj_item_slime_extract
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:346
    public override async DmTask<object> activate(mob_living_carbon_human user = null, datum_species_jelly_luminescent species = null, object activation_type = null)
    {
        object sw__1 = activation_type;
        if (Dm.eq(sw__1, SLIME_ACTIVATE_MINOR))
        {
            to_chat(user, span_notice(I18n.T("You activate []. You start feeling fast!", this)));
            await ((datum_reagents)user.reagents).add_reagent(typeof(datum_reagent_medicine_ephedrine), 5);
            return 450;
        }
        else if (Dm.eq(sw__1, SLIME_ACTIVATE_MAJOR))
        {
            user.visible_message(span_warning(I18n.T("[]'s skin flashes red for a moment...", user)), span_warning(I18n.T("Your skin flashes red as you emit rage-inducing pheromones...")));
            foreach (mob_living_basic_slime slime in Dm.each<mob_living_basic_slime>(viewers(get_turf(user), null)))
            {
                (slime.ai_controller as datum_ai_controller)?.set_blackboard_key(BB_SLIME_RABID, TRUE);
                slime.visible_message(span_danger(I18n.T("The [] is driven into a frenzy!", slime)));
            }
            return 600;
        }
        return null;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:360
public partial class obj_item_slime_extract_blue : obj_item_slime_extract
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:365
    public override async DmTask<object> activate(mob_living_carbon_human user = null, datum_species_jelly_luminescent species = null, object activation_type = null)
    {
        object sw__1 = activation_type;
        if (Dm.eq(sw__1, SLIME_ACTIVATE_MINOR))
        {
            to_chat(user, span_notice(I18n.T("You activate []. Your genome feels more stable!", this)));
            await ((datum_reagents)user.reagents).add_reagent(typeof(datum_reagent_medicine_mutadone), 10);
            await ((datum_reagents)user.reagents).add_reagent(typeof(datum_reagent_medicine_potass_iodide), 10);
            return 250;
        }
        else if (Dm.eq(sw__1, SLIME_ACTIVATE_MAJOR))
        {
            await ((datum_reagents)user.reagents).create_foam(typeof(datum_effect_system_fluid_spread_foam), 20, log: TRUE);
            user.visible_message(span_danger(I18n.T("Foam spews out from []'s skin!", user)), span_warning(I18n.T("You activate [], and foam bursts out of your skin!", this)));
            return 600;
        }
        return null;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:378
public partial class obj_item_slime_extract_darkblue : obj_item_slime_extract
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:383
    public override async DmTask<object> activate(mob_living_carbon_human user = null, datum_species_jelly_luminescent species = null, object activation_type = null)
    {
        object sw__1 = activation_type;
        if (Dm.eq(sw__1, SLIME_ACTIVATE_MINOR))
        {
            to_chat(user, span_notice(I18n.T("You activate []. You start feeling colder!", this)));
            user.extinguish_mob();
            await user.adjust_wet_stacks(20);
            await ((datum_reagents)user.reagents).add_reagent(typeof(datum_reagent_consumable_frostoil), 6);
            await ((datum_reagents)user.reagents).add_reagent(typeof(datum_reagent_medicine_regen_jelly), 7);
            return 100;
        }
        else if (Dm.eq(sw__1, SLIME_ACTIVATE_MAJOR))
        {
            turf_open T = get_turf(user) as turf_open;
            if (T != null)
                T.atmos_spawn_air(Dm.text("[]=40;[]", GAS_N2, TURF_TEMPERATURE(2.7f)));
            to_chat(user, span_warning(I18n.T("You activate [], and icy air bursts out of your skin!", this)));
            return 900;
        }
        return null;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:400
public partial class obj_item_slime_extract_pink : obj_item_slime_extract
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:405
    public override async DmTask<object> activate(mob_living_carbon_human user = null, datum_species_jelly_luminescent species = null, object activation_type = null)
    {
        object sw__1 = activation_type;
        if (Dm.eq(sw__1, SLIME_ACTIVATE_MINOR))
        {
            if (user.gender != MALE && user.gender != FEMALE)
            {
                to_chat(user, span_warning(I18n.T("You can't swap your gender!")));
                return null;
            }

            if (user.gender == MALE)
            {
                user.gender = FEMALE;
                user.visible_message(span_boldnotice(I18n.T("[] suddenly looks more feminine!", user)), span_boldwarning(I18n.T("You suddenly feel more feminine!")));
            }
            else
            {
                user.gender = MALE;
                user.visible_message(span_boldnotice(I18n.T("[] suddenly looks more masculine!", user)), span_boldwarning(I18n.T("You suddenly feel more masculine!")));
            }
            return 100;
        }
        else if (Dm.eq(sw__1, SLIME_ACTIVATE_MAJOR))
        {
            user.visible_message(span_warning(I18n.T("[]'s skin starts flashing hypnotically...", user)), span_notice(I18n.T("Your skin starts forming odd patterns, pacifying creatures around you.")));
            foreach (mob_living_carbon C in Dm.each<mob_living_carbon>(viewers(user, null)))
            {
                if (C != user)
                    await ((datum_reagents)C.reagents).add_reagent(typeof(datum_reagent_pax), 2);
            }
            return 600;
        }
        return null;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:427
public partial class obj_item_slime_extract_green : obj_item_slime_extract
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:432
    public override async DmTask<object> activate(mob_living_carbon_human user = null, datum_species_jelly_luminescent species = null, object activation_type = null)
    {
        object sw__1 = activation_type;
        if (Dm.eq(sw__1, SLIME_ACTIVATE_MINOR))
        {
            to_chat(user, span_warning(I18n.T("You feel yourself reverting to human form...")));
            if (Dm.truthy(await do_after(user, 12 * SECONDS, target: user)))
            {
                to_chat(user, span_warning(I18n.T("You feel human again!")));
                user.set_species(typeof(datum_species_human));
                return null;
            }
            to_chat(user, span_notice(I18n.T("You stop the transformation.")));
        }
        else if (Dm.eq(sw__1, SLIME_ACTIVATE_MAJOR))
        {
            to_chat(user, span_warning(I18n.T("You feel yourself radically changing your slime type...")));
            if (Dm.truthy(await do_after(user, 12 * SECONDS, target: user)))
            {
                to_chat(user, span_warning(I18n.T("You feel different!")));
                user.set_species(pick(typeof(datum_species_jelly_slime), typeof(datum_species_jelly_stargazer)));
                return null;
            }
            to_chat(user, span_notice(I18n.T("You stop the transformation.")));
        }
        return null;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:450
public partial class obj_item_slime_extract_lightpink : obj_item_slime_extract
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:455
    public override async DmTask<object> activate(mob_living_carbon_human user = null, datum_species_jelly_luminescent species = null, object activation_type = null)
    {
        object sw__1 = activation_type;
        if (Dm.eq(sw__1, SLIME_ACTIVATE_MINOR))
        {
            obj_item_slimepotion_renaming O = Dm.@new<obj_item_slimepotion_renaming>(null, 1);
            if (!Dm.truthy(await user.put_in_active_hand(O)))
                await O.forceMove(user.drop_location() as atom);
            playsound(user, Dm.resource("sound/effects/splat.ogg"), 50, TRUE);
            user.visible_message(span_warning(I18n.T("[] spits out []!", user, O)), span_notice(I18n.T("You spit out []!", O)));
            return 150;
        }
        else if (Dm.eq(sw__1, SLIME_ACTIVATE_MAJOR))
        {
            obj_item_slimepotion_sentience O__1 = Dm.@new<obj_item_slimepotion_sentience>(null, 1);
            if (!Dm.truthy(await user.put_in_active_hand(O__1)))
                await O__1.forceMove(user.drop_location() as atom);
            playsound(user, Dm.resource("sound/effects/splat.ogg"), 50, TRUE);
            user.visible_message(span_warning(I18n.T("[] spits out []!", user, O__1)), span_notice(I18n.T("You spit out []!", O__1)));
            return 450;
        }
        return null;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:473
public partial class obj_item_slime_extract_black : obj_item_slime_extract
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:478
    public override async DmTask<object> activate(mob_living_carbon_human user = null, datum_species_jelly_luminescent species = null, object activation_type = null)
    {
        object sw__1 = activation_type;
        if (Dm.eq(sw__1, SLIME_ACTIVATE_MINOR))
        {
            to_chat(user, span_userdanger(I18n.T("You feel something <i>wrong</i> inside you...")));
            await user.ForceContractDisease(Dm.@new<datum_disease_transformation_slime>(), FALSE, TRUE);
            return 100;
        }
        else if (Dm.eq(sw__1, SLIME_ACTIVATE_MAJOR))
        {
            to_chat(user, span_warning(I18n.T("You feel your own light turning dark...")));
            if (Dm.truthy(await do_after(user, 12 * SECONDS, target: user)))
            {
                to_chat(user, span_warning(I18n.T("You feel a longing for darkness.")));
                user.set_species(pick(typeof(datum_species_shadow)));
                return null;
            }
            to_chat(user, span_notice(I18n.T("You stop feeding [].", this)));
        }
        return null;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:493
public partial class obj_item_slime_extract_oil : obj_item_slime_extract
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:498
    public override async DmTask<object> activate(mob_living_carbon_human user = null, datum_species_jelly_luminescent species = null, object activation_type = null)
    {
        object sw__1 = activation_type;
        if (Dm.eq(sw__1, SLIME_ACTIVATE_MINOR))
        {
            to_chat(user, span_warning(I18n.T("You vomit slippery oil.")));
            playsound(user, Dm.resource("sound/effects/splat.ogg"), 50, TRUE);
            Dm.@new<obj_effect_decal_cleanable_blood_oil_slippery>(get_turf(user));
            return 450;
        }
        else if (Dm.eq(sw__1, SLIME_ACTIVATE_MAJOR))
        {
            user.visible_message(span_warning(I18n.T("[]'s skin starts pulsing and glowing ominously...", user)), span_userdanger(I18n.T("You feel unstable...")));
            if (Dm.truthy(await do_after(user, 6 * SECONDS, target: user)))
            {
                to_chat(user, span_userdanger(I18n.T("You explode!")));
                await explosion(user, devastation_range: 1, heavy_impact_range: 3, light_impact_range: 6, explosion_cause: this);
                user.investigate_log("has been gibbed by an oil slime extract explosion.", INVESTIGATE_DEATHS);
                await user.gib(DROP_ALL_REMAINS);
                return null;
            }
            to_chat(user, span_notice(I18n.T("You stop feeding [], and the feeling passes.", this)));
        }
        return null;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:516
public partial class obj_item_slime_extract_adamantine : obj_item_slime_extract
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:521
    public override async DmTask<object> activate(mob_living_carbon_human user = null, datum_species_jelly_luminescent species = null, object activation_type = null)
    {
        object sw__1 = activation_type;
        if (Dm.eq(sw__1, SLIME_ACTIVATE_MINOR))
        {
            if (HAS_TRAIT(user, TRAIT_ADAMANTINE_EXTRACT_ARMOR))
            {
                to_chat(user, span_warning(I18n.T("Your skin is already hardened!")));
                return null;
            }
            ADD_TRAIT(user, TRAIT_ADAMANTINE_EXTRACT_ARMOR, ADAMANTINE_EXTRACT_TRAIT);
            to_chat(user, span_notice(I18n.T("You feel your skin harden and become more resistant.")));
            user.damage_resistance += 25;
            _addtimer(Dm.@new<datum_callback>(this, nameof(reset_armor), user), 120 * SECONDS, file: "code/modules/research/xenobiology/xenobiology.dm", line: 530);
            return 450;
        }
        else if (Dm.eq(sw__1, SLIME_ACTIVATE_MAJOR))
        {
            to_chat(user, span_warning(I18n.T("You feel your body rapidly hardening...")));
            if (Dm.truthy(await do_after(user, 12 * SECONDS, target: user)))
            {
                to_chat(user, span_warning(I18n.T("You feel solid.")));
                user.set_species(typeof(datum_species_golem));
                return null;
            }
            to_chat(user, span_notice(I18n.T("You stop feeding [], and your body returns to its slimelike state.", this)));
        }
        return null;
    }

    // tg: code/modules/research/xenobiology/xenobiology.dm:541
    public virtual void reset_armor(mob_living_carbon_human user = null)
    {
        REMOVE_TRAIT(user, TRAIT_ADAMANTINE_EXTRACT_ARMOR, ADAMANTINE_EXTRACT_TRAIT);
        user.damage_resistance -= 25;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:545
public partial class obj_item_slime_extract_bluespace : obj_item_slime_extract
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:554
    public override async DmTask<object> activate(mob_living_carbon_human user = null, datum_species_jelly_luminescent species = null, object activation_type = null)
    {
        object sw__1 = activation_type;
        if (Dm.eq(sw__1, SLIME_ACTIVATE_MINOR))
        {
            to_chat(user, span_warning(I18n.T("You feel your body vibrating...")));
            if (Dm.truthy(await do_after(user, 2.5f * SECONDS, target: user)))
            {
                to_chat(user, span_warning(I18n.T("You teleport!")));
                await do_teleport(user, get_turf(user), 6, asoundin: Dm.resource("sound/items/weapons/emitter2.ogg"), channel: TELEPORT_CHANNEL_BLUESPACE);
                return 300;
            }
        }
        else if (Dm.eq(sw__1, SLIME_ACTIVATE_MAJOR))
        {
            if (!Dm.truthy(teleport_ready))
            {
                to_chat(user, span_notice(I18n.T("You feel yourself anchoring to this spot...")));
                turf T = get_turf(user);
                teleport_x = T.x;
                teleport_y = T.y;
                teleport_z = T.z;
                teleport_ready = TRUE;
            }
            else
            {
                teleport_ready = FALSE;
                if (Dm.truthy(teleport_x) && Dm.truthy(teleport_y) && Dm.truthy(teleport_z))
                {
                    turf T__1 = locate(teleport_x, teleport_y, teleport_z);
                    to_chat(user, span_notice(I18n.T("You snap back to your anchor point!")));
                    await do_teleport(user, T__1, asoundin: Dm.resource("sound/items/weapons/emitter2.ogg"), channel: TELEPORT_CHANNEL_BLUESPACE);
                    return 450;
                }
            }
        }
        return null;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:580
public partial class obj_item_slime_extract_pyrite : obj_item_slime_extract
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:585
    public override async DmTask<object> activate(mob_living_carbon_human user = null, datum_species_jelly_luminescent species = null, object activation_type = null)
    {
        object sw__1 = activation_type;
        if (Dm.eq(sw__1, SLIME_ACTIVATE_MINOR))
        {
            object chosen = pick(Dm.sub(subtypesof(typeof(obj_item_toy_crayon)), typesof(typeof(obj_item_toy_crayon_spraycan)))); // difflist(a, b): the types of a that are not in b
            obj_item O = Dm.@new((Type)chosen, null) as obj_item;
            if (!Dm.truthy(await user.put_in_active_hand(O)))
                await O.forceMove(user.drop_location() as atom);
            playsound(user, Dm.resource("sound/effects/splat.ogg"), 50, TRUE);
            user.visible_message(span_warning(I18n.T("[] spits out []!", user, O)), span_notice(I18n.T("You spit out []!", O)));
            return 150;
        }
        else if (Dm.eq(sw__1, SLIME_ACTIVATE_MAJOR))
        {
            DmList blacklisted_cans = Dm.list(typeof(obj_item_toy_crayon_spraycan_borg), typeof(obj_item_toy_crayon_spraycan_infinite));
            object chosen__1 = pick(Dm.sub(subtypesof(typeof(obj_item_toy_crayon_spraycan)), blacklisted_cans));
            obj_item O__1 = Dm.@new((Type)chosen__1, null) as obj_item;
            if (!Dm.truthy(await user.put_in_active_hand(O__1)))
                await O__1.forceMove(user.drop_location() as atom);
            playsound(user, Dm.resource("sound/effects/splat.ogg"), 50, TRUE);
            user.visible_message(span_warning(I18n.T("[] spits out []!", user, O__1)), span_notice(I18n.T("You spit out []!", O__1)));
            return 250;
        }
        return null;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:606
public partial class obj_item_slime_extract_cerulean : obj_item_slime_extract
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:611
    public override async DmTask<object> activate(mob_living_carbon_human user = null, datum_species_jelly_luminescent species = null, object activation_type = null)
    {
        object sw__1 = activation_type;
        if (Dm.eq(sw__1, SLIME_ACTIVATE_MINOR))
        {
            await ((datum_reagents)user.reagents).add_reagent(typeof(datum_reagent_medicine_salbutamol), 15);
            to_chat(user, span_notice(I18n.T("You feel like you don't need to breathe!")));
            return 150;
        }
        else if (Dm.eq(sw__1, SLIME_ACTIVATE_MAJOR))
        {
            turf_open T = get_turf(user) as turf_open;
            if (T != null)
            {
                T.atmos_spawn_air(Dm.text("[]=11;[]=41;[]", GAS_O2, GAS_N2, TURF_TEMPERATURE(T20C)));
                to_chat(user, span_warning(I18n.T("You activate [], and fresh air bursts out of your skin!", this)));
                return 600;
            }
        }
        return null;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:625
public partial class obj_item_slime_extract_sepia : obj_item_slime_extract
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:630
    public override async DmTask<object> activate(mob_living_carbon_human user = null, datum_species_jelly_luminescent species = null, object activation_type = null)
    {
        object sw__1 = activation_type;
        if (Dm.eq(sw__1, SLIME_ACTIVATE_MINOR))
        {
            obj_item_camera O = Dm.@new<obj_item_camera>(null, 1);
            if (!Dm.truthy(await user.put_in_active_hand(O)))
                await O.forceMove(user.drop_location() as atom);
            playsound(user, Dm.resource("sound/effects/splat.ogg"), 50, TRUE);
            user.visible_message(span_warning(I18n.T("[] spits out []!", user, O)), span_notice(I18n.T("You spit out []!", O)));
            return 150;
        }
        else if (Dm.eq(sw__1, SLIME_ACTIVATE_MAJOR))
        {
            to_chat(user, span_warning(I18n.T("You feel time slow down...")));
            if (Dm.truthy(await do_after(user, 3 * SECONDS, target: user)))
            {
                Dm.@new<obj_effect_timestop>(get_turf(user), 2, 50, Dm.list(user));
                return 900;
            }
        }
        return null;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:646
public partial class obj_item_slime_extract_rainbow : obj_item_slime_extract
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:651
    public override async DmTask<object> activate(mob_living_carbon_human user = null, datum_species_jelly_luminescent species = null, object activation_type = null)
    {
        object sw__1 = activation_type;
        if (Dm.eq(sw__1, SLIME_ACTIVATE_MINOR))
        {
            user.dna.features[FEATURE_MUTANT_COLOR] = Dm.text("#[][][]", pick("7F", "FF"), pick("7F", "FF"), pick("7F", "FF"));
            user.dna.update_uf_block(typeof(datum_dna_block_feature_mutant_color));
            await user.updateappearance(mutcolor_update: 1);
            species.update_glow(user);
            to_chat(user, span_notice(I18n.T("You feel different...")));
            return 100;
        }
        else if (Dm.eq(sw__1, SLIME_ACTIVATE_MAJOR))
        {
            object chosen = pick(subtypesof(typeof(obj_item_slime_extract)));
            obj_item O = Dm.@new((Type)chosen, null) as obj_item;
            if (!Dm.truthy(await user.put_in_active_hand(O)))
                await O.forceMove(user.drop_location() as atom);
            playsound(user, Dm.resource("sound/effects/splat.ogg"), 50, TRUE);
            user.visible_message(span_warning(I18n.T("[] spits out []!", user, O)), span_notice(I18n.T("You spit out []!", O)));
            return 150;
        }
        return null;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:682
public partial class obj_item_slimepotion : obj_item
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:688
    public override DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (is_reagent_container(interacting_with))
        {
            to_chat(user, span_warning(I18n.T("You cannot transfer [] to []! It appears the potion must be given directly to a slime or other object to absorb.", this, interacting_with)));
            return DmTask<object>.FromResult(ITEM_INTERACT_BLOCKING);
        }
        return DmTask<object>.FromResult(NONE);
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:695
public partial class obj_item_slimepotion_slime : obj_item_slimepotion
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:695
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;
        if (isslime(interacting_with))
            return await interact_with_slime((mob_living_basic_slime)interacting_with, user, modifiers);
        else
        {
            to_chat(user, span_warning(I18n.T("It appears [] must be given directly to a slime to absorb.", this)));
            return NONE;
        }
    }

    // tg: code/modules/research/xenobiology/xenobiology.dm:705
    public virtual async DmTask<object> interact_with_slime(mob_living_basic_slime interacting_slime = null, mob_living user = null, DmList modifiers = null)
    {
        return null;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:708
public partial class obj_item_slimepotion_slime_docility : obj_item_slimepotion_slime
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:713
    public override async DmTask<object> interact_with_slime(mob_living_basic_slime interacting_slime = null, mob_living user = null, DmList modifiers = null)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(interacting_slime))
        {
            to_chat(user, span_warning(I18n.T("The slime is dead!")));
            return ITEM_INTERACT_BLOCKING;
        }
        // tg tests the return of clear_blackboard_key, which returns nothing: the rabid branch never runs and a docility potion
        // always pacifies (tg's behaviour, kept)
        (interacting_slime.ai_controller as datum_ai_controller)?.clear_blackboard_key(BB_SLIME_RABID);
        object cleared = null;
        if (Dm.truthy(cleared)) //Stops being rabid, but doesn't become truly docile.
        {
            to_chat(interacting_slime, span_warning(I18n.T("You absorb the potion, and your rabid hunger finally settles to a normal desire to feed.")));
            to_chat(user, span_notice(I18n.T("You feed the slime the potion, calming its rabid rage.")));
            interacting_slime.set_default_behaviour();
            qdel(this);
            return ITEM_INTERACT_SUCCESS;
        }
        interacting_slime.set_pacified_behaviour();
        to_chat(interacting_slime, span_warning(I18n.T("You absorb the potion and feel your intense desire to feed melt away.")));
        to_chat(user, span_notice(I18n.T("You feed the slime the potion, removing its hunger and calming it.")));
        object newname = await sanitize_name(await tgui_input_text(user, I18n.T("Would you like to give the slime a name?"), I18n.T("Name your new pet"), I18n.T("Pet Slime"), MAX_NAME_LEN));

        if (!Dm.truthy(newname))
            newname = I18n.T("Pet Slime");
        interacting_slime.name = Dm.str(newname);
        interacting_slime.real_name = Dm.str(newname);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:736
public partial class obj_item_slimepotion_sentience : obj_item_slimepotion
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:746
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(I18n.T("Alt-click to set potion offer reason. []", Dm.truthy(potion_reason) ? I18n.T("Current reason: []", span_warning(Dm.str(potion_reason))) : null)));
        return dot;
    }

    // tg: code/modules/research/xenobiology/xenobiology.dm:750
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        // register_context(): the context tips (D264)
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/research/xenobiology/xenobiology.dm:754  add_context: the context tips (D264)

    // tg: code/modules/research/xenobiology/xenobiology.dm:758
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        potion_reason = await tgui_input_text(user, I18n.T("Enter reason for offering potion"), I18n.T("Intelligence Potion"), potion_reason, max_length: MAX_MESSAGE_LEN, multiline: TRUE);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/research/xenobiology/xenobiology.dm:762
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (((int)Dm.num(dot) & ITEM_INTERACT_ANY_BLOCKER) != 0)
            return dot;
        if (!isliving(interacting_with))
            return NONE;
        mob_living dumb_mob = (mob_living)interacting_with;
        if (Dm.truthy(being_used))
            return ITEM_INTERACT_BLOCKING;
        if (Dm.truthy(dumb_mob.ckey)) //only works on animals that aren't player controlled
        {
            balloon_alert(user, I18n.T("already sentient!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (IS_UNCONSCIOUS_OR_CRIT(dumb_mob))
        {
            balloon_alert(user, I18n.T("it's dead!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(dumb_mob.compare_sentience_type(sentience_type))) // Will also return false if not a basic or simple mob, which are the only two we want anyway
        {
            balloon_alert(user, I18n.T("invalid creature!"));
            return ITEM_INTERACT_BLOCKING;
        }
        balloon_alert(user, I18n.T("offering..."));
        being_used = TRUE;
        mob chosen_one = await SSpolling.poll_ghosts_for_target(
            question: I18n.T("[] is offering [] an intelligence potion![]", span_danger(user.name), span_notice(dumb_mob.name), Dm.truthy(potion_reason) ? I18n.T(" Reason: []", span_boldnotice(Dm.str(potion_reason))) : ""),
            check_jobban: ROLE_SENTIENCE,
            poll_time: 20 * SECONDS,
            checked_target: dumb_mob,
            ignore_category: POLL_IGNORE_SENTIENCE_POTION,
            alert_pic: dumb_mob,
            role_name_text: "intelligence potion",
            chat_text_border_icon: this) as mob;
        await on_poll_concluded(user, dumb_mob, chosen_one as mob_dead_observer);
        return ITEM_INTERACT_SUCCESS;
    }

    /// Assign the chosen ghost to the mob
    // tg: code/modules/research/xenobiology/xenobiology.dm:796
    public virtual async DmTask on_poll_concluded(mob user = null, mob_living dumb_mob = null, mob_dead_observer ghost = null)
    {
        if (ghost == null)
        {
            balloon_alert(user, I18n.T("try again later!"));
            being_used = FALSE;
            return;
        }

        dumb_mob.PossessByPlayer(ghost.key);
        await dumb_mob.mind.enslave_mind_to_creator(user as mob_living);
        dumb_mob.SEND_SIGNAL(COMSIG_SIMPLEMOB_SENTIENCEPOTION, Dm.list(dumb_mob, user));

        if (isanimal(dumb_mob))
        {
            mob_living_simple_animal smart_animal = dumb_mob as mob_living_simple_animal;
            smart_animal.sentience_act();
        }

        await dumb_mob.mind.add_antag_datum(typeof(datum_antagonist_sentient_creature));
        balloon_alert(user, I18n.T("success"));
        await after_success(user as mob_living, dumb_mob);
        qdel(this);
    }

    // tg: code/modules/research/xenobiology/xenobiology.dm:815
    public virtual DmTask after_success(mob_living user = null, mob_living smart_mob = null) => DmTask.CompletedTask;
}

// tg: code/modules/research/xenobiology/xenobiology.dm:1063
public partial class obj_item_slimepotion_renaming : obj_item_slimepotion
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:1070
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (((int)Dm.num(dot) & ITEM_INTERACT_ANY_BLOCKER) != 0)
            return dot;
        if (!isliving(interacting_with))
            return NONE;
        mob_living renaming_mob = (mob_living)interacting_with;
        if (Dm.truthy(being_used))
            return ITEM_INTERACT_BLOCKING;
        if (!Dm.truthy(renaming_mob.ckey)) //only works on animals that aren't player controlled
        {
            to_chat(user, span_warning(I18n.T("[] is not self aware, and cannot pick its own name.", renaming_mob)));
            return ITEM_INTERACT_BLOCKING;
        }

        being_used = TRUE;

        to_chat(user, span_notice(I18n.T("You offer [] to []...", this, user)));

        object new_name = await sanitize_name(await tgui_input_text(renaming_mob, I18n.T("What would you like your name to be?"), I18n.T("Input a name"), renaming_mob.real_name, MAX_NAME_LEN));

        if (!Dm.truthy(new_name) || QDELETED(this) || QDELETED(renaming_mob) || Dm.eq(new_name, renaming_mob.real_name) || !Dm.truthy(await renaming_mob.Adjacent(user)))
        {
            being_used = FALSE;
            return ITEM_INTERACT_BLOCKING;
        }

        renaming_mob.visible_message(span_notice(I18n.T("[] has a new name, [].", span_name(Dm.text("[]", renaming_mob)), span_name(Dm.text("[]", new_name)))), span_notice(I18n.T("Your old name of [] fades away, and your new name [] anchors itself in your mind.", span_name(Dm.text("[]", renaming_mob.real_name)), span_name(Dm.text("[]", new_name)))));
        message_admins(Dm.text("[] used [] on [], letting them rename themselves into [].", ADMIN_LOOKUPFLW(user), this, ADMIN_LOOKUPFLW(renaming_mob), new_name));
        user.log_message(Dm.text("used [] on [], letting them rename themselves into [].", this, key_name(renaming_mob), new_name), LOG_GAME);

        // pass null as first arg to not update records or ID/PDA
        await renaming_mob.fully_replace_character_name(null, new_name, log_new_name: TRUE);

        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:818
public partial class obj_item_slimepotion_sentience_nuclear : obj_item_slimepotion_sentience
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:822
    public override async DmTask after_success(mob_living user = null, mob_living smart_mob = null)
    {
        obj_item_implant_radio_syndicate imp = Dm.@new<obj_item_implant_radio_syndicate>(this);
        await imp.implant(smart_mob, user);
        await smart_mob.AddComponent(typeof(datum_component_simple_access), Dm.list(ACCESS_SYNDICATE, ACCESS_MAINT_TUNNELS));
        // var/obj/item/implant/implanter = SSwardrobe.provide_type(/obj/item/implant/tacmap/nuclear/cayenne, src);
        // implanter.implant(src, null, TRUE): the nuclear operatives' tactical map implant (N1); tg implants it into the potion itself
        Dm.Deferred("code/modules/research/xenobiology/xenobiology.dm:826", "after_success: the tactical map implant /obj/item/implant/tacmap/nuclear/cayenne (the nuclear operatives' minimap, N1)");
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:833
public partial class obj_item_slimepotion_transference : obj_item_slimepotion
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:840
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;
        if (!isliving(interacting_with))
            return NONE;
        mob_living switchy_mob = (mob_living)interacting_with;
        if (Dm.truthy(prompted))
            return ITEM_INTERACT_BLOCKING;
        if (Dm.truthy(switchy_mob.ckey)) //much like sentience, these will not work on something that is already player controlled
        {
            balloon_alert(user, I18n.T("already sentient!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (IS_UNCONSCIOUS_OR_CRIT(switchy_mob))
        {
            balloon_alert(user, I18n.T("it's dead!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(switchy_mob.compare_sentience_type(animal_type)))
        {
            balloon_alert(user, I18n.T("invalid creature!"));
            return ITEM_INTERACT_BLOCKING;
        }

        object job_banned = is_banned_from(user.ckey, ROLE_MIND_TRANSFER);
        if (QDELETED(this) || QDELETED(switchy_mob) || QDELETED(user))
            return ITEM_INTERACT_BLOCKING;

        if (Dm.truthy(job_banned))
        {
            balloon_alert(user, I18n.T("you're banned!"));
            return ITEM_INTERACT_BLOCKING;
        }

        await user.do_attack_animation(interacting_with);
        prompted = 1;
        if (!Dm.eq(await tgui_alert(usr, I18n.T("This will permanently transfer your consciousness to []. Are you sure you want to do this?", switchy_mob), null, Dm.list(I18n.T("Yes"), I18n.T("No"))), I18n.T("Yes")))
        {
            prompted = 0;
            return ITEM_INTERACT_BLOCKING;
        }

        to_chat(user, span_notice(I18n.T("You drink the potion then place your hands on []...", switchy_mob)));

        await user.mind.transfer_to(switchy_mob);
        switchy_mob.SEND_SIGNAL(COMSIG_SIMPLEMOB_TRANSFERPOTION, Dm.list(switchy_mob, user));
        // SET_FACTION_AND_ALLIES_FROM(switchy_mob, user) (__DEFINES/mobfactions.dm:119), inline
        switchy_mob.set_faction(user.get_faction());
        switchy_mob.set_allies(LAZYLISTDUPLICATE(user.allies));
        switchy_mob.copy_languages(user, LANGUAGE_MIND);
        await user.death();
        to_chat(switchy_mob, span_notice(I18n.T("In a quick flash, you feel your consciousness flow into []!", switchy_mob)));
        to_chat(switchy_mob, span_warning(I18n.T("You are now []. Your allegiances, alliances, and role is still the same as it was prior to consciousness transfer!", switchy_mob)));
        switchy_mob.name = Dm.text("[]", user.real_name);
        qdel(this);
        if (isanimal(switchy_mob))
        {
            mob_living_simple_animal switchy_animal = switchy_mob as mob_living_simple_animal;
            switchy_animal.sentience_act();
        }
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:889
public partial class obj_item_slimepotion_slime_steroid : obj_item_slimepotion_slime
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:894
    public override async DmTask<object> interact_with_slime(mob_living_basic_slime interacting_slime = null, mob_living user = null, DmList modifiers = null)
    {
        if (interacting_slime.life_stage == SLIME_LIFE_STAGE_ADULT) //Can't steroidify adults
        {
            to_chat(user, span_warning(I18n.T("Only baby slimes can use the steroid!")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (IS_UNCONSCIOUS_OR_CRIT(interacting_slime))
        {
            to_chat(user, span_warning(I18n.T("The slime is dead!")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (interacting_slime.cores >= 5)
        {
            to_chat(user, span_warning(I18n.T("The slime already has the maximum amount of extract!")));
            return ITEM_INTERACT_BLOCKING;
        }

        to_chat(user, span_notice(I18n.T("You feed the slime the steroid. It will now produce one more extract.")));
        interacting_slime.cores++;
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:915
public partial class obj_item_slimepotion_slime_stabilizer : obj_item_slimepotion_slime
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:920
    public override async DmTask<object> interact_with_slime(mob_living_basic_slime interacting_slime = null, mob_living user = null, DmList modifiers = null)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(interacting_slime))
        {
            to_chat(user, span_warning(I18n.T("The slime is dead!")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (interacting_slime.mutation_chance == 0)
        {
            to_chat(user, span_warning(I18n.T("The slime already has no chance of mutating!")));
            return ITEM_INTERACT_BLOCKING;
        }

        to_chat(user, span_notice(I18n.T("You feed the slime the stabilizer. It is now less likely to mutate.")));
        interacting_slime.mutation_chance = clamp(interacting_slime.mutation_chance - 15, 0, 100);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:933
public partial class obj_item_slimepotion_slime_mutator : obj_item_slimepotion_slime
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:938
    public override async DmTask<object> interact_with_slime(mob_living_basic_slime interacting_slime = null, mob_living user = null, DmList modifiers = null)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(interacting_slime))
        {
            to_chat(user, span_warning(I18n.T("The slime is dead!")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(interacting_slime.mutator_used))
        {
            to_chat(user, span_warning(I18n.T("This slime has already consumed a mutator, any more would be far too unstable!")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (interacting_slime.mutation_chance == 100)
        {
            to_chat(user, span_warning(I18n.T("The slime is already guaranteed to mutate!")));
            return ITEM_INTERACT_BLOCKING;
        }

        to_chat(user, span_notice(I18n.T("You feed the slime the mutator. It is now more likely to mutate.")));
        interacting_slime.mutation_chance = clamp(interacting_slime.mutation_chance + 12, 0, 100);
        interacting_slime.mutator_used = TRUE;
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:955
public partial class obj_item_slimepotion_speed : obj_item_slimepotion
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:960
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;
        if (!isobj(interacting_with))
        {
            to_chat(user, span_warning(I18n.T("The potion can only be used on objects!")));
            return NONE;
        }

        if (HAS_TRAIT(interacting_with, TRAIT_SPEED_POTIONED))
        {
            to_chat(user, span_warning(I18n.T("[] can't be made any faster!", interacting_with)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (isitem(interacting_with))
        {
            obj_item apply_to = (obj_item)interacting_with;
            if (apply_to.slowdown <= 0 || Dm.truthy(Dm.bitand(apply_to.item_flags, IMMUTABLE_SLOW)) || HAS_TRAIT(apply_to, TRAIT_NO_SPEED_POTION))
            {
                if (interacting_with.atom_storage != null)
                    return NONE; // lets us put the potion in the bag
                to_chat(user, span_warning(I18n.T("[] can't be made any faster!", apply_to)));
                return ITEM_INTERACT_BLOCKING;
            }
        }

        if (Dm.truthy(Dm.bitand(interacting_with.SEND_SIGNAL(COMSIG_SPEED_POTION_APPLIED, Dm.list(interacting_with, this, user)), SPEED_POTION_STOP)))
            return ITEM_INTERACT_SUCCESS;

        if (isitem(interacting_with))
        {
            obj_item apply_to = (obj_item)interacting_with;
            apply_to.slowdown = 0;
        }

        to_chat(user, span_notice(I18n.T("You slather the red gunk over the [], making it faster.", interacting_with)));
        await interacting_with.remove_atom_colour(WASHABLE_COLOUR_PRIORITY);
        await interacting_with.add_atom_colour(color_transition_filter(COLOR_RED, SATURATION_OVERRIDE), FIXED_COLOUR_PRIORITY);
        ((obj)interacting_with).drag_slowdown = 0;
        ADD_TRAIT(interacting_with, TRAIT_SPEED_POTIONED, SLIME_POTION_TRAIT);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:995
public partial class obj_item_slimepotion_fireproof : obj_item_slimepotion
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:1002
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;
        if (uses <= 0)
        {
            qdel(this);
            return ITEM_INTERACT_BLOCKING;
        }
        obj_item_clothing clothing = interacting_with as obj_item_clothing;
        if (!(clothing is obj_item_clothing))
        {
            to_chat(user, span_warning(I18n.T("The potion can only be used on clothing!")));
            return NONE;
        }
        if (Dm.num(clothing.max_heat_protection_temperature) >= FIRE_IMMUNITY_MAX_TEMP_PROTECT)
        {
            to_chat(user, span_warning(I18n.T("The [] is already fireproof!", clothing)));
            return ITEM_INTERACT_BLOCKING;
        }
        to_chat(user, span_notice(I18n.T("You slather the blue gunk over the [], fireproofing it.", clothing)));
        clothing.name = Dm.text("fireproofed []", clothing.name);
        await clothing.remove_atom_colour(WASHABLE_COLOUR_PRIORITY);
        await clothing.add_atom_colour(color_transition_filter(COLOR_NAVY, SATURATION_OVERRIDE), FIXED_COLOUR_PRIORITY);
        clothing.max_heat_protection_temperature = FIRE_IMMUNITY_MAX_TEMP_PROTECT;
        clothing.heat_protection = clothing.body_parts_covered;
        clothing.resistance_flags = Dm.bitor(clothing.resistance_flags, FIRE_PROOF);
        uses--;
        if (uses <= 0)
            qdel(this);
        return ITEM_INTERACT_BLOCKING;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:1028
public partial class obj_item_slimepotion_genderchange : obj_item_slimepotion
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:1033
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;
        if (!isliving(interacting_with))
            return NONE;
        mob_living living_mob = (mob_living)interacting_with;
        if (living_mob.stat == DEAD)
        {
            to_chat(user, span_warning(I18n.T("The potion can only be used on living things!")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.eq(living_mob.gender, MALE) && !Dm.eq(living_mob.gender, FEMALE))
        {
            to_chat(user, span_warning(I18n.T("The potion can only be used on gendered things!")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (living_mob.mind != null)
        {
            if (!Dm.truthy(await do_after(user, delay: 5 * SECONDS, target: living_mob)))
            {
                balloon_alert(user, I18n.T("interrupted!"));
                return ITEM_INTERACT_BLOCKING;
            }
        }

        if (Dm.eq(living_mob.gender, MALE))
        {
            living_mob.gender = FEMALE;
            living_mob.visible_message(span_boldnotice(I18n.T("[] suddenly looks more feminine!", living_mob)), span_boldwarning(I18n.T("You suddenly feel more feminine!")));
        }
        else
        {
            living_mob.gender = MALE;
            living_mob.visible_message(span_boldnotice(I18n.T("[] suddenly looks more masculine!", living_mob)), span_boldwarning(I18n.T("You suddenly feel more masculine!")));
        }
        await living_mob.regenerate_icons();
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:1063
public partial class obj_item_slimepotion_renaming : obj_item_slimepotion
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:1070
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;
        if (!isliving(interacting_with))
            return NONE;
        mob_living renaming_mob = (mob_living)interacting_with;
        if (Dm.truthy(being_used))
            return ITEM_INTERACT_BLOCKING;
        if (!Dm.truthy(renaming_mob.ckey)) //only works on animals that aren't player controlled
        {
            to_chat(user, span_warning(I18n.T("[] is not self aware, and cannot pick its own name.", renaming_mob)));
            return ITEM_INTERACT_BLOCKING;
        }

        being_used = TRUE;

        to_chat(user, span_notice(I18n.T("You offer [] to []...", this, user)));

        object new_name = await sanitize_name(await tgui_input_text(renaming_mob, I18n.T("What would you like your name to be?"), I18n.T("Input a name"), renaming_mob.real_name, MAX_NAME_LEN));

        if (!Dm.truthy(new_name) || QDELETED(this) || QDELETED(renaming_mob) || Dm.eq(new_name, renaming_mob.real_name) || !Dm.truthy(await renaming_mob.Adjacent(user)))
        {
            being_used = FALSE;
            return ITEM_INTERACT_BLOCKING;
        }

        renaming_mob.visible_message(span_notice(I18n.T("[] has a new name, [].", span_name(Dm.text("[]", renaming_mob)), span_name(Dm.text("[]", new_name)))), span_notice(I18n.T("Your old name of [] fades away, and your new name [] anchors itself in your mind.", span_name(Dm.text("[]", renaming_mob.real_name)), span_name(Dm.text("[]", new_name)))));
        message_admins(Dm.text("[] used [] on [], letting them rename themselves into [].", ADMIN_LOOKUPFLW(user), this, ADMIN_LOOKUPFLW(renaming_mob), new_name));
        user.log_message(Dm.text("used [] on [], letting them rename themselves into [].", this, key_name(renaming_mob), new_name), LOG_GAME);

        // pass null as first arg to not update records or ID/PDA
        await renaming_mob.fully_replace_character_name(null, new_name, log_new_name: TRUE);

        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/research/xenobiology/xenobiology.dm:1103
public partial class obj_item_slimepotion_slimeradio : obj_item_slimepotion
{
    // tg: code/modules/research/xenobiology/xenobiology.dm:1108
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;
        if (!isliving(interacting_with))
            return NONE;
        if (!isanimal_or_basicmob(interacting_with))
        {
            to_chat(user, span_warning(I18n.T("[] is too complex for the potion!", interacting_with)));
            return ITEM_INTERACT_BLOCKING;
        }
        mob_living radio_head = (mob_living)interacting_with;
        if (IS_UNCONSCIOUS_OR_CRIT(radio_head))
        {
            to_chat(user, span_warning(I18n.T("[] is dead!", radio_head)));
            return ITEM_INTERACT_BLOCKING;
        }

        to_chat(user, span_notice(I18n.T("You feed the potion to [].", radio_head)));
        to_chat(radio_head, span_notice(I18n.T("Your mind tingles as you are fed the potion. You can hear radio waves now!")));
        obj_item_implant_radio_slime imp = Dm.@new<obj_item_implant_radio_slime>(this);
        await imp.implant(radio_head, user);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}
