// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/gloves/special.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// J1 (D298): the captain's gloves' and the nitrile gloves' Initialize (with its fishing difficulty element) and the
// coroner's gloves' add_blood_DNA (they never take blood). Gaps CL1 (D333): the cargo gauntlets (the wearer's strong pull,
// datums/components/strong_pull.cs) and the race gloves. The athletic fishing gloves, which fish with the wearer's bare hands
// (their own rod) and work the athletics out while the minigame plays, and their rod that hurts the arms on a big catch. The var
// blocks are generated.

// tg: code/modules/clothing/gloves/special.dm:2
public partial class obj_item_clothing_gloves_cargo_gauntlet : obj_item_clothing_gloves
{
    // tg: code/modules/clothing/gloves/special.dm:13
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_ITEM_EQUIPPED, nameof(on_glove_equip));
        RegisterSignal(this, COMSIG_ITEM_POST_UNEQUIP, nameof(on_glove_unequip));
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), 19));
        return dot;
    }

    // tg: code/modules/clothing/gloves/special.dm:20
    [SignalHandler]
    public virtual void on_glove_equip(datum source = null, mob equipper = null, object slot = null)
    {
        if (!Dm.truthy(Dm.bitand(slot, ITEM_SLOT_GLOVES)))
            return;

        datum_component_strong_pull pull_component = pull_component_weakref?.resolve() as datum_component_strong_pull;
        if (pull_component != null)
        {
            stack_trace(Dm.text("Gloves already have a pull component associated with \\[[]\\] when \\[[]\\] is trying to equip them.", pull_component.parent, equipper), "code/modules/clothing/gloves/special.dm", 28);
            QDEL_NULL(ref pull_component_weakref);
        }

        to_chat(equipper, span_notice(I18n.T("You feel the gauntlets activate as soon as you fit them on, making your pulls stronger!")));

        pull_component_weakref = WEAKREF(Waiting.NoSleep(equipper.AddComponent(typeof(datum_component_strong_pull))) as datum) as datum_weakref;
    }

    // tg: code/modules/clothing/gloves/special.dm:41
    [SignalHandler]
    public virtual void on_glove_unequip(datum source = null, object force = null, atom newloc = null, object no_move = null, object invdrop = null, object silent = null)
    {
        datum_component_strong_pull pull_component = pull_component_weakref?.resolve() as datum_component_strong_pull;

        if (pull_component == null)
            return;

        to_chat(pull_component.parent, span_warning(I18n.T("You have lost the grip power of []!", this)));

        QDEL_NULL(ref pull_component_weakref);
    }
}

// tg: code/modules/clothing/gloves/special.dm:73
public partial class obj_item_clothing_gloves_race : obj_item_clothing_gloves
{
    // tg: code/modules/clothing/gloves/special.dm:80
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), -9));
        return dot;
    }
}

// tg: code/modules/clothing/gloves/special.dm:52
public partial class obj_item_clothing_gloves_rapid : obj_item_clothing_gloves
{
    // tg: code/modules/clothing/gloves/special.dm:59  (the syndicate campaign G2, D316)
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_wearertargeting_punchcooldown)));
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), -9));
        return dot;
    }
}

// tg: code/modules/clothing/gloves/special.dm:85
public partial class obj_item_clothing_gloves_captain : obj_item_clothing_gloves
{
    // tg: code/modules/clothing/gloves/special.dm:100
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), -6));
        return dot;
    }
}

// tg: code/modules/clothing/gloves/special.dm:125
public partial class obj_item_clothing_gloves_latex_nitrile : obj_item_clothing_gloves_latex
{
    // tg: code/modules/clothing/gloves/special.dm:132
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), -6));
        return dot;
    }
}

// tg: code/modules/clothing/gloves/special.dm:137
public partial class obj_item_clothing_gloves_latex_coroner : obj_item_clothing_gloves_latex
{
    // tg: code/modules/clothing/gloves/special.dm:144
    public override async DmTask<object> add_blood_DNA(DmList blood_DNA_to_add = null, DmList diseases = null)
    {
        return FALSE;
    }
}

