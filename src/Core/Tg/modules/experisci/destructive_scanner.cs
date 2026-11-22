// Translated from tgstation (snapshot 1d99bd7f): code/modules/experisci/destructive_scanner.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RD3 (D333, the experiments): the whole file: the experimental destructive scanner, which closes its tube over everything on
// its tile when its window's Perform Experiment is pressed, scans for 6 seconds, opens again and hands what it took to its experiment
// handler (the destructive scans destroy what counts; a living thing inside is gibbed; humans only when emagged). tg's tube_down and
// tube_up flicks are the client model's tube lowered and raised (models.json "open" over tube_on). The var block is generated (D111).

/**
 * # Destructive scanner
 *
 * Placed machine that handles destructive experiments (but can also do the normal ones)
 */
// tg: code/modules/experisci/destructive_scanner.dm:6
public partial class obj_machinery_destructive_scanner : obj_machinery
{
    // tg: code/modules/experisci/destructive_scanner.dm:18  var/static/list/destructive_signals
    private static ref DmList post_machine_initialize__destructive_signals => ref TgState.Current.obj_machinery_destructive_scanner__post_machine_initialize__destructive_signals;

    // Late load to ensure the component initialization occurs after the machines are initialized
    // tg: code/modules/experisci/destructive_scanner.dm:15
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();

        await AddComponent(typeof(datum_component_experiment_handler),
            Dm.named("allowed_experiments", Dm.list(typeof(datum_experiment_scanning))),
            Dm.named("config_mode", EXPERIMENT_CONFIG_CLICK),
            Dm.named("start_experiment_callback", Dm.@new<datum_callback>(this, nameof(activate))),
            Dm.named("experiment_signals", post_machine_initialize__destructive_signals));
    }

    ///Activates the machine; checks if it can actually scan, then starts.
    // tg: code/modules/experisci/destructive_scanner.dm:30
    public virtual async DmTask activate()
    {
        atom pickup_zone = drop_location() as atom;
        float aggressive = FALSE;
        foreach (mob_living living_mob in Dm.each<mob_living>(pickup_zone))
        {
            if (!Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) && ishuman(living_mob)) //Can only kill humans when emagged.
            {
                playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 25);
                await say(I18n.T("Cannot scan with humans inside."));
                return;
            }
            aggressive = TRUE;
        }
        await start_closing(aggressive);
        await use_energy(idle_power_usage);
    }

    ///Closes the machine to kidnap everything in the turf into it.
    // tg: code/modules/experisci/destructive_scanner.dm:43
    public virtual async DmTask start_closing(object aggressive = null)
    {
        if (Dm.truthy(scanning))
            return;
        atom pickup_zone = drop_location() as atom;
        foreach (atom_movable to_pickup in Dm.each<atom_movable>(pickup_zone))
        {
            if (to_pickup == this)
                continue;
            await to_pickup.forceMove(this);
        }
        flick("tube_down", this);
        scanning = TRUE;
        await update_icon();
        playsound(this, Dm.resource("sound/machines/destructive_scanner/TubeDown.ogg"), 100);
        await use_energy(idle_power_usage);
        _addtimer(Dm.@new<datum_callback>(this, nameof(start_scanning), aggressive), 1.2f * SECONDS, file: "code/modules/experisci/destructive_scanner.dm", line: 56);
    }

    ///Starts scanning the fancy scanning effects
    // tg: code/modules/experisci/destructive_scanner.dm:59
    public virtual async DmTask start_scanning(object aggressive = null)
    {
        if (Dm.truthy(aggressive))
            playsound(this, Dm.resource("sound/machines/destructive_scanner/ScanDangerous.ogg"), 100, extrarange: 5);
        else
            playsound(this, Dm.resource("sound/machines/destructive_scanner/ScanSafe.ogg"), 100);
        await use_energy(active_power_usage);
        _addtimer(Dm.@new<datum_callback>(this, nameof(finish_scanning), aggressive), 6 * SECONDS, file: "code/modules/experisci/destructive_scanner.dm", line: 65);
    }

    ///Performs the actual scan, happens once the tube effects are done
    // tg: code/modules/experisci/destructive_scanner.dm:69
    public virtual async DmTask finish_scanning(object aggressive = null)
    {
        flick("tube_up", this);
        scanning = FALSE;
        await update_icon();
        playsound(this, Dm.resource("sound/machines/destructive_scanner/TubeUp.ogg"), 100);
        _addtimer(Dm.@new<datum_callback>(this, nameof(open), aggressive), 1.2f * SECONDS, file: "code/modules/experisci/destructive_scanner.dm", line: 74);
    }

    ///Opens the machine to let out any contents. If the scan had mobs it'll gib them.
    // tg: code/modules/experisci/destructive_scanner.dm:77
    public virtual async DmTask open(object aggressive = null)
    {
        turf this_turf = get_turf(this);
        DmList scanned_atoms = Dm.list();

        foreach (atom_movable movable_atom in Dm.each<atom_movable>(contents))
        {
            if (Dm.@in(movable_atom, component_parts))
                continue;
            Dm.append(ref scanned_atoms, movable_atom);
            await movable_atom.forceMove(this_turf);
            if (isliving(movable_atom))
            {
                mob_living fucked_up_thing = movable_atom as mob_living;
                fucked_up_thing.investigate_log(Dm.text("has been gibbed by [].", this), INVESTIGATE_DEATHS);
                await fucked_up_thing.gib(DROP_ALL_REMAINS);
            }
        }

        if (Dm.truthy(this._listen_lookup?[COMSIG_MACHINERY_DESTRUCTIVE_SCAN]))
            this._SendSignal(COMSIG_MACHINERY_DESTRUCTIVE_SCAN, Dm.list(this, scanned_atoms));
    }

    // tg: code/modules/experisci/destructive_scanner.dm:94
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        Dm.combine(ref obj_flags, EMAGGED);
        playsound(this, SFX_SPARKS, 75, TRUE, SILENCED_SOUND_EXTRARANGE);
        balloon_alert(user, "safety sensor BIOS disabled");
        return TRUE;
    }

    // tg: code/modules/experisci/destructive_scanner.dm:102
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(scanning) ? "tube_on" : "tube_open";
        return dot;
    }

    // tg: code/modules/experisci/destructive_scanner.dm:106
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return Dm.truthy(scanning) ? (object)NONE : default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/experisci/destructive_scanner.dm:109
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/experisci/destructive_scanner.dm:112
    public override object can_crowbar_deconstruct()
    {
        return (base.can_crowbar_deconstruct() is var and__1 && !Dm.truthy(and__1) ? and__1 : Dm.num(!Dm.truthy(scanning)));
    }
}

public sealed partial class TgState
{
    // tg: code/modules/experisci/destructive_scanner.dm:18  var/static/list/destructive_signals
    internal DmList obj_machinery_destructive_scanner__post_machine_initialize__destructive_signals = Dm.list(
        Dm.kv(COMSIG_MACHINERY_DESTRUCTIVE_SCAN, nameof(datum_component_experiment_handler.try_run_destructive_experiment)));
}
