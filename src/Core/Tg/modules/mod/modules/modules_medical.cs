// Translated from tgstation (snapshot 1d99bd7f): code/modules/mod/modules/modules_medical.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO2 (D333): the file whole. The medical MOD modules: the health analyzer (a ranged health, wound or chemical scan, the wearer's
// vitals in the window), quick carry (faster carrying and medical work) and its advanced kind, the injector (an extendable piercing
// syringe), the organizer (picks up organs and shoots them into an opened patient), the patient transport (a hardlight body bag), the
// defibrillator and its combat kind (shock gauntlets paid from the suit's charge), the thread ripper (opens a patient's clothing for
// surgery, mended when they part) and the surgical processor. The var blocks are generated (D111).

///Health Analyzer - Gives the user a ranged health analyzer and their health status in the panel.
// tg: code/modules/mod/modules/modules_medical.dm:8
public partial class obj_item_mod_module_health_analyzer : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_medical.dm:3 (file-local, #undef at :62)
    private const string HEALTH_SCAN = "Health";
    // tg: code/modules/mod/modules/modules_medical.dm:4
    private const string WOUND_SCAN = "Wound";
    // tg: code/modules/mod/modules/modules_medical.dm:5
    private const string CHEM_SCAN = "Chemical";

    /// List of all scanning modes.
    // tg: code/modules/mod/modules/modules_medical.dm:26  var/static/list/modes
    public static DmList modes => TgState.Current.obj_item_mod_module_health_analyzer__modes ??= Dm.list(HEALTH_SCAN, WOUND_SCAN, CHEM_SCAN);

    // tg: code/modules/mod/modules/modules_medical.dm:28
    public override object add_ui_data()
    {
        DmList dot = base.add_ui_data() as DmList;
        dot["health"] = mod.wearer?.health is var or__1 && Dm.truthy(or__1) ? or__1 : 0;
        dot["health_max"] = mod.wearer?.getMaxHealth() is var or__2 && Dm.truthy(or__2) ? or__2 : 0;
        dot["loss_brute"] = mod.wearer?.get_brute_loss() is var or__3 && Dm.truthy(or__3) ? or__3 : 0;
        dot["loss_fire"] = mod.wearer?.get_fire_loss() is var or__4 && Dm.truthy(or__4) ? or__4 : 0;
        dot["loss_tox"] = mod.wearer?.get_tox_loss() is var or__5 && Dm.truthy(or__5) ? or__5 : 0;
        dot["loss_oxy"] = mod.wearer?.get_oxy_loss() is var or__6 && Dm.truthy(or__6) ? or__6 : 0;

        return dot;
    }

    // tg: code/modules/mod/modules/modules_medical.dm:39
    public override async DmTask<object> on_select_use(atom target = null)
    {
        object dot = await base.on_select_use(target);
        if (!Dm.truthy(dot))
            return dot;
        if (!isliving(target) || !Dm.truthy(mod.wearer.can_read(this)))
            return dot;
        switch (mode)
        {
            case HEALTH_SCAN:
                healthscan(mod.wearer, target as mob_living);
                break;
            case WOUND_SCAN:
                woundscan(mod.wearer, target as mob_living_carbon);
                break;
            case CHEM_SCAN:
                chemscan(mod.wearer, target as mob_living);
                break;
        }
        await drain_power(use_energy_cost);
        return dot;
    }

    // tg: code/modules/mod/modules/modules_medical.dm:54
    public override object get_configuration(mob user = null)
    {
        DmList dot = base.get_configuration(user) as DmList;
        dot["mode"] = add_ui_configuration(I18n.T("Scan Mode"), "list", mode, modes);
        return dot;
    }

    // tg: code/modules/mod/modules/modules_medical.dm:58
    public override async DmTask configure_edit(object key = null, object value = null)
    {
        switch (key)
        {
            case "mode":
                mode = value as string;
                break;
        }
    }
}

///Quick Carry - Lets the user carry bodies quicker.
// tg: code/modules/mod/modules/modules_medical.dm:68
public partial class obj_item_mod_module_quick_carry : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_medical.dm:80
    public override async DmTask on_part_activation()
    {
        await base.on_part_activation();
        ADD_TRAIT(mod.wearer, TRAIT_FASTMED, REF(this));
        ADD_TRAIT(mod.wearer, quick_carry_trait, REF(this));
    }

    // tg: code/modules/mod/modules/modules_medical.dm:85
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        deleting ??= FALSE;
        await base.on_part_deactivation(deleting);
        REMOVE_TRAIT(mod.wearer, TRAIT_FASTMED, REF(this));
        REMOVE_TRAIT(mod.wearer, quick_carry_trait, REF(this));
    }
}

