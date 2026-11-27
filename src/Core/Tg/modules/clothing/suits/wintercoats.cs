// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/suits/wintercoats.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Holds the [DmPath] of a type that M2 F1's translated defaults name (the job outfit's slots, the cloth that repairs clothing, the
// prisoner suit get_random_jumpsuit leaves out), in the earliest .dme file that declares it (rules 2.3). X3: examine; D296's sound
// audit: the alt-click zip (click_alt). J1 (D298): the coat's Initialize (the allowed list), on_hood_up and on_hood_down, the
// Initialize of the job coats (captain, head of personnel, security, medical, science, engineering and cargo) and the engineering
// coat's and its hood's worn_overlays (the lights' emissive mask, DV12). Gaps CL1 (D333): the rest of the file (the EVA, CentCom
// and virologist coats' allowed lists, the tailored coat's, the pullover's and the zip-up hoodie's colours passed to their hoods,
// the zip-up hoodie's hood drawn down on the back).

// tg: code/modules/clothing/suits/wintercoats.dm:2
[DmPath("/obj/item/clothing/suit/hooded/wintercoat")]
public partial class obj_item_clothing_suit_hooded_wintercoat : obj_item_clothing_suit_hooded
{
    // tg: code/modules/clothing/suits/wintercoats.dm:25
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, Dm.list(
            typeof(obj_item_flashlight),
            typeof(obj_item_lighter),
            typeof(obj_item_modular_computer_pda),
            typeof(obj_item_radio),
            typeof(obj_item_storage_bag_books),
            typeof(obj_item_storage_fancy_cigarettes),
            typeof(obj_item_tank_internals_emergency_oxygen),
            typeof(obj_item_tank_internals_plasmaman),
            typeof(obj_item_toy)
        ));
        return dot;
    }

    // tg: code/modules/clothing/suits/wintercoats.dm:39
    public override void on_hood_up(obj_item_clothing_head_hooded hood = null)
    {
        base.on_hood_up(hood);
        zipped = TRUE;
    }

    /// Called when the hood is hidden
    // tg: code/modules/clothing/suits/wintercoats.dm:44
    public override void on_hood_down(obj_item_clothing_head_hooded hood = null)
    {
        base.on_hood_down(hood);
        zipped = FALSE;
    }

    // tg: code/modules/clothing/suits/wintercoats.dm:48
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(can_altclick_zip))
            Dm.append(ref dot, span_notice(I18n.T("<b>Alt-click</b> to []zip.", Dm.truthy(zipped) ? I18n.T("un") : "")));
        return dot;
    }

    // tg: code/modules/clothing/suits/wintercoats.dm:54
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!Dm.truthy(can_altclick_zip))
            return CLICK_ACTION_BLOCKING;
        zipped = Dm.truthy(zipped) ? FALSE : TRUE;
        playsound(this, Dm.resource("sound/items/zip/zip_up.ogg"), 30, TRUE, -3);
        object base_state = Dm.initial(this, (atom p) => p.post_init_icon_state);
        worn_icon_state = Dm.text("[][]", Dm.truthy(base_state) ? base_state : Dm.initial(this, (atom p) => p.icon_state), Dm.truthy(zipped) ? "_t" : "");
        balloon_alert(user, Dm.truthy(zipped) ? I18n.T("zipped") : I18n.T("unzipped"));

        if (ishuman(loc))
        {
            mob_living_carbon_human wearer = loc as mob_living_carbon_human;
            await wearer.update_worn_oversuit();
        }
        return CLICK_ACTION_SUCCESS;
    }
}

// tg: code/modules/clothing/suits/wintercoats.dm:85
public partial class obj_item_clothing_suit_hooded_wintercoat_eva : obj_item_clothing_suit_hooded_wintercoat
{
    // tg: code/modules/clothing/suits/wintercoats.dm:108
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, typeof(obj_item_tank_internals));
        return dot;
    }
}

