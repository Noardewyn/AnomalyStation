// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/human_movement.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file is translated: get_movespeed_modifiers (TRAIT_IGNORESLOWDOWN: the dev fly and godmode, 2026-10-15), slip,
// mob_negates_gravity and Move (M4b slice 3: wet floors and slipping; M4c slice 2: the magboots' gravity and the shoes'
// step signal).

// tg: code/modules/mob/living/carbon/human/human_movement.dm:1
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/human/human_movement.dm:1
    public override object get_movespeed_modifiers()
    {
        object dot = null;
        DmList considering = base.get_movespeed_modifiers() as DmList;
        if (HAS_TRAIT(this, TRAIT_IGNORESLOWDOWN))
        {
            dot = Dm.list();
            foreach (object id in Dm.each(considering))
            {
                datum_movespeed_modifier M = considering[id] as datum_movespeed_modifier;
                if (Dm.truthy(Dm.bitand(M.flags, IGNORE_NOSLOW)) || Dm.num(M.multiplicative_slowdown) < 0)
                    ((DmList)dot)[id] = M;
            }
            return dot;
        }
        return considering;
    }

    // tg: code/modules/mob/living/carbon/human/human_movement.dm:12
    public override async DmTask<object> slip(object knockdown_amount = null, atom slipped_on = null, object lube_flags = null, object paralyze = null, object daze = null, float? force_drop = null)
    {
        force_drop ??= FALSE;
        if (HAS_TRAIT(this, TRAIT_NO_SLIP_ALL))
            return FALSE;

        if (HAS_TRAIT(this, TRAIT_NO_SLIP_WATER) && !Dm.truthy(Dm.bitand(lube_flags, GALOSHES_DONT_HELP)))
            return FALSE;

        if (HAS_TRAIT(this, TRAIT_NO_SLIP_ICE) && Dm.truthy(Dm.bitand(lube_flags, SLIDE_ICE)))
            return FALSE;

        return await base.slip(knockdown_amount, slipped_on, lube_flags, paralyze, daze, force_drop);
    }

    // tg: code/modules/mob/living/carbon/human/human_movement.dm:24
    public override object mob_negates_gravity()
    {
        // DEVIATION(DV83): an inert human (D112: Meta's monkeys until C5, whose Initialize never made their DNA) has no species to ask
        if (dna == null && InertSeam.IsInert(this))
            return base.mob_negates_gravity();
        return (dna.species as datum_species).negates_gravity(this) is var or__1 && Dm.truthy(or__1) ? or__1 : base.mob_negates_gravity();
    }

    // tg: code/modules/mob/living/carbon/human/human_movement.dm:27
    [DmParams("NewLoc", "direct")]
    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        object dot = null;
        dot = await base.Move(newloc, direct, glide_size_override, update_dir);
        if (shoes != null && body_position == STANDING_UP && Dm.truthy(has_gravity(loc as turf)))
        {
            if ((Dm.truthy(dot) && !Dm.truthy(moving_diagonally)) || (!Dm.truthy(dot) && moving_diagonally == SECOND_DIAG_STEP))
            {
                if (Dm.truthy(shoes._listen_lookup?[COMSIG_SHOES_STEP_ACTION]))
                    shoes._SendSignal(COMSIG_SHOES_STEP_ACTION, Dm.list(shoes));
            }
        }
        return dot;
    }
}
