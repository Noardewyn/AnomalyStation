// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/packaged.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the kitchen links of the food (make_grillable, make_bakeable, make_microwaveable, make_processable, make_dryable),
// the cans' base (:4-38: opening a can) and the Ready-Donk's box (:241) came with the kitchen (gaps H2, D333); the Envirochow that
// buffs a pet, the squid ink can's splat and the ration pack nobody likes (the chilling silver slime extract's emergency food) with
// the restaurant. The var blocks are generated.

// Cans
// tg: code/modules/food_and_drinks/food/packaged.dm:4
public partial class obj_item_food_canned : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/packaged.dm:19
    public override async DmTask make_germ_sensitive(object mapload = null)
    {
        return; // It's in a can
    }

    // tg: code/modules/food_and_drinks/food/packaged.dm:22
    public virtual async DmTask open_can(mob user = null)
    {
        to_chat(user, span_notice(I18n.T("You pull back the tab of \\the [].", this)));
        playsound(user.loc, Dm.resource("sound/items/foodcanopen.ogg"), 50);
        Dm.combine(ref ((datum_reagents)reagents).flags, OPENCONTAINER);
        preserved_food = FALSE;
    }

    // tg: code/modules/food_and_drinks/food/packaged.dm:28
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(is_drainable()))
        {
            await open_can(user);
            icon_state = Dm.text("[]_open", icon_state);
        }
        return await base.attack_self(user, modifiers, __extra);
    }

    // tg: code/modules/food_and_drinks/food/packaged.dm:34  (tg names the third argument def_zone)
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(is_drainable()))
        {
            to_chat(user, span_warning(I18n.T("[]'s lid hasn't been opened!", this)));
            return FALSE;
        }
        return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
    }
}

// tg: code/modules/food_and_drinks/food/packaged.dm:178
public partial class obj_item_food_canned_chap : obj_item_food_canned
{
    // tg: code/modules/food_and_drinks/food/packaged.dm:187
    public override async DmTask make_processable()
    {
        await AddElement(typeof(datum_element_processable), TOOL_KNIFE, typeof(obj_item_food_chapslice), 5, 3 * SECONDS, Dm.named("table_required", TRUE), Dm.named("screentip_verb", "Cut"));
    }
}

public partial class obj_item_food_chapslice : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/packaged.dm:202
    public override async DmTask make_grillable()
    {
        await AddComponent(typeof(datum_component_grillable), typeof(obj_item_food_grilled_chapslice), rand(20 * SECONDS, 40 * SECONDS), TRUE, TRUE);
    }
}

// DONK DINNER: THE INNOVATIVE WAY TO GET YOUR DAILY RECOMMENDED ALLOWANCE OF SALT... AND THEN SOME!
// tg: code/modules/food_and_drinks/food/packaged.dm:219
public partial class obj_item_food_ready_donk : obj_item_food
{
    /// What reagents should be added when this item is warmed?
    // tg: code/modules/food_and_drinks/food/packaged.dm:233  var/static/list/added_reagents (rules 10.6)
    public static DmList added_reagents => TgState.Current.obj_item_food_ready_donk__added_reagents ??= Dm.list(Dm.kv(typeof(datum_reagent_medicine_omnizine), 3));

    // tg: code/modules/food_and_drinks/food/packaged.dm:235
    public override async DmTask make_bakeable()
    {
        await AddComponent(typeof(datum_component_bakeable), warm_type, rand(15 * SECONDS, 20 * SECONDS), TRUE, TRUE, added_reagents);
    }

    // tg: code/modules/food_and_drinks/food/packaged.dm:238
    public override async DmTask make_microwaveable()
    {
        await AddElement(typeof(datum_element_microwavable), warm_type, added_reagents);
    }

    // tg: code/modules/food_and_drinks/food/packaged.dm:241
    public override async DmTask<object> examine_more(mob user = null)
    {
        DmList dot = await base.examine_more(user) as DmList;
        dot.Add(span_notice(I18n.T("<i>You browse the back of the box...</i>")));
        dot.Add(Dm.text("\t[]", span_info(I18n.T("Ready-Donk: a product of Donk Co."))));
        dot.Add(Dm.text("\t[]", span_info(I18n.T("Heating instructions: open box and pierce film, heat in microwave on high for 2 minutes. Allow to stand for 60 seconds prior to eating. Product will be hot."))));
        dot.Add(Dm.text("\t[]", span_info(I18n.T("Per 200g serving contains: 8g Sodium; 25g Fat, of which 22g are saturated; 2g Sugar."))));
        return dot;
    }
}

