// Engine ("BYOND builtin") part of tg's /mob (docs/ai/translation-rules.md 1.1; docs/ai/specs/object-model.md section 2).
// Not translated from a tg file: BYOND declares /mob and its builtin vars; tg's own vars and procs are in
// modules/mob/mob_defines.cs and the other mirror files.
//
// Owner in M1: P1 (the client layer). The key and ckey vars carry BYOND's engine behaviour (BYOND ref /mob/var/key): a
// mob's key links it to the player of that key. Pinned in docs/ai/specs/dm-conformance.md, section "The /client engine
// part (M1 P1)".
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Core.Tg;

// RULE-GAP: as for /datum (Builtin/datum.builtin.cs), the engine part of a BYOND builtin type carries its [DmPath].
[DmPath("/mob")]
public partial class mob : atom_movable
{
    /// <summary>
    /// BYOND's defaults of /mob: dense, layer 4 (MOB_LAYER; BYOND ref <c>/atom/var/density</c>, <c>layer</c>; OpenDream
    /// DMStandard/Types/Atoms/Mob.dm). tg's overrides of /mob follow in <see cref="dm_tg_vars"/> (rules 10.2, "Builtin
    /// types").
    /// </summary>
    public mob()
    {
        density = TRUE;
        layer = MOB_LAYER;
        dm_tg_vars();
    }

    // tg's overrides of /mob's defaults, implemented in modules/mob/mob_defines.cs (rules 10.2, "Builtin types").
    private partial void dm_tg_vars();

    // The values of key and ckey; hashed through these backing fields (object-model.md section 8).
    private string _key;
    private string _ckey;

    /// <summary>
    /// BYOND <c>ckey</c>: the canonical form of the player's key. Assigning it links the mob to that player as an
    /// assignment of <see cref="key"/> does (tg's <c>PossessByPlayer</c> does <c>src.ckey = ckey(ckey)</c>, mob.dm:119),
    /// but keeps <see cref="key"/> until a player attaches (BYOND's symmetry then sets the player's key).
    /// </summary>
    public string ckey
    {
        get => _ckey;
        set => dm_set_key(value, keyText: false);
    }

    /// <summary>
    /// BYOND <c>key</c> (BYOND ref <c>/mob/var/key</c>): the player's key, or for a mob without a player its "desired"
    /// key. Assigning it connects a connected player of that key to this mob (Logout on the player's old mob, Login on
    /// this one), and any other mob with the same key loses it; keys are compared in canonical form.
    /// </summary>
    public string key
    {
        get => _key;
        set => dm_set_key(value, keyText: true);
    }

    /// <summary>
    /// BYOND <c>verbs</c> of a mob (silicons SI4, D326): the verbs tg adds and removes at run time (add_verb, remove_verb and SSverbs'
    /// assign_verb), by their verb paths. BYOND's list also starts with the verbs the mob's type declares; the port reads those from the
    /// type (<see cref="CommandVerbs"/>), so the list holds the run-time ones only.
    /// </summary>
    public DmList verbs { get; } = Dm.list();

    /// <summary>BYOND <c>client</c>: the player's client, null for a mob without a player. Kept by the client's engine part (<c>client.dm_attach</c>); tg never assigns a mob's client.</summary>
    public client client;
    /// <summary>BYOND <c>sight</c>.</summary>
    public float sight;
    /// <summary>BYOND <c>see_invisible</c>.</summary>
    public float see_invisible;
    /// <summary>BYOND <c>see_in_dark</c> (default 2).</summary>
    public float see_in_dark = 2;
    /// <summary>BYOND <c>see_infrared</c>.</summary>
    public float see_infrared;

    /// <summary>
    /// BYOND's own <c>/mob/Logout</c>, which tg's <c>..()</c> reaches (logout.dm:7; rules 7.5). BYOND ref
    /// <c>/mob/proc/Logout</c>, default action: none.
    /// </summary>
    internal void Logout__d0()
    {
    }

    /// <summary>
    /// The canonical form of a key (BYOND ref <c>/mob/var/key</c>: "Key values are always compared in canonical form (ie
    /// the form returned by ckey())"), except that a leading <c>@</c> stays: tg's admin ghosts give a body the key
    /// <c>"@[key]"</c> and read the <c>@</c> back from its ckey (<c>__DEFINES/client.dm:8</c>, <c>mob.dm:29</c>).
    /// </summary>
    internal static string dm_canonical_key(string value)
    {
        if (value == null)
            return null;
        if (value.Length > 0 && value[0] == '@')
            return "@" + Dm.ckey(value.Substring(1));
        return Dm.ckey(value);
    }

    // The symmetry's side of an attachment (client.dm_attach): the key and its canonical form, no engine behaviour.
    internal void dm_raw_key(string value)
    {
        _key = value;
        dm_store_ckey(dm_canonical_key(value));
    }

    // Every write of the ckey goes here: the engine's index of keyed mobs (client.dm_index_keyed, M3 I1d) learns the mob
    // under its new ckey. The index only finds candidates; a lookup checks each one's ckey, so a cleared key needs nothing.
    private void dm_store_ckey(string value)
    {
        _ckey = value;
        if (value != null)
            client.dm_index_keyed(this, value);
    }

    // An assignment of key (keyText) or ckey: BYOND's linkage of the mob to the player of that key.
    private void dm_set_key(string value, bool keyText)
    {
        string canonical = dm_canonical_key(value);
        if (canonical == _ckey)
        {
            // The same key in canonical form: nothing moves.
            if (keyText)
                _key = value;
            else
                dm_store_ckey(value);
            return;
        }
        // A player attached to this mob under another key leaves it first (Logout; the mob's key becomes null and is set
        // below).
        client attached = client;
        if (attached != null && attached.dm_connected && attached.mob == this && attached.ckey != canonical)
            attached.mob = null;
        if (canonical != null)
        {
            // "Any other mob with the same key will lose it." (for (var/mob/keyed in world), through the engine's index of
            // keyed mobs: the same mobs, without a scan of every atom; M3 I1d, the join stall)
            foreach (mob keyed in client.dm_mobs_with_key(canonical))
            {
                if (keyed != this)
                    keyed.dm_raw_key(null);
            }
        }
        if (keyText)
            dm_raw_key(value);
        else
            dm_store_ckey(value);

        // "Setting the mob's key will cause a client with the same key to connect to the mob."
        client player = client.dm_connected_client(canonical);
        if (player != null && player.mob != this)
            player.mob = this;
    }
}
