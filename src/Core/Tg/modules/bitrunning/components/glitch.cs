// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/components/glitch.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, a glitch that broke out to the station through an emagged server's byteforge: its faction,
// health, aura, holographic look, its fade on death and the slowdown while its byteforge is broken. The var block is generated (D111).

// tg: code/modules/bitrunning/components/glitch.dm:1
public partial class datum_component_glitch : datum_component
{
    // tg: code/modules/bitrunning/components/glitch.dm:6
    [DmParams("server", "forge")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        obj_machinery_quantum_server server = Dm.arg(__extra, 0) as obj_machinery_quantum_server;
        obj_machinery_byteforge forge = Dm.arg(__extra, 1) as obj_machinery_byteforge;
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;

        mob_living owner = parent as mob_living;
        float health_boost = 0;

        if (forge != null)
        {
            RegisterSignal(forge, COMSIG_MACHINERY_POWER_RESTORED, nameof(on_forge_power_restored));
            RegisterSignals(forge, Dm.list(COMSIG_MACHINERY_BROKEN, COMSIG_MACHINERY_POWER_LOST), nameof(on_forge_broken));
            forge_ref = WEAKREF(forge) as datum_weakref;
            server.remove_threat(owner); // so the server doesn't dust us
            health_boost = ROUND_UP(server.threat * 0.2f);
        }

        owner.set_faction(Dm.list(ROLE_GLITCH));

        owner.maxHealth = clamp(owner.maxHealth + health_boost, 200, 500);
        await owner.fully_heal();
        await owner.create_digital_aura();
        return null;
    }

    // tg: code/modules/bitrunning/components/glitch.dm:26
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_LIVING_DEATH, nameof(on_death));
        distort_component = WEAKREF(await parent.AddComponent(typeof(datum_component_holographic_nature)) as datum) as datum_weakref;
    }

    // tg: code/modules/bitrunning/components/glitch.dm:30
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await DmTask.CompletedTask;
        UnregisterSignal(parent, COMSIG_LIVING_DEATH);
        QDEL_NULL(ref distort_component);
    }

    /// We don't want digital entities just lingering around as corpses.
    // tg: code/modules/bitrunning/components/glitch.dm:35
    [SignalHandler]
    public virtual void on_death()
    {
        if (QDELETED(parent))
            return;

        mob_living owner = parent as mob_living;
        to_chat(owner, span_userdanger(I18n.T("You feel a strange sensation...")));

        obj_machinery_byteforge forge = forge_ref?.resolve() as obj_machinery_byteforge;
        forge?.setup_particles();

        owner.fade_into_nothing(3 * SECONDS, 2 * SECONDS);
    }

    /// If the forge breaks, we take a massive slowdown
    // tg: code/modules/bitrunning/components/glitch.dm:50
    [SignalHandler]
    public virtual void on_forge_broken(datum source = null)
    {
        mob_living player = parent as mob_living;
        atom_movable_screen_alert_bitrunning alert = player.throw_alert(
            ALERT_BITRUNNER_GLITCH,
            typeof(atom_movable_screen_alert_bitrunning),
            new_master: source as atom) as atom_movable_screen_alert_bitrunning;
        alert.name = "Source Broken";
        alert.desc = "Our byteforge has been broken.";

        if (!iscarbon(parent)) // Too powerful!
            return;

        Waiting.NoSleep(player.add_movespeed_modifier(typeof(datum_movespeed_modifier_status_effect_glitch_slowdown)));
        to_chat(player, span_danger(I18n.T("Your body feels sluggish...")));
    }

    /// Power restored
    // tg: code/modules/bitrunning/components/glitch.dm:69
    [SignalHandler]
    public virtual void on_forge_power_restored(datum source = null)
    {
        obj_machinery_byteforge forge = source as obj_machinery_byteforge;
        forge.setup_particles(angry: TRUE);

        if (!iscarbon(parent))
            return;

        mob_living player = parent as mob_living;
        player.clear_alert(ALERT_BITRUNNER_GLITCH);
        player.remove_movespeed_modifier(typeof(datum_movespeed_modifier_status_effect_glitch_slowdown));
    }
}
