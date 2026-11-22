using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tests.Tg.Wave4.Mob;

/// <summary>Records the signals it is registered for, one line per call (host-side record, not hashed).</summary>
[DmPath("/datum/test_mob_listener")]
public class datum_test_mob_listener : datum
{
    [NotHashed] public readonly List<string> heard = new();

    [SignalHandler]
    public virtual void on_began(datum source = null) => heard.Add("BEGAN");

    [SignalHandler]
    public virtual void on_ended(datum source = null) => heard.Add("ENDED");

    [SignalHandler]
    public virtual void on_statchange(datum source = null, object new_stat = null, object old_stat = null)
        => heard.Add("STATCHANGE " + Dm.str(new_stat) + " from " + Dm.str(old_stat));

    [SignalHandler]
    public virtual void on_incapacitate_changed(datum source = null, object old_incap = null, object new_incap = null)
        => heard.Add("INCAPACITATE_CHANGED " + Dm.str(old_incap) + " -> " + Dm.str(new_incap));

    [SignalHandler]
    public virtual void on_mob_created(datum source = null, object created = null)
        => heard.Add("GLOB MOB_CREATED " + ((datum)created).tag);
}

/// <summary>An obj with a map tag (tg's /obj/var/id_tag, objs.dm:41).</summary>
[DmPath("/obj/test_mob_tagged")]
public class obj_test_mob_tagged : obj
{
    public obj_test_mob_tagged()
    {
        id_tag = "test_mob_tag";
    }
}

/// <summary>An anchored obj that rises above tables (tg's /obj/var/anchored_tabletop_offset, objs.dm:18).</summary>
[DmPath("/obj/test_mob_tabletop")]
public class obj_test_mob_tabletop : obj
{
    public obj_test_mob_tabletop()
    {
        anchored_tabletop_offset = 5;
        anchored = TRUE;
    }
}

/// <summary>A mob whose incapacitation flags the test sets (/mob/proc/build_incapacitated, mob.dm:476).</summary>
[DmPath("/mob/test_mob_incap")]
public class mob_test_mob_incap : Anomaly.Core.Tg.mob
{
    public float incap_flags = NONE;

    public override object build_incapacitated(params object[] __extra) => incap_flags;
}

/// <summary>An extra_checks target that counts its polls and answers what the test set.</summary>
[DmPath("/datum/test_mob_check")]
public class datum_test_mob_check : datum
{
    public object answer = TRUE;
    public float polls = 0;

    public virtual object check()
    {
        polls++;
        return answer;
    }
}
