// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/fish/types/syndicate.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the fish of the syndicate fishing portal setting and the ominous fish case (the emulsijack, the donkfish,
// the jumpercable, the chainsawfish and the armored pike). The var blocks are generated (D111).

// tg: code/modules/fishing/fish/types/syndicate.dm:2
public partial class obj_item_fish_emulsijack : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/syndicate.dm:17
    public override object get_fish_taste()
    {
        return Dm.list(Dm.kv("raw fish", 2), Dm.kv("acid", 1)); //no scales
    }
}

// tg: code/modules/fishing/fish/types/syndicate.dm:20
public partial class obj_item_fish_donkfish : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/syndicate.dm:35
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] swallows [] whole! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
        if (!ishuman(user))
            return TOXLOSS;
        mob_living_carbon_human human_user = (mob_living_carbon_human)user;
        float times = rand(5, 15);
        for (int i = 1; i <= times; i++)
        {
            human_user.dir = Dm.num(pick(GLOB.alldirs));
            await human_user.vomit(vomit_flags: Dm.num(pick(VOMIT_CATEGORY_DEFAULT, VOMIT_CATEGORY_BLOOD)), distance: rand(1, 7));
        }
        qdel(this);
        return TOXLOSS;
    }
}

// tg: code/modules/fishing/fish/types/syndicate.dm:47
public partial class obj_item_fish_jumpercable : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/syndicate.dm:70
    [DmParams("price", "elasticity_percent")]
    public override object get_export_price(object price = null, params object[] __extra)
    {
        //without this, they'd sell for over 6000 each, minimum. That's a lot for a fish that requires no maintance nor partner to farm.
        return Dm.num(base.get_export_price(price, __extra)) * 0.04f;
    }

    // tg: code/modules/fishing/fish/types/syndicate.dm:74
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] hooks both ends of [] to their chest! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
        await electrocute_mob(user as mob_living_carbon, power_source: get_area(this), source: this, siemens_coeff: 1, dist_check: FALSE);
        await tesla_zap(source: user, zap_range: 4, power: electrogenesis_power, cutoff: 1e3f, zap_flags: Dm.num(Dm.bitor(ZAP_LOW_POWER_GEN, ZAP_MOB_DAMAGE)));
        playsound(user, Dm.resource("sound/items/weapons/zapbang.ogg"), 75);
        return OXYLOSS;
    }
}

