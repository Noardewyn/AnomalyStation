using System.Globalization;
using System.Text;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.DmShim;

// Owner: contracts (complete). The presentation seam (docs/ai/specs/presentation-seam.md): BYOND's output primitives
// become three write-only, world-owned sinks with no-op defaults. Targets are typed object because atoms, mobs
// and clients are tg types.

/// <summary>Messages: the tgchat transport, BYOND <c>target &lt;&lt; text</c> and balloon alerts (presentation-seam.md section 3).</summary>
public interface IMessageSink
{
    /// <summary>
    /// tg chat transport (code/modules/tgchat): what <c>SSchat.queue</c> / <c>SSchat.send_immediate</c> would send.
    /// <paramref name="target"/>: a client, a mob, world, or a DmList of those, exactly as tg passes it to SSchat.
    /// <paramref name="message"/>: tg's associative list with the keys "type", "text", "html", "avoidHighlighting".
    /// </summary>
    void Chat(object target, DmList message);

    /// <summary>BYOND <c>target &lt;&lt; text</c> (DIRECT_OUTPUT with text): plain output to a client, mob or world.</summary>
    void Output(object target, string text);

    /// <summary>tg balloon alert: the floating text one viewer sees above an atom (balloon_alert_perform).</summary>
    void Balloon(object viewer, object atom, string text);

    /// <summary>
    /// M9 (protocol version 38, DV160): one runechat line a client sees over <paramref name="atom"/> (tg's <c>/datum/chatmessage</c>: its image
    /// holds <paramref name="maptext"/> and lives <paramref name="lifespan"/> deciseconds); the client measures, stacks and fades it.
    /// </summary>
    void Runechat(object viewer, object atom, string maptext, float lifespan) { }

    /// <summary>
    /// M9 (protocol version 38): a message from tg's say window datum to the client's say window (<c>window.send_message</c>): "props" with
    /// <paramref name="maxLength"/> and <paramref name="lightMode"/>, "force", "save" or "close".
    /// </summary>
    void SayModal(object client, string type, int maxLength, bool lightMode) { }

    /// <summary>
    /// D307 (DV262): one line a listener's client voices itself (tg's <c>SStts.play_tts</c> plays the TTS server's audio instead): the
    /// <paramref name="text"/> as this listener understands it, in <paramref name="voice"/> at <paramref name="pitch"/> semitones and at
    /// tg's <paramref name="volume"/>, from <paramref name="atom"/> (null: played to the listener only, as tg's local preview). D308: tg's
    /// ffmpeg <paramref name="filter"/> chain and <paramref name="special_filters"/> ("radio", "silicon", joined by "|"), the frequency of a
    /// radio broadcast (0: spoken), and whether the listener is the speaker (<paramref name="own"/>).
    /// </summary>
    void Tts(object listener, object atom, string text, string voice, float pitch, float volume, string filter = null, string special_filters = null, float radio_frequency = 0, bool own = false) { }
}

/// <summary>Sounds (presentation-seam.md section 4).</summary>
public interface ISoundSink
{
    /// <summary>
    /// BYOND <c>target &lt;&lt; sound</c> (tg SEND_SOUND, __DEFINES/logging.dm:191): the sound as computed for one
    /// listener. A sound whose file is null stops its channel.
    /// </summary>
    void Play(object target, sound s);
}

/// <summary>Appearance and light (presentation-seam.md section 5).</summary>
public interface IAppearanceSink
{
    /// <summary>
    /// A builtin appearance var of a registered atom was assigned, or one of its engine lists (overlays,
    /// underlays, vis_contents) was mutated. A dirty hint for replication.
    /// </summary>
    void Changed(object atom, AppearanceAspect aspect);

    /// <summary>
    /// BYOND <c>animate()</c>: target (atom, image or client) and the call's arguments as given (named arguments as
    /// text keys). A call without a target continues the previous animation (target null).
    /// </summary>
    void Animate(object target, DmList arguments);

    /// <summary>
    /// BYOND <c>animate()</c> with what the shim resolved (M4v, protocol version 9): <paramref name="target"/> and
    /// <paramref name="arguments"/> as <see cref="Animate"/> gets them; <paramref name="subject"/> the object the step applies to (the
    /// target, or for a step without one the object of the last call that named one; null when there is none); and
    /// <paramref name="before"/> the values the step's named vars of the subject had before the shim applied it (text keys, a matrix
    /// copied; null without a subject). BYOND animates from those values to the step's. <c>Dm.animate</c> calls this member only;
    /// a sink that does not override it gets <see cref="Animate"/>, so the older contract holds.
    /// </summary>
    void AnimateStep(object target, object subject, DmList arguments, DmList before) => Animate(target, arguments);

    /// <summary>BYOND <c>flick(icon_state or icon, target)</c>.</summary>
    void Flick(object target, object state);

