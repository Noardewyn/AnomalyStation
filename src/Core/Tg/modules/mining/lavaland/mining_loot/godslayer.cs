// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/lavaland/mining_loot/godslayer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// tg: code/modules/mining/lavaland/mining_loot/godslayer.dm:1
public partial class obj_item_clothing_suit_hooded_cloak_godslayer : obj_item_clothing_suit_hooded_cloak
{
    // tg: code/modules/mining/lavaland/mining_loot/godslayer.dm:21 (var/static/list/damage_heal_order)
    public static DmList damage_heal_order => TgState.Current.obj_item_clothing_suit_hooded_cloak_godslayer__damage_heal_order ??= Dm.list(BRUTE, BURN, OXY);

    // tg: code/modules/mining/lavaland/mining_loot/godslayer.dm:34
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        allowed = GLOB.mining_suit_allowed;
        Waiting.NoSleep(AddComponent(typeof(datum_component_item_equipped_movement_rustle), SFX_PLATE_ARMOR_RUSTLE, 8));
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/godslayer.dm:39
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (loc == user && !(effect_cooldown <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
            Dm.append(ref dot, I18n.T("You feel like the revival effect will be able to occur again in [] seconds.", (effect_cooldown - world.time) / 10)); // COOLDOWN_TIMELEFT (__DEFINES/cooldowns.dm:121), inline
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/godslayer.dm:44
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_OCLOTHING)))
        {
            RegisterSignal(user, COMSIG_MOB_STATCHANGE, nameof(resurrect));
            return;
        }
        UnregisterSignal(user, COMSIG_MOB_STATCHANGE);
    }

    // tg: code/modules/mining/lavaland/mining_loot/godslayer.dm:51
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        await base.dropped(user, silent, __extra);
        UnregisterSignal(user, COMSIG_MOB_STATCHANGE);
        return null;
    }

    // tg: code/modules/mining/lavaland/mining_loot/godslayer.dm:55
    [SignalHandler]
    public virtual void resurrect(mob_living_carbon user = null, object new_stat = null)
    {
        if (!Dm.eq(new_stat, STABLE) && !Dm.eq(new_stat, DEAD) && effect_cooldown <= world.time) // COOLDOWN_FINISHED, inline
        {
            effect_cooldown = world.time + effect_cooldown_time; //This needs to happen first, otherwise there's an infinite loop (COOLDOWN_START, inline)
            Waiting.NoSleep(user.heal_ordered_damage(heal_amount, damage_heal_order));
            user.visible_message(span_notice(I18n.T("[] suddenly revives, as their armor swirls with demonic energy!", user)), span_notice(I18n.T("You suddenly feel invigorated!")));
            playsound(user.loc, Dm.resource("sound/effects/magic/clockwork/ratvar_attack.ogg"), 50);
        }
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mining/lavaland/mining_loot/godslayer.dm:21 (var/static/list/damage_heal_order)
    internal DmList obj_item_clothing_suit_hooded_cloak_godslayer__damage_heal_order;
}
