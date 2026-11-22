// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/boxes/job_boxes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: completed with its module (a later milestone). Holds the [DmPath] of a type that M2 F1's translated defaults
// name (the job outfit's slots, the cloth that repairs clothing, the prisoner suit get_random_jumpsuit leaves out), in the
// earliest .dme file that declares it (rules 2.3). Only the type is declared.

// tg: code/game/objects/items/storage/boxes/job_boxes.dm:4
[DmPath("/obj/item/storage/box/survival")]
public partial class obj_item_storage_box_survival : obj_item_storage_box
{
}

// M4a P3: the survival box (the job outfit's box): create_storage, PopulateContents, apply_overrides.

// tg: code/game/objects/items/storage/boxes/job_boxes.dm:4
public partial class obj_item_storage_box_survival : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/job_boxes.dm:22
    [DmParams("max_slots", "max_specific_storage", "max_total_storage", "canhold", "canthold", "storage_type")]
    public override async DmTask<object> create_storage(object max_slots = null, object max_specific_storage = null, object max_total_storage = null, DmList canhold = null, DmList canthold = null, Type storage_type = null, object rustle_sound = null, object remove_rustle_sound = null)
    {
        object dot = null;
        dot = await base.create_storage(max_slots, max_specific_storage, max_total_storage, canhold, canthold, storage_type, rustle_sound, remove_rustle_sound);
        if (Dm.truthy(crafted) || !HAS_TRAIT(SSstation, STATION_TRAIT_PREMIUM_INTERNALS))
            return dot;
        atom_storage.max_slots += 2;
        atom_storage.max_total_storage += 4;
        name = Dm.text("large []", name);
        icon_state = Dm.text("[]_large", icon_state);
        return dot;
    }

    // tg: code/game/objects/items/storage/boxes/job_boxes.dm:31
    public override async DmTask PopulateContents()
    {
        if (Dm.truthy(crafted))
            return;
        if (!(mask_type == null))
            await Dm.new_async(mask_type, this);
        if (!(internal_type == null))
            await Dm.new_async(internal_type, this);
        if (!(medipen_type == null))
            await Dm.new_async(medipen_type, this);
        if (Dm.truthy(give_premium_goods) && HAS_TRAIT(SSstation, STATION_TRAIT_PREMIUM_INTERNALS))
        {
            Dm.@new<obj_item_flashlight_flare>(this);
            Dm.@new<obj_item_radio_off>(this);
        }
        if (HAS_TRAIT(SSstation, STATION_TRAIT_RADIOACTIVE_NEBULA))
            Dm.@new<obj_item_storage_pill_bottle_potassiodide>(this);
        if (Dm.truthy(give_hook) && Dm.length(SSmapping.levels_by_trait(ZTRAIT_STATION)) > 1 && Dm.truthy(SSmapping.current_map.give_players_hooks))
            Dm.@new<obj_item_climbing_hook_emergency>(this);
    }

    // tg: code/game/objects/items/storage/boxes/job_boxes.dm:57
    public virtual async DmTask apply_overrides()
    {
        if (!ishuman(loc) || QDELING(this))
            return;
        mob_living_carbon_human owner = loc as mob_living_carbon_human;
        if (!Dm.truthy(Dm.length(((datum_species)owner.dna.species).survival_box_overrides)))
            return;
        DmList items_to_clear = Dm.list(
            Dm.locate_type(mask_type, contents),
            Dm.locate_type(internal_type, contents),
            Dm.locate_type(medipen_type, contents)
        );
        QDEL_LIST(items_to_clear);
        DmList survival_box_overrides = ((DmList)((datum_species)owner.dna.species).survival_box_overrides).Copy();
        foreach (object survival_types in Dm.each(survival_box_overrides))
        {
            object item_to_add = survival_box_overrides[survival_types];
            await Dm.new_async((Type)item_to_add, this);
        }
    }
}

// Gaps J2 (D298): the mime's invisible box and the clown's and mime's box of hugs.

// tg: code/game/objects/items/storage/boxes/job_boxes.dm:100  (syndicate N1, D316)
public partial class obj_item_storage_box_survival_syndie : obj_item_storage_box_survival
{
    // tg: code/game/objects/items/storage/boxes/job_boxes.dm:109
    public override async DmTask PopulateContents()
    {
        await base.PopulateContents();
        await Dm.new_async<obj_item_crowbar_red>(this);
        await Dm.new_async<obj_item_screwdriver_red>(this);
        await Dm.new_async<obj_item_weldingtool_mini>(this);
        await Dm.new_async<obj_item_paper_fluff_operative>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/job_boxes.dm:146
public partial class obj_item_storage_box_mime : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/job_boxes.dm:154
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (HAS_MIND_TRAIT(user, TRAIT_MIMING))
            alpha = 255;
        return dot;
    }

    // tg: code/game/objects/items/storage/boxes/job_boxes.dm:159
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        momentum_change ??= TRUE;
        if (iscarbon(old_loc))
            alpha = 0;
        return await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
    }
}

