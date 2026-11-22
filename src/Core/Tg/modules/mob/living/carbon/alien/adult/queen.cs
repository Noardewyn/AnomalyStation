// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/alien/adult/queen.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file, the royals (praetorian and queen), the queen's egg laying and her royal parasite (the promotion to
// praetorian). The var blocks are generated (D111).

// tg: code/modules/mob/living/carbon/alien/adult/queen.dm:1
public partial class mob_living_carbon_alien_adult_royal : mob_living_carbon_alien_adult
{
    // tg: code/modules/mob/living/carbon/alien/adult/queen.dm:17
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // as a wise man once wrote: "pull over that ass too fat"
        REMOVE_TRAIT(this, TRAIT_VENTCRAWLER_ALWAYS, INNATE_TRAIT);
        // that'd be a too cheeky shield bashing strat
        ADD_TRAIT(this, TRAIT_BRAWLING_KNOCKDOWN_BLOCKED, INNATE_TRAIT);
        // Lets you spin without falling over
        ADD_TRAIT(this, TRAIT_STRENGTH, INNATE_TRAIT);
        // DEVIATION(DV460): the see-through component fades a big sprite for its own top-down viewer; the first-person client draws
        // no own body over the view, so the component and its toggle action are not added.
        Dm.Deferred("code/modules/mob/living/carbon/alien/adult/queen.dm:25", "Initialize: AddComponent(/datum/component/seethrough_mob), the royal's see-through for its own top-down view (DV460)");
        return dot;
    }

    // tg: code/modules/mob/living/carbon/alien/adult/queen.dm:27
    public override async DmTask on_lying_down(object new_lying_angle = null)
    {
        await base.on_lying_down(new_lying_angle);
        layer = LYING_MOB_LAYER;
    }

    // tg: code/modules/mob/living/carbon/alien/adult/queen.dm:31
    [DmParams("new_lying_angle")]
    public override async DmTask on_standing_up(params object[] __extra)
    {
        await base.on_standing_up(__extra);
        layer = Dm.initial(this, (atom p) => p.layer);
    }

    // tg: code/modules/mob/living/carbon/alien/adult/queen.dm:35
    public override object can_inject(mob user = null, object target_zone = null, object injection_flags = null)
    {
        return FALSE;
    }

    // tg: code/modules/mob/living/carbon/alien/adult/queen.dm:38
    public override object get_fire_overlay(object stacks = null, object on_fire = null)
    {
        string fire_key = "royal_fire";

        if (!Dm.truthy(GLOB.fire_appearances[fire_key]))
        {
            mutable_appearance fire = mutable_appearance(
                Dm.resource("icons/mob/effects/onfire.dmi"),
                "generic_fire",
                ABOVE_ALL_MOB_LAYER,
                appearance_flags: Dm.bitor(Dm.bitor(RESET_COLOR, KEEP_APART), PIXEL_SCALE)
            ) as mutable_appearance;
            fire.pixel_x = 16;
            fire.pixel_y = 8;
            fire.transform = ((matrix)fire.transform).Scale(2, 2);
            GLOB.fire_appearances[fire_key] = fire;
        }

        return GLOB.fire_appearances[fire_key];
    }
}

// tg: code/modules/mob/living/carbon/alien/adult/queen.dm:55
public partial class mob_living_carbon_alien_adult_royal_queen : mob_living_carbon_alien_adult_royal
{
    // tg: code/modules/mob/living/carbon/alien/adult/queen.dm:82  var/static/list/innate_actions (a proc-level static, rules 10.6)
    private static ref DmList Initialize__innate_actions => ref TgState.Current.mob_living_carbon_alien_adult_royal_queen__Initialize__innate_actions;

    // tg: code/modules/mob/living/carbon/alien/adult/queen.dm:81
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        grant_actions_by_list(Initialize__innate_actions);
        add_movespeed_mod_immunities(type, typeof(datum_movespeed_modifier_tail_dragger));
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/mob/living/carbon/alien/adult/queen.dm:89
    public override void set_name()
    {
        if (get_alien_type(typeof(mob_living_carbon_alien_adult_royal_queen), ignored: this) != null)
            name = I18n.T("alien princess");
        base.set_name();
    }
}

//Queen verbs
// tg: code/modules/mob/living/carbon/alien/adult/queen.dm:95
public partial class datum_action_cooldown_alien_make_structure_lay_egg : datum_action_cooldown_alien_make_structure
{
    // tg: code/modules/mob/living/carbon/alien/adult/queen.dm:102
    public override async DmTask<object> Activate(atom target = null)
    {
        object dot = await base.Activate(target);
        owner.visible_message(span_alertalien(I18n.T("[] lays an egg!", owner)));
        return dot;
    }
}

