// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/glowshroom.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the whole file (acid_act came with the acid, silicons SI1c): a
// planted glowshroom glows with its seed's glow gene, spreads over the floor and walls around it and decays. The screentip element is
// the context tips' (D264, a comment). `myseed` is hand-declared (rule V3 types it Type: the subtypes start it as a seed's path).

public partial class datum_controller_global_vars
{
    // tg: code/game/objects/effects/glowshroom.dm:1  GLOBAL_VAR_INIT(glowshrooms, 0)
    public float glowshrooms;

    // tg: code/game/objects/effects/glowshroom.dm:1  (GLOBAL_MANAGED(glowshrooms, 0), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalglowshrooms()
    {
        glowshrooms = 0;
        Dm.append(ref gvars_datum_init_order, "glowshrooms");
    }
}

// tg: code/game/objects/effects/glowshroom.dm:3
public partial class obj_structure_glowshroom : obj_structure
{
    // tg: code/game/objects/effects/glowshroom.dm:263
    public override async DmTask<object> acid_act(object acidpwr = null, object acid_volume = null, params object[] __extra)
    {
        visible_message(span_danger(I18n.T("[] melts away!", this)));
        obj_effect_decal_cleanable_molten_object I = Dm.@new<obj_effect_decal_cleanable_molten_object>(get_turf(this));
        I.desc = I18n.T("Looks like this was \\an [] some time ago.", this);
        qdel(this);
        return TRUE;
    }

    /// Internal seed of the glowshroom, stats are stored here
    // tg: code/game/objects/effects/glowshroom.dm:33  (hand-declared: generated as a path, rule V3; a seed after Initialize)
    public object myseed = typeof(obj_item_seeds_glowshroom);

    // tg: code/game/objects/effects/glowshroom.dm:3  (the generated constructor, kept with the hand-declared var: types-converter.md 3.2)
    public obj_structure_glowshroom()
    {
        name = "glowshroom";
        desc = "Mycena Bregprox, a species of mushroom that glows in the dark.";
        anchored = 1;
        opacity = 0;
        density = 0;
        icon = Dm.resource("icons/obj/lighting.dmi");
        icon_state = "glowshroom";
        layer = 2.08f;
        max_integrity = 60;
    }

    private obj_item_seeds seed => myseed as obj_item_seeds;

    /// Turfs where the glowshroom cannot spread to (var/static/list, rules 10.6)
    // tg: code/game/objects/effects/glowshroom.dm:36
    public static ref DmList blacklisted_glowshroom_turfs => ref TgState.Current.obj_structure_glowshroom__blacklisted_glowshroom_turfs;

