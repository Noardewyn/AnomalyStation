using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M2Medical.P1;

/// <summary>
/// A living mob with the two carbon procs that the living core's scenarios read (M2 P1's own tests; a fixture type of the
/// port's own, <c>/mob/living/test_p1_carbonlike</c>, rules 15). A plain <c>/mob/living</c> has no <c>update_stat</c> of its
/// own (mob.dm:1216 is empty) and no crawl modifier; tg's humans get both from <c>/mob/living/carbon</c>, which is M2 P2's.
/// So the fixture copies them, ADAPTED from carbon.dm:610 (<c>update_stat</c>: dead, hard crit and soft crit at the mob's
/// thresholds) and carbon.dm:415 (<c>set_body_position</c>: the carbon crawling modifier), and moves with
/// <c>MOBILITY_FLAGS_CARBON_DEFAULT</c> (a carbon's mobility, which can rest). The presentation procs it reaches that are M2 P3
/// skeletons in this branch (alerts, fullscreens, the transform, offsets, eyes) and the organ lookup of P2 are recorded
/// instead of drawn: the scenarios pin the state of the living core, not the HUD.
/// </summary>
[DmPath("/mob/living/test_p1_carbonlike")]
public class mob_living_test_p1_carbonlike : mob_living
{
    /// <summary>The presentation calls in order ("throw_alert blind", "overlay_fullscreen brute", ...).</summary>
    public readonly List<string> Presentation = new List<string>();

    public mob_living_test_p1_carbonlike()
    {
        mobility_flags = MOBILITY_FLAGS_CARBON_DEFAULT;
    }

    // ADAPTED from code/modules/mob/living/carbon/carbon.dm:610 (/mob/living/carbon/update_stat); med_hud_set_status (:626)
    // is the medical data HUD, left out as the living core leaves it (M8).
    public override void update_stat()
    {
        if (HAS_TRAIT(this, TRAIT_GODMODE))
            return;
        if (stat != DEAD)
        {
            if (health <= dead_threshold && !HAS_TRAIT(this, TRAIT_NODEATH))
            {
                Waiting.NoSleep(death());
                return;
            }
            if (health <= hardcrit_threshold && !HAS_TRAIT(this, TRAIT_NOHARDCRIT))
                Waiting.NoSleep(set_stat(HARD_CRIT));
            else if (health <= crit_threshold && !HAS_TRAIT(this, TRAIT_NOSOFTCRIT))
                Waiting.NoSleep(set_stat(SOFT_CRIT));
            else
                Waiting.NoSleep(set_stat(STABLE));
        }
        update_damage_hud();
        update_health_hud();
        update_stamina_hud();
    }

    // ADAPTED from code/modules/mob/living/carbon/carbon.dm:415 (/mob/living/carbon/set_body_position).
    public override async DmTask<object> set_body_position(object new_value = null)
    {
        object dot = await base.set_body_position(new_value);
        if (dot == null)
            return null;
        if (Dm.eq(new_value, LYING_DOWN))
            await add_movespeed_modifier(typeof(datum_movespeed_modifier_carbon_crawling));
        else
            remove_movespeed_modifier(typeof(datum_movespeed_modifier_carbon_crawling));
        return dot;
    }

    // ---- recorded instead of drawn (M2 P3's skeletons in this branch; P2's organ lookup) ----

    public override object throw_alert(object category = null, object type = null, object severity = null, atom new_master = null, float? @override = null, object timeout_override = null, float? no_anim = null)
    {
        Presentation.Add("throw_alert " + Dm.str(category));
        // tg's throw_alert makes the alert (new type(), alert.dm:44) and returns it; a status effect links itself to it
        // (_status_effect.dm:64). The fixture makes it in nullspace and keeps no screen.
        return type is Type alert ? Dm.@new(alert) : null;
    }

    public override object clear_alert(object category = null, float? clear_override = null)
    {
        Presentation.Add("clear_alert " + Dm.str(category));
        return null;
    }