    /// <summary>tg <c>update_light()</c> (lighting module not translated, D24): the light source vars of the atom changed.</summary>
    void LightChanged(object atom);

    /// <summary><c>opacity</c> or <c>invisibility</c> changed: inputs of the client's view, LOS and light computation.</summary>
    void VisibilityChanged(object atom);
}

/// <summary>
/// Replication hints (M1; presentation-seam.md section 1 rule 5, object-model.md section 6): which registered objects may
/// have changed their replicated state during the tick. A hint only selects a candidate; it never carries a value, and the
/// replicator reads the value itself after <c>World.Step</c> and compares it with its own copy, so a missed or extra hint
/// cannot desynchronize a client (a sweep catches what no hint reported). Like every sink it is write-only, synchronous
/// and called inside the world in the order of the simulation, never for an object with <c>dm_id == 0</c> (rules 1, 2 and
/// 4); a sink must only record the object (its id, preferably: a reference kept across ticks would keep the object alive
/// and change the state hash).
/// <para>
/// Sources: <see cref="Moved"/> from every engine move of the atom engine parts (the raw <c>loc</c> write, a DM contents
/// edit applied, a turf replacement's movables); <see cref="AppearanceChanged"/> and <see cref="VisibilityChanged"/> from
/// the same points as <see cref="IAppearanceSink.Changed"/> and <see cref="IAppearanceSink.VisibilityChanged"/>;
/// <see cref="Deleted"/> from the world registry's <c>ObjectRegistry.Unregistered</c>. Creation has no call: the objects
/// created since the replicator last looked are the live registered ones above the id watermark it last read
/// (<c>ObjectRegistry.HighestId</c>).
/// </para>
/// </summary>
public interface IReplicationHintSink
{
    /// <summary>A registered atom's container changed (so may its <c>x</c>, <c>y</c>, <c>z</c>, or whether it stands on a turf).</summary>
    void Moved(object atom);

    /// <summary>A builtin appearance var of a registered atom was assigned (the call <see cref="IAppearanceSink.Changed"/> also gets).</summary>
    void AppearanceChanged(object atom, AppearanceAspect aspect);

    /// <summary><c>opacity</c> or <c>invisibility</c> of a registered atom changed (the call <see cref="IAppearanceSink.VisibilityChanged"/> also gets).</summary>
    void VisibilityChanged(object atom);

    /// <summary>The object of <paramref name="id"/> left the world registry (qdel, a replaced turf).</summary>
    void Deleted(long id);
}

/// <summary>Which appearance aspect of an atom changed (<see cref="IAppearanceSink.Changed"/>).</summary>
public enum AppearanceAspect
{
    Icon,
    IconState,
    Dir,
    Layer,
    Plane,
    Alpha,
    Color,
    Pixel,
    Transform,
    Overlays,
    Underlays,
    VisContents,
    Maptext,
    AppearanceFlags,
    BlendMode,
    Filters,
    Render,
    Glide,

    /// <summary>BYOND <c>name</c> (an appearance var in BYOND; an engine property of <c>/atom</c> since M2, replication.md 5.2).</summary>
    Name,
}

/// <summary>The no-op sinks every world starts with (stateless, shared).</summary>
public static class NullSinks
{
    public static readonly IMessageSink Messages = new NullSink();
    public static readonly ISoundSink Sounds = (ISoundSink)Messages;
    public static readonly IAppearanceSink Appearance = (IAppearanceSink)Messages;

    private sealed class NullSink : IMessageSink, ISoundSink, IAppearanceSink
    {
        public void Chat(object target, DmList message) { }
        public void Output(object target, string text) { }
        public void Balloon(object viewer, object atom, string text) { }
        public void Play(object target, sound s) { }
        public void Changed(object atom, AppearanceAspect aspect) { }
        public void Animate(object target, DmList arguments) { }
        public void Flick(object target, object state) { }
        public void LightChanged(object atom) { }
        public void VisibilityChanged(object atom) { }
    }
}

/// <summary>
/// A world's set of sinks (presentation-seam.md section 2). Hosts install their implementations
/// (<c>PresentationSinks.Of(world).Messages = ...</c>); assigning null restores the no-op sink. Translated code
/// never calls a sink directly: it reaches them through the shim builtins (<c>Dm.output_to</c>, <c>animate</c>,
/// <c>flick</c>, builtin var setters) and the few tg procs replaced at proc level.
/// </summary>
public sealed class PresentationSinks
{
    private readonly World _world;
    private IMessageSink _messages = NullSinks.Messages;
    private ISoundSink _sounds = NullSinks.Sounds;
    private IAppearanceSink _appearance = NullSinks.Appearance;
    private IReplicationHintSink _replication;
    private Action<long> _deleted;

    internal PresentationSinks(World world)
    {
        _world = world;
    }

    /// <summary>The sinks of <paramref name="world"/> (created with null sinks on first use).</summary>
    public static PresentationSinks Of(World world) => DmWorldState.Of(world).Sinks;

