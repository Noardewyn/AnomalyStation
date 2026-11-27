// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/hydroponics.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the hydroponics tray whole: water, nutrients, toxicity, pests, weeds, health, age and the growth stages,
// harvesting, the auto-grow, mutations, the pollination, the snail, the tray's lights and plant picture. Left out: the screentip
// element and add_context (D264, live-partial rows), the investigate and admin logs (comments, M10+), the emissive overlays
// (DV12). The tray's USB port takes its circuit component. `myseed` is hand-declared (rule V3 types it Type).

// tg: code/modules/hydroponics/hydroponics.dm:2
public partial class obj_machinery_hydroponics : obj_machinery
{
    ///The currently planted seed
    // tg: code/modules/hydroponics/hydroponics.dm:44  (hand-declared: generated as a path, rule V3)
    public obj_item_seeds myseed;

    // tg: code/modules/hydroponics/hydroponics.dm:2  (the generated constructor, kept with the hand-declared var: types-converter.md 3.2)
    public obj_machinery_hydroponics()
    {
        name = "hydroponics tray";
        desc = "A basin used to grow plants in.";
        icon = Dm.resource("icons/obj/service/hydroponics/equipment.dmi");
        icon_state = "hydrotray";
        density = 1;
        pass_flags_self = 192;
        pixel_z = 8;
        obj_flags = 10;
        circuit = typeof(obj_item_circuitboard_machine_hydroponics);
        interaction_flags_click = 32;
        use_power = 0;
    }

    // tg: code/modules/hydroponics/hydroponics.dm:70
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        //ALRIGHT YOU DEGENERATES. YOU HAD REAGENT HOLDERS FOR AT LEAST 4 YEARS AND NONE OF YOU MADE HYDROPONICS TRAYS HOLD NUTRIENT CHEMS INSTEAD OF USING "Points".
        //SO HERE LIES THE "nutrilevel" VAR. IT'S DEAD AND I PUT IT OUT OF ITS MISERY. USE "reagents" INSTEAD. ~ArcaneMusic, accept no substitutes.
        create_reagents(maxnutri, INJECTABLE);
        if (Dm.truthy(mapload))
        {
            Waiting.NoSleep(((datum_reagents)reagents).add_reagent(typeof(datum_reagent_plantnutriment_eznutriment), Dm.max(maxnutri / 2, 10))); //Half filled nutrient trays for dirt trays to have more to grow with in prison/lavaland.
            waterlevel = maxwater;
        }
        object dot = base.Initialize(mapload, __extra);

