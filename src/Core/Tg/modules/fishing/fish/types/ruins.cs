// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/fish/types/ruins.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the fish of the ruins (the mastodon skeleton fish, and the soul fish and the skin crab of the cursed
// spring). The var blocks are generated (D111).

// tg: code/modules/fishing/fish/types/ruins.dm:2
public partial class obj_item_fish_mastodon : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/ruins.dm:29
    [DmParams("mapload", "apply_qualities")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_FISH_MADE_OF_BONE, INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/fishing/fish/types/ruins.dm:33
    public override object fish_grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_bone_dust), 5), Dm.kv(typeof(datum_reagent_consumable_liquidgibs), 5));
    }

    // tg: code/modules/fishing/fish/types/ruins.dm:36
    [DmParams("weight_val")]
    public override async DmTask make_edible(params object[] __extra)
    {
        await DmTask.CompletedTask; //it's all bones and gibs.
    }

    // tg: code/modules/fishing/fish/types/ruins.dm:39
    [DmParams("price", "elasticity_percent")]
    public override object get_export_price(object price = null, params object[] __extra)
    {
        return Dm.num(base.get_export_price(price, __extra)) * 1.2f; //This should push its soft-capped (it's pretty big) price a bit above the rest
    }

    // tg: code/modules/fishing/fish/types/ruins.dm:42
    public override async DmTask<object> get_health_warnings(mob user = null, float? always_deep = null)
    {
        await DmTask.CompletedTask;
        return Dm.list(span_deadsay(I18n.T("It's bones.")));
    }

    // tg: code/modules/fishing/fish/types/ruins.dm:45
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        // Kept as tg has it: the message misses a bracket, so it prints "user.p_theyre()]" literally.
        user.visible_message(span_suicide(I18n.T("[] swallows [] whole (somehow)! It looks like user.p_theyre()] trying to commit suicide!", user, this)));
        await forceMove(user);
        await user.update_transform(1.25f); // become BIG from eating BIG fish
        _addtimer(Dm.@new<datum_callback>(this, nameof(skeleton_appears), user), 2 * SECONDS, file: "code/modules/fishing/fish/types/ruins.dm", line: 49);
        return MANUAL_SUICIDE_NONLETHAL; // chance not to die
    }

    // tg: code/modules/fishing/fish/types/ruins.dm:52
    public virtual async DmTask skeleton_appears(mob_living user = null)
    {
        user.visible_message(span_warning(I18n.T("[]'s skin melts off!", user)), span_boldwarning(I18n.T("Your skin melts off!")));
        await user.spawn_gibs();
        await user.drop_everything(del_on_drop: FALSE, force: FALSE, del_if_nodrop: FALSE);
        user.set_species(typeof(datum_species_skeleton));
        await user.say("AAAAAAAAAAAAHHHHHHHHHH!!!!!!!!!!!!!!", forced: "mastodon fish suicide");
        await user.AddComponent(typeof(datum_component_omen)); // the curse of the fish
        if (prob(75)) // rare so less likely (the curse keeps you alive)
        {
            _addtimer(Dm.@new<datum_callback>(user, nameof(mob_living.death)), 3 * SECONDS, file: "code/modules/fishing/fish/types/ruins.dm", line: 60);
            user.set_suicide(TRUE);
        }
        qdel(this);
    }
}

