// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/icemoon/cave_entrance.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the ice moon's cave entrances and demonic portals, and the collapsing portal's loot). The var blocks are
// generated.

public partial class datum_controller_global_vars
{
    // tg: code/game/objects/structures/icemoon/cave_entrance.dm:1  GLOBAL_LIST_INIT(ore_probability, ...)
    public DmList ore_probability;

    public virtual void InitGlobalore_probability()
    {
        ore_probability = Dm.list(
            Dm.kv(typeof(obj_item_stack_ore_uranium), 50),
            Dm.kv(typeof(obj_item_stack_ore_iron), 100),
            Dm.kv(typeof(obj_item_stack_ore_plasma), 75),
            Dm.kv(typeof(obj_item_stack_ore_silver), 50),
            Dm.kv(typeof(obj_item_stack_ore_gold), 50),
            Dm.kv(typeof(obj_item_stack_ore_diamond), 25),
            Dm.kv(typeof(obj_item_stack_ore_bananium), 5),
            Dm.kv(typeof(obj_item_stack_ore_titanium), 75));
        Dm.append(ref gvars_datum_init_order, "ore_probability");
    }
}

// tg: code/game/objects/structures/icemoon/cave_entrance.dm:12
public partial class obj_structure_spawner_ice_moon : obj_structure_spawner
{
    // tg: code/game/objects/structures/icemoon/cave_entrance.dm:27
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        clear_rock();
        return dot;
    }

    /**
     * Clears rocks around the spawner when it is created. Ignore any rocks that explicitly do not want to be cleared.
     *
     */
    // tg: code/game/objects/structures/icemoon/cave_entrance.dm:35
    public virtual void clear_rock()
    {
        foreach (turf potential in Dm.each<turf>(RANGE_TURFS(2, this)))
        {
            if (abs(x - potential.x) + abs(y - potential.y) > 3)
                continue;
            if (ismineralturf(potential) && !Dm.truthy(Dm.bitand(potential.turf_flags, NO_CLEARING)))
            {
                turf_closed_mineral clearable = potential as turf_closed_mineral;
                Waiting.NoSleep(clearable.ScrapeAway(null, CHANGETURF_IGNORE_AIR));
            }
        }
    }

    // tg: code/game/objects/structures/icemoon/cave_entrance.dm:43
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        destroy_effect();
        drop_loot();
        await DmTask.CompletedTask;
    }

    /**
     * Effects and messages created when the spawner is destroyed
     *
     */
    // tg: code/game/objects/structures/icemoon/cave_entrance.dm:51
    public virtual void destroy_effect()
    {
        playsound(loc, Dm.resource("sound/effects/explosion/explosionfar.ogg"), 200, TRUE);
        visible_message(span_bolddanger(I18n.T("[] collapses, sealing everything inside!</span>\n<span class='warning'>Ores fall out of the cave as it is destroyed!", this)));
    }

    /**
     * Drops items after the spawner is destroyed
     *
     */
    // tg: code/game/objects/structures/icemoon/cave_entrance.dm:59
    public virtual void drop_loot()
    {
        foreach (object type in Dm.each(GLOB.ore_probability))
        {
            object chance = GLOB.ore_probability[type];
            if (!prob(Dm.num(chance)))
                continue;
            Dm.@new((Type)type, loc, rand(5, 10));
        }
    }
}

// tg: code/game/objects/structures/icemoon/cave_entrance.dm:66
public partial class obj_structure_spawner_ice_moon_polarbear : obj_structure_spawner_ice_moon
{
    // tg: code/game/objects/structures/icemoon/cave_entrance.dm:72
    public override void clear_rock()
    {
        foreach (turf potential in Dm.each<turf>(RANGE_TURFS(1, this)))
        {
            if (ismineralturf(potential) && !Dm.truthy(Dm.bitand(potential.turf_flags, NO_CLEARING)))
            {
                turf_closed_mineral clearable = potential as turf_closed_mineral;
                Waiting.NoSleep(clearable.ScrapeAway(null, CHANGETURF_IGNORE_AIR));
            }
        }
    }
}

// tg: code/game/objects/structures/icemoon/cave_entrance.dm:78
public partial class obj_structure_spawner_ice_moon_demonic_portal : obj_structure_spawner_ice_moon
{
    // tg: code/game/objects/structures/icemoon/cave_entrance.dm:88
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_gps), "Netheric Signal"));
        return dot;
    }

    // tg: code/game/objects/structures/icemoon/cave_entrance.dm:92
    public override void clear_rock()
    {
        foreach (turf potential in Dm.each<turf>(RANGE_TURFS(3, this)))
        {
            if (abs(x - potential.x) + abs(y - potential.y) > 5)
                continue;
            if (ismineralturf(potential) && !Dm.truthy(Dm.bitand(potential.turf_flags, NO_CLEARING)))
            {
                turf_closed_mineral clearable = potential as turf_closed_mineral;
                Waiting.NoSleep(clearable.ScrapeAway(null, CHANGETURF_IGNORE_AIR));
            }
        }
    }

    // tg: code/game/objects/structures/icemoon/cave_entrance.dm:100
    public override void destroy_effect()
    {
        Dm.@new<obj_effect_collapsing_demonic_portal>(loc);
    }

    // tg: code/game/objects/structures/icemoon/cave_entrance.dm:103
    public override void drop_loot()
    {
        return;
    }
}

// tg: code/game/objects/structures/icemoon/cave_entrance.dm:114
public partial class obj_effect_collapsing_demonic_portal : obj_effect
{
    // tg: code/game/objects/structures/icemoon/cave_entrance.dm:123
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        playsound(loc, Dm.resource("sound/effects/tendril_destroyed.ogg"), 200, FALSE, 50, TRUE, TRUE);
        visible_message(span_bolddanger(I18n.T("[] begins to collapse, cutting it off from this world!", this)));
        animate(this, Dm.named("transform", matrix().Scale(0, 1)), Dm.named("alpha", 50), Dm.named("time", 5 * SECONDS));
        _addtimer(Dm.@new<datum_callback>(this, nameof(collapse)), 5 * SECONDS, file: "code/game/objects/structures/icemoon/cave_entrance.dm", line: 128);
        return dot;
    }

    /**
     * Handles portal deletion
     *
     */
    // tg: code/game/objects/structures/icemoon/cave_entrance.dm:134
    public virtual void collapse()
    {
        drop_loot();
        qdel(this);
    }

    /**
     * Drops loot from the portal
     *
     */
    // tg: code/game/objects/structures/icemoon/cave_entrance.dm:142
    public virtual void drop_loot()
    {
        visible_message(span_warning(I18n.T("Something slips out of []!", this)));
        float loot = rand(1, 100);
        if (loot >= 1 && loot <= 80)
            Dm.@new<obj_structure_closet_crate_necropolis_tendril_demonic>(loc);
        else if (loot >= 81 && loot <= 90)
            Dm.@new<mob_living_basic_boss_blood_drunk_miner_doom>(loc);
        else if (loot >= 91)
            Dm.@new<obj_structure_elite_tumor>(loc);
    }
}