        // var/static/list/hovering_item_typechecks ...; AddElement(/datum/element/contextual_screentip_item_typechecks, ...): the context tips (D264)
        register_context();
        return dot;
    }

    // tg: code/modules/hydroponics/hydroponics.dm:235
    public override object Destroy(float? force = null)
    {
        if (myseed != null)
        {
            qdel(myseed);
            myseed = null;
        }
        // remove_shared_particles(/particles/pollen): particles are not ported
        qdel(current_soil);
        current_soil = null;
        return base.Destroy(force);
    }

    // tg: code/modules/hydroponics/hydroponics.dm:242
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (!QDELETED(this) && gone == myseed)
        {
            await set_seed(null, FALSE);
            return dot;
        }
        if (gone == current_soil)
        {
            current_soil = null;
            if (!QDELETED(this))
                update_appearance();
            return dot;
        }
        if (gone is obj_item_mob_holder_snail snail_object)
        {
            if (snail_object.held_mob != null)
                UnregisterSignal(snail_object.held_mob, Dm.list(COMSIG_LIVING_DEATH, COMSIG_MOVABLE_ATTEMPTED_MOVE));
            qdel(our_snail);
            our_snail = null;
        }
        return dot;
    }

    // tg: code/modules/hydroponics/hydroponics.dm:267
    public override async DmTask<object> bullet_act(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, float? blocked = null) //Works with the Somatoray to modify plant variables.
    {
        if (myseed == null)
            return await base.bullet_act(hitting_projectile, def_zone, piercing_hit, blocked);
        if (hitting_projectile is obj_projectile_energy_flora_mut)
        {
            mutate();
            return BULLET_ACT_HIT;
        }
        if (hitting_projectile is obj_projectile_energy_flora_yield)
            return await myseed.projectile_hit(hitting_projectile);
        if (hitting_projectile is obj_projectile_energy_flora_evolution)
        {
            if (myseed != null)
            {
                if (Dm.truthy(LAZYLEN(myseed.mutatelist)))
                    myseed.set_instability(myseed.instability / 2);
            }
            await mutatespecie();
            return BULLET_ACT_HIT;
        }
        return await base.bullet_act(hitting_projectile, def_zone, piercing_hit, blocked);
    }

    // tg: code/modules/hydroponics/hydroponics.dm:283
    public override object power_change()
    {
        object dot = base.power_change();
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)) && Dm.truthy(self_sustaining))
            set_self_sustaining(FALSE);
        return dot;
    }

    // tg: code/modules/hydroponics/hydroponics.dm:288
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(seconds_per_tick), null); // the slot's set waitfor = FALSE (processing.dm:48)
    }

    private async DmTask<object> process__body(object seconds_per_tick)
    {
        float spt = Dm.num(seconds_per_tick);
        bool needs_update = false; // Checks if the icon needs updating so we don't redraw empty trays every time

        if (our_snail != null)
            handle_snail();

        if (Dm.truthy(self_sustaining))
        {
            if (Dm.truthy(powered__proc()))
            {
                adjust_waterlevel(rand(1, 2) * spt * 0.5f);
                adjust_weedlevel(-0.5f * spt);
                adjust_pestlevel(-0.5f * spt);
            }
            else
            {
                set_self_sustaining(FALSE);
                visible_message(span_warning(I18n.T("[]'s auto-grow functionality shuts off!", name)));
            }
        }

        if (world.time > (lastcycle + cycledelay))
        {
            lastcycle = world.time;
            if (myseed != null && plant_status != HYDROTRAY_PLANT_DEAD)
            {
                bool is_fungus = myseed.get_gene(typeof(datum_plant_gene_trait_plant_type_fungal_metabolism)) != null;
                // Advance age, if planted in mushroom friendly soil and we are a mushroom we mature 40% faster.
                age = Dm.num(age) + ((1 * (is_fungus && Dm.truthy(Dm.bitand(tray_flags, FAST_MUSHROOMS)) ? 1 : 0)) != 0 ? FAST_MUSH_MODIFIER : 1);
                if (Dm.num(age) < myseed.maturation)
                    lastproduce = Dm.num(age);
                needs_update = true;

//Nutrients//////////////////////////////////////////////////////////////
                // Nutrients deplete at a constant rate, since new nutrients can boost stats far easier.
                await apply_chemicals(lastuser?.resolve() as mob);
                datum_reagents holder = reagents as datum_reagents;
                if (Dm.truthy(self_sustaining))
                    await holder.remove_all(Dm.min(0.5f, nutridrain));
                else
                    await holder.remove_all(nutridrain);

                if (Dm.num(holder.total_volume) <= 0)
                {
                    //The microbiome slowly create nutrients by fixing nitrogen from the air or metabolizing organic matter.
                    if (Dm.truthy(Dm.bitand(tray_flags, SLOW_RELEASE)))
                        await holder.add_reagent(typeof(datum_reagent_nitrogen), 1); // In reality the nitrogen is in the form of ammonia or nitrates but this might be too OP and boring.
                    // Lack of nutrients hurts non-weeds
                    else if (myseed.get_gene(typeof(datum_plant_gene_trait_plant_type_weed_hardy)) == null)
                        adjust_plant_health(-rand(1, 3));
                }

//Photosynthesis/////////////////////////////////////////////////////////
                // Lack of light hurts non-mushrooms
                if (Dm.truthy((loc as turf)?.check_lumcount_below(is_fungus ? 0.2f : 0.4f)))
                    adjust_plant_health((is_fungus ? -1 : -2) / Dm.num(rating));

//Water//////////////////////////////////////////////////////////////////
                // Drink random amount of water
                adjust_waterlevel(-rand(1, 6) * (Dm.truthy(Dm.bitand(tray_flags, SUPERWATER)) ? SUPER_WATER_MODIFIER : 1) / Dm.num(rating));

                // If the plant is dry, it loses health pretty fast, unless mushroom
                if (waterlevel <= 10 && !is_fungus)
                {
                    adjust_plant_health(-rand(0, 1) / Dm.num(rating));
                    if (waterlevel <= 0)
                        adjust_plant_health(-rand(0, 2) / Dm.num(rating));
                }

                // Sufficient water level and nutrient level = plant healthy but also spawns weeds
                else if (waterlevel > 10 && Dm.num(holder.total_volume) > 0)
                {
                    adjust_plant_health(rand(1, 2) / Dm.num(rating));
                    if (myseed != null && prob(myseed.weed_chance))
                        adjust_weedlevel(myseed.weed_rate);
                    else if (prob(5))  //5 percent chance the weed population will increase
                        adjust_weedlevel(1 / Dm.num(rating));
                }

//Toxins/////////////////////////////////////////////////////////////////

                // Too much toxins cause harm, but when the plant drinks the contaiminated water, the toxins disappear slowly
                if (toxic >= 10)
                {
                    if (myseed.get_gene(typeof(datum_plant_gene_trait_plant_type_toxin_adaptation)) != null)
                    {
                        adjust_plant_health(Dm.round(toxic / rand(10, 16)));
                        myseed.adjust_potency(Dm.round(toxic / rand(20, 30)));
                    }

                    else if (toxic >= 40 && myseed.get_gene(typeof(datum_plant_gene_trait_tox_resistance)) == null)
                    {
                        if (toxic < 80)
                        {
                            adjust_plant_health(-1 / Dm.num(rating));
                            adjust_toxic(-Dm.num(rating) * 2);
                        }
                        else
                        {
                            adjust_plant_health(-3);
                            adjust_toxic(-Dm.num(rating) * 3);
                        }
                    }
                }

//Pests & Weeds//////////////////////////////////////////////////////////

                if (pestlevel >= 8)
                {
                    if (myseed.get_gene(typeof(datum_plant_gene_trait_carnivory)) == null)
                    {
                        if (Dm.num(myseed.potency) >= 30)
                            // Pests eat leaves and nibble on fruit, lowering potency.
                            myseed.set_potency(Dm.min(Dm.num(myseed.potency) - rand(2, 6), CARNIVORY_POTENCY_MIN));
                    }
                    else
                    {
                        adjust_plant_health(2 / Dm.num(rating));
                        adjust_pestlevel(-1 / Dm.num(rating));
                    }
                }

                else if (pestlevel >= 4)
                {
                    if (myseed.get_gene(typeof(datum_plant_gene_trait_carnivory)) == null)
                    {
                        if (Dm.num(myseed.potency) >= 30)
                            myseed.set_potency(Dm.min(Dm.num(myseed.potency) - rand(1, 4), CARNIVORY_POTENCY_MIN));
                    }

                    else
                    {
                        adjust_plant_health(1 / Dm.num(rating));
                        if (prob(50))
                            adjust_pestlevel(-1 / Dm.num(rating));
                    }
                }

                else if (pestlevel < 4 && myseed.get_gene(typeof(datum_plant_gene_trait_carnivory)) != null)
                {
                    if (prob(5))
                        adjust_pestlevel(-1 / Dm.num(rating));
                }

                // If it's a weed, it doesn't stunt the growth
                if (weedlevel >= 5 && myseed.get_gene(typeof(datum_plant_gene_trait_plant_type_weed_hardy)) == null)
                {
                    if (myseed.yield >= 3)
                    {
                        myseed.adjust_yield(-rand(1, 2)); //Weeds choke out the plant's ability to bear more fruit.
                        myseed.set_yield(Dm.clamp(myseed.yield, WEED_HARDY_YIELD_MIN, MAX_PLANT_YIELD));
                    }
                }

//This is the part with pollination
                await pollinate();

//This is where stability mutations exist now.
                if (myseed.instability >= 80)
                    traitmutate(myseed.instability - 75); //Scaling odds of a random trait or chemical
                if (myseed.instability >= 60)
                {
                    if (prob(myseed.instability / 2) && !Dm.truthy(self_sustaining) && Dm.truthy(LAZYLEN(myseed.mutatelist)) && myseed.get_gene(typeof(datum_plant_gene_trait_never_mutate)) == null) //Minimum 30%, Maximum 50% chance of mutating every age tick when not on autogrow or having Prosophobic Inclination trait.
                    {
                        await mutatespecie();
                        myseed.set_instability(myseed.instability / 2);
                    }
                }
                if (myseed.instability >= 20 && prob(myseed.instability) && myseed.get_gene(typeof(datum_plant_gene_trait_stable_stats)) == null) //No hardmutation if Symbiotic Resilience trait is present.
                {
                    if (myseed.instability >= 40)
                        hardmutate(stabmut: myseed.instability >= 80 ? 5 : 0);
                    else
                        mutate(stabmut: 0);
                }

//Health & Age///////////////////////////////////////////////////////////

                // Plant dies if plant_health <= 0
                if (Dm.num(plant_health) <= 0)
                {
                    plantdies();
                    adjust_weedlevel(1 / Dm.num(rating)); // Weeds flourish
                }

                // If the plant is too old, lose health fast
                if (Dm.num(age) > Dm.num(myseed.lifespan))
                    adjust_plant_health(-rand(1, 5) / Dm.num(rating));

                // Harvest code
                if (Dm.num(age) > Dm.num(myseed.production) && (Dm.num(age) - lastproduce) > Dm.num(myseed.production) && plant_status == HYDROTRAY_PLANT_GROWING)
                {
                    if (myseed != null && myseed.yield != -1) // Unharvestable shouldn't be harvested
                        set_plant_status(HYDROTRAY_PLANT_HARVESTABLE);
                    else
                        lastproduce = Dm.num(age);
                }
                if (prob(5))  // On each tick, there's a 5 percent chance the pest population will increase
                    adjust_pestlevel(1 / Dm.num(rating));
            }
            else
            {
                if (waterlevel > 10 && Dm.num(((datum_reagents)reagents).total_volume) > 0 && prob(10))  // If there's no plant, the percentage chance is 10%
                    adjust_weedlevel(1 / Dm.num(rating));
            }

            // Weeeeeeeeeeeeeeedddssss
            if (weedlevel >= 10 && prob(50) && !Dm.truthy(self_sustaining)) // At this point the plant is kind of fucked. Weeds can overtake the plant spot.
            {
                if (myseed != null && myseed.yield >= 3)
                {
                    myseed.adjust_yield(-rand(1, 2)); //Loses even more yield per tick, quickly dropping to 3 minimum.
                    myseed.set_yield(Dm.clamp(myseed.yield, WEED_HARDY_YIELD_MIN, MAX_PLANT_YIELD));
                }
                if (myseed == null)
                    await weedinvasion();
                needs_update = true;
            }
            if (needs_update)
                update_appearance();

            if (myseed != null && Dm.truthy(myseed._listen_lookup?[COMSIG_SEED_ON_GROW]))
                myseed._SendSignal(COMSIG_SEED_ON_GROW, Dm.list(myseed, this));
        }

        return null;
    }

    // tg: code/modules/hydroponics/hydroponics.dm:455
    public override object update_appearance(float? updates = null)
    {
        object dot = base.update_appearance(updates);
        if (Dm.truthy(self_sustaining))
        {
            Waiting.NoSleep(set_light(3));
            return dot;
        }
        if (myseed?.get_gene(typeof(datum_plant_gene_trait_glow)) != null) // Hydroponics needs a refactor, badly.
        {
            datum_plant_gene_trait_glow G = myseed.get_gene(typeof(datum_plant_gene_trait_glow)) as datum_plant_gene_trait_glow;
            Waiting.NoSleep(set_light(G.glow_range(myseed), G.glow_power(myseed), G.glow_color));
            return dot;
        }
        Waiting.NoSleep(set_light(0));
        return dot;
    }

    // tg: code/modules/hydroponics/hydroponics.dm:466
    public override object update_name(float? updates = null)
    {
        object dot = base.update_name(updates);
        if (GetComponent(typeof(datum_component_rename)) != null)
            return dot;
        name = current_soil != null ? "botanic tray" : Dm.initial(this, (atom a) => a.name);
        if (myseed != null)
            name += Dm.text(" ([])", myseed.plantname);
        return dot;
    }

    // tg: code/modules/hydroponics/hydroponics.dm:474
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        if (GetComponent(typeof(datum_component_rename)) != null)
            return dot;
        desc = Dm.initial(this, (atom a) => a.desc);
        if (current_soil != null)
            desc += Dm.text(" Filled with [].", current_soil.name);
        return dot;
    }

    // tg: code/modules/hydroponics/hydroponics.dm:482
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (myseed != null)
        {
            Dm.append(ref dot, myseed.get_tray_overlay(age, plant_status, plant_offset_y));
            Dm.append(ref dot, update_status_light_overlays());
        }

        if (current_soil != null)
        {
            string soil_overlay = Dm.text("[]_tray", current_soil.icon_state);
            Dm.append(ref dot, mutable_appearance(icon, soil_overlay, OBJ_LAYER + 0.001f));
        }

        if (Dm.truthy(self_sustaining) && Dm.truthy(self_sustaining_overlay_icon_state))
        {
            Dm.append(ref dot, mutable_appearance(icon, self_sustaining_overlay_icon_state, OBJ_LAYER + 0.002f));
            // DEVIATION(DV12): . += emissive_appearance(icon, self_sustaining_overlay_icon_state, src, OBJ_LAYER + 0.002), the glow on the emissive plane
        }
        return dot;
    }

    // tg: code/modules/hydroponics/hydroponics.dm:496
    public virtual object update_status_light_overlays()
    {
        DmList dot = Dm.list();
        string indicatorsuffix = Dm.truthy(alt_tray) ? "-alt" : "";
        // DEVIATION(DV12): each light's emissive_appearance(icon, state, src, alpha = src.alpha) twin on the emissive plane is not created
        if (waterlevel <= 10)
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/obj/service/hydroponics/equipment.dmi"), Dm.text("over_lowwater3[]", indicatorsuffix)));
        if (Dm.num(((datum_reagents)reagents).total_volume) <= 2)
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/obj/service/hydroponics/equipment.dmi"), Dm.text("over_lownutri3[]", indicatorsuffix)));
        if (Dm.num(plant_health) <= (myseed.endurance / 2))
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/obj/service/hydroponics/equipment.dmi"), Dm.text("over_lowhealth3[]", indicatorsuffix)));
        if (weedlevel >= 5 || pestlevel >= 5 || toxic >= 40)
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/obj/service/hydroponics/equipment.dmi"), Dm.text("over_alert3[]", indicatorsuffix)));
        if (plant_status == HYDROTRAY_PLANT_HARVESTABLE)
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/obj/service/hydroponics/equipment.dmi"), Dm.text("over_harvest3[]", indicatorsuffix)));
        return dot;
    }

    ///Sets a new value for the myseed variable, which is the seed of the plant that's growing inside the tray.
    // tg: code/modules/hydroponics/hydroponics.dm:516
    public virtual async DmTask set_seed(obj_item_seeds new_seed = null, float? delete_old_seed = null)
    {
        delete_old_seed ??= TRUE;
        obj_item_seeds old_seed = myseed;
        myseed = new_seed;
        foreach (datum_plant_gene_trait gene in Dm.each<datum_plant_gene_trait>(old_seed?.genes))
            gene.on_unplanted_from_tray(this, old_seed);
        if (old_seed != null && Dm.truthy(delete_old_seed))
            qdel(old_seed);
        set_plant_status(new_seed != null ? HYDROTRAY_PLANT_GROWING : HYDROTRAY_NO_PLANT); //To make sure they can't just put in another seed and insta-harvest it
        if (myseed != null && myseed.loc != this)
            await myseed.forceMove(this);
        if (Dm.truthy(_listen_lookup?[COMSIG_HYDROTRAY_SET_SEED]))
            _SendSignal(COMSIG_HYDROTRAY_SET_SEED, Dm.list(this, new_seed));
        foreach (datum_plant_gene_trait gene in Dm.each<datum_plant_gene_trait>(myseed?.genes))
            gene.on_plant_in_tray(this, myseed);
        age = 0;
        update_appearance();
        // if(isnull(myseed)) remove_shared_particles(/particles/pollen): particles are not ported
    }

    /*
     * Setter proc to set a tray to a new self_sustaining state and update all values associated with it.
     *
     * new_value - true / false value that self_sustaining is being set to
     */
    // tg: code/modules/hydroponics/hydroponics.dm:540
    public virtual void set_self_sustaining(object new_value = null)
    {
        if (Dm.eq(self_sustaining, new_value))
            return;

        self_sustaining = Dm.num(new_value);

        update_use_power(Dm.truthy(self_sustaining) ? ACTIVE_POWER_USE : NO_POWER_USE);
        update_appearance();

        if (Dm.truthy(_listen_lookup?[COMSIG_HYDROTRAY_SET_SELFSUSTAINING]))
            _SendSignal(COMSIG_HYDROTRAY_SET_SELFSUSTAINING, Dm.list(this, new_value));
    }

    // tg: code/modules/hydroponics/hydroponics.dm:551
    public virtual void set_weedlevel(object new_weedlevel = null, float? update_icon = null)
    {
        update_icon ??= TRUE;
        if (Dm.eq(weedlevel, new_weedlevel))
            return;
        if (Dm.truthy(_listen_lookup?[COMSIG_HYDROTRAY_SET_WEEDLEVEL]))
            _SendSignal(COMSIG_HYDROTRAY_SET_WEEDLEVEL, Dm.list(this, new_weedlevel));
        weedlevel = Dm.max(Dm.num(new_weedlevel), 0);
        if (Dm.truthy(update_icon))
            update_appearance();
    }

    // tg: code/modules/hydroponics/hydroponics.dm:559
    public virtual void set_pestlevel(object new_pestlevel = null, float? update_icon = null)
    {
        update_icon ??= TRUE;
        if (Dm.eq(pestlevel, new_pestlevel))
            return;
        if (Dm.truthy(_listen_lookup?[COMSIG_HYDROTRAY_SET_PESTLEVEL]))
            _SendSignal(COMSIG_HYDROTRAY_SET_PESTLEVEL, Dm.list(this, new_pestlevel));
        pestlevel = Dm.num(new_pestlevel);
        if (Dm.truthy(update_icon))
            update_appearance();
    }

    // tg: code/modules/hydroponics/hydroponics.dm:567
    public virtual void set_waterlevel(object new_waterlevel = null, float? update_icon = null)
    {
        update_icon ??= TRUE;
        if (Dm.eq(waterlevel, new_waterlevel))
            return;
        if (Dm.truthy(_listen_lookup?[COMSIG_HYDROTRAY_SET_WATERLEVEL]))
            _SendSignal(COMSIG_HYDROTRAY_SET_WATERLEVEL, Dm.list(this, new_waterlevel));
        waterlevel = Dm.num(new_waterlevel);
        if (Dm.truthy(update_icon))
            update_appearance();

        float difference = Dm.num(new_waterlevel) - waterlevel;
        if (difference > 0)
            adjust_toxic(-Dm.round(difference / 4));//Toxicity dilutation code. The more water you put in, the lesser the toxin concentration.
    }

    // tg: code/modules/hydroponics/hydroponics.dm:579
    public virtual void set_plant_health(object new_plant_health = null, float? update_icon = null, float? forced = null)
    {
        update_icon ??= TRUE;
        forced ??= FALSE;
        if (Dm.eq(plant_health, new_plant_health) || ((myseed == null || plant_status == HYDROTRAY_PLANT_DEAD) && !Dm.truthy(forced)))
            return;
        if (Dm.truthy(_listen_lookup?[COMSIG_HYDROTRAY_SET_PLANT_HEALTH]))
            _SendSignal(COMSIG_HYDROTRAY_SET_PLANT_HEALTH, Dm.list(this, new_plant_health));
        plant_health = new_plant_health;
        if (Dm.truthy(update_icon))
            update_appearance();
    }

    // tg: code/modules/hydroponics/hydroponics.dm:587
    public virtual void set_toxic(object new_toxic = null, float? update_icon = null)
    {
        update_icon ??= TRUE;
        if (Dm.eq(toxic, new_toxic))
            return;
        if (Dm.truthy(_listen_lookup?[COMSIG_HYDROTRAY_SET_TOXIC]))
            _SendSignal(COMSIG_HYDROTRAY_SET_TOXIC, Dm.list(this, new_toxic));
        toxic = Dm.num(new_toxic);
        if (Dm.truthy(update_icon))
            update_appearance();
    }

    // tg: code/modules/hydroponics/hydroponics.dm:595
    public virtual void set_plant_status(object new_plant_status = null)
    {
        if (Dm.eq(plant_status, new_plant_status))
            return;
        if (Dm.truthy(_listen_lookup?[COMSIG_HYDROTRAY_SET_PLANT_STATUS]))
            _SendSignal(COMSIG_HYDROTRAY_SET_PLANT_STATUS, Dm.list(this, new_plant_status));
        plant_status = new_plant_status as string;
    }

    // The following procs adjust the hydroponics tray variables, and make sure that the stat doesn't go out of bounds.

    /**
     * Adjust water.
     * Raises or lowers tray water values by a set value. Adding water will dillute toxicity from the tray.
     * Returns the amount of water actually added/taken
     * * adjustamt - determines how much water the tray will be adjusted upwards or downwards.
     */
    // tg: code/modules/hydroponics/hydroponics.dm:609
    public virtual object adjust_waterlevel(object amt = null)
    {
        float initial_waterlevel = waterlevel;
        set_waterlevel(Dm.clamp(waterlevel + Dm.num(amt), 0, maxwater), FALSE);
        return waterlevel - initial_waterlevel;
    }

    /**
     * Adjust Health.
     * Raises the tray's plant_health stat by a given amount, with total health determined by the seed's endurance.
     * * adjustamt - Determines how much the plant_health will be adjusted upwards or downwards.
     */
    // tg: code/modules/hydroponics/hydroponics.dm:619
    public virtual void adjust_plant_health(object amt = null)
    {
        // clamp(x, 0, null): DM's clamp with a null high bound reads it as 0 (an empty tray's health stays 0)
        set_plant_health(Dm.clamp(Dm.num(plant_health) + Dm.num(amt), 0, myseed?.endurance ?? 0), FALSE);
    }

    /**
     * Adjust toxicity.
     * Raises the plant's toxic stat by a given amount.
     * * adjustamt - Determines how much the toxic will be adjusted upwards or downwards.
     */
    // tg: code/modules/hydroponics/hydroponics.dm:627
    public virtual void adjust_toxic(object amt = null)
    {
        set_toxic(Dm.clamp(toxic + Dm.num(amt), 0, MAX_TRAY_TOXINS), FALSE);
    }

    /**
     * Adjust Pests.
     * Raises the tray's pest level stat by a given amount.
     * * adjustamt - Determines how much the pest level will be adjusted upwards or downwards.
     */
    // tg: code/modules/hydroponics/hydroponics.dm:635
    public virtual void adjust_pestlevel(object amt = null)
    {
        set_pestlevel(Dm.clamp(pestlevel + Dm.num(amt), 0, MAX_TRAY_PESTS), FALSE);
    }

    // tg: code/modules/hydroponics/hydroponics.dm:638
    [SignalHandler]
    public virtual void remove_snail(mob source = null)
    {
        atom_movable mob_holder = source.loc as atom_movable;
        Waiting.NoSleep(mob_holder.forceMove(drop_location() as atom));
    }

    // tg: code/modules/hydroponics/hydroponics.dm:644
    public virtual void handle_snail()
    {
        if (prob(15))
            our_snail.handle_animation(); //our snail waddles throughout the tray
        if (prob(5))
            adjust_weedlevel(-2);
    }

    /**
     * Adjust Weeds.
     * Raises the plant's weed level stat by a given amount.
     * * adjustamt - Determines how much the weed level will be adjusted upwards or downwards.
     */
    // tg: code/modules/hydroponics/hydroponics.dm:655
    public virtual void adjust_weedlevel(object amt = null)
    {
        float weed_mod = 1;
        //If we are not a soil type tray and out plant is semiaquatic, increase weed.
        if (!Dm.truthy(Dm.bitand(tray_flags, SOIL)) && myseed?.get_gene(typeof(datum_plant_gene_trait_semiaquatic)) != null)
            weed_mod *= SEMIAQUATIC_SOIL_WEED_MALUS;
        set_weedlevel(Dm.clamp(weedlevel + Dm.num(amt) * weed_mod, 0, MAX_TRAY_WEEDS), FALSE);
    }

    // tg: code/modules/hydroponics/hydroponics.dm:662
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (myseed != null)
        {
            Dm.append(ref dot, span_info(I18n.T("It has [] planted.", span_name(Dm.text("[]", myseed.plantname)))));
            if (plant_status == HYDROTRAY_PLANT_DEAD)
                Dm.append(ref dot, span_warning(I18n.T("It's dead!")));
            else if (plant_status == HYDROTRAY_PLANT_HARVESTABLE)
                Dm.append(ref dot, span_info(I18n.T("It's ready to harvest.")));
            else if (Dm.num(plant_health) <= (myseed.endurance / 2))
                Dm.append(ref dot, span_warning(I18n.T("It looks unhealthy.")));
        }
        else
        {
            Dm.append(ref dot, span_info(I18n.T("It's empty.")));
        }

        Dm.append(ref dot, span_info(I18n.T("Water: []/[].", waterlevel, maxwater)));
        Dm.append(ref dot, span_info(I18n.T("Nutrient: []/[].", ((datum_reagents)reagents).total_volume, maxnutri)));
        if (Dm.truthy(self_sustaining))
            Dm.append(ref dot, span_info(I18n.T("The tray's autogrow is active, protecting it from species mutations, weeds, and pests.")));

        if (weedlevel >= 5)
            Dm.append(ref dot, span_warning(I18n.T("It's filled with weeds!")));
        if (pestlevel >= 5)
            Dm.append(ref dot, span_warning(I18n.T("It's filled with tiny worms!")));
        return dot;
    }

    /**
     * What happens when a tray's weeds grow too large.
     * Plants a new weed in an empty tray, then resets the tray.
     */
    // tg: code/modules/hydroponics/hydroponics.dm:689
    public virtual async DmTask weedinvasion()
    {
        string oldPlantName;
        if (myseed != null) // In case there's nothing in the tray beforehand
            oldPlantName = myseed.plantname;
        else
            oldPlantName = "empty tray";
        obj_item_seeds new_seed;
        float roll = rand(1, 18);
        if (roll >= 16 && roll <= 18) // randomly pick predominative weed
            new_seed = Dm.@new<obj_item_seeds_reishi>(this);
        else if (roll >= 14 && roll <= 15)
            new_seed = Dm.@new<obj_item_seeds_nettle>(this);
        else if (roll >= 12 && roll <= 13)
            new_seed = Dm.@new<obj_item_seeds_harebell>(this);
        else if (roll >= 10 && roll <= 11)
            new_seed = Dm.@new<obj_item_seeds_amanita>(this);
        else if (roll >= 8 && roll <= 9)
            new_seed = Dm.@new<obj_item_seeds_chanter>(this);
        else if (roll >= 6 && roll <= 7)
            new_seed = Dm.@new<obj_item_seeds_tower>(this);
        else if (roll >= 4 && roll <= 5)
            new_seed = Dm.@new<obj_item_seeds_plump>(this);
        else
            new_seed = Dm.@new<obj_item_seeds_starthistle>(this);
        await set_seed(new_seed);
        lastcycle = world.time;
        set_plant_health(myseed.endurance, update_icon: FALSE);
        set_weedlevel(0, update_icon: FALSE); // Reset
        set_pestlevel(0); // Reset
        visible_message(span_warning(I18n.T("The [] is overtaken by some []!", oldPlantName, myseed.plantname)));
    }

    /// Mutates the stats of the current seed
    // tg: code/modules/hydroponics/hydroponics.dm:721
    public virtual void mutate(float? lifemut = null, float? endmut = null, float? productmut = null, float? yieldmut = null, float? potmut = null, float? wrmut = null, float? wcmut = null, float? traitmut = null, float? stabmut = null) // Mutates the current seed
    {
        myseed?.mutate(
            lifemut: lifemut ?? 2,
            endmut: endmut ?? 5,
            productmut: productmut ?? 1,
            yieldmut: yieldmut ?? 2,
            potmut: potmut ?? 25,
            wrmut: wrmut ?? 2,
            wcmut: wcmut ?? 5,
            traitmut: traitmut ?? 0,
            stabmut: stabmut ?? 3);
    }

    /// Mutate but with higher default values
    // tg: code/modules/hydroponics/hydroponics.dm:735
    public virtual void hardmutate(float? lifemut = null, float? endmut = null, float? productmut = null, float? yieldmut = null, float? potmut = null, float? wrmut = null, float? wcmut = null, float? traitmut = null, float? stabmut = null)
    {
        myseed?.mutate(
            lifemut: lifemut ?? 4,
            endmut: endmut ?? 10,
            productmut: productmut ?? 2,
            yieldmut: yieldmut ?? 4,
            potmut: potmut ?? 50,
            wrmut: wrmut ?? 4,
            wcmut: wcmut ?? 10,
            traitmut: traitmut ?? 0,
            stabmut: stabmut ?? 4);
    }

    /// Mutate but only introduce a random trait
    // tg: code/modules/hydroponics/hydroponics.dm:749
    public virtual void traitmutate(float? traitmut = null)
    {
        myseed?.mutate(
            lifemut: 0,
            endmut: 0,
            productmut: 0,
            yieldmut: 0,
            potmut: 0,
            wrmut: 0,
            wcmut: 0,
            traitmut: traitmut ?? 1,
            stabmut: 0);
    }

    /// Mutate the species of the plant into one of its mutations
    // tg: code/modules/hydroponics/hydroponics.dm:763
    public virtual async DmTask mutatespecie()
    {
        if (myseed == null || plant_status == HYDROTRAY_PLANT_DEAD || !Dm.truthy(LAZYLEN(myseed.mutatelist)))
            return;

        string oldPlantName = myseed.plantname;
        object mutantseed = pick(myseed.mutatelist);
        await set_seed(Dm.@new((Type)mutantseed, this) as obj_item_seeds);

        hardmutate();
        set_plant_health(myseed.endurance, update_icon: FALSE);
        lastcycle = world.time;
        set_weedlevel(0, update_icon: FALSE);

        object message = span_warning(I18n.T("[] suddenly mutates into []!", oldPlantName, myseed.plantname));
        _addtimer(Dm.@new<datum_callback>(this, nameof(after_mutation), message), 0.5f * SECONDS, file: "code/modules/hydroponics/hydroponics.dm", line: 777);
    }

    /// Transform the plant into a completely random species
    // tg: code/modules/hydroponics/hydroponics.dm:780
    public virtual async DmTask polymorph()
    {
        if (myseed == null || plant_status == HYDROTRAY_PLANT_DEAD)
            return;

        string oldPlantName = myseed.plantname;
        object polymorph_seed = pick(subtypesof(typeof(obj_item_seeds)));
        await set_seed(Dm.@new((Type)polymorph_seed, this) as obj_item_seeds);

        hardmutate();
        set_plant_health(myseed.endurance, update_icon: FALSE);
        lastcycle = world.time;
        set_weedlevel(0, update_icon: FALSE);

        object message = span_warning(I18n.T("[] suddenly polymorphs into []!", oldPlantName, myseed.plantname));
        _addtimer(Dm.@new<datum_callback>(this, nameof(after_mutation), message), 0.5f * SECONDS, file: "code/modules/hydroponics/hydroponics.dm", line: 794);
    }

    /// Mutates the weeds in the tray into a random weed plant (which can overtake existing plants)
    // tg: code/modules/hydroponics/hydroponics.dm:797
    public virtual async DmTask mutateweed()
    {
        if (weedlevel <= 5)
        {
            visible_message(span_warning(I18n.T("The few weeds in [] seem to react, but only for a moment...", this)));
            return;
        }

        await set_seed(null);
        object newWeed = pick(typeof(obj_item_seeds_liberty), typeof(obj_item_seeds_angel), typeof(obj_item_seeds_nettle_death), typeof(obj_item_seeds_kudzu));
        await set_seed(Dm.@new((Type)newWeed, this) as obj_item_seeds);
        hardmutate();
        set_plant_health(myseed.endurance, update_icon: FALSE);
        lastcycle = world.time;
        set_weedlevel(0, update_icon: FALSE); // Reset

        object message = span_warning(I18n.T("The mutated weeds in [] spawn some []!", this, myseed.plantname));
        _addtimer(Dm.@new<datum_callback>(this, nameof(after_mutation), message), 0.5f * SECONDS, file: "code/modules/hydroponics/hydroponics.dm", line: 813);
    }

    /**
     * Called after plant mutation, update the appearance of the tray content and send a visible_message()
     */
    // tg: code/modules/hydroponics/hydroponics.dm:816
    public virtual void after_mutation(object message = null)
    {
        visible_message(message);
        update_appearance();
    }

    /**
     * Plant Death Proc.
     * Cleans up various stats for the plant upon death, including pests, harvestability, and plant health.
     */
    // tg: code/modules/hydroponics/hydroponics.dm:824
    public virtual void plantdies(float? update_icon = null)
    {
        update_icon ??= TRUE;
        set_plant_health(0, update_icon: FALSE, forced: TRUE);
        set_plant_status(HYDROTRAY_PLANT_DEAD);
        set_pestlevel(0, update_icon: FALSE); // Pests die
        lastproduce = 0;
        if (Dm.truthy(_listen_lookup?[COMSIG_HYDROTRAY_PLANT_DEATH]))
            _SendSignal(COMSIG_HYDROTRAY_PLANT_DEATH, Dm.list(this));
        // remove_shared_particles(/particles/pollen): particles are not ported. We shouldn't look like we're pollenating if we're dead.
        if (Dm.truthy(update_icon))
            update_appearance();
    }

    /**
     * Plant Cross-Pollination.
     * Checks all plants in the tray's oview range, then averages out the seed's potency, instability, and yield values.
     * If the seed's instability is >= 20, the seed donates one of its reagents to that nearby plant.
     * * Range - The Oview range of trays to which to look for plants to donate reagents.
     */
    // tg: code/modules/hydroponics/hydroponics.dm:840
    public virtual async DmTask pollinate(float? range = null)
    {
        range ??= 1;
        being_pollinated = FALSE;
        foreach (obj_machinery_hydroponics T in Dm.each<obj_machinery_hydroponics>(oview(range, this)))
        {
            //Here is where we check for window blocking.
            if (!Dm.truthy(await Adjacent(T)) && range <= 1)
                continue;
            if (T.myseed != null && T.plant_status != HYDROTRAY_PLANT_DEAD)
            {
                T.myseed.set_potency(Dm.round(Dm.num(T.myseed.potency) + (1f / 10) * (Dm.num(myseed.potency) - Dm.num(T.myseed.potency))));
                if (T.myseed.get_gene(typeof(datum_plant_gene_trait_safe_instability)) == null)
                    T.myseed.set_instability(Dm.round(T.myseed.instability + (1f / 10) * (myseed.instability - T.myseed.instability)));
                T.myseed.set_yield(Dm.round(T.myseed.yield + (1f / 2) * (myseed.yield - T.myseed.yield)));
                being_pollinated = TRUE;
                // add_shared_particles(/particles/pollen): particles are not ported
                if (myseed.instability >= 20 && prob(70) && Dm.truthy(length(T.myseed.reagents_add)))
                    myseed.perform_reagent_pollination(T.myseed);
            }
        }
        // if(!being_pollinated) remove_shared_particles(/particles/pollen): particles are not ported
    }

    /**
     * Bee pollinate proc.
     * Checks if the bee can pollinate the plant
     */
    // tg: code/modules/hydroponics/hydroponics.dm:862
    public virtual object can_bee_pollinate()
    {
        if (myseed == null)
            return FALSE;
        if (plant_status == HYDROTRAY_PLANT_DEAD || Dm.truthy(recent_bee_visit))
            return FALSE;
        return TRUE;
    }

    /**
     * Pest Mutation Proc.
     * When a tray is mutated with high pest values, it will spawn spiders.
     * * User - Person who last added chemicals to the tray for logging purposes.
     */
    // tg: code/modules/hydroponics/hydroponics.dm:874
    public virtual void mutatepest()
    {
        if (pestlevel <= 5)
        {
            visible_message(span_warning(I18n.T("The pests seem to behave oddly in [], but quickly settle down...", this)));
            return;
        }

        mob user = lastuser?.resolve() as mob;
        if (user != null)
        {
            message_admins(Dm.text("[] last altered a hydro tray's contents which spawned spiderlings.", ADMIN_LOOKUPFLW(user)));
            user.log_message("last altered a hydro tray, which spiderlings spawned from.", LOG_GAME);
        }

        visible_message(span_warning(I18n.T("The pests seem to behave oddly...")));
        spawn_atom_to_turf(typeof(mob_living_basic_spider_growing_spiderling_hunter), this, 3, FALSE);
    }

    // tg: code/modules/hydroponics/hydroponics.dm:887
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/hydroponics/hydroponics.dm:892
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (IS_EDIBLE(tool) || is_reagent_container(tool))  // Syringe stuff (and other reagent containers now too)
        {
            obj_item reagent_source = tool;
            datum_reagents source_reagents = reagent_source.reagents as datum_reagents;
            datum_reagents our_reagents = reagents as datum_reagents;

            if (!Dm.truthy(source_reagents.total_volume))
            {
                to_chat(user, span_warning(I18n.T("[] is empty!", reagent_source)));
                return ITEM_INTERACT_BLOCKING;
            }

            if (Dm.num(our_reagents.total_volume) >= Dm.num(our_reagents.maximum_volume) && !Dm.truthy(source_reagents.has_reagent(typeof(datum_reagent_water), 1)))
            {
                to_chat(user, span_notice(I18n.T("[] is full.", this)));
                return ITEM_INTERACT_BLOCKING;
            }

            DmList trays = Dm.list(this);//makes the list just this in cases of syringes and compost etc
            object target = myseed != null ? myseed.plantname : this;
            string visi_msg = "";
            float transfer_amount;

            if (IS_EDIBLE(reagent_source))
            {
                if (HAS_TRAIT(reagent_source, TRAIT_UNCOMPOSTABLE))
                {
                    to_chat(user, I18n.T("[] cannot be composted in its current state.", reagent_source));
                    return ITEM_INTERACT_BLOCKING;
                }
                visi_msg = I18n.T("[] composts [], spreading it through []", user, reagent_source, target);
                transfer_amount = Dm.num(source_reagents.total_volume);
                if (Dm.truthy(reagent_source._listen_lookup?[COMSIG_ITEM_ON_COMPOSTED]))
                    reagent_source._SendSignal(COMSIG_ITEM_ON_COMPOSTED, Dm.list(reagent_source, user));
                if (Dm.truthy(Dm.bitand(tray_flags, WORM_HABITAT)) && prob(transfer_amount / 2))
                {
                    obj_item_food_bait_worm_premium fat_worm = Dm.@new<obj_item_food_bait_worm_premium>(loc);
                    fat_worm.pixel_x = rand(-6, 6);
                    fat_worm.pixel_y = rand(-6, 6);
                    playsound(fat_worm, Dm.resource("sound/items/eatfood.ogg"), 20, TRUE);
                }
            }
            else
            {
                obj_item_reagent_containers container = reagent_source as obj_item_reagent_containers;
                transfer_amount = Dm.min(Dm.num(container.amount_per_transfer_from_this), Dm.num(source_reagents.total_volume));
                if (reagent_source is obj_item_reagent_containers_syringe)
                    visi_msg = I18n.T("[] injects [] with []", user, target, reagent_source);
                // Beakers, bottles, buckets, etc.
                if (Dm.truthy(reagent_source.is_drainable()))
                {
                    playsound(loc, Dm.resource("sound/effects/slosh.ogg"), 25, TRUE);
                    mutable_appearance splash_animation = mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "splash_hydroponics") as mutable_appearance;
                    splash_animation.color = mix_color_from_reagents(source_reagents.reagent_list);
                    flick_overlay_view(splash_animation, 1.1f * SECONDS);
                }
            }

            if (Dm.truthy(visi_msg))
                visible_message(span_notice(Dm.text("[].", visi_msg)));

            foreach (obj_machinery_hydroponics iterating_tray in Dm.each<obj_machinery_hydroponics>(trays))
            {
            //cause I don't want to feel like im juggling 15 tamagotchis and I can get to my real work of ripping flooring apart in hopes of validating my life choices of becoming a space-gardener
                //This was originally in apply_chemicals, but due to apply_chemicals only holding nutrients, we handle it here now.
                if (Dm.truthy(source_reagents.has_reagent(typeof(datum_reagent_water))))
                {
                    float water_amt = Dm.num(source_reagents.get_reagent_amount(typeof(datum_reagent_water))) * transfer_amount / Dm.num(source_reagents.total_volume);
                    object water_amt_adjusted = iterating_tray.adjust_waterlevel(Dm.round(water_amt));
                    await source_reagents.remove_reagent(typeof(datum_reagent_water), water_amt_adjusted);
                    foreach (datum_reagent not_water_reagent in Dm.each_cast<datum_reagent>(source_reagents.reagent_list))
                    {
                        if (not_water_reagent is datum_reagent_water)
                            continue;
                        float transfer_me_to_tray = Dm.num(source_reagents.get_reagent_amount(not_water_reagent.type)) * transfer_amount / Dm.num(source_reagents.total_volume);
                        await source_reagents.trans_to(iterating_tray.reagents, transfer_me_to_tray, target_id: not_water_reagent.type);
                    }
                }
                else
                {
                    await source_reagents.trans_to(iterating_tray.reagents, transfer_amount, transferred_by: user);
                }
                lastuser = WEAKREF(user) as datum_weakref;
                if (IS_EDIBLE(reagent_source) || reagent_source is obj_item_reagent_containers_applicator_pill)
                {
                    qdel(reagent_source);
                    iterating_tray.update_appearance();
                    return ITEM_INTERACT_SUCCESS;
                }

                iterating_tray.update_appearance();
            }
            if (reagent_source != null) // If the source wasn't composted and destroyed
                reagent_source.update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_seeds)
        {
            await propagate_plant(tool as obj_item_seeds, user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_cultivator)
        {
            if (!Dm.truthy(weedlevel))
            {
                to_chat(user, span_warning(I18n.T("This plot is completely devoid of weeds! It doesn't need uprooting.")));
                return ITEM_INTERACT_BLOCKING;
            }

            user.visible_message(span_notice(I18n.T("[] uproots the weeds.", user)), span_notice(I18n.T("You remove the weeds from [].", this)));
            set_weedlevel(0);
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_secateurs)
        {
            if (myseed == null)
            {
                to_chat(user, span_notice(I18n.T("This plot is empty.")));
                return ITEM_INTERACT_BLOCKING;
            }

            if (plant_status != HYDROTRAY_PLANT_HARVESTABLE)
            {
                to_chat(user, span_notice(I18n.T("This plant must be harvestable in order to be grafted.")));
                return ITEM_INTERACT_BLOCKING;
            }

            if (myseed.grafts_taken >= (Dm.truthy(Dm.bitand(tray_flags, MULTIGRAFT)) ? MULTI_GRAFT_MAX_COUNT : 1))
            {
                to_chat(user, span_notice(I18n.T("You can't take any more cuttings from this plant!")));
                return ITEM_INTERACT_BLOCKING;
            }
            user.visible_message(span_notice(I18n.T("[] grafts off a limb from [].", user, this)), span_notice(I18n.T("You carefully graft off a portion of [].", this)));
            obj_item_graft snip = myseed.create_graft() as obj_item_graft;
            if (snip == null)
                return ITEM_INTERACT_BLOCKING; // The plant did not return a graft.

            await snip.forceMove(drop_location() as atom);
            myseed.grafts_taken++;
            adjust_plant_health(-5);
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_geneshears)
        {
            if (myseed == null)
            {
                to_chat(user, span_notice(I18n.T("The tray is empty.")));
                return ITEM_INTERACT_BLOCKING;
            }

            if (Dm.num(plant_health) <= GENE_SHEAR_MIN_HEALTH)
            {
                to_chat(user, span_notice(I18n.T("This plant looks too unhealty to be sheared right now.")));
                return ITEM_INTERACT_BLOCKING;
            }

            DmList current_traits = Dm.list();
            foreach (object gene_obj in Dm.each(myseed.genes))
            {
                if (gene_obj is not datum_plant_gene gene)
                    continue;
                if (islist(gene))
                    continue;
                if (!Dm.truthy(Dm.bitand(gene.mutability_flags, PLANT_GENE_REMOVABLE)))
                    continue; // Don't show genes that can't be removed.
                current_traits[gene.name] = gene;
            }
            object removed_trait = await tgui_input_list(user, I18n.T("Trait to remove from the []", myseed.plantname), "Plant Trait Removal", sort_list(current_traits) as DmList);
            if (removed_trait == null)
                return ITEM_INTERACT_BLOCKING;

            if (!Dm.truthy(await user.can_perform_action(this)))
                return ITEM_INTERACT_BLOCKING;

            if (myseed == null)
                return ITEM_INTERACT_BLOCKING;

            if (Dm.num(plant_health) <= GENE_SHEAR_MIN_HEALTH) //Check health again to make sure they're not keeping inputs open to get free shears.
                return ITEM_INTERACT_BLOCKING;

            foreach (datum_plant_gene gene in Dm.each<datum_plant_gene>(myseed.genes))
            {
                if (Dm.eq(gene.name, removed_trait))
                {
                    if (Dm.truthy(myseed.genes.Remove(gene)))
                    {
                        gene.on_removed(myseed);
                        qdel(gene);
                        break;
                    }
                }
            }
            myseed.reagents_from_genes();
            adjust_plant_health(-15);
            to_chat(user, span_notice(I18n.T("You carefully shear the genes off of the [], leaving the plant looking weaker.", myseed.plantname)));
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_graft snip_graft)
        {
            if (myseed == null)
            {
                if (!Dm.truthy(Dm.bitand(tray_flags, GRAFT_MEDIUM)))
                {
                    to_chat(user, span_notice(I18n.T("The tray is empty.")));
                    return ITEM_INTERACT_BLOCKING;
                }

                await propagate_plant(snip_graft.plant_dna as obj_item_seeds, user);
                qdel(snip_graft);
                return ITEM_INTERACT_SUCCESS;
            }

            object dot;
            datum_plant_gene grafted_trait = myseed.apply_graft(snip_graft) as datum_plant_gene;
            if (grafted_trait != null)
            {
                to_chat(user, span_notice(I18n.T("You carefully integrate the grafted plant limb onto [], granting it [].", myseed.plantname, grafted_trait.get_name())));
                dot = ITEM_INTERACT_SUCCESS;
            }
            else
            {
                to_chat(user, span_notice(I18n.T("You try to integrate the grafted plant limb onto [], but it rejects the trait from the [].", myseed.plantname, snip_graft)));
                dot = ITEM_INTERACT_BLOCKING;
            }
            qdel(snip_graft);
            return dot;
        }

        if (tool is obj_item_storage_bag_plants)
        {
            if (plant_status == HYDROTRAY_PLANT_HARVESTABLE)
            {
                DmList harvest = await myseed.harvest(user) as DmList;
                foreach (obj_item_food_grown bounty in Dm.each<obj_item_food_grown>(harvest))
                    tool.atom_storage?.attempt_insert(bounty, user, TRUE);
                return ITEM_INTERACT_SUCCESS;
            }

            if (plant_status == HYDROTRAY_PLANT_DEAD)
            {
                to_chat(user, span_notice(I18n.T("You remove the dead plant from [].", this)));
                await set_seed(null);
                return ITEM_INTERACT_SUCCESS;
            }

            return NONE;
        }

        if (Dm.eq(tool.tool_behaviour, TOOL_SHOVEL))
        {
            if (myseed == null && !Dm.truthy(weedlevel))
            {
                to_chat(user, span_warning(I18n.T("[] doesn't have any plants or weeds!", this)));
                return ITEM_INTERACT_BLOCKING;
            }

            user.visible_message(span_notice(I18n.T("[] starts digging out []'s plants...", user, this)),
                                span_notice(I18n.T("You start digging out []'s plants...", this)));
            if (!Dm.truthy(await tool.use_tool(this, user, 5 * SECONDS, volume: 50)) || (myseed == null && !Dm.truthy(weedlevel)))
                return ITEM_INTERACT_BLOCKING;

            user.visible_message(span_notice(I18n.T("[] digs out the plants in []!", user, this)), span_notice(I18n.T("You dig out all of []'s plants!", this)));
            await remove_plant();
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_gun_energy_floragun flowergun)
        {
            obj_item_stock_parts_power_store gun_cell = flowergun.cell as obj_item_stock_parts_power_store;
            if (Dm.num(gun_cell.charge) < Dm.num(gun_cell.maxcharge))
            {
                to_chat(user, span_notice(I18n.T("[] must be fully charged to lock in a mutation!", flowergun)));
                return ITEM_INTERACT_BLOCKING;
            }

            if (myseed == null)
            {
                to_chat(user, span_warning(I18n.T("[] is empty!", this)));
                return ITEM_INTERACT_BLOCKING;
            }

            if (myseed.endurance <= FLORA_GUN_MIN_ENDURANCE)
            {
                to_chat(user, span_warning(I18n.T("[] isn't hardy enough to sequence its mutation!", myseed.plantname)));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(LAZYLEN(myseed.mutatelist)))
            {
                to_chat(user, span_warning(I18n.T("[] has nothing else to mutate into!", myseed.plantname)));
                return ITEM_INTERACT_BLOCKING;
            }

            DmList fresh_mut_list = Dm.list();
            foreach (object muties in Dm.each(myseed.mutatelist))
            {
                obj_item_seeds another_mut = Dm.@new((Type)muties) as obj_item_seeds;
                fresh_mut_list[another_mut.plantname] = muties;
            }
            object locked_mutation = await tgui_input_list(user, "Mutation to lock", "Plant Mutation Locks", sort_list(fresh_mut_list) as DmList);
            if (locked_mutation == null)
                return ITEM_INTERACT_BLOCKING;

            if (fresh_mut_list[locked_mutation] == null)
                return ITEM_INTERACT_BLOCKING;

            if (!Dm.truthy(await user.can_perform_action(this)))
                return ITEM_INTERACT_BLOCKING;

            myseed.mutatelist = Dm.list(fresh_mut_list[locked_mutation]);
            myseed.set_endurance(myseed.endurance / 2);
            await gun_cell.use(gun_cell.charge);
            flowergun.update_appearance();
            to_chat(user, span_notice(I18n.T("[]'s mutation was set to [], depleting []'s cell!", myseed.plantname, locked_mutation, flowergun)));
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_soil_sack oursoil)
        {
            if (plant_status != HYDROTRAY_NO_PLANT)
            {
                balloon_alert(user, I18n.T("remove the plants first!"));
                return ITEM_INTERACT_BLOCKING;
            }

            if (current_soil != null)
            {
                balloon_alert(user, I18n.T("tray is full!"));
                return ITEM_INTERACT_BLOCKING;
            }

            balloon_alert(user, I18n.T("filling the tray..."));
            if (!Dm.truthy(await do_after(user, 2 * SECONDS, this)))
                return ITEM_INTERACT_BLOCKING;

            if ((plant_status != HYDROTRAY_NO_PLANT) || current_soil != null)
                return ITEM_INTERACT_BLOCKING;

            current_soil = await oursoil.transfer_soil(this, inside_tray: TRUE) as obj_machinery_hydroponics_soil;
            await RefreshParts();
            tray_flags = current_soil.tray_flags;

            qdel(oursoil);
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/hydroponics/hydroponics.dm:1152
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (tool is obj_item_reagent_containers_syringe)
        {
            to_chat(user, span_warning(I18n.T("You can't get any extract out of this plant.")));
            return ITEM_INTERACT_BLOCKING;
        }
        return NONE;
    }

    // tg: code/modules/hydroponics/hydroponics.dm:1158
    public override object can_be_unfasten_wrench(mob user = null, object silent = null)
    {
        if (!Dm.truthy(unwrenchable))  // case also covered by NODECONSTRUCT checks in default_unfasten_wrench
            return CANT_UNFASTEN;

        return base.can_be_unfasten_wrench(user, silent);
    }

    // tg: code/modules/hydroponics/hydroponics.dm:1164
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (HAS_SILICON_ACCESS(user)) //How does AI know what plant is?
            return dot;
        if (plant_status == HYDROTRAY_PLANT_HARVESTABLE)
            return await myseed.harvest(user);

        else if (plant_status == HYDROTRAY_PLANT_DEAD)
        {
            to_chat(user, span_notice(I18n.T("You remove the dead plant from [].", this)));
            await set_seed(null);
        }
        else
        {
            if (user != null)
                user.examinate(this);
        }
        return dot;
    }

    // tg: code/modules/hydroponics/hydroponics.dm:1180
    public override async DmTask<object> click_ctrl(mob user = null)
    {
        if (!Dm.truthy(anchored))
            return NONE;

        update_use_power(ACTIVE_POWER_USE);

        if (!Dm.truthy(powered__proc()))
        {
            to_chat(user, span_warning(I18n.T("[] has no power.", name)));
            update_use_power(NO_POWER_USE);
            return CLICK_ACTION_BLOCKING;
        }

        set_self_sustaining(!Dm.truthy(self_sustaining) ? TRUE : FALSE);
        to_chat(user, span_notice(I18n.T("You [] []'s autogrow function[].", Dm.truthy(self_sustaining) ? "activate" : "deactivated", this, Dm.truthy(self_sustaining) ? ", maintaining the tray's health while using high amounts of power" : "")));
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/hydroponics/hydroponics.dm:1195
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return dot;
        if (!Dm.truthy(anchored))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        object warning = await tgui_alert(user, "Are you sure you wish to empty the tray's nutrient beaker?", "Empty Tray Nutrients?", Dm.list("Yes", "No"));
        if (Dm.eq(warning, "Yes") && Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
            await empty_tray(user);
        update_appearance();
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/hydroponics/hydroponics.dm:1208
    public virtual async DmTask empty_tray(mob user = null)
    {
        await ((datum_reagents)reagents).clear_reagents();
        foreach (obj_item_mob_holder_snail possible_snail in Dm.each<obj_item_mob_holder_snail>(contents))
            await possible_snail.forceMove(drop_location() as atom);
        to_chat(user, span_warning(I18n.T("You empty []'s nutrient tank.", this)));
    }

    /**
     * Update Tray Proc
     * Handles plant harvesting on the tray side, by clearing the seed, names, description, and dead stat.
     * Shuts off autogrow if enabled.
     * Sends messages to the cleaer about plants harvested, or if nothing was harvested at all.
     * * User - The mob who clears the tray.
     */
    // tg: code/modules/hydroponics/hydroponics.dm:1221
    public virtual async DmTask update_tray(mob user = null, object product_count = null)
    {
        lastproduce = Dm.num(age);
        if (myseed is obj_item_seeds_replicapod)
            to_chat(user, span_notice(I18n.T("You harvest from the [].", myseed.plantname)));
        else if (Dm.num(product_count) <= 0)
            to_chat(user, span_warning(I18n.T("You fail to harvest anything useful!")));
        else
            to_chat(user, span_notice(I18n.T("You harvest [] items from the [].", product_count, myseed.plantname)));
        if (myseed.get_gene(typeof(datum_plant_gene_trait_repeated_harvest)) == null)
        {
            await set_seed(null);
            if (Dm.truthy(self_sustaining)) //No reason to pay for an empty tray.
                set_self_sustaining(FALSE);
        }
        else
        {
            set_plant_status(HYDROTRAY_PLANT_GROWING);
        }
        update_appearance();
        if (Dm.truthy(_listen_lookup?[COMSIG_HYDROTRAY_ON_HARVEST]))
            _SendSignal(COMSIG_HYDROTRAY_ON_HARVEST, Dm.list(this, user, product_count));
    }

    /**
     * Spawn Plant.
     * Upon using strange reagent on a tray, it will spawn a killer tomato or killer tree at random.
     */
    // tg: code/modules/hydroponics/hydroponics.dm:1242
    public virtual void spawnplant() // why would you put strange reagent in a hydro tray you monster I bet you also feed them blood
    {
        DmList livingplants = Dm.list(typeof(mob_living_basic_tree), typeof(mob_living_basic_killer_tomato));
        object chosen = pick(livingplants);
        mob_living C = Dm.@new((Type)chosen, get_turf(this)) as mob_living;
        C.set_faction(Dm.list(FACTION_PLANTS));
    }

    /// Plants the seed / graft into the tray and resets growth related stats such as maturity on the tray.
    // tg: code/modules/hydroponics/hydroponics.dm:1249
    public virtual async DmTask propagate_plant(obj_item_seeds young_plant = null, mob_living user = null)
    {
        if (young_plant == null)
            return;
        if (myseed != null)
        {
            to_chat(user, span_warning(I18n.T("[] already has a plant growing in it!", this)));
            return;
        }
        if (Dm.truthy(Dm.bitand(young_plant.seed_flags, NO_PLANTING)))
        {
            to_chat(user, span_warning(I18n.T("[] cannot be planted in []!", young_plant, this)));
            return;
        }
        if (istype(young_plant, typeof(obj_item_seeds_kudzu)))
            investigate_log(Dm.text("had Kudzu planted in it by [] at [].", key_name(user), AREACOORD(this)), INVESTIGATE_BOTANY);
        if (!Dm.truthy(await user.transferItemToLoc(young_plant, this)))
            return;
        if (Dm.truthy(young_plant._listen_lookup?[COMSIG_SEED_ON_PLANTED]))
            young_plant._SendSignal(COMSIG_SEED_ON_PLANTED, Dm.list(young_plant, this));
        to_chat(user, span_notice(I18n.T("You plant [].", young_plant)));
        await set_seed(young_plant);
        set_plant_health(myseed.endurance);
        lastcycle = world.time;
    }

    /// Clears the plant from the tray, killing it in the process, optionally clearing weeds as well.
    // tg: code/modules/hydroponics/hydroponics.dm:1269
    public virtual async DmTask remove_plant(float? clear_weeds = null)
    {
        clear_weeds ??= TRUE;
        if (myseed == null)
            return;
        plantdies(FALSE);
        if (Dm.truthy(clear_weeds))
            set_weedlevel(0, FALSE);
        if (Dm.truthy(self_sustaining)) //No reason to pay for an empty tray.
            set_self_sustaining(FALSE);
        await set_seed(null);
    }

    // tg: code/modules/hydroponics/hydroponics.dm:1330
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(arrived, old_loc, old_locs);
        if (arrived is not obj_item_mob_holder_snail snail)
            return dot;
        our_snail = Dm.@new<obj_effect_overlay_vis_effect_snail>();
        Dm.append(vis_contents, our_snail);
        our_snail.layer = layer + 0.01f;
        RegisterSignals(snail.held_mob, Dm.list(COMSIG_MOVABLE_ATTEMPTED_MOVE, COMSIG_LIVING_DEATH), nameof(remove_snail)); //rip
        return dot;
    }
}

