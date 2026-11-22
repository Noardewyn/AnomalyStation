using System.Globalization;
using ImGuiNET;
using Anomaly.Client.Shared.Dev;
using Anomaly.Protocol;

namespace Anomaly.Client;

// The dev menu's Player tab (the owner, 2026-10-13: "all the health stats and characteristics of the selected player, and
// change the stats of any online player, not only mine"; "move the godmode, wallhack and similar switches there"; D140):
// the online players from the server's mobinfo answer, the chosen one's numbers (asked twice a second while the tab is on
// screen; the answer is a dev_mob ServerText, MobInfoModel), its dev switches and the dev commands aimed at it.
public partial class GameScene
{
    private const double MobInfoSeconds = 0.5;

    private MobInfoModel _mobInfo;
    private string _mobTarget = "me";
    private double _nextMobInfoAt;
    private double _playerTabDrawnAt = double.MinValue;
    private int _damageTypeIndex, _damageZoneIndex = 1;
    private float _damageAmount = 20f, _statusSeconds = 3f, _bloodMl = 560f, _effectNumber = 10f, _ownSpeed = 1f;
    private float _respawnSeconds = 10f;
    private bool _respawnSecondsEdited;
    private int _effectTypeIndex;
    private static readonly string[] DamageTypes = { "brute", "burn", "tox", "oxy" };
    private static readonly string[] Zones = { "(no zone)", "head", "chest", "l_arm", "r_arm", "l_leg", "r_leg" };

    // Asked while the tab was drawn within the last second (the menu open on it).
    private void UpdateMobInfo(double now)
    {
        FollowOwnMob();
        if (!_devMenuOpen || now - _playerTabDrawnAt > 1.0 || now < _nextMobInfoAt) return;
        _nextMobInfoAt = now + MobInfoSeconds;
        _session.DevCommand("mobinfo " + _mobTarget); // not through the console: no echo in its log
    }

    // D346 (the owner, 2026-11-21: revive and heal the body you are in): when the own mob changes (possess, unpossess, a ghost, a respawn),
    // a panel aimed at the own previous mob by its world id ("#id (you)" in the list) follows to the new one ("me"), so its Revive, Heal and
    // the rest act on the body played now, not on the one left behind.
    private long _panelOwnEntity;

    private void FollowOwnMob()
    {
        long own = _feed.OwnEntity;
        if (own == _panelOwnEntity) return;
        long was = _panelOwnEntity;
        _panelOwnEntity = own;
        if (was != 0 && _mobTarget == "#" + was.ToString(CultureInfo.InvariantCulture))
            SetMobTarget("me");
    }

    // A dev_mob ServerText: the panel's, not the chat's.
    private bool TakeMobInfo(ServerText text)
    {
        if (text.Type != MobInfoModel.TextType) return false;
        _mobInfo = MobInfoModel.Parse(System.Net.WebUtility.HtmlDecode(text.Html ?? string.Empty));
        return true;
    }

