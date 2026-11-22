// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/head/tinfoilhat.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CL1 (D333): the whole file (the tin foil hat's mind shielding anti-magic with six charges, the conspiracies phobia it gives
// its wearer, the warp when it is used up or microwaved, its suicide). The var blocks are generated (D111).

// tg: code/modules/clothing/head/tinfoilhat.dm:1
public partial class obj_item_clothing_head_costume_foilhat : obj_item_clothing_head_costume
{
    // tg: code/modules/clothing/head/tinfoilhat.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(warped))
        {
            warp_up();
            return dot;
        }

        Waiting.NoSleep(AddComponent(
            typeof(datum_component_anti_magic),
            Dm.named("antimagic_flags", MAGIC_RESISTANCE_MIND),
            Dm.named("inventory_flags", ITEM_SLOT_HEAD),
            Dm.named("charges", 6),
            Dm.named("block_magic", Dm.@new<datum_callback>(this, nameof(drain_antimagic))),
            Dm.named("expiration", Dm.@new<datum_callback>(this, nameof(warp_up)))
        ));
        return dot;
    }

    // tg: code/modules/clothing/head/tinfoilhat.dm:36
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (!Dm.truthy(Dm.bitand(slot, ITEM_SLOT_HEAD)) || Dm.truthy(warped))
            return;
        if (paranoia != null)
            QDEL_NULL(ref paranoia);
        paranoia = Dm.@new<datum_brain_trauma_mild_phobia_conspiracies>();

        RegisterSignal(user, COMSIG_LIVING_SUICIDE_ACT, nameof(call_suicide));

        await ((mob_living_carbon)user).gain_trauma(paranoia, TRAUMA_RESILIENCE_MAGIC);
        to_chat(user, span_warning(I18n.T("As you don the foiled hat, an entire world of conspiracy theories and seemingly insane ideas suddenly rush into your mind. What you once thought unbelievable suddenly seems.. undeniable. Everything is connected and nothing happens just by accident. You know too much and now they're out to get you. ")));
    }

    // tg: code/modules/clothing/head/tinfoilhat.dm:49
    [DmParams("over_object", "user")]
    public override async DmTask<object> mouse_drop_dragged(atom over = null, mob user = null, object src_location = null, object over_location = null, object @params = null)
    {
        //God Im sorry
        if (!Dm.truthy(warped) && user.get_item_by_slot(ITEM_SLOT_HEAD) == this)
        {
            to_chat(user, span_userdanger(I18n.T("Why would you want to take this off? Do you want them to get into your mind?!")));
            return null;
        }
        return await base.mouse_drop_dragged(over, user, src_location, over_location, @params);
    }

    // tg: code/modules/clothing/head/tinfoilhat.dm:56
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        if (paranoia != null)
            QDEL_NULL(ref paranoia);
        UnregisterSignal(user, COMSIG_LIVING_SUICIDE_ACT);
        return dot;
    }

    // tg: code/modules/clothing/head/tinfoilhat.dm:63
    public virtual void drain_antimagic(mob user = null)
    {
        to_chat(user, span_warning(I18n.T("[] crumples slightly. Something is trying to get inside your mind!", this)));
    }

    // tg: code/modules/clothing/head/tinfoilhat.dm:66
    public virtual void warp_up()
    {
        name = I18n.T("scorched tinfoil hat");
        desc = I18n.T("A badly warped up hat. Quite unlikely this will still work against any of the fictional or real dangers it used to.");
        warped = TRUE;
        if (!isliving(loc) || paranoia == null)
            return;
        mob_living target = (mob_living)loc;
        UnregisterSignal(target, COMSIG_LIVING_SUICIDE_ACT);
        if (target.get_item_by_slot(ITEM_SLOT_HEAD) != this)
            return;
        QDEL_NULL(ref paranoia);
        if (!IS_UNCONSCIOUS(target))
            to_chat(target, span_warning(I18n.T("Your zealous conspirationism rapidly dissipates as the donned hat warps up into a ruined mess. All those theories starting to sound like nothing but a ridicolous fanfare.")));
    }

    // tg: code/modules/clothing/head/tinfoilhat.dm:80
    public override object can_throw_equip(atom hit_atom = null)
    {
        return warped;
    }

    // tg: code/modules/clothing/head/tinfoilhat.dm:83
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(warped) && user.get_item_by_slot(ITEM_SLOT_HEAD) == this)
        {
            to_chat(user, span_userdanger(I18n.T("Why would you want to take this off? Do you want them to get into your mind?!")));
            return null;
        }
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/clothing/head/tinfoilhat.dm:89
    public override object microwave_act(obj_machinery_microwave microwave_source = null, mob microwaver = null, object randomize_pixel_offset = null)
    {
        object dot = base.microwave_act(microwave_source, microwaver, randomize_pixel_offset);
        if (Dm.truthy(warped))
            return dot;

        warp_up();
        return Dm.bitor(dot, COMPONENT_MICROWAVE_SUCCESS);
    }

    // tg: code/modules/clothing/head/tinfoilhat.dm:97
    [SignalHandler]
    public virtual object call_suicide(datum source = null)
    {
        Waiting.Detach(suicide_act(source as mob_living)); //SIGNAL_HANDLER doesn't like things waiting; INVOKE_ASYNC bypasses that
        return OXYLOSS;
    }

    // tg: code/modules/clothing/head/tinfoilhat.dm:102
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] gets a crazed look in [] eyes! [] [] witnessed the truth, and try to commit suicide!", user, user.p_their(), capitalize(user.p_they()), user.p_have())));
        // tg: code/modules/clothing/head/tinfoilhat.dm:104  var/static/list/conspiracy_line (a proc-level static, rules 10.6)
        ref DmList conspiracy_line = ref TgState.Current.obj_item_clothing_head_costume_foilhat__suicide_act__conspiracy_line;
        conspiracy_line ??= Dm.list(
            I18n.T(";THEY'RE HIDING CAMERAS IN THE CEILINGS! THEY WITNESS EVERYTHING WE DO!!"),
            I18n.T(";HOW CAN I LIVE IN A WORLD WHERE MY FATE AND EXISTENCE IS DECIDED BY A GROUP OF INDIVIDUALS?!!"),
            I18n.T(";THEY'RE TOYING WITH ALL OF YOUR MINDS AND TREATING YOU AS EXPERIMENTS!!"),
            I18n.T(";THEY HIRE ASSISTANTS WITHOUT DOING BACKGROUND CHECKS!!"),
            I18n.T(";WE LIVE IN A ZOO AND WE ARE THE ONES BEING OBSERVED!!"),
            I18n.T(";WE REPEAT OUR LIVES DAILY WITHOUT FURTHER QUESTIONS!!")
        );
        await user.say(pick(conspiracy_line), forced: type);
        obj_item_organ_brain brain = user.get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;
        if (brain != null)
            await brain.set_organ_damage(BRAIN_DAMAGE_DEATH);
        return OXYLOSS;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/clothing/head/tinfoilhat.dm:104  /obj/item/clothing/head/costume/foilhat/suicide_act var/static/list/conspiracy_line
    internal DmList obj_item_clothing_head_costume_foilhat__suicide_act__conspiracy_line;
}
