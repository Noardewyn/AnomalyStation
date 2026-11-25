using System.Linq;
using System.Runtime.CompilerServices;
using Godot;
using Anomaly.Client.Render;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;

namespace Anomaly.Client.Hud;

/// <summary>
/// D285 (replaces M4a P7's flat picture of D126): the own hands' things in first person, seen only by their holder. Each hand's item (tg's hand slot
/// carries it as an overlay, HeldHands) is its tg picture made solid the way Minecraft draws a held item (SpriteExtrusion), floating where the hand
/// would hold it (no hands yet), lit by the light grid where the eye is and drawn in front of the world. A taken thing comes up from under the view,
/// one put away goes down, one dropped or thrown is gone at once; tg's attack lunge swings it; a gun is held with its barrel at the crosshair, kicks
/// and flashes at its muzzle when it fires, dips when loaded and jerks back when racked (protocol version 54). The holder's state moves them too: the walk, tg's jitter, dizziness
/// and drunkenness, a hurt arm.
/// </summary>
public partial class ViewModel : Node3D
{
    /// <summary>The held things' own turns (HeldTurns), from <see cref="TurnsPath"/>; the dev panel edits them.</summary>
    public static readonly HeldTurns Turns = HeldTurns.Parse(FileAccess.FileExists(TurnsPath) ? FileAccess.GetFileAsString(TurnsPath) : null);

    /// <summary>The dev panel's tuning of every held thing: the view model's field of view, its size and a shift of every pose (metres), saved with the turns.</summary>
    public static float Fov = Turns.All.Fov, Size = Turns.All.Size;

    private const float FlashStarMetres = 0.56f; // the own muzzle flash's star at the gun's size 1

    public static Vector3 Shift = new(Turns.All.X, Turns.All.Y, Turns.All.Z);

    public const string TurnsPath = "res://assets/held_items.json";

    /// <summary>DV361: the start of a mech arm's tuning key (held_items.json): <c>mech:</c> and its picture's icon file.</summary>
    public const string MechTurnPrefix = "mech:";

    private ReplicaWorld _replica;
    private AppearanceTextures _textures;
    private Camera3D _camera;
    private readonly Hand _left = new(true), _right = new(false);
    private readonly List<Hand> _leaving = new();
    private double _now;
    private double _letGoAt = -10, _firedAt = -10, _reloadAt = -10, _rackAt = -10, _lungeAt = -10;
    private bool _lungeSeen, _wasThrowing, _leftActive, _mechRight, _dirty = true;
    private HeldHandsView _hands;
    private (float Left, float Right) _hurt;

    private static ShaderMaterial _baseMaterial, _glowMaterial;
    // a picture's solids by its flips (HeldFlip.X + 2 * Y)
    private static readonly ConditionalWeakTable<Raster, Built>[] Meshes = { new(), new(), new(), new() };

    /// <summary>Whether the active hand holds something (the doll's click on itself, DragModel).</summary>
    public bool HasItem => !(_leftActive ? _left : _right).Appearance.IsNone;

    /// <summary>The modifiers of the holder this frame (GameScene reads them off the replica).</summary>
    /// <param name="StepSpeed">The walk's bob, 0 to 1 (CameraLayer's: off with the head bob option).</param>
    /// <param name="StridePhase">The walk's phase, radians, a step every pi.</param>
    /// <param name="ShakeX">The own mob's animated pixel offset now (tg's jitter, do_jitter_animation), tg pixels east.</param>
    /// <param name="ShakeY">The same north.</param>
    /// <param name="Dizzy">tg's dizziness wobble is turning the view, 0 to 1.</param>
    /// <param name="Kick">The own gun's last view kick (CameraLayer.Kick, DV96): the held gun kicks as hard and as heavily, and rolls to its side.</param>
    public readonly record struct Holder(float StepSpeed, double StridePhase, double ShakeX, double ShakeY, float Dizzy, HeldKick Kick = default);

    /// <summary>A tg pixel of the own mob's shake moves the held things this much (metres).</summary>
    private const float ShakeMetresPerPixel = 0.004f;

    public void Bind(ReplicaWorld replica, IAppearanceTextures textures, Camera3D camera)
    {
        _replica = replica;
        _textures = textures as AppearanceTextures;
        _camera = camera;
        _ = SwingLooks; // starts reading them
        Name = "ViewModel";
        camera.AddChild(this);
        replica.HeldMotionReceived += OnHeldMotion;
        replica.ScreenEntryChanged += OnScreenChanged;
        replica.ScreenEntryRemoved += OnScreenRemoved;
    }

