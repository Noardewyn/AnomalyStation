// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/rcd/RSF.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI2 (D326): the Rapid-Service-Fabricator and the cookie synthesizer, whole (the service cyborg's, the bartender's). The var
// blocks are generated (D111).

/*
CONTAINS:
RSF

*/
// tg: code/game/objects/items/rcd/RSF.dm:8
public partial class obj_item_rsf : obj_item
{
    ///Extracts the related object from an associated list of objects and values, or lists and objects.
    // tg: code/game/objects/items/rcd/RSF.dm:7  #define OBJECT_OR_LIST_ELEMENT(from, input) (islist(input) ? from[input] : input)
    private static object OBJECT_OR_LIST_ELEMENT(DmList from, object input) => input is DmList ? from[input] : input;

    // tg: code/game/objects/items/rcd/RSF.dm:63
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        to_dispense = cost_by_item[1] as Type;
        dispense_cost = Dm.num(cost_by_item[to_dispense]);
        return dot;
    }

    // tg: code/game/objects/items/rcd/RSF.dm:68
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("It currently holds []/[] [].", matter, max_matter, discriptor)));
        return dot;
    }

    // tg: code/game/objects/items/rcd/RSF.dm:75
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(is_type_in_list(tool, matter_by_item))) //If the thing we got hit by is in our matter list
            return NONE;

        float tempMatter = Dm.num(matter_by_item[tool.type]) + matter;
        if (tempMatter > max_matter)
        {
            to_chat(user, span_warning(I18n.T("\\The [] can't hold any more []!", this, discriptor)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (isstack(tool))
        {
            obj_item_stack stack = (obj_item_stack)tool;
            await stack.use(1);
        }
        else
            qdel(tool);
        matter = tempMatter; //We add its value
        playsound(loc, Dm.resource("sound/machines/click.ogg"), 10, TRUE);
        to_chat(user, span_notice(I18n.T("\\The [] now holds []/[] [].", this, matter, max_matter, discriptor)));
        icon_state = base_icon_state as string; //and set the icon state to the base state
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/rcd/RSF.dm:95
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        playsound(loc, Dm.resource("sound/effects/pop.ogg"), 50, FALSE);
        object target = cost_by_item;
        object cost = 0;
        //Warning, prepare for bodgecode
        while (target is DmList target_list) //While target is a list we continue the loop
        {
            object picked = await show_radial_menu(user, this, formRadial(target_list), custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), user), require_near: TRUE);
            if (!Dm.truthy(check_menu(user)) || picked == null)
                return null;
            foreach (object emem in Dm.each(target_list)) //Back through target agian
            {
                object test = OBJECT_OR_LIST_ELEMENT(target_list, emem);
                if (Dm.eq(picked, Dm.initial((Type)test, (atom p) => p.name))) //We try and find the entry that matches the radial selection
                {
                    cost = target_list[emem]; //We cash the cost
                    target = emem;
                    break;
                }
            }
            //If we found a list we start it all again, this time looking through its contents.
            //This allows for sublists
        }
        to_dispense = target as Type;
        dispense_cost = Dm.num(cost);
        // Change mode
        return null;
    }

    ///Forms a radial menu based off an object in a list, or a list's associated object
    // tg: code/game/objects/items/rcd/RSF.dm:117
    public virtual DmList formRadial(DmList from = null)
    {
        DmList radial_list = Dm.list();
        foreach (object meme in Dm.each(from)) //We iterate through all of targets entrys
        {
            Type temp = (Type)OBJECT_OR_LIST_ELEMENT(from, meme);
            //We then add their data into the radial menu
            radial_list[Dm.initial(temp, (atom p) => p.name)] = image(icon: Dm.initial(temp, (atom p) => p.icon), icon_state: Dm.initial(temp, (atom p) => p.icon_state));
        }
        return radial_list;
    }

    // tg: code/game/objects/items/rcd/RSF.dm:125
    public virtual object check_menu(mob user = null)
    {
        if (Dm.truthy(user.incapacitated) || !Dm.truthy(Waiting.NoSleep(user.Adjacent(this))))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/objects/items/rcd/RSF.dm:130
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (cooldown > world.time)
            return NONE;
        if (!Dm.truthy(is_allowed(interacting_with)))
            return NONE;
        if (Dm.truthy(use_matter(dispense_cost, user))) //If we can charge that amount of charge, we do so and return true
        {
            playsound(loc, Dm.resource("sound/machines/click.ogg"), 10, TRUE);
            atom meme = Dm.@new(to_dispense, get_turf(interacting_with)) as atom;
            to_chat(user, span_notice(I18n.T("[] []...", action_type, meme.name)));
            cooldown = world.time + cooldowndelay;
            return ITEM_INTERACT_SUCCESS;
        }
        return ITEM_INTERACT_BLOCKING;
    }

    ///A helper proc. checks to see if we can afford the amount of charge that is passed, and if we can docs the charge from our base, and returns TRUE. If we can't we return FALSE
    // tg: code/game/objects/items/rcd/RSF.dm:144
    public virtual object use_matter(float charge = 0, mob user = null)
    {
        if (iscyborg(user))
        {
            mob_living_silicon_robot R = (mob_living_silicon_robot)user;
            float end_charge = Dm.num(R.cell__obj.charge) - charge;
            if (end_charge < 0)
            {
                to_chat(user, span_warning(I18n.T("You do not have enough power to use [].", this)));
                icon_state = spent_icon_state;
                return FALSE;
            }
            R.cell__obj.charge = end_charge;
            return TRUE;
        }
        else
        {
            if (matter - 1 < 0)
            {
                to_chat(user, span_warning(I18n.T("\\The [] doesn't have enough [] left.", this, discriptor)));
                icon_state = spent_icon_state;
                return FALSE;
            }
            matter--;
            to_chat(user, span_notice(I18n.T("\\The [] now holds []/[] [].", this, matter, max_matter, discriptor)));
            return TRUE;
        }
    }

    ///Helper proc that iterates through all the things we are allowed to spawn on, and sees if the passed atom is one of them
    // tg: code/game/objects/items/rcd/RSF.dm:164
    public virtual object is_allowed(atom to_check = null)
    {
        return is_type_in_list(to_check, allowed_surfaces);
    }
}

// tg: code/game/objects/items/rcd/RSF.dm:167
public partial class obj_item_rsf_cookiesynth : obj_item_rsf
{
    // tg: code/game/objects/items/rcd/RSF.dm:182
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        obj_flags = Dm.bitxor(obj_flags, EMAGGED);
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            balloon_alert(user, I18n.T("reagent safety checker shorted out"));
        else
            balloon_alert(user, I18n.T("reagent safety checker reset"));
        return TRUE;
    }

    // tg: code/game/objects/items/rcd/RSF.dm:190
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        mob_living_silicon_robot P = null;
        if (iscyborg(user))
            P = (mob_living_silicon_robot)user;
        if ((Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) || Dm.truthy(P?.emagged)) && !Dm.truthy(toxin))
        {
            toxin = TRUE;
            to_dispense = typeof(obj_item_food_cookie_sleepy);
            to_chat(user, span_alert(I18n.T("Cookie Synthesizer hacked.")));
        }
        else
        {
            toxin = FALSE;
            to_dispense = typeof(obj_item_food_cookie);
            to_chat(user, span_notice(I18n.T("Cookie Synthesizer reset.")));
        }
        return null;
    }
}
