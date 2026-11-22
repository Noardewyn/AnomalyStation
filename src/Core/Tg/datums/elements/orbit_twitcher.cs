// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/orbit_twitcher.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps OG1: a recovered crew body twitches while a ghost orbits it). The var block is generated (D111).

// tg: code/datums/elements/orbit_twitcher.dm:1
public partial class datum_element_orbit_twitcher : datum_element
{
    // tg: code/datums/elements/orbit_twitcher.dm:9
    [DmParams("target", "twitch_chance")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object twitch_chance = Dm.arg(__extra, 0);
        object dot = await base.Attach(target, __extra);
        if (!isliving(target))
            return ELEMENT_INCOMPATIBLE;

        this.twitch_chance = twitch_chance;

        RegisterSignal(target, COMSIG_ATOM_ORBIT_BEGIN, nameof(orbit_begin));
        RegisterSignal(target, COMSIG_ATOM_ORBIT_STOP, nameof(orbit_stop));
        return dot;
    }

    // tg: code/datums/elements/orbit_twitcher.dm:19
    [DmParams("source")]
    public override object Detach(datum target = null, params object[] __extra)
    {
        object dot = base.Detach(target, __extra);

        twitchers.Remove(target);
        UnregisterSignal(target, Dm.list(COMSIG_ATOM_ORBIT_BEGIN, COMSIG_ATOM_ORBIT_STOP));
        return dot;
    }

    // tg: code/datums/elements/orbit_twitcher.dm:25
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        foreach (object living__it in Dm.each(twitchers))
        {
            mob_living living = living__it as mob_living;
            if (SPT_PROB(Dm.num(twitch_chance), seconds_per_tick))
            {
                if (prob(60))
                    Waiting.NoSleep(living.emote("twitch_s", forced: TRUE));
                else
                    Waiting.NoSleep(living.emote("twitch", forced: TRUE));

                // if we're in a bodybag or morgue slab go up a level and shake that as well
                obj container = (get(living, typeof(obj_structure_bodycontainer)) is var or__1 && Dm.truthy(or__1) ? or__1 : get(living, typeof(obj_structure_closet_body_bag))) as obj;
                if (container != null)
                    container.Shake(2, 1, 0.3f * SECONDS, 0.1f * SECONDS);
            }
        }
        return null;
    }

    // tg: code/datums/elements/orbit_twitcher.dm:38
    [SignalHandler]
    public virtual void orbit_begin(atom source = null, atom orbiter = null)
    {
        twitchers.Add(source);
        // It checks if we're already processing so it's fine to always call
        START_PROCESSING(SSdcs, this);
    }

    // tg: code/datums/elements/orbit_twitcher.dm:45
    [SignalHandler]
    public virtual void orbit_stop(atom source = null, atom orbiter = null)
    {
        twitchers.Remove(source);

        if (!Dm.truthy(twitchers.len))
            STOP_PROCESSING(SSdcs, this);
    }

    // tg: code/datums/elements/orbit_twitcher.dm:53
    public override void OnTargetDelete(datum source = null)
    {
        twitchers.Remove(source);
        base.OnTargetDelete(source);
    }
}