// tg: code/modules/mod/modules/modules_medical.dm:112
public partial class obj_item_reagent_containers_syringe_mod : obj_item_reagent_containers_syringe
{
    // tg: code/modules/mod/modules/modules_medical.dm:124
    public override object update_reagent_overlay()
    {
        DmList dot = null;
        if (Dm.truthy((reagents as datum_reagents)?.total_volume))
        {
            mutable_appearance filling_overlay = mutable_appearance(Dm.resource("icons/obj/medical/reagent_fillings.dmi"), Dm.text("mod[]", get_rounded_vol())) as mutable_appearance;
            filling_overlay.color = mix_color_from_reagents((reagents as datum_reagents).reagent_list);
            Dm.append(ref dot, filling_overlay);
        }
        return dot;
    }
}

///Organizer - Lets you shoot organs, immediately replacing them if the target has the organ manipulation surgery.
// tg: code/modules/mod/modules/modules_medical.dm:131
public partial class obj_item_mod_module_organizer : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_medical.dm:151
    public override async DmTask<object> on_select_use(atom target = null)
    {
        object dot = await base.on_select_use(target);
        if (!Dm.truthy(dot))
            return dot;
        mob_living_carbon_human wearer_human = mod.wearer;
        if (isorgan(target))
        {
            if (!Dm.truthy(await wearer_human.Adjacent(target)))
                return dot;
            atom_movable organ = target as atom_movable;
            if (length(organ_list) >= max_organs)
            {
                balloon_alert(mod.wearer, I18n.T("too many organs!"));
                return dot;
            }
            Dm.append(ref organ_list, organ);
            await organ.forceMove(this);
            balloon_alert(mod.wearer, I18n.T("picked up []", organ));
            playsound(this, Dm.resource("sound/vehicles/mecha/hydraulic.ogg"), 25, TRUE);
            await drain_power(use_energy_cost);
            return dot;
        }
        if (!Dm.truthy(length(organ_list)))
            return dot;
        atom_movable fired_organ = pop(organ_list) as atom_movable;
        obj_projectile_organ projectile = Dm.@new<obj_projectile_organ>(mod.wearer.loc, fired_organ);
        await projectile.aim_projectile(target, mod.wearer);
        projectile.firer = mod.wearer;
        playsound(this, Dm.resource("sound/vehicles/mecha/hydraulic.ogg"), 25, TRUE);
        Waiting.Detach(projectile.fire()); // INVOKE_ASYNC(projectile, TYPE_PROC_REF(/obj/projectile, fire))
        await drain_power(use_energy_cost);
        return dot;
    }
}

// tg: code/modules/mod/modules/modules_medical.dm:179
public partial class obj_projectile_organ : obj_projectile
{
    // tg: code/modules/mod/modules/modules_medical.dm:187
    [DmParams("mapload", "stored_organ")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_item stored_organ = Dm.arg(__extra, 0) as obj_item;
        object dot = base.Initialize(mapload, __extra);
        if (stored_organ == null)
            return INITIALIZE_HINT_QDEL;
        appearance = stored_organ.appearance;
        Waiting.NoSleep(stored_organ.forceMove(this));
        organ = stored_organ as obj_item_organ;
        return dot;
    }

    // tg: code/modules/mod/modules/modules_medical.dm:195
    public override object Destroy(float? force = null)
    {
        organ = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mod/modules/modules_medical.dm:199
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == organ)
            organ = null;
        return dot;
    }

    // tg: code/modules/mod/modules/modules_medical.dm:204
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        if (!isliving(target) || Dm.truthy(Dm.bitand(organ.organ_flags, ORGAN_UNUSABLE)))
        {
            await organ.forceMove(drop_location() as atom);
            return dot;
        }
        mob_living organ_receiver = target as mob_living;
        // bodyparts actually *do* hit a specific bodypart, but random variance would make this projectile unusable
        // so we just fake it, and assume the organ always hits the place it needs to go
        obj_item_bodypart fake_hit_part = organ_receiver.get_bodypart(Dm.truthy(length(organ.valid_zones)) ? pick(organ.valid_zones) as string : deprecise_zone(organ.zone) as string) as obj_item_bodypart;
        if (!LIMB_HAS_SURGERY_STATE(fake_hit_part, Dm.bitor(Dm.bitor(SURGERY_SKIN_OPEN, SURGERY_ORGANS_CUT), SURGERY_BONE_SAWED)))
        {
            await organ.forceMove(drop_location() as atom);
            return dot;
        }

        // handles swapping any existing organ out for us
        await organ.Insert(target as mob_living_carbon);
        return dot;
    }
}

