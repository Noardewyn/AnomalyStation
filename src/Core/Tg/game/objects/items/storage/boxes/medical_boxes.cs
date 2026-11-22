// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/boxes/medical_boxes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P3: the PopulateContents of Meta's containers in this file (D133), translated with the help of m4/p3w/popgen2.py
// (the pure `new /path(src)` bodies, their loops, prob and switch); the file's other procs are their milestones'.

// tg: code/game/objects/items/storage/boxes/medical_boxes.dm:156
public partial class obj_item_storage_box_bandages : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:156
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 5; i++) // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:157
        {
            Dm.@new<obj_item_stack_medical_bandage>(this); // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:158
        }
    }

    // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:160 (M8 slice C: the paramedics' box of bandages)
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        float sw__1 = Dm.length(contents);
        if (sw__1 == 5)
            icon_state = Dm.text("[]_f", base_icon_state);
        else if (sw__1 >= 3 && sw__1 <= 4)
            icon_state = Dm.text("[]_almostfull", base_icon_state);
        else if (sw__1 >= 1 && sw__1 <= 2)
            icon_state = Dm.text("[]_almostempty", base_icon_state);
        else if (sw__1 == 0)
            icon_state = (string)base_icon_state;
        return dot;
    }
}

// tg: code/game/objects/items/storage/boxes/medical_boxes.dm:44
public partial class obj_item_storage_box_beakers : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:44
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++) // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:45
        {
            Dm.@new<obj_item_reagent_containers_cup_beaker>(this); // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:46
        }
    }
}

// tg: code/game/objects/items/storage/boxes/medical_boxes.dm:101
public partial class obj_item_storage_box_bodybags : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:101
    public override async DmTask PopulateContents()
    {
        await base.PopulateContents();
        for (float i = 1; i <= 7; i++) // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:103
        {
            Dm.@new<obj_item_bodybag>(this); // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:104
        }
    }
}

// tg: code/game/objects/items/storage/boxes/medical_boxes.dm:111
public partial class obj_item_storage_box_pillbottles : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:111
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++) // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:112
        {
            Dm.@new<obj_item_storage_pill_bottle>(this); // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:113
        }
    }
}

// tg: code/game/objects/items/storage/boxes/medical_boxes.dm:8
public partial class obj_item_storage_box_syringes : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:8
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++) // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:9
        {
            Dm.@new<obj_item_reagent_containers_syringe>(this); // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:10
        }
    }
}

// tg: code/game/objects/items/storage/boxes/medical_boxes.dm:76  (gaps J5, D298)
public partial class obj_item_storage_box_medigels : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:81
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++)
            Dm.@new<obj_item_reagent_containers_medigel>(this);
    }
}

// The triage cards and their stack (gaps S3, D298: the paperwork made paper live).
// tg: code/game/objects/items/storage/boxes/medical_boxes.dm:191
public partial class obj_item_paper_triage : obj_item_paper
{
    // tg: medical_boxes.dm:186-189 (file-local #defines)
    private const string SEVERITY_MINIMAL = "minimal";
    private const string SEVERITY_DELAYED = "delayed";
    private const string SEVERITY_IMMEDIATE = "immediate";
    private const string SEVERITY_DEAD = "expectant / deceased";

    // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:197
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        name = I18n.T("\"[]\" []", severity, name);
        default_raw_text = Dm.text("\n\t\t<center>\n\t\t\t<h2>TAG - TRIAGE</h2>\n\t\t</center>\n\t\t<b>Name:</b><br>\n\t\t[______________________________]<br>\n\t\t<center>\n\t\t\t<h3>- [] -<h3>\n\t\t\t<h4>[]</h4>\n\t\t</center>\n\t\t<b>Injuries:</b><br>\n\t\t[________________________________________]<br>\n\t\t[________________________________________]<br>\n\t\t[________________________________________]<br>\n\t\t[________________________________________]<br>\n\t\t[________________________________________]<br>\n\t", uppertext(severity), severity_to_subtitle());
        return base.Initialize(mapload, __extra);
    }

    // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:218
    public virtual object severity_to_subtitle()
    {
        switch (severity)
        {
            case SEVERITY_DEAD:
                return "NO RESPIRATION";
            case SEVERITY_IMMEDIATE:
                return "LIFE THREATENING";
            case SEVERITY_DELAYED:
                return "SERIOUS INJURIES - NOT LIFE THREATENING";
            case SEVERITY_MINIMAL:
                return "MINOR INJURIES - WALKING WOUNDED";
        }
        return "UNKNOWN";
    }

    // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:230
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        switch (severity)
        {
            case SEVERITY_DEAD:
                Dm.append(ref dot, span_notice(I18n.T("This card indicates that the patient is deceased or is not expected to survive.")));
                break;
            case SEVERITY_IMMEDIATE:
                Dm.append(ref dot, span_notice(I18n.T("This card indicates that the patient is in a critical condition and requires immediate attention.")));
                break;
            case SEVERITY_DELAYED:
                Dm.append(ref dot, span_notice(I18n.T("This card indicates that the patient is seriously injured, but not in immediate danger.")));
                break;
            case SEVERITY_MINIMAL:
                Dm.append(ref dot, span_notice(I18n.T("This card indicates that the patient is only slightly injured.")));
                break;
        }

        Dm.append(ref dot, span_smallnoticeital(I18n.T("There is a guide to triage on the back of the card, if you <i>look closer</i>.")));
        return dot;
    }

    // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:244
    public override async DmTask<object> examine_more(mob user = null)
    {
        object dot = await base.examine_more(user);
        Dm.append(ref dot, span_notice(I18n.T("<i>The back of [] has a guide to performing triage:</i>", this)));
        Dm.append(ref dot, I18n.T("&bull; \"Is the victim walking and can respond to simple orders?\" If so, mark as <b>minimal</b>."));
        Dm.append(ref dot, I18n.T("&bull; \"Has the victim stopped breathing entirely (without even gasping for air)?\" If so, mark as <b>expectant / deceased</b>."));
        Dm.append(ref dot, I18n.T("&bull; \"Is the victim bleeding, failing to follow simple commands, lacking a pulse, having difficulties breathing?\" If so, mark as <b>immediate</b>."));
        Dm.append(ref dot, I18n.T("&bull; Otherwise, mark as <b>delayed</b>."));
        return dot;
    }

    // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:252
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isliving(interacting_with))
            return NONE;

        float? px = text2num(LAZYACCESS(modifiers, ICON_X));
        float? py = text2num(LAZYACCESS(modifiers, ICON_Y));

        if (px == null || py == null)
            return NONE;

        await user.do_attack_animation(interacting_with, used_item: this);
        interacting_with.balloon_alert(user, I18n.T("card attached"));
        await interacting_with.AddComponent(typeof(datum_component_sticker), this, get_dir(interacting_with, this), px, py);
        return ITEM_INTERACT_SUCCESS;
    }
}

// Paper bin to store a lot of triage cards. Goes away when empty.
// tg: code/game/objects/items/storage/boxes/medical_boxes.dm:283
public partial class obj_item_paper_bin_triage : obj_item_paper_bin
{
    // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:298
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        total_paper -= round(Dm.num(exposed_volume) / 25, 1);
        if (total_paper <= 0)
            qdel(this);
        else
            update_appearance();
        return null;
    }

    // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:305
    public override async DmTask dump_contents(params object[] __extra)
    {
        await base.dump_contents(__extra);
        if (total_paper <= 0)
            qdel(this);
    }

    // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:310
    public override void remove_paper(float? amount = null)
    {
        base.remove_paper(amount);
        if (total_paper <= 0)
            qdel(this);
    }

    // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:315
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (total_paper <= 0 && !QDELING(this))
            qdel(this);
        return dot;
    }
}

// Gaps SR1 (D333): the file's other boxes (variety syringes, medipens, the big, bluespace and variety beakers, the DNA injectors,
// the plumbing and evil meds kits, the triage cards).

