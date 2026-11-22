// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/implants/implant_misc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the smoke implant (gaps F1), the EMP and internal radio implants (syndicate G3). The weapons authorization
// implant has no procs. The var blocks are generated (D111), but the radio implant's, whose radio OpenDream types as a path.

// tg: code/game/objects/items/implants/implant_misc.dm:18
public partial class obj_item_implant_emp : obj_item_implant
{
    // tg: code/game/objects/items/implants/implant_misc.dm:32
    public override async DmTask<object> activate(params object[] __extra)
    {
        object dot = await base.activate(__extra);
        uses--;
        await empulse(imp_in, 3, 5, emp_source: this);
        if (!Dm.truthy(uses))
            qdel(this);
        return dot;
    }
}

// tg: code/game/objects/items/implants/implant_misc.dm:43
public partial class obj_item_implant_smoke : obj_item_implant
{
    // tg: code/game/objects/items/implants/implant_misc.dm:56
    public override async DmTask<object> activate(params object[] __extra)
    {
        object dot = await base.activate(__extra);
        uses--;
        await do_smoke(6, imp_in, imp_in.loc, smoke_type: typeof(datum_effect_system_fluid_spread_smoke_bad));
        if (!Dm.truthy(uses))
            qdel(this);
        return dot;
    }
}

// tg: code/game/objects/items/implants/implant_misc.dm:68
public partial class obj_item_implant_radio : obj_item_implant
{
    // tg: code/game/objects/items/implants/implant_misc.dm:69
    public obj_item_radio radio;
    // tg: code/game/objects/items/implants/implant_misc.dm:70
    public Type radio_key;
    // tg: code/game/objects/items/implants/implant_misc.dm:71
    public float subspace_transmission = 0;

    // tg: code/game/objects/items/implants/implant_misc.dm:68
    public obj_item_implant_radio()
    {
        name = "internal radio implant";
        icon = Dm.resource("icons/obj/devices/voice.dmi");
        icon_state = "walkietalkie";
        implant_info = "Automatically activates upon implantation. Provides radio transmission and reception capabilities.";
        implant_lore = "The Internal Radio Implant, a long open-sourced design manufactured by just about everyone from Nanotrasen to Cybersun, is a subdermal two-way radio system designed to interface with telecommunications networks. It's mainly useful for those who expect either equipment loss or lack the ability to use standalone radio equipment easily.";
    }

    // tg: code/game/objects/items/implants/implant_misc.dm:82
    public override async DmTask<object> activate(params object[] __extra)
    {
        object dot = await base.activate(__extra);
        // needs to be GLOB.deep_inventory_state otherwise it won't open
        await radio.ui_interact(usr, null, GLOB.deep_inventory_state);
        return dot;
    }

    // tg: code/game/objects/items/implants/implant_misc.dm:87
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        radio = Dm.@new<obj_item_radio>(this);
        // almost like an internal headset, but without the
        // "must be in ears to hear" restriction.
        radio.name = "internal radio";
        radio.subspace_transmission = subspace_transmission;
        radio.canhear_range = 0;
        if (radio_key != null)
            radio.keyslot = Dm.@new(radio_key);
        radio.recalculateChannels();
        return dot;
    }

    // tg: code/game/objects/items/implants/implant_misc.dm:100
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref radio);
        return base.Destroy(force);
    }
}
