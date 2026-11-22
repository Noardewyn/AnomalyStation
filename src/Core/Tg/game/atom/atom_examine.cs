// Translated from tgstation (snapshot 1d99bd7f): code/game/atom/atom_examine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the examine base (D128's minimal examine, M4a P1: examine, examine_tags, examine_descriptor,
// examine_post_descriptor, get_examine_name and the living mob's, get_examine_icon, examine_title, get_name_chaser) and
// get_visible_name (M2 F1: the root of the human's visible name, human_helpers.dm:43); get_sniff_examine, examine_more and
// get_id_examine_strings (M9 slice G). Materials (M5 slice 2) are translated; chat icons (icon2html) are a deferred leaf.

// tg: code/game/atom/atom_examine.dm:15
public partial class atom : datum
{
    /**
     * Called when a mob examines this atom: [/mob/verb/examinate]
     *
     * Default behaviour is to get the name and icon of the object and its reagents where
     * the [TRANSPARENT] flag is set on the reagents holder
     *
     * Produces a signal [COMSIG_ATOM_EXAMINE], for modifying the list returned from this proc
     */
    // tg: code/game/atom/atom_examine.dm:15
    public virtual async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = Dm.list();
        Dm.append(ref dot, get_name_chaser(user));
        if (Dm.truthy(desc))
            Dm.append(ref dot, I18n.T("<i>[]</i>", desc));

        DmList tags_list = examine_tags(user) as DmList;
        DmList post_descriptor = await examine_post_descriptor(user) as DmList;
        string post_desc_string = Dm.truthy(Dm.length(post_descriptor)) ? I18n.T(" []", jointext(post_descriptor, " ")) : "";
        if (Dm.truthy(Dm.length(tags_list)))
        {
            object tag_string = Dm.list();
            foreach (object atom_tag in Dm.each(tags_list))
                Dm.append(ref tag_string, (tags_list[atom_tag] == null ? atom_tag : span_tooltip(Dm.str(tags_list[atom_tag]), Dm.str(atom_tag))));
            // some regex to ensure that we don't add another "and" if the final element's main text (not tooltip) has one
            // GAP(lead): BYOND's /regex is not in the shim; .NET's Regex matches tg's pattern (lazy quantifiers, no flags) as
            // findtext(text, regex) does: a match anywhere is truthy.
            DmList tag_list = tag_string as DmList;
            tag_string = english_list(tag_list, and_text: System.Text.RegularExpressions.Regex.IsMatch(Dm.str(tag_list[Dm.length(tag_list)]), ">.*?and .*?<") ? " " : " and ");
            Dm.append(ref dot, I18n.T("[] [] a [] [][].", p_They(), p_are(), tag_string, examine_descriptor(user), post_desc_string));
        }
        else if (Dm.truthy(post_desc_string))
            Dm.append(ref dot, I18n.T("[] [] a [][].", p_They(), p_are(), examine_descriptor(user), post_desc_string));