    public override object overlay_fullscreen(object category = null, object type = null, object severity = null)
    {
        Presentation.Add("overlay_fullscreen " + Dm.str(category));
        return null;
    }

    public override void clear_fullscreen(object category = null, float? animated = null)
        => Presentation.Add("clear_fullscreen " + Dm.str(category));

    public override void reload_fullscreen() => Presentation.Add("reload_fullscreen");

    public override DmTask<object> update_transform(float? resize = null)
    {
        Presentation.Add("update_transform");
        return DmTask<object>.FromResult(null);
    }

    public override void add_offsets(object source = null, object w_add = null, object x_add = null, object y_add = null, object z_add = null, float? animate = null)
        => Presentation.Add("add_offsets " + Dm.str(source));

    public override void remove_offsets(object source = null, float? animate = null)
        => Presentation.Add("remove_offsets " + Dm.str(source));

    public override DmTask update_eyes(float? refresh = null)
    {
        Presentation.Add("update_eyes");
        return DmTask.CompletedTask;
    }

    public override object get_organ_slot(object slot = null) => null;
}

/// <summary>
/// A recording listener for the living core's scenarios (a fixture type of the port's own, <c>/datum/test_m2p1_listener</c>,
/// rules 15): each <c>COMSIG_LIVING_LIFE</c> of the mobs it watches, with the world tick and the seconds per tick.
/// </summary>
[DmPath("/datum/test_m2p1_listener")]
public class datum_test_m2p1_listener : datum
{
    public readonly List<(long Tick, object SecondsPerTick)> Lives = new List<(long, object)>();

    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;

    public void Watch(mob_living living) => RegisterSignal(living, COMSIG_LIVING_LIFE, nameof(on_life));

    [SignalHandler]
    public virtual void on_life(datum source = null, object seconds_per_tick = null)
        => Lives.Add((World.Current.Tick, seconds_per_tick));
}

/// <summary>A booted toy room for the living core (SSmobs, SSpriority_effects, SSfastprocess and SStimed_actions fire).</summary>
internal sealed class LivingRoom
{
    public TgToyRoom Room { get; }

    public World World => Room.World;

    public LivingRoom(ulong seed = 51, bool strict = true)
    {
        Room = TgToyRoom.Boot(seed: seed);
        DmWorldState.Of(World).NotTranslatedThrows = strict;
    }

    public void In(Action body) => Room.In(body);

    public T In<T>(Func<T> body) => Room.In(body);

    public void Step(int ticks = 1) => Room.Step(ticks);

    public long Tick => World.Tick;

    public mob_living_test_p1_carbonlike Spawn(int x = 3, int y = 3)
        => In(() => Dm.@new<mob_living_test_p1_carbonlike>(locate(x, y, 1)));

    /// <summary>Steps until <paramref name="condition"/> holds (read inside the world), at most <paramref name="maxTicks"/>; returns the ticks stepped.</summary>
    public int StepUntil(Func<bool> condition, int maxTicks)
    {
        for (int i = 1; i <= maxTicks; i++)
        {
            Step();
            if (In(condition))
                return i;
        }
        throw new Xunit.Sdk.XunitException($"the condition did not hold within {maxTicks} ticks\n" + string.Join("\n", Room.RuntimeText));
    }

    public void AssertNoRuntimes() => Assert.True(Room.Runtimes.Count == 0, string.Join("\n", Room.RuntimeText));

    /// <summary>
    /// The runtimes are only <c>Dm.NotTranslated</c> reports of the given skeletons (other packages' procs, for a scenario that
    /// must run with <c>NotTranslatedThrows</c> off until they land).
    /// </summary>
    public void AssertOnlySkeletons(params string[] wheres)
    {
        foreach (Exception e in Room.Runtimes)
        {
            Assert.True(e is DmNotTranslatedException, e.ToString());
            Assert.True(wheres.Any(w => e.Message.Contains(w, StringComparison.Ordinal)), e.ToString());
        }
    }
}
