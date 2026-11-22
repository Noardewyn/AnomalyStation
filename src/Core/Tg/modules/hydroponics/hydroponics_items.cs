// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/hydroponics_items.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J3 (D298): the scythe. Gaps P2 (D298): the rest of the file (the sprays, cultivators and the rake, hatchets, secateurs, the
// nutrient bottles). The var blocks are generated.

// tg: code/modules/hydroponics/hydroponics_items.dm:5
public partial class obj_item_reagent_containers_spray_weedspray : obj_item_reagent_containers_spray
{
    // tg: code/modules/hydroponics/hydroponics_items.dm:17
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is huffing []! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
        return TOXLOSS;
    }
}

// tg: code/modules/hydroponics/hydroponics_items.dm:21
public partial class obj_item_reagent_containers_spray_pestspray : obj_item_reagent_containers_spray
{
    // tg: code/modules/hydroponics/hydroponics_items.dm:33
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is huffing []! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
        return TOXLOSS;
    }
}

// tg: code/modules/hydroponics/hydroponics_items.dm:37
public partial class obj_item_cultivator : obj_item
{
    // tg: code/modules/hydroponics/hydroponics_items.dm:55
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is scratching [] back as hard as [] can with \\the []! It looks like [] trying to commit suicide!", user, user.p_their(), user.p_they(), this, user.p_theyre())));
        return BRUTELOSS;
    }
}

// tg: code/modules/hydroponics/hydroponics_items.dm:59
public partial class obj_item_cultivator_rake : obj_item_cultivator
{
    // tg: code/modules/hydroponics/hydroponics_items.dm:71
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        return dot;
    }

    /// var/static/list/loc_connections of Initialize (rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_item_cultivator_rake__Initialize__loc_connections;

    // tg: code/modules/hydroponics/hydroponics_items.dm:78
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable AM = null)
    {
        if (!ishuman(AM))
            return;
        mob_living_carbon_human H = AM as mob_living_carbon_human;
        if (Dm.truthy(has_gravity(loc as turf)) && HAS_TRAIT(H, TRAIT_CLUMSY) && !Dm.truthy(H.resting))
        {
            Waiting.NoSleep(H.set_confusion_if_lower(10 * SECONDS));
            Waiting.NoSleep(H.Stun(20));
            playsound(this, Dm.resource("sound/items/weapons/punch4.ogg"), 50, TRUE);
            H.visible_message(span_warning(I18n.T("[] steps on [] causing the handle to hit [] right in the face!", H, this, H.p_them())),
                              span_userdanger(I18n.T("You step on [] causing the handle to hit you right in the face!", this)));
        }
    }
}

// tg: code/modules/hydroponics/hydroponics_items.dm:119
public partial class obj_item_hatchet : obj_item
{
    // tg: code/modules/hydroponics/hydroponics_items.dm:146
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_butchering), Dm.named("speed", 7 * SECONDS), Dm.named("effectiveness", 100)));
        return dot;
    }

    // tg: code/modules/hydroponics/hydroponics_items.dm:153
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is chopping at []self with []! It looks like [] trying to commit suicide!", user, user.p_them(), this, user.p_theyre())));
        playsound(this, Dm.resource("sound/items/weapons/bladeslice.ogg"), 50, TRUE, -1);
        return BRUTELOSS;
    }
}

// tg: code/modules/hydroponics/hydroponics_items.dm:172
public partial class obj_item_scythe : obj_item
{
    // tg: code/modules/hydroponics/hydroponics_items.dm:198
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_butchering), Dm.named("speed", 9 * SECONDS), Dm.named("effectiveness", 105f)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_bane), Dm.named("affected_biotypes", MOB_PLANT), Dm.named("damage_multiplier", 1.5f)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_walking_aid)));
        return dot;
    }

    // tg: code/modules/hydroponics/hydroponics_items.dm:204
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is beheading []self with []! It looks like [] trying to commit suicide!", user, user.p_them(), this, user.p_theyre())));
        if (iscarbon(user))
        {
            mob_living_carbon C = (mob_living_carbon)user;
            obj_item_bodypart BP = C.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart;
            if (BP != null)
            {
                await BP.drop_limb();
                playsound(this, SFX_DESECRATION, 50, TRUE, -1);
            }
        }
        return BRUTELOSS;
    }

    // tg: code/modules/hydroponics/hydroponics_items.dm:214
    public override async DmTask<object> pre_attack(atom target = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (target is not obj_structure_alien_resin_flower_bud && target is not obj_structure_spacevine)
            return await base.pre_attack(target, user, modifiers, attack_modifiers);
        if (Dm.truthy(swiping) || get_turf(target) == get_turf(user))
            return await base.pre_attack(target, user, modifiers, attack_modifiers);
        turf user_turf = get_turf(user);
        float dir_to_target = get_dir(user_turf, get_turf(target));
        swiping = TRUE;
        foreach (object i in Dm.each(Dm.list(0, 45, 90, -45, -90)))
        {
            turf adjacent_turf = get_step(user_turf, turn(dir_to_target, Dm.num(i)));
            foreach (obj_structure_spacevine vine in Dm.each<obj_structure_spacevine>(adjacent_turf))
            {
                if (Dm.truthy(await user.Adjacent(vine)))
                    await melee_attack_chain(user, vine);
            }
            foreach (obj_structure_alien_resin_flower_bud flower in Dm.each<obj_structure_alien_resin_flower_bud>(adjacent_turf))
            {
                if (Dm.truthy(await user.Adjacent(flower)))
                    await melee_attack_chain(user, flower);
            }
        }
        swiping = FALSE;
        return TRUE;
    }
}

// tg: code/modules/hydroponics/hydroponics_items.dm:234
public partial class obj_item_secateurs : obj_item
{
    ///Catch right clicks so we can stylize!
    // tg: code/modules/hydroponics/hydroponics_items.dm:255
    public override async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;

        restyle(interacting_with, user);
        return ITEM_INTERACT_SUCCESS;
    }

    ///Send a signal to whatever we clicked and ask them if they wanna be PLANT RESTYLED YEAAAAAAAH
    // tg: code/modules/hydroponics/hydroponics_items.dm:263
    public virtual void restyle(atom target = null, mob_living user = null)
    {
        if (Dm.truthy(target._listen_lookup?[COMSIG_ATOM_RESTYLE]))
            target._SendSignal(COMSIG_ATOM_RESTYLE, Dm.list(target, user, target, user.zone_selected, EXTERNAL_RESTYLE_PLANT, 6 * SECONDS));
    }
}

// *************************************
// Nutrient defines for hydroponics
// *************************************

// tg: code/modules/hydroponics/hydroponics_items.dm:296
public partial class obj_item_reagent_containers_cup_bottle_nutrient : obj_item_reagent_containers_cup_bottle
{
    // tg: code/modules/hydroponics/hydroponics_items.dm:302
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        pixel_x = base_pixel_x + rand(-5, 5);
        pixel_y = base_pixel_y + rand(-5, 5);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/hydroponics/hydroponics_items.dm:73  var/static/list/loc_connections = list(COMSIG_ATOM_ENTERED = PROC_REF(on_entered))
    internal DmList obj_item_cultivator_rake__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_item_cultivator_rake.on_entered)));
}
