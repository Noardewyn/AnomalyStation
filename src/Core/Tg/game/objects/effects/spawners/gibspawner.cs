// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/spawners/gibspawner.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M6 slice 5 (gibbing): the whole file (the spawners that throw a gibbed body's gibs around, with their sound). The var
// blocks are generated (D111). The gibtypes lists repeat a key (/obj/effect/decal/cleanable/blood/gibs = 1 twice): a list
// literal keeps one entry per key, as BYOND and OpenDream do, so the human spawner makes six gibs, each streaking along the
// directions list of its own index. The UNIT_TESTS branch (no streak, :41-43) is inactive in production (rules 1.7). The fake
// alien of the xeno and larva spawners is live since gaps X5.

// tg: code/game/objects/effects/spawners/gibspawner.dm:1
public partial class obj_effect_gibspawner : obj_effect
{
    // tg: code/game/objects/effects/spawners/gibspawner.dm:11
    [DmParams("mapload", "source_mob", "diseases", "blood_dna_info")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        mob_living source_mob = Dm.arg<mob_living>(__extra, 0);
        DmList diseases = Dm.arg<DmList>(__extra, 1);
        object blood_dna_info = Dm.arg(__extra, 2);
        object dot = base.Initialize(mapload, __extra);

        if (Dm.truthy(sound_to_play) && isnum(sound_vol))
            playsound(this, sound_to_play, sound_vol, TRUE);

        object dna_to_add; //find the dna to pass to the spawned gibs. do note this can be null if the mob doesn't have blood. add_blood_DNA() has built in null handling.
        if (Dm.truthy(blood_dna_info))
            dna_to_add = blood_dna_info;
        else if (source_mob != null)
            dna_to_add = source_mob.get_blood_dna_list(); //ez pz
        else if (gib_mob_type != null)
        {
            mob_living temp_mob = (mob_living)Dm.@new(gib_mob_type, this); //generate a fake mob so that we pull the right type of DNA for the gibs.
            dna_to_add = temp_mob.get_blood_dna_list();
            qdel(temp_mob);
        }
        else
            dna_to_add = Dm.list(Dm.kv("Non-human DNA", random_human_blood_type())); //else, generate a random bloodtype for it.

        for (int i = 1; i <= Dm.num(length(gibtypes)); i++)
        {
            object gibType = gibtypes[i];
            float amount = Dm.num(gibtypes[gibType]);
            for (int j = 1; j <= amount; j++)
            {
                obj_effect_decal_cleanable_blood_gibs gib = (obj_effect_decal_cleanable_blood_gibs)Dm.@new((Type)gibType, loc, diseases, dna_to_add);
                DmList directions = gibdirections[i] as DmList;
                if (isturf(loc))
                {
                    if (Dm.truthy(length(directions)))
                        Waiting.NoSleep(gib.streak(directions, mapload));
                }
            }
        }

        return INITIALIZE_HINT_QDEL;
    }
}

