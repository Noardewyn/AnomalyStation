// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/nukeop/outfits.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1b, D316); the outfits with no proc are their generated var blocks (D111).

// tg: code/modules/antagonists/nukeop/outfits.dm:1
public partial class datum_outfit_syndicate : datum_outfit
{
    // tg: code/modules/antagonists/nukeop/outfits.dm:56
    public override async DmTask post_equip(mob_living_carbon_human nukie = null, float? visuals_only = null)
    {
        if (Dm.truthy(visuals_only))
            return;

        // We don't require the nukiebase be loaded to function, but lets go ahead and kick off loading just in case
        Waiting.Detach(SSmapping.lazy_load_template(LAZY_TEMPLATE_KEY_NUKIEBASE));
        var radio = nukie.ears as obj_item_radio;
        radio.set_frequency(FREQ_SYNDICATE);
        radio.freqlock = RADIO_FREQENCY_LOCKED;
        if (Dm.truthy(command_radio))
        {
            radio.command = TRUE;
            radio.use_command = TRUE;
        }
        if (ispath(uplink_type, typeof(obj_item_uplink)) || Dm.truthy(tc)) // /obj/item/uplink understands 0 tc
        {
            var uplink = Dm.@new(uplink_type, nukie, nukie.key, tc) as obj_item;
            nukie.equip_to_storage(uplink, ITEM_SLOT_BACK, indirect_action: TRUE, del_on_fail: TRUE);
        }

        var weapons_implant = Dm.@new<obj_item_implant_weapons_auth>(nukie);
        await weapons_implant.implant(nukie);
        var explosive_implant = Dm.@new<obj_item_implant_explosive>(nukie);
        await explosive_implant.implant(nukie);
        nukie.add_faction(ROLE_SYNDICATE);
        await nukie.update_icons();
    }
}

// tg: code/modules/antagonists/nukeop/outfits.dm:104
public partial class datum_outfit_syndicate_full_plasmaman : datum_outfit_syndicate_full
{
    // tg: code/modules/antagonists/nukeop/outfits.dm:112
    public override async DmTask New(params object[] __extra)
    {
        Dm.append(ref backpack_contents, typeof(obj_item_clothing_head_helmet_space_plasmaman_syndie));
        await base.New(__extra);
    }
}

// tg: code/modules/antagonists/nukeop/outfits.dm:120
public partial class datum_outfit_syndicate_reinforcement : datum_outfit_syndicate
{
    // tg: code/modules/antagonists/nukeop/outfits.dm:129
    public override async DmTask post_equip(mob_living_carbon_human H = null, float? visuals_only = null)
    {
        await base.post_equip(H, visuals_only);
        if (Dm.truthy(visuals_only))
            return;
        to_chat(H, span_notice(I18n.T("You're an agent of [], sent to accompany the nuclear squad on their mission. Support your allies, and remember: Down with Nanotrasen.", faction)));
    }
}

// tg: code/modules/antagonists/nukeop/outfits.dm:209
public partial class datum_outfit_nuclear_operative_elite : datum_outfit
{
    // tg: code/modules/antagonists/nukeop/outfits.dm:217
    public override async DmTask post_equip(mob_living_carbon_human H = null, float? visuals_only = null)
    {
        var shield = Dm.locate<obj_item_shield_energy>(H.held_items);
        shield.icon_state = Dm.text("[]1", shield.base_icon_state);
        await H.update_held_items();
    }
}
