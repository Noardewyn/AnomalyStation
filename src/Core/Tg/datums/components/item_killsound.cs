// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/item_killsound.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DE1 (D333): the whole file (the carpenter hammer's death scream: a hit gives the mob a second of a death sound, or of a
// deathgasp that sounds as the scream). The var block is generated (D111).

// tg: code/datums/components/item_killsound.dm:1
public partial class datum_component_item_killsound : datum_component
{
    // tg: code/datums/components/item_killsound.dm:14
    [DmParams("allowed_mobs", "blacklisted_mobs", "killsound", "killsound_volume", "replace_default_death_sound")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object allowed_mobs = Dm.arg(__extra, 0);
        object blacklisted_mobs = Dm.arg(__extra, 1);
        object killsound = Dm.arg(__extra, 2);
        object killsound_volume = Dm.arg(__extra, 3) ?? 100;
        object replace_default_death_sound = Dm.arg(__extra, 4) ?? FALSE;
        this.allowed_mobs = allowed_mobs as DmList;
        this.blacklisted_mobs = blacklisted_mobs as DmList;
        this.killsound = killsound;
        this.killsound_volume = Dm.num(killsound_volume);
        this.replace_default_death_sound = replace_default_death_sound;
        return null;
    }

    // tg: code/datums/components/item_killsound.dm:27
    public override async DmTask RegisterWithParent()
    {
        obj_item item_parent = parent as obj_item;
        RegisterSignal(item_parent, COMSIG_ITEM_ATTACK, nameof(on_attack));
    }

    // tg: code/datums/components/item_killsound.dm:31
    [SignalHandler]
    public virtual void on_attack(object host = null, object target_mob = null, object user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(allowed_mobs) || Dm.truthy(is_type_in_list(target_mob as datum, allowed_mobs)))
        {
            if (Dm.truthy(is_type_in_list(target_mob as datum, blacklisted_mobs)))
                return;
            mob_living mob = target_mob as mob_living;
            if (Dm.truthy(replace_default_death_sound))
                Waiting.NoSleep(mob.apply_status_effect(typeof(datum_status_effect_replace_sound), 1 * SECONDS, Dm.list(Dm.kv(Dm.initial(typeof(datum_emote_living_deathgasp), (datum_emote p) => p.key), killsound))));
            else
                Waiting.NoSleep(mob.apply_status_effect(typeof(datum_status_effect_death_sound), 1 * SECONDS, killsound, killsound_volume));
        }
    }
}