///Defibrillator - Gives the suit an extendable pair of shock paddles.
// tg: code/modules/mod/modules/modules_medical.dm:236
public partial class obj_item_mod_module_defibrillator : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_medical.dm:257
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(device as datum, COMSIG_DEFIBRILLATOR_SUCCESS, nameof(on_defib_success));
        return dot;
    }

    // tg: code/modules/mod/modules/modules_medical.dm:261
    public virtual async DmTask<object> on_defib_success(obj_item_shockpaddles source = null)
    {
        await drain_power(use_energy_cost);
        source.recharge(defib_cooldown);
        return COMPONENT_DEFIB_STOP;
    }
}

///Thread Ripper - Temporarily rips apart clothing to make it not cover the body.
// tg: code/modules/mod/modules/modules_medical.dm:299
public partial class obj_item_mod_module_thread_ripper : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_medical.dm:321
    public override async DmTask<object> on_select_use(atom target = null)
    {
        object dot = await base.on_select_use(target);
        if (!Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(await mod.wearer.Adjacent(target)) || !iscarbon(target) || target == mod.wearer)
        {
            balloon_alert(mod.wearer, I18n.T("invalid target!"));
            return dot;
        }
        mob_living_carbon carbon_target = target as mob_living_carbon;
        if (Dm.truthy(length(ripped_clothing)))
        {
            balloon_alert(mod.wearer, I18n.T("already ripped!"));
            return dot;
        }
        balloon_alert(mod.wearer, I18n.T("ripping clothing..."));
        playsound(this, Dm.resource("sound/items/zip/zip.ogg"), 25, TRUE, frequency: -1);
        if (!Dm.truthy(await do_after(mod.wearer, 1.5f * SECONDS, target: carbon_target)))
        {
            balloon_alert(mod.wearer, I18n.T("interrupted!"));
            return dot;
        }
        object target_zones = body_zone2cover_flags(mod.wearer.zone_selected);
        foreach (obj_item clothing in Dm.each_cast<obj_item>(carbon_target.get_equipped_items()))
        {
            if (clothing == null)
                continue;
            float shared_flags = Dm.num(Dm.bitand(target_zones, clothing.body_parts_covered));
            if (Dm.truthy(shared_flags))
            {
                ripped_clothing[clothing] = shared_flags;
                clothing.body_parts_covered = Dm.bitand(clothing.body_parts_covered, Dm.bitnot(shared_flags));
            }
        }
        return dot;
    }

    // tg: code/modules/mod/modules/modules_medical.dm:346
    public override async DmTask<object> on_process(object seconds_per_tick = null)
    {
        object dot = await base.on_process(seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(length(ripped_clothing)))
            return dot;
        float zipped = FALSE;
        foreach (obj_item clothing in Dm.each_cast<obj_item>(ripped_clothing))
        {
            if (QDELETED(clothing))
            {
                Dm.remove(ref ripped_clothing, clothing);
                continue;
            }
            mob_living_carbon clothing_wearer = clothing.loc as mob_living_carbon;
            if (istype(clothing_wearer, typeof(mob_living_carbon)) && Dm.truthy(await mod.wearer.Adjacent(clothing_wearer)) && !Dm.truthy(clothing_wearer.is_holding(clothing)))
                continue;
            zipped = TRUE;
            clothing.body_parts_covered = Dm.bitor(clothing.body_parts_covered, Dm.num(ripped_clothing[clothing]));
            Dm.remove(ref ripped_clothing, clothing);
        }
        if (Dm.truthy(zipped))
        {
            playsound(this, Dm.resource("sound/items/zip/zip.ogg"), 25, TRUE);
            balloon_alert(mod.wearer, I18n.T("clothing mended"));
        }
        return dot;
    }

    // tg: code/modules/mod/modules/modules_medical.dm:367
    public override async DmTask on_part_deactivation(float? deleting = null)
    {
        deleting ??= FALSE;
        if (!Dm.truthy(length(ripped_clothing)))
            return;
        foreach (obj_item clothing in Dm.each_cast<obj_item>(ripped_clothing))
        {
            if (QDELETED(clothing))
            {
                Dm.remove(ref ripped_clothing, clothing);
                continue;
            }
            clothing.body_parts_covered = Dm.bitor(clothing.body_parts_covered, Dm.num(ripped_clothing[clothing]));
        }
        ripped_clothing = Dm.list();
        if (!Dm.truthy(deleting))
            playsound(this, Dm.resource("sound/items/zip/zip.ogg"), 25, TRUE);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mod/modules/modules_medical.dm:26  var/static/list/modes
    internal DmList obj_item_mod_module_health_analyzer__modes;
}
