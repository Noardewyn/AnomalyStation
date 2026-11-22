// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/clown_items.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps J2, D298): the bike horn and its golden variant; the soaps (syndicate G3b: the syndicate soap with its kin; the omega
// soap's suicide makes tg's inert chrono field); the balloon mallet (gaps OD1, D333); the rest of the file is other packages'.

// tg: code/game/objects/items/clown_items.dm:196
public partial class obj_item_bikehorn : obj_item
{
    // tg: code/game/objects/items/clown_items.dm:216
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        DmList sound_list = Dm.list();
        sound_list[sound_file] = 1;
        Waiting.NoSleep(AddComponent(typeof(datum_component_squeak), sound_list, 50, Dm.named("falloff_exponent", 20)));
        return dot;
    }

    // tg: code/game/objects/items/clown_items.dm:222
    public override async DmTask<object> attack__proc(mob_living M = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (user != M && ishuman(user))
        {
            if (HAS_TRAIT(user, TRAIT_CLUMSY)) //only clowns can unlock its true powers
                await M.add_mood_event("honk", typeof(datum_mood_event_honk));
        }
        return await base.attack__proc(M, user, modifiers, attack_modifiers, __extra);
    }

    // tg: code/game/objects/items/clown_items.dm:229
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] solemnly points [] at [] temple! It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_theyre())));
        playsound(this, Dm.resource("sound/items/bikehorn.ogg"), 50, TRUE);
        return BRUTELOSS;
    }
}

//golden bikehorn
// tg: code/game/objects/items/clown_items.dm:254
public partial class obj_item_bikehorn_golden : obj_item_bikehorn
{
    // tg: code/game/objects/items/clown_items.dm:262
    public override async DmTask<object> attack__proc(mob_living M = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        await flip_mobs();
        return await base.attack__proc(M, user, modifiers, attack_modifiers, __extra);
    }

    // tg: code/game/objects/items/clown_items.dm:266
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await flip_mobs();
        await base.attack_self(user, modifiers, __extra);
        return null;
    }

    // tg: code/game/objects/items/clown_items.dm:270
    public virtual async DmTask flip_mobs(mob_living_carbon M = null, mob user = null)
    {
        if (!(golden_horn_cooldown <= world.time)) // COOLDOWN_FINISHED
            return;
        turf T = get_turf(this);
        foreach (mob_living_carbon hearer in Dm.each<mob_living_carbon>(Dm.sub(hearers(7, T), T)))
        {
            if (!HAS_TRAIT(hearer, TRAIT_DEAF))
                await hearer.emote("flip");
        }
        golden_horn_cooldown = world.time + 1 * SECONDS; // COOLDOWN_START
    }
}

/*
 * Soap
 */

