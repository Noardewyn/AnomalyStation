// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/crossbreeding/charged.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the charged extracts, filled with 10u of plasma and squeezed in hand for a stronger form of their colour's
// reaction. The jellypeople and the golems are not ported (not-translated.md section 243): the green extract offers the slime
// subspecies and the black one rolls among the species a slime extract may give, but a species not ported yet leaves the body as it
// is, and the adamantine extract's golem shell stands inert. The var blocks are generated (D111).

/*
Charged extracts:
	Have a unique, effect when filled with
	10u plasma and activated in-hand, related to their
	normal extract effect.
*/
// tg: code/modules/research/xenobiology/crossbreeding/charged.dm:8
public partial class obj_item_slimecross_charged : obj_item_slimecross
{
    // tg: code/modules/research/xenobiology/crossbreeding/charged.dm:13
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(10, Dm.bitor(INJECTABLE, DRAWABLE));
        return dot;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/charged.dm:17
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        datum_reagents holder = (datum_reagents)reagents;
        if (!Dm.truthy(holder.has_reagent(typeof(datum_reagent_toxin_plasma), 10)))
        {
            to_chat(user, span_warning(I18n.T("This extract needs to be full of plasma to activate!")));
            return null;
        }
        await holder.remove_reagent(typeof(datum_reagent_toxin_plasma), 10);
        to_chat(user, span_notice(I18n.T("You squeeze the extract, and it absorbs the plasma!")));
        playsound(this, "sound/effects/bubbles/bubbles.ogg", 50, TRUE);
        playsound(this, "sound/effects/light_flicker.ogg", 50, TRUE);
        await do_effect(user);
        return null;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/charged.dm:27
    public virtual async DmTask do_effect(mob user = null) //If, for whatever reason, you don't want to delete the extract, don't do ..()
    {
        await DmTask.CompletedTask;
        qdel(this);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/charged.dm:32
public partial class obj_item_slimecross_charged_grey : obj_item_slimecross_charged
{
    // tg: code/modules/research/xenobiology/crossbreeding/charged.dm:35
    public override async DmTask do_effect(mob user = null)
    {
        Dm.@new<obj_item_slimepotion_slime_reviver>(get_turf(user));
        user.visible_message(span_notice(I18n.T("[] distills into a potion!", this)));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/charged.dm:41
public partial class obj_item_slimecross_charged_orange : obj_item_slimecross_charged
{
    // tg: code/modules/research/xenobiology/crossbreeding/charged.dm:44
    public override async DmTask do_effect(mob user = null)
    {
        turf targetturf = get_turf(user);
        foreach (turf turf in Dm.each_cast<turf>(RANGE_TURFS(5, targetturf)))
        {
            if (Dm.locate<obj_effect_hotspot>(turf) == null)
                Dm.@new<obj_effect_hotspot>(turf);
        }
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/charged.dm:52
public partial class obj_item_slimecross_charged_purple : obj_item_slimecross_charged
{
    // tg: code/modules/research/xenobiology/crossbreeding/charged.dm:55
    public override async DmTask do_effect(mob user = null)
    {
        Dm.@new<obj_item_slimecrossbeaker_omnizine>(get_turf(user));
        user.visible_message(span_notice(I18n.T("[] sparks, and floods with a regenerative solution!", this)));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/charged.dm:61
public partial class obj_item_slimecross_charged_blue : obj_item_slimecross_charged
{
    // tg: code/modules/research/xenobiology/crossbreeding/charged.dm:64
    public override async DmTask do_effect(mob user = null)
    {
        Dm.@new<obj_item_slimepotion_slime_chargedstabilizer>(get_turf(user));
        user.visible_message(span_notice(I18n.T("[] distills into a potion!", this)));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/charged.dm:70
public partial class obj_item_slimecross_charged_metal : obj_item_slimecross_charged
{
    // tg: code/modules/research/xenobiology/crossbreeding/charged.dm:73
    public override async DmTask do_effect(mob user = null)
    {
        Dm.@new<obj_item_stack_sheet_iron>(get_turf(user), 25);
        Dm.@new<obj_item_stack_sheet_plasteel>(get_turf(user), 10);
        user.visible_message(span_notice(I18n.T("[] grows into a plethora of metals!", this)));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/charged.dm:80
public partial class obj_item_slimecross_charged_yellow : obj_item_slimecross_charged
{
    // tg: code/modules/research/xenobiology/crossbreeding/charged.dm:83
    public override async DmTask do_effect(mob user = null)
    {
        Dm.@new<obj_item_stock_parts_power_store_cell_high_slime_hypercharged>(get_turf(user));
        user.visible_message(span_notice(I18n.T("[] sparks violently, and swells with electric power!", this)));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/charged.dm:89
public partial class obj_item_slimecross_charged_darkpurple : obj_item_slimecross_charged
{
    // tg: code/modules/research/xenobiology/crossbreeding/charged.dm:92
    public override async DmTask do_effect(mob user = null)
    {
        Dm.@new<obj_item_stack_sheet_mineral_plasma>(get_turf(user), 10);
        user.visible_message(span_notice(I18n.T("[] produces a large amount of plasma!", this)));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/charged.dm:98
public partial class obj_item_slimecross_charged_darkblue : obj_item_slimecross_charged
{
    // tg: code/modules/research/xenobiology/crossbreeding/charged.dm:101
    public override async DmTask do_effect(mob user = null)
    {
        Dm.@new<obj_item_slimepotion_spaceproof>(get_turf(user));
        user.visible_message(span_notice(I18n.T("[] distills into a potion!", this)));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/charged.dm:107
public partial class obj_item_slimecross_charged_silver : obj_item_slimecross_charged
{
    // tg: code/modules/research/xenobiology/crossbreeding/charged.dm:110
    public override async DmTask do_effect(mob user = null)
    {
        Dm.@new<obj_item_food_cake_slimecake>(get_turf(user));
        for (int i = 1; i <= 10; i++)
        {
            object drink_type = get_random_drink();
            Dm.@new((System.Type)drink_type, get_turf(user));
        }
        user.visible_message(span_notice(I18n.T("[] produces a party's worth of cake and drinks!", this)));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/charged.dm:119
public partial class obj_item_slimecross_charged_bluespace : obj_item_slimecross_charged
{
    // tg: code/modules/research/xenobiology/crossbreeding/charged.dm:122
    public override async DmTask do_effect(mob user = null)
    {
        Dm.@new<obj_item_stack_sheet_bluespace_crystal>(get_turf(user), 10);
        user.visible_message(span_notice(I18n.T("[] produces several sheets of polycrystal!", this)));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/charged.dm:128
public partial class obj_item_slimecross_charged_sepia : obj_item_slimecross_charged
{
    // tg: code/modules/research/xenobiology/crossbreeding/charged.dm:131
    public override async DmTask do_effect(mob user = null)
    {
        Dm.@new<obj_item_camera_spooky>(get_turf(user));
        user.visible_message(span_notice(I18n.T("[] flickers in a strange, ethereal manner, and produces a camera!", this)));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/charged.dm:137
public partial class obj_item_slimecross_charged_cerulean : obj_item_slimecross_charged
{
    // tg: code/modules/research/xenobiology/crossbreeding/charged.dm:140
    public override async DmTask do_effect(mob user = null)
    {
        Dm.@new<obj_item_slimepotion_enhancer_max>(get_turf(user));
        user.visible_message(span_notice(I18n.T("[] distills into a potion!", this)));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/charged.dm:146
public partial class obj_item_slimecross_charged_pyrite : obj_item_slimecross_charged
{
    // tg: code/modules/research/xenobiology/crossbreeding/charged.dm:149
    public override async DmTask do_effect(mob user = null)
    {
        Dm.@new<obj_item_stack_sheet_mineral_bananium>(get_turf(user), 10);
        user.visible_message(span_warning(I18n.T("[] solidifies with a horrifying banana stench!", this)));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/charged.dm:155
public partial class obj_item_slimecross_charged_red : obj_item_slimecross_charged
{
    // tg: code/modules/research/xenobiology/crossbreeding/charged.dm:158
    public override async DmTask do_effect(mob user = null)
    {
        Dm.@new<obj_item_slimepotion_lavaproof>(get_turf(user));
        user.visible_message(span_notice(I18n.T("[] distills into a potion!", this)));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/charged.dm:164
public partial class obj_item_slimecross_charged_green : obj_item_slimecross_charged
{
    // tg: code/modules/research/xenobiology/crossbreeding/charged.dm:167
    public override async DmTask do_effect(mob user = null)
    {
        mob_living_carbon_human human_user = user as mob_living_carbon_human;
        if (!(human_user is mob_living_carbon_human))
        {
            to_chat(user, span_warning(I18n.T("You must be a humanoid to use this!")));
            return;
        }
        DmList choice_list = Dm.list();
        foreach (Type species_type in Dm.each_cast<Type>(subtypesof(typeof(datum_species_jelly))))
            choice_list[Dm.initial(species_type, (datum_species s) => s.name)] = species_type;
        object racechoice = await tgui_input_list(human_user, I18n.T("Choose your slime subspecies"), I18n.T("Slime Selection"), sort_list(choice_list) as DmList);
        if (racechoice == null)
        {
            to_chat(user, span_notice(I18n.T("You decide not to become a slime for now.")));
            return;
        }
        if (!Dm.truthy(await user.can_perform_action(this)))
            return;
        Type chosen = choice_list[racechoice] as Type;
        // GAP(SP3): the jellypeople are inert (not-translated.md section 243): tg turns the user into the chosen subspecies, the port
        // leaves the body as it is and keeps the extract (with the black extract's fizzle) until the species is ported.
        if (chosen != null && InertSeam.IsInertType(chosen))
        {
            visible_message(span_notice(I18n.T("[] fizzes uselessly.", this)));
            return;
        }
        human_user.set_species(chosen, icon_update: 1);
        human_user.visible_message(span_warning(I18n.T("[] suddenly shifts form as [] dissolves into [] skin!", human_user, this, human_user.p_their())));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/charged.dm:186
public partial class obj_item_slimecross_charged_pink : obj_item_slimecross_charged
{
    // tg: code/modules/research/xenobiology/crossbreeding/charged.dm:189
    public override async DmTask do_effect(mob user = null)
    {
        Dm.@new<obj_item_slimepotion_lovepotion>(get_turf(user));
        user.visible_message(span_notice(I18n.T("[] distills into a potion!", this)));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/charged.dm:195
public partial class obj_item_slimecross_charged_gold : obj_item_slimecross_charged
{
    // tg: code/modules/research/xenobiology/crossbreeding/charged.dm:200
    public override async DmTask do_effect(mob user = null)
    {
        await DmTask.CompletedTask;
        user.visible_message(span_warning(I18n.T("[] starts shuddering violently!", this)));
        _addtimer(Dm.@new<datum_callback>(this, nameof(startTimer)), 5 * SECONDS, file: "code/modules/research/xenobiology/crossbreeding/charged.dm", line: 201);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/charged.dm:204
    public virtual void startTimer()
    {
        START_PROCESSING(SSobj, this);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/charged.dm:207
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        visible_message(span_warning(I18n.T("[] lets off a spark, and produces a living creature!", this)));
        Dm.@new<obj_effect_particle_effect_sparks>(get_turf(this));
        playsound(get_turf(this), SFX_SPARKS, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        create_random_mob(get_turf(this), HOSTILE_SPAWN);
        spawned++;
        if (spawned >= max_spawn)
        {
            visible_message(span_warning(I18n.T("[] collapses into a puddle of goo.", this)));
            qdel(this);
        }
        return null;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/charged.dm:217
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/charged.dm:222
public partial class obj_item_slimecross_charged_oil : obj_item_slimecross_charged
{
    // tg: code/modules/research/xenobiology/crossbreeding/charged.dm:225
    public override async DmTask do_effect(mob user = null)
    {
        await DmTask.CompletedTask;
        user.visible_message(span_danger(I18n.T("[] begins to shake with rapidly increasing force!", this)));
        _addtimer(Dm.@new<datum_callback>(this, nameof(boom)), 5 * SECONDS, file: "code/modules/research/xenobiology/crossbreeding/charged.dm", line: 227);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/charged.dm:229
    public virtual async DmTask boom()
    {
        await explosion(this, devastation_range: 2, heavy_impact_range: 3, light_impact_range: 4, explosion_cause: this); //Much smaller effect than normal oils, but devastatingly strong where it does hit.
        qdel(this);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/charged.dm:234
public partial class obj_item_slimecross_charged_black : obj_item_slimecross_charged
{
    // tg: code/modules/research/xenobiology/crossbreeding/charged.dm:237
    public override async DmTask do_effect(mob user = null)
    {
        mob_living_carbon_human experiment_subject = user as mob_living_carbon_human;
        if (!(experiment_subject is mob_living_carbon_human))
        {
            balloon_alert(experiment_subject, I18n.T("incompatible biology!"));
            return;
        }
        DmList allowed_species = Dm.list();
        foreach (object stype in Dm.each(subtypesof(typeof(datum_species))))
        {
            Type try_species = (Type)stype;
            if (Dm.truthy(Dm.bitand(Dm.initial(try_species, (datum_species s) => s.changesource_flags), SLIME_EXTRACT)))
                Dm.append(ref allowed_species, stype);
        }

        Type changed = pick(allowed_species) as Type;
        // GAP(SP3): a species not ported yet is inert (not-translated.md section 243): tg turns the subject into it, the port takes it
        // for the empty roll below (the extract fizzes and stays) until the species is ported.
        if (changed == null || InertSeam.IsInertType(changed))
        {
            visible_message(span_notice(I18n.T("[] fizzes uselessly.", this)));
            return;
        }
        experiment_subject.set_species(changed, icon_update: TRUE);
        to_chat(experiment_subject, span_danger(I18n.T("You feel very different!")));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/charged.dm:257
public partial class obj_item_slimecross_charged_lightpink : obj_item_slimecross_charged
{
    // tg: code/modules/research/xenobiology/crossbreeding/charged.dm:260
    public override async DmTask do_effect(mob user = null)
    {
        Dm.@new<obj_item_slimepotion_peacepotion>(get_turf(user));
        user.visible_message(span_notice(I18n.T("[] distills into a potion!", this)));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/charged.dm:266
public partial class obj_item_slimecross_charged_adamantine : obj_item_slimecross_charged
{
    // tg: code/modules/research/xenobiology/crossbreeding/charged.dm:269
    public override async DmTask do_effect(mob user = null)
    {
        user.visible_message(span_notice(I18n.T("[] produces a fully formed golem shell!", this)));
        // the golem shell and the golem species are inert until the golems are ported (not-translated.md section 243)
        Dm.@new<obj_effect_mob_spawn_ghost_role_human_golem_servant>(get_turf(this), typeof(datum_species_golem), user);
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/charged.dm:275
public partial class obj_item_slimecross_charged_rainbow : obj_item_slimecross_charged
{
    // tg: code/modules/research/xenobiology/crossbreeding/charged.dm:278
    public override async DmTask do_effect(mob user = null)
    {
        user.visible_message(span_warning(I18n.T("[] swells and splits into three new slimes!", this)));
        for (int i = 1; i <= 3; i++)
            Dm.@new<mob_living_basic_slime_random>(get_turf(user));
        await base.do_effect(user);
    }
}
