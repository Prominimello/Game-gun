using Breachpoint.Core.Data;
using Breachpoint.Core.State;
using Breachpoint.Gfx;
using Breachpoint.UI;
using C = Breachpoint.Gfx.Colors;

namespace Breachpoint.Screens;

/// <summary>Shared modal dialogs: settings, how-to-play, new campaign, confirmations.</summary>
public static class Dialogs
{
    public static RectangleF Box(Gui ui, float w, float h, string title)
    {
        w = Math.Min(w, ui.W - 20); h = Math.Min(h, ui.H - 20);
        var r = new RectangleF(ui.W / 2 - w / 2, ui.H / 2 - h / 2, w, h);
        ui.Panel(r, C.Panel, C.Hex("#3a4856"), C.Accent);
        ui.Header(title, r.X + 18, r.Y + 16, 17);
        return r;
    }

    public static void Settings(BreachpointGame g)
    {
        g.Modal(() =>
        {
            var ui = g.Gui; var st = g.Settings;
            var r = Box(ui, 520, 400, "Settings");
            float y = r.Y + 56;
            void Slide(string label, Func<float> get, Action<float> set)
            {
                ui.Label(label, r.X + 20, y + 4, 13);
                float v = ui.Slider(new RectangleF(r.X + 170, y, r.Width - 200, 24), get());
                if (Math.Abs(v - get()) > 0.001f) { set(v); g.Audio.ApplyVolumes(); }
                y += 40;
            }
            Slide("Master volume", () => st.Master, v => st.Master = v);
            Slide("Effects", () => st.Sfx, v => st.Sfx = v);
            Slide("Ambience", () => st.Ambient, v => st.Ambient = v);
            Slide("Music", () => st.Music, v => st.Music = v);
            st.Voice = ui.Toggle(new RectangleF(r.X + 20, y, 300, 28), "Voice callouts (text-to-speech)", st.Voice);
            y += 50;
            float bx = r.X + 20;
            if (ui.Button(new RectangleF(bx, r.Bottom - 54, 120, 36), "Test sound")) g.Audio.Play("flashbang");
            if (ui.Button(new RectangleF(bx + 130, r.Bottom - 54, 110, 36), "Test voice")) g.Audio.Voice("Radio check. Police! Show me your hands!", 1, true);
            if (g.Profile != null && ui.Button(new RectangleF(bx + 250, r.Bottom - 54, 100, 36), "Save now")) { g.SaveProfile(); g.Toast("Game saved."); }
            if (ui.Button(new RectangleF(r.Right - 110, r.Bottom - 54, 90, 36), "Close", BtnStyle.Primary)) { g.SaveSettings(); return false; }
            return true;
        });
    }

    public static void HowTo(BreachpointGame g)
    {
        g.Modal(() =>
        {
            var ui = g.Gui;
            var r = Box(ui, 780, 600, "How to Play");
            string text =
                "THE LOOP. From HQ pick an operation in the Command Center, choose your squad (vehicle seats limit squad size), tune loadouts and entry points, then deploy.\n\n" +
                "PLANNING. Missions start frozen. Order moves, queue waypoints, set up door breaches (open, kick, flashbang & clear, breaching charge, shotgun, ram, pick lock, door camera) and \"wait for GO\" points, then EXECUTE. Pause any time to re-plan.\n\n" +
                "CONTROLS (PC). Left-click select / drag-box. Right-click: move (Shift = add waypoint), or act on a door, suspect, civilian, item or downed teammate. Space pause, Enter GO/Execute, 1-8 select operator, WASD pan, wheel zoom, Q/E rotate, Tab floor, G/T/Y/U grenades, H hold, F follow, R regroup, V cover, C crouch, Z pace, B wait-for-GO, X cancel, F1 help.\n\n" +
                "CONTROLS (TOUCH). Tap an operator to select (Multi to add, All for squad). Tap ground, a door, a suspect or an item to order. Long-press or toggle +Waypoint to queue. Drag to pan, pinch to zoom, twist to rotate. Orders opens the command panel.\n\n" +
                "RULES OF ENGAGEMENT. Operators shout commands. Many suspects surrender, especially when flashed, gassed, hurt or outnumbered. Arrest them for a much better rating. Shooting a surrendering suspect is an ROE violation.\n\n" +
                "SUSPECTS hear gunfire, doors and running, use radios, investigate, hide, ambush and flee. Hostage guards may execute hostages when alerted (⚠ timer). Move quietly and use suppressors to keep surprise.\n\n" +
                "CONSEQUENCES. Injuries take days to heal, fatigue builds, weapons wear. On Veteran, Milsim and Ironman operators can die permanently. Evidence builds intel on each organization.";
            float off = ui.BeginScroll("howto", new RectangleF(r.X + 18, r.Y + 50, r.Width - 36, r.Height - 110), ui.Wrap(text, 0, 0, r.Width - 50, 13, draw: false) + 10);
            ui.Wrap(text, r.X + 20, r.Y + 52 + off, r.Width - 50, 13);
            ui.EndScroll();
            return !ui.Button(new RectangleF(r.Right - 120, r.Bottom - 50, 100, 36), "Got it", BtnStyle.Primary);
        });
    }