        if (Dm.truthy(reagents))
        {
            datum_reagents holder = (datum_reagents)reagents;
            // tg: :35  var/user_sees_reagents = user.can_see_reagents()
            // GAP(lead): /mob/proc/can_see_reagents (modules/mob/mob_helpers.dm:428) is not declared; its one line is
            // expanded here: dead guys and reagent scanners see reagents.
            float user_sees_reagents = Dm.num(user.stat == DEAD || HAS_TRAIT(user, TRAIT_REAGENT_SCANNER));
            object reagent_sigreturn = (!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_REAGENT_EXAMINE]) ? (object)NONE : this._SendSignal(COMSIG_ATOM_REAGENT_EXAMINE, Dm.list(this, user, dot, user_sees_reagents)));
            if (!Dm.truthy(Dm.bitand(reagent_sigreturn, STOP_GENERIC_REAGENT_EXAMINE)))
            {
                if (Dm.truthy(Dm.bitand(holder.flags, TRANSPARENT)))
                {
                    if (Dm.truthy(holder.total_volume))
                    {
                        Dm.append(ref dot, I18n.T("It contains <b>[]</b> units of various reagents[]", holder.total_volume, Dm.truthy(user_sees_reagents) ? ":" : "."));
                        if (Dm.truthy(user_sees_reagents) || Dm.truthy(Dm.bitand(reagent_sigreturn, ALLOW_GENERIC_REAGENT_EXAMINE))) //Show each individual reagent for detailed examination
                        {
                            foreach (datum_reagent current_reagent in Dm.each_cast<datum_reagent>(holder.reagent_list))
                                Dm.append(ref dot, I18n.T("&bull; [] units of []", round(Dm.num(current_reagent.volume), CHEMICAL_VOLUME_ROUNDING), current_reagent.name));
                            if (Dm.truthy(holder.is_reacting))
                                Dm.append(ref dot, span_warning(I18n.T("It is currently reacting!")));
                            Dm.append(ref dot, span_notice(I18n.T("The solution's pH is [] and has a temperature of []K.", round(holder.ph, 0.01f), holder.chem_temp)));
                        }
                    }
                    else
                        Dm.append(ref dot, I18n.T("It contains:<br>Nothing."));
                }
                else if (Dm.truthy(Dm.bitand(holder.flags, AMOUNT_VISIBLE)))
                {
                    if (Dm.truthy(holder.total_volume))
                        Dm.append(ref dot, span_notice(I18n.T("It has [] unit\\s left.", holder.total_volume)));
                    else
                        Dm.append(ref dot, span_danger(I18n.T("It's empty.")));
                }
            }

            if (HAS_TRAIT(user, TRAIT_KEEN_NOSE))
            {
                object sniff_text = get_sniff_examine(user as mob_living_carbon);
                if (Dm.truthy(sniff_text))
                    Dm.append(ref dot, sniff_text);
            }
        }

        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_EXAMINE]))
            this._SendSignal(COMSIG_ATOM_EXAMINE, Dm.list(this, user, dot));
        return dot;
    }

    /// Returns an examine string describing what the contents of this atom smell like
    // tg: code/game/atom/atom_examine.dm:63
    public virtual object get_sniff_examine(mob_living_carbon sniffer = null)
    {
        if (!(sniffer is mob_living_carbon) || HAS_TRAIT(sniffer, TRAIT_ANOSMIA))
            return null;
        if (!Dm.truthy(is_open_container()) || !Dm.truthy((reagents as datum_reagents)?.total_volume))
            return null;
        if (!Dm.truthy(sniffer.is_holding(this as obj_item)))
            return null;
        if (!Dm.truthy(sniffer.get_bodypart(BODY_ZONE_HEAD))) // Need a nose to smell
            return null;
        if (Dm.truthy(sniffer.is_mouth_covered()))
            return span_warning(I18n.T("You can't get a whiff of [] with your face covered.", this));

        object smell_message = generate_reagents_taste_message(((datum_reagents)reagents).reagent_list, sniffer, 10);
        return span_notice(I18n.T("You catch a whiff of []. It smells like [].", this, smell_message));
    }

    /**
     * A list of "tags" displayed after atom's description in examine.
     * This should return an assoc list of tags -> tooltips for them. If item is null, then no tooltip is assigned.
     *
     * * TGUI tooltips (not the main text) in chat cannot use HTML stuff at all, so
     * trying something like `<b><big>ffff</big></b>` will not work for tooltips.
     *
     * For example:
     * ```byond
     * . = list()
     * .["small"] = "It is a small item."
     * .["fireproof"] = "It is made of fire-retardant materials."
     * .["and conductive"] = "It's made of conductive materials and whatnot. Blah blah blah." // having "and " in the end tag's main text/key works too!
     * ```
     * will result in
     *
     * It is a *small*, *fireproof* *and conductive* item.
     *
     * where "item" is pulled from [/atom/proc/examine_descriptor]
     */
    // tg: code/game/atom/atom_examine.dm:98
    public virtual object examine_tags(mob user = null)
    {
        DmList dot = Dm.list();
        if (abstract_type == type)
            dot[span_hypnophrase(I18n.T("abstract"))] = I18n.T("This is an abstract concept, you should report this to a strange entity called GITHUB!");

        if (Dm.truthy(Dm.bitand(resistance_flags, INDESTRUCTIBLE)))
            dot[I18n.T("indestructible")] = I18n.T("It is extremely robust! It'll probably withstand anything that could happen to it!");
        else
        {
            if (Dm.truthy(Dm.bitand(resistance_flags, LAVA_PROOF)))
                dot[I18n.T("lava-proof")] = I18n.T("It is made of an extremely heat-resistant material, it'd probably be able to withstand lava!");
            if (Dm.truthy(Dm.bitand(resistance_flags, Dm.bitor(ACID_PROOF, UNACIDABLE))))
                dot[I18n.T("acid-proof")] = I18n.T("It looks pretty robust! It'd probably be able to withstand acid!");
            if (Dm.truthy(Dm.bitand(resistance_flags, FREEZE_PROOF)))
                dot[I18n.T("freeze-proof")] = I18n.T("It is made of cold-resistant materials.");
            if (Dm.truthy(Dm.bitand(resistance_flags, FIRE_PROOF)))
                dot[I18n.T("fire-proof")] = I18n.T("It is made of fire-retardant materials.");
            if (Dm.truthy(Dm.bitand(resistance_flags, SHUTTLE_CRUSH_PROOF)))
                dot[I18n.T("crush-proof")] = I18n.T("It is extremely solid. It should be able to withstand being run over by a shuttle!");
            if (Dm.truthy(Dm.bitand(resistance_flags, BOMB_PROOF)))
                dot[I18n.T("bomb-proof")] = I18n.T("It looks like it could survive an explosion!");
            if (Dm.truthy(Dm.bitand(resistance_flags, FLAMMABLE)))
                dot[I18n.T("flammable")] = I18n.T("It looks like it could easily catch on fire.");
        }

        if (Dm.truthy(Dm.bitand(flags_1, HOLOGRAM_1)))
            dot[I18n.T("holographic")] = I18n.T("It looks like a hologram.");

        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_EXAMINE_TAGS]))
            this._SendSignal(COMSIG_ATOM_EXAMINE_TAGS, Dm.list(this, user, dot));
        return dot;
    }

    /// What this atom should be called in examine tags
    // tg: code/game/atom/atom_examine.dm:127
    public virtual object examine_descriptor(mob user = null)
    {
        return I18n.T("object");
    }

    /// Returns a list of strings to be displayed after the descriptor
    // tg: code/game/atom/atom_examine.dm:131
    public virtual async DmTask<object> examine_post_descriptor(mob user = null)
    {
        DmList dot = Dm.list();
        if (!Dm.truthy(custom_materials))
            return dot;
        DmList mats_list = Dm.list();
        foreach (object custom_material in Dm.each(custom_materials))
        {
            datum_material current_material = await SSmaterials.get_material(custom_material) as datum_material;
            Dm.append(ref mats_list, span_tooltip(I18n.T("It is made out of [].", current_material.name), current_material.name));
        }
        Dm.append(ref dot, I18n.T("made of []", english_list(mats_list)));
        return dot;
    }

    /**
     * Called when a mob examines (shift click or verb) this atom twice (or more) within EXAMINE_MORE_WINDOW (default 1 second)
     *
     * This is where you can put extra information on something that may be superfluous or not important in critical gameplay
     * moments, while allowing people to manually double-examine to take a closer look
     *
     * Produces a signal [COMSIG_ATOM_EXAMINE_MORE]
     */
    // tg: code/game/atom/atom_examine.dm:149
    [ShouldCallParent]
    public virtual async DmTask<object> examine_more(mob user = null)
    {
        DmList dot = Dm.list();
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_EXAMINE_MORE]))
            this._SendSignal(COMSIG_ATOM_EXAMINE_MORE, Dm.list(this, user, dot));
        if (Dm.truthy(user._listen_lookup?[COMSIG_MOB_EXAMINING_MORE]))
            user._SendSignal(COMSIG_MOB_EXAMINING_MORE, Dm.list(user, this, dot));

        if (!Dm.truthy(Dm.length(custom_materials)) || Dm.truthy(Dm.bitand(material_flags, MATERIAL_NO_DESCRIPTORS)) || !HAS_TRAIT(user, TRAIT_RESEARCH_SCANNER))
            return dot;

        foreach (object custom_material in Dm.each(custom_materials))
        {
            // custom_materials is keyed by the material singletons (examine_post_descriptor's get_material reads them the same way)
            datum_material material = await SSmaterials.get_material(custom_material) as datum_material;
            DmList material_string = Dm.list();
            foreach (object prop_id in Dm.each(material.mat_properties))
            {
                datum_material_property property = SSmaterials.properties[prop_id] as datum_material_property;
                object prop_value = material.get_property(prop_id);
                if (prop_value == null) // Error?
                    continue;
                object descriptor = property?.get_descriptor(prop_value);
                object tooltip_hint = property?.get_tooltip(prop_value);
                if (Dm.truthy(descriptor)) // Overriden derivative property?
                    Dm.append(ref material_string, span_tooltip(I18n.T("[]: []", property, tooltip_hint), Dm.str(descriptor)));
            }

            if (Dm.truthy(Dm.length(material_string)))
                Dm.append(ref dot, span_info(I18n.T("[] is [].", capitalize(material.name), english_list(material_string))));
        }
        return dot;
    }

    /**
     * Get the name of this object for examine
     *
     * You can override what is returned from this proc by registering to listen for the
     * [COMSIG_ATOM_GET_EXAMINE_NAME] signal
     */
    // tg: code/game/atom/atom_examine.dm:181
    public virtual object get_examine_name(mob user = null)
    {
        DmList @override = Dm.list(article, null, I18n.T("<em>[]</em>", get_visible_name()));
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_GET_EXAMINE_NAME]))
            this._SendSignal(COMSIG_ATOM_GET_EXAMINE_NAME, Dm.list(this, user, @override));
        if (@override[EXAMINE_POSITION_ARTICLE] != null)
        {
            Dm.remove(ref @override, null); // IF there is no "before", don't try to join it
            return jointext(@override, " ");
        }
        if (@override[EXAMINE_POSITION_BEFORE] != null)
        {
            Dm.remove(ref @override, null); // There is no article, don't try to join it
            return I18n.T("\\a []", jointext(@override, " "));
        }
        return I18n.T("\\a []", this);
    }
}

