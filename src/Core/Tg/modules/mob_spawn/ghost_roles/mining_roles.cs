// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob_spawn/ghost_roles/mining_roles.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: Lavaland's ghost roles, the hermit, the beach bums and the lifeguard, the bartender, the seed vault's lifebringer,
// the ash walkers' eggs and the syndicate base's scientist and comms agent with their outfits and GPS mask. The var blocks are
// generated (but the ash walker egg's, whose team the generator types as a path). The lifebringer's pod people species is not
// ported (not-translated.md).

//lava hermit

//Malfunctioning cryostasis sleepers: Spawns in makeshift shelters in lavaland. Ghosts become hermits with knowledge of how they got to where they are now.
// tg: code/modules/mob_spawn/ghost_roles/mining_roles.dm:6
public partial class obj_effect_mob_spawn_ghost_role_human_hermit : obj_effect_mob_spawn_ghost_role_human
{
    // tg: code/modules/mob_spawn/ghost_roles/mining_roles.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // outfit = new outfit //who cares equip outfit works with outfit as a path or an instance
        // PORT: the spawner's outfit var holds the outfit's type (mob_spawn.cs); the vars tg sets on the made outfit go through
        // outfit_override, which equip sets on the outfit it makes: the same outfit
        DmList changes = Dm.list();
        float arrpee = Dm.rand(1, 4);
        switch (arrpee)
        {
            case 1:
                flavour_text += Dm.text("you were a []'s assistant on a small trading station several sectors from here. Raiders attacked, and there was only one pod left when you got to the escape bay. You took it and launched it alone, and the crowd of terrified faces crowding at the airlock door as your pod's engines burst to life and sent you to this hell are forever branded into your memory.",
                    pick("arms dealer", "shipwright", "docking manager"));
                changes["uniform"] = typeof(obj_item_clothing_under_misc_assistantformal);
                break;
            case 2:
                flavour_text += "you're an exile from the Tiger Cooperative. Their technological fanaticism drove you to question the power and beliefs of the Exolitics, and they saw you as a heretic and subjected you to hours of horrible torture. You were hours away from execution when a high-ranking friend of yours in the Cooperative managed to secure you a pod, scrambled its destination's coordinates, and launched it. You awoke from stasis when you landed and have been surviving - barely - ever since.";
                changes["uniform"] = typeof(obj_item_clothing_under_rank_prisoner);
                changes["shoes"] = typeof(obj_item_clothing_shoes_sneakers_orange);
                break;
            case 3:
                flavour_text += "you were a doctor on one of Nanotrasen's space stations, but you left behind that damn corporation's tyranny and everything it stood for. From a metaphorical hell to a literal one, you find yourself nonetheless missing the recycled air and warm floors of what you left behind... but you'd still rather be here than there.";
                changes["uniform"] = typeof(obj_item_clothing_under_rank_medical_doctor);
                changes["suit"] = typeof(obj_item_clothing_suit_toggle_labcoat);
                changes["back"] = typeof(obj_item_storage_backpack_medic);
                break;
            case 4:
                flavour_text += "you were always joked about by your friends for \"not playing with a full deck\", as they so kindly put it. It seems that they were right when you, on a tour at one of Nanotrasen's state-of-the-art research facilities, were in one of the escape pods alone and saw the red button. It was big and shiny, and it caught your eye. You pressed it, and after a terrifying and fast ride for days, you landed here. You've had time to wisen up since then, and you think that your old friends wouldn't be laughing now.";
                break;
        }
        if (changes.len > 0)
            outfit_override = changes;
        return dot;
    }

    // tg: code/modules/mob_spawn/ghost_roles/mining_roles.dm:46
    public override object Destroy(float? force = null)
    {
        Dm.@new<obj_structure_fluff_empty_cryostasis_sleeper>(get_turf(this));
        return base.Destroy(force);
    }
}

// tg: code/modules/mob_spawn/ghost_roles/mining_roles.dm:86
public partial class obj_effect_mob_spawn_ghost_role_human_beach_lifeguard : obj_effect_mob_spawn_ghost_role_human_beach
{
    // tg: code/modules/mob_spawn/ghost_roles/mining_roles.dm:92
    [DmParams("lifeguard", "mob_possessor", "apply_prefs")]
    public override void special(mob_living spawned_mob = null, mob mob_possessor = null, object apply_prefs = null)
    {
        base.special(spawned_mob, mob_possessor, apply_prefs);
        var lifeguard = (mob_living_carbon_human)spawned_mob;
        lifeguard.gender = FEMALE;
        Waiting.NoSleep(lifeguard.update_body());
    }
}

// tg: code/modules/mob_spawn/ghost_roles/mining_roles.dm:98
public partial class datum_outfit_beachbum : datum_outfit
{
    // tg: code/modules/mob_spawn/ghost_roles/mining_roles.dm:105
    public override async DmTask post_equip(mob_living_carbon_human bum = null, float? visuals_only = null)
    {
        await base.post_equip(bum, visuals_only);
        if (Dm.truthy(visuals_only))
            return;
        await bum.dna.add_mutation(typeof(datum_mutation_stoner), MUTATION_SOURCE_GHOST_ROLE);
    }
}

