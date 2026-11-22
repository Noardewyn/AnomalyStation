// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/external/restyling.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps OG1, D339, D341): restyling a body's visual organs with the flesh reshaper and the secateurs (their signal is the
// bodypart's and the organ's). The var blocks are generated (D111).

//Contains a bunch of procs for different types, but in the end it just lets you restyle the bodypart overlay so that's why it's here

// tg: code/modules/surgery/organs/external/restyling.dm:4
public partial class obj_item_organ : obj_item
{
    ///Helper proc to fetch a list of styles a player might want to restyle their features into during the round : returns list("Cabbage" = /datum/sprite_accessory/cabbage)
    // tg: code/modules/surgery/organs/external/restyling.dm:4
    public virtual object get_valid_restyles()
    {
        DmList valid_restyles = null;

        valid_restyles = Dm.list();
        DmList feature_list = ((datum_bodypart_overlay_mutant)bodypart_overlay).get_global_feature_list() as DmList;
        foreach (object accessory in Dm.each(feature_list))
        {
            datum_sprite_accessory accessory_datum = feature_list[accessory] as datum_sprite_accessory;
            if (Dm.truthy(Dm.initial(accessory_datum, (datum_sprite_accessory p) => p.locked))) //locked is for stuff that shouldn't appear here
                continue;
            valid_restyles[accessory] = accessory_datum;
        }

        return valid_restyles;
    }
}

// tg: code/modules/surgery/organs/external/restyling.dm:18
public partial class obj_item_bodypart : obj_item
{
    ///Someone used a restyling thingymajigga on our limb owner
    // tg: code/modules/surgery/organs/external/restyling.dm:18
    [SignalHandler]
    public virtual void on_attempt_feature_restyle_mob(atom source = null, mob_living trimmer = null, atom_movable original_target = null, object body_zone = null, object restyle_type = null, object style_speed = null)
    {
        //Check what body zone we are against the targeted zone, so we're sure we are the targeted limb
        if (Dm.eq(this.body_zone, body_zone))
            Waiting.Detach(attempt_feature_restyle(source, trimmer, original_target, body_zone, restyle_type, style_speed)); // INVOKE_ASYNC
    }

    ///Invoke async so we don't break signals
    // tg: code/modules/surgery/organs/external/restyling.dm:26
    [SignalHandler]
    public virtual void on_attempt_feature_restyle(atom source = null, mob_living trimmer = null, atom_movable original_target = null, object body_zone = null, object restyle_type = null, object style_speed = null)
    {
        Waiting.Detach(attempt_feature_restyle(source, trimmer, original_target, body_zone, restyle_type, style_speed)); // INVOKE_ASYNC
    }

    ///Asks the external organs inside the limb if they can restyle
    // tg: code/modules/surgery/organs/external/restyling.dm:32
    public virtual async DmTask attempt_feature_restyle(atom source = null, mob_living trimmer = null, atom_movable original_target = null, object body_zone = null, object restyle_type = null, object style_speed = null)
    {
        DmList valid_features = Dm.list();
        foreach (obj_item_organ feature in Dm.each_cast<obj_item_organ>(contents))
        {
            if (Dm.truthy(Dm.bitand(feature.restyle_flags, restyle_type)))
                valid_features.Add(feature);
        }

        obj_item_organ target_organ = null;
        float sw__1 = LAZYLEN(valid_features);
        if (sw__1 == 1)
            target_organ = valid_features[1] as obj_item_organ;
        else if (sw__1 >= 2)
        {
            DmList choose_options = Dm.list();
            DmList name_to_organ = Dm.list(); //literally so I don't have to loop again after someone's made their choice
            foreach (object organ_choice__it in Dm.each(valid_features))
            {
                obj_item_organ organ_choice = organ_choice__it as obj_item_organ;
                choose_options[organ_choice.name] = image(organ_choice);
                name_to_organ[organ_choice.name] = organ_choice;
            }
            object picked_option = await show_radial_menu(trimmer, original_target, choose_options, radius: 38, require_near: TRUE);
            if (Dm.truthy(picked_option))
                target_organ = name_to_organ[picked_option] as obj_item_organ;
            else
                return;
        }
        else
        {
            to_chat(trimmer, span_warning(I18n.T("There are no restylable features there!")));
            return;
        }

        await target_organ.attempt_feature_restyle(source, trimmer, original_target, body_zone, restyle_type, style_speed);
    }
}

