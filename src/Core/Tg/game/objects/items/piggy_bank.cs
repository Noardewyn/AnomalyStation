// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/piggy_bank.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps OD2 (D333): the file whole. A piggy bank (one of six greyscale colours) takes cash, holochips and coins up to its maximum,
// rattles in hand to tell how full it is, shatters as a weapon or when broken, spilling its money, and squashes its chips into one
// past 35 items. The museum's Pigston Swinelord VI stays in its area; the vault piggy bank saves at most its shift's limit. Keeping
// the money between rounds (SSpersistence's load, save and broken-piggy queue) is owner-deferred (D333): those three calls are
// Dm.Deferred leaves (not-translated.md 218), so a persistent piggy bank starts a round with its initial money. The var block is
// generated (D111).

/**
 * Piggy banks. They store your hard-earned money until you or someone destroys it.
 * If the persistence id is set, money will be carried between rounds until broken.
 */
// tg: code/game/objects/items/piggy_bank.dm:5
public partial class obj_item_piggy_bank : obj_item
{
    private const int MAXIMUM_PIGGY_BANK_CONTENTS_LENGTH = 35; // tg: piggy_bank.dm:66 (file-local, #undef at :85)

    // tg: code/game/objects/items/piggy_bank.dm:28
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (!Dm.truthy(greyscale_colors))
        {
            greyscale_colors = pick(COLOR_PINK,
                COLOR_LIGHT_ORANGE,
                COLOR_GREEN_GRAY,
                COLOR_PALE_BLUE_GRAY,
                COLOR_DARK_MODERATE_LIME_GREEN,
                COLOR_OFF_WHITE
            );
        }

        object dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(AddElement(typeof(datum_element_can_shatter), Dm.named("shattering_sound", SFX_SHATTER), Dm.named("shatters_as_weapon", TRUE)));
        Waiting.NoSleep(AddElement(typeof(datum_element_beauty), 500));
        if (!Dm.truthy(persistence_id))
        {
            if (Dm.truthy(initial_value))
                Dm.@new<obj_item_holochip>(this, initial_value);
            return dot;
        }

        Dm.Deferred("code/game/objects/items/piggy_bank.dm:47", "Initialize: SSpersistence.load_piggy_bank, the money kept between rounds (persistence is owner-deferred, D333)");
        persistence_cb = Dm.@new<datum_callback>(this, nameof(save_cash));
        SSticker.OnRoundend(persistence_cb);

        if (Dm.truthy(initial_value) && initial_value + Dm.num(calculate_dosh_amount()) <= maximum_value)
        {
            obj_item_holochip chip = Dm.locate_type(typeof(obj_item_holochip), this) as obj_item_holochip;
            if (chip == null)
                Dm.@new<obj_item_holochip>(this, initial_value);
            else
            {
                chip.credits = Dm.num(chip.credits) + initial_value;
                chip.update_appearance();
            }
        }