// tg: code/modules/mob_spawn/ghost_roles/mining_roles.dm:128
public partial class datum_outfit_spacebartender : datum_outfit
{
    // tg: code/modules/mob_spawn/ghost_roles/mining_roles.dm:140
    public override async DmTask post_equip(mob_living_carbon_human bartender = null, float? visuals_only = null)
    {
        await base.post_equip(bartender, visuals_only);
        var id_card = bartender.wear_id as obj_item_card_id;
        if (bartender.age < AGE_MINOR)
        {
            id_card.registered_age = AGE_MINOR;
            to_chat(bartender, span_notice(I18n.T("You're not technically old enough to access or serve alcohol, but your ID has been discreetly modified to display your age as []. Try to keep that a secret!", AGE_MINOR)));
        }
    }
}

// tg: code/modules/mob_spawn/ghost_roles/mining_roles.dm:146
public partial class obj_effect_mob_spawn_ghost_role_human_seed_vault : obj_effect_mob_spawn_ghost_role_human
{
    // tg: code/modules/mob_spawn/ghost_roles/mining_roles.dm:162
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        mob_name = pick("Tomato", "Potato", "Broccoli", "Carrot", "Ambrosia", "Pumpkin", "Ivy", "Kudzu", "Banana", "Moss", "Flower", "Bloom", "Root", "Bark", "Glowshroom", "Petal", "Leaf",
            "Venus", "Sprout", "Cocoa", "Strawberry", "Citrus", "Oak", "Cactus", "Pepper", "Juniper");
        return dot;
    }

    // tg: code/modules/mob_spawn/ghost_roles/mining_roles.dm:167
    public override object Destroy(float? force = null)
    {
        Dm.@new<obj_structure_fluff_empty_terrarium>(get_turf(this));
        return base.Destroy(force);
    }
}

//Ash walker eggs: Spawns in ash walker dens in lavaland. Ghosts become unbreathing lizards that worship the Necropolis and are advised to retrieve corpses to create more ash walkers.

// tg: code/modules/mob_spawn/ghost_roles/mining_roles.dm:174
public partial class obj_structure_ash_walker_eggshell : obj_structure
{
    // tg: code/modules/mob_spawn/ghost_roles/mining_roles.dm:182
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra) //lifted from xeno eggs
    {
        damage_type ??= BRUTE;
        object sw = damage_type;
        if (Dm.eq(sw, BRUTE))
        {
            if (Dm.truthy(damage_amount))
                playsound(loc, Dm.resource("sound/effects/blob/attackblob.ogg"), 100, TRUE);
            else
                playsound(this, Dm.resource("sound/items/weapons/tap.ogg"), 50, TRUE);
        }
        else if (Dm.eq(sw, BURN))
        {
            if (Dm.truthy(damage_amount))
                playsound(loc, Dm.resource("sound/items/tools/welder.ogg"), 100, TRUE);
        }
        return null;
    }

    // tg: code/modules/mob_spawn/ghost_roles/mining_roles.dm:193
    public override async DmTask<object> attack_ghost(mob_dead_observer user = null) //Pass on ghost clicks to the mob spawner
    {
        if (egg != null)
            await egg.attack_ghost(user);
        return await base.attack_ghost(user);
    }

    // tg: code/modules/mob_spawn/ghost_roles/mining_roles.dm:198
    public override object Destroy(float? force = null)
    {
        if (egg == null)
            return base.Destroy(force);
        var yolk = Dm.@new<mob_living_carbon_human>(get_turf(this), typeof(datum_species_lizard_ashwalker));
        Waiting.NoSleep(yolk.fully_replace_character_name(null, yolk.generate_random_mob_name(TRUE)));
        yolk.underwear = "Nude";
        Waiting.NoSleep(yolk.equipOutfit(typeof(datum_outfit_ashwalker))); //this is an authentic mess we're making
        Waiting.NoSleep(yolk.update_body());
        Waiting.NoSleep(yolk.gib(DROP_ALL_REMAINS));
        QDEL_NULL(ref egg);
        return base.Destroy(force);
    }
}

// tg: code/modules/mob_spawn/ghost_roles/mining_roles.dm:211
public partial class obj_effect_mob_spawn_ghost_role_human_ash_walker : obj_effect_mob_spawn_ghost_role_human
{
    // tg: code/modules/mob_spawn/ghost_roles/mining_roles.dm:225 (the generator types the var as a path: tg assigns /datum/team paths to other
    // vars named team; this one holds the nest's team)
    public datum_team_ashwalkers team;
    // tg: code/modules/mob_spawn/ghost_roles/mining_roles.dm:226
    public obj_structure_ash_walker_eggshell eggshell;