// tg: code/game/objects/items/clown_items.dm:14
public partial class obj_item_soap : obj_item
{
    // tg: code/game/objects/items/clown_items.dm:33
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_slippery), 80));
        Waiting.NoSleep(AddComponent(typeof(datum_component_cleaner), cleanspeed, 0.1f, Dm.named("pre_clean_callback", Dm.@new<datum_callback>(this, nameof(should_clean))), Dm.named("on_cleaned_callback", Dm.@new<datum_callback>(this, nameof(decreaseUses))))); //less scaling for soapies
        return dot;
    }

    // tg: code/game/objects/items/clown_items.dm:38
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_lye), 10));
    }

    // tg: code/game/objects/items/clown_items.dm:41
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        float max_uses = Dm.initial(this, (obj_item_soap s) => s.uses);
        string msg = I18n.T("It looks like it just came out of the package.");
        if (uses != max_uses)
        {
            float percentage_left = uses / max_uses;
            if (percentage_left >= 0 && percentage_left <= 0.15f)
                msg = I18n.T("There's just a tiny bit left of what it used to be, you're not sure it'll last much longer.");
            else if (percentage_left >= 0.15f && percentage_left <= 0.30f)
                msg = I18n.T("It's dissolved quite a bit, but there's still some life to it.");
            else if (percentage_left >= 0.30f && percentage_left <= 0.50f)
                msg = I18n.T("It's past its prime, but it's definitely still good.");
            else if (percentage_left >= 0.50f && percentage_left <= 0.75f)
                msg = I18n.T("It's started to get a little smaller than it used to be, but it'll definitely still last for a while.");
            else
                msg = I18n.T("It's seen some light use, but it's still pretty fresh.");
        }
        dot.Add(span_notice(Dm.text("[]", msg)));
        return dot;
    }

    // tg: code/game/objects/items/clown_items.dm:146
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        await user.say(I18n.T(";FFFFFFFFFFFFFFFFUUUUUUUDGE!!"), forced: "soap suicide");
        user.visible_message(span_suicide(I18n.T("[] lifts [] to [] mouth and gnaws on it furiously, producing a thick froth! []'ll never get that BB gun now!", user, this, user.p_their(), user.p_They())));
        await do_foam(1, this, get_turf(user));
        return TOXLOSS;
    }

    // tg: code/game/objects/items/clown_items.dm:152
    public virtual object should_clean(datum cleaning_source = null, atom atom_to_clean = null, mob_living cleaner = null)
    {
        object dot = CLEAN_ALLOWED;
        if (!Dm.truthy(check_allowed_items(atom_to_clean)))
            dot = Dm.bitor(dot, Dm.bitor(CLEAN_NO_XP, CLEAN_NO_WASH));
        return dot;
    }

    /**
     * Decrease the number of uses the bar of soap has.
     *
     * The higher the cleaning skill, the less likely the soap will lose a use.
     * Arguments
     * * source - the source of the cleaning
     * * target - The atom that is being cleaned
     * * user - The mob that is using the soap to clean.
     */
    // tg: code/game/objects/items/clown_items.dm:166
    public virtual void decreaseUses(datum source = null, atom target = null, mob_living user = null, object clean_succeeded = null)
    {
        if (!Dm.truthy(clean_succeeded))
            return;
        object skillcheck = 1;
        if (Dm.truthy(user?.mind))
            skillcheck = user.mind.get_skill_modifier(typeof(datum_skill_cleaning), SKILL_SPEED_MODIFIER);
        if (prob(Dm.num(Dm.mul(skillcheck, 100)))) //higher level = more uses assuming RNG is nice
            uses--;
        if (uses <= 0)
            noUses(user);
    }

    // tg: code/game/objects/items/clown_items.dm:177
    public virtual void noUses(mob user = null)
    {
        to_chat(user, span_warning(I18n.T("[] crumbles into tiny bits!", this)));
        qdel(this);
    }
}

// tg: code/game/objects/items/clown_items.dm:60
public partial class obj_item_soap_homemade : obj_item_soap
{
    // tg: code/game/objects/items/clown_items.dm:67
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_consumable_liquidgibs), 9), Dm.kv(typeof(datum_reagent_lye), 9));
    }
}

// tg: code/game/objects/items/clown_items.dm:70
public partial class obj_item_soap_nanotrasen : obj_item_soap
{
    // tg: code/game/objects/items/clown_items.dm:78
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_toxin_plasma), 10), Dm.kv(typeof(datum_reagent_lye), 10));
    }
}

// tg: code/game/objects/items/clown_items.dm:181
public partial class obj_item_soap_nanotrasen_cyborg : obj_item_soap_nanotrasen
{
    // tg: code/game/objects/items/clown_items.dm:184
    public override void noUses(mob user = null)
    {
        to_chat(user, span_warning(I18n.T("[] has ran out of chemicals! Head to a recharger to refill it.", this)));
    }

