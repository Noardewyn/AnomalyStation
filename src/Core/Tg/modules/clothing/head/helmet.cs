// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/head/helmet.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: /obj/item/clothing/head/helmet's Initialize (M4c slice 2: the space helmet is a helmet) and, J1 (D298), the security helmet
// (the straps, the visor, the signaler welded on for a secbot frame; the flashlight mount, the seclite attachable component, is a
// deferred leaf with the guns' mounts). Gaps CL1 (D333): the press, marine, SWAT, knight and Russian helmets, the helmet of justice (its
// siren loop while the lights are on), the durability helmets and the holymelon's anti-magic. Their fishing difficulty elements wait
// for fishing and their emissive overlays are DV12 (not-translated.md 209). The var blocks are generated.

// tg: code/modules/clothing/head/helmet.dm:1
public partial class obj_item_clothing_head_helmet : obj_item_clothing_head
{
    // tg: code/modules/clothing/head/helmet.dm:31
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        return dot;
    }
}

// tg: code/modules/clothing/head/helmet.dm:36
public partial class obj_item_clothing_head_helmet_sec : obj_item_clothing_head_helmet
{
    // tg: code/modules/clothing/head/helmet.dm:45
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_seclite_attachable), Dm.named("light_icon_state", "flight")));
        return dot;
    }

    // tg: code/modules/clothing/head/helmet.dm:49
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!(tool is obj_item_assembly_signaler))
            return await base.item_interaction(user, tool, modifiers, __extra);

        obj_item_assembly_signaler attached_signaler = tool as obj_item_assembly_signaler;
        // There's a flashlight in us. Remove it first, or it'll be lost forever!
        obj_item_flashlight_seclite blocking_us = Dm.locate_type(typeof(obj_item_flashlight_seclite), contents) as obj_item_flashlight_seclite;
        if (Dm.truthy(blocking_us))
        {
            to_chat(user, span_warning(I18n.T("[] is in the way, remove it first!", blocking_us)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(attached_signaler.secured))
        {
            to_chat(user, span_warning(I18n.T("Secure [] first!", attached_signaler)));
            return ITEM_INTERACT_BLOCKING;
        }

        to_chat(user, span_notice(I18n.T("You add [] to [].", attached_signaler, this)));

        qdel(attached_signaler);
        obj_item_bot_assembly_secbot secbot_frame = Dm.@new<obj_item_bot_assembly_secbot>(drop_location());
        object held_index = user.is_holding(this);
        qdel(this);
        if (Dm.truthy(held_index))
            await user.put_in_hand(secbot_frame, held_index);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/clothing/head/helmet.dm:74
    [DmParams("user")]
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        balloon_alert(user, I18n.T("[] straps...", Dm.truthy(Dm.bitand(flags_inv, HIDEHAIR)) ? I18n.T("loosening") : I18n.T("tightening")));
        if (!Dm.truthy(await do_after(user, 3 * SECONDS, this)))
            return dot;
        Dm.bitxor_assign(ref flags_inv, HIDEHAIR);
        balloon_alert(user, I18n.T("[] straps", Dm.truthy(Dm.bitand(flags_inv, HIDEHAIR)) ? I18n.T("tightened") : I18n.T("loosened")));
        return TRUE;
    }

    // tg: code/modules/clothing/head/helmet.dm:85
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        flipped_visor = Dm.truthy(flipped_visor) ? FALSE : TRUE;
        balloon_alert(user, I18n.T("visor flipped"));
        // base_icon_state is modified for seclight attachment component
        base_icon_state = Dm.text("[][]", Dm.initial(this, (atom p) => p.base_icon_state), Dm.truthy(flipped_visor) ? "-novisor" : "");
        icon_state = (string)base_icon_state;
        if (Dm.truthy(flipped_visor))
        {
            Dm.mask(ref flags_cover, Dm.bitnot(HEADCOVERSEYES));
            playsound(this, SFX_VISOR_DOWN, 20, TRUE, -1);
        }
        else
        {
            Dm.combine(ref flags_cover, HEADCOVERSEYES);
            playsound(this, SFX_VISOR_UP, 20, TRUE, -1);
        }
        update_appearance();
        return CLICK_ACTION_SUCCESS;
    }
}

// tg: code/modules/clothing/head/helmet.dm:100
public partial class obj_item_clothing_head_helmet_press : obj_item_clothing_head_helmet
{
    // tg: code/modules/clothing/head/helmet.dm:110
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_seclite_attachable), Dm.named("light_icon_state", "flight")));
        return dot;
    }

    // tg: code/modules/clothing/head/helmet.dm:114
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        bodyshape ??= NONE;
        object dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape);
        // DEVIATION(DV12): if(!isinhands) . += emissive_appearance(icon_file, "[icon_state]-emissive", src, alpha = src.alpha,
        // effect_type = EMISSIVE_SPECULAR): the press letters' glow on the emissive plane is not created
        return dot;
    }
}

// tg: code/modules/clothing/head/helmet.dm:120
public partial class obj_item_clothing_head_helmet_alt : obj_item_clothing_head_helmet
{
    // tg: code/modules/clothing/head/helmet.dm:142
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_seclite_attachable), Dm.named("light_icon_state", "flight")));
        return dot;
    }
}