    public override void _ExitTree()
    {
        if (_replica == null)
            return;
        _replica.HeldMotionReceived -= OnHeldMotion;
        _replica.ScreenEntryChanged -= OnScreenChanged;
        _replica.ScreenEntryRemoved -= OnScreenRemoved;
    }

    private void OnScreenChanged(ScreenEntry entry) => _dirty = true;

    private void OnScreenRemoved(long id) => _dirty = true;

    /// <summary>The active hand's thing's picture (icon file, state), null without one: the dev panel's turn editor.</summary>
    public (string Icon, string State)? ActivePicture
    {
        get
        {
            Hand hand = _leftActive ? _left : _right;
            return hand.Icon == null || hand.State == null ? null : (hand.Icon, hand.State);
        }
    }

    /// <summary>Whether the active hand's thing is wielded (tg's two_handed): the dev panel tunes its own wielded move then.</summary>
    public bool ActiveWielded => Wielded(_leftActive ? _left : _right);

    /// <summary>In a mech, the right arm's equipment picture too (the left's is <see cref="ActivePicture"/>): both arms are drawn and tuned (DV361).</summary>
    public (string Icon, string State)? SecondPicture => !_hands.Mech || _right.Icon == null || _right.State == null ? null : (_right.Icon, _right.State);

    /// <summary>
    /// The own click on the world with the active hand (GameScene.SendClick): a thing that is no gun swings, as Minecraft's does on every click (tg's
    /// attack lunge alone missed hits on the own tile and uses that lunge nothing). The swing whooshes (DV586), an empty hand's in combat mode
    /// only (out of it an empty hand neither swings nor whooshes): its whoosh, null for none. An empty hand's click on an item
    /// (<paramref name="onItem"/>) never swings: tg's empty hand picks an item up in any mode (UnarmedAttack, the item's attack_hand).
    /// </summary>
    public SwingSound.Whoosh? Clicked(bool onItem = false)
    {
        _mechRight = false; // tg's mech: the left button uses the left arm (_mecha.dm:705)
        Hand hand = _leftActive ? _left : _right;
        if (_hands.Mech || hand.Kind == HeldKind.Gun && hand.Node != null || _now - _lungeAt < Math.Max(SwingLength * 0.8, ClickCooldown))
            return null;
        if (hand.Node == null && (!_hands.Combat || onItem))
            return null;
        StartSwing(_now);
        SwingKind kind = SwingSound.KindOf(SwingLooks, hand.Node == null ? null : hand.Icon, hand.State, hand.Kind == HeldKind.Long);
        return new SwingSound.Whoosh(SwingSound.FileOf(kind, _swings++), SwingSound.VolumeOf(kind));
    }

    // tg's items' pictures that swing sharp or heavy (SwingSound.Looks): read once in the background, as it takes seconds; light until then
    private static System.Threading.Tasks.Task<Dictionary<(string Icon, string State), SwingKind>> _swingLooks;
    private int _swings;

    private static Dictionary<(string Icon, string State), SwingKind> SwingLooks
    {
        get
        {
            _swingLooks ??= System.Threading.Tasks.Task.Run(() => SwingSound.Looks(new ReflectionTypeSource()));
            return _swingLooks.IsCompletedSuccessfully ? _swingLooks.Result : null;
        }
    }

    /// <summary>The own right click on the world: in a mech it uses the right arm (tg's _mecha.dm:705), which fires next.</summary>
    public void RightClicked() => _mechRight = true;

    /// <summary>The own drop key or a throw: the thing that leaves a hand next is gone at once instead of going down.</summary>
    public void LetGo() => _letGoAt = _now;

    /// <summary>D176: the own gun fired (tg's firing effect): it kicks back and up and flashes at its muzzle in <paramref name="colour"/> (the flash, if shown).</summary>
    public ShotFlash Fire(Color colour)
    {
        _firedAt = _now;
        // the star only: the flash's light is the world's (EntityLayer's own firing light), not one carried with the camera; it is as big as the
        // gun is drawn (the tools' size for all and for this gun)
        if (!TryMuzzle(out Vector3 at))
            return null;
        Hand hand = GunHand();
        float scale = Size * Turns.Find(hand.Icon, hand.State).Size;
        return ShotFlash.Spawn(_camera, at, colour, FlashStarMetres * scale, 0f, 0f, ShotLook.FlashSeconds);
    }

