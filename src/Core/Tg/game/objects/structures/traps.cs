// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/traps.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the magic traps whole but the fugitive hunters' bounty trap (gaps ST5, D333). The traps lie nearly invisible (alpha 30);
// a living thing stepping on one sets it off once a minute (not an effect or a ghost, not a mind it spares; one that blocks magic only
// reveals it): it flares with sparks and fades back over its cooldown, and its charges run out into nothing; examined from a step
// away it is revealed the same way. The shock trap electrocutes and paralyzes ten seconds, the flame trap sets a hotspot, the frost
// trap freezes (minus 300 K and tg's freon status), the earth trap throws a rock up for twenty seconds, the unholy trap lets two
// hostile proteons out and goes; the divine ward fades after two minutes. The bounty trap (`/obj/structure/trap/stun/hunter`,
// `/obj/item/bountytrap`: a fugitive hunter's tool, which only fugitives set off) waits with the fugitive hunters (antagonists,
// D333, not-translated.md 213). The var blocks are generated (D111).

// tg: code/game/objects/structures/traps.dm:1
public partial class obj_structure_trap : obj_structure
{
    // tg: code/game/objects/structures/traps.dm:15  var/static/list/ignore_typecache (a type-level static, rules 10.6)
    public static DmList ignore_typecache
    {
        get => TgState.Current.obj_structure_trap__ignore_typecache;
        set => TgState.Current.obj_structure_trap__ignore_typecache = value;
    }

    // tg: code/game/objects/structures/traps.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        flare_message = span_warning(I18n.T("[] flares brightly!", this));
        spark_system = Dm.@new<datum_effect_system_basic_spark_spread>(this, 4, TRUE);

        DmList loc_connections = TgState.Current.obj_structure_trap__Initialize__loc_connections ??= Dm.list(
            Dm.kv(COMSIG_ATOM_ENTERED, nameof(on_entered)));
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), loc_connections));

        if (ignore_typecache == null)
        {
            ignore_typecache = typecacheof(Dm.list(
                typeof(obj_effect),
                typeof(mob_dead))) as DmList;
        }
        return dot;
    }

    // tg: code/game/objects/structures/traps.dm:37
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref spark_system);
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/traps.dm:41
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList ?? Dm.list();
        if (!isliving(user))
            return dot;
        if (user.mind != null && Dm.@in(user.mind, immune_minds))
            return dot;
        if (Dm.num(get_dist(user, this)) <= 1)
        {
            Dm.append(ref dot, span_notice(I18n.T("You reveal []!", this)));
            await flare();
        }
        return dot;
    }

    // tg: code/game/objects/structures/traps.dm:51
    public virtual async DmTask flare()
    {
        // Makes the trap visible, and starts the cooldown until it's
        // able to be triggered again.
        visible_message(flare_message);
        if (Dm.truthy(sparks))
            await spark_system.start();
        alpha = 200;
        last_trigger = world.time;
        charges--;
        if (charges <= 0)
        {
            animate(this, Dm.named("alpha", 0), Dm.named("time", 1 * SECONDS));
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 1 * SECONDS > GC_FILTER_QUEUE ? (object)WEAKREF(this) : this), 1 * SECONDS, file: "code/game/objects/structures/traps.dm", line: 62); // QDEL_IN(src, 1 SECONDS), inline (rules 11.1 (f))
        }
        else
            animate(this, Dm.named("alpha", Dm.initial(this, (atom p) => p.alpha)), Dm.named("time", time_between_triggers));
        await DmTask.CompletedTask;
    }

    // tg: code/game/objects/structures/traps.dm:66
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable victim = null)
    {
        if (last_trigger + time_between_triggers > world.time)
            return;
        // Don't want the traps triggered by sparks, ghosts or projectiles.
        if (Dm.truthy(is_type_in_typecache(victim, ignore_typecache)))
            return;
        if (ismob(victim))
        {
            mob mob_victim = (mob)victim;
            if (Dm.@in(mob_victim.mind, immune_minds))
                return;
            if (Dm.truthy(mob_victim.can_block_magic(antimagic_flags)))
            {
                Waiting.NoSleep(flare());
                return;
            }
        }
        if (charges <= 0)
            return;
        Waiting.NoSleep(flare());
        if (isliving(victim))
            Waiting.NoSleep(trap_effect((mob_living)victim));
    }

    // tg: code/game/objects/structures/traps.dm:86
    public virtual async DmTask trap_effect(mob_living victim = null)
    {
        await DmTask.CompletedTask;
    }
}

