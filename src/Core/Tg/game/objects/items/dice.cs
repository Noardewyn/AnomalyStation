// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/dice.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DE1 (D333): the file but the Die of Fate. Every die rolls in the hand or when thrown with tg's dice sound, shows the face it
// landed on as its `<state>-<result>` overlay, says "NAT 20!" or "Ouch, bad luck.", is rigged by a microwave (all but the ebony
// die), and the special dice read their faces (the fudge die, the strange d20's eight-ball answers, the 4d d6's cube sides, the d00's
// tens); the d4 is a caltrop; the dice bags hold a set and one odd die, the hazard bag five d6 (now and then an ebony one). The Die of
// Fate (/obj/item/dice/d20/fate and its subtypes, :266-444) is the wizard's and stays inert until the wizard comes (not-translated.md
// 206, D333). The die's result holds the number or the face it rolled, so the types converter types it object
// (Generator.Members.cs' ObjectHoldingVars). The var blocks are generated (D111).

///holding bag for dice
// tg: code/game/objects/items/dice.dm:6
public partial class obj_item_storage_dice : obj_item_storage
{
    // tg: code/game/objects/items/dice.dm:14
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_dice_d4>(this);
        Dm.@new<obj_item_dice_d6>(this);
        Dm.@new<obj_item_dice_d8>(this);
        Dm.@new<obj_item_dice_d10>(this);
        Dm.@new<obj_item_dice_d12>(this);
        Dm.@new<obj_item_dice_d20>(this);
        object picked = pick(Dm.list(
            typeof(obj_item_dice_d1),
            typeof(obj_item_dice_d2),
            typeof(obj_item_dice_fudge),
            typeof(obj_item_dice_d6_space),
            typeof(obj_item_dice_d00),
            typeof(obj_item_dice_eightbd20),
            typeof(obj_item_dice_fourdd6),
            typeof(obj_item_dice_d100)
        ));
        Dm.@new((Type)picked, this);
    }

    // tg: code/game/objects/items/dice.dm:33
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is gambling with death! It looks like [] trying to commit suicide!", user, user.p_theyre())));
        return OXYLOSS;
    }
}

// tg: code/game/objects/items/dice.dm:37
public partial class obj_item_storage_dice_hazard : obj_item_storage_dice
{
    // tg: code/game/objects/items/dice.dm:37
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_dice_d6>(this);
        Dm.@new<obj_item_dice_d6>(this);
        Dm.@new<obj_item_dice_d6>(this);
        for (float i = 1, i__end = 2; i <= i__end; i++)
        {
            if (prob(7))
                Dm.@new<obj_item_dice_d6_ebony>(this);
            else
                Dm.@new<obj_item_dice_d6>(this);
        }
    }
}

///this is a prototype for dice, for a real d6 use "/obj/item/dice/d6"
// tg: code/game/objects/items/dice.dm:48
public partial class obj_item_dice : obj_item
{
    // don't produce a comment if the dice has less than this many sides
    // so you don't have d1's and d4's constantly producing comments
    // tg: code/game/objects/items/dice.dm:3  #define MIN_SIDES_ALERT 5 (#undef'd at the file's end)
    private const float MIN_SIDES_ALERT = 5;

