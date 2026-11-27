// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/crates.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P3: the crate base (docs/ai/specs/m4a-ownership.md, P3's list for crates.dm). tear_manifest is translated beside the
// list (the crate's attack_hand and after_open call it on every use; no crate has a manifest until cargo, M10). M8z slice C:
// the climbing elements (D239); the elevation element and the soapbox component are deferred leaves (the elevation is the client's
// structure top, D239; the soapbox M10). Later: the trash cart, freezers' air (M4b), glitter and gold crates; gaps P4d: the market crate.

// tg: code/game/objects/structures/crates_lockers/crates.dm:141
public partial class obj_structure_closet : obj_structure
{
    ///Spawns two to six maintenance spawners inside the closet
    // tg: code/game/objects/structures/crates_lockers/crates.dm:141
    public virtual void populate_with_random_maint_loot()
    {
        for (float i = 1, i__end = rand(2, 6); i <= i__end; i++)
            Dm.@new<obj_effect_spawner_random_maintenance>(this);

        UnregisterSignal(this, COMSIG_CLOSET_CONTENTS_INITIALIZED);
    }
}

// tg: code/game/objects/structures/crates_lockers/crates.dm:1
public partial class obj_structure_closet_crate : obj_structure_closet
{
    // tg: code/game/objects/structures/crates_lockers/crates.dm:48  (proc-level var/static: rules 10.6, a TgState field)
    private static ref DmList Initialize__crate_paint_jobs => ref TgState.Current.obj_structure_closet_crate__Initialize__crate_paint_jobs;

    // tg: code/game/objects/structures/crates_lockers/crates.dm:41
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        Waiting.NoSleep(AddElement(typeof(datum_element_climbable), Dm.named("climb_time", crate_climb_time), Dm.named("climb_stun", 0))); //add element in closed state before parent init opens it(if it does)
        if (Dm.truthy(elevation))
        {
            Waiting.NoSleep(AddElement(typeof(datum_element_climb_walkable)));
            Dm.Deferred("code/game/objects/structures/crates_lockers/crates.dm:45", "Initialize: AddElement(/datum/element/elevation, pixel_shift = elevation): the raised position of what stands on it: the client draws a standing mob on the structure's top instead (D239, DV154)");
        }
        dot = base.Initialize(mapload, __extra);

