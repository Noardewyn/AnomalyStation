// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/death_drops.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1; handle_corpse with the corpse spawners, gaps E2c).

/**
 * ## death drops element!
 *
 * bespoke element that spawn can spawn one or multiple objects when a mob is killed
 */
// tg: code/datums/elements/death_drops.dm:6
public partial class datum_element_death_drops : datum_element
{
    // tg: code/datums/elements/death_drops.dm:14
    [DmParams("target", "loot", "no_gib_drops")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object loot = Dm.arg(__extra, 0);
        object no_gib_drops = Dm.arg(__extra, 1) ?? FALSE;
        object dot = await base.Attach(target, __extra);
        if (!isliving(target))
            return ELEMENT_INCOMPATIBLE;
        if (!Dm.truthy(loot))
            stack_trace(Dm.text("[] added to [] with NO LOOT.", type, target), "code/datums/elements/death_drops.dm", 19);
        this.loot = loot as DmList ?? Dm.list(loot);
        this.no_gib_drops = no_gib_drops;
        RegisterSignal(target, COMSIG_LIVING_DEATH, nameof(on_death));
        return dot;
    }

    // tg: code/datums/elements/death_drops.dm:24
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, COMSIG_LIVING_DEATH);
        return dot;
    }

    ///signal called by the stat of the target changing
    // tg: code/datums/elements/death_drops.dm:29
    [SignalHandler]
    public virtual void on_death(mob_living target = null, object gibbed = null)
    {
        if (Dm.truthy(no_gib_drops) && Dm.truthy(gibbed))
            return;

        DmList spawn_loot = loot.Copy();

        atom loot_loc = target.drop_location() as atom;
        if (Dm.truthy(target._listen_lookup?[COMSIG_LIVING_DROP_LOOT]) && Dm.truthy(Dm.bitand(target._SendSignal(COMSIG_LIVING_DROP_LOOT, Dm.list(target, spawn_loot, gibbed)), COMPONENT_NO_LOOT_DROP)))
            return;

        DmList all_loot = Dm.list();
        foreach (object thing_to_spawn in Dm.each(spawn_loot))
        {
            for (float i = 1, i__end = Dm.truthy(spawn_loot[thing_to_spawn]) ? Dm.num(spawn_loot[thing_to_spawn]) : 1; i <= i__end; i++)
                Dm.append(ref all_loot, create_loot(thing_to_spawn, loot_loc, target, gibbed, spawn_loot.len * 3));
        }

        list_clear_nulls(all_loot); // in case of gibbed corpses
        if (Dm.truthy(target._listen_lookup?[COMSIG_LIVING_DROPPED_LOOT]))
            target._SendSignal(COMSIG_LIVING_DROPPED_LOOT, Dm.list(target, all_loot, gibbed));
    }

    /// Handles creating the loots
    // tg: code/datums/elements/death_drops.dm:52
    public virtual object create_loot(object typepath = null, atom loot_loc = null, mob_living dead = null, object gibbed = null, float? spread_px = null)
    {
        spread_px ??= 4;
        if (ispath(typepath, typeof(obj_effect_mob_spawn_corpse)))
            return handle_corpse(typepath, loot_loc, dead, gibbed);

        object drop = Dm.@new((Type)typepath, loot_loc);
        if (drop is obj_item dropped_item && Dm.truthy(spread_px))
        {
            float clamped_px = clamp(Dm.num(spread_px), 0, 16);
            dropped_item.pixel_x = rand(-clamped_px, clamped_px);
            dropped_item.pixel_y = rand(-clamped_px, clamped_px);
        }
        return drop;
    }

    /// Handles snowflake case of mob corpses
    // tg: code/datums/elements/death_drops.dm:66
    public virtual object handle_corpse(object typepath = null, atom loot_loc = null, mob_living dead = null, object gibbed = null)
    {
        var spawner = (obj_effect_mob_spawn_corpse)Dm.@new((Type)typepath, loot_loc, TRUE);
        var body = spawner.create() as mob_living;
        // done before the gib check so the bodyparts will be damaged
        Waiting.NoSleep(body.set_brute_loss(dead.get_brute_loss()));
        Waiting.NoSleep(body.set_fire_loss(dead.get_fire_loss()));
        // if gibbed, dispose of the body
        if (Dm.truthy(gibbed))
        {
            Waiting.NoSleep(body.gib(DROP_ALL_REMAINS));
            return null;
        }
        // otherwise continue with the rest of the damage types
        Waiting.NoSleep(body.set_tox_loss(dead.get_tox_loss()));
        Waiting.NoSleep(body.set_oxy_loss(dead.get_oxy_loss()));
        return body;
    }
}
