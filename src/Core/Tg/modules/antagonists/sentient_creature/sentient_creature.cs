// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/sentient_creature/sentient_creature.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3c, D316): the intelligence potion's creature and its info window (AntagInfoSentient). The var block
// is generated (D111).

// tg: code/modules/antagonists/sentient_creature/sentient_creature.dm:1
public partial class datum_antagonist_sentient_creature : datum_antagonist
{
    // tg: code/modules/antagonists/sentient_creature/sentient_creature.dm:8
    public override DmTask<object> get_preview_icon()
    {
        icon final_icon = Dm.icon(Dm.resource("icons/mob/simple/pets.dmi"), "corgi");

        icon pandora = Dm.icon(Dm.resource("icons/mob/simple/lavaland/lavaland_elites.dmi"), "pandora");
        pandora.Blend("#80808080", ICON_MULTIPLY);
        final_icon.Blend(pandora, ICON_UNDERLAY, -ICON_SIZE_X / 4, 0);

        icon rat = Dm.icon(Dm.resource("icons/mob/simple/animal.dmi"), "regalrat");
        rat.Blend("#80808080", ICON_MULTIPLY);
        final_icon.Blend(rat, ICON_UNDERLAY, ICON_SIZE_X / 4, 0);

        final_icon.Scale(ANTAGONIST_PREVIEW_ICON_SIZE, ANTAGONIST_PREVIEW_ICON_SIZE);
        return DmTask<object>.FromResult(final_icon);
    }

    // tg: code/modules/antagonists/sentient_creature/sentient_creature.dm:22
    public override async DmTask on_gain()
    {
        mob_living master = owner.enslaved_to?.resolve() as mob_living;
        if (master != null)
        {
            owner.current.copy_languages(master, LANGUAGE_MASTER);
            ADD_TRAIT(owner, TRAIT_UNCONVERTABLE, REF(this));
        }
        await base.on_gain();
    }

    // tg: code/modules/antagonists/sentient_creature/sentient_creature.dm:29
    public override async DmTask on_removal()
    {
        REMOVE_TRAIT(owner, TRAIT_UNCONVERTABLE, REF(this));
        await base.on_removal();
    }

    // tg: code/modules/antagonists/sentient_creature/sentient_creature.dm:33
    public override DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        mob_living master = owner.enslaved_to?.resolve() as mob_living;
        if (master != null)
        {
            data["enslaved_to"] = master.real_name;
            data["p_them"] = master.p_them();
            data["p_their"] = master.p_their();
        }
        data["holographic"] = (int)Dm.num(owner.current.flags_1) & HOLOGRAM_1;
        return DmTask<object>.FromResult(data);
    }
}
