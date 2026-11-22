// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/energy/beam_rifle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps GU1, D333): the Event Horizon anti-existential beam rifle (fires only scoped) and its beam, which opens a
// temporary reality tear where it hits. The doomsday casing declares no procs. The var blocks are generated (D111).

// tg: code/modules/projectiles/guns/energy/beam_rifle.dm:1
public partial class obj_item_gun_energy_event_horizon : obj_item_gun_energy
{
    // tg: code/modules/projectiles/guns/energy/beam_rifle.dm:33
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_scope), Dm.named("range_modifier", 4)));
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/beam_rifle.dm:37
    public override async DmTask<object> process_fire(atom target = null, mob_living user = null, float? message = null, object @params = null, string zone_override = null, float? bonus_spread = null)
    {
        if (!HAS_TRAIT(user, TRAIT_USER_SCOPED))
        {
            balloon_alert(user, I18n.T("must be scoped!"));
            return null;
        }

        object dot = await base.process_fire(target, user, message, @params, zone_override, bonus_spread);
        message_admins(Dm.text("[] has fired an anti-existential beam at [].", ADMIN_LOOKUPFLW(user), ADMIN_VERBOSEJMP(user)));
        return dot;
    }
}

// tg: code/modules/projectiles/guns/energy/beam_rifle.dm:52
public partial class obj_projectile_beam_event_horizon : obj_projectile_beam
{
    // tg: code/modules/projectiles/guns/energy/beam_rifle.dm:64
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        object dot = await base.on_hit(target, blocked, pierce_hit);

        // Where we droppin' boys?
        turf rift_loc = get_turf(target);

        // Spawn our temporary rift, then activate it.
        obj_reality_tear_temporary tear = Dm.@new<obj_reality_tear_temporary>(rift_loc);
        await tear.start_disaster();
        message_admins(Dm.text("[] has been hit by an anti-existential beam at [], creating a singularity.", ADMIN_LOOKUPFLW(target), ADMIN_VERBOSEJMP(rift_loc)));
        return dot;
    }
}