    private void DrawPlayerTab()
    {
        double now = App.NowSeconds;
        if (now - _playerTabDrawnAt > 1.0) _nextMobInfoAt = 0; // just opened: ask at once
        _playerTabDrawnAt = now;

        // the player: me, or an online one (the mob's world id)
        string label = _mobTarget == "me" ? "me" : PlayerLabel(_mobTarget);
        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##player", "Player: " + label))
        {
            if (ImGui.Selectable("me", _mobTarget == "me")) SetMobTarget("me");
            if (_mobInfo != null)
            {
                foreach (MobInfoPlayer p in _mobInfo.Players)
                {
                    string id = "#" + p.Id.ToString(CultureInfo.InvariantCulture);
                    bool you = p.Id == _feed.OwnEntity;
                    // D346: yourself is "me", the body you play whichever it is (a fixed id would stay on a body you left)
                    if (ImGui.Selectable(p.Key + "   " + id + (you ? "  (you)" : ""), you ? _mobTarget == "me" : _mobTarget == id)) SetMobTarget(you ? "me" : id);
                }
            }
            ImGui.EndCombo();
        }
        if (_mobInfo == null)
        {
            ImGui.TextDisabled("Asking the server (its dev commands must be on)...");
            return;
        }
        DrawAutoRespawn();
        DrawPossess();
        if (_mobInfo.Error != null)
        {
            ImGui.TextColored(new System.Numerics.Vector4(1f, 0.5f, 0.4f, 1f), _mobInfo.Error);
            return;
        }

        DrawOnlinePlayers();
        string who = _mobTarget;
        if (_mobInfo.IsBodilessGhost)
        {
            ImGui.TextDisabled("A ghost without a body (" + _mobInfo.Value("Ghost", "name") + "): revive gives it a new one.");
            if (ImGui.Button("Revive")) OnConsoleSubmitted("revive " + who);
            return;
        }
        DrawOwnSpeed();

        if (ImGui.CollapsingHeader("Switches and commands", ImGuiTreeNodeFlags.DefaultOpen))
        {
            Switch("Godmode (no damage)", "godmode", who);
            ImGui.SameLine();
            Switch("Noclip (through walls)", "noclip", who);
            ImGui.SameLine();
            Switch("Fly", "fly", who);
            ImGui.SameLine();
            Switch("Invulnerable (no one can act on you)", "invuln", who);
            ImGui.SameLine();
            Switch("See ghosts", "esp", who);
            ImGui.SameLine();
            Switch("Invisible (like a ghost)", "invisimin", who);
            if (ImGui.Button("All three")) OnConsoleSubmitted("god " + who);

            if (ImGui.Button("Heal")) OnConsoleSubmitted("heal " + who);
            ImGui.SameLine();
            if (ImGui.Button("Full heal")) OnConsoleSubmitted("fullheal " + who);
            ImGui.SameLine();
            if (ImGui.Button("Revive")) OnConsoleSubmitted("revive " + who);
            ImGui.SameLine();
            if (ImGui.Button("Kill")) OnConsoleSubmitted("kill " + who);
            ImGui.SameLine();
            if (ImGui.Button("Analyze")) OnConsoleSubmitted("analyze " + who);

            float w = 110 * _imgui.UiScale;
            ImGui.SetNextItemWidth(w);
            ImGui.Combo("##dtype", ref _damageTypeIndex, DamageTypes, DamageTypes.Length);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(w);
            ImGui.Combo("##dzone", ref _damageZoneIndex, Zones, Zones.Length);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(w * 1.5f);
            ImGui.SliderFloat("##damount", ref _damageAmount, 1f, 200f, "%.0f");
            ImGui.SameLine();
            if (ImGui.Button("Damage"))
                OnConsoleSubmitted(Inv($"damage {who} {DamageTypes[_damageTypeIndex]} {_damageAmount:0}{(_damageZoneIndex > 0 ? " " + Zones[_damageZoneIndex] : "")}"));
            ImGui.SameLine();
            if (ImGui.Button("Heal type")) OnConsoleSubmitted("heal " + who + " " + DamageTypes[_damageTypeIndex]);

            ImGui.SetNextItemWidth(w * 1.5f);
            ImGui.SliderFloat("##status", ref _statusSeconds, 0.5f, 60f, "%.1f s");
            ImGui.SameLine();
            if (ImGui.Button("Knockdown")) OnConsoleSubmitted(Inv($"knockdown {who} {_statusSeconds:0.#}"));
            ImGui.SameLine();
            if (ImGui.Button("Stun")) OnConsoleSubmitted(Inv($"stun {who} {_statusSeconds:0.#}"));

            ImGui.SetNextItemWidth(w * 1.5f);
            ImGui.SliderFloat("##blood", ref _bloodMl, 0f, 2000f, "%.0f ml");
            ImGui.SameLine();
            if (ImGui.Button("Set blood")) OnConsoleSubmitted(Inv($"blood {who} {_bloodMl:0}"));
            ImGui.SameLine();
            if (ImGui.Button("All-access ID")) OnConsoleSubmitted("give " + who + " /obj/item/card/id/advanced/debug");
        }

        DrawStatusEffects(who);

        foreach ((string name, List<MobInfoRow> rows) in _mobInfo.Sections)
        {
            if (name is "Dev" or "Respawn" or "Possess") continue; // the switches above
            if (name is MobInfoModel.EffectsSection or MobInfoModel.EffectTypesSection) continue; // DrawStatusEffects
            if (!ImGui.CollapsingHeader(name + "##mi", name is "Mob" or "Damage" or "Blood" or "Body" ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None))
                continue;
            if (name == "Traits")
            {
                foreach (MobInfoRow row in rows) ImGui.TextWrapped(row.Value);
                continue;
            }
            if (!ImGui.BeginTable("##t" + name, 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit)) continue;
            foreach (MobInfoRow row in rows)
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.TextDisabled(row.Key);
                ImGui.TableSetColumnIndex(1);
                ImGui.TextUnformatted(row.Value);
            }
            ImGui.EndTable();
        }
    }

    // The owner (2026-10-30): tp goes to the player, tp to me brings the player here (tg's Jump To Mob and Get Mob).
    private void DrawOnlinePlayers()
    {
        if (!ImGui.BeginTable("##online", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit)) return;
        foreach (MobInfoPlayer p in _mobInfo.Players)
        {
            if (p.Id == _feed.OwnEntity) continue;
            string id = "#" + p.Id.ToString(CultureInfo.InvariantCulture);
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.TextUnformatted(p.Key + "   " + id);
            ImGui.TableSetColumnIndex(1);
            // not "##" + id: "###" makes the part after it the whole ID, so both buttons were one item
            if (ImGui.SmallButton("tp##go" + id)) OnConsoleSubmitted("tp me " + id);
            ImGui.TableSetColumnIndex(2);
            if (ImGui.SmallButton("tp to me##get" + id)) OnConsoleSubmitted("tp " + id + " me");
        }
        ImGui.EndTable();
    }

    // The owner (2026-10-30): my speed only, over the Commands tab's run speed for everyone; x1 is that speed. Sent when the
    // slider is let go; the client does not read it back.
    private void DrawOwnSpeed()
    {
        ImGui.SetNextItemWidth(165 * _imgui.UiScale);
        ImGui.SliderFloat("My speed", ref _ownSpeed, 0.25f, 4f, "x%.2f");
        if (ImGui.IsItemDeactivatedAfterEdit()) SetOwnSpeed(_ownSpeed);
        foreach (float x in new[] { 1f, 2f, 4f })
        {
            ImGui.SameLine();
            if (ImGui.Button(Inv($"x{x:0}##own"))) SetOwnSpeed(x);
        }
    }

    private void SetOwnSpeed(float x)
    {
        _ownSpeed = x;
        OnConsoleSubmitted(Inv($"speed me {x:0.##}"));
    }

    // The owner (2026-10-15): the chosen player's status effects (tg's buffs and debuffs) with their time left and a remove
    // button each, and an add field over the types the server has live (effect WHO add|remove, apps.md 2.17).
    private void DrawStatusEffects(string who)
    {
        List<MobInfoRow> effects = _mobInfo.StatusEffects;
        if (!ImGui.CollapsingHeader(Inv($"Status effects ({effects.Count})##mi"), ImGuiTreeNodeFlags.DefaultOpen))
            return;
        if (effects.Count == 0)
            ImGui.TextDisabled("none");
        else if (ImGui.BeginTable("##teffects", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
        {
            foreach (MobInfoRow row in effects)
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.TextUnformatted(row.Value);
                ImGui.TableSetColumnIndex(1);
                ImGui.TextDisabled(row.Key);
                ImGui.TableSetColumnIndex(2);
                if (ImGui.SmallButton("Remove##" + row.Key))
                {
                    OnConsoleSubmitted(MobInfoModel.RemoveEffectCommand(who, row.Key));
                    _nextMobInfoAt = 0; // show it gone at once
                }
            }
            ImGui.EndTable();
        }
        List<string> types = _mobInfo.EffectTypes;
        if (types.Count == 0) return;
        _effectTypeIndex = Math.Clamp(_effectTypeIndex, 0, types.Count - 1);
        float w = 110 * _imgui.UiScale;
        ImGui.SetNextItemWidth(w * 2.5f);
        if (ImGui.BeginCombo("##effecttype", types[_effectTypeIndex]))
        {
            for (int i = 0; i < types.Count; i++)
            {
                if (ImGui.Selectable(types[i], i == _effectTypeIndex)) _effectTypeIndex = i;
            }
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(w * 1.5f);
        ImGui.SliderFloat("##effectn", ref _effectNumber, 0f, 120f, _effectNumber > 0f ? "%.1f s / stacks" : "tg's default");
        ImGui.SameLine();
        if (ImGui.Button("Add effect"))
        {
            OnConsoleSubmitted(MobInfoModel.AddEffectCommand(who, types[_effectTypeIndex], _effectNumber));
            _nextMobInfoAt = 0;
        }
    }

    // DV580 (the owner, 2026-11-21): the server's auto-respawn for every player, read back from the mobinfo answer's Respawn rows.
    private void DrawAutoRespawn()
    {
        if (!ImGui.CollapsingHeader("Auto-respawn (all players)##respawn", ImGuiTreeNodeFlags.DefaultOpen))
            return;
        bool on = _mobInfo.Value("Respawn", "auto") == "on";
        if (!_respawnSecondsEdited && float.TryParse(_mobInfo.Value("Respawn", "delay"), NumberStyles.Float, CultureInfo.InvariantCulture, out float delay))
            _respawnSeconds = delay;
        if (ImGui.Checkbox("Respawn the dead in a new body at a spawn point", ref on))
        {
            OnConsoleSubmitted(Inv($"autorespawn {(on ? "on" : "off")} {_respawnSeconds:0.#}"));
            _nextMobInfoAt = 0;
        }
        ImGui.SetNextItemWidth(160 * _imgui.UiScale);
        ImGui.SliderFloat("Delay##respawn", ref _respawnSeconds, 0f, 120f, "%.0f s");
        _respawnSecondsEdited = ImGui.IsItemActive();
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            OnConsoleSubmitted(Inv($"autorespawn {(on ? "on" : "off")} {_respawnSeconds:0.#}"));
            _nextMobInfoAt = 0;
        }
    }

    // D346 (the owner, 2026-11-21): take over any body, an NPC, a creature or a robot (tg's Assume Direct Control): the way back to the own
    // body, the chosen player's mob, and the mobs round you from the mobinfo answer (nearest first); the possess tool clicks one on the map.
    private void DrawPossess()
    {
        string back = _mobInfo.PossessReturn;
        if (!ImGui.CollapsingHeader(Inv($"Possess ({_mobInfo.Nearby.Count} mobs near you)##possess"), back != null ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None))
            return;
        if (back != null)
        {
            if (ImGui.Button(back == "ghost" ? "Leave this body (as a ghost)" : "Return to my body (" + back + ")")) PossessCommand("unpossess");
            ImGui.SameLine();
        }
        if (ImGui.Button(_devTool == DevTool.Possess ? "Possess tool: click a mob (right click: off)" : "Click a mob to possess it"))
            _devTool = _devTool == DevTool.Possess ? DevTool.None : DevTool.Possess;
        if (_mobTarget != "me")
        {
            ImGui.SameLine();
            if (ImGui.Button("Take over " + PlayerLabel(_mobTarget))) PossessCommand("possess " + _mobTarget + " force");
            if (ImGui.IsItemHovered()) ImGui.SetItemTooltip("You play the chosen player's mob; that player becomes a ghost (tg's Assume Direct Control)");
        }
        if (_mobInfo.Nearby.Count == 0)
        {
            ImGui.TextDisabled(Inv($"No mob within {NearbyTiles} tiles on your level."));
            return;
        }
        if (!ImGui.BeginTable("##nearby", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.ScrollY, new System.Numerics.Vector2(0, 180 * _imgui.UiScale)))
            return;
        foreach (MobInfoNearby m in _mobInfo.Nearby)
        {
            string id = "#" + m.Id.ToString(CultureInfo.InvariantCulture);
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.TextUnformatted(m.Name + (m.Dead ? " (dead)" : ""));
            if (ImGui.IsItemHovered()) ImGui.SetItemTooltip(m.TypePath + "   " + id);
            ImGui.TableSetColumnIndex(1);
            if (m.Player.Length > 0) ImGui.TextColored(new System.Numerics.Vector4(0.5f, 1f, 0.5f, 1f), m.Player);
            else ImGui.TextDisabled("NPC");
            ImGui.TableSetColumnIndex(2);
            ImGui.TextDisabled(Inv($"{m.Distance} t"));
            ImGui.TableSetColumnIndex(3);
            if (ImGui.SmallButton((m.Player.Length > 0 ? "Take over" : "Possess") + "##possess" + id)) PossessCommand(m.PossessCommand);
        }
        ImGui.EndTable();
    }

    /// <summary>The server's nearby range (ServerHost.NearbyMobRange), for the empty list's line.</summary>
    private const int NearbyTiles = 12;

    private void PossessCommand(string line)
    {
        OnConsoleSubmitted(line);
        SetMobTarget("me"); // the panel follows you into the new body
        _nextMobInfoAt = 0;
    }

    private void Switch(string label, string verb, string who)
    {
        bool on = _mobInfo.IsOn(verb);
        if (ImGui.Checkbox(label, ref on))
        {
            OnConsoleSubmitted(verb + " " + who);
            _nextMobInfoAt = 0; // show the new state at once
        }
    }

    private void SetMobTarget(string target)
    {
        if (_mobTarget == target) return;
        _mobTarget = target;
        _mobInfo = null;
        _nextMobInfoAt = 0;
    }

    private string PlayerLabel(string target)
    {
        if (_mobInfo != null)
        {
            foreach (MobInfoPlayer p in _mobInfo.Players)
            {
                if ("#" + p.Id.ToString(CultureInfo.InvariantCulture) == target) return p.Key + "   " + target;
            }
        }
        return target;
    }
}