    // tg: code/modules/mob_spawn/ghost_roles/mining_roles.dm:211 (the var block, hand-written with the team's var)
    public obj_effect_mob_spawn_ghost_role_human_ash_walker()
    {
        name = "ash walker egg";
        desc = "A man-sized yellow egg, spawned from some unfathomable creature. A humanoid silhouette lurks within.";
        prompt_name = "necropolis ash walker";
        icon = Dm.resource("icons/mob/simple/lavaland/lavaland_monsters.dmi");
        icon_state = "large_egg";
        mob_species = typeof(datum_species_lizard_ashwalker);
        outfit = typeof(datum_outfit_ashwalker);
        move_resist = MOVE_FORCE_NORMAL;
        density = FALSE;
        you_are_text = "You are an ash walker. Your tribe worships the Necropolis.";
        flavour_text = "The wastes are sacred ground, its monsters a blessed bounty. You have seen lights in the distance... they foreshadow the arrival of outsiders that seek to tear apart the Necropolis and its domain. Fresh sacrifices for your nest.";
        spawner_job_path = typeof(datum_job_ash_walker);
    }

    // tg: code/modules/mob_spawn/ghost_roles/mining_roles.dm:228
    public override object Destroy(float? force = null)
    {
        eggshell = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mob_spawn/ghost_roles/mining_roles.dm:232
    public override object allow_spawn(mob user = null, object silent = null)
    {
        silent ??= FALSE;
        if (team == null)
            return FALSE;
        if (!Dm.@in(user.ckey, team.players_spawned)) //one per person unless you get a bonus spawn
            return TRUE;
        if (!Dm.truthy(silent))
            to_chat(user, span_warning(I18n.T("You have exhausted your usefulness to the Necropolis.")));
        return FALSE;
    }

    // tg: code/modules/mob_spawn/ghost_roles/mining_roles.dm:241
    [DmParams("spawned_human", "mob_possessor", "apply_prefs")]
    public override void special(mob_living spawned_mob = null, mob mob_possessor = null, object apply_prefs = null)
    {
        base.special(spawned_mob, mob_possessor, apply_prefs);
        var spawned_human = (mob_living_carbon_human)spawned_mob;
        Waiting.NoSleep(spawned_human.fully_replace_character_name(null, spawned_human.generate_random_mob_name(TRUE)));
        to_chat(spawned_human, I18n.T("<b>Drag the corpses of men and beasts to your nest. It will absorb them to create more of your kind. Invade the strange structure of the outsiders if you must. Do not cause unnecessary destruction, as littering the wastes with ugly wreckage is certain to not gain you favor. Glory to the Necropolis!</b>"));

        Waiting.NoSleep(spawned_human.mind.add_antag_datum(typeof(datum_antagonist_ashwalker), team));

        spawned_human.remove_language(typeof(datum_language_common));
        Dm.append(ref team.players_spawned, spawned_human.ckey);
        eggshell.egg = null;
        QDEL_NULL(ref eggshell);
    }

    // tg: code/modules/mob_spawn/ghost_roles/mining_roles.dm:253
    [DmParams("mapload", "ashteam")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object ashteam = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        area spawner_area = get_area(this) as area;
        team = ashteam as datum_team_ashwalkers;
        eggshell = Dm.@new<obj_structure_ash_walker_eggshell>(get_turf(loc));
        eggshell.egg = this;
        Waiting.NoSleep(forceMove(eggshell));
        if (spawner_area != null)
        {
            notify_ghosts(
                I18n.T("An ash walker egg is ready to hatch in \\the [].", spawner_area.name),
                source: this,
                header: "Ash Walker Egg",
                click_interact: TRUE,
                ignore_key: POLL_IGNORE_ASHWALKER,
                notify_flags: NOTIFY_CATEGORY_NOFLASH);
        }
        return dot;
    }
}

// tg: code/modules/mob_spawn/ghost_roles/mining_roles.dm:281
public partial class obj_effect_mob_spawn_ghost_role_human_lavaland_syndicate : obj_effect_mob_spawn_ghost_role_human
{
    // tg: code/modules/mob_spawn/ghost_roles/mining_roles.dm:294
    [DmParams("new_spawn", "mob_possessor", "apply_prefs")]
    public override void special(mob_living spawned_mob = null, mob mob_possessor = null, object apply_prefs = null)
    {
        base.special(spawned_mob, mob_possessor, apply_prefs);
        spawned_mob.grant_language(typeof(datum_language_codespeak), source: LANGUAGE_MIND);
    }
}

// tg: code/modules/mob_spawn/ghost_roles/mining_roles.dm:307
public partial class datum_outfit_lavaland_syndicate : datum_outfit
{
    // tg: code/modules/mob_spawn/ghost_roles/mining_roles.dm:323
    [DmParams("syndicate", "visuals_only")]
    public override async DmTask post_equip(mob_living_carbon_human H = null, float? visuals_only = null)
    {
        await DmTask.CompletedTask;
        H.add_faction(ROLE_SYNDICATE);
    }
}

// tg: code/modules/mob_spawn/ghost_roles/mining_roles.dm:339
public partial class obj_item_clothing_mask_chameleon_gps : obj_item_clothing_mask_chameleon
{
    // tg: code/modules/mob_spawn/ghost_roles/mining_roles.dm:339
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_gps), "Encrypted Signal"));
        return dot;
    }
}