    // Protocol version 54 (DV213): the own gun was loaded or unloaded (it dips and turns) or racked (it jerks back)
    private void OnHeldMotion(HeldMotionMessage message)
    {
        if (message.Kind == HeldMotionKind.Rack)
            _rackAt = _now;
        else
            _reloadAt = _now;
    }

    /// <summary>
    /// Where the active hand's gun's muzzle looks to be, in world space: its point in the view model's projection carried to the camera's own field
    /// of view at its depth (the own bolt starts there, EntityLayer). False without a shown gun.
    /// </summary>
    public bool TryMuzzle(out Vector3 world)
    {
        world = default;
        Hand hand = GunHand();
        if (!Visible || hand == null || !hand.Node.Visible || hand.Built?.HasMuzzle != true)
            return false;
        Vector3 view = hand.Node.Transform * hand.Built.Muzzle; // in the camera's space
        float k = Mathf.Tan(Mathf.DegToRad(_camera.Fov) / 2f) / Mathf.Tan(Mathf.DegToRad(Fov) / 2f);
        world = _camera.GlobalTransform * new Vector3(view.X * k, view.Y * k, view.Z);
        return true;
    }

    /// <summary>DV96: the gun in hand's own recoil tuning (held_items.json's <c>recoil</c>; 1 without a gun or a tuning).</summary>
    public float RecoilScale
    {
        get
        {
            Hand hand = GunHand();
            return hand == null ? 1f : Turns.Find(hand.Icon, hand.State).Kick;
        }
    }

    private Hand GunHand()
    {
        // DV361: a mech fires the arm of the button clicked last, the other if that one has no gun
        if (_hands.Mech)
        {
            Hand clicked = _mechRight ? _right : _left, unclicked = _mechRight ? _left : _right;
            return clicked.Kind == HeldKind.Gun && clicked.Node != null ? clicked : unclicked.Kind == HeldKind.Gun && unclicked.Node != null ? unclicked : null;
        }
        Hand active = _leftActive ? _left : _right, other = _leftActive ? _right : _left;
        return active.Kind == HeldKind.Gun && active.Node != null ? active : other.Kind == HeldKind.Gun && other.Node != null ? other : null;
    }

    public void Update(double now, bool show, long own, in Holder holder)
    {
        _now = now;
        if (_replica == null)
            return;
        Visible = show;
        if (_dirty)
        {
            _dirty = false;
            _hands = HeldHands.Find(_replica);
            _hurt = HeldHands.ArmHurt(_replica);
        }
        HeldHandsView hands = _hands;
        _leftActive = hands.LeftActive;
        // a click in throw mode throws the active hand's thing
        if (_wasThrowing && !hands.Throwing)
            LetGo();
        _wasThrowing = hands.Throwing;
        if (own != 0 && _replica.TryGet(own, out ReplicaEntity me))
        {
            if (me.LungeDir != 0 && !_lungeSeen && now - _lungeAt >= SwingLength)
                StartSwing(now);
            _lungeSeen = me.LungeDir != 0;
        }
        Track(_left, hands.LeftOffhand ? TableRef.None : hands.Left, now);
        Track(_right, hands.RightOffhand ? TableRef.None : hands.Right, now);
        Pose(_left, now, hands.LeftActive, Wielded(_left), _hurt.Left, holder);
        Pose(_right, now, hands.Mech || !hands.LeftActive, Wielded(_right), _hurt.Right, holder);
        for (int i = _leaving.Count - 1; i >= 0; i--)
        {
            Hand gone = _leaving[i];
            double age = now - gone.LeftAt;
            if (age >= HeldPose.LowerSeconds)
            {
                gone.Node.QueueFree();
                _leaving.RemoveAt(i);
                continue;
            }
            Place(gone, gone.Rest + new HeldOffset(0f, -HeldPose.Drop * HeldPose.Lowered(age), 0f));
        }
    }

