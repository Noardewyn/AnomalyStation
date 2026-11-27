// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/fish/types/saltwater.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the saltwater fish (clownfish and lubefish, cardinalfish, green chromis, firefish, pufferfish, lanternfish,
// stingray, swordfish with its own weapon ranks, squid, monkfish and plaice). The var blocks are generated (D111).

// tg: code/modules/fishing/fish/types/saltwater.dm:1
public partial class obj_item_fish_clownfish : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/saltwater.dm:18
    public override object get_fish_taste()
    {
        return Dm.list(Dm.kv("raw fish", 2), Dm.kv("something funny", 1));
    }
}

// tg: code/modules/fishing/fish/types/saltwater.dm:21
public partial class obj_item_fish_clownfish_lube : obj_item_fish_clownfish
{
    // become lubeman. but you suicide
    // tg: code/modules/fishing/fish/types/saltwater.dm:35
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] covers themselves in []'s residue, then swallows it whole! It looks like [] trying to commit lubide!", user, this, user.p_theyre())));
        await user.AddComponent(typeof(datum_component_slippery), 8 * SECONDS, Dm.bitor(SLIDE, GALOSHES_DONT_HELP));
        await user.AddElement(typeof(datum_element_lube_walking));
        qdel(this);
        return OXYLOSS;
    }
}

// tg: code/modules/fishing/fish/types/saltwater.dm:89
public partial class obj_item_fish_pufferfish : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/saltwater.dm:106
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] bites into [] and starts sucking on it! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
        await DmTask.CompletedTask;
        return TOXLOSS;
    }
}

// tg: code/modules/fishing/fish/types/saltwater.dm:142
public partial class obj_item_fish_swordfish : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/saltwater.dm:186
    public override void get_force_rank()
    {
        switch (w_class)
        {
            case WEIGHT_CLASS_TINY:
                force -= 11;
                attack_speed -= 0.4f * SECONDS;
                block_chance -= 45;
                armour_penetration -= 20;
                wound_bonus -= 15;
                exposed_wound_bonus -= 20;
                break;
            case WEIGHT_CLASS_SMALL:
                force -= 8;
                attack_speed -= 0.3f * SECONDS;
                block_chance -= 30;
                armour_penetration -= 15;
                wound_bonus -= 10;
                exposed_wound_bonus -= 20;
                break;
            case WEIGHT_CLASS_NORMAL:
                force -= 5;
                attack_speed -= 0.2f * SECONDS;
                block_chance -= 20;
                armour_penetration -= 10;
                wound_bonus -= 10;
                exposed_wound_bonus -= 15;
                break;
            case WEIGHT_CLASS_BULKY:
                force -= 3;
                attack_speed -= 0.1f * SECONDS;
                block_chance -= 10;
                armour_penetration -= 5;
                wound_bonus -= 5;
                exposed_wound_bonus -= 10;
                break;
            case WEIGHT_CLASS_GIGANTIC:
                force += 5;
                attack_speed += 0.2f * SECONDS;
                demolition_mod += 0.15f;
                block_chance += 10;
                armour_penetration += 5;
                wound_bonus += 5;
                exposed_wound_bonus += 10;
                break;
        }

        if (status == FISH_DEAD)
        {
            force -= 4 + w_class;
            block_chance -= 25;
            armour_penetration -= 30;
            wound_bonus -= 10;
            exposed_wound_bonus -= 10;
        }
    }

    // tg: code/modules/fishing/fish/types/saltwater.dm:232
    public override void calculate_fish_force_bonus(object bonus_malus = null)
    {
        base.calculate_fish_force_bonus(bonus_malus);
        armour_penetration += Dm.num(bonus_malus) * 5;
        wound_bonus += Dm.num(bonus_malus) * 3;
        exposed_wound_bonus += Dm.num(bonus_malus) * 5;
        block_chance += Dm.num(bonus_malus) * 7;
    }
}

// tg: code/modules/fishing/fish/types/saltwater.dm:239
public partial class obj_item_fish_squid : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/saltwater.dm:255
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] points []'s ink glands at their face and presses INCREDIBLY hard! It looks like [] trying to commit squidcide!", user, this, user.p_theyre())));

        // No head? Bozo.
        obj_item_bodypart head = user.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart;
        if (head == null)
        {
            user.visible_message(span_suicide(I18n.T("[] has no head! The ink goes flying by!", user)));
            return SHAME;
        }

        // get inked.
        user.visible_message(span_warning(I18n.T("[] is inked by []!", user, this)), span_userdanger(I18n.T("You've been inked by []!", this)));
        await user.AddComponent(typeof(datum_component_face_decal_splat),
            Dm.named("color", COLOR_NEARLY_ALL_BLACK),
            Dm.named("memory_type", typeof(datum_memory_witnessed_inking)),
            Dm.named("mood_event_type", typeof(datum_mood_event_inked)));
        playsound(user, SFX_DESECRATION, 50, TRUE);

        if (!HAS_TRAIT(user, TRAIT_STRENGTH) && !HAS_TRAIT(user, TRAIT_HULK))
            return OXYLOSS;

        await head.dismember(silent: FALSE);
        user.visible_message(span_suicide(I18n.T("[]'s head goes FLYING OFF from the overpressurized ink jet!", user)));
        return MANUAL_SUICIDE;
    }

    // tg: code/modules/fishing/fish/types/saltwater.dm:280
    public override object get_fish_taste()
    {
        return Dm.list(Dm.kv("raw mollusk", 2));
    }

    // tg: code/modules/fishing/fish/types/saltwater.dm:283
    public override object get_fish_taste_cooked()
    {
        return Dm.list(Dm.kv("cooked mollusk", 2), Dm.kv("tenderness", 0.5f));
    }
}

// tg: code/modules/fishing/fish/types/saltwater.dm:286
public partial class obj_item_fish_monkfish : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/saltwater.dm:309
    [DmParams("mapload", "apply_qualities")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        name = (string)pick(I18n.T("monkfish"), I18n.T("fishing-frog"), I18n.T("frog-fish"), I18n.T("sea-devil"), I18n.T("goosefish"));
        return dot;
    }
}
