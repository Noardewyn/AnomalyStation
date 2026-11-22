// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/dna_infuser/organ_sets/fly_organs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3b, D327): the rejected infusion's fly organs (the all-seeing eyes, the buzzing proboscis, the odd organs with
// their gross names, the stomach that throws up what it eats) and their set bonus, which makes a flyperson. The var blocks are
// generated (D111).

// tg: code/game/machinery/dna_infuser/organ_sets/fly_organs.dm:1  #define FLY_INFUSED_ORGAN_DESC (file-local, its value is the
// generated var blocks' desc); :2  #define FLY_INFUSED_ORGAN_ICON pick(...) (a fresh pick at each use)
public static partial class GlobalProcs
{
    internal static object FLY_INFUSED_ORGAN_ICON() => pick("brain-x-d", "liver-x", "kidneys-x", "spinner-x", "lungs-x", "random_fly_1", "random_fly_2", "random_fly_3", "random_fly_4", "random_fly_5");
}

///bonus of the fly: you... are a flyperson now. sorry.
// tg: code/game/machinery/dna_infuser/organ_sets/fly_organs.dm:5
public partial class datum_status_effect_organ_set_bonus_fly : datum_status_effect_organ_set_bonus
{
    // tg: code/game/machinery/dna_infuser/organ_sets/fly_organs.dm:11
    public override async DmTask<object> enable_bonus(obj_item_organ inserted_organ = null, object visuals_only = null)
    {
        object dot = await base.enable_bonus(inserted_organ, visuals_only);
        if (!Dm.truthy(dot) || !ishuman(owner))
            return dot;
        mob_living_carbon_human new_fly = owner as mob_living_carbon_human;
        if (isflyperson(new_fly))
            return dot;
        // This is ugly as sin, but we're called before the organ finishes inserting into the bodypart
        // so if we swap species directly the bodypart will be replaced and we'll be gone
        // so we need to delay species change until we're fully inserted
        RegisterSignal(inserted_organ, COMSIG_ORGAN_BODYPART_INSERTED, nameof(flyify));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fly_organs.dm:23
    [SignalHandler]
    public virtual void flyify(obj_item_organ source = null, obj_item_bodypart limb = null)
    {
        mob_living_carbon_human new_fly = owner as mob_living_carbon_human;
        // just in case?
        if (isflyperson(new_fly))
            return;
        // needs to be done before the species is set
        UnregisterSignal(source, COMSIG_ORGAN_BODYPART_INSERTED);
        // okay you NEED to be a fly
        to_chat(new_fly, span_danger(I18n.T("Too much fly DNA! Your skin begins to discolor into a horrible black as you become more fly than person!")));
        new_fly.set_species(typeof(datum_species_fly));
    }
}

// tg: code/game/machinery/dna_infuser/organ_sets/fly_organs.dm:35
public partial class obj_item_organ_eyes_fly : obj_item_organ_eyes
{
    // tg: code/game/machinery/dna_infuser/organ_sets/fly_organs.dm:45
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_fly)));
        return dot;
    }
}

// tg: code/game/machinery/dna_infuser/organ_sets/fly_organs.dm:49
public partial class obj_item_organ_tongue_fly : obj_item_organ_tongue
{
    // tg: code/game/machinery/dna_infuser/organ_sets/fly_organs.dm:61  var/static/list/speech_replacements (a type static, rules 10.6)
    private static DmList speech_replacements => TgState.Current.obj_item_organ_tongue_fly__speech_replacements ??= Dm.list(
        Dm.kv(Dm.@new<regex>("z+", "g"), "zzz"),
        Dm.kv(Dm.@new<regex>("Z+", "g"), "ZZZ"),
        Dm.kv("s", "z"),
        Dm.kv("S", "Z"));

    // tg: code/game/machinery/dna_infuser/organ_sets/fly_organs.dm:69
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_speechmod), Dm.named("replacements", speech_replacements), Dm.named("should_modify_speech", Dm.@new<datum_callback>(this, nameof(should_modify_speech)))));
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_fly)));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fly_organs.dm:74
    public override object get_possible_languages()
    {
        return Dm.add(base.get_possible_languages(), typeof(datum_language_buzzwords));
    }
}