// tg: code/game/objects/effects/spawners/gibspawner.dm:48
public partial class obj_effect_gibspawner_generic : obj_effect_gibspawner
{
    // tg: code/game/objects/effects/spawners/gibspawner.dm:52
    [DmParams("mapload", "source_mob", "diseases", "blood_dna_info")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (!Dm.truthy(length(gibdirections)))
            gibdirections = Dm.list(Dm.list(WEST, NORTHWEST, SOUTHWEST, NORTH), Dm.list(EAST, NORTHEAST, SOUTHEAST, SOUTH), Dm.list());
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/game/objects/effects/spawners/gibspawner.dm:60
public partial class obj_effect_gibspawner_human : obj_effect_gibspawner
{
    // tg: code/game/objects/effects/spawners/gibspawner.dm:65
    [DmParams("mapload", "source_mob", "diseases", "blood_dna_info")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object source_mob = Dm.arg(__extra, 0);
        object diseases = Dm.arg(__extra, 1);
        object blood_dna_info = Dm.arg(__extra, 2);
        if (!Dm.truthy(length(gibdirections)))
        {
            gibdirections = Dm.list(
                Dm.list(NORTH, NORTHEAST, NORTHWEST),
                Dm.list(SOUTH, SOUTHEAST, SOUTHWEST),
                Dm.list(WEST, NORTHWEST, SOUTHWEST),
                Dm.list(EAST, NORTHEAST, SOUTHEAST),
                GLOB.alldirs,
                GLOB.alldirs,
                Dm.list());
        }

        if (!iscarbon(source_mob) && blood_dna_info == null)
            return base.Initialize(mapload, source_mob, diseases, Dm.list(Dm.kv("Human DNA", random_human_blood_type())));

        return base.Initialize(mapload, __extra);
    }
}

//only the gibs that don't look like actual full bodyparts (except torso).
// tg: code/game/objects/effects/spawners/gibspawner.dm:82
public partial class obj_effect_gibspawner_human_bodypartless : obj_effect_gibspawner_human
{
    // tg: code/game/objects/effects/spawners/gibspawner.dm:85
    [DmParams("mapload", "source_mob", "diseases", "blood_dna_info")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (!Dm.truthy(length(gibdirections)))
            gibdirections = Dm.list(Dm.list(NORTH, NORTHEAST, NORTHWEST), Dm.list(SOUTH, SOUTHEAST, SOUTHWEST), Dm.list(WEST, NORTHWEST, SOUTHWEST), Dm.list(EAST, NORTHEAST, SOUTHEAST), GLOB.alldirs, Dm.list());
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/game/objects/effects/spawners/gibspawner.dm:90
public partial class obj_effect_gibspawner_xeno : obj_effect_gibspawner
{
    // tg: code/game/objects/effects/spawners/gibspawner.dm:94
    [DmParams("mapload", "source_mob", "diseases", "blood_dna_info")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (!Dm.truthy(length(gibdirections)))
            gibdirections = Dm.list(Dm.list(NORTH, NORTHEAST, NORTHWEST), Dm.list(SOUTH, SOUTHEAST, SOUTHWEST), Dm.list(WEST, NORTHWEST, SOUTHWEST), Dm.list(EAST, NORTHEAST, SOUTHEAST), GLOB.alldirs, GLOB.alldirs, Dm.list());
        return base.Initialize(mapload, __extra);
    }
}

//only the gibs that don't look like actual full bodyparts (except torso).
// tg: code/game/objects/effects/spawners/gibspawner.dm:99
public partial class obj_effect_gibspawner_xeno_bodypartless : obj_effect_gibspawner_xeno
{
    // tg: code/game/objects/effects/spawners/gibspawner.dm:102
    [DmParams("mapload", "source_mob", "diseases", "blood_dna_info")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (!Dm.truthy(length(gibdirections)))
            gibdirections = Dm.list(Dm.list(NORTH, NORTHEAST, NORTHWEST), Dm.list(SOUTH, SOUTHEAST, SOUTHWEST), Dm.list(WEST, NORTHWEST, SOUTHWEST), Dm.list(EAST, NORTHEAST, SOUTHEAST), GLOB.alldirs, Dm.list());
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/game/objects/effects/spawners/gibspawner.dm:107
public partial class obj_effect_gibspawner_larva : obj_effect_gibspawner
{
    // tg: code/game/objects/effects/spawners/gibspawner.dm:111
    [DmParams("mapload", "source_mob", "diseases", "blood_dna_info")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (!Dm.truthy(length(gibdirections)))
            gibdirections = Dm.list(Dm.list(NORTH, NORTHEAST, NORTHWEST), Dm.list(SOUTH, SOUTHEAST, SOUTHWEST), Dm.list(), GLOB.alldirs);
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/game/objects/effects/spawners/gibspawner.dm:116
public partial class obj_effect_gibspawner_larva_bodypartless : obj_effect_gibspawner_larva
{
    // tg: code/game/objects/effects/spawners/gibspawner.dm:119
    [DmParams("mapload", "source_mob", "diseases", "blood_dna_info")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (!Dm.truthy(length(gibdirections)))
            gibdirections = Dm.list(Dm.list(NORTH, NORTHEAST, NORTHWEST), Dm.list(SOUTH, SOUTHEAST, SOUTHWEST), Dm.list());
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/game/objects/effects/spawners/gibspawner.dm:124
public partial class obj_effect_gibspawner_robot : obj_effect_gibspawner
{
    // tg: code/game/objects/effects/spawners/gibspawner.dm:128
    [DmParams("mapload", "source_mob", "diseases", "blood_dna_info")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (!Dm.truthy(length(gibdirections)))
            gibdirections = Dm.list(Dm.list(NORTH, NORTHEAST, NORTHWEST), Dm.list(SOUTH, SOUTHEAST, SOUTHWEST), Dm.list(WEST, NORTHWEST, SOUTHWEST), Dm.list(EAST, NORTHEAST, SOUTHEAST), GLOB.alldirs, GLOB.alldirs);
        gibtypes[typeof(obj_effect_decal_cleanable_blood_gibs_robot_debris_limb)] = pick(0f, 1f, 2f);
        object dot = base.Initialize(mapload, __extra);
        do_sparks(2, TRUE, drop_location() as atom);
        update_appearance();
        return dot;
    }
}
