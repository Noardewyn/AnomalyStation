// Translated from tgstation (snapshot 1d99bd7f): code/modules/religion/religion_structures.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the altar (climbable, raised, its candles, the coaxing of a pulled body onto it), the altar of the gods with its
// religious tool and the sect's look, and the ritual totem. The var blocks are generated (D111).

// tg: code/modules/religion/religion_structures.dm:1
public partial class obj_structure_altar : obj_structure
{
    // tg: code/modules/religion/religion_structures.dm:17
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_climbable)));
        Waiting.NoSleep(AddElement(typeof(datum_element_elevation), Dm.named("pixel_shift", 12)));
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/modules/religion/religion_structures.dm:23
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(lit_candles))
        {
            Dm.append(ref dot, mutable_appearance(icon, "convertaltarcandle", alpha: this.alpha));
            // DEVIATION(DV12): . += emissive_appearance(icon, "convertaltarcandle", src, alpha = src.alpha), the candles' glow on the emissive plane, is not created
        }
        // DEVIATION(DV12): if(emissive_icon_state) . += emissive_appearance(icon, emissive_icon_state, src, alpha = src.alpha), the dream altar's glow, is not created
        return dot;
    }

    // tg: code/modules/religion/religion_structures.dm:31
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(await Adjacent(user)) || user.pulling == null)
            return await base.attack_hand(user, modifiers, __extra);
        if (!isliving(user.pulling))
            return await base.attack_hand(user, modifiers, __extra);
        mob_living pushed_mob = user.pulling as mob_living;
        if (pushed_mob.buckled != null)
        {
            to_chat(user, span_warning(I18n.T("[] is buckled to []!", pushed_mob, pushed_mob.buckled)));
            return await base.attack_hand(user, modifiers, __extra);
        }
        to_chat(user, span_notice(I18n.T("You try to coax [] onto []...", pushed_mob, this)));
        if (!Dm.truthy(await do_after(user, (5 * SECONDS), target: pushed_mob)))
            return await base.attack_hand(user, modifiers, __extra);
        await pushed_mob.forceMove(loc);
        return await base.attack_hand(user, modifiers, __extra);
    }
}

/// This one actually has relevance to chaplains
// tg: code/modules/religion/religion_structures.dm:47
public partial class obj_structure_altar_of_gods : obj_structure_altar
{
    // tg: code/modules/religion/religion_structures.dm:53
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_religious_tool), ALL, FALSE, Dm.@new<datum_callback>(this, nameof(reflect_sect_in_icons))));
        reflect_sect_in_icons();
        Dm.append(ref GLOB.chaplain_altars, this);
        return dot;
    }

    // tg: code/modules/religion/religion_structures.dm:59
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.chaplain_altars, this);
        return base.Destroy(force);
    }

    // tg: code/modules/religion/religion_structures.dm:63
    public override async DmTask<object> examine_more(mob user = null)
    {
        if (!isobserver(user))
            return await base.examine_more(user);
        DmList dot = Dm.list(span_notice(I18n.T("<i>You examine [] closer, and note the following...</i>", this)));
        if (Dm.truthy(GLOB.religion))
        {
            Dm.append(ref dot, Dm.list(span_notice(I18n.T("Deity: [].", GLOB.deity))));
            Dm.append(ref dot, Dm.list(span_notice(I18n.T("Religion: [].", GLOB.religion))));
            Dm.append(ref dot, Dm.list(span_notice(I18n.T("Bible: [].", GLOB.bible_name))));
        }
        if (GLOB.religious_sect != null)
        {
            Dm.append(ref dot, Dm.list(span_notice(I18n.T("Sect: [].", GLOB.religious_sect))));
            Dm.append(ref dot, Dm.list(span_notice(I18n.T("Favor: [].", GLOB.religious_sect.favor))));
        }
        object chaplains = get_chaplains();
        if (Dm.truthy(isAdminObserver(user)) && Dm.truthy(chaplains))
            Dm.append(ref dot, Dm.list(span_notice(I18n.T("Chaplains: [].", chaplains))));
        return dot;
    }

    // tg: code/modules/religion/religion_structures.dm:78
    public virtual void reflect_sect_in_icons()
    {
        if (GLOB.religious_sect == null)
        {
            lit_candles = FALSE;
            icon = Dm.initial(this, (obj_structure_altar_of_gods p) => p.icon);
            icon_state = Dm.initial(this, (obj_structure_altar_of_gods p) => p.icon_state);
            emissive_icon_state = Dm.initial(this, (obj_structure_altar_of_gods p) => p.emissive_icon_state);
        }
        else
        {
            sect_to_altar = GLOB.religious_sect;
            lit_candles = GLOB.religious_sect.candle_overlay;
            if (Dm.truthy(sect_to_altar.altar_icon))
                icon = sect_to_altar.altar_icon;
            if (Dm.truthy(sect_to_altar.altar_icon_state))
                icon_state = (string)sect_to_altar.altar_icon_state;
            if (Dm.truthy(sect_to_altar.altar_emissive_icon_state))
                emissive_icon_state = sect_to_altar.altar_emissive_icon_state;
        }
        update_appearance(UPDATE_OVERLAYS); //Light the candles!
    }

    // tg: code/modules/religion/religion_structures.dm:95
    public virtual object get_chaplains()
    {
        string chaplain_string = "";
        foreach (mob_living_carbon_human potential_chap in Dm.each<mob_living_carbon_human>(GLOB.player_list))
        {
            if (Dm.truthy(potential_chap.key) && is_chaplain_job(potential_chap.mind?.assigned_role))
            {
                if (Dm.truthy(chaplain_string))
                    chaplain_string += ", ";
                chaplain_string += Dm.text("[] ([])", potential_chap, potential_chap.key);
            }
        }
        return chaplain_string;
    }
}