// tg: code/modules/hydroponics/hydroponics.dm:166
public partial class obj_machinery_hydroponics_constructable : obj_machinery_hydroponics
{
    // tg: code/modules/hydroponics/hydroponics.dm:179
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_hydroponics)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_usb_port), typecacheof(Dm.list(typeof(obj_item_circuit_component_hydroponics)), only_root_path: TRUE)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_fishing_spot), typeof(datum_fish_source_hydro_tray)));
        return dot;
    }

    // tg: code/modules/hydroponics/hydroponics.dm:186
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        float tmp_capacity = 0;
        foreach (datum_stock_part_matter_bin matter_bin in Dm.each<datum_stock_part_matter_bin>(component_parts))
            tmp_capacity += Dm.num(matter_bin.tier);
        foreach (datum_stock_part_servo servo in Dm.each<datum_stock_part_servo>(component_parts))
            rating = Dm.num(servo.tier);
        if (current_soil != null)
        {
            maxwater = current_soil.maxwater + ((tmp_capacity - 2) * 50);
            maxnutri = current_soil.maxnutri + ((tmp_capacity - 2) * 5);
        }
        else
        {
            maxwater = tmp_capacity * 50;
            maxnutri = (tmp_capacity * 5) + STATIC_NUTRIENT_CAPACITY;
        }
        ((datum_reagents)reagents).maximum_volume = maxnutri;
        nutridrain = 1 / Dm.num(rating);

        // Active power draw reduction inspired by stasis units in code\game\machinery\stasis.dm
        // This really only matters if you're using the autogrow, because, by default, trays don't draw power.
        // Not using energy rating because they're nonlinear and make the power draw reduction too generous.
        float total_rating = 0;
        foreach (datum_stock_part part in Dm.each<datum_stock_part>(component_parts))
            total_rating += Dm.num(part.tier);

        /**
         * We sum up the part tier ratings, divide by how many upgradable parts we have (in this case, 3) for a modifier,
         * and divide the initial power usage by the modifier. Power draw thus becomes 1 kW / 500 W / 333.3 W / 250 W at time of writing.
         */
        idle_power_usage = Dm.num(Dm.initial(this, "idle_power_usage")) / (total_rating / 3);
        active_power_usage = Dm.num(Dm.initial(this, "active_power_usage")) / (total_rating / 3);
        update_current_power_usage();
        return dot;
    }

    // tg: code/modules/hydroponics/hydroponics.dm:217
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Use <b>Ctrl-Click</b> to activate autogrow. <b>RMB</b> to empty the tray's nutrients.")));
        if (Dm.truthy(in_range(user, this)) || isobserver(user))
            Dm.append(ref dot, span_notice(I18n.T("The status display reads: Tray efficiency at <b>[]%</b>.", Dm.num(rating) * 100)));
        return dot;
    }

    // tg: code/modules/hydroponics/hydroponics.dm:261
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/hydroponics/hydroponics.dm:264
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }
}

