// Translated from tgstation (snapshot 1d99bd7f): code/modules/holodeck/holo_effect.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the holodeck's effects, which spawn its holographic deck of cards, sparks, a random manual and its holographic
// animals. The var blocks are generated (D111).

/*
	The holodeck activates these shortly after the program loads,
	and deactivates them immediately before changing or disabling the holodeck.

	These remove snowflake code for special holodeck functions.
*/
// tg: code/modules/holodeck/holo_effect.dm:7
public partial class obj_effect_holodeck_effect : obj_effect
{
    // tg: code/modules/holodeck/holo_effect.dm:12
    public virtual object activate(obj_machinery_computer_holodeck HC = null)
    {
        return null;
    }

    // tg: code/modules/holodeck/holo_effect.dm:15
    public virtual async DmTask deactivate(obj_machinery_computer_holodeck HC = null)
    {
        qdel(this);
    }

    // Called by the holodeck computer as long as the program is running
    // tg: code/modules/holodeck/holo_effect.dm:20
    public virtual void tick(obj_machinery_computer_holodeck HC = null)
    {
    }

    // tg: code/modules/holodeck/holo_effect.dm:23
    public virtual void safety(object active = null)
    {
    }
}

// Generates a holodeck-tracked card deck
// tg: code/modules/holodeck/holo_effect.dm:28
public partial class obj_effect_holodeck_effect_cards : obj_effect_holodeck_effect
{
    // tg: code/modules/holodeck/holo_effect.dm:32
    public override object activate(obj_machinery_computer_holodeck HC = null)
    {
        obj_machinery_computer_holodeck holodeck = HC;
        var deck = Dm.@new<obj_item_toy_cards_deck_syndicate_holographic>(loc, holodeck);
        deck.flags_1 = Dm.num(Dm.bitor(deck.flags_1, HOLOGRAM_1));
        return deck;
    }
}

// tg: code/modules/holodeck/holo_effect.dm:37
public partial class obj_effect_holodeck_effect_sparks : obj_effect_holodeck_effect
{
    // tg: code/modules/holodeck/holo_effect.dm:37
    public override object activate(obj_machinery_computer_holodeck HC = null)
    {
        turf T = get_turf(this);
        if (T != null)
        {
            do_sparks(3, TRUE, T);
            T.temperature = 5000; //Why? not quite sure to be honest with you
            T.hotspot_expose(50000, 50000, 1);
        }
        return null;
    }
}

// tg: code/modules/holodeck/holo_effect.dm:44
public partial class obj_effect_holodeck_effect_random_book : obj_effect_holodeck_effect
{
    // tg: code/modules/holodeck/holo_effect.dm:47
    public override object activate(obj_machinery_computer_holodeck HC = null)
    {
        object newtype = get_random_manual();
        var to_spawn = Dm.@new((Type)newtype, loc) as obj_item_book_manual;
        to_spawn.flags_1 = Dm.num(Dm.bitor(to_spawn.flags_1, HOLOGRAM_1));
        to_spawn.obj_flags = Dm.bitor(to_spawn.obj_flags, NO_DEBRIS_AFTER_DECONSTRUCTION);
        return to_spawn;
    }
}

// tg: code/modules/holodeck/holo_effect.dm:53
public partial class obj_effect_holodeck_effect_mobspawner : obj_effect_holodeck_effect
{
    // tg: code/modules/holodeck/holo_effect.dm:57
    public override object activate(obj_machinery_computer_holodeck HC = null)
    {
        DmList dot = Dm.list();

        if (islist(mobtype))
            mobtype = pick((DmList)mobtype);
        our_mob = Dm.@new((Type)mobtype, loc) as mob;
        our_mob.flags_1 = Dm.num(Dm.bitor(our_mob.flags_1, HOLOGRAM_1));
        Dm.append(ref dot, our_mob);

        foreach (atom holo_atom in Dm.each<atom>(our_mob.contents))
        {
            holo_atom.flags_1 = Dm.num(Dm.bitor(holo_atom.flags_1, HOLOGRAM_1));
            Dm.append(ref dot, holo_atom);
        }

        if (iscarbon(our_mob))
        {
            var holo_carbon_mob = (mob_living_carbon)our_mob;
            foreach (obj_item_organ holo_organ in Dm.each<obj_item_organ>(holo_carbon_mob.organs))
            {
                holo_organ.flags_1 = Dm.num(Dm.bitor(holo_organ.flags_1, HOLOGRAM_1));
                Dm.append(ref dot, holo_organ);
            }
        }

        // these vars are not really standardized but all would theoretically create stuff on death
        our_mob.add_traits(Dm.list(TRAIT_PERMANENTLY_MORTAL, TRAIT_NO_BLOOD_OVERLAY, TRAIT_NOBLOOD, TRAIT_NOHUNGER, TRAIT_SPAWNED_MOB), INNATE_TRAIT);
        RegisterSignal(our_mob, COMSIG_QDELETING, nameof(handle_mob_delete));

        return dot;
    }

    // tg: code/modules/holodeck/holo_effect.dm:83
    public override async DmTask deactivate(obj_machinery_computer_holodeck HC = null)
    {
        if (our_mob != null)
        {
            foreach (atom holo_atom in Dm.each<atom>(our_mob.contents))
            {
                if (Dm.truthy(Dm.bitand(holo_atom.flags_1, HOLOGRAM_1)))
                    await HC.derez(holo_atom as atom_movable);
            }

            if (iscarbon(our_mob))
            {
                var holo_carbon_mob = (mob_living_carbon)our_mob;
                foreach (obj_item_organ holo_organ in Dm.each<obj_item_organ>(holo_carbon_mob.organs))
                {
                    if (Dm.truthy(Dm.bitand(holo_organ.flags_1, HOLOGRAM_1)))
                        await HC.derez(holo_organ);
                }
            }

            await HC.derez(our_mob);
        }
        qdel(this);
    }

    // tg: code/modules/holodeck/holo_effect.dm:99
    public virtual void handle_mob_delete(datum source = null)
    {
        our_mob = null;
    }
}

// tg: code/modules/holodeck/holo_effect.dm:103
public partial class obj_effect_holodeck_effect_mobspawner_pet : obj_effect_holodeck_effect_mobspawner
{
    // tg: code/modules/holodeck/holo_effect.dm:105
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        mobtype = Dm.list(
            typeof(mob_living_basic_butterfly),
            typeof(mob_living_basic_chick_permanent),
            typeof(mob_living_basic_pet_fox_docile),
            typeof(mob_living_basic_rabbit));
        Dm.append(ref mobtype, pick(
            typeof(mob_living_basic_pet_dog_corgi),
            typeof(mob_living_basic_pet_dog_corgi_puppy),
            typeof(mob_living_basic_pet_dog_pug)));
        Dm.append(ref mobtype, pick(
            typeof(mob_living_basic_pet_cat),
            typeof(mob_living_basic_pet_cat_kitten)));
        return dot;
    }
}

// tg: code/modules/holodeck/holo_effect.dm:127
public partial class obj_effect_holodeck_effect_mobspawner_penguin : obj_effect_holodeck_effect_mobspawner
{
    // tg: code/modules/holodeck/holo_effect.dm:130
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (prob(1))
            mobtype = typeof(mob_living_basic_pet_penguin_emperor_shamebrero_neuter);
        return base.Initialize(mapload, __extra);
    }
}