// tg: code/modules/clothing/suits/wintercoats.dm:131
public partial class obj_item_clothing_suit_hooded_wintercoat_centcom : obj_item_clothing_suit_hooded_wintercoat
{
    // tg: code/modules/clothing/suits/wintercoats.dm:149
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, GLOB.security_wintercoat_allowed);
        return dot;
    }
}

// tg: code/modules/clothing/suits/wintercoats.dm:169
public partial class obj_item_clothing_suit_hooded_wintercoat_captain : obj_item_clothing_suit_hooded_wintercoat
{
    // tg: code/modules/clothing/suits/wintercoats.dm:186
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, GLOB.security_wintercoat_allowed);
        return dot;
    }
}

// tg: code/modules/clothing/suits/wintercoats.dm:403
public partial class obj_item_clothing_suit_hooded_wintercoat_medical_viro : obj_item_clothing_suit_hooded_wintercoat_medical
{
    // tg: code/modules/clothing/suits/wintercoats.dm:410
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, typeof(obj_item_storage_bag_bio));
        return dot;
    }
}

// tg: code/modules/clothing/suits/wintercoats.dm:621
public partial class obj_item_clothing_suit_hooded_wintercoat_cargo_qm : obj_item_clothing_suit_hooded_wintercoat_cargo
{
    // tg: code/modules/clothing/suits/wintercoats.dm:627
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, Dm.list(
            typeof(obj_item_melee_baton_telescopic)
        ));
        return dot;
    }
}

// tg: code/modules/clothing/suits/wintercoats.dm:568
public partial class obj_item_clothing_suit_hooded_wintercoat_engineering_ce : obj_item_clothing_suit_hooded_wintercoat_engineering
{
    // tg: code/modules/clothing/suits/wintercoats.dm:579
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, Dm.list(
            typeof(obj_item_melee_baton_telescopic)
        ));
        return dot;
    }
}

// tg: code/modules/clothing/suits/wintercoats.dm:204
public partial class obj_item_clothing_suit_hooded_wintercoat_hop : obj_item_clothing_suit_hooded_wintercoat
{
    // tg: code/modules/clothing/suits/wintercoats.dm:223
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, GLOB.security_wintercoat_allowed);
        return dot;
    }
}

// tg: code/modules/clothing/suits/wintercoats.dm:366
public partial class obj_item_clothing_suit_hooded_wintercoat_medical_chemistry : obj_item_clothing_suit_hooded_wintercoat_medical
{
    // tg: code/modules/clothing/suits/wintercoats.dm:372
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, typeof(obj_item_storage_bag_chemistry));
        return dot;
    }
}

// tg: code/modules/clothing/suits/wintercoats.dm:341
public partial class obj_item_clothing_suit_hooded_wintercoat_medical_cmo : obj_item_clothing_suit_hooded_wintercoat_medical
{
    // tg: code/modules/clothing/suits/wintercoats.dm:353
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, Dm.list(
            typeof(obj_item_melee_baton_telescopic)
        ));
        return dot;
    }
}

// tg: code/modules/clothing/suits/wintercoats.dm:382
public partial class obj_item_clothing_suit_hooded_wintercoat_medical_coroner : obj_item_clothing_suit_hooded_wintercoat_medical
{
    // tg: code/modules/clothing/suits/wintercoats.dm:388
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, Dm.list(
            typeof(obj_item_autopsy_scanner),
            typeof(obj_item_scythe),
            typeof(obj_item_shovel),
            typeof(obj_item_shovel_serrated),
            typeof(obj_item_trench_tool)
        ));
        return dot;
    }
}

// tg: code/modules/clothing/suits/wintercoats.dm:420
public partial class obj_item_clothing_suit_hooded_wintercoat_medical_paramedic : obj_item_clothing_suit_hooded_wintercoat_medical
{
    // tg: code/modules/clothing/suits/wintercoats.dm:426
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), -3)); //mirrored from jacket
        Dm.append(ref allowed, typeof(obj_item_crowbar_power_paramedic));
        return dot;
    }
}