        if (Initialize__crate_paint_jobs == null)
        {
            Initialize__crate_paint_jobs = Dm.list(
                Dm.kv("Internals", Dm.list(Dm.kv("icon_state", "o2crate"))),
                Dm.kv("Medical", Dm.list(Dm.kv("icon_state", "medical"))),
                Dm.kv("Medical Plus", Dm.list(Dm.kv("icon_state", "medicalcrate"))),
                Dm.kv("Radiation", Dm.list(Dm.kv("icon_state", "radiation"))),
                Dm.kv("Hydrophonics", Dm.list(Dm.kv("icon_state", "hydrocrate"))),
                Dm.kv("Science", Dm.list(Dm.kv("icon_state", "scicrate"))),
                Dm.kv("Robotics", Dm.list(Dm.kv("icon_state", "robo"))),
                Dm.kv("Solar", Dm.list(Dm.kv("icon_state", "engi_e_crate"))),
                Dm.kv("Engineering", Dm.list(Dm.kv("icon_state", "engi_crate"))),
                Dm.kv("Atmospherics", Dm.list(Dm.kv("icon_state", "atmos"))),
                Dm.kv("Cargo", Dm.list(Dm.kv("icon_state", "cargo"))),
                Dm.kv("Mining", Dm.list(Dm.kv("icon_state", "mining"))),
                Dm.kv("Command", Dm.list(Dm.kv("icon_state", "centcom")))
            );
        }
        if (Dm.truthy(paint_jobs))
            paint_jobs = Initialize__crate_paint_jobs;
        Dm.Deferred("code/game/objects/structures/crates_lockers/crates.dm:66", "Initialize: AddComponent(/datum/component/soapbox) (speaking from a crate, M10+)");
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/crates.dm:69
    public override object Destroy(float? force = null)
    {
        manifest = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/crates_lockers/crates.dm:73
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = null;
        dot = await base.CanAllowThrough(mover, border_dir);
        if (!(mover is obj_structure_closet))
        {
            obj_structure_closet_crate locatedcrate = Dm.locate<obj_structure_closet_crate>(get_turf(mover));
            if (Dm.truthy(locatedcrate)) //you can walk on it like tables, if you're not in an open crate trying to move to a closed crate
            {
                if (Dm.truthy(opened)) //if we're open, allow entering regardless of located crate openness
                    return TRUE;
                if (!Dm.truthy(locatedcrate.opened)) //otherwise, if the located crate is closed, allow entering
                    return TRUE;
            }
        }
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/crates.dm:83
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", base_icon_state == null ? Dm.initial<obj_structure_closet_crate, string>(this, c => c.icon_state) : base_icon_state, Dm.truthy(opened) ? "open" : "");
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/structures/crates_lockers/crates.dm:87
    public override object closet_update_overlays(DmList new_overlays = null)
    {
        DmList dot = null;
        dot = new_overlays;
        if (Dm.truthy(manifest))
            Dm.append(ref dot, "manifest");
        if (!Dm.truthy(opened))
        {
            if (Dm.truthy(broken))
                Dm.append(ref dot, "securecrateemag");
            else if (Dm.truthy(locked))
                Dm.append(ref dot, "securecrater");
            else if (Dm.truthy(secure))
                Dm.append(ref dot, "securecrateg");
        }
        if (Dm.truthy(welded))
        {
            mutable_appearance weld_overlay = mutable_appearance(icon, "welded") as mutable_appearance;
            weld_overlay.pixel_w = weld_w;
            weld_overlay.pixel_z = weld_z;
            Dm.append(ref dot, weld_overlay);
        }
        if (Dm.truthy(opened) && Dm.truthy(lid_icon_state))
        {
            mutable_appearance lid = mutable_appearance(icon: lid_icon, icon_state: lid_icon_state as string) as mutable_appearance;
            lid.pixel_w = lid_w;
            lid.pixel_z = lid_z;
            lid.layer = layer;
            Dm.append(ref dot, lid);
        }
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/crates.dm:113
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(await tear_manifest(user)))
            return TRUE;
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/game/objects/structures/crates_lockers/crates.dm:118
    public override async DmTask after_open(mob_living user = null, float? force = null)
    {
        await base.after_open(user, force);
        RemoveElement(typeof(datum_element_climbable), Dm.named("climb_time", crate_climb_time), Dm.named("climb_stun", 0));
        await AddElement(typeof(datum_element_climbable), Dm.named("climb_time", Dm.num(crate_climb_time) * 0.5f), Dm.named("climb_stun", 0));
        if (!Dm.eq(elevation, elevation_open))
            Dm.Deferred("code/game/objects/structures/crates_lockers/crates.dm:122", "after_open: the open elevation element: the raised position of what stands on it: the client draws a standing mob on the structure's top instead (D239, DV154)");
        await tear_manifest();
    }

    // tg: code/game/objects/structures/crates_lockers/crates.dm:130
    [DmParams("user")]
    public override async DmTask after_close(mob_living user = null, params object[] __extra)
    {
        await base.after_close(user, __extra);
        RemoveElement(typeof(datum_element_climbable), Dm.named("climb_time", Dm.num(crate_climb_time) * 0.5f), Dm.named("climb_stun", 0));
        await AddElement(typeof(datum_element_climbable), Dm.named("climb_time", crate_climb_time), Dm.named("climb_stun", 0));
        if (!Dm.eq(elevation, elevation_open))
            Dm.Deferred("code/game/objects/structures/crates_lockers/crates.dm:134", "after_close: the closed elevation element: the raised position of what stands on it: the client draws a standing mob on the structure's top instead (D239, DV154)");
    }

    ///Removes the supply manifest from the closet, returning TRUE if one was torn off
    // tg: code/game/objects/structures/crates_lockers/crates.dm:150
    public virtual async DmTask<object> tear_manifest(mob user = null)
    {
        obj_item our_manifest = manifest?.resolve() as obj_item;
        if (QDELETED(our_manifest))
        {
            manifest = null;
            return FALSE;
        }
        if (Dm.truthy(user))
            to_chat(user, span_notice(I18n.T("You tear the manifest off of [].", this)));
        playsound(this, Dm.resource("sound/items/poster/poster_ripped.ogg"), 75, TRUE); // tg: code/game/objects/structures/crates_lockers/crates.dm:157

        await our_manifest.forceMove(drop_location() as atom);
        if (ishuman(user))
            await user.put_in_hands(our_manifest);
        manifest = null;
        update_appearance();
        return TRUE;
    }

    // tg: code/game/objects/structures/crates_lockers/crates.dm:483
    public override void add_to_roundstart_list()
    {
        return;
    }
}

// tg: code/game/objects/structures/crates_lockers/crates.dm:510  (gaps P4d: the black market's shield bubble, its Launch shipping)
public partial class obj_structure_closet_crate_market : obj_structure_closet_crate
{
    // tg: code/game/objects/structures/crates_lockers/crates.dm:517
    public override async DmTask after_open(mob_living user = null, float? force = null)
    {
        await base.after_open(user, force);
        visible_message(span_notice(I18n.T("[] pops as [] touches it!", this, user)));
        await pop_crate();
    }

