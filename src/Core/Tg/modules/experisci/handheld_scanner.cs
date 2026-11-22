// Translated from tgstation (snapshot 1d99bd7f): code/modules/experisci/handheld_scanner.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RD3 (D333, the experiments): the whole file: the experi-scanner, a handheld experiment handler for the scanning and physical
// experiments (used in hand to configure it, on an atom to scan it), and its suicide that swallows the user and unleashes a meat
// toilet. The var block is generated (D111).

/**
 * # Experi-Scanner
 *
 * Handheld scanning unit to perform scanning experiments
 */
// tg: code/modules/experisci/handheld_scanner.dm:6
public partial class obj_item_experi_scanner : obj_item
{
    // tg: code/modules/experisci/handheld_scanner.dm:26  var/static/list/handheld_signals
    private static ref DmList LateInitialize__handheld_signals => ref TgState.Current.obj_item_experi_scanner__LateInitialize__handheld_signals;

    // tg: code/modules/experisci/handheld_scanner.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        return INITIALIZE_HINT_LATELOAD;
    }

    // Late initialize to allow for the rnd servers to initialize first
    // tg: code/modules/experisci/handheld_scanner.dm:25
    public override void LateInitialize(params object[] __extra)
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_experiment_handler),
            Dm.named("allowed_experiments", Dm.list(typeof(datum_experiment_scanning), typeof(datum_experiment_physical))),
            Dm.named("disallowed_traits", EXPERIMENT_TRAIT_DESTRUCTIVE),
            Dm.named("config_flags", EXPERIMENT_CONFIG_ALWAYS_ANNOUNCE),
            Dm.named("experiment_signals", LateInitialize__handheld_signals)));
    }

    // tg: code/modules/experisci/handheld_scanner.dm:37
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is giving in to the Great Toilet Beyond! It looks like [] trying to commit suicide!", user, user.p_theyre())));

        await forceMove(drop_location() as atom);
        await user.forceMove(this);
        await user.AddComponent(typeof(datum_component_itembound), this); //basically a bread smite but with a bloody finale
        icon_state = "experiscanner_closed";
        await add_atom_colour(COLOR_RED, ADMIN_COLOUR_PRIORITY);

        playsound(this, Dm.resource("sound/effects/pope_entry.ogg"), 60, TRUE);
        playsound(this, Dm.resource("sound/machines/destructive_scanner/ScanDangerous.ogg"), 40);
        await user.emote("scream");

        _addtimer(Dm.@new<datum_callback>(this, nameof(make_meat_toilet), user), 5 * SECONDS, file: "code/modules/experisci/handheld_scanner.dm", line: 50);
        return MANUAL_SUICIDE;
    }

    // tg: code/modules/experisci/handheld_scanner.dm:53
    public virtual async DmTask make_meat_toilet(mob_living user = null)
    {
        ///The toilet we're about to unleash unto this cursed plane of existence
        Dm.@new<obj_structure_toilet_greyscale_flesh>(drop_location(), user); //the toilet's Initialize proc will handle the rest from here.

        icon_state = "experiscanner";
        await remove_atom_colour(ADMIN_COLOUR_PRIORITY, COLOR_RED);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/experisci/handheld_scanner.dm:26  var/static/list/handheld_signals
    internal DmList obj_item_experi_scanner__LateInitialize__handheld_signals = Dm.list(
        Dm.kv(COMSIG_ITEM_PRE_ATTACK, nameof(datum_component_experiment_handler.try_run_handheld_experiment)),
        Dm.kv(COMSIG_ITEM_AFTERATTACK, nameof(datum_component_experiment_handler.ignored_handheld_experiment_attempt)));
}
