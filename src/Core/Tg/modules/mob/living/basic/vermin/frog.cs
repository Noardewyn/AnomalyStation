// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/vermin/frog.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

// tg: code/modules/mob/living/basic/vermin/frog.dm:2
public partial class mob_living_basic_frog : mob_living_basic
{
    // tg: code/modules/mob/living/basic/vermin/frog.dm:49
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        add_traits(Dm.list(TRAIT_NODROWN, TRAIT_SWIMMER, TRAIT_VENTCRAWLER_ALWAYS), INNATE_TRAIT);

        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), TgState.Current.mob_living_basic_frog__loc_connections));
        Waiting.NoSleep(AddElement(typeof(datum_element_venomous), poison_type, poison_per_bite));
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_FROG, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 5f));
        Waiting.NoSleep(AddElement(typeof(datum_element_can_be_held)));
        if (minion_type != null)
            Waiting.NoSleep(AddElement(typeof(datum_element_regal_rat_minion), minion_type, "ribbit", GLOB.regal_rat_minion_commands));
        return dot;
    }

    // tg: code/modules/mob/living/basic/vermin/frog.dm:66
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable entered = null)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(this) || !isliving(entered))
            return;
        mob_living entered_mob = entered as mob_living;
        if (Dm.num(entered_mob.mob_size) > MOB_SIZE_TINY)
            playsound(this, stepped_sound, vol: 50, vary: TRUE);
    }
}

// tg: code/modules/mob/living/basic/vermin/frog.dm:132
public partial class mob_living_basic_frog_suicide : mob_living_basic_frog
{
    // tg: code/modules/mob/living/basic/vermin/frog.dm:143
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_explode_on_attack), null, null, typecacheof(Dm.list(typeof(mob_living_basic_frog), typeof(mob_living_basic_leaper)))));
        _addtimer(Dm.@new<datum_callback>(this, nameof(death)), existence_period, file: "code/modules/mob/living/basic/vermin/frog.dm", line: 146);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/vermin/frog.dm:53
    internal DmList mob_living_basic_frog__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(mob_living_basic_frog.on_entered)));
}