// tg: code/modules/religion/religion_structures.dm:104
public partial class obj_item_ritual_totem : obj_item
{
    // tg: code/modules/religion/religion_structures.dm:116
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_anti_magic),
            Dm.named("antimagic_flags", Dm.bitor(MAGIC_RESISTANCE, MAGIC_RESISTANCE_HOLY)),
            Dm.named("charges", 1),
            Dm.named("expiration", Dm.@new<datum_callback>(this, nameof(expire)))));
        Waiting.NoSleep(AddComponent(typeof(datum_component_religious_tool), RELIGION_TOOL_INVOKE, FALSE));
        return dot;
    }

    /// When the ritual totem is depleted of antimagic
    // tg: code/modules/religion/religion_structures.dm:126
    public virtual void expire(mob user = null)
    {
        to_chat(user, span_warning(I18n.T("[] consumes the magic within itself and quickly decays into rot!", this)));
        Dm.@new<obj_effect_decal_cleanable_ash>(drop_location());
        qdel(this);
    }

    // tg: code/modules/religion/religion_structures.dm:131
    public override object can_be_pulled(object user = null, object force = null)
    {
        object dot = base.can_be_pulled(user, force);
        return FALSE; //no
    }

    // tg: code/modules/religion/religion_structures.dm:135
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        object is_holy = user.mind?.holy_role;
        if (Dm.truthy(is_holy))
            Dm.append(ref dot, span_notice(I18n.T("[] can only be moved by important followers of [].", this, GLOB.deity)));
        return dot;
    }

    // tg: code/modules/religion/religion_structures.dm:141
    public override async DmTask<object> pickup(mob user = null)
    {
        mob taker = user;
        object initial_loc = loc;
        object holiness = taker.mind?.holy_role;
        float no_take = FALSE;
        if (Dm.eq(holiness, NONE))
        {
            to_chat(taker, span_warning(I18n.T("Try as you may, you're seemingly unable to pick [] up!", this)));
            no_take = TRUE;
        }
        else if (Dm.eq(holiness, HOLY_ROLE_DEACON)) //deacons cannot pick them up either
        {
            no_take = TRUE;
            to_chat(taker, span_warning(I18n.T("You cannot pick [] up. It seems you aren't important enough to [] to do that.", this, GLOB.deity)));
        }
        await base.pickup(user);
        if (Dm.truthy(no_take))
        {
            await taker.dropItemToGround(this);
            await forceMove(initial_loc as atom);
        }
        return null;
    }
}