///The usb port circuit

// tg: code/modules/hydroponics/hydroponics.dm:1281
public partial class obj_item_circuit_component_hydroponics : obj_item_circuit_component
{
    // tg: code/modules/hydroponics/hydroponics.dm:1314
    public override async DmTask populate_ports()
    {
        selfsustaining_setting = add_input_port("Auto-Grow Setting", PORT_TYPE_BOOLEAN) as datum_port_input;

        plant_status = add_output_port("Plant Status", PORT_TYPE_NUMBER) as datum_port_output;
        is_self_sustaining = add_output_port("Auto-Grow Status", PORT_TYPE_BOOLEAN) as datum_port_output;
        plant_harvested = add_output_port("Plant Harvested", PORT_TYPE_SIGNAL) as datum_port_output;
        last_harvest = add_output_port("Last Harvest Amount", PORT_TYPE_NUMBER) as datum_port_output;
        plant_died = add_output_port("Plant Died", PORT_TYPE_SIGNAL) as datum_port_output;
        seeds_planted = add_output_port("Seeds Planted", PORT_TYPE_SIGNAL) as datum_port_output;
        water_level = add_output_port("Water Level", PORT_TYPE_NUMBER) as datum_port_output;
        toxic_level = add_output_port("Toxins Level", PORT_TYPE_NUMBER) as datum_port_output;
        pests_level = add_output_port("Pests Level", PORT_TYPE_NUMBER) as datum_port_output;
        weeds_level = add_output_port("Weeds Level", PORT_TYPE_NUMBER) as datum_port_output;
        plant_health = add_output_port("Plant Health", PORT_TYPE_NUMBER) as datum_port_output;
        reagents_level = add_output_port("Reagents Level", PORT_TYPE_NUMBER) as datum_port_output;
    }

