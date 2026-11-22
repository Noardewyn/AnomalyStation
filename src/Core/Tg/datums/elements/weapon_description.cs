// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/weapon_description.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// D291: the whole file (the "See combat information." link of an item's examine and the readout a click on it prints). The
// link is a byond:// chat link; its click reaches COMSIG_TOPIC through ClientSeam.Topic (D154). The var block is generated.

/**
 *
 * The purpose of this element is to widely provide the ability to examine an object and determine its stats, with the ability to add
 * additional notes or information based on type or other factors
 *
 */
// tg: code/datums/elements/weapon_description.dm:8
public partial class datum_element_weapon_description : datum_element
{
    // tg: code/datums/elements/weapon_description.dm:14
    [DmParams("target", "attached_proc")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        string attached_proc = Dm.arg(__extra, 0) as string;
        object dot = await base.Attach(target, __extra);
        if (!isitem(target)) // Do not attach this to anything that isn't an item
            return ELEMENT_INCOMPATIBLE;
        RegisterSignal(target, COMSIG_ATOM_EXAMINE, nameof(warning_label));
        RegisterSignal(target, COMSIG_TOPIC, nameof(topic_handler));
        // Don't perform the assignment if there is nothing to assign, or if we already have something for this bespoke element
        if (Dm.truthy(attached_proc) && !Dm.truthy(this.attached_proc))
            this.attached_proc = attached_proc;
        return dot;
    }

    // tg: code/datums/elements/weapon_description.dm:25
    public override object Detach(datum target = null, params object[] __extra)
    {
        object dot = base.Detach(target, __extra);
        UnregisterSignal(target, Dm.list(COMSIG_ATOM_EXAMINE, COMSIG_TOPIC));
        return dot;
    }

    /**
     *
     * This proc is called when the user examines an object with the associated element. This produces a hyperlinked
     * text line provided that the given item meets the weapon-determining criteria (Sufficient force or notes)
     *
     * Arguments:
     * 	* source - Object being examined, cast into an item variable
     *  * user - Unused
     *  * examine_texts - The output text list of the original examine function
     */
    // tg: code/datums/elements/weapon_description.dm:39
    [SignalHandler]
    public virtual void warning_label(obj_item item = null, mob user = null, DmList examine_texts = null)
    {
        if (item.force >= 5 || Dm.num(item.throwforce) >= 5 || Dm.truthy(item.override_notes) || Dm.truthy(item.offensive_notes) || Dm.truthy(attached_proc)) /// Only show this tag for items that could feasibly be weapons, shields, or those that have special notes
            Dm.append(ref examine_texts, span_notice(I18n.T("<a href='byond://?src=[];examine=1'>See combat information.</a>", REF(item))));
    }

    /**
     *
     * Details the stats of the examined weapon
     *
     * This function is called when the user clicks the hyperlink provided by
     * warning_label(). It calls build_label_text() and outputs its return value to the user
     *
     * Arguments:
     *  * source - Object being examined, sent to build_label_text()
     *  * href-list - List provided by the href of input values, used to know what hyperlinked action is being attempted
     */
    // tg: code/datums/elements/weapon_description.dm:58
    [SignalHandler]
    public virtual void topic_handler(atom source = null, object user = null, DmList href_list = null)
    {
        if (Dm.truthy(href_list?["examine"]))
            to_chat(user, span_notice(boxed_message(Dm.text("[]", build_label_text(source as obj_item)))));
    }

    /**
     *
     * Compiles a warning label detailing various statistics of the examined weapon
     *
     * This function is called by the "examine" function of Topic(), and compiles a number of relevant
     * weapon stats into a message that is then shown to the user
     * Arguments:
     *  * source - The object whose stats are being examined
     */
    // tg: code/datums/elements/weapon_description.dm:73
    public virtual object build_label_text(obj_item source = null)
    {
        DmList readout = Dm.list(); // Readout is used to store the text block output to the user so it all can be sent in one message

        // Doesn't show the base notes for items that have the override notes variable set to true
        if (!Dm.truthy(source.override_notes))
        {
            if (Dm.truthy(Dm.bitand(source.get_sharpness(), SHARP_EDGED)))
                Dm.append(ref readout, I18n.T("It's sharp and could cause bleeding wounds."));
            if (Dm.truthy(Dm.bitand(source.get_sharpness(), SHARP_POINTY)))
                Dm.append(ref readout, I18n.T("It's pointy and could cause piercing wounds."));
            // Make sure not to divide by 0 on accident
            if (source.force > 0)
                Dm.append(ref readout, I18n.T("It takes about [] to take down an enemy.", span_warning(I18n.T("[] melee hit\\s", HITS_TO_CRIT(source.force)))));
            else
                Dm.append(ref readout, I18n.T("It does not deal noticeable melee damage."));

            if (Dm.num(source.throwforce) > 0)
                Dm.append(ref readout, I18n.T("It takes about [] to take down an enemy.", span_warning(I18n.T("[] throwing hit\\s", HITS_TO_CRIT(Dm.num(source.throwforce))))));
            else
                Dm.append(ref readout, I18n.T("It does not deal noticeable throwing damage."));
            if (source.armour_penetration > 0 || source.block_chance > 0)
                Dm.append(ref readout, I18n.T("It has [] armor-piercing capability and [] blocking capability.", span_warning(weapon_tag_convert(source.armour_penetration)), span_warning(weapon_tag_convert(source.block_chance))));
        }
        // Custom manual notes
        if (Dm.truthy(source.offensive_notes))
            Dm.append(ref readout, source.offensive_notes);

        // Check if we have an additional proc, if so, add it to the readout
        if (Dm.truthy(attached_proc))
            Dm.append(ref readout, Waiting.NoSleep(Dm.Call(source, (string)attached_proc)));

        if (Dm.truthy(source._listen_lookup?[COMSIG_ITEM_WEAPON_LABEL_READOUT]))
            source._SendSignal(COMSIG_ITEM_WEAPON_LABEL_READOUT, Dm.list(source, readout));

        // Finally bringing the fields together
        return readout.Join("\n");
    }

    /**
     *
     * Converts percentile based stats to an adjective appropriate for the
     * examined warning label
     *
     * Arguments:
     *  * tag_val: The value of the item to be added to the tag
     */
    // tg: code/datums/elements/weapon_description.dm:112
    public virtual string weapon_tag_convert(object tag_val = null)
    {
        float v = Dm.num(tag_val);
        if (v == 0)
            return I18n.T("NO");
        if (v >= 1 && v <= 25)
            return I18n.T("LITTLE");
        if (v >= 26 && v <= 50)
            return I18n.T("AVERAGE");
        if (v >= 51 && v <= 75)
            return I18n.T("ABOVE-AVERAGE");
        if (v >= 76 && v <= INFINITY)
            return I18n.T("EXCELLENT");
        return I18n.T("WEIRD");
    }
}