    public IMessageSink Messages
    {
        get => _messages;
        set => _messages = value ?? NullSinks.Messages;
    }

    public ISoundSink Sounds
    {
        get => _sounds;
        set => _sounds = value ?? NullSinks.Sounds;
    }

    public IAppearanceSink Appearance
    {
        get => _appearance;
        set => _appearance = value ?? NullSinks.Appearance;
    }

    /// <summary>
    /// The replication hint sink (<see cref="IReplicationHintSink"/>), null by default: unlike the other sinks it has no
    /// no-op stand-in, so a world without a replicator pays one null check per hint site and nothing else. Setting it also
    /// subscribes its <see cref="IReplicationHintSink.Deleted"/> to the world registry's <c>Unregistered</c> event (and
    /// unsubscribes the previous sink's); null removes both.
    /// </summary>
    public IReplicationHintSink Replication
    {
        get => _replication;
        set
        {
            if (ReferenceEquals(value, _replication)) return;
            if (_deleted != null) _world.Objects.Unregistered -= _deleted;
            _replication = value;
            _deleted = value == null ? null : value.Deleted;
            if (_deleted != null) _world.Objects.Unregistered += _deleted;
        }
    }

    /// <summary>
    /// Whether a sink call should be made for <paramref name="target"/>: nothing is reported for an object that is
    /// not registered (<c>dm_id == 0</c>: a prototype or an object still in its constructor), presentation-seam.md
    /// section 1, rule 4. Non-DM targets (text, lists, world, images) pass.
    /// </summary>
    public static bool Reportable(object target) => target is not IDmObject o || o.dm_id != 0;
}

/// <summary>
/// Sinks for tests: every call appends one line to <see cref="Lines"/>, prefixed with the world tick
/// ("tick 12 chat mob#41 notice: You start prying..."), so a test can assert the exact order of events.
/// Objects are written as C# type name and id (<c>mob#41</c>), text as is.
/// </summary>
public sealed class RecordingSinks : IMessageSink, ISoundSink, IAppearanceSink
{
    private readonly World _world;

    public RecordingSinks(World world) => _world = world;

    /// <summary>The recorded lines, in call order.</summary>
    public List<string> Lines { get; } = new();

    /// <summary>Creates a recorder and installs it as all three sinks of <paramref name="world"/>.</summary>
    public static RecordingSinks Install(World world)
    {
        var sinks = new RecordingSinks(world);
        PresentationSinks set = PresentationSinks.Of(world);
        set.Messages = sinks;
        set.Sounds = sinks;
        set.Appearance = sinks;
        return sinks;
    }

    public void Chat(object target, DmList message)
        => Add("chat " + Describe(target) + " " + Describe(message?["type"]) + ": " + Describe(message?["text"]));

    public void Output(object target, string text) => Add("output " + Describe(target) + " " + text);

    public void Balloon(object viewer, object atom, string text) => Add("balloon " + Describe(viewer) + " " + Describe(atom) + " " + text);

    public void Play(object target, sound s)
        => Add("sound " + Describe(target) + " " + Describe(s?.file) + " volume=" + Describe(s?.volume) + " channel=" + Describe(s?.channel));

    public void Changed(object atom, AppearanceAspect aspect) => Add("changed " + Describe(atom) + " " + aspect);

    public void Animate(object target, DmList arguments) => Add("animate " + Describe(target) + " " + DescribeList(arguments));

    public void Flick(object target, object state) => Add("flick " + Describe(target) + " " + Describe(state));

    public void LightChanged(object atom) => Add("light " + Describe(atom));

    public void VisibilityChanged(object atom) => Add("visibility " + Describe(atom));

    private void Add(string line) => Lines.Add("tick " + _world.Tick.ToString(CultureInfo.InvariantCulture) + " " + line);

    /// <summary>The recorder's text for a value: null, text as is, numbers invariant, a resource as its path, objects as type#id.</summary>
    public static string Describe(object value) => value switch
    {
        null => "null",
        string s => s,
        float f => f.ToString("R", CultureInfo.InvariantCulture),
        int i => i.ToString(CultureInfo.InvariantCulture),
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        IDmObject o => o.GetType().Name + "#" + o.dm_id.ToString(CultureInfo.InvariantCulture),
        DmList l => DescribeList(l),
        DmWorld => "world",
        DmResource r => r.Path,
        Type t => t.Name,
        _ => value.GetType().Name,
    };

    private static string DescribeList(DmList list)
    {
        if (list == null) return "null";
        var sb = new StringBuilder("list(");
        for (int i = 1; i <= list.Count; i++)
        {
            if (i > 1) sb.Append(", ");
            object item = list[i];
            sb.Append(Describe(item));
            if (list.TryGetAssociated(item, out object value)) sb.Append('=').Append(Describe(value));
        }
        return sb.Append(')').ToString();
    }
}