// tg: code/game/atom/atom_examine.dm:193
public partial class mob_living : mob
{
    // tg: code/game/atom/atom_examine.dm:193
    public override object get_examine_name(mob user = null)
    {
        object visible_name = get_visible_name();
        DmList name_override = Dm.list(visible_name);
        if (Dm.truthy(Dm.bitand((!Dm.truthy(user._listen_lookup?[COMSIG_LIVING_PERCEIVE_EXAMINE_NAME]) ? (object)NONE : user._SendSignal(COMSIG_LIVING_PERCEIVE_EXAMINE_NAME, Dm.list(user, this, visible_name, name_override))), COMPONENT_EXAMINE_NAME_OVERRIDEN)))
            return name_override[1];
        return visible_name;
    }
}

// tg: code/game/atom/atom_examine.dm:201
public partial class atom : datum
{
    /// Icon displayed in examine
    // tg: code/game/atom/atom_examine.dm:201
    public virtual object get_examine_icon(mob user = null)
    {
        // M4a P1: translated (not in P1's list: examinate's title calls it through examine_title, D128).
        Dm.Deferred("code/game/atom/atom_examine.dm:202", "get_examine_icon: icon2html(src, user), the atom's icon in the chat line (__HELPERS/icons.dm, not declared; the 3D client's chat is text: chat icons, M10+)");
        return null;
    }

