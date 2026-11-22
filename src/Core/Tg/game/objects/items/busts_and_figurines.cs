// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/busts_and_figurines.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps OD2 (D333): the file whole. The marble bust is art and beauty. The Hippocrates bust gives a medical HUD while held, and in
// hand its holder swears the Hippocratic Oath in four spoken lines (pacifism) or forswears it, once per five minutes; a slip in the
// oath bruises the brain. One in a hundred (every evil one) is the blood-caked Solemn Vow, which attacks slower and takes no oath.
// The Maneki-Neko is a white, gold or dark figurine whose beckoning paw ("maneki-neko-overlay") keeps its own colour. The var blocks
// are generated (D111).

// tg: code/game/objects/items/busts_and_figurines.dm:1
public partial class obj_item_statuebust : obj_item
{
    // tg: code/game/objects/items/busts_and_figurines.dm:14
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_art), impressiveness));
        Waiting.NoSleep(AddElement(typeof(datum_element_beauty), 1000));
        return dot;
    }
}

// tg: code/game/objects/items/busts_and_figurines.dm:19
public partial class obj_item_statuebust_hippocratic : obj_item_statuebust
{
    // tg: code/game/objects/items/busts_and_figurines.dm:34
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (prob(reference_chance))
        {
            name = I18n.T("Solemn Vow");
            desc = I18n.T("Art lovers will cherish the bust of Hippocrates, commemorating a time when medics still thought doing no harm was a good idea.");
            attack_speed = CLICK_CD_SLOW;
            reference = TRUE;
        }
        return dot;
    }

    // tg: code/game/objects/items/busts_and_figurines.dm:42
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(reference))
        {
            Dm.append(ref dot, span_notice(I18n.T("You could activate the bust in-hand to swear or forswear a Hippocratic Oath... but it seems like somebody decided it was more of a Hippocratic Suggestion. This thing is caked with bits of blood and gore.")));
            return dot;
        }
        Dm.append(ref dot, span_notice(I18n.T("You can activate the bust in-hand to swear or forswear a Hippocratic Oath! This has no effects except pacifism or bragging rights. Does not remove other sources of pacifism. Do not eat.")));
        return dot;
    }

    // tg: code/game/objects/items/busts_and_figurines.dm:49
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (!Dm.truthy(Dm.bitand(slot, ITEM_SLOT_HANDS)))
            return;
        ADD_TRAIT(user, TRAIT_MEDICAL_HUD, type);
    }

    // tg: code/game/objects/items/busts_and_figurines.dm:55
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        await base.dropped(user, silent, __extra);
        if (HAS_TRAIT_NOT_FROM(user, TRAIT_MEDICAL_HUD, type))
            return null;
        REMOVE_TRAIT(user, TRAIT_MEDICAL_HUD, type);
        return null;
    }

    // tg: code/game/objects/items/busts_and_figurines.dm:61
    [DmParams("user")]
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!iscarbon(user))
        {
            to_chat(user, span_warning(I18n.T("You remember how the Hippocratic Oath specifies 'my fellow human beings' and realize that it's completely meaningless to you.")));
            return null;
        }

        if (Dm.truthy(reference))
        {
            to_chat(user, span_warning(I18n.T("As you prepare yourself to swear the Oath, you realize that doing so on a blood-caked bust is probably not a good idea.")));
            return null;
        }

        if (!(oath_cd <= world.time)) // COOLDOWN_FINISHED
        {
            to_chat(user, span_warning(I18n.T("You've sworn or forsworn an oath too recently to undo your decisions. The bust looks at you with disgust.")));
            return null;
        }

        oath_cd = world.time + 5 * MINUTES; // COOLDOWN_START

        if (HAS_TRAIT_FROM(user, TRAIT_PACIFISM, type))
        {
            to_chat(user, span_warning(I18n.T("You've already sworn a vow. You start preparing to rescind it...")));
            if (Dm.truthy(await do_after(user, 5 * SECONDS, target: user)))
            {
                await user.say(I18n.T("Yeah this Hippopotamus thing isn't working out. I quit!"), forced: "hippocratic hippocrisy");
                REMOVE_TRAIT(user, TRAIT_PACIFISM, type);
            }
        }

        // they can still do it for rp purposes
        if (HAS_TRAIT_NOT_FROM(user, TRAIT_PACIFISM, type))
            to_chat(user, span_warning(I18n.T("You already don't want to harm people, this isn't going to do anything!")));


        to_chat(user, span_notice(I18n.T("You remind yourself of the Hippocratic Oath's contents and prepare to swear yourself to it...")));
        if (Dm.truthy(await do_after(user, 4 * SECONDS, target: user)))
            await user.say(I18n.T("I swear to fulfill, to the best of my ability and judgment, this covenant:"), forced: "hippocratic oath");
        else
        {
            await fuck_it_up(user as mob_living_carbon);
            return null;
        }
        if (Dm.truthy(await do_after(user, 2 * SECONDS, target: user)))
            await user.say(I18n.T("I will apply, for the benefit of the sick, all measures that are required, avoiding those twin traps of overtreatment and therapeutic nihilism."), forced: "hippocratic oath");
        else
        {
            await fuck_it_up(user as mob_living_carbon);
            return null;
        }
        if (Dm.truthy(await do_after(user, 3 * SECONDS, target: user)))
            await user.say(I18n.T("I will remember that I remain a member of society, with special obligations to all my fellow human beings, those sound of mind and body as well as the infirm."), forced: "hippocratic oath");
        else
        {

            await fuck_it_up(user as mob_living_carbon);
            return null;
        }
        if (Dm.truthy(await do_after(user, 3 * SECONDS, target: user)))
            await user.say(I18n.T("If I do not violate this oath, may I enjoy life and art, respected while I live and remembered with affection thereafter. May I always act so as to preserve the finest traditions of my calling and may I long experience the joy of healing those who seek my help."), forced: "hippocratic oath");
        else
        {
            await fuck_it_up(user as mob_living_carbon);
            return null;
        }

        to_chat(user, span_notice(I18n.T("Contentment, understanding, and purpose washes over you as you finish the oath. You consider for a second the concept of harm and shudder.")));
        ADD_TRAIT(user, TRAIT_PACIFISM, type);
        return null;
    }

    // Bully the guy for fucking up.
    // tg: code/game/objects/items/busts_and_figurines.dm:110
    public virtual async DmTask fuck_it_up(mob_living_carbon user = null)
    {
        to_chat(user, span_warning(I18n.T("You forget what comes next like a dumbass. The Hippocrates bust looks down on you, disappointed.")));
        await user.adjust_organ_loss(ORGAN_SLOT_BRAIN, 2);
        oath_cd = 0; // COOLDOWN_RESET
    }
}

// tg: code/game/objects/items/busts_and_figurines.dm:115
public partial class obj_item_maneki_neko : obj_item
{
    // tg: code/game/objects/items/busts_and_figurines.dm:128
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        //Not compatible with greyscale configs because it's animated.
        Waiting.NoSleep(add_atom_colour(pick_weight(Dm.list(Dm.kv(COLOR_WHITE, 3), Dm.kv(COLOR_GOLD, 2), Dm.kv(COLOR_DARK, 1))), FIXED_COLOUR_PRIORITY));
        mutable_appearance neko_overlay = mutable_appearance(icon, "maneki-neko-overlay", appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)) as mutable_appearance;
        add_overlay(neko_overlay);
        Waiting.NoSleep(AddElement(typeof(datum_element_art), GOOD_ART));
        Waiting.NoSleep(AddElement(typeof(datum_element_beauty), 800));
        return dot;
    }
}
