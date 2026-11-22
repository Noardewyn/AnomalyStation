// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/reagent_containers/cups/organ_jar.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps H3 (D333): the whole file: the organ jar (an organ inside it, drawn small in the jar, kept from rotting while the jar is full of
// formaldehyde) and the brain in a jar with its note; the brain's revival by a ghost (the ghostrole_on_revive component, live since gaps
// OG1). The var blocks are generated (D111).

// The organ jar - a 120u beaker that can hold a single organ
// tg: code/modules/reagents/reagent_containers/cups/organ_jar.dm:2
public partial class obj_item_reagent_containers_cup_beaker_organ_jar : obj_item_reagent_containers_cup_beaker
{
    // Organ icon size goes from 32 to this
    // tg: code/modules/reagents/reagent_containers/cups/organ_jar.dm:64  #define JAR_INNER_ICON_SIZE 24
    private const float JAR_INNER_ICON_SIZE = 24;

    // tg: code/modules/reagents/reagent_containers/cups/organ_jar.dm:21
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_info(I18n.T("Any organ inside the jar will be preserved if it is entirely filled with formaldehyde.")));
        if (held_organ != null && held_organ.GetComponent(typeof(datum_component_ghostrole_on_revive)) != null)
            Dm.append(ref dot, span_smallnoticeital(I18n.T("The brain is twitching..."))); // Guaranteed to be a brain if it has that component
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/organ_jar.dm:27
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        QDEL_NULL(ref held_organ);
        return dot;
    }

    // Alt click lets you take the organ out, if it's present
    // tg: code/modules/reagents/reagent_containers/cups/organ_jar.dm:32
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (held_organ != null)
        {
            balloon_alert(user, Dm.text("removed []", held_organ));
            await user.put_in_hands(held_organ);
            held_organ.organ_flags = Dm.bitand(held_organ.organ_flags, Dm.bitnot(ORGAN_FROZEN));
            held_organ = null;
            name = Dm.initial(GetType(), (obj_item_reagent_containers_cup_beaker_organ_jar j) => j.name);
            desc = Dm.initial(GetType(), (obj_item_reagent_containers_cup_beaker_organ_jar j) => j.desc);
            update_appearance();
            return CLICK_ACTION_SUCCESS;
        }
        return await base.click_alt(user, __extra);
    }

    // Clicking on the jar with an organ lets you put the organ inside, if there isn't one already
    // Otherwise it should act like a normal bottle
    // tg: code/modules/reagents/reagent_containers/cups/organ_jar.dm:46
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_organ organ)
            return await base.item_interaction(user, tool, modifiers, __extra);
        if (held_organ != null)
        {
            balloon_alert(user, Dm.text("the jar already contains []", held_organ));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            return ITEM_INTERACT_BLOCKING;
        balloon_alert(user, Dm.text("inserted []", tool));
        held_organ = organ;
        name = Dm.text("[] in a jar", tool.name);
        desc = Dm.text("A jar with \\the [] inside it.", tool);
        check_organ_freeze();
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/reagents/reagent_containers/cups/organ_jar.dm:66
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        // Draw the organ icon inside the jar, if present
        if (held_organ != null)
        {
            image organ_img = image(held_organ, this, layer: FLOAT_LAYER);
            DmList icon_dimensions = get_icon_dimensions(held_organ.icon);
            organ_img.transform = (organ_img.transform as matrix ?? matrix()).Scale( // Make it smaller so it fits
                JAR_INNER_ICON_SIZE / Dm.num(icon_dimensions["width"]),
                JAR_INNER_ICON_SIZE / Dm.num(icon_dimensions["height"]));
            organ_img.pixel_z -= 3;
            organ_img.plane = FLOAT_PLANE;
            organ_img.blend_mode = BLEND_INSET_OVERLAY;
            Dm.append(ref dot, organ_img);
        }
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/organ_jar.dm:83
    public override void on_reagent_change(datum holder = null, params object[] __extra)
    {
        base.on_reagent_change(holder, __extra);
        var reagents_holder = (datum_reagents)holder;
        full_of_formaldehyde = Dm.truthy(reagents_holder.has_reagent(typeof(datum_reagent_toxin_formaldehyde), amount: reagents_holder.maximum_volume)) ? TRUE : FALSE;
        check_organ_freeze();
    }

    // Proc that stops the held organ from rotting if the jar is full of formaldehyde
    // tg: code/modules/reagents/reagent_containers/cups/organ_jar.dm:89
    public virtual void check_organ_freeze()
    {
        if (held_organ == null)
            return;
        if (Dm.truthy(full_of_formaldehyde))
            held_organ.organ_flags = Dm.bitor(held_organ.organ_flags, ORGAN_FROZEN);
        else
            held_organ.organ_flags = Dm.bitand(held_organ.organ_flags, Dm.bitnot(ORGAN_FROZEN));
    }
}