    /**
     * Formats the atom's name into a string for use in examine (as the "title" of the atom)
     *
     * * user - the mob examining the atom
     * * thats - whether to include "That's", or similar (mobs use "This is") before the name
     */
    // tg: code/game/atom/atom_examine.dm:210
    public virtual object examine_title(mob user = null, float? thats = null)
    {
        thats ??= FALSE;
        object examine_icon = get_examine_icon(user);
        return I18n.T("[][]<em>[]</em>", Dm.truthy(examine_icon) ? I18n.T("[] ", examine_icon) : "", Dm.truthy(thats) ? I18n.T("[] ", examine_thats) : "", get_examine_name(user));
    }

    /**
     * Returns an extended list of examine strings for any contained ID cards.
     *
     * Arguments:
     * * user - The user who is doing the examining.
     */
    // tg: code/game/atom/atom_examine.dm:220
    public virtual object get_id_examine_strings(mob user = null)
    {
        return Dm.list();
    }

    ///Used to insert text after the name but before the description in examine()
    // tg: code/game/atom/atom_examine.dm:224
    public virtual object get_name_chaser(mob user = null, DmList name_chaser = null)
    {
        name_chaser ??= Dm.list();
        return name_chaser;
    }
}

// tg: code/game/atom/atom_examine.dm:234
public partial class atom : datum
{
    /**
     * Used by mobs to determine the name for someone wearing a mask, or with a disfigured or missing face.
     * By default just returns the atom's name.
     *
     * * add_id_name - If TRUE, ID information such as honorifics or name (if mismatched) are appended
     * * force_real_name - If TRUE, will always return real_name and add (as face_name/id_name) if it doesn't match their appearance
     */
    // tg: code/game/atom/atom_examine.dm:234
    public virtual object get_visible_name(float? add_id_name = null, float? force_real_name = null)
    {
        add_id_name ??= TRUE;
        force_real_name ??= FALSE;
        return name;
    }
}