    // tg: code/modules/hydroponics/hydroponics.dm:1341
    public override void register_usb_parent(atom_movable shell = null)
    {
        base.register_usb_parent(shell);
        if (istype(shell, typeof(obj_machinery_hydroponics)))
        {
            attached_tray = shell as obj_machinery_hydroponics;
            RegisterSignal(attached_tray, COMSIG_HYDROTRAY_SET_SEED, nameof(on_set_seed));
            RegisterSignal(attached_tray, COMSIG_HYDROTRAY_SET_SELFSUSTAINING, nameof(on_set_selfsustaining));
            RegisterSignal(attached_tray, COMSIG_HYDROTRAY_SET_WEEDLEVEL, nameof(on_set_weedlevel));
            RegisterSignal(attached_tray, COMSIG_HYDROTRAY_SET_PESTLEVEL, nameof(on_set_pestlevel));
            RegisterSignal(attached_tray, COMSIG_HYDROTRAY_SET_WATERLEVEL, nameof(on_set_waterlevel));
            RegisterSignal(attached_tray, COMSIG_HYDROTRAY_SET_PLANT_HEALTH, nameof(on_set_plant_health));
            RegisterSignal(attached_tray, COMSIG_HYDROTRAY_SET_TOXIC, nameof(on_set_toxic_level));
            RegisterSignal(attached_tray, COMSIG_HYDROTRAY_SET_PLANT_STATUS, nameof(on_set_plant_status));
            RegisterSignal(attached_tray, COMSIG_HYDROTRAY_ON_HARVEST, nameof(on_harvest));
            RegisterSignal(attached_tray, COMSIG_HYDROTRAY_PLANT_DEATH, nameof(on_plant_death));
            RegisterSignal(attached_tray.reagents as datum, COMSIG_REAGENTS_HOLDER_UPDATED, nameof(update_reagents_level));
        }
    }

