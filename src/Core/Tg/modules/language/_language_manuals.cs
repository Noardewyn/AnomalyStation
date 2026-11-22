// Translated from tgstation (snapshot 1d99bd7f): code/modules/language/_language_manuals.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N2c, D316: the uplink's deluxe codespeak manual, codespeak being live with the overwatch agent). The var
// blocks are generated (D111).

// tg: code/modules/language/_language_manuals.dm:1
public partial class obj_item_language_manual : obj_item
{
    // tg: code/modules/language/_language_manuals.dm:11
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!isliving(user))
            return null;

        if (Dm.truthy(user.has_language(language)))
        {
            to_chat(user, span_boldwarning(I18n.T("You start skimming through [], but you already know [].", this, Dm.initial(language, (datum_language p) => p.name))));
            return null;
        }

        to_chat(user, span_bolddanger(I18n.T("You start skimming through [], and [].", this, flavour_text)));

        user.grant_language(language);
        user.remove_blocked_language(language, source: LANGUAGE_ALL);
        ADD_TRAIT(user.mind, TRAIT_TOWER_OF_BABEL, MAGIC_TRAIT); // this makes you immune to babel effects

        use_charge(user);
        return null;
    }

    // tg: code/modules/language/_language_manuals.dm:27
    [DmParams("M", "user")]
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        mob_living M = target_mob;
        if (M == null || user == null)
            return null;
        if (M == user)
        {
            await attack_self(user);
            return null;
        }

        playsound(loc, SFX_PUNCH, 25, TRUE, -1);

        if (Dm.eq(M.stat, DEAD))
            M.visible_message(span_danger(I18n.T("[] smacks []'s lifeless corpse with [].", user, M, this)), span_userdanger(I18n.T("[] smacks your lifeless corpse with [].", user, this)), span_hear(I18n.T("You hear smacking.")));
        else if (Dm.truthy(M.has_language(language)))
            M.visible_message(span_danger(I18n.T("[] beats [] over the head with []!", user, M, this)), span_userdanger(I18n.T("[] beats you over the head with []!", user, this)), span_hear(I18n.T("You hear smacking.")));
        else
        {
            M.visible_message(span_notice(I18n.T("[] teaches [] by beating [] over the head with []!", user, M, M.p_them(), this)), span_boldnotice(I18n.T("As [] hits you with [], [].", user, this, flavour_text)), span_hear(I18n.T("You hear smacking.")));
            M.grant_language(language, source: LANGUAGE_MIND);
            use_charge(user);
        }
        return null;
    }

    // tg: code/modules/language/_language_manuals.dm:45
    public virtual void use_charge(mob user = null)
    {
        charges--;
        if (!Dm.truthy(charges))
        {
            user.visible_message(span_notice(I18n.T("The cover of []'s book start shifting and changing! It falls out of [] hands!", user, user.p_their())),
                span_warning(I18n.T("The cover and contents of [] start shifting and changing! It slips out of your hands!", this)));
            Dm.@new<obj_item_book_manual_random>(get_turf(this));
            qdel(this);
        }
    }
}

// tg: code/modules/language/_language_manuals.dm:63
public partial class obj_item_language_manual_roundstart_species : obj_item_language_manual
{
    // tg: code/modules/language/_language_manuals.dm:66
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (language == null) //if a language is already set, use it instead.
        {
            DmList available_languages = Dm.truthy(length(GLOB.uncommon_roundstart_languages)) ? GLOB.uncommon_roundstart_languages : Dm.list(typeof(datum_language_common));
            language = pick(available_languages) as Type;
        }
        name = Dm.text("[] manual", Dm.initial(language, (datum_language p) => p.name));
        desc = Dm.text("The book's cover reads: \"[] for Xenos - Learn common galactic tongues in seconds.\"", Dm.initial(language, (datum_language p) => p.name));
        flavour_text = Dm.text("you feel empowered with a mastery over []", Dm.initial(language, (datum_language p) => p.name));
        return dot;
    }
}

// tg: code/modules/language/_language_manuals.dm:75
public partial class obj_item_language_manual_roundstart_species_unlimited : obj_item_language_manual_roundstart_species
{
    // tg: code/modules/language/_language_manuals.dm:78
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        name = Dm.text("deluxe [] manual", Dm.initial(language, (datum_language p) => p.name));
        return dot;
    }
}

// tg: code/modules/language/_language_manuals.dm:82
public partial class obj_item_language_manual_roundstart_species_five : obj_item_language_manual_roundstart_species
{
    // tg: code/modules/language/_language_manuals.dm:85
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        name = Dm.text("extended [] manual", Dm.initial(language, (datum_language p) => p.name));
        return dot;
    }
}

// So drones can teach borgs and AI dronespeak. For best effect, combine with mother drone lawset.
// tg: code/modules/language/_language_manuals.dm:101
public partial class obj_item_language_manual_dronespeak_manual : obj_item_language_manual
{
    // tg: code/modules/language/_language_manuals.dm:108
    [DmParams("M", "user")]
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        mob_living M = target_mob;
        // If they are not drone or silicon, we don't want them to learn this language.
        if (!(isdrone(M) || issilicon(M)))
        {
            M.visible_message(span_danger(I18n.T("[] beats [] over the head with []!", user, M, this)), span_userdanger(I18n.T("[] beats you over the head with []!", user, this)), span_hear(I18n.T("You hear smacking.")));
            return null;
        }

        return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
    }

    // tg: code/modules/language/_language_manuals.dm:116
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!(isdrone(user) || issilicon(user)))
        {
            to_chat(user, span_danger(I18n.T("You beat yourself over the head with []!", this)));
            return null;
        }

        return await base.attack_self(user, modifiers, __extra);
    }
}