// tg: code/game/objects/items/storage/boxes/job_boxes.dm:223
public partial class obj_item_storage_box_survival_hug : obj_item_storage_box_survival
{
    // tg: code/game/objects/items/storage/boxes/job_boxes.dm:232
    public override async DmTask PopulateContents()
    {
        if (!Dm.truthy(random_funny_internals))
        {
            await base.PopulateContents();
            return;
        }
        internal_type = pick(
            typeof(obj_item_tank_internals_emergency_oxygen_engi_clown_n2o),
            typeof(obj_item_tank_internals_emergency_oxygen_engi_clown_bz),
            typeof(obj_item_tank_internals_emergency_oxygen_engi_clown_helium)) as Type;
        await base.PopulateContents();
    }

    //Duplicated suicide/attack self procs, since the survival boxes are a subtype of box/survival
    // tg: code/game/objects/items/storage/boxes/job_boxes.dm:249
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] clamps the box of hugs on [] jugular! Guess it wasn't such a hugbox after all..", user, user.p_their())));
        return BRUTELOSS;
    }

    // tg: code/game/objects/items/storage/boxes/job_boxes.dm:253
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await base.attack_self(user, modifiers, __extra);
        user.changeNext_move(CLICK_CD_MELEE);
        playsound(loc, SFX_RUSTLE, 50, vary: TRUE, extrarange: -5);
        user.visible_message(span_notice(I18n.T("[] hugs [].", user, this)), span_notice(I18n.T("You hug [].", this)));
        return null;
    }
}

// tg: code/game/objects/items/storage/boxes/job_boxes.dm:311  (gaps J5, D298)
public partial class obj_item_storage_box_skillchips_science : obj_item_storage_box_skillchips
{
    // tg: code/game/objects/items/storage/boxes/job_boxes.dm:311
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_skillchip_job_roboticist>(this); // tg: code/game/objects/items/storage/boxes/job_boxes.dm:312
        Dm.@new<obj_item_skillchip_job_roboticist>(this); // tg: code/game/objects/items/storage/boxes/job_boxes.dm:313
    }
}

// tg: code/game/objects/items/storage/boxes/job_boxes.dm:319  (gaps J5, D298)
public partial class obj_item_storage_box_skillchips_engineering : obj_item_storage_box_skillchips
{
    // tg: code/game/objects/items/storage/boxes/job_boxes.dm:319
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_skillchip_job_engineer>(this); // tg: code/game/objects/items/storage/boxes/job_boxes.dm:320
        Dm.@new<obj_item_skillchip_job_engineer>(this); // tg: code/game/objects/items/storage/boxes/job_boxes.dm:321
    }
}

// tg: code/game/objects/items/storage/boxes/job_boxes.dm:323  (syndicate X1b, D316)
public partial class obj_item_storage_box_contractor_fulton_extraction : obj_item_storage_box_contractor
{
    // tg: code/game/objects/items/storage/boxes/job_boxes.dm:328
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_extraction_pack_syndicate>(this);
        Dm.@new<obj_item_fulton_core_syndicate>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/job_boxes.dm:298  (gaps J5, D298)
public partial class obj_item_storage_box_skillchips : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/job_boxes.dm:302
    public override async DmTask PopulateContents()
    {
        DmList skillchips = subtypesof(typeof(obj_item_skillchip)) as DmList;
        foreach (object skillchip in Dm.each(skillchips))
            Dm.@new(skillchip as Type, this);
    }
}

// Gaps J6 (D298): the miner's survival box.
// tg: code/game/objects/items/storage/boxes/job_boxes.dm:81
public partial class obj_item_storage_box_survival_mining : obj_item_storage_box_survival
{
    // tg: code/game/objects/items/storage/boxes/job_boxes.dm:83
    public override async DmTask PopulateContents()
    {
        await base.PopulateContents();
        Dm.@new<obj_item_crowbar_red>(this);
        Dm.@new<obj_item_healthanalyzer_simple_miner>(this);
    }
}

// Syndicate G4a (D316): the box of hugs (the clown's reverse revolver comes in one) and its kinds.
// tg: code/game/objects/items/storage/boxes/job_boxes.dm:165
public partial class obj_item_storage_box_hug : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/job_boxes.dm:172
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] clamps the box of hugs on [] jugular! Guess it wasn't such a hugbox after all..", user, user.p_their())));
        return BRUTELOSS;
    }

    // tg: code/game/objects/items/storage/boxes/job_boxes.dm:176
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await base.attack_self(user, modifiers, __extra);
        user.changeNext_move(CLICK_CD_MELEE);
        playsound(loc, SFX_RUSTLE, 50, vary: TRUE, extrarange: -5);
        user.visible_message(span_notice(I18n.T("[] hugs [].", user, this)), span_notice(I18n.T("You hug [].", this)));
        return null;
    }
}