    // tg: code/modules/hydroponics/hydroponics.dm:1357
    public override object unregister_usb_parent(atom_movable shell = null)
    {
        attached_tray = null;
        UnregisterSignal(shell, Dm.list(COMSIG_HYDROTRAY_SET_SEED, COMSIG_HYDROTRAY_SET_SELFSUSTAINING,
            COMSIG_HYDROTRAY_SET_WEEDLEVEL, COMSIG_HYDROTRAY_SET_PESTLEVEL, COMSIG_HYDROTRAY_SET_WATERLEVEL,
            COMSIG_HYDROTRAY_SET_PLANT_HEALTH, COMSIG_HYDROTRAY_SET_TOXIC, COMSIG_HYDROTRAY_SET_PLANT_STATUS,
            COMSIG_HYDROTRAY_ON_HARVEST, COMSIG_HYDROTRAY_PLANT_DEATH));
        if (shell.reagents != null)
            UnregisterSignal(shell.reagents as datum, COMSIG_REAGENTS_HOLDER_UPDATED);
        return base.unregister_usb_parent(shell);
    }

    // tg: code/modules/hydroponics/hydroponics.dm:1367
    public override object get_ui_notices()
    {
        object dot = base.get_ui_notices();
        Dm.append(ref dot, create_ui_notice(Dm.text("Plant Status Index: \"[]\", \"[]\", \"[]\", \"[]\"", HYDROTRAY_NO_PLANT, HYDROTRAY_PLANT_GROWING, HYDROTRAY_PLANT_DEAD, HYDROTRAY_PLANT_HARVESTABLE), "orange", "info"));
        return dot;
    }

