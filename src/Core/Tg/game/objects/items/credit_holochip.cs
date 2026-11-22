// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/credit_holochip.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4a (D327): the whole file: the holochip that carries credits, its kilo, mega and giga pictures with the coloured value band,
// merging, splitting with Alt-click and the EMP wipe. The var blocks are generated.

// tg: code/game/objects/items/credit_holochip.dm:1
public partial class obj_item_holochip : obj_item
{
    // tg: code/game/objects/items/credit_holochip.dm:14
    [DmParams("mapload", "amount")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object amount = Dm.arg(__extra, 0) ?? 1;
        object dot = base.Initialize(mapload);
        if (!Dm.truthy(credits) && Dm.truthy(amount))
            credits = Dm.num(amount);
        if (credits <= 0 && !Dm.truthy(mapload))
        {
            stack_trace(Dm.text("Holochip created with 0 or less [] in []!", MONEY_NAME, get_area_name(this)), "code/game/objects/items/credit_holochip.dm", 19);
            return INITIALIZE_HINT_QDEL;
        }
        add_traits(Dm.list(TRAIT_FISHING_BAIT, TRAIT_BAIT_ALLOW_FISHING_DUD), INNATE_TRAIT);
        update_appearance();
        register_context();
        return dot;
    }

    // tg: code/game/objects/items/credit_holochip.dm:25
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        // MONEY_NAME_AUTOPURAL(credits) (__DEFINES/economy.dm:99), inline (rules 11.1)
        Dm.append(ref dot, Dm.text("[]\n", span_notice(I18n.T("It's loaded with [] []", credits, Dm.text("credit[]", credits == 1 ? "" : "s")))) + span_notice(I18n.T("Alt-Click to split.")));
        return dot;
    }

    // tg: code/game/objects/items/credit_holochip.dm:30
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (held_item is obj_item_holochip)
            context[SCREENTIP_CONTEXT_LMB] = I18n.T("Merge Into");
        context[SCREENTIP_CONTEXT_ALT_LMB] = I18n.T("Extract []", MONEY_NAME_CAPITALIZED);
        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/game/objects/items/credit_holochip.dm:36
    public override object get_item_credit_value()
    {
        return credits;
    }

    // tg: code/game/objects/items/credit_holochip.dm:39
    public override object update_name(float? updates = null)
    {
        name = I18n.T("\\improper [] [] holochip", credits, MONEY_NAME_SINGULAR);
        return base.update_name(updates);
    }

    // tg: code/game/objects/items/credit_holochip.dm:43
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        string icon_suffix = "";
        if (credits >= 1e3f && credits <= 1e6f - 1)
            icon_suffix = "_kilo";
        else if (credits >= 1e6f && credits <= 1e9f - 1)
            icon_suffix = "_mega";
        else if (credits >= 1e9f && credits <= INFINITY)
            icon_suffix = "_giga";

        icon_state = Dm.text("[][]", base_icon_state, icon_suffix);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/credit_holochip.dm:56
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        float rounded_credits = 0;
        if (credits >= 0 && credits <= 1e3f - 1)
            rounded_credits = round(Dm.num(credits));
        else if (credits >= 1e3f && credits <= 1e6f - 1)
            rounded_credits = round(Dm.num(credits) * 1e-3f);
        else if (credits >= 1e6f && credits <= 1e9f - 1)
            rounded_credits = round(Dm.num(credits) * 1e-6f);
        else if (credits >= 1e9f && credits <= INFINITY)
            rounded_credits = round(Dm.num(credits) * 1e-9f);

        string overlay_color = "#914792";
        if (rounded_credits >= 0 && rounded_credits <= 4)
            overlay_color = "#8E2E38";
        else if (rounded_credits >= 5 && rounded_credits <= 9)
            overlay_color = "#914792";
        else if (rounded_credits >= 10 && rounded_credits <= 19)
            overlay_color = "#BF5E0A";
        else if (rounded_credits >= 20 && rounded_credits <= 49)
            overlay_color = "#358F34";
        else if (rounded_credits >= 50 && rounded_credits <= 99)
            overlay_color = COLOR_SLIME_METAL;
        else if (rounded_credits >= 100 && rounded_credits <= 199)
            overlay_color = "#009D9B";
        else if (rounded_credits >= 200 && rounded_credits <= 499)
            overlay_color = "#0153C1";
        else if (rounded_credits >= 500 && rounded_credits <= INFINITY)
            overlay_color = "#2C2C2C";

        var holochip_overlay = mutable_appearance(Dm.resource("icons/obj/economy.dmi"), Dm.text("[]-color", icon_state)) as mutable_appearance;
        holochip_overlay.color = overlay_color;
        Dm.append(ref dot, holochip_overlay);
        return dot;
    }

    // tg: code/game/objects/items/credit_holochip.dm:92
    public virtual float spend(object amount = null, object pay_anyway = null)
    {
        float value = Dm.num(amount);
        if (credits >= value)
        {
            credits -= value;
            if (credits == 0)
                qdel(this);
            update_appearance();
            return value;
        }
        else if (Dm.truthy(pay_anyway))
        {
            qdel(this);
            return credits ?? 0;
        }
        else
            return 0;
    }

    // tg: code/game/objects/items/credit_holochip.dm:105
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_holochip merged_holochip)
            return NONE;

        credits += merged_holochip.credits;
        balloon_alert(user, I18n.T("merged!"));
        update_appearance();
        qdel(merged_holochip);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/credit_holochip.dm:116
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (loc != user)
        {
            to_chat(user, span_warning(I18n.T("You must be holding the holochip to continue!")));
            return CLICK_ACTION_BLOCKING;
        }
        object split_amount = await tgui_input_number(user, I18n.T("How many [] do you want to extract from the holochip? (Max: [] [])", MONEY_NAME, credits, MONEY_SYMBOL), I18n.T("Holochip"), max_value: credits);
        if (!Dm.truthy(split_amount) || QDELETED(user) || QDELETED(this) || issilicon(user) || !Dm.truthy(await usr.can_perform_action(this, Dm.bitor(NEED_DEXTERITY, FORBID_TELEKINESIS_REACH))) || loc != user)
            return CLICK_ACTION_BLOCKING;
        float new_credits = spend(split_amount, TRUE);
        var chip = Dm.@new<obj_item_holochip>(user != null ? user : drop_location(), new_credits);
        if (user != null)
        {
            if (!Dm.truthy(await user.put_in_hands(chip)))
                await chip.forceMove(user.drop_location() as atom);
            add_fingerprint(user);
        }
        to_chat(user, span_notice(I18n.T("You extract [] [] into a new holochip.", split_amount, MONEY_NAME)));
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/credit_holochip.dm:132
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        float wipe_chance = 60 / Dm.num(severity);
        if (prob(wipe_chance))
        {
            visible_message(span_warning(I18n.T("[] fizzles and disappears!", this)));
            qdel(this); //rip cash
        }
        return dot;
    }
}