// tg: code/modules/clothing/suits/wintercoats.dm:510
public partial class obj_item_clothing_suit_hooded_wintercoat_science_genetics : obj_item_clothing_suit_hooded_wintercoat_science
{
    // tg: code/modules/clothing/suits/wintercoats.dm:516
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, typeof(obj_item_sequence_scanner));
        return dot;
    }
}

// tg: code/modules/clothing/suits/wintercoats.dm:474
public partial class obj_item_clothing_suit_hooded_wintercoat_science_rd : obj_item_clothing_suit_hooded_wintercoat_science
{
    // tg: code/modules/clothing/suits/wintercoats.dm:485
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, Dm.list(
            typeof(obj_item_melee_baton_telescopic)
        ));
        return dot;
    }
}

// tg: code/modules/clothing/suits/wintercoats.dm:268
public partial class obj_item_clothing_suit_hooded_wintercoat_security : obj_item_clothing_suit_hooded_wintercoat
{
    // tg: code/modules/clothing/suits/wintercoats.dm:283
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, GLOB.security_wintercoat_allowed);
        return dot;
    }
}

// tg: code/modules/clothing/suits/wintercoats.dm:526
public partial class obj_item_clothing_suit_hooded_wintercoat_engineering : obj_item_clothing_suit_hooded_wintercoat
{
    // tg: code/modules/clothing/suits/wintercoats.dm:548
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        isinhands ??= FALSE;
        bodyshape ??= NONE;
        DmList dot = null;
        dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape) as DmList;
        // DEVIATION(DV12): . += emissive_appearance(icon_file, "[icon_state]-emissive", src, alpha = src.alpha, effect_type = EMISSIVE_SPECULAR), the
        // coat's glow on the emissive plane, which the 3D client does not have
        if (!Dm.truthy(isinhands))
            Dm.Deferred("code/modules/clothing/suits/wintercoats.dm:551", "worn_overlays: emissive_appearance of the engineering coat's lights (DEVIATION DV12)");
        return dot;
    }
}

// tg: code/modules/clothing/suits/wintercoats.dm:553
public partial class obj_item_clothing_head_hooded_winterhood_engineering : obj_item_clothing_head_hooded_winterhood
{
    // tg: code/modules/clothing/suits/wintercoats.dm:561
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        isinhands ??= FALSE;
        bodyshape ??= NONE;
        DmList dot = null;
        dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape) as DmList;
        // DEVIATION(DV12): . += emissive_appearance(icon_file, "[icon_state]-emissive", src, alpha = src.alpha, effect_type = EMISSIVE_SPECULAR), the
        // hood's glow on the emissive plane, which the 3D client does not have
        if (!Dm.truthy(isinhands))
            Dm.Deferred("code/modules/clothing/suits/wintercoats.dm:564", "worn_overlays: emissive_appearance of the engineering hood's lights (DEVIATION DV12)");
        return dot;
    }
}

// tg: code/modules/clothing/suits/wintercoats.dm:668
public partial class obj_item_clothing_suit_hooded_wintercoat_custom : obj_item_clothing_suit_hooded_wintercoat
{
    // tg: code/modules/clothing/suits/wintercoats.dm:682
    [DmParams("colors", "new_config", "new_worn_config", "new_inhand_left", "new_inhand_right")]
    public override async DmTask set_greyscale(object colors = null, object new_config = null, params object[] __extra)
    {
        await base.set_greyscale(colors, new_config, __extra);
        if (hood == null)
            return;
        DmList coat_colors = SSgreyscale.ParseColorString(greyscale_colors) as DmList;
        DmList new_coat_colors = coat_colors.Copy(1, 4);
        await hood.set_greyscale(new_coat_colors); //Adopt the suit's grayscale coloring for visual clarity.
        hood.update_slot_icon();
    }