// tg: code/game/objects/structures/traps.dm:89
public partial class obj_structure_trap_stun : obj_structure_trap
{
    // tg: code/game/objects/structures/traps.dm:95
    public override async DmTask trap_effect(mob_living victim = null)
    {
        await victim.electrocute_act(30, this, flags: SHOCK_NOGLOVES); // electrocute act does a message.
        await victim.Paralyze(stun_time);
    }
}

// tg: code/game/objects/structures/traps.dm:183
public partial class obj_structure_trap_fire : obj_structure_trap
{
    // tg: code/game/objects/structures/traps.dm:188
    public override async DmTask trap_effect(mob_living victim = null)
    {
        to_chat(victim, span_danger(I18n.T("<B>Spontaneous combustion!</B>")));
        await victim.Paralyze(2 * SECONDS);
        Dm.@new<obj_effect_hotspot>(get_turf(this));
    }
}

// tg: code/game/objects/structures/traps.dm:193
public partial class obj_structure_trap_chill : obj_structure_trap
{
    // tg: code/game/objects/structures/traps.dm:198
    public override async DmTask trap_effect(mob_living victim = null)
    {
        if (HAS_TRAIT(victim, TRAIT_RESISTCOLD))
            return;
        to_chat(victim, span_bolddanger(I18n.T("You're frozen solid!")));
        await victim.Paralyze(2 * SECONDS);
        victim.adjust_bodytemperature(-300);
        await victim.apply_status_effect(typeof(datum_status_effect_freon));
    }
}

// tg: code/game/objects/structures/traps.dm:207
public partial class obj_structure_trap_damage : obj_structure_trap
{
    // tg: code/game/objects/structures/traps.dm:213
    public override async DmTask trap_effect(mob_living victim = null)
    {
        to_chat(victim, span_bolddanger(I18n.T("The ground quakes beneath your feet!")));
        await victim.Paralyze(10 * SECONDS);
        await victim.adjust_brute_loss(35);
        obj_structure_flora_rock_style_random giant_rock = Dm.@new<obj_structure_flora_rock_style_random>(get_turf(this));
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 20 * SECONDS > GC_FILTER_QUEUE ? (object)WEAKREF(giant_rock) : giant_rock), 20 * SECONDS, file: "code/game/objects/structures/traps.dm", line: 218); // QDEL_IN(giant_rock, 20 SECONDS), inline (rules 11.1 (f))
    }
}

// tg: code/game/objects/structures/traps.dm:221
public partial class obj_structure_trap_ward : obj_structure_trap
{
    // tg: code/game/objects/structures/traps.dm:228
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), time_between_triggers > GC_FILTER_QUEUE ? (object)WEAKREF(this) : this), time_between_triggers, file: "code/game/objects/structures/traps.dm", line: 230); // QDEL_IN(src, time_between_triggers), inline (rules 11.1 (f))
        return dot;
    }
}

// tg: code/game/objects/structures/traps.dm:232
public partial class obj_structure_trap_cult : obj_structure_trap
{
    // tg: code/game/objects/structures/traps.dm:237
    public override async DmTask trap_effect(mob_living victim = null)
    {
        to_chat(victim, span_bolddanger(I18n.T("With a crack, the hostile constructs come out of hiding, stunning you!")));
        await victim.electrocute_act(10, this, flags: SHOCK_NOGLOVES); // electrocute act does a message.
        await victim.Paralyze(2 * SECONDS);
        Dm.@new<mob_living_basic_construct_proteon_hostile>(loc);
        Dm.@new<mob_living_basic_construct_proteon_hostile>(loc);
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 3 * SECONDS > GC_FILTER_QUEUE ? (object)WEAKREF(this) : this), 3 * SECONDS, file: "code/game/objects/structures/traps.dm", line: 243); // QDEL_IN(src, 3 SECONDS), inline (rules 11.1 (f))
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/structures/traps.dm:15  /obj/structure/trap's var/static/list/ignore_typecache
    internal DmList obj_structure_trap__ignore_typecache;

    // tg: code/game/objects/structures/traps.dm:26  /obj/structure/trap/Initialize's var/static/list/loc_connections
    internal DmList obj_structure_trap__Initialize__loc_connections;
}
