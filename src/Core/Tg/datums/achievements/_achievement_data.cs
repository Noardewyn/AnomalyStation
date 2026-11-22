// Translated from tgstation (snapshot 1d99bd7f): code/datums/achievements/_achievement_data.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the var block and New translated (M1 P1: /datum/persistent_client/New makes one per player,
// persistent_client.dm:43); the achievements themselves (their database load and save, unlocks, the UI) come with
// SSachievements (M10; their database part never, rules 13).

///Datum that handles
// tg: code/datums/achievements/_achievement_data.dm:2
[DmPath("/datum/achievement_data")]
public partial class datum_achievement_data : datum
{
    ///Ckey of this achievement data's owner
    // tg: code/datums/achievements/_achievement_data.dm:4
    public object owner_ckey;
    ///Up to date list of all achievements and their info.
    // tg: code/datums/achievements/_achievement_data.dm:6 (list(): dm_init_vars below)
    public DmList data;
    ///Original status of achievement.
    // tg: code/datums/achievements/_achievement_data.dm:8 (list(): dm_init_vars below)
    public DmList original_cached_data;
    ///Have we done our set-up yet?
    // tg: code/datums/achievements/_achievement_data.dm:10
    public float initialized = FALSE;

    // The object-creating defaults of the var block (rules 10.2), in DM declaration order.
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        // tg: code/datums/achievements/_achievement_data.dm:6
        data = Dm.list();
        // tg: code/datums/achievements/_achievement_data.dm:8
        original_cached_data = Dm.list();
    }

    // tg: code/datums/achievements/_achievement_data.dm:12
    [DmParams("ckey")]
    public override async DmTask New(params object[] __extra)
    {
        object ckey = Dm.arg(__extra, 0);
        owner_ckey = ckey;
        // tg: :14-15  if(SSachievements.initialized && !initialized) InitializeData()
        // SSachievements is not declared (achievements, M10): without a database its Initialize returns SS_INIT_NO_NEED
        // (controllers/subsystem/achievements.dm:16) and InitializeData's query fails (:39), so this only marks the data
        // initialized and loads nothing.
        Dm.Deferred("code/datums/achievements/_achievement_data.dm:14", "achievement_data/New: InitializeData() once SSachievements is initialized (the achievements' database load; SSachievements, M10+, its database part never)");
    }
}
