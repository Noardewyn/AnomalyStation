// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/mobs/chase_target.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

// tg: code/datums/actions/mobs/chase_target.dm:1
public partial class datum_action_cooldown_mob_cooldown_chase_target : datum_action_cooldown_mob_cooldown
{
    // tg: code/datums/actions/mobs/chase_target.dm:10
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        disable_cooldown_actions();
        await charge(target_atom);
        StartCooldown();
        enable_cooldown_actions();
        return TRUE;
    }

    /// Causes the mob to gain speed and charge at a target
    // tg: code/datums/actions/mobs/chase_target.dm:18
    public virtual async DmTask charge(atom target = null)
    {
        if (target is mob_living living_mob && living_mob.stat == DEAD)
            return;
        owner.visible_message(span_boldwarning(I18n.T("[] charges!", owner)));
        owner.SpinAnimation(speed: 20, loops: 3, parallel: FALSE);
        if (ishostile(owner))
        {
            mob_living_simple_animal_hostile hostile_mob = (mob_living_simple_animal_hostile)owner;
            hostile_mob.retreat_distance = 0;
            hostile_mob.minimum_distance = 0;
            await hostile_mob.set_varspeed(0);
            _addtimer(Dm.@new<datum_callback>(this, nameof(reset_charge)), 6 * SECONDS, file: "code/datums/actions/mobs/chase_target.dm", line: 29);
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(throw_thyself)), 2 * SECONDS, file: "code/datums/actions/mobs/chase_target.dm", line: 30);
    }

    /// This is the proc that actually does the throwing. Charge only adds a timer for this.
    // tg: code/datums/actions/mobs/chase_target.dm:33
    public virtual async DmTask throw_thyself()
    {
        playsound(owner, Dm.resource("sound/items/weapons/sonic_jackhammer.ogg"), 50, TRUE);
        await owner.throw_at(target as atom, 7, 1.1f, owner, FALSE, FALSE, Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.playsound), owner, Dm.resource("sound/effects/meteorimpact.ogg"), 50 * size, TRUE, 2), INFINITY);
    }

    /// Resets the charge buffs.
    // tg: code/datums/actions/mobs/chase_target.dm:38
    public virtual async DmTask reset_charge()
    {
        mob_living_simple_animal_hostile hostile_mob = (mob_living_simple_animal_hostile)owner;
        hostile_mob.retreat_distance = 5;
        hostile_mob.minimum_distance = 5;
        await hostile_mob.set_varspeed(2);
    }
}