    // tg: code/game/objects/structures/crates_lockers/crates.dm:522
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        object dot = base.throw_impact(hit_atom, throwingdatum, __extra);
        Waiting.NoSleep(pop_crate());
        return dot;
    }

    /// Called when the bubble either arrives at the station, or is interacted with someone/something.
    // tg: code/game/objects/structures/crates_lockers/crates.dm:527
    public virtual async DmTask pop_crate()
    {
        do_sparks(1, TRUE, get_turf(this));
        await dump_contents();
        qdel(this);
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/structures/crates_lockers/crates.dm:48  /obj/structure/closet/crate/Initialize's var/static/list/crate_paint_jobs
    internal DmList obj_structure_closet_crate__Initialize__crate_paint_jobs;
}

// M4a P3: the PopulateContents of Meta's containers in this file (D133), translated with the help of m4/p3w/popgen2.py
// (the pure `new /path(src)` bodies, their loops, prob and switch); the file's other procs are their milestones'.

// tg: code/game/objects/structures/crates_lockers/crates.dm:255
public partial class obj_structure_closet_crate_freezer : obj_structure_closet_crate
{
    // tg: code/game/objects/structures/crates_lockers/crates.dm:266
    public override async DmTask process_internal_air(object seconds_per_tick = null)
    {
        if (Dm.truthy(opened))
        {
            datum_gas_mixture current_exposed_air = await loc.return_air() as datum_gas_mixture;
            if (current_exposed_air == null)
                return;
            // The internal air won't cool down the external air when the freezer is opened.
            internal_air.temperature = Math.Max(current_exposed_air.temperature, internal_air.temperature);
            await base.process_internal_air(seconds_per_tick);
            return;
        }
        else
        {
            if (internal_air.temperature <= minimum_temperature)
                return;
            float temperature_decrease_this_tick = Math.Min(cooling_rate_per_second * Dm.num(seconds_per_tick), internal_air.temperature - minimum_temperature);
            internal_air.temperature -= temperature_decrease_this_tick;
        }
    }
}

// tg: code/game/objects/structures/crates_lockers/crates.dm:284
public partial class obj_structure_closet_crate_freezer_blood : obj_structure_closet_crate_freezer
{
    // tg: code/game/objects/structures/crates_lockers/crates.dm:284
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_reagent_containers_blood>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:286
        Dm.@new<obj_item_reagent_containers_blood>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:287
        Dm.@new<obj_item_reagent_containers_blood_a_minus>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:288
        Dm.@new<obj_item_reagent_containers_blood_b_minus>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:289
        Dm.@new<obj_item_reagent_containers_blood_b_plus>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:290
        Dm.@new<obj_item_reagent_containers_blood_o_minus>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:291
        Dm.@new<obj_item_reagent_containers_blood_o_plus>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:292
        Dm.@new<obj_item_reagent_containers_blood_lizard>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:293
        Dm.@new<obj_item_reagent_containers_blood_ethereal>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:294
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/crates.dm:295
        {
            Dm.@new<obj_item_reagent_containers_blood_random>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:296
        }
        Dm.@new<obj_item_paper_fluff_jobs_medical_blood_types>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:297
    }
}