//Button to let queen choose her praetorian.
// tg: code/modules/mob/living/carbon/alien/adult/queen.dm:107
public partial class datum_action_cooldown_alien_promote : datum_action_cooldown_alien
{
    // tg: code/modules/mob/living/carbon/alien/adult/queen.dm:114
    [DmParams("Target")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        //not free
        if (promotion_plasma_cost != 0)
            name = I18n.T("[] ([]P)", Dm.initial(this, (datum_action p) => p.name), promotion_plasma_cost);
    }

    // tg: code/modules/mob/living/carbon/alien/adult/queen.dm:120
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        feedback ??= FALSE;
        object dot = await base.IsAvailable(feedback);
        if (!Dm.truthy(dot))
            return FALSE;

        mob_living_carbon carbon_owner = owner as mob_living_carbon;
        if (Dm.num(carbon_owner.getPlasma()) < promotion_plasma_cost)
            return FALSE;

        if (get_alien_type(typeof(mob_living_carbon_alien_adult_royal_praetorian)) != null)
            return FALSE;

        return TRUE;
    }

    // tg: code/modules/mob/living/carbon/alien/adult/queen.dm:134
    public override async DmTask<object> Activate(atom target = null)
    {
        obj_item_queen_promotion existing_promotion = Dm.locate<obj_item_queen_promotion>(owner.held_items);
        if (existing_promotion != null)
        {
            to_chat(owner, span_noticealien(I18n.T("You discard [].", existing_promotion)));
            await owner.temporarilyRemoveItemFromInventory(existing_promotion);
            qdel(existing_promotion);
            return TRUE;
        }

        if (!Dm.truthy(owner.get_empty_held_indexes()))
        {
            to_chat(owner, span_warning(I18n.T("You must have an empty hand before preparing the parasite.")));
            return FALSE;
        }

        obj_item_queen_promotion new_promotion = Dm.@new<obj_item_queen_promotion>(owner.loc);
        if (!Dm.truthy(await owner.put_in_hands(new_promotion, del_on_fail: TRUE)))
        {
            to_chat(owner, span_noticealien(I18n.T("You fail to prepare a parasite.")));
            return FALSE;
        }

        to_chat(owner, span_noticealien(I18n.T("Use [] on one of your children to promote her to a Praetorian!", new_promotion)));
        return TRUE;
    }
}

// tg: code/modules/mob/living/carbon/alien/adult/queen.dm:154
public partial class obj_item_queen_promotion : obj_item
{
    // tg: code/modules/mob/living/carbon/alien/adult/queen.dm:161
    [DmParams("to_promote", "queen")]
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        mob_living to_promote = target_mob;
        mob_living_carbon_alien_adult queen = user as mob_living_carbon_alien_adult;
        object dot = await base.attack__proc(to_promote, queen);
        if (Dm.truthy(dot))
            return dot;

        datum_action_cooldown_alien_promote promotion = Dm.locate<datum_action_cooldown_alien_promote>(queen.actions);
        if (promotion == null)
            throw Dm.CRASH(Dm.text("[] was created and handled by a mob ([]) that didn't have a promotion action associated.", type, queen), "code/modules/mob/living/carbon/alien/adult/queen.dm", 168);

        if (!isalienadult(to_promote) || isalienroyal(to_promote))
        {
            to_chat(queen, span_noticealien(I18n.T("You may only use this with your adult, non-royal children!")));
            return dot;
        }

        if (!Dm.truthy(await promotion.IsAvailable()))
        {
            to_chat(queen, span_noticealien(I18n.T("You cannot promote a child right now!")));
            return dot;
        }

        if (IS_UNCONSCIOUS_OR_CRIT(to_promote) || to_promote.mind == null || !Dm.truthy(to_promote.key))
            return dot;

        await queen.adjustPlasma(-promotion.promotion_plasma_cost);

        to_chat(queen, span_noticealien(I18n.T("You have promoted [] to a Praetorian!", to_promote)));
        to_promote.visible_message(
            span_alertalien(I18n.T("[] begins to expand, twist and contort!", to_promote)),
            span_noticealien(I18n.T("The queen has granted you a promotion to Praetorian!")));

        mob_living_carbon_alien lucky_winner = to_promote as mob_living_carbon_alien;
        mob_living_carbon_alien_adult_royal_praetorian new_prae = Dm.@new<mob_living_carbon_alien_adult_royal_praetorian>(lucky_winner.loc);
        await lucky_winner.alien_evolve(new_prae);
        qdel(this);
        return TRUE;
    }

    // tg: code/modules/mob/living/carbon/alien/adult/queen.dm:195
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        to_chat(user, span_noticealien(I18n.T("You discard [].", this)));
        qdel(this);
        return null;
    }

    // tg: code/modules/mob/living/carbon/alien/adult/queen.dm:199
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        if (!Dm.truthy(silent))
            to_chat(user, span_noticealien(I18n.T("You discard [].", this)));
        return await base.dropped(user, silent, __extra);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/carbon/alien/adult/queen.dm:82
    internal DmList mob_living_carbon_alien_adult_royal_queen__Initialize__innate_actions = Dm.list(
        typeof(datum_action_cooldown_alien_promote));
}
