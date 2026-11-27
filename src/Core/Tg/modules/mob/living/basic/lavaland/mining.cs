// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/mining.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

///prototype for mining mobs
// tg: code/modules/mob/living/basic/lavaland/mining.dm:3
public partial class mob_living_basic_mining : mob_living_basic
{
    // tg: code/modules/mob/living/basic/lavaland/mining.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_traits(Dm.list(TRAIT_LAVA_IMMUNE, TRAIT_ASHSTORM_IMMUNE, TRAIT_SNOWSTORM_IMMUNE), INNATE_TRAIT);
        if (Dm.truthy(kill_count))
            Waiting.NoSleep(AddElement(typeof(datum_element_mob_killed_tally), "mobs_killed_mining"));
        if (TgState.Current.mob_living_basic_mining__Initialize__vulnerable_projectiles == null)
            TgState.Current.mob_living_basic_mining__Initialize__vulnerable_projectiles = string_list(Dm.list(BRUTE)) as DmList; // MINING_MOB_PROJECTILE_VULNERABILITY (__DEFINES/mobs.dm:1073)
        add_ranged_armour(TgState.Current.mob_living_basic_mining__Initialize__vulnerable_projectiles);
        if (crusher_loot != null)
        {
            Waiting.NoSleep(AddElement(
                typeof(datum_element_crusher_loot),
                Dm.named("trophy_type", crusher_loot),
                Dm.named("drop_mod", crusher_drop_chance),
                Dm.named("drop_immediately", Dm.bitand(basic_mob_flags, DEL_ON_DEATH))));
        }
        RegisterSignal(this, COMSIG_ATOM_WAS_ATTACKED, nameof(on_attacked));
        // We add this to ensure that mobs will actually receive the above signal, as some will lack AI
        // handling for retaliation and attack special cases
        Waiting.NoSleep(AddElement(typeof(datum_element_relay_attackers)));
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate))); //Used by priority behaviors
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/mining.dm:50
    public virtual void add_ranged_armour(DmList vulnerable_projectiles = null)
    {
        Waiting.NoSleep(AddElement(
            typeof(datum_element_ranged_armour),
            Dm.named("minimum_projectile_force", 30),
            Dm.named("below_projectile_multiplier", 0.3f),
            Dm.named("vulnerable_projectile_types", vulnerable_projectiles),
            Dm.named("minimum_thrown_force", 20),
            Dm.named("throw_blocked_message", throw_blocked_message)));
    }

    // tg: code/modules/mob/living/basic/lavaland/mining.dm:60
    [SignalHandler]
    public virtual void on_attacked(datum source = null, atom attacker = null, object attack_flags = null)
    {
        if (!Dm.truthy(has_faction(FACTION_ASHWALKER)) || !Dm.truthy((attacker as mob_living)?.has_faction(FACTION_ASHWALKER)))
            return;
        remove_faction(FACTION_ASHWALKER);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/lavaland/mining.dm:32 (var/static/list/vulnerable_projectiles)
    internal DmList mob_living_basic_mining__Initialize__vulnerable_projectiles;
}