    // A hand's thing changed: a new thing comes up (the old one goes down, or is gone at once after a drop or a throw); the same thing's new look
    // (a gun's ammo overlay) is only redrawn.
    private void Track(Hand hand, TableRef appearance, double now)
    {
        if (appearance.Equals(hand.Appearance))
        {
            Refresh(hand, now);
            return;
        }
        string icon = null, state = null;
        if (!appearance.IsNone && _replica.TryGetAppearance(appearance, out AppearanceEntry app))
        {
            icon = _replica.ResolveResource(app.Icon) ?? _replica.ResolveString(app.Icon);
            state = _replica.ResolveString(app.IconState);
            // a thing drawn by its overlays alone (a cut-off limb: no icon state of its own) is known by its first drawn overlay's picture, so it
            // can be tuned
            if (string.IsNullOrEmpty(state))
            {
                foreach (TableRef overlay in app.Overlays)
                {
                    if (!_replica.TryGetAppearance(overlay, out AppearanceEntry o) || _replica.ResolveString(o.IconState) is not { Length: > 0 } overlayState)
                        continue;
                    icon = _replica.ResolveResource(o.Icon) ?? _replica.ResolveString(o.Icon) ?? icon;
                    state = overlayState;
                    break;
                }
            }
        }
        // DV361: a mech's arm is posed as a gun, so it is tuned apart from the held pose's tunings of the same picture (its key's icon is
        // MechTurnPrefix and the file; the dev panel tunes and saves it so)
        if (_hands.Mech && icon != null)
            icon = MechTurnPrefix + icon;
        bool same = !appearance.IsNone && !hand.Appearance.IsNone && HeldHands.SameThing(icon, state, hand.Icon, hand.State);
        if (!same && hand.Node != null)
        {
            if (now - _letGoAt < 1.0)
            {
                hand.Node.QueueFree();
                _letGoAt = -10; // one let-go per thing
            }
            else
            {
                _leaving.Add(new Hand(hand.Left) { Node = hand.Node, Built = hand.Built, Rest = hand.Rest, LeftAt = now });
            }
            hand.Node = null;
            hand.Built = null;
        }
        if (!same)
            hand.TakenAt = now;
        hand.Appearance = appearance;
        hand.Icon = icon;
        hand.State = state;
        // DV361: a mech's arm equipment is mounted as a gun: aimed at the crosshair, kicking and flashing at its muzzle when it fires
        hand.Kind = HeldHands.IsGunIcon(icon) || _hands.Mech ? HeldKind.Gun : HeldKind.Small;
        Refresh(hand, now);
    }

    // The hand's mesh for its thing's picture now (an animated icon, a filter's change: a new picture, a new solid)
    private void Refresh(Hand hand, double now)
    {
        if (hand.Appearance.IsNone || _textures == null
            || !_textures.TryGet(_textures.TablesOf(_replica), hand.Appearance, ByondDir.South, now, out ComposedRaster raster, out _))
        {
            if (hand.Appearance.IsNone && hand.Node != null)
            {
                hand.Node.QueueFree();
                hand.Node = null;
                hand.Built = null;
            }
            return;
        }
        HeldFlip flips = Turns.FlipOf(hand.Icon, hand.State, hand.Kind, Wielded(hand));
        int flip = (flips.X ? 1 : 0) + (flips.Y ? 2 : 0);
        if (ReferenceEquals(hand.Built?.Picture, raster.Image) && hand.Built.Flip == flip)
            return;
        Built built = Meshes[flip].GetValue(raster.Image, image => Build(image, hand.Kind == HeldKind.Gun, flips.X, flips.Y));
        if (built == null)
            return;
        hand.Built = built;
        if (hand.Kind != HeldKind.Gun)
            hand.Kind = built.Long ? HeldKind.Long : HeldKind.Small;
        if (hand.Node == null)
        {
            hand.Node = new MeshInstance3D { Name = hand.Left ? "LeftItem" : "RightItem", CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Layers = Light.LightLayer.HeldLayer };
            AddChild(hand.Node);
        }
        hand.Node.Mesh = built.Mesh;
        hand.Node.MaterialOverride = built.Material;
        // a picture is lit or not for good (tg's "-on" look is its own picture), so its shared material keeps it
        built.Material.SetShaderParameter("lit", HeldHands.IsLit(_replica, hand.Appearance) ? 1f : 0f);
    }

    // tg's two_handed: the hand's thing is wielded (the other hand holds its offhand)
    private bool Wielded(Hand hand) => !_hands.Wielded.IsNone && _hands.Wielded.Equals(hand.Appearance);

