// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/internal/fangs/_fangs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P3b (D327: the carp's and the stoat's infused fangs): the fangs' bite on the head (_fangs.dm:1-41); gaps SP1 (the felinids): the
// cat's fangs with their feral action and the cybernetic cat fangs (:43-end), the whole file. The var blocks are generated (D111).

// tg: code/modules/surgery/organs/internal/fangs/_fangs.dm:1
public partial class obj_item_organ_fangs : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/fangs/_fangs.dm:24
    public override async DmTask on_bodypart_insert(obj_item_bodypart limb = null)
    {
        obj_item_bodypart head = limb;
        await base.on_bodypart_insert(limb);
        head.unarmed_damage_low += bite_low;
        head.unarmed_damage_high += bite_high;
        head.unarmed_effectiveness += bite_effectiveness;
        head.unarmed_pummeling_bonus += bite_pummeling_bonus;
        head.unarmed_attack_effect = bite_attack_effect;
        head.unarmed_sharpness = bite_sharpness;
    }

    // tg: code/modules/surgery/organs/internal/fangs/_fangs.dm:33
    public override async DmTask on_bodypart_remove(obj_item_bodypart limb = null, object movement_flags = null)
    {
        obj_item_bodypart head = limb;
        await base.on_bodypart_remove(limb, movement_flags);
        head.unarmed_damage_low -= bite_low;
        head.unarmed_damage_high -= bite_high;
        head.unarmed_effectiveness -= bite_effectiveness;
        head.unarmed_pummeling_bonus -= bite_pummeling_bonus;
        head.unarmed_attack_effect = Dm.initial(head, (obj_item_bodypart b) => b.unarmed_attack_effect);
        head.unarmed_sharpness = Dm.initial(head, (obj_item_bodypart b) => b.unarmed_sharpness);
    }
}

// tg: code/modules/surgery/organs/internal/fangs/_fangs.dm:42
public partial class obj_item_organ_fangs_cat : obj_item_organ_fangs
{
    // tg: code/modules/surgery/organs/internal/fangs/_fangs.dm:48
    public override async DmTask on_bodypart_insert(obj_item_bodypart limb = null)
    {
        await base.on_bodypart_insert(limb);
        if (Dm.truthy(feral_mode))
            add_organ_trait(TRAIT_FERAL_BITER);
    }

    // tg: code/modules/surgery/organs/internal/fangs/_fangs.dm:53
    public override async DmTask on_bodypart_remove(obj_item_bodypart limb = null, object movement_flags = null)
    {
        await base.on_bodypart_remove(limb, movement_flags);
        remove_organ_trait(TRAIT_FERAL_BITER);
    }

    // tg: code/modules/surgery/organs/internal/fangs/_fangs.dm:57
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        RegisterSignal(organ_owner, COMSIG_HUMAN_PUNCHED, nameof(nommies));
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/fangs/_fangs.dm:61
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        UnregisterSignal(organ_owner as datum, COMSIG_HUMAN_PUNCHED);
    }

    // tg: code/modules/surgery/organs/internal/fangs/_fangs.dm:65
    public virtual void toggle_feral()
    {
        feral_mode = Dm.truthy(feral_mode) ? FALSE : TRUE;
        if (Dm.truthy(feral_mode))
            add_organ_trait(TRAIT_FERAL_BITER);
        else
            remove_organ_trait(TRAIT_FERAL_BITER);
    }

    // tg: code/modules/surgery/organs/internal/fangs/_fangs.dm:72
    public virtual void nommies(mob_living source = null, mob_living target = null, object damage = null, object attack_type = null, object atk_effect = null, obj_item_bodypart affecting = null, object final_armor_block = null, object limb_sharpness = null)
    {
        if (!ishuman(source))
            return;

        mob_living_carbon_human wittle_kitty_cat_that_bities_real_hard_nya = (mob_living_carbon_human)source;

        if (Dm.num(wittle_kitty_cat_that_bities_real_hard_nya.nutrition) > NUTRITION_LEVEL_WELL_FED)
            return;

        if (wittle_kitty_cat_that_bities_real_hard_nya != target && Dm.eq(atk_effect, ATTACK_EFFECT_BITE) && Dm.truthy(Dm.bitand(target.mob_biotypes, MOB_ORGANIC))) //Good for you. You probably just ate someone alive.
            Waiting.NoSleep(wittle_kitty_cat_that_bities_real_hard_nya.adjust_nutrition(Dm.num(damage) * 0.1f));
    }
}

// tg: code/modules/surgery/organs/internal/fangs/_fangs.dm:106
public partial class obj_item_organ_fangs_cat_cybernetic : obj_item_organ_fangs_cat
{
    // tg: code/modules/surgery/organs/internal/fangs/_fangs.dm:117
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        // tg's var/obj/item/organ/tongue/tongue_to_bite = owner holds the mob, whose apply_organ_damage is no proc: tg's runtime, kept
        object tongue_to_bite = owner;
        if (Dm.truthy(tongue_to_bite))
        {
            to_chat(owner, span_danger(I18n.T("You bite down on your own tongue!")));
            await Dm.Call(tongue_to_bite, "apply_organ_damage", bite_high);
        }
        return dot;
    }
}