// tg: code/modules/food_and_drinks/food/packaged.dm:97
public partial class obj_item_food_canned_envirochow : obj_item_food_canned
{
    // tg: code/modules/food_and_drinks/food/packaged.dm:111
    public override async DmTask<object> attack_animal(mob user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(check_buffability(user as mob_living)))
            return await base.attack_animal(user, modifiers);
        await apply_buff(user as mob_living);
        return null;
    }

    // tg: code/modules/food_and_drinks/food/packaged.dm:116
    public override async DmTask<object> attack_basic_mob(mob user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(check_buffability(user as mob_living)))
            return await base.attack_basic_mob(user, modifiers);
        await apply_buff(user as mob_living);
        return TRUE;
    }

    // tg: code/modules/food_and_drinks/food/packaged.dm:122
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(check_buffability(interacting_with as mob_living)))
            return NONE;
        await apply_buff(interacting_with as mob_living, user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/food_and_drinks/food/packaged.dm:129
    public virtual object check_buffability(mob_living hungry_pet = null)
    {
        if (!isanimal_or_basicmob(hungry_pet)) // Not a pet
            return FALSE;
        if (!Dm.truthy(is_drainable())) // Can is not open
            return FALSE;
        if (IS_UNCONSCIOUS_OR_CRIT(hungry_pet)) // Parrot deceased
            return FALSE;
        if (Dm.truthy(Dm.bitand(hungry_pet.mob_biotypes, Dm.bitor(Dm.bitor(MOB_BEAST, MOB_REPTILE), MOB_BUG))))
            return TRUE;
        else
            return FALSE; // Humans, robots & spooky ghosts not allowed
    }

    // tg: code/modules/food_and_drinks/food/packaged.dm:142
    public virtual async DmTask apply_buff(mob_living hungry_pet = null, mob_living dog_mom = null)
    {
        await hungry_pet.apply_status_effect(typeof(datum_status_effect_limited_buff_health_buff)); //the status effect keeps track of the stacks
        hungry_pet.visible_message(
            span_notice(I18n.T("[] chows down on [].", hungry_pet, this)),
            span_nicegreen(I18n.T("You chow down on [].", this)),
            span_notice(I18n.T("You hear sloppy eating noises.")));
        // SEND_SIGNAL(src, COMSIG_FOOD_CONSUMED, hungry_pet, dog_mom ? dog_mom : hungry_pet) (rules 11.1 (e), a statement): if there is no dog mom, we assume the pet fed itself
        if (Dm.truthy(this._listen_lookup?[COMSIG_FOOD_CONSUMED]))
            this._SendSignal(COMSIG_FOOD_CONSUMED, Dm.list(this, hungry_pet, Dm.truthy(dog_mom) ? dog_mom : hungry_pet));
        playsound(loc, Dm.resource("sound/items/eatfood.ogg"), rand(30, 50), TRUE);
        qdel(this);
    }
}

// tg: code/modules/food_and_drinks/food/packaged.dm:152
public partial class obj_item_food_canned_squid_ink : obj_item_food_canned
{
    // tg: code/modules/food_and_drinks/food/packaged.dm:161
    public override async DmTask open_can(mob user = null)
    {
        await base.open_can(user);
        await AddComponent(typeof(datum_component_splat),
            Dm.named("memory_type", typeof(datum_memory_witnessed_inking)),
            Dm.named("smudge_type", typeof(obj_effect_decal_cleanable_food_squid_ink)),
            Dm.named("moodlet_type", typeof(datum_mood_event_inked)),
            Dm.named("splat_color", COLOR_NEARLY_ALL_BLACK),
            Dm.named("hit_callback", Dm.@new<datum_callback>(this, nameof(blind_em)))
        );
    }

    // tg: code/modules/food_and_drinks/food/packaged.dm:171
    public virtual async DmTask blind_em(mob_living victim = null, object can_splat_on = null)
    {
        if (Dm.truthy(can_splat_on))
        {
            await victim.adjust_temp_blindness_up_to(2.5f * SECONDS, 3 * SECONDS);
            await victim.adjust_confusion_up_to(2.5f * SECONDS, 3 * SECONDS);
        }
        victim.visible_message(span_warning(I18n.T("[] is inked by []!", victim, this)), span_userdanger(I18n.T("You've been inked by []!", this)));
        playsound(victim, SFX_DESECRATION, 50, TRUE);
    }
}

// tg: code/modules/food_and_drinks/food/packaged.dm:359
public partial class obj_item_food_rationpack : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/packaged.dm:373
    public override async DmTask make_edible()
    {
        await base.make_edible();
        await AddComponentFrom(SOURCE_EDIBLE_INNATE, typeof(datum_component_edible), Dm.named("check_liked", Dm.@new<datum_callback>(this, nameof(check_liked))));
    }

    // tg: code/modules/food_and_drinks/food/packaged.dm:377
    public virtual object check_liked(mob mob = null) //Nobody likes rationpacks. Nobody.
    {
        return FOOD_DISLIKED;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/food_and_drinks/food/packaged.dm:233  /obj/item/food/ready_donk's var/static/list/added_reagents
    internal DmList obj_item_food_ready_donk__added_reagents;
}