    // tg: code/modules/hydroponics/hydroponics.dm:1371
    [SignalHandler]
    public virtual void on_set_seed(datum source = null, obj_item_seeds new_seed = null)
    {
        seeds_planted.set_output(COMPONENT_SIGNAL);
    }

    // tg: code/modules/hydroponics/hydroponics.dm:1375
    [SignalHandler]
    public virtual void on_set_selfsustaining(datum source = null, object new_value = null)
    {
        is_self_sustaining.set_output(new_value);
    }

    // tg: code/modules/hydroponics/hydroponics.dm:1379
    [SignalHandler]
    public virtual void on_set_weedlevel(datum source = null, object new_value = null)
    {
        weeds_level.set_output(new_value);
    }

    // tg: code/modules/hydroponics/hydroponics.dm:1383
    [SignalHandler]
    public virtual void on_set_pestlevel(datum source = null, object new_value = null)
    {
        pests_level.set_output(new_value);
    }

    // tg: code/modules/hydroponics/hydroponics.dm:1387
    [SignalHandler]
    public virtual void on_set_waterlevel(datum source = null, object new_value = null)
    {
        water_level.set_output(new_value);
    }

    // tg: code/modules/hydroponics/hydroponics.dm:1391
    [SignalHandler]
    public virtual void on_set_plant_health(datum source = null, object new_value = null)
    {
        plant_health.set_output(new_value);
    }