    //But also keep old method in case the hood is (re-)created later
    // tg: code/modules/clothing/suits/wintercoats.dm:692
    public override async DmTask on_hood_created(obj_item_clothing_head_hooded hood = null)
    {
        await base.on_hood_created(hood);
        DmList coat_colors = SSgreyscale.ParseColorString(greyscale_colors) as DmList;
        DmList new_coat_colors = coat_colors.Copy(1, 4);
        await hood.set_greyscale(new_coat_colors); //Adopt the suit's grayscale coloring for visual clarity.
    }
}

// tg: code/modules/clothing/suits/wintercoats.dm:705
public partial class obj_item_clothing_suit_hooded_wintercoat_pullover : obj_item_clothing_suit_hooded_wintercoat
{
    // tg: code/modules/clothing/suits/wintercoats.dm:720
    public override void on_hood_up(obj_item_clothing_head_hooded hood = null)
    {
        // return: the pullover does not zip up with its hood
    }

    // tg: code/modules/clothing/suits/wintercoats.dm:723
    public override void on_hood_down(obj_item_clothing_head_hooded hood = null)
    {
        // return
    }

    // tg: code/modules/clothing/suits/wintercoats.dm:737
    [DmParams("colors", "new_config", "new_worn_config", "new_inhand_left", "new_inhand_right")]
    public override async DmTask set_greyscale(object colors = null, object new_config = null, params object[] __extra)
    {
        await base.set_greyscale(colors, new_config, __extra);
        if (hood == null)
            return;
        DmList coat_colors = SSgreyscale.ParseColorString(greyscale_colors) as DmList;
        await hood.set_greyscale(coat_colors);
        hood.update_slot_icon();
    }

    // tg: code/modules/clothing/suits/wintercoats.dm:745
    public override async DmTask on_hood_created(obj_item_clothing_head_hooded hood = null)
    {
        await base.on_hood_created(hood);
        DmList coat_colors = SSgreyscale.ParseColorString(greyscale_colors) as DmList;
        await hood.set_greyscale(coat_colors);
    }
}

// tg: code/modules/clothing/suits/wintercoats.dm:750
public partial class obj_item_clothing_suit_hooded_wintercoat_zipup : obj_item_clothing_suit_hooded_wintercoat
{
    // tg: code/modules/clothing/suits/wintercoats.dm:764
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        bodyshape ??= NONE;
        DmList dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape) as DmList;
        if (Dm.truthy(isinhands) || (hood != null && hood.loc != this))
            return dot;

        string suffix = Dm.truthy(zipped) ? "hood_t" : "hood";
        object initial_state = Dm.initial(this, (atom p) => p.post_init_icon_state);
        string state = Dm.text("[]_[]", Dm.truthy(initial_state) ? initial_state : Dm.initial(this, (atom p) => p.icon_state), suffix);
        Dm.append(ref dot, mutable_appearance(icon_file, state, -SUIT_LAYER));
        return dot;
    }

    // tg: code/modules/clothing/suits/wintercoats.dm:784
    [DmParams("colors", "new_config", "new_worn_config", "new_inhand_left", "new_inhand_right")]
    public override async DmTask set_greyscale(object colors = null, object new_config = null, params object[] __extra)
    {
        await base.set_greyscale(colors, new_config, __extra);
        if (hood == null)
            return;
        DmList coat_colors = SSgreyscale.ParseColorString(greyscale_colors) as DmList;
        await hood.set_greyscale(coat_colors);
        hood.update_slot_icon();
    }

    // tg: code/modules/clothing/suits/wintercoats.dm:792
    public override async DmTask on_hood_created(obj_item_clothing_head_hooded hood = null)
    {
        await base.on_hood_created(hood);
        DmList coat_colors = SSgreyscale.ParseColorString(greyscale_colors) as DmList;
        await hood.set_greyscale(coat_colors);
    }
}