// tg: code/game/objects/structures/crates_lockers/crates.dm:303
public partial class obj_structure_closet_crate_freezer_surplus_limbs : obj_structure_closet_crate_freezer
{
    // tg: code/game/objects/structures/crates_lockers/crates.dm:303
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_bodypart_arm_left_robot_surplus>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:305
        Dm.@new<obj_item_bodypart_arm_left_robot_surplus>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:306
        Dm.@new<obj_item_bodypart_arm_right_robot_surplus>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:307
        Dm.@new<obj_item_bodypart_arm_right_robot_surplus>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:308
        Dm.@new<obj_item_bodypart_leg_left_robot_surplus>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:309
        Dm.@new<obj_item_bodypart_leg_left_robot_surplus>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:310
        Dm.@new<obj_item_bodypart_leg_right_robot_surplus>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:311
        Dm.@new<obj_item_bodypart_leg_right_robot_surplus>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:312
    }
}

// tg: code/game/objects/structures/crates_lockers/crates.dm:454
public partial class obj_structure_closet_crate_goldcrate_stocked : obj_structure_closet_crate_goldcrate
{
    // tg: code/game/objects/structures/crates_lockers/crates.dm:454
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_storage_belt_champion>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:456
    }
}

// tg: code/game/objects/structures/crates_lockers/crates.dm:427
public partial class obj_structure_closet_crate_mod : obj_structure_closet_crate
{
    // tg: code/game/objects/structures/crates_lockers/crates.dm:427
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/crates.dm:429
        {
            Dm.@new<obj_item_mod_core_standard>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:430
        }
        for (float i = 1; i <= 2; i++) // tg: code/game/objects/structures/crates_lockers/crates.dm:431
        {
            Dm.@new<obj_item_clothing_neck_link_scryer_loaded>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:432
        }
    }
}

// tg: code/game/objects/structures/crates_lockers/crates.dm:469
public partial class obj_structure_closet_crate_silvercrate : obj_structure_closet_crate
{
    // tg: code/game/objects/structures/crates_lockers/crates.dm:469
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        for (float i = 1; i <= 5; i++) // tg: code/game/objects/structures/crates_lockers/crates.dm:471
        {
            Dm.@new<obj_item_coin_silver>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:472
        }
    }
}

// tg: code/game/objects/structures/crates_lockers/crates.dm:439
public partial class obj_structure_closet_crate_solarpanel_small : obj_structure_closet_crate
{
    // tg: code/game/objects/structures/crates_lockers/crates.dm:439
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        for (float i = 1; i <= 13; i++) // tg: code/game/objects/structures/crates_lockers/crates.dm:441
        {
            Dm.@new<obj_item_solar_assembly>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:442
        }
        Dm.@new<obj_item_circuitboard_computer_solar_control>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:443
        Dm.@new<obj_item_paper_guides_jobs_engi_solars>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:444
        Dm.@new<obj_item_electronics_tracker>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:445
    }
}

// tg: code/game/objects/structures/crates_lockers/crates.dm:214  (gaps ST3, D333)
public partial class obj_structure_closet_crate_trashcart_filled : obj_structure_closet_crate_trashcart
{
    // tg: code/game/objects/structures/crates_lockers/crates.dm:219
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra); // tg: code/game/objects/structures/crates_lockers/crates.dm:220
        for (float i = 1, i__end = rand(7, 15); i <= i__end; i++) // tg: code/game/objects/structures/crates_lockers/crates.dm:221
        {
            Dm.@new<obj_effect_spawner_random_trash_garbage>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:222
            if (prob(12)) // tg: code/game/objects/structures/crates_lockers/crates.dm:223
            {
                Dm.@new<obj_item_storage_bag_trash_filled>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:224
            }
        }
    }
}