    // tg: code/game/objects/items/clown_items.dm:187
    public override object should_clean(datum cleaning_source = null, atom atom_to_clean = null, mob_living cleaner = null)
    {
        if (Dm.num(uses) <= 0)
            return CLEAN_BLOCKED;
        return base.should_clean(cleaning_source, atom_to_clean, cleaner);
    }
}

// tg: code/game/objects/items/clown_items.dm:81
public partial class obj_item_soap_deluxe : obj_item_soap
{
    // tg: code/game/objects/items/clown_items.dm:88
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_consumable_aloejuice), 10), Dm.kv(typeof(datum_reagent_lye), 10));
    }
}

// tg: code/game/objects/items/clown_items.dm:91
public partial class obj_item_soap_syndie : obj_item_soap
{
    // tg: code/game/objects/items/clown_items.dm:98
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_toxin_acid), 10), Dm.kv(typeof(datum_reagent_lye), 10));
    }
}

// tg: code/game/objects/items/clown_items.dm:108
public partial class obj_item_soap_omega : obj_item_soap
{
    // tg: code/game/objects/items/clown_items.dm:117
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_slippery), 120)); //Same slipperiness as Super Lube
        return dot;
    }

    // tg: code/game/objects/items/clown_items.dm:121
    public override async DmTask<object> grind_results()
    {
        return Dm.list(
            Dm.kv(typeof(datum_reagent_consumable_potato_juice), 9),
            Dm.kv(typeof(datum_reagent_consumable_ethanol_lizardwine), 9),
            Dm.kv(typeof(datum_reagent_monkey_powder), 9),
            Dm.kv(typeof(datum_reagent_drug_krokodil), 9),
            Dm.kv(typeof(datum_reagent_toxin_acid_nitracid), 9),
            Dm.kv(typeof(datum_reagent_baldium), 9),
            Dm.kv(typeof(datum_reagent_consumable_ethanol_hooch), 9),
            Dm.kv(typeof(datum_reagent_bluespace), 9),
            Dm.kv(typeof(datum_reagent_drug_pumpup), 9),
            Dm.kv(typeof(datum_reagent_consumable_space_cola), 9),
            Dm.kv(typeof(datum_reagent_iron), 9));
    }

    // tg: code/game/objects/items/clown_items.dm:136
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is using [] to scrub themselves from the timeline! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
        await Dm.new_async<obj_structure_chrono_field>(user.loc, user);
        return MANUAL_SUICIDE;
    }
}

// tg: code/game/objects/items/clown_items.dm:305  (gaps OD1)
public partial class obj_item_balloon_mallet : obj_item
{
    // tg: code/game/objects/items/clown_items.dm:326
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (HAS_TRAIT(user, TRAIT_BALLOON_SUTRA))
            Dm.append(ref dot, I18n.T("A sacred weapon of the higher castes from the clown planet, used to strike fear into the hearts of their foes. Wield it with care."));
        return dot;
    }

    // tg: code/game/objects/items/clown_items.dm:331
    public override async DmTask<object> attack__proc(mob_living target = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        playsound(loc, Dm.resource("sound/mobs/non-humanoids/clown/hehe.ogg"), 20);
        if (!isliving(target))
            return null;
        if (target.mob_mood == null)
            return null; // tg reads target.mob_mood unguarded: a moodless mob's runtime ends the attack here, no hit lands (kept, DV1)
        float sanity = target.mob_mood.sanity;
        if (sanity >= SANITY_INSANE && sanity <= SANITY_CRAZY)
            force = 8;
        else if (sanity >= SANITY_UNSTABLE && sanity <= SANITY_DISTURBED)
        {
            force = 4;
            await target.add_mood_event("humiliated", typeof(datum_mood_event_mallet_humiliation));
        }
        else if (sanity >= SANITY_NEUTRAL && sanity <= SANITY_GREAT)
            await target.add_mood_event("humiliated", typeof(datum_mood_event_mallet_humiliation));

        if (Dm.truthy(user.combat_mode))
            return await base.attack__proc(target, user);
        return null;
    }
}