    // tg: code/game/objects/effects/glowshroom.dm:69
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T("This is a []\\th generation []!", generation, name));
        return dot;
    }

    // tg: code/game/objects/effects/glowshroom.dm:82
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_item_seeds newseed = Dm.arg(__extra, 0) as obj_item_seeds;
        object dot = base.Initialize(mapload, __extra);
        GLOB.glowshrooms++;
        if (newseed != null)
        {
            myseed = newseed;
            Waiting.NoSleep(newseed.forceMove(this));
        }
        else
            myseed = Dm.@new((Type)myseed, this);

        Waiting.NoSleep(modify_max_integrity(GLOWSHROOM_BASE_INTEGRITY + ((100 - GLOWSHROOM_BASE_INTEGRITY) / 100f * Dm.num(seed.endurance)))); //goes up to 100 with peak endurance

        object our_glow_gene = seed.get_gene(typeof(datum_plant_gene_trait_glow));
        if (our_glow_gene is Type glow_path) // Seeds were ported to initialize so their genes are still typepaths here, luckily their initializer is smart enough to handle us doing this
        {
            Dm.remove(ref seed.genes, glow_path);
            our_glow_gene = Dm.@new(glow_path);
            Dm.append(ref seed.genes, our_glow_gene);
        }
        if (our_glow_gene is datum_plant_gene_trait_glow glow)
            Waiting.NoSleep(set_light(glow.glow_range(seed), glow.glow_power(seed), glow.glow_color));
        Waiting.NoSleep(setDir(calc_dir()));
        base_icon_state = Dm.str(Dm.initial(GetType(), (atom a) => a.icon_state));
        if (!Dm.truthy(floor))
        {
            switch (Dm.num(dir)) //offset to make it be on the wall rather than on the floor
            {
                case NORTH:
                    pixel_y = 32;
                    break;
                case SOUTH:
                    pixel_y = -32;
                    break;
                case EAST:
                    pixel_x = 32;
                    break;
                case WEST:
                    pixel_x = -32;
                    break;
            }
            icon_state = Dm.text("[][]", base_icon_state, rand(1, 3));
        }
        else //if on the floor, glowshroom on-floor sprite
            icon_state = Dm.str(base_icon_state);

        Waiting.NoSleep(AddElement(typeof(datum_element_atmos_sensitive), mapload));
        // COOLDOWN_START(src, spread_cooldown, rand(min_delay_spread, max_delay_spread)) (__DEFINES/cooldowns.dm:110), inline (rules 11.1 (a))
        spread_cooldown = world.time + rand(min_delay_spread, max_delay_spread);

        START_PROCESSING(SSobj, this);

        // var/static/list/hovering_item_typechecks (the plant analyzer's "Scan shroom stats" and "Scan shroom chemicals");
        // AddElement(/datum/element/contextual_screentip_item_typechecks, hovering_item_typechecks): the context tips (D264)
        return dot;
    }

    // tg: code/game/objects/effects/glowshroom.dm:130
    public override object Destroy(float? force = null)
    {
        if (isatom(myseed))
        {
            qdel(myseed);
            myseed = null;
        }
        GLOB.glowshrooms--;
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    /**
     * Causes glowshroom spreading across the floor/walls.
     */

    // tg: code/game/objects/effects/glowshroom.dm:141
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(seconds_per_tick), null); // the slot's set waitfor = FALSE (processing.dm:48)
    }

    private async DmTask<object> process__body(object seconds_per_tick)
    {
        // COOLDOWN_FINISHED(src, spread_cooldown) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        if (spread_cooldown <= world.time)
        {
            // COOLDOWN_START(src, spread_cooldown, rand(min_delay_spread, max_delay_spread))
            spread_cooldown = world.time + rand(min_delay_spread, max_delay_spread);
            Spread();
        }

        await Decay(rand(idle_decay_min, idle_decay_max) * Dm.num(seconds_per_tick));
        return null;
    }

    // tg: code/game/objects/effects/glowshroom.dm:150
    public virtual void Spread()
    {
        turf ownturf = get_turf(this) as turf;
        if (!Dm.truthy(TURF_SHARES(ownturf))) //If we are in a 1x1 room
            return; //Deal with it not now

        DmList possible_locs = Dm.list();
        //Lets collect a list of possible viewable turfs BEFORE we iterate for yield so we don't call view multiple
        //times when there's no real chance of the viewable range changing, really you could do this once on item
        //spawn and most people probably would not notice.
        foreach (turf_open_floor earth in Dm.each<turf_open_floor>(oview(2, this)))
        {
            if (Dm.truthy(is_type_in_typecache(earth, blacklisted_glowshroom_turfs)))
                continue;
            if (!Dm.truthy(TURF_SHARES(earth)))
                continue;
            Dm.append(ref possible_locs, earth);
        }

        //Lets not even try to spawn again if somehow we have ZERO possible locations
        if (!Dm.truthy(possible_locs.len))
            return;

        float chance_generation = 100 * Dm.pow(NUM_E, -((GLOWSHROOM_SPREAD_BASE_DIMINISH_FACTOR + GLOWSHROOM_SPREAD_DIMINISH_FACTOR_PER_GLOWSHROOM * GLOB.glowshrooms) / Dm.num(seed.potency) * (generation - 1))); //https://www.desmos.com/calculator/istvjvcelz

        for (float i = 1; i <= Dm.num(seed.yield); i++)
        {
            if (!prob(chance_generation))
                continue;
            bool spreads_into_adjacent = prob(spread_into_adjacent_chance);
            turf new_loc = null;

            //Try three random locations to spawn before giving up tradeoff
            //between running view(1, earth) on every single collected possibleLoc
            //and failing to spread if we get 3 bad picks, which should only be a problem
            //if there's a lot of glow shroom clustered about
            for (int iterator = 1; iterator <= 3; iterator++)
            {
                turf possibleLoc = pick(possible_locs) as turf;
                // DM parse: !(locate(/obj/structure/glowshroom) in view(1, possibleLoc)) (locate's own "in", section 10.4)
                if (spreads_into_adjacent || Dm.locate<obj_structure_glowshroom>(view(1, possibleLoc)) == null)
                {
                    new_loc = possibleLoc;
                    break;
                }
            }

            //We failed to find any location, skip trying to yield
            if (new_loc == null)
                break;

            float shroom_count = 0;
            float place_count = 1;
            foreach (obj_structure_glowshroom shroom in Dm.each<obj_structure_glowshroom>(new_loc))
                shroom_count++;
            foreach (object wall_dir in Dm.each(GLOB.cardinals))
            {
                turf potential_wall = get_step(new_loc, Dm.num(wall_dir)) as turf;
                if (Dm.truthy(potential_wall.density))
                    place_count++;
            }
            if (shroom_count >= place_count)
                continue;

            var child = (obj_structure_glowshroom)Dm.@new(GetType(), new_loc, seed.Copy());
            child.generation = generation + 1;
        }
    }

    // tg: code/game/objects/effects/glowshroom.dm:207
    public virtual object calc_dir(turf location = null)
    {
        location ??= loc as turf;
        int direction = 16;

        foreach (object wall_dir in Dm.each(GLOB.cardinals))
        {
            turf new_turf = get_step(location, Dm.num(wall_dir)) as turf;
            if (Dm.truthy(new_turf.density))
                direction |= (int)Dm.num(wall_dir);
        }

        foreach (obj_structure_glowshroom shroom in Dm.each<obj_structure_glowshroom>(location))
        {
            if (shroom == this)
                continue;
            if (Dm.truthy(shroom.floor)) //special
                direction &= ~16;
            else
                direction &= ~(int)Dm.num(shroom.dir);
        }

        DmList dir_list = Dm.list();

        for (int i = 1; i <= 16; i <<= 1)
        {
            if ((direction & i) != 0)
                Dm.append(ref dir_list, i);
        }

        if (Dm.truthy(dir_list.len))
        {
            float new_dir = Dm.num(pick(dir_list));
            if (new_dir == 16)
            {
                floor = 1;
                new_dir = 1;
            }
            return new_dir;
        }

        floor = 1;
        return 1;
    }

    /**
     * Causes the glowshroom to decay by decreasing its endurance, destroying it when it gets too low.
     *
     * Arguments:
     * * amount - Amount of endurance to be reduced due to spread decay.
     */
    // tg: code/game/objects/effects/glowshroom.dm:245
    public virtual async DmTask Decay(object amount = null)
    {
        seed.adjust_endurance(-Dm.num(amount) * endurance_decay_rate);
        await take_damage(amount);
        // take_damage could qdel our shroom, so check beforehand
        // if our endurance dropped before the min plant endurance, then delete our shroom anyways
        if (!QDELETED(this) && Dm.num(seed.endurance) <= MIN_PLANT_ENDURANCE)
            qdel(this);
    }

    // tg: code/game/objects/effects/glowshroom.dm:253
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        if (damage_type == BURN && Dm.truthy(damage_amount))
            playsound(loc, Dm.resource("sound/items/tools/welder.ogg"), 100, TRUE);
        return null;
    }

    // tg: code/game/objects/effects/glowshroom.dm:257
    public override object should_atmos_process(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        return Dm.num(exposed_temperature) > 300 ? TRUE : FALSE;
    }

    // tg: code/game/objects/effects/glowshroom.dm:260
    public override async DmTask<object> atmos_expose(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        await take_damage(5, BURN, 0, 0);
        return null;
    }
}

