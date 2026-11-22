// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/suits/_lasertag_components.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CL1 (D333): the whole file (the laser tag vest's team: only its team's tag pins fire, the other team's tag beams take
// stamina). The var block is generated (D111).

// tg: code/modules/clothing/suits/_lasertag_components.dm:2
public partial class datum_component_lasertag : datum_component
{
    // tg: code/modules/clothing/suits/_lasertag_components.dm:7
    [DmParams("team_color")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object team_color = Dm.arg(__extra, 0);
        if (!ishuman(parent))
            return COMPONENT_INCOMPATIBLE;
        register_lasertag_signals();
        this.team_color = Dm.str(team_color);
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/modules/clothing/suits/_lasertag_components.dm:14
    public virtual void register_lasertag_signals()
    {
        RegisterSignal(parent, COMSIG_LIVING_FIRING_PIN_CHECK, nameof(team_color_match));
        RegisterSignal(parent, COMSIG_ATOM_BULLET_ACT, nameof(on_laser_hit));
    }

    // tg: code/modules/clothing/suits/_lasertag_components.dm:19
    [SignalHandler]
    public virtual object team_color_match(datum source = null, object firing_pin = null)
    {
        obj_item_firing_pin_tag pin = firing_pin as obj_item_firing_pin_tag;
        if (Dm.eq(pin.tagcolor, team_color))
            return ALLOW_FIRE;
        return BLOCK_FIRE;
    }

    // tg: code/modules/clothing/suits/_lasertag_components.dm:26
    [SignalHandler]
    public virtual void on_laser_hit(datum source = null, object projectile = null)
    {
        if (!(projectile is obj_projectile_beam_lasertag))
            return;
        obj_projectile_beam_lasertag laser = (obj_projectile_beam_lasertag)projectile;
        if (!Dm.eq(laser.lasertag_team, team_color))
        {
            mob_living_carbon_human laser_target = (mob_living_carbon_human)parent;
            Waiting.NoSleep(laser_target.adjust_stamina_loss(laser.lasertag_damage));
        }
    }
}