// tg: code/modules/fishing/fish/types/syndicate.dm:81
public partial class obj_item_fish_chainsawfish : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/syndicate.dm:123
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        return dot;
    }

    // tg: code/modules/fishing/fish/types/syndicate.dm:127
    public override object get_fish_taste()
    {
        return Dm.list(Dm.kv("raw fish", 2.5f), Dm.kv("anger", 1));
    }

    // tg: code/modules/fishing/fish/types/syndicate.dm:130
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (status == FISH_DEAD)
            inhand_icon_state = "chainsawfish_dead";
        else
            inhand_icon_state = "chainsawfish";
        if (HAS_TRAIT(this, TRAIT_WIELDED))
            inhand_icon_state = $"{inhand_icon_state}_wielded";
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/fishing/fish/types/syndicate.dm:139
    public override void get_force_rank()
    {
        switch (w_class)
        {
            case WEIGHT_CLASS_TINY:
                force -= 10;
                attack_speed -= 0.2f * SECONDS;
                demolition_mod -= 0.4f;
                block_chance -= 15;
                armour_penetration -= 10;
                wound_bonus -= 10;
                exposed_wound_bonus -= 10;
                toolspeed += 0.6f;
                break;
            case WEIGHT_CLASS_SMALL:
                force -= 8;
                attack_speed -= 0.1f * SECONDS;
                demolition_mod -= 0.3f;
                block_chance -= 10;
                armour_penetration -= 10;
                wound_bonus -= 10;
                exposed_wound_bonus -= 10;
                toolspeed += 0.4f;
                break;
            case WEIGHT_CLASS_NORMAL:
                force -= 5;
                demolition_mod -= 0.15f;
                block_chance -= 5;
                armour_penetration -= 5;
                wound_bonus -= 5;
                exposed_wound_bonus -= 5;
                toolspeed += 0.2f;
                break;
            case WEIGHT_CLASS_HUGE:
                force += 2;
                attack_speed += 0.2f * SECONDS;
                demolition_mod += 0.15f;
                armour_penetration += 10;
                block_chance += 10;
                wound_bonus += 10;
                exposed_wound_bonus += 5;
                break;
            case WEIGHT_CLASS_GIGANTIC:
                force += 4;
                attack_speed += 0.4f * SECONDS;
                demolition_mod += 0.3f;
                block_chance += 20;
                armour_penetration += 20;
                wound_bonus += 15;
                exposed_wound_bonus += 10;
                toolspeed -= 0.1f;
                break;
        }

        if (status == FISH_DEAD)
        {
            force -= 8 + w_class;
            hitsound = SFX_SWING_HIT;
            block_chance -= 25;
            demolition_mod -= 0.3f;
            armour_penetration -= 15;
            wound_bonus -= 5;
            exposed_wound_bonus -= 5;
            toolspeed += 1;
        }
    }

    // tg: code/modules/fishing/fish/types/syndicate.dm:195
    public override void calculate_fish_force_bonus(object bonus_malus = null)
    {
        base.calculate_fish_force_bonus(bonus_malus);
        float bonus = Dm.num(bonus_malus);
        armour_penetration += bonus * 3;
        wound_bonus += bonus * 2;
        exposed_wound_bonus += bonus * 3;
        block_chance += bonus * 2;
        toolspeed -= bonus * 0.1f;
    }

    // you suicide like a real chainsaw
    // tg: code/modules/fishing/fish/types/syndicate.dm:204
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (status == FISH_DEAD)
        {
            user.visible_message(span_suicide(I18n.T("[] smashes [] into [] neck, destroying [] esophagus! It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_their(), user.p_theyre())));
            playsound(this, Dm.resource("sound/items/weapons/genhit1.ogg"), 100, TRUE);
            return BRUTELOSS;
        }

        user.visible_message(span_suicide(I18n.T("[] begins to tear [] head off with []! It looks like [] trying to commit suicide!", user, user.p_their(), this, user.p_theyre())));
        playsound(this, Dm.resource("sound/items/weapons/chainsawhit.ogg"), 100, TRUE);
        obj_item_bodypart_head myhead = user.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
        if (myhead != null)
            await myhead.dismember();
        return BRUTELOSS;
    }
}

// tg: code/modules/fishing/fish/types/syndicate.dm:217
public partial class obj_item_fish_pike_armored : obj_item_fish_pike
{
    // The first of tg's two get_fish_taste definitions on the armored pike (rules 2.5, __d0): the second (:257) replaces it,
    // so the raw armored pike tastes of cooked fish, as in tg.
    // tg: code/modules/fishing/fish/types/syndicate.dm:254
    public object get_fish_taste__d0()
    {
        return Dm.list(Dm.kv("raw fish", 2.5f), Dm.kv("metal", 1));
    }

    // tg: code/modules/fishing/fish/types/syndicate.dm:257
    public override object get_fish_taste()
    {
        return Dm.list(Dm.kv("cooked fish", 2.5f), Dm.kv("metal", 1));
    }

    // tg: code/modules/fishing/fish/types/syndicate.dm:260
    public override void get_force_rank()
    {
        switch (w_class)
        {
            case WEIGHT_CLASS_TINY:
                force -= 11;
                attack_speed -= 0.4f * SECONDS;
                block_chance -= 25;
                armour_penetration -= 15;
                wound_bonus -= 15;
                exposed_wound_bonus -= 30;
                break;
            case WEIGHT_CLASS_SMALL:
                force -= 6;
                attack_speed -= 0.3f * SECONDS;
                block_chance -= 20;
                armour_penetration -= 10;
                wound_bonus -= 10;
                exposed_wound_bonus -= 25;
                break;
            case WEIGHT_CLASS_NORMAL:
                force -= 4;
                attack_speed -= 0.2f * SECONDS;
                block_chance -= 20;
                armour_penetration -= 5;
                wound_bonus -= 10;
                exposed_wound_bonus -= 15;
                break;
            case WEIGHT_CLASS_HUGE:
                force += 3;
                attack_speed += 0.2f * SECONDS;
                block_chance += 10;
                demolition_mod += 0.1f;
                armour_penetration += 5;
                wound_bonus += 10;
                exposed_wound_bonus += 5;
                break;
            case WEIGHT_CLASS_GIGANTIC:
                force += 7;
                attack_speed += 0.3f * SECONDS;
                demolition_mod += 0.2f;
                block_chance += 20;
                armour_penetration += 10;
                wound_bonus += 15;
                exposed_wound_bonus += 10;
                break;
        }

        if (status == FISH_DEAD)
        {
            force -= 5 + w_class;
            block_chance -= 15;
            armour_penetration -= 10;
            wound_bonus -= 5;
            exposed_wound_bonus -= 15;
        }
    }

    // tg: code/modules/fishing/fish/types/syndicate.dm:307
    public override void calculate_fish_force_bonus(object bonus_malus = null)
    {
        base.calculate_fish_force_bonus(bonus_malus);
        float bonus = Dm.num(bonus_malus);
        armour_penetration += bonus * 3;
        wound_bonus += bonus * 2;
        exposed_wound_bonus += bonus * 4;
        block_chance += bonus * 4;
    }
}