///A pair of gloves that both allow the user to fish without the need of a held fishing rod and provides athletics experience.
// tg: code/modules/clothing/gloves/special.dm:180
public partial class obj_item_clothing_gloves_fishing : obj_item_clothing_gloves
{
    // tg: code/modules/clothing/gloves/special.dm:188
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_profound_fisher), Dm.@new<obj_item_fishing_rod_mob_fisher_athletic>(this)));
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), -4)); //on top of the extra that you get from the athletics skill.
        return dot;
    }

    // tg: code/modules/clothing/gloves/special.dm:193
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.eq(slot, ITEM_SLOT_GLOVES))
            RegisterSignal(user, COMSIG_MOB_BEGIN_FISHING_MINIGAME, nameof(begin_workout));
    }

    // tg: code/modules/clothing/gloves/special.dm:198
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        UnregisterSignal(user, COMSIG_MOB_BEGIN_FISHING_MINIGAME);
        if (challenge != null)
            stop_workout(user);
        return await base.dropped(user, silent, __extra);
    }

    // tg: code/modules/clothing/gloves/special.dm:204
    [SignalHandler]
    public virtual void begin_workout(datum source = null, datum_fishing_challenge challenge = null)
    {
        RegisterSignal(source, COMSIG_MOB_COMPLETE_FISHING, nameof(stop_workout));
        if (HAS_TRAIT(source, TRAIT_PROFOUND_FISHER)) //Only begin working out if we're fishing with these gloves and not some other fishing rod..
        {
            START_PROCESSING(SSprocessing, this);
            this.challenge = challenge;
        }
    }

    // tg: code/modules/clothing/gloves/special.dm:211
    [SignalHandler]
    public virtual void stop_workout(datum source = null)
    {
        UnregisterSignal(source, COMSIG_MOB_COMPLETE_FISHING);
        challenge = null;
        STOP_PROCESSING(SSprocessing, this);
    }

    // tg: code/modules/clothing/gloves/special.dm:217
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        mob_living wearer = (mob_living)loc;
        float stamina_exhaustion = 2 + challenge.difficulty * 0.02f;
        bool is_heavy_gravity = Dm.num(wearer.has_gravity()) > STANDARD_GRAVITY;
        obj_item_organ_cyberimp_chest_spine potential_spine = wearer.get_organ_slot(ORGAN_SLOT_SPINE) as obj_item_organ_cyberimp_chest_spine;
        if (potential_spine != null)
            stamina_exhaustion *= potential_spine.athletics_boost_multiplier;
        if (HAS_TRAIT(wearer, TRAIT_STRENGTH))
            stamina_exhaustion *= 0.5f;

        float experience = 0.3f + challenge.difficulty * 0.003f;
        if (is_heavy_gravity)
        {
            stamina_exhaustion *= 1.5f;
            experience *= 2;
        }

        Waiting.NoSleep(wearer.adjust_stamina_loss(stamina_exhaustion));
        wearer.mind?.adjust_experience(typeof(datum_skill_athletics), experience);
        Waiting.NoSleep(wearer.apply_status_effect(typeof(datum_status_effect_exercised)));
        return null;
    }
}

///The internal fishing rod of the athletic fishing gloves. The more athletic you're, the easier the minigame will be.
// tg: code/modules/clothing/gloves/special.dm:237
public partial class obj_item_fishing_rod_mob_fisher_athletic : obj_item_fishing_rod_mob_fisher
{
    // tg: code/modules/clothing/gloves/special.dm:248
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_FISHING_ROD_CAUGHT_FISH, nameof(noodling_is_dangerous));
        return dot;
    }

    // tg: code/modules/clothing/gloves/special.dm:252
    public override object get_fishing_overlays()
    {
        return Dm.list();
    }

    // tg: code/modules/clothing/gloves/special.dm:255
    public override async DmTask hook_hit(atom atom_hit_by_hook_projectile = null, mob user = null)
    {
        difficulty_modifier = -3 * (Dm.num(user.mind?.get_skill_level(typeof(datum_skill_athletics))) - 1);
        await base.hook_hit(atom_hit_by_hook_projectile, user);
    }

    // tg: code/modules/clothing/gloves/special.dm:259
    [SignalHandler]
    public virtual void noodling_is_dangerous(datum source = null, atom_movable reward = null, mob_living user = null)
    {
        if (!isfish(reward))
            return;
        float damage = 0;
        obj_item_fish fishe = (obj_item_fish)reward;
        switch (fishe.w_class)
        {
            case WEIGHT_CLASS_BULKY:
                damage = 10;
                break;
            case WEIGHT_CLASS_HUGE:
                damage = 14;
                break;
            case WEIGHT_CLASS_GIGANTIC:
                damage = 18;
                break;
        }
        if (damage == 0 && Dm.num(fishe.weight) >= 2000)
            damage = 5;
        damage = round(damage * Dm.num(fishe.weight) * 0.0005f);
        if (damage != 0)
        {
            object body_zone = pick(BODY_ZONE_R_ARM, BODY_ZONE_L_ARM);
            Waiting.NoSleep(user.apply_damage(damage, BRUTE, body_zone, Dm.num(user.run_armor_check(body_zone, MELEE))));
            playsound(this, Dm.resource("sound/items/weapons/bite.ogg"), damage * 2, TRUE);
        }
    }
}