// tg: code/modules/reagents/reagent_containers/cups/organ_jar.dm:106
public partial class obj_item_reagent_containers_cup_beaker_organ_jar_brain_in_a_jar : obj_item_reagent_containers_cup_beaker_organ_jar
{
    // tg: code/modules/reagents/reagent_containers/cups/organ_jar.dm:100-104 (the file's #defines, #undef'd at its end)
    // A note with a "stuck in mail" flavor will appear upon examining more
    private const float NOTE_STUCK_IN_MAIL = 0;
    // A note with a "gift from a fellow morbid researcher" flavor will appear upon examining more
    private const float NOTE_MORBID_GIFT = 1;
    // A note with a "discarded brain from the recovered crew" flavor will appear upon examining more
    private const float NOTE_DISCARDED_LOST_CREW = 2;

    // tg: code/modules/reagents/reagent_containers/cups/organ_jar.dm:110
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(I18n.T("<i>You can see a note attached to the bottom..</i>")));
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/organ_jar.dm:114
    public override async DmTask<object> examine_more(mob user = null)
    {
        DmList dot = await base.examine_more(user) as DmList;
        // Flavor for why the brain is scarred
        switch (Dm.num(note_type))
        {
            case NOTE_STUCK_IN_MAIL:
                Dm.append(ref dot, span_notice(I18n.T("According to the note, this jar must've been stuck in the mail for at least 50 years...")));
                break;
            case NOTE_MORBID_GIFT:
                Dm.append(ref dot, span_notice(I18n.T("It reads...")));
                Dm.append(ref dot, span_notice(I18n.T("Greetings, XXX. I stumbled upon a hermit in my travels, whose quirks immediately piqued my interest. I'm sure his brain will be as useful to your research as it has been to mine. Signed, YYY.")));
                break;
            case NOTE_DISCARDED_LOST_CREW:
                Dm.append(ref dot, span_notice(I18n.T("It reads...")));
                Dm.append(ref dot, span_notice(I18n.T("Hey, XXX. Management wanted me to discard this poor schmuck's brain, claiming it's 'too damaged to viably recover', so I figured I might as well throw you a bone. I know you like these sorts of things. Signed, ZZZ.")));
                break;
        }
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/organ_jar.dm:132
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        note_type = rand(0, 2); // Attach a random note to it
        var scarred_brain = Dm.@new<obj_item_organ_brain>(); // Make a new brain
        // Make it revivable, scar it if revival is successful
        Waiting.NoSleep(scarred_brain.AddComponent(
            typeof(datum_component_ghostrole_on_revive),
            /* refuse_revival_if_failed = */ TRUE,
            /* on_successful_revive = */ Dm.@new<datum_callback>(this, nameof(handle_revival), scarred_brain))); // gaps OG1
        held_organ = scarred_brain; // Put the brain inside the jar
        var holder = (datum_reagents)reagents;
        Waiting.NoSleep(holder.add_reagent(typeof(datum_reagent_toxin_formaldehyde), holder.maximum_volume)); // Fill the jar with formaldehyde
        name = "brain in a jar"; // Set a custom name&description
        desc = "A brain in a jar. You can see it twitching...";
        update_appearance();
        return dot;
    }

    // All this does is add a random special brain trauma + add recovered crew antag datum for logging
    // tg: code/modules/reagents/reagent_containers/cups/organ_jar.dm:149
    public virtual async DmTask handle_revival(obj_item_organ_brain brain_to_scar = null)
    {
        await brain_to_scar.gain_trauma_type(typeof(datum_brain_trauma_special) /* BRAIN_TRAUMA_SPECIAL */, TRAUMA_RESILIENCE_ABSOLUTE); // tg's natural_gain = TRUE: the port's gain_trauma_type has no such argument
        var owner = brain_to_scar.owner as mob_living_carbon_human;
        await owner.mind.add_antag_datum(typeof(datum_antagonist_recovered_crew)); // for tracking mostly (c)
    }
}
