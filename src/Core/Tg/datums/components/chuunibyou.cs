// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/chuunibyou.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SL1 (D333): the whole file (the chuunibyou manga granter's reader). Every spell the parent casts is shouted with a dumb
// invocation, its projectiles turn pink, and a cast heals a little (not the manga's no_healing). The var blocks are generated (D111).

/**
 * ## chuunibyou component!
 *
 * Component that makes casted spells always a shout invocation, a very dumb one. And their projectiles are dumb too.
 * Oh, but it does heal after each spell cast.
 */
// tg: code/datums/components/chuunibyou.dm:13
public partial class datum_component_chuunibyou : datum_component
{
    /// how much health healed from casting a chuuni spell
    // tg: code/datums/components/chuunibyou.dm:3  #define CHUUNIBYOU_HEAL_AMOUNT 3 (file-local, #undef at :111; the var block's default)
    ///cooldown between healing to prevent stuff like instant blink spell spam healing
    // tg: code/datums/components/chuunibyou.dm:5  #define CHUUNIBYOU_COOLDOWN_TIME 5 SECONDS (file-local, #undef at :112)
    private const float CHUUNIBYOU_COOLDOWN_TIME = 5 * SECONDS;

    /// invocations per school the spell is from
    // tg: code/datums/components/chuunibyou.dm:15  var/static/list/chuunibyou_invocations (a TgState field, rules 10.6)
    private static ref DmList chuunibyou_invocations => ref TgState.Current.datum_component_chuunibyou__chuunibyou_invocations;