// tg: code/game/objects/items/storage/boxes/job_boxes.dm:217
public partial class obj_item_storage_box_hug_medical : obj_item_storage_box_hug
{
    // tg: code/game/objects/items/storage/boxes/job_boxes.dm:217
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_stack_medical_bruise_pack>(this);
        Dm.@new<obj_item_stack_medical_ointment>(this);
        Dm.@new<obj_item_reagent_containers_hypospray_medipen>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/job_boxes.dm:264
public partial class obj_item_storage_box_hug_plushes : obj_item_storage_box_hug
{
    // tg: code/game/objects/items/storage/boxes/job_boxes.dm:264
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++)
        {
            System.Type plush_path = typeof(obj_effect_spawner_random_entertainment_plushie);
            Dm.@new(plush_path, this);
        }
    }
}

// Gaps SR1 (D333): the radio and bonus survival boxes, the CentCom box, the clown box (the honkbot assembly) and the miner's modkit box.

// tg: code/game/objects/items/storage/boxes/job_boxes.dm:53
public partial class obj_item_storage_box_survival_radio : obj_item_storage_box_survival
{
    // tg: code/game/objects/items/storage/boxes/job_boxes.dm:53
    public override async DmTask PopulateContents()
    {
        await base.PopulateContents(); // we want the survival stuff too.
        Dm.@new<obj_item_radio_off>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/job_boxes.dm:273
public partial class obj_item_storage_box_survival_mining_bonus : obj_item_storage_box_survival_mining
{
    // tg: code/game/objects/items/storage/boxes/job_boxes.dm:273
    public override async DmTask PopulateContents()
    {
        await base.PopulateContents();
        Dm.@new<obj_item_gps_mining>(this);
        Dm.@new<obj_item_t_scanner_adv_mining_scanner>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/job_boxes.dm:95
public partial class obj_item_storage_box_survival_engineer_radio : obj_item_storage_box_survival_engineer
{
    // tg: code/game/objects/items/storage/boxes/job_boxes.dm:95
    public override async DmTask PopulateContents()
    {
        await base.PopulateContents(); // we want the regular items too.
        Dm.@new<obj_item_radio_off>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/job_boxes.dm:122
public partial class obj_item_storage_box_survival_centcom : obj_item_storage_box_survival
{
    // tg: code/game/objects/items/storage/boxes/job_boxes.dm:122
    public override async DmTask PopulateContents()
    {
        await base.PopulateContents();
        Dm.@new<obj_item_crowbar>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/job_boxes.dm:130
public partial class obj_item_storage_box_survival_security_radio : obj_item_storage_box_survival_security
{
    // tg: code/game/objects/items/storage/boxes/job_boxes.dm:130
    public override async DmTask PopulateContents()
    {
        await base.PopulateContents(); // we want the regular stuff too
        Dm.@new<obj_item_radio_off>(this);
    }
}

// clown box, we also use this for the honk bot assembly
// tg: code/game/objects/items/storage/boxes/job_boxes.dm:185
public partial class obj_item_storage_box_clown : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/job_boxes.dm:191
    public override async DmTask<object> tool_act(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (!istype(tool, typeof(obj_item_bodypart_arm_left_robot)) && !istype(tool, typeof(obj_item_bodypart_arm_right_robot)))
            return await base.tool_act(user, tool, modifiers);
        if (Dm.truthy(contents.len)) //prevent accidently deleting contents
        {
            balloon_alert(user, I18n.T("items inside!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
            return ITEM_INTERACT_BLOCKING;
        qdel(tool);
        loc.balloon_alert(user, I18n.T("wheels added, honk!"));
        obj_item_bot_assembly_honkbot assembly = Dm.@new<obj_item_bot_assembly_honkbot>(drop_location());
        object held_index = user.is_holding(this);
        qdel(this);
        if (Dm.truthy(held_index))
            await user.put_in_hand(assembly, held_index);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/storage/boxes/job_boxes.dm:208
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] opens [] and gets consumed by []! It looks like [] trying to commit suicide!", user, this, p_them(), user.p_theyre())));
        playsound(user, Dm.resource("sound/misc/scary_horn.ogg"), 70, vary: TRUE);
        await forceMove(user.drop_location() as atom);
        obj_item_mob_holder consumed = Dm.@new<obj_item_mob_holder>(this, user);
        consumed.desc = I18n.T("It's []! It looks like [] committed suicide!", user.real_name, user.p_they());
        return OXYLOSS;
    }
}

// tg: code/game/objects/items/storage/boxes/job_boxes.dm:278
public partial class obj_item_storage_box_miner_modkits : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/job_boxes.dm:282
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        atom_storage.set_holdable(Dm.list(
            typeof(obj_item_borg_upgrade_modkit),
            typeof(obj_item_crusher_trophy)));
        atom_storage.numerical_stacking = TRUE;
        return dot;
    }

    // tg: code/game/objects/items/storage/boxes/job_boxes.dm:290
    public override async DmTask PopulateContents()
    {
        foreach (object trophy in Dm.each(subtypesof(typeof(obj_item_crusher_trophy))))
            Dm.@new((Type)trophy, this);
        foreach (object modkit in Dm.each(subtypesof(typeof(obj_item_borg_upgrade_modkit))))
        {
            for (float i = 1; i <= 10; i++) //minimum cost ucrrently is 20, and 2 pkas, so lets go with that
                Dm.@new((Type)modkit, this);
        }
    }
}