    public static void NewCampaign(BreachpointGame g)
    {
        string diff = "standard", name = "Task Force Breachpoint";
        bool iron = false;
        g.Modal(() =>
        {
            var ui = g.Gui;
            var r = Box(ui, 620, 560, "New Campaign");
            float y = r.Y + 56;
            ui.Label("Unit name", r.X + 20, y + 6, 13, C.Dim);
            ui.TextField("unitname", new RectangleF(r.X + 130, y, 300, 30), name, v => name = string.IsNullOrWhiteSpace(v) ? name : v, 13, 30, "Unit name");
            y += 46;
            ui.Label("Difficulty", r.X + 20, y, 13, C.Dim); y += 22;
            foreach (var id in CampaignData.DifficultyOrder)
            {
                var d = CampaignData.Difficulty[id];
                var cr = new RectangleF(r.X + 20, y, r.Width - 40, 52);
                ui.Fill(cr, diff == id ? C.Hex("#1d2833") : C.Panel2);
                ui.Border(cr, diff == id ? C.Accent : C.Line);
                ui.Label(d.Name, cr.X + 12, cr.Y + 8, 14, diff == id ? C.Accent : C.Text, FontKind.Bold);
                ui.Label(d.Desc, cr.X + 12, cr.Y + 29, 11, C.Dim);
                if (ui.Clicked(cr)) { diff = id; g.Audio.Ui(); }
                y += 58;
            }
            iron = ui.Toggle(new RectangleF(r.X + 20, y + 4, r.Width - 40, 28), "IRONMAN — one life per operator, permanent death on any difficulty", iron);
            if (g.HasSave) ui.Label("Warning: this overwrites your current save.", r.X + 20, r.Bottom - 86, 12, C.Red);
            if (ui.Button(new RectangleF(r.X + 20, r.Bottom - 54, 140, 38), "Begin", BtnStyle.Primary, true, 14))
            {
                ui.CommitFocus();
                g.Profile = ProfileService.NewGame(diff, iron, name);
                g.SaveProfile();
                g.Switch(new HqScreen(g));
                g.Toast("Welcome, Commander. Start with \"Corner Stash\" in the Command Center.");
                return false;
            }
            return !ui.Button(new RectangleF(r.X + 170, r.Bottom - 54, 110, 38), "Cancel");
        });
    }

    public static void Confirm(BreachpointGame g, string title, string message, string yes, Action onYes, bool danger = true)
    {
        g.Modal(() =>
        {
            var ui = g.Gui;
            var r = Box(ui, 480, 220, title);
            ui.Wrap(message, r.X + 20, r.Y + 56, r.Width - 40, 13);
            if (ui.Button(new RectangleF(r.X + 20, r.Bottom - 54, 170, 38), yes, danger ? BtnStyle.Danger : BtnStyle.Primary)) { onYes(); return false; }
            return !ui.Button(new RectangleF(r.X + 200, r.Bottom - 54, 110, 38), "Cancel");
        });
    }
}