    // tg: code/datums/components/chuunibyou.dm:23
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;
        if (!Dm.truthy(chuunibyou_invocations))
        {
            chuunibyou_invocations = Dm.list(
                Dm.kv(SCHOOL_UNSET, I18n.T("This is embarrassing... I can't remember the words... um... maybe if I just wave my hand like this... no, that's not wor- Ah! There it goes!")),
                Dm.kv(SCHOOL_HOLY, I18n.T("By the grace of the holy one, I summon the light of salvation. Let my allies rejoice. O, Heaven! Bless them!")),
                Dm.kv(SCHOOL_PSYCHIC, I18n.T("By the secret of the hidden one, I reveal the truth of creation. Let my mind expand. O, Mystery! Enlighten me!")),
                Dm.kv(SCHOOL_MIME, I18n.T("O, Silence! Embrace my soul and amplify my gesture. Let me create the illusion and manipulate the perception!")),
                Dm.kv(SCHOOL_RESTORATION, I18n.T("I invoke the name of the goddess of mercy, hear my plea and grant your blessing to this soul! Divine Grace!")),

                Dm.kv(SCHOOL_EVOCATION, I18n.T("Behold, the ultimate power of the Dark Flame Master! I call upon the ancient forces of chaos and destruction to unleash their wrath upon my enemies!")),
                Dm.kv(SCHOOL_TRANSMUTATION, I18n.T("I invoke the law of equivalent exchange, the balance of the cosmos. As I offer this sacrifice, I demand a new creation. Reveal, the mystery of transmutation!")),
                Dm.kv(SCHOOL_TRANSLOCATION, I18n.T("By the power of the spatial rifts, I bend the fabric of reality and move across the dimensions! Let nothing stand in my way as I travel to my destination!")),
                Dm.kv(SCHOOL_CONJURATION, I18n.T("With the eye of fate, I see through the threads of destiny. Nothing can hide from me. Witness me, witness the miracle of manifestation!")),

                Dm.kv(SCHOOL_NECROMANCY, I18n.T("I am the Lord of the Dead, the Master of Bones, the Ruler of Shadows. I command the legions of the damned to rise from their graves and serve me!")),
                Dm.kv(SCHOOL_FORBIDDEN, I18n.T("I renounce the laws of this world and embrace the chaos of the old gods! Let the forbidden power flow through me and destroy everything in its path!")),
                Dm.kv(SCHOOL_SANGUINE, I18n.T("I cover my eye with an eyepatch to seal my true power, but now I will unleash it upon you. I feast on the life force of my prey and grow stronger with every drop!")));
        }
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/datums/components/chuunibyou.dm:44
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        RegisterSignal(parent, COMSIG_MOB_SPELL_PROJECTILE, nameof(on_spell_projectile));
        RegisterSignal(parent, COMSIG_MOB_PRE_INVOCATION, nameof(on_pre_invocation));
        RegisterSignal(parent, COMSIG_MOB_TRY_SPEECH, nameof(on_try_speech));
        RegisterSignal(parent, COMSIG_MOB_AFTER_SPELL_CAST, nameof(on_after_spell_cast));
        ADD_TRAIT(parent, TRAIT_CHUUNIBYOU, REF(this));
    }

    // tg: code/datums/components/chuunibyou.dm:52
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await base.UnregisterFromParent(__extra);
        UnregisterSignal(parent, Dm.list(
            COMSIG_MOB_SPELL_PROJECTILE,
            COMSIG_MOB_PRE_INVOCATION,
            COMSIG_MOB_TRY_SPEECH,
            COMSIG_MOB_AFTER_SPELL_CAST));
        REMOVE_TRAIT(parent, TRAIT_CHUUNIBYOU, REF(this));
    }

    /// signal sent when the parent tries to speak. we let speech pass if we are casting a spell so mimes still chuuni their spellcasts
    /// (this may end in the mime dying)
    // tg: code/datums/components/chuunibyou.dm:64
    [SignalHandler]
    public virtual object on_try_speech(datum source = null, object message = null, object ignore_spam = null, object forced = null)
    {
        // HAS_TRAIT(src, TRAIT_MUTE) reads the component's own traits (tg's; kept): it is never true
        if (Dm.truthy(casting_spell) && !HAS_TRAIT(this, TRAIT_MUTE))
            return COMPONENT_IGNORE_CAN_SPEAK;
        return null;
    }

    ///signal sent when the parent casts a spell that has a projectile
    // tg: code/datums/components/chuunibyou.dm:71
    [SignalHandler]
    public virtual void on_spell_projectile(mob_living source = null, datum_action_cooldown_spell spell = null, atom cast_on = null, obj_projectile to_fire = null)
    {
        playsound(to_fire, Dm.resource("sound/effects/magic/staff_change.ogg"), 75, TRUE);
        to_fire.color = "#f825f8";
        to_fire.name = Dm.text("chuuni-[]", to_fire.name);
        Waiting.NoSleep(to_fire.set_light(2, 2, LIGHT_COLOR_PINK, l_on: TRUE));
    }

    ///signal sent before parent invokes a spell
    // tg: code/datums/components/chuunibyou.dm:80
    [SignalHandler]
    public virtual void on_pre_invocation(mob_living source = null, datum_action_cooldown_spell spell = null, DmList invocation_list = null)
    {
        // this makes it bypass speech checks (being a mime) until the spell is done casting
        // this lets mimes cast with it, but, um... might get them lynched
        casting_spell = TRUE;
        invocation_list[INVOCATION_TYPE] = INVOCATION_SHOUT;
        invocation_list[INVOCATION_GARBLE_PROB] = 0;
        object chuuni_invocation = chuunibyou_invocations[spell.school];
        if (!Dm.truthy(chuuni_invocation)) // someone forgot to update the CHUUNI LIST to include a desc for the new school
        {
            stack_trace(Dm.text("Chunnibyou invocations is missing a line for spell school \"[]\"", spell.school), "code/datums/components/chuunibyou.dm", 90);
            chuuni_invocation = chuunibyou_invocations[SCHOOL_UNSET];
        }
        invocation_list[INVOCATION_MESSAGE] = chuuni_invocation;
    }

    ///signal sent after parent casts a spell
    // tg: code/datums/components/chuunibyou.dm:95
    [SignalHandler]
    public virtual void on_after_spell_cast(mob_living source = null, datum_action_cooldown_spell spell = null, atom cast_on = null)
    {
        casting_spell = FALSE;
        if (!(heal_cooldown <= world.time)) // COOLDOWN_FINISHED(src, heal_cooldown), inline (rules 11.1 (a))
        {
            to_chat(source, span_danger(I18n.T("Your chuuni powers are on cooldown! You fail to heal!")));
            return;
        }
        heal_cooldown = world.time + CHUUNIBYOU_COOLDOWN_TIME; // COOLDOWN_START(src, heal_cooldown, CHUUNIBYOU_COOLDOWN_TIME)

        Waiting.NoSleep(source.heal_overall_damage(heal_amount));
        playsound(source, Dm.resource("sound/effects/magic/staff_healing.ogg"), 30);
        to_chat(source, span_danger(I18n.T("You feel slightly healed by your chuuni powers.")));
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/chuunibyou.dm:15  the component's var/static/list/chuunibyou_invocations (rules 10.6)
    internal DmList datum_component_chuunibyou__chuunibyou_invocations;
}
