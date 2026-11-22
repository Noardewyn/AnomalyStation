// Translated from tgstation (snapshot 1d99bd7f): code/modules/spells/spell_types/aoe_spell/_aoe_spell.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

/// A spell that iterates over atoms near the caster and casts a spell on them.
// tg: code/modules/spells/spell_types/aoe_spell/_aoe_spell.dm:9
public partial class datum_action_cooldown_spell_aoe : datum_action_cooldown_spell
{
    // tg: code/modules/spells/spell_types/aoe_spell/_aoe_spell.dm:17
    public override async DmTask<object> is_valid_target(atom cast_on = null)
    {
        return isturf(cast_on.loc) ? TRUE : FALSE;
    }

    // tg: code/modules/spells/spell_types/aoe_spell/_aoe_spell.dm:22
    public override async DmTask<object> cast(atom cast_on = null)
    {
        object dot = await base.cast(cast_on);
        // Get every atom around us to our aoe cast on
        DmList things_to_cast_on = get_things_to_cast_on(cast_on);
        // If set, shuffle the list of things we're going to cast on to remove any existing order
        if (Dm.truthy(shuffle_targets_list))
            shuffle_inplace(things_to_cast_on);

        if (Dm.truthy(_listen_lookup?[COMSIG_SPELL_AOE_ON_CAST]))
            _SendSignal(COMSIG_SPELL_AOE_ON_CAST, Dm.list(this, things_to_cast_on, cast_on));

        // Now go through and cast our spell where applicable
        int num_targets = 0;
        foreach (object thing_to_target in Dm.each(things_to_cast_on))
        {
            if (max_targets > 0 && num_targets >= max_targets)
                continue;

            await cast_on_thing_in_aoe(thing_to_target as atom, cast_on);
            num_targets++;
        }
        return dot;
    }

    /// Gets a list of atoms around center that are within range and going to be affected by our aoe.
    // tg: code/modules/spells/spell_types/aoe_spell/_aoe_spell.dm:52
    public virtual DmList get_things_to_cast_on(atom center = null)
    {
        DmList things = Dm.list();
        // Default behavior is to get all atoms in range, center and owner not included.
        foreach (atom nearby_thing in Dm.each<atom>(range(aoe_radius, center)))
        {
            if (nearby_thing == owner || nearby_thing == center)
                continue;

            Dm.append(ref things, nearby_thing);
        }

        return things;
    }

    /// Actually cause effects on the thing in our aoe. Override this for your spell! Not cast().
    // tg: code/modules/spells/spell_types/aoe_spell/_aoe_spell.dm:73
    public virtual async DmTask cast_on_thing_in_aoe(atom victim = null, atom caster = null)
    {
        throw Dm.CRASH(Dm.text("[] did not implement cast_on_thing_in_aoe and either has no effects or implemented the spell incorrectly.", type), "code/modules/spells/spell_types/aoe_spell/_aoe_spell.dm", 75);
    }
}