// tg: code/game/machinery/dna_infuser/organ_sets/fly_organs.dm:77
public partial class obj_item_organ_heart_fly : obj_item_organ_heart
{
    // tg: code/game/machinery/dna_infuser/organ_sets/fly_organs.dm:80
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        name = odd_organ_name();
        icon_state = Dm.str(FLY_INFUSED_ORGAN_ICON());
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_fly)));
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_blocker)));
        return dot;
    }
}

// tg: code/game/machinery/dna_infuser/organ_sets/fly_organs.dm:87
public partial class obj_item_organ_lungs_fly : obj_item_organ_lungs
{
    // tg: code/game/machinery/dna_infuser/organ_sets/fly_organs.dm:90
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        name = odd_organ_name();
        icon_state = Dm.str(FLY_INFUSED_ORGAN_ICON());
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_fly)));
        return dot;
    }
}

// tg: code/game/machinery/dna_infuser/organ_sets/fly_organs.dm:96
public partial class obj_item_organ_liver_fly : obj_item_organ_liver
{
    // tg: code/game/machinery/dna_infuser/organ_sets/fly_organs.dm:100
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        name = odd_organ_name();
        icon_state = Dm.str(FLY_INFUSED_ORGAN_ICON());
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_fly)));
        return dot;
    }
}

// tg: code/game/machinery/dna_infuser/organ_sets/fly_organs.dm:106
public partial class obj_item_organ_stomach_fly : obj_item_organ_stomach
{
    // tg: code/game/machinery/dna_infuser/organ_sets/fly_organs.dm:109
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        name = odd_organ_name();
        icon_state = Dm.str(FLY_INFUSED_ORGAN_ICON());
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_fly)));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fly_organs.dm:115
    public override async DmTask after_eat(atom edible = null)
    {
        mob_living_carbon body = owner as mob_living_carbon;
        if (body == null)
            throw Dm.CRASH("assertion failed: istype(body)", "code/game/machinery/dna_infuser/organ_sets/fly_organs.dm", 117);
        // we do not lose any nutrition as a fly when vomiting out food
        await body.vomit(vomit_flags: MOB_VOMIT_MESSAGE | MOB_VOMIT_FORCE | MOB_VOMIT_HARM, lost_nutrition: 0, distance: 2, purge_ratio: 0.67f);
        playsound(get_turf(owner), "sound/effects/splat.ogg", 50, TRUE);
        body.visible_message(
            span_danger(I18n.T("[] vomits on the floor!", body)),
            span_userdanger(I18n.T("You throw up on the floor!")));
        await base.after_eat(edible);
    }
}

// tg: code/game/machinery/dna_infuser/organ_sets/fly_organs.dm:127
public partial class obj_item_organ_appendix_fly : obj_item_organ_appendix
{
    // tg: code/game/machinery/dna_infuser/organ_sets/fly_organs.dm:130
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        name = odd_organ_name();
        icon_state = Dm.str(FLY_INFUSED_ORGAN_ICON());
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_fly)));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fly_organs.dm:136
    public override object update_appearance(float? updates = null)
    {
        updates ??= ALL;
        return base.update_appearance(Dm.num(Dm.bitand(updates, ~(UPDATE_NAME | UPDATE_ICON)))); //don't set name or icon thank you
    }
}

//useless organs we throw in just to fuck with surgeons a bit more. they aren't part of a bonus, just the (absolute) state of flies
// tg: code/game/machinery/dna_infuser/organ_sets/fly_organs.dm:140
public partial class obj_item_organ_fly : obj_item_organ
{
    // tg: code/game/machinery/dna_infuser/organ_sets/fly_organs.dm:143
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        name = odd_organ_name();
        icon_state = Dm.str(FLY_INFUSED_ORGAN_ICON());
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/machinery/dna_infuser/organ_sets/fly_organs.dm:61  var/static/list/speech_replacements
    internal DmList obj_item_organ_tongue_fly__speech_replacements;
}