///From the cursed spring
// tg: code/modules/fishing/fish/types/ruins.dm:65
public partial class obj_item_fish_soul : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/ruins.dm:89
    public override object get_food_types()
    {
        return Dm.bitor(Dm.bitor(MEAT, RAW), GORE); //Not-so-quite-seafood
    }

    // tg: code/modules/fishing/fish/types/ruins.dm:92
    public override object get_fish_taste()
    {
        return Dm.list(Dm.kv("meat", 2), Dm.kv("soulfulness", 1));
    }

    // tg: code/modules/fishing/fish/types/ruins.dm:95
    public override object get_fish_taste_cooked()
    {
        return Dm.list(Dm.kv("cooked meat", 2));
    }

    // tg: code/modules/fishing/fish/types/ruins.dm:98
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] swallows [] whole! It looks like [] trying to commit soulcide!", user, this, user.p_theyre())));
        await forceMove(user);
        _addtimer(Dm.@new<datum_callback>(this, nameof(good_ending), user), 2.5f * SECONDS, file: "code/modules/fishing/fish/types/ruins.dm", line: 101);
        for (int i = 1; i <= 7; i++)
            _addtimer(Dm.@new<datum_callback>(this, nameof(soul_attack), user, i), 0.2f * SECONDS * i, file: "code/modules/fishing/fish/types/ruins.dm", line: 103);
        return MANUAL_SUICIDE;
    }

    // tg: code/modules/fishing/fish/types/ruins.dm:106
    public virtual async DmTask good_ending(mob_living user = null)
    {
        mob_living_basic_spaceman soulman = Dm.@new<mob_living_basic_spaceman>(get_turf(user));
        await soulman.fully_replace_character_name(user.real_name);
        _addtimer(Dm.@new<datum_callback>(soulman, nameof(atom.visible_message), span_notice(I18n.T("[] was too pure for this world...", soulman))), 5 * SECONDS, TIMER_DELETE_ME, file: "code/modules/fishing/fish/types/ruins.dm", line: 109);
        _addtimer(Dm.@new<datum_callback>(soulman, nameof(mob_living.death)), 5 * SECONDS, TIMER_DELETE_ME, file: "code/modules/fishing/fish/types/ruins.dm", line: 110);
        if (prob(80)) // the percentage is important.
        {
            soulman.PossessByPlayer(user.ckey);
            to_chat(soulman, span_notice(I18n.T("You finally feel at peace.")));
        }
        await user.gib();
        qdel(this);
    }

    // tg: code/modules/fishing/fish/types/ruins.dm:117
    public virtual async DmTask soul_attack(mob user = null, object iteration = null)
    {
        Type soulbox_type = (Type)pick(typeof(obj_item_storage_toolbox_mechanical_old), typeof(obj_item_storage_toolbox_mechanical_old_cleaner));
        obj_item_storage_toolbox_mechanical_old soulbox = (obj_item_storage_toolbox_mechanical_old)Dm.@new(soulbox_type, get_turf(user));
        object yeet_direction = pick(GLOB.alldirs);
        float yeet_distance = rand(1, 7);
        if (ishuman(user))
        {
            mob_living_carbon_human human_user = (mob_living_carbon_human)user;
            await human_user.setDir(yeet_direction);
            await human_user.vomit(distance: yeet_distance);
        }
        await soulbox.throw_at(get_edge_target_turf(get_turf(user), yeet_direction) as atom, yeet_distance, 2, user, spin: TRUE);
        await soulbox.AddElement(typeof(datum_element_haunted), Dm.named("haunt_color", "#124CD5"));
        if (prob(86)) // 1 in 7 chance to stay
            soulbox.fade_into_nothing(1 * SECONDS * Dm.num(iteration), 0.5f * SECONDS);
    }
}

///From the cursed spring
// tg: code/modules/fishing/fish/types/ruins.dm:132
public partial class obj_item_fish_skin_crab : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/ruins.dm:153
    public override object get_fish_taste()
    {
        return Dm.list(Dm.kv("raw crab", 2));
    }

    // tg: code/modules/fishing/fish/types/ruins.dm:156
    public override object get_fish_taste_cooked()
    {
        return Dm.list(Dm.kv("cooked crab", 2));
    }

    // tg: code/modules/fishing/fish/types/ruins.dm:159
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] puts [] hand on [] and focuses intently! It looks like [] trying to transfer [] skin to []!", user, user.p_their(), this, user.p_theyre(), user.p_their(), this)));
        if (HAS_TRAIT(user, TRAIT_UNHUSKABLE))
        {
            user.visible_message(span_suicide(I18n.T("[] has no skin! How embarrassing!", user)));
            return SHAME;
        }
        if (!ishuman(user))
        {
            user.visible_message(span_suicide(I18n.T("[] doesn't want []'s skin! How embarrassing!", this, user)));
            return SHAME;
        }
        if (status == FISH_DEAD)
        {
            user.visible_message(span_suicide(I18n.T("[] is dead! [] just looks like a doofus!", this, user)));
            return SHAME;
        }

        mob_living_carbon skin_haver = (mob_living_carbon)user;
        object skin_tone = null;
        foreach (obj_item_bodypart to_wound in Dm.each<obj_item_bodypart>(user.get_bodyparts()))
        {
            if (to_wound == user.get_bodypart(BODY_ZONE_CHEST))
                skin_tone = Dm.truthy(to_wound.species_color) ? to_wound.species_color : skintone2hex(to_wound.skin_tone);
            await skin_haver.cause_wound_of_type_and_severity(WOUND_SLASH, to_wound, WOUND_SEVERITY_CRITICAL, WOUND_SEVERITY_CRITICAL);
            await skin_haver.cause_wound_of_type_and_severity(WOUND_PIERCE, to_wound, WOUND_SEVERITY_CRITICAL, WOUND_SEVERITY_CRITICAL);
            await skin_haver.cause_wound_of_type_and_severity(WOUND_BLUNT, to_wound, WOUND_SEVERITY_CRITICAL, WOUND_SEVERITY_CRITICAL);
            user.become_husk(REF(this));
            to_wound.skin_tone = COLOR_RED; // skin is gone. (if they somehow get revived, don't worry - death from loss of skin takes longer than dehydration, so it's still realistic)
        }

        // skin crab grows powerful
        color = skin_tone; //skintone2hex(skin_tone) //wait til smartkar's recolorwork
        visible_message(span_danger(I18n.T("[] starts glowing eerily...", user)));
        await AddElement(typeof(datum_element_haunted), Dm.named("haunt_color", skin_tone));

        return BRUTELOSS;
    }
}
