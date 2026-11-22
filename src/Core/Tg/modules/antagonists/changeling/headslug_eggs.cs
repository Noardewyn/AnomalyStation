// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/changeling/headslug_eggs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: the headslug's egg, which grows in its dead host (paused while removed) and bursts into a
// monkey that the changeling's mind (or a fresh headslug changeling's) takes over, with the host's DNA and Lesser Form. The var
// block is generated (D111).

/// The actual organ that the changeling headslug implants into a dead host.
// tg: code/modules/antagonists/changeling/headslug_eggs.dm:4
public partial class obj_item_organ_body_egg_changeling_egg : obj_item_organ_body_egg
{
    // tg: code/modules/antagonists/changeling/headslug_eggs.dm:1  #define EGG_INCUBATION_TIME (4 MINUTES) (file-local, #undef at :51)
    private const int EGG_INCUBATION_TIME = (4 * MINUTES);

    // tg: code/modules/antagonists/changeling/headslug_eggs.dm:14
    public override async DmTask<object> on_mob_insert(mob_living_carbon egg_owner = null, float? special = null, object movement_flags = null)
    {
        special ??= FALSE;
        object dot = null;
        dot = await base.on_mob_insert(egg_owner, special, movement_flags);
        hatch_time = world.time + (removal_time == -1 ? EGG_INCUBATION_TIME : (hatch_time - removal_time));
        return dot;
    }

    // tg: code/modules/antagonists/changeling/headslug_eggs.dm:18
    public override async DmTask on_mob_remove(object egg_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(egg_owner, special, movement_flags);
        removal_time = world.time;
    }

    // tg: code/modules/antagonists/changeling/headslug_eggs.dm:22
    public override async DmTask egg_process(object seconds_per_tick = null)
    {
        if (owner != null && hatch_time <= world.time)
            await pop();
    }

    /// Once the egg is fully grown, we gib the host and spawn a monkey (with the changeling's player controlling it). Very descriptive proc name.
    // tg: code/modules/antagonists/changeling/headslug_eggs.dm:27
    public virtual async DmTask pop()
    {
        mob_living_carbon_human spawned_monkey = Dm.@new<mob_living_carbon_human>(owner);
        spawned_monkey.monkeyize(instant: TRUE);

        foreach (obj_item_organ insertable in Dm.each<obj_item_organ>(this))
            await insertable.Insert(spawned_monkey, 1);

        if (origin != null && (origin.current != null ? (origin.current.stat == DEAD) : Dm.truthy(origin.get_ghost())))
        {
            await origin.transfer_to(spawned_monkey);
            spawned_monkey.PossessByPlayer(origin.key);
            datum_antagonist_changeling changeling_datum = origin.has_antag_datum(typeof(datum_antagonist_changeling)) as datum_antagonist_changeling;
            if (changeling_datum == null)
                changeling_datum = await origin.add_antag_datum(typeof(datum_antagonist_changeling_headslug)) as datum_antagonist_changeling;
            if (Dm.truthy(changeling_datum.can_absorb_dna(owner as mob_living_carbon_human)))
                await changeling_datum.add_new_profile(owner as mob_living_carbon_human);

            datum_action_changeling_lesserform transform = Dm.@new<datum_action_changeling_lesserform>();
            changeling_datum.purchased_powers[transform.type] = transform;
            await changeling_datum.regain_powers();
        }

        owner.investigate_log("has been gibbed by a changeling egg burst.", INVESTIGATE_DEATHS);
        await owner.gib(DROP_ALL_REMAINS);
        qdel(this);
    }
}