    // tg: code/game/objects/items/dice.dm:62
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(result))
            result = roll(sides);
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/dice.dm:68
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        diceroll(user, in_hand: TRUE);
        return null;
    }

    // tg: code/game/objects/items/dice.dm:71
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        mob thrown_by = throwingdatum?.get_thrower() as mob;
        if (thrown_by != null) // istype(thrown_by)
            diceroll(thrown_by);
        return base.throw_impact(hit_atom, throwingdatum, __extra);
    }

    // tg: code/game/objects/items/dice.dm:77
    public virtual object diceroll(mob user = null, float? in_hand = null)
    {
        in_hand ??= FALSE;
        object dot = null;
        result = roll(sides);
        if (rigged != DICE_NOT_RIGGED && !Dm.eq(result, rigged_value))
        {
            if (rigged == DICE_BASICALLY_RIGGED && prob(clamp(Dm.div(1, sides - 1) * 100, 25, 80)))
                result = rigged_value;
            else if (rigged == DICE_TOTALLY_RIGGED)
                result = rigged_value;
        }

        dot = result;
        playsound(this, Dm.resource("sound/items/dice_roll.ogg"), 50, TRUE);

        float fake_result = roll(sides); //Daredevil isn't as good as he used to be
        object comment = "";
        if (sides > MIN_SIDES_ALERT && Dm.eq(result, 1))  // less comment spam
            comment = I18n.T("Ouch, bad luck.");
        if (sides == 20 && Dm.eq(result, 20))
            comment = I18n.T("NAT 20!");
        update_appearance();
        result = manipulate_result(result);
        if (special_faces.len == sides)
        {
            comment = "";  // its not a number
            result = special_faces[result];
            if (!(isnum(result) && round(Dm.num(result)) == Dm.num(result))) // !ISINTEGER(result) (maths.dm:94); DM's round of a text is not the text
                comment = special_faces[result];  // should be a str now
        }

        if (Dm.truthy(in_hand)) //Dice was rolled in someone's hand
        {
            user.visible_message(
                span_notice(I18n.T("[] rolls []. It lands on []. []", user, this, result, comment)),
                span_notice(I18n.T("You roll []. It lands on []. []", this, result, comment)),
                span_hear(I18n.T("You hear [] rolling, it sounds like a [].", this, fake_result)));
        }
        else
            visible_message(span_notice(I18n.T("[] rolls to a stop, landing on []. []", this, result, comment)));

        return dot;
    }

    // tg: code/game/objects/items/dice.dm:114
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        Dm.append(ref dot, Dm.text("[]-[]", icon_state, result));
        return dot;
    }

    // tg: code/game/objects/items/dice.dm:118
    public override object microwave_act(obj_machinery_microwave microwave_source = null, mob microwaver = null, object randomize_pixel_offset = null)
    {
        if (Dm.truthy(microwave_riggable))
        {
            rigged = DICE_BASICALLY_RIGGED;
            rigged_value = result;
        }

        return Dm.bitor(base.microwave_act(microwave_source, microwaver, randomize_pixel_offset), COMPONENT_MICROWAVE_SUCCESS);
    }

    /// A proc to modify the displayed result. (Does not affect what the icon_state is passed.)
    // tg: code/game/objects/items/dice.dm:126
    public virtual object manipulate_result(object original = null)
    {
        return original;
    }

    // tg: code/game/objects/items/dice.dm:129
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is gambling with death! It looks like [] trying to commit suicide!", user, user.p_theyre())));
        return OXYLOSS;
    }
}

// tg: code/game/objects/items/dice.dm:145
public partial class obj_item_dice_d4 : obj_item_dice
{
    // tg: code/game/objects/items/dice.dm:151
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // 1d4 damage
        Waiting.NoSleep(AddComponent(typeof(datum_component_caltrop), Dm.named("min_damage", 1), Dm.named("max_damage", 4)));
        return dot;
    }
}

// tg: code/game/objects/items/dice.dm:165
public partial class obj_item_dice_d6_space : obj_item_dice_d6
{
    // tg: code/game/objects/items/dice.dm:170
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (prob(10))
            name = I18n.T("spess cube");
        return dot;
    }
}

// tg: code/game/objects/items/dice.dm:212
public partial class obj_item_dice_d00 : obj_item_dice
{
    // tg: code/game/objects/items/dice.dm:218
    public override object manipulate_result(object original = null)
    {
        return (Dm.num(original) - 1) * 10;  // 10, 20, 30, etc
    }
}

// tg: code/game/objects/items/dice.dm:233
public partial class obj_item_dice_d100 : obj_item_dice
{
    // tg: code/game/objects/items/dice.dm:240
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_blocker)));
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/game/objects/items/dice.dm:244
public partial class obj_item_dice_eightbd20 : obj_item_dice
{
    // tg: code/game/objects/items/dice.dm:251
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_blocker)));
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/game/objects/items/dice.dm:255
public partial class obj_item_dice_fourdd6 : obj_item_dice
{
    // tg: code/game/objects/items/dice.dm:262
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_blocker)));
        return base.Initialize(mapload, __extra);
    }
}