// tg: code/modules/clothing/head/helmet.dm:146
public partial class obj_item_clothing_head_helmet_marine : obj_item_clothing_head_helmet
{
    // tg: code/modules/clothing/head/helmet.dm:173
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_seclite_attachable), Dm.named("starting_light", Dm.@new<obj_item_flashlight_seclite>(this)), Dm.named("light_icon_state", "flight")));
        return dot;
    }
}

// tg: code/modules/clothing/head/helmet.dm:221
public partial class obj_item_clothing_head_helmet_toggleable : obj_item_clothing_head_helmet
{
    // tg: code/modules/clothing/head/helmet.dm:225
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await adjust_visor(user as mob_living);
        return null;
    }

    // tg: code/modules/clothing/head/helmet.dm:228
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        base_icon_state = Dm.text("[]", Dm.initial(this, (obj_item_clothing_head_helmet_toggleable p) => p.base_icon_state));
        datum_component_seclite_attachable light = GetComponent(typeof(datum_component_seclite_attachable)) as datum_component_seclite_attachable;
        if (Dm.truthy(up))
            base_icon_state += "up";
        light?.on_update_icon_state(this);
        if (light == null)
            icon_state = Dm.str(base_icon_state);
        return dot;
    }
}

// tg: code/modules/clothing/head/helmet.dm:239
public partial class obj_item_clothing_head_helmet_toggleable_riot : obj_item_clothing_head_helmet_toggleable
{
    // tg: code/modules/clothing/head/helmet.dm:261 and :265  (tg's two Initialize overrides, the second calling the first)
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_seclite_attachable), Dm.named("light_icon_state", "flight")));
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), 2));
        return dot;
    }
}

// tg: code/modules/clothing/head/helmet.dm:294
public partial class obj_item_clothing_head_helmet_toggleable_justice : obj_item_clothing_head_helmet_toggleable
{
    // tg: code/modules/clothing/head/helmet.dm:314
    public override async DmTask<object> adjust_visor(mob_living user = null)
    {
        if (!(visor_toggle_cooldown <= world.time)) // !COOLDOWN_FINISHED(src, visor_toggle_cooldown)
            return FALSE;
        visor_toggle_cooldown = world.time + 2 * SECONDS; // COOLDOWN_START(src, visor_toggle_cooldown, 2 SECONDS)
        return await base.adjust_visor(user);
    }

    // tg: code/modules/clothing/head/helmet.dm:320
    public override void visor_toggling(params object[] __extra)
    {
        base.visor_toggling(__extra);
        if (Dm.truthy(up))
            weewooloop.start();
        else
            weewooloop.stop();
    }

    // tg: code/modules/clothing/head/helmet.dm:327
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        weewooloop = Dm.@new<datum_looping_sound_siren>(this, FALSE, FALSE);
        return dot;
    }

    // tg: code/modules/clothing/head/helmet.dm:331
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref weewooloop);
        return base.Destroy(force);
    }
}

// tg: code/modules/clothing/head/helmet.dm:341
public partial class obj_item_clothing_head_helmet_swat : obj_item_clothing_head_helmet
{
    // tg: code/modules/clothing/head/helmet.dm:361
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), 3));
        return dot;
    }
}

// tg: code/modules/clothing/head/helmet.dm:376
public partial class obj_item_clothing_head_helmet_swat_nanotrasen : obj_item_clothing_head_helmet_swat
{
    // tg: code/modules/clothing/head/helmet.dm:389
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_seclite_attachable), Dm.named("light_icon_state", "flight")));
        return dot;
    }
}

// tg: code/modules/clothing/head/helmet.dm:499
public partial class obj_item_clothing_head_helmet_knight : obj_item_clothing_head_helmet
{
    // tg: code/modules/clothing/head/helmet.dm:512
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), 3));
        return dot;
    }
}

// tg: code/modules/clothing/head/helmet.dm:560
public partial class obj_item_clothing_head_helmet_rus_helmet : obj_item_clothing_head_helmet
{
    // tg: code/modules/clothing/head/helmet.dm:580
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(create_storage(storage_type: typeof(datum_storage_pockets_helmet)));
        return dot;
    }
}

// tg: code/modules/clothing/head/helmet.dm:674
public partial class obj_item_clothing_head_helmet_durability : obj_item_clothing_head_helmet
{
    // tg: code/modules/clothing/head/helmet.dm:674
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        await take_damage(1, BRUTE, 0, 0);
        return null;
    }
}

// tg: code/modules/clothing/head/helmet.dm:709
public partial class obj_item_clothing_head_helmet_durability_holymelon : obj_item_clothing_head_helmet_durability
{
    // tg: code/modules/clothing/head/helmet.dm:723
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(AddComponent(
            typeof(datum_component_anti_magic),
            Dm.named("antimagic_flags", Dm.bitor(MAGIC_RESISTANCE, MAGIC_RESISTANCE_HOLY)),
            Dm.named("inventory_flags", ITEM_SLOT_OCLOTHING),
            Dm.named("charges", 1),
            Dm.named("block_magic", Dm.@new<datum_callback>(this, nameof(drain_antimagic))),
            Dm.named("expiration", Dm.@new<datum_callback>(this, nameof(decay)))
        ));
        return dot;
    }

    // tg: code/modules/clothing/head/helmet.dm:735
    public virtual void drain_antimagic(mob user = null)
    {
        to_chat(user, span_warning(I18n.T("[] looses a bit of its shimmer and glossiness...", this)));
    }

    // tg: code/modules/clothing/head/helmet.dm:738
    public virtual async DmTask decay()
    {
        await take_damage(8, BRUTE, 0, 0);
    }
}