// tg: code/game/objects/items/storage/boxes/medical_boxes.dm:15
public partial class obj_item_storage_box_syringes_variety : obj_item_storage_box_syringes
{
    // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:15
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_reagent_containers_syringe>(this);
        Dm.@new<obj_item_reagent_containers_syringe_lethal>(this);
        Dm.@new<obj_item_reagent_containers_syringe_piercing>(this);
        Dm.@new<obj_item_reagent_containers_syringe_bluespace>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/medical_boxes.dm:26
public partial class obj_item_storage_box_medipens : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:26
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++)
        {
            Dm.@new<obj_item_reagent_containers_hypospray_medipen>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/medical_boxes.dm:35
public partial class obj_item_storage_box_medipens_utility : obj_item_storage_box_medipens
{
    // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:35
    public override async DmTask PopulateContents()
    {
        await base.PopulateContents(); // includes regular medipens.
        for (float i = 1; i <= 5; i++)
        {
            Dm.@new<obj_item_reagent_containers_hypospray_medipen_stimpack>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/medical_boxes.dm:52
public partial class obj_item_storage_box_beakers_big : obj_item_storage_box_beakers
{
    // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:52
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++)
        {
            Dm.@new<obj_item_reagent_containers_cup_beaker_large>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/medical_boxes.dm:60
public partial class obj_item_storage_box_beakers_bluespace : obj_item_storage_box_beakers
{
    // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:60
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++)
        {
            Dm.@new<obj_item_reagent_containers_cup_beaker_bluespace>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/medical_boxes.dm:67
public partial class obj_item_storage_box_beakers_variety : obj_item_storage_box_beakers
{
    // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:67
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_reagent_containers_cup_beaker>(this);
        Dm.@new<obj_item_reagent_containers_cup_beaker_bluespace>(this);
        Dm.@new<obj_item_reagent_containers_cup_beaker_large>(this);
        Dm.@new<obj_item_reagent_containers_cup_beaker_meta>(this);
        Dm.@new<obj_item_reagent_containers_cup_beaker_noreact>(this);
        Dm.@new<obj_item_reagent_containers_cup_beaker_plastic>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/medical_boxes.dm:89
public partial class obj_item_storage_box_injectors : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:89
    public override async DmTask PopulateContents()
    {
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_dnainjector_h2m), 3),
            Dm.kv(typeof(obj_item_dnainjector_m2h), 3));
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/boxes/medical_boxes.dm:115
public partial class obj_item_storage_box_plumbing : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:115
    public override async DmTask PopulateContents()
    {
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_stock_parts_water_recycler), 2),
            Dm.kv(typeof(obj_item_stack_ducts_fifty), 1),
            Dm.kv(typeof(obj_item_stack_sheet_iron_ten), 1));
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/boxes/medical_boxes.dm:129
public partial class obj_item_storage_box_evilmeds : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:129
    public override async DmTask PopulateContents()
    {
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_reagent_containers_cup_beaker_meta_omnizine), 1),
            Dm.kv(typeof(obj_item_reagent_containers_cup_beaker_meta_sal_acid), 1),
            Dm.kv(typeof(obj_item_reagent_containers_cup_beaker_meta_oxandrolone), 1),
            Dm.kv(typeof(obj_item_reagent_containers_cup_beaker_meta_pen_acid), 1),
            Dm.kv(typeof(obj_item_reagent_containers_cup_beaker_meta_atropine), 1),
            Dm.kv(typeof(obj_item_reagent_containers_cup_beaker_meta_salbutamol), 1),
            Dm.kv(typeof(obj_item_reagent_containers_cup_beaker_meta_rezadone), 1));
        generate_items_inside(items_inside, this);
    }
}

// tg: code/game/objects/items/storage/boxes/medical_boxes.dm:178
public partial class obj_item_storage_box_triage_cards : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/medical_boxes.dm:178
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_paper_bin_triage_minor>(this);
        Dm.@new<obj_item_paper_bin_triage_major>(this);
        Dm.@new<obj_item_paper_bin_triage_critical>(this);
        Dm.@new<obj_item_paper_bin_triage_dead>(this);
        Dm.@new<obj_item_pen>(this);
    }
}
