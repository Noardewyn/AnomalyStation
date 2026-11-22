// Translated from tgstation (snapshot 1d99bd7f): code/datums/mocking/client.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1c, D316: the mock client that randomize_human_appearance makes its throwaway preferences with, as the
// lone operative's event does). The UNIT_TESTS block of New (registering in GLOB.directory and a persistent client) is tg's unit-test
// build only, as in tg's production build. Drag and drop (protocol version 17): check_drag_proximity (:89), which /atom/MouseDrop calls
// on GET_CLIENT(usr) (a mock client in tg's unit tests unequip_defib and buckling, ported as tests). The var block is generated (D111).

// tg: code/datums/mocking/client.dm:2
[DmPath("/datum/client_interface")]
public partial class datum_client_interface : datum
{
    // tg: code/datums/mocking/client.dm:46
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);

        byond_version = client.EngineByondVersion; // world.byond_version: the version the port reports
        byond_build = client.EngineByondBuild;

        key = Dm.text("[]_[]", key, TgState.Current.datum_client_interface__mock_client_uid++);
        ckey = Dm.ckey(key);

        // #ifdef UNIT_TESTS: GLOB.directory[ckey] = src and its persistent client (tg's unit-test build only)

        fully_created = TRUE;
    }

    // tg: code/datums/mocking/client.dm:67
    public override object Destroy(float? force = null)
    {
        GLOB.directory.Remove(ckey);
        if (persistent_client != null && ReferenceEquals(persistent_client.client, this))
            persistent_client.set_client(null);
        persistent_client = null;
        return base.Destroy(force);
    }

    // tg: code/datums/mocking/client.dm:74
    public virtual object IsByondMember() => FALSE;

    // tg: code/datums/mocking/client.dm:77
    public virtual void set_macros() { }

    // tg: code/datums/mocking/client.dm:80
    public virtual void update_ambience_pref() { }

    // tg: code/datums/mocking/client.dm:83
    public virtual object get_award_status(object achievement_type = null, mob user = null, object value = null) => FALSE;

    // tg: code/datums/mocking/client.dm:86
    public virtual object set_fullscreen() => TRUE;

    // tg: code/datums/mocking/client.dm:89
    public virtual object check_drag_proximity(atom dragging = null, atom over = null, object src_location = null, object over_location = null, object src_control = null, object over_control = null, object @params = null)
    {
        return FALSE;
    }

    // tg: code/datums/mocking/client.dm:92
    public virtual void update_parallax_prefs() { }
}

public sealed partial class TgState
{
    // tg: code/datums/mocking/client.dm:44  var/static/mock_client_uid = 0
    internal int datum_client_interface__mock_client_uid;
}
