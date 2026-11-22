// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/spooky.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI6c (D326): the whole file (the spectral instruments' doot). The var block is generated (D111).

// tg: code/datums/elements/spooky.dm:1
public partial class datum_element_spooky : datum_element
{
    // tg: code/datums/elements/spooky.dm:11
    [DmParams("target", "too_spooky", "single_use", "stam_damage_mult")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object too_spooky = Dm.arg(__extra, 0) ?? TRUE;
        object single_use = Dm.arg(__extra, 1) ?? FALSE;
        object stam_damage_mult = Dm.arg(__extra, 2) ?? 1;
        object dot = await base.Attach(target, __extra);
        if (!isitem(target))
            return ELEMENT_INCOMPATIBLE;
        this.too_spooky = Dm.num(too_spooky);
        this.single_use = Dm.num(single_use);
        this.stam_damage_mult = stam_damage_mult;
        RegisterSignal(target, COMSIG_ITEM_ATTACK, nameof(spectral_attack));
        return dot;
    }

    // tg: code/datums/elements/spooky.dm:20
    public override object Detach(datum target = null, params object[] __extra)
    {
        UnregisterSignal(target, COMSIG_ITEM_ATTACK);
        return base.Detach(target, __extra);
    }

    // tg: code/datums/elements/spooky.dm:24
    public virtual void spectral_attack(datum source = null, mob_living_carbon target = null, mob user = null)
    {
        if (!HAS_TRAIT(user, TRAIT_SPOOKY_INSTRUMENT_PLAYER)) //this weapon wasn't meant for mortals.
        {
            if (Dm.truthy(rattle_bones(user as mob_living, Dm.num(stam_damage_mult) * 2)))
            {
                to_chat(user, span_userdanger(I18n.T("Your ears weren't meant for this spectral sound.")));
                if (ishuman(user))
                    Waiting.Detach(spectral_change((mob_living_carbon_human)user, user as mob_living, source as obj_item));
            }
            return;
        }

        to_chat(target, span_userdanger(I18n.T("<b>DOOT</b")));

        if (Dm.truthy(rattle_bones(target)))
            Waiting.Detach(spectral_change(target as mob_living_carbon_human, user as mob_living, source as obj_item));
    }

    ///Cause jitteriness and stamina to the target relative to the amount of their bodyparts made of flesh and bone.
    // tg: code/datums/elements/spooky.dm:40
    public virtual object rattle_bones(mob_living target = null, object stam_dam_mult = null)
    {
        stam_dam_mult ??= stam_damage_mult;
        if (HAS_TRAIT(target, TRAIT_SPOOKY_INSTRUMENT_PLAYER))
            return 0; //those who can play spooky instruments are unaffected by the spook-pocalypse. No redundant skeletonization or bad mood event.

        Waiting.NoSleep(target.add_mood_event("spooked", typeof(datum_mood_event_spooked)));

        if (!ishuman(target))//the sound will spook basic mobs.
        {
            Waiting.NoSleep(target.set_jitter_if_lower(30 * SECONDS));
            Waiting.NoSleep(target.set_stutter(40 * SECONDS));
            return 0;
        }

        int bone_amount = 0;
        foreach (obj_item_bodypart part in Dm.each<obj_item_bodypart>(target.get_bodyparts()))
        {
            if (Dm.eq(Dm.bitand(part.biological_state, BIO_FLESH_BONE), BIO_FLESH_BONE))
                bone_amount++;
        }
        if (bone_amount > 0)
        {
            Waiting.NoSleep(target.set_jitter_if_lower(12 * SECONDS * bone_amount));
            Waiting.NoSleep(target.set_stutter(6.5f * SECONDS * bone_amount));
            Waiting.NoSleep(target.adjust_stamina_loss(3 * bone_amount * Dm.num(stam_dam_mult)));
        }
        if (Dm.truthy(Dm.bitand(target.mob_biotypes, MOB_UNDEAD)))
        {
            Waiting.NoSleep(target.adjust_stamina_loss(25));
            Waiting.NoSleep(target.Paralyze(15)); //the rest of the undead can't resist the doot
        }

        return bone_amount;
    }

    // tg: code/datums/elements/spooky.dm:65
    public virtual async DmTask spectral_change(mob_living_carbon_human human = null, mob_living user = null, obj_item source = null)
    {
        if (Dm.num(human.get_stamina_loss()) <= 95)
            return;

        if (Dm.truthy(single_use))
        {
            to_chat(user, span_warning(I18n.T("You feel like [] has lost its spookiness...", source)));
            Detach(source);
        }

        await human.Paralyze(2 * SECONDS);
        human.set_species(typeof(datum_species_skeleton));
        human.visible_message(span_warning(I18n.T("[] has given up on life as a mortal.", human)));
        to_chat(human, span_boldnotice(I18n.T("You are a spooky skeleton!")));
        to_chat(human,
            span_boldnotice(I18n.T("A new life and identity has begun.[] You haven't forgotten your past life, and are still beholden to past loyalties.", Dm.truthy(too_spooky) ? " Help your fellow skeletons into bringing out the spooky-pocalypse." : "")));
        Waiting.Detach(change_name(human)); //time for a new name!

        if (!Dm.truthy(too_spooky))
            return;
        turf turf = get_turf(human);
        if (!prob(90))
        {
            to_chat(human, span_boldwarning(I18n.T("The spooky gods forgot to ship your instrument. Better luck next unlife.")));
            return;
        }
        var instrument = (Type)pick(typeof(obj_item_instrument_saxophone_spectral), typeof(obj_item_instrument_trumpet_spectral), typeof(obj_item_instrument_trombone_spectral));
        Dm.@new(instrument, turf);
    }

    // tg: code/datums/elements/spooky.dm:97
    public virtual async DmTask change_name(mob_living_carbon_human spooked = null)
    {
        object skeleton_name = spooked.client != null ? await sanitize_name(await tgui_input_text(spooked, I18n.T("Enter your new skeleton name"), I18n.T("Spookifier"), spooked.real_name, MAX_NAME_LEN)) : null;
        if (!Dm.truthy(skeleton_name))
            skeleton_name = "spooky skeleton"; // "\improper spooky skeleton"
        await spooked.fully_replace_character_name(null, skeleton_name, log_new_name: TRUE);
    }
}