        if (Dm.truthy(maximum_savings_per_shift))
            maximum_value = Dm.num(calculate_dosh_amount()) + maximum_savings_per_shift;
        return dot;
    }

    // tg: code/game/objects/items/piggy_bank.dm:62
    public virtual void save_cash()
    {
        sanitize_piggy_bank_contents_len();
        Dm.Deferred("code/game/objects/items/piggy_bank.dm:64", "save_cash: SSpersistence.save_piggy_bank, the money kept between rounds (persistence is owner-deferred, D333)");
    }

    ///This prevents the piggy bank from becoming laggy as hell if broken with hundred upon hundreds of chips inside it.
    // tg: code/game/objects/items/piggy_bank.dm:69
    public virtual void sanitize_piggy_bank_contents_len()
    {
        float contents_len = length(contents);
        if (contents_len <= MAXIMUM_PIGGY_BANK_CONTENTS_LENGTH)
            return;
        // that +1 is to make space for the holochip with the collected amount
        float iterations = contents_len + 1 - MAXIMUM_PIGGY_BANK_CONTENTS_LENGTH;
        float creds_amount = 0;
        for (float i = 1, i__end = iterations; i <= i__end; i++)
        {
            obj_item money = pick(contents) as obj_item;
            if (!(money is obj_item)) // Very barebone safety for somethig that shouldn't happen just in case
                continue; // Yes, this means we lose an iteration, the code is that simple.
            creds_amount += Dm.num(money.get_item_credit_value());
            qdel(money);
        }
        if (Dm.truthy(creds_amount))
            Dm.@new<obj_item_holochip>(this, creds_amount);
    }

    // tg: code/game/objects/items/piggy_bank.dm:87
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(persistence_cb))
        {
            LAZYREMOVE(ref SSticker.round_end_events, persistence_cb); //cleanup the callback.
            persistence_cb = null;
        }
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/piggy_bank.dm:93
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        foreach (obj_item thing in Dm.each_cast<obj_item>(contents))
            await thing.forceMove(loc);
        //Smashing the piggy after the round is over doesn't count.
        if (Dm.truthy(persistence_id) && SSticker.current_state < GAME_STATE_FINISHED)
            Dm.Deferred("code/game/objects/items/piggy_bank.dm:98", "atom_deconstruct: LAZYADD(SSpersistence.queued_broken_piggy_ids), a broken persistent piggy bank starting empty next round (persistence is owner-deferred, D333)");
        await base.atom_deconstruct(disassembled);
    }

    // tg: code/game/objects/items/piggy_bank.dm:101
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(user, this)))
            return dot;
        balloon_alert(user, I18n.T("rattle rattle..."));
        if (!Dm.truthy(await do_after(user, 0.5f * SECONDS, this)))
            return dot;
        float percentile = round(Dm.num(calculate_dosh_amount()) / maximum_value * 100, 1);
        if (percentile >= 10)
            playsound(this, SFX_RATTLE, percentile * 0.5f, FALSE, FALSE);
        float sw__1 = percentile;
        if (sw__1 == 0)
            balloon_alert(user, I18n.T("it's empty"));
        else if (sw__1 >= 1 && sw__1 <= 9)
            balloon_alert(user, I18n.T("it's almost empty"));
        else if (sw__1 >= 10 && sw__1 <= 25)
            balloon_alert(user, I18n.T("it's some cash"));
        else if (sw__1 >= 25 && sw__1 <= 45)
            balloon_alert(user, I18n.T("it's plenty of cash"));
        else if (sw__1 >= 45 && sw__1 <= 70)
            balloon_alert(user, I18n.T("it feels almost full"));
        else if (sw__1 >= 70 && sw__1 <= 95)
            balloon_alert(user, I18n.T("it feels full"));
        else if (sw__1 >= 95 && sw__1 <= INFINITY)
            balloon_alert(user, I18n.T("brimming with cash"));
        return dot;
    }

    // tg: code/game/objects/items/piggy_bank.dm:127
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object creds_value = tool.get_item_credit_value();
        if (creds_value == null)
            return NONE;

        float dosh_amount = Dm.num(calculate_dosh_amount());

        if (dosh_amount >= maximum_value)
        {
            balloon_alert(user, I18n.T("it's full!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (dosh_amount + Dm.num(creds_value) > maximum_value)
        {
            balloon_alert(user, I18n.T("too much cash!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
        {
            balloon_alert(user, I18n.T("stuck in your hands!"));
            return ITEM_INTERACT_BLOCKING;
        }

        balloon_alert(user, I18n.T("inserted [] creds", creds_value));
        sanitize_piggy_bank_contents_len();
        return ITEM_INTERACT_SUCCESS;
    }

    ///Returns the total amount of credits that its contents amount to.
    // tg: code/game/objects/items/piggy_bank.dm:151
    public virtual object calculate_dosh_amount()
    {
        float total_value = 0;
        foreach (obj_item item in Dm.each<obj_item>(contents))
            total_value += Dm.num(item.get_item_credit_value());
        return total_value;
    }
}

// tg: code/game/objects/items/piggy_bank.dm:157
public partial class obj_item_piggy_bank_museum : obj_item_piggy_bank
{
    // tg: code/game/objects/items/piggy_bank.dm:168
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_areabound))); //do not steal.
        return dot;
    }
}

// tg: code/game/objects/items/piggy_bank.dm:172
public partial class obj_item_piggy_bank_vault : obj_item_piggy_bank
{
    // tg: code/game/objects/items/piggy_bank.dm:184
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        //one piggy bank should exist, preferibly inside the vault's safe.
        // REGISTER_REQUIRED_MAP_ITEM(1, 1): the unit tests' map item check (UNIT_TESTS)
        return dot;
    }
}