// tg: code/modules/surgery/organs/external/restyling.dm:60
public partial class obj_item_organ : obj_item
{
    ///Invoke async so we don't break signals
    // tg: code/modules/surgery/organs/external/restyling.dm:60
    [SignalHandler]
    public virtual void on_attempt_feature_restyle(atom source = null, mob_living trimmer = null, atom_movable original_target = null, object body_zone = null, object restyle_type = null, object style_speed = null)
    {
        if (Dm.truthy(Dm.bitand(restyle_flags, restyle_type)))
            Waiting.Detach(attempt_feature_restyle(source, trimmer, original_target, body_zone, restyle_type, style_speed)); // INVOKE_ASYNC
        else
            to_chat(trimmer, span_warning(I18n.T("This tool is incompatible with the []!", this)));
    }

    ///Restyles the external organ from a list of valid options
    // tg: code/modules/surgery/organs/external/restyling.dm:69
    public virtual async DmTask attempt_feature_restyle(atom source = null, mob_living trimmer = null, atom_movable original_target = null, object body_zone = null, object restyle_type = null, object style_speed = null)
    {
        DmList restyles = get_valid_restyles() as DmList;
        object new_style = await tgui_input_list(trimmer, I18n.T("Select a new style"), I18n.T("Grooming"), restyles);

        trimmer.visible_message(
            span_notice(I18n.T("[] tries to change [] [].", trimmer, original_target == trimmer ? trimmer.p_their() : original_target.name + "'s", name)),
            span_notice(I18n.T("You try to change [] [].", original_target == trimmer ? I18n.T("your") : original_target.name + "'s", name)));
        if (Dm.truthy(new_style) && Dm.truthy(await do_after(trimmer, style_speed, target: original_target)))
        {
            trimmer.visible_message(
                span_notice(I18n.T("[] successfully changes [] [].", trimmer, original_target == trimmer ? trimmer.p_their() : original_target.name + "'s", name)),
                span_notice(I18n.T("You successfully change [] [].", original_target == trimmer ? I18n.T("your") : original_target.name + "'s", name)));

            await simple_change_sprite(restyles[new_style]); //turn name to type and pass it on
        }
    }
}

/// Allows for the restyling of visual organs to other styles within the same pool (if not forbidden)
// tg: code/modules/surgery/organs/external/restyling.dm:86
public partial class obj_item_flesh_shears : obj_item
{
    ///Catch right clicks so we can stylize!
    // tg: code/modules/surgery/organs/external/restyling.dm:114
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        await DmTask.CompletedTask;
        if (Dm.truthy(user.combat_mode) || !iscarbon(interacting_with))
            return NONE;

        restyle(interacting_with, user);
        return ITEM_INTERACT_SUCCESS;
    }

    ///Send a signal to whatever we clicked and start RESTYLING
    // tg: code/modules/surgery/organs/external/restyling.dm:122
    public virtual void restyle(atom target = null, mob_living user = null)
    {
        // SEND_SIGNAL(target, COMSIG_ATOM_RESTYLE, user, target, user.zone_selected, EXTERNAL_RESTYLE_ALL, restyle_speed) (rules 11.1 (e))
        if (Dm.truthy(target._listen_lookup?[COMSIG_ATOM_RESTYLE]))
            target._SendSignal(COMSIG_ATOM_RESTYLE, Dm.list(target, user, target, user.zone_selected, EXTERNAL_RESTYLE_ALL, restyle_speed));
    }
}