    // The clip playing now (HeldClip.For), null for the code's swing (HeldPose.Swing: a gun's, or any thing's when the clips are missing)
    private HeldClip _clip;

    private double SwingLength => _clip?.Length ?? HeldPose.SwingSeconds;

    // tg's CLICK_CD_MELEE (0.8 s) and a tick of slack: the server takes no blow before it, so a click sooner neither swings nor whooshes
    // shortcut: every held thing is taken at tg's default attack_speed, the dozen tg things with their own (a high-frequency blade) swing at it
    private const double ClickCooldown = Anomaly.Core.Tg.Defines.CLICK_CD_MELEE / 10.0 + 0.05;

    // the held swings' clips from the models folder, read once; none when the file is missing or broken
    private static Dictionary<string, HeldClip> _clips;

    private static HeldClip Clip(string name)
    {
        if (_clips == null)
        {
            _clips = new Dictionary<string, HeldClip>();
            string path = System.IO.Path.Combine(Render.DataPaths.Find("assets/models"), HeldClip.File);
            try
            {
                Shared.Models.GlbFile file = Shared.Models.GlbFile.Read(System.IO.File.ReadAllBytes(path));
                foreach (string clip in HeldClip.Names)
                    _clips[clip] = HeldClip.Of(file, clip);
            }
            catch (System.Exception e) when (e is System.IO.IOException or System.IO.InvalidDataException or System.UnauthorizedAccessException)
            {
                GD.PushWarning($"held swings: {path}: {e.Message}");
                _clips.Clear();
            }
        }
        return _clips.GetValueOrDefault(name);
    }

    private void StartSwing(double now)
    {
        Hand hand = _leftActive ? _left : _right;
        bool pointy = SwingSound.KindOf(SwingLooks, hand.Icon, hand.State, false) == SwingKind.Pointy;
        _clip = hand.Node == null || hand.Kind == HeldKind.Gun ? null : Clip(Turns.ClipOf(hand.Icon, hand.State, Wielded(hand), pointy));
        _lungeAt = now;
    }

    private void Pose(Hand hand, double now, bool active, bool wielded, float hurt, in Holder holder)
    {
        if (hand.Node == null)
            return;
        float delta = (float)GetProcessDeltaTime();
        hand.ActiveBlend = Mathf.MoveToward(hand.ActiveBlend, active ? 1f : 0f, delta / 0.15f);
        hand.WieldBlend = Mathf.MoveToward(hand.WieldBlend, wielded ? 1f : 0f, delta / (float)HeldPose.WieldSeconds);
        hand.Rest = HeldPose.Rest(hand.Kind, hand.Left, Turns.WieldOf(hand.Icon, hand.State, hand.Kind) * Smoothstep(hand.WieldBlend));
        HeldOffset pose = hand.Rest
            + new HeldOffset(0f, -HeldPose.Drop * HeldPose.Raised(now - hand.TakenAt) - HeldPose.InactiveDrop * (1f - hand.ActiveBlend), 0.02f * (1f - hand.ActiveBlend))
            + HeldPose.Breath(now + (hand.Left ? 1.3 : 0))
            + HeldPose.Bob(holder.StridePhase + (hand.Left ? Math.PI : 0), holder.StepSpeed)
            + HeldPose.Float(now, holder.Dizzy)
            + new HeldOffset((float)holder.ShakeX * ShakeMetresPerPixel, (float)holder.ShakeY * ShakeMetresPerPixel, 0f)
            + HeldPose.Hurt(now, hurt, hand.Left);
        HeldOffset swing = default;
        System.Numerics.Quaternion arc = System.Numerics.Quaternion.Identity;
        if (active)
        {
            if (_clip != null)
                (swing, arc) = _clip.At(now - _lungeAt, hand.Left); // the Blender clip: the hands' move, and the thing's turn about them
            else
                swing = hand.Left ? HeldPose.Swing(now - _lungeAt).Mirrored : HeldPose.Swing(now - _lungeAt);
            pose += new HeldOffset(swing.X, swing.Y, swing.Z);
            if (hand.Kind == HeldKind.Gun)
            {
                if (!_hands.Mech || hand == GunHand()) // a mech's two arms are both up: only the one that fired kicks
                    pose += HeldPose.Kick(now - _firedAt, holder.Kick);
                HeldOffset reload = HeldPose.Reload(now - _reloadAt) + HeldPose.Rack(now - _rackAt);
                pose += hand.Left ? reload.Mirrored : reload;
            }
        }
        Place(hand, pose, swing, arc);
    }