    // tg: code/modules/hydroponics/hydroponics.dm:1395
    [SignalHandler]
    public virtual void on_set_toxic_level(datum source = null, object new_value = null)
    {
        toxic_level.set_output(new_value);
    }

    // tg: code/modules/hydroponics/hydroponics.dm:1399
    [SignalHandler]
    public virtual void on_set_plant_status(datum source = null, object new_value = null)
    {
        plant_status.set_output(new_value);
    }

    // tg: code/modules/hydroponics/hydroponics.dm:1403
    [SignalHandler]
    public virtual void on_harvest(datum source = null, object product_amount = null)
    {
        last_harvest.set_output(product_amount);
        plant_harvested.set_output(COMPONENT_SIGNAL);
    }

    // tg: code/modules/hydroponics/hydroponics.dm:1408
    [SignalHandler]
    public virtual void on_plant_death(datum source = null)
    {
        plant_died.set_output(COMPONENT_SIGNAL);
    }

    // tg: code/modules/hydroponics/hydroponics.dm:1412
    [SignalHandler]
    public virtual void update_reagents_level(datum source = null)
    {
        reagents_level.set_output(((datum_reagents)attached_tray.reagents).total_volume);
    }

    // tg: code/modules/hydroponics/hydroponics.dm:1416
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (Dm.truthy(attached_tray.anchored) && Dm.truthy(attached_tray.powered__proc()))
            attached_tray.set_self_sustaining(Dm.not(Dm.not(selfsustaining_setting.value)));
        return null;
    }
}