// tg: code/game/objects/structures/crates_lockers/crates.dm:315  (gaps ST3, D333)
public partial class obj_structure_closet_crate_freezer_organ : obj_structure_closet_crate_freezer
{
    // tg: code/game/objects/structures/crates_lockers/crates.dm:318
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra); // tg: code/game/objects/structures/crates_lockers/crates.dm:319
        Dm.@new<obj_item_organ_heart>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:320
        Dm.@new<obj_item_organ_lungs>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:321
        Dm.@new<obj_item_organ_eyes>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:322
        Dm.@new<obj_item_organ_ears>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:323
        Dm.@new<obj_item_organ_tongue>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:324
        Dm.@new<obj_item_organ_liver>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:325
        Dm.@new<obj_item_organ_stomach>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:326
        Dm.@new<obj_item_organ_appendix>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:327
    }
}

// tg: code/game/objects/structures/crates_lockers/crates.dm:400  (gaps ST3, D333)
public partial class obj_structure_closet_crate_rcd : obj_structure_closet_crate
{
    // tg: code/game/objects/structures/crates_lockers/crates.dm:405
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra); // tg: code/game/objects/structures/crates_lockers/crates.dm:406
        for (float i = 1; i <= 4; i++) // tg: code/game/objects/structures/crates_lockers/crates.dm:407
        {
            Dm.@new<obj_item_rcd_ammo>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:408
        }
        Dm.@new<obj_item_construction_rcd>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:409
    }
}

// tg: code/game/objects/structures/crates_lockers/crates.dm:454  (gaps ST3, D333)
public partial class obj_structure_closet_crate_goldcrate_stocked : obj_structure_closet_crate_goldcrate
{
    // tg: code/game/objects/structures/crates_lockers/crates.dm:458
    public override async DmTask populate_contents_immediate()
    {
        await base.populate_contents_immediate(); // tg: code/game/objects/structures/crates_lockers/crates.dm:459
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/structures/crates_lockers/crates.dm:460
        {
            Dm.@new<obj_item_stack_sheet_mineral_gold>(this, 1, FALSE); // tg: code/game/objects/structures/crates_lockers/crates.dm:461
        }
    }
}

// tg: code/game/objects/structures/crates_lockers/crates.dm:475  (gaps ST3, D333)
public partial class obj_structure_closet_crate_decorations : obj_structure_closet_crate
{
    // tg: code/game/objects/structures/crates_lockers/crates.dm:478
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra); // tg: code/game/objects/structures/crates_lockers/crates.dm:479
        for (float i = 1; i <= 4; i++) // tg: code/game/objects/structures/crates_lockers/crates.dm:480
        {
            Dm.@new<obj_effect_spawner_random_decoration_generic>(this); // tg: code/game/objects/structures/crates_lockers/crates.dm:481
        }
    }
}

// gaps ST3 (D333): the trash and laundry carts, the glittery crates; the contents above of the filled trash cart, the organ freezer,
// the RCD crate, the stocked gold crate and the decorations crate.

// tg: code/game/objects/structures/crates_lockers/crates.dm:189  (gaps ST3)
public partial class obj_structure_closet_crate_trashcart : obj_structure_closet_crate
{
    // tg: code/game/objects/structures/crates_lockers/crates.dm:207
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_SLUDGE, CELL_VIRUS_TABLE_GENERIC, rand(2, 3), 15));
        Waiting.NoSleep(AddElement(typeof(datum_element_noisy_movement)));
        return dot;
    }
}

// tg: code/game/objects/structures/crates_lockers/crates.dm:212  (gaps ST3)
public partial class obj_structure_closet_crate_trashcart_filled : obj_structure_closet_crate_trashcart
{
    // tg: code/game/objects/structures/crates_lockers/crates.dm:214
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload))
            Dm.@new<obj_effect_spawner_random_trash_grime>(loc); //needs to be done before the trashcart is opened because it spawns things in a range outside of the trashcart
        return dot;
    }
}

// tg: code/game/objects/structures/crates_lockers/crates.dm:487  (gaps ST3)
public partial class obj_structure_closet_crate_glitter : obj_structure_closet_crate
{
    // tg: code/game/objects/structures/crates_lockers/crates.dm:494
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);

        turf old_turf = get_turf(old_loc);
        if (old_turf == null)
            return dot;
        if (prob(glitter_prob))
            old_turf.spawn_glitter(Dm.list(Dm.kv(Dm.text("[]", glitter_color), 100)));
        return dot;
    }
}