    private static float Smoothstep(float t) => t * t * (3f - 2f * t);

    // swing and arc: their turns are in the camera's space about the grip, after every other turn (HeldPose.Swing, HeldClip)
    private void Place(Hand hand, HeldOffset pose, HeldOffset swing = default, System.Numerics.Quaternion? arc = null)
    {
        HeldTurn config = Turns.Find(hand.Icon, hand.State);
        // the config's own move and the dev panel's shift, the left hand's mirrored
        var moved = new Vector3(hand.Left ? -(config.X + Shift.X) : config.X + Shift.X, config.Y + Shift.Y, config.Z + Shift.Z);
        var at = new Vector3(pose.X, pose.Y, pose.Z) + moved;
        Basis turn = Basis.FromEuler(new Vector3(Mathf.DegToRad(pose.Pitch), Mathf.DegToRad(pose.Yaw), Mathf.DegToRad(pose.Roll)));
        Basis own = TurnOf(hand);
        Basis basis = turn * own;
        if (hand.Kind == HeldKind.Long && hand.Built != null)
        {
            // Minecraft's held tool: whatever way tg drew it, its handle is down and its head up and toward the middle of the view. The left hand
            // holds tg's picture as drawn (its head up and right, as tg mostly draws a tool); the right hand holds the left's mirror image, as a
            // real right hand shows the other side of the same thing
            Basis mirrored = Basis.FromEuler(new Vector3(Mathf.DegToRad(pose.Pitch), Mathf.DegToRad(hand.Left ? pose.Yaw : -pose.Yaw), Mathf.DegToRad(hand.Left ? pose.Roll : -pose.Roll)));
            basis = mirrored * Basis.FromEuler(new Vector3(0f, 0f, Mathf.DegToRad(HeldPose.LongLean) - hand.Built.HeadAngle)) * own;
            if (!hand.Left)
                basis = Basis.FromScale(new Vector3(-1f, 1f, 1f)) * basis;
        }
        if (hand.Kind == HeldKind.Gun && hand.Built != null)
        {
            // tg's guns point east in their picture: turned so the barrel (the picture's level, not the slant from the grip to the muzzle) points at
            // the crosshair's point ahead
            Basis side = Basis.FromEuler(new Vector3(0f, Mathf.Pi / 2f, 0f));
            Vector3 barrel = side * Vector3.Right;
            Vector3 aim = (new Vector3(0f, 0f, -HeldPose.Converge) - (new Vector3(hand.Rest.X, hand.Rest.Y, hand.Rest.Z) + moved)).Normalized();
            basis = turn * new Basis(new Quaternion(barrel, aim)) * side * own;
        }
        basis = Basis.FromEuler(new Vector3(Mathf.DegToRad(swing.Pitch), Mathf.DegToRad(swing.Yaw), Mathf.DegToRad(swing.Roll))) * basis;
        if (arc is System.Numerics.Quaternion q)
            basis = new Basis(new Quaternion(q.X, q.Y, q.Z, q.W)) * basis;
        hand.Node.Transform = new Transform3D(basis.Scaled(Vector3.One * (Size * config.Size)), at);
    }

    // The config's own turn of the hand's picture (HeldTurns)
    private static Basis TurnOf(Hand hand)
    {
        HeldTurn t = Turns.Find(hand.Icon, hand.State);
        return t.IsNone ? Basis.Identity : Basis.FromEuler(new Vector3(Mathf.DegToRad(t.Pitch), Mathf.DegToRad(t.Yaw), Mathf.DegToRad(t.Roll)));
    }