// tg: code/game/objects/effects/glowshroom.dm:53
/// Mapping object, a glowshroom that doesn't spread or die
public partial class obj_structure_glowshroom_single : obj_structure_glowshroom
{
    // tg: code/game/objects/effects/glowshroom.dm:56
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        STOP_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/game/objects/effects/glowshroom.dm:60
    public override void Spread()
    {
        return;
    }
}

// tg: code/game/objects/effects/glowshroom.dm:270
public partial class obj_structure_glowshroom_extreme : obj_structure_glowshroom
{
    // tg: code/game/objects/effects/glowshroom.dm:270
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (generation == 1)
        {
            ((obj_item_seeds)myseed).potency = 100;
            ((obj_item_seeds)myseed).endurance = 100;
            ((obj_item_seeds)myseed).yield = 10;
        }
        return dot;
    }
}

// tg: code/game/objects/effects/glowshroom.dm:277
public partial class obj_structure_glowshroom_medium : obj_structure_glowshroom
{
    // tg: code/game/objects/effects/glowshroom.dm:277
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (generation == 1)
        {
            ((obj_item_seeds)myseed).potency = 50;
            ((obj_item_seeds)myseed).endurance = 50;
            ((obj_item_seeds)myseed).yield = 5;
        }
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/effects/glowshroom.dm:36  var/static/list/blacklisted_glowshroom_turfs = typecacheof(list(/turf/open/lava, /turf/open/water))
    internal DmList obj_structure_glowshroom__blacklisted_glowshroom_turfs = typecacheof(Dm.list(
        typeof(turf_open_lava),
        typeof(turf_open_water))) as DmList;
}
