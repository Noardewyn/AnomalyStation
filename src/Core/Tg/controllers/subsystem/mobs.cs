// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/mobs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/controllers/subsystem/mobs.dm:1 SUBSYSTEM_DEF(mobs)
[DmPath("/datum/controller/subsystem/mobs")]
public partial class datum_controller_subsystem_mobs : datum_controller_subsystem
{
    // tg: code/controllers/subsystem/mobs.dm:2
    public datum_controller_subsystem_mobs()
    {
        name = "Mobs";
        priority = FIRE_PRIORITY_MOBS;
        ss_flags = Dm.bitor(SS_KEEP_TIMING, SS_NO_INIT);
        runlevels = Dm.bitor(RUNLEVEL_GAME, RUNLEVEL_POSTGAME);
        wait = 2 * SECONDS;
    }

    // tg: code/controllers/subsystem/mobs.dm:8  the object-creating default (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        currentrun = Dm.list();
    }

    // tg: code/controllers/subsystem/mobs.dm:8 (list(): dm_init_vars above)
    public DmList currentrun;
    ///only contains living players for some reason
    // tg: code/controllers/subsystem/mobs.dm:10  var/static/list/clients_by_zlevel[][]  (rules 10.6: a TgState field, below; empty brackets declare a
    // list reference, null, BYOND ref /list)
    public static ref DmList clients_by_zlevel => ref TgState.Current.datum_controller_subsystem_mobs__clients_by_zlevel;
    // tg: code/controllers/subsystem/mobs.dm:11  var/static/list/dead_players_by_zlevel[][] = list(list())  (rules 10.6: a TgState field, below)
    public static ref DmList dead_players_by_zlevel => ref TgState.Current.datum_controller_subsystem_mobs__dead_players_by_zlevel;
    // tg: code/controllers/subsystem/mobs.dm:12  var/static/list/cubemonkeys = list()  (rules 10.6: a TgState field, below)
    public static ref DmList cubemonkeys => ref TgState.Current.datum_controller_subsystem_mobs__cubemonkeys;
    // tg: code/controllers/subsystem/mobs.dm:13  var/static/list/cheeserats = list()  (rules 10.6: a TgState field, below)
    public static ref DmList cheeserats => ref TgState.Current.datum_controller_subsystem_mobs__cheeserats;
    // tg: code/controllers/subsystem/mobs.dm:14  var/static/list/relicmobs = list()  (rules 10.6: a TgState field, below)
    public static ref DmList relicmobs => ref TgState.Current.datum_controller_subsystem_mobs__relicmobs;

    // The New() that SUBSYSTEM_DEF(mobs) defines (__DEFINES/MC.dm:93-96), with NEW_SS_GLOBAL(SSmobs) (__DEFINES/MC.dm:29)
    // expanded inline (it reads src).
    // tg: code/controllers/subsystem/mobs.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSmobs != this)
        {
            if (SSmobs is datum_controller_subsystem_mobs)
            {
                Recover();
                qdel(SSmobs);
            }
            SSmobs = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/mobs.dm:16
    public override object stat_entry(object msg = null)
    {
        Dm.NotTranslated("code/controllers/subsystem/mobs.dm:16", "stat_entry: stat panels are not translated (translation-rules.md 13)");
        return null;
    }

    // tg: code/controllers/subsystem/mobs.dm:20
    public virtual void MaxZChanged()
    {
        if (!islist(clients_by_zlevel))
        {
            clients_by_zlevel = Dm.@new<DmList>(world.maxz, 0);
            dead_players_by_zlevel = Dm.@new<DmList>(world.maxz, 0);
        }
        while (clients_by_zlevel.len < world.maxz)
        {
            clients_by_zlevel.len++;
            clients_by_zlevel[clients_by_zlevel.len] = Dm.list();
            dead_players_by_zlevel.len++;
            dead_players_by_zlevel[dead_players_by_zlevel.len] = Dm.list();
        }
    }

    // tg: code/controllers/subsystem/mobs.dm:30
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        resumed ??= FALSE;
        if (!Dm.truthy(resumed))
            this.currentrun = GLOB.mob_living_list.Copy();

        //cache for sanic speed (lists are references anyways)
        DmList currentrun = this.currentrun;
        float seconds_per_tick = wait / (1 * SECONDS);
        while (Dm.truthy(currentrun.len))
        {
            mob_living processing_mob = currentrun[currentrun.len] as mob_living;
            currentrun.len--;
            if (Dm.truthy(processing_mob))
            {
                try { processing_mob.Life(seconds_per_tick); }
                catch (Exception e) { World.Current.ReportRuntime(e); } // isolation boundary (translation-rules.md 9.2)
            }
            else
                GLOB.mob_living_list.Remove(processing_mob);
            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                return;
        }
    }

    // tg: code/controllers/subsystem/mobs.dm:47
    public virtual void register_relic_mob(mob_living spawned = null)
    {
        Dm.NotTranslated("code/controllers/subsystem/mobs.dm:47", "register_relic_mob: not in M2 (relic mobs, the cargo bounty of mob types, a later milestone)");
    }

    // tg: code/controllers/subsystem/mobs.dm:51
    [SignalHandler]
    public virtual void relic_mob_deleted(mob_living source = null)
    {
        Dm.NotTranslated("code/controllers/subsystem/mobs.dm:51", "relic_mob_deleted: not in M2 (relic mobs)");
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/mobs.dm:1  SUBSYSTEM_DEF(mobs): GLOBAL_REAL(SSmobs, /datum/controller/subsystem/mobs)
    internal datum_controller_subsystem_mobs SSmobs;

    // tg: code/controllers/subsystem/mobs.dm:10  /datum/controller/subsystem/mobs/var/static/list/clients_by_zlevel[][] (null: a list reference)
    internal DmList datum_controller_subsystem_mobs__clients_by_zlevel;
    // tg: code/controllers/subsystem/mobs.dm:11  var/static/list/dead_players_by_zlevel[][] = list(list())
    // Needs to support zlevel 1 here, MaxZChanged only happens when z2 is created and new_players can login before that.
    internal DmList datum_controller_subsystem_mobs__dead_players_by_zlevel = Dm.list(Dm.list());
    // tg: code/controllers/subsystem/mobs.dm:12  var/static/list/cubemonkeys = list()
    internal DmList datum_controller_subsystem_mobs__cubemonkeys = Dm.list();
    // tg: code/controllers/subsystem/mobs.dm:13  var/static/list/cheeserats = list()
    internal DmList datum_controller_subsystem_mobs__cheeserats = Dm.list();
    // tg: code/controllers/subsystem/mobs.dm:14  var/static/list/relicmobs = list()
    internal DmList datum_controller_subsystem_mobs__relicmobs = Dm.list();
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_mobs SSmobs => ref TgState.Current.SSmobs;
}