    // A picture's solid, its grip (a gun's and a long thing's by its handle, a small thing's by its middle), its muzzle and its materials; a
    // flipped picture is mirrored first, so its grip, head and muzzle are found on the mirrored picture
    private static Built Build(Raster picture, bool gun, bool flipX, bool flipY)
    {
        Raster image = flipX || flipY ? picture.Flipped(flipX, flipY) : picture;
        int x0 = image.Width, y0 = image.Height, x1 = -1, y1 = -1;
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                if (image.Pixels[(y * image.Width + x) * 4 + 3] < SpriteExtrusion.AlphaCut)
                    continue;
                x0 = Math.Min(x0, x);
                y0 = Math.Min(y0, y);
                x1 = Math.Max(x1, x);
                y1 = Math.Max(y1, y);
            }
        }
        if (x1 < 0)
            return null;
        (double gx, double gy) = HeldItemGrip.Grip(image, (x0 + x1 + 1) / 2.0, (y0 + y1 + 1) / 2.0);
        bool isLong = gun || HeldItemGrip.IsLarge(x1 - x0 + 1, y1 - y0 + 1);
        float pixel = HeldPose.PixelMetres(gun ? HeldKind.Gun : isLong ? HeldKind.Long : HeldKind.Small);
        ExtrudedSprite solid = SpriteExtrusion.Build(image, gx, gy, pixel, gun ? HeldPose.GunThickness : HeldPose.Thickness);
        if (solid == null)
            return null;
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = Vectors3(solid.Positions);
        arrays[(int)Mesh.ArrayType.Normal] = Vectors3(solid.Normals);
        var uvs = new Vector2[solid.Uvs.Length / 2];
        for (int i = 0; i < uvs.Length; i++)
            uvs[i] = new Vector2(solid.Uvs[i * 2], solid.Uvs[i * 2 + 1]);
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Index] = solid.Indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        ImageTexture texture = ImageTexture.CreateFromImage(AppearanceTextures.ToImage(solid.Texture));
        _baseMaterial ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/held_item_lit.gdshader") };
        _glowMaterial ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/held_item_glow.gdshader") };
        var glow = (ShaderMaterial)_glowMaterial.Duplicate();
        glow.SetShaderParameter("picture", texture);
        glow.SetShaderParameter("view_fov", Fov);
        var material = (ShaderMaterial)_baseMaterial.Duplicate();
        material.SetShaderParameter("picture", texture);
        material.SetShaderParameter("view_fov", Fov);
        material.NextPass = glow;
        bool hasMuzzle = ShotLook.TryMuzzle(image.Pixels, image.Width, image.Height, out float mx, out float my);
        // the way from the grip to the drawn pixels' middle (the head's side), y up
        double cx = 0, cy = 0;
        int count = 0;
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                if (image.Pixels[(y * image.Width + x) * 4 + 3] < SpriteExtrusion.AlphaCut)
                    continue;
                cx += x + 0.5;
                cy += y + 0.5;
                count++;
            }
        }
        return new Built
        {
            Picture = picture,
            Flip = (flipX ? 1 : 0) + (flipY ? 2 : 0),
            Mesh = mesh,
            Material = material,
            Long = isLong,
            HeadAngle = (float)Math.Atan2(gy - cy / count, cx / count - gx),
            HasMuzzle = gun && hasMuzzle,
            Muzzle = new Vector3((float)((mx - gx) * pixel), (float)((gy - my) * pixel), 0f),
        };
    }

    private static Vector3[] Vectors3(float[] xyz)
    {
        var v = new Vector3[xyz.Length / 3];
        for (int i = 0; i < v.Length; i++)
            v[i] = new Vector3(xyz[i * 3], xyz[i * 3 + 1], xyz[i * 3 + 2]);
        return v;
    }

    /// <summary>The dev panel moved the field of view: every built material takes it.</summary>
    public void ApplyFov()
    {
        foreach (Hand hand in new[] { _left, _right }.Concat(_leaving))
        {
            if (hand.Built == null)
                continue;
            hand.Built.Material.SetShaderParameter("view_fov", Fov);
            ((ShaderMaterial)hand.Built.Material.NextPass).SetShaderParameter("view_fov", Fov);
        }
    }

    private sealed class Built
    {
        public Raster Picture; // as tg draws it, before a flip
        public int Flip;
        public ArrayMesh Mesh;
        public ShaderMaterial Material;
        public bool Long, HasMuzzle;
        public float HeadAngle; // radians from the picture's right, counter-clockwise: where the head is from the grip
        public Vector3 Muzzle; // from the grip, in the mesh's space
    }

    private sealed class Hand(bool left)
    {
        public readonly bool Left = left;
        public TableRef Appearance;
        public string Icon, State;
        public HeldKind Kind;
        public MeshInstance3D Node;
        public Built Built;
        public double TakenAt = -10, LeftAt;
        public float ActiveBlend, WieldBlend;
        public HeldOffset Rest;
    }
}
