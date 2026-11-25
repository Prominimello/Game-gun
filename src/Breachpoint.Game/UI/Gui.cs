using Breachpoint.Gfx;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Breachpoint.UI;

public enum BtnStyle { Normal, Primary, Danger, Ghost, On, Tab, TabOn, Menu }

/// <summary>
/// Immediate-mode UI. Layout is in "UI units" (the screen is treated as roughly 720 units tall);
/// everything is multiplied by <see cref="S"/> when drawn so it is crisp and finger-sized on phones.
/// </summary>
public sealed class Gui
{
    public readonly Painter P;
    public readonly Input In;
    public readonly IPlatform Platform;
    public float S = 1;
    public float W, H;              // screen size in UI units
    public bool Enabled = true;     // false while a modal is open (for the screen beneath)
    public Action Click;            // sound hook
    private readonly Dictionary<string, float> _scroll = new();
    private readonly Stack<RectangleF> _clipStack = new();
    private string _scrollDrag;
    private float _scrollDragStart, _scrollDragOffset;
    private bool _dragging;
    public string FocusId;
    public string FocusText;
    private Action<string> _focusCommit;
    private float _caretT;

    public Gui(Painter p, Input input, IPlatform platform) { P = p; In = input; Platform = platform; }

    public void BeginFrame(int pxW, int pxH)
    {
        float baseH = Platform.IsMobile ? 560 : 760;
        S = Math.Max(0.75f, Math.Min(pxH / baseH, pxW / 1100f));
        W = pxW / S; H = pxH / S;
        _caretT += 0.016f;
        if (In.Pressed) _dragging = false;
        if (In.Down && !_dragging && Vector2.Distance(In.Pointer, In.PressPos) > 10 * S) _dragging = true;
        HandleFocusKeys();
    }

    public Vector2 Mouse => In.Pointer / S;
    public bool Hover(RectangleF r) => Enabled && !In.TouchActive && r.Contains(Mouse) && ClipContains(Mouse);
    private bool ClipContains(Vector2 m) => _clipStack.Count == 0 || _clipStack.Peek().Contains(m);

    /// <summary>True when the pointer was pressed and released inside r without dragging.</summary>
    public bool Clicked(RectangleF r)
    {
        if (!Enabled || !In.Released || _dragging || In.Consumed) return false;
        var m = Mouse; var pp = In.PressPos / S;
        if (r.Contains(m) && r.Contains(pp) && ClipContains(m)) { In.Consumed = true; return true; }
        return false;
    }

    public bool PressedIn(RectangleF r) => Enabled && In.Pressed && r.Contains(Mouse) && ClipContains(Mouse);
    public bool Dragging => _dragging;

    // ---------------- drawing helpers (UI units) ----------------
    public void Fill(RectangleF r, Color c) => P.FillRect(r.X * S, r.Y * S, r.Width * S, r.Height * S, c);
    public void Border(RectangleF r, Color c, float t = 1) => P.Rect(r.X * S, r.Y * S, r.Width * S, r.Height * S, c, Math.Max(1, t * S));
    public void Panel(RectangleF r, Color? fill = null, Color? border = null, Color? top = null)
    {
        Fill(r, fill ?? Colors.Panel);
        Border(r, border ?? Colors.Line);
        if (top.HasValue) Fill(new RectangleF(r.X, r.Y, r.Width, 3), top.Value);
    }
    public void Label(string s, float x, float y, float size = 13, Color? c = null, FontKind k = FontKind.Regular, Align a = Align.Left) =>
        P.Text(s, x * S, y * S, size * S, c ?? Colors.Text, k, a);
    public float Wrap(string s, float x, float y, float w, float size = 13, Color? c = null, FontKind k = FontKind.Regular, bool draw = true) =>
        P.TextWrap(s, x * S, y * S, w * S, size * S, c ?? Colors.Text, k, draw) / S;
    public float TextW(string s, float size = 13, FontKind k = FontKind.Regular) => P.Measure(s, size * S, k).X / S;
    public void Line(float x0, float y0, float x1, float y1, Color c, float t = 1) => P.Line(x0 * S, y0 * S, x1 * S, y1 * S, Math.Max(1, t * S), c);
    public void Bar(RectangleF r, float frac, Color c, Color? bg = null)
    {
        Fill(r, bg ?? Colors.Hex("#0a0e12"));
        Fill(new RectangleF(r.X, r.Y, r.Width * Math.Clamp(frac, 0, 1), r.Height), c);
        Border(r, Colors.Hex("#2a3542"));
    }
    public void Header(string s, float x, float y, float size = 15) => Label(s.ToUpperInvariant(), x, y, size, Colors.Accent, FontKind.Bold);

    // ---------------- widgets ----------------
    public bool Button(RectangleF r, string label, BtnStyle style = BtnStyle.Normal, bool enabled = true, float size = 13)
    {
        bool hot = enabled && Hover(r);
        bool pressed = enabled && In.Down && r.Contains(Mouse) && r.Contains(In.PressPos / S) && Enabled && !_dragging;
        Color bg, border, fg;
        switch (style)
        {
            case BtnStyle.Primary: bg = hot ? Colors.Hex("#f0b54f") : Colors.Accent; border = Colors.Accent; fg = Colors.Hex("#14110a"); break;
            case BtnStyle.Danger: bg = hot ? Colors.Hex("#7a2a2a") : Colors.Hex("#5a1f1f"); border = Colors.Hex("#8a3030"); fg = Colors.Text; break;
            case BtnStyle.Ghost: bg = hot ? Colors.Hex("#1f2a36") : Color.Transparent; border = Colors.ButtonBorder; fg = Colors.Text; break;
            case BtnStyle.On: bg = hot ? Colors.ButtonHot : Colors.Button; border = Colors.Accent; fg = Colors.Accent; break;
            case BtnStyle.Tab: bg = hot ? Colors.Hex("#16202a") : Color.Transparent; border = Color.Transparent; fg = Colors.Text; break;
            case BtnStyle.TabOn: bg = Colors.Hex("#16202a"); border = Color.Transparent; fg = Colors.Accent; break;
            case BtnStyle.Menu: bg = hot ? Colors.Hex("#1d2833") : Colors.Hex("#141c24") * 0.9f; border = hot ? Colors.Accent : Colors.ButtonBorder; fg = Colors.Text; break;
            default: bg = hot ? Colors.ButtonHot : Colors.Button; border = hot ? Colors.Accent : Colors.ButtonBorder; fg = Colors.Text; break;
        }
        if (!enabled) { bg *= 0.45f; fg *= 0.45f; border *= 0.45f; }
        if (pressed) bg = Colors.Shade(bg, 0.12f);
        Fill(r, bg);
        if (border.A > 0) Border(r, border);
        if (style == BtnStyle.TabOn) Fill(new RectangleF(r.X, r.Bottom - 3, r.Width, 3), Colors.Accent);
        var kind = style is BtnStyle.Primary or BtnStyle.Menu or BtnStyle.TabOn ? FontKind.Bold : FontKind.Regular;
        if (style == BtnStyle.Menu) Label(label, r.X + 16, r.Y + r.Height / 2 - size * 0.62f, size, fg, kind);
        else Label(label, r.X + r.Width / 2, r.Y + r.Height / 2 - size * 0.62f, size, fg, kind, Align.Center);
        bool c = enabled && Clicked(r);
        if (c) Click?.Invoke();
        return c;
    }

    /// <summary>A "◀ value ▶" selector. Returns -1, 0 or +1.</summary>
    public int Cycler(RectangleF r, string value, bool enabled = true, float size = 12)
    {
        float bw = Math.Min(30, r.Height + 4);
        int d = 0;
        if (Button(new RectangleF(r.X, r.Y, bw, r.Height), "◀", BtnStyle.Normal, enabled, size)) d = -1;
        Fill(new RectangleF(r.X + bw, r.Y, r.Width - bw * 2, r.Height), Colors.Hex("#0e141a"));
        Border(new RectangleF(r.X + bw, r.Y, r.Width - bw * 2, r.Height), Colors.ButtonBorder);
        string v = value ?? "";
        while (v.Length > 3 && TextW(v, size) > r.Width - bw * 2 - 8) v = v[..^2];
        if (v != (value ?? "")) v += "…";
        Label(v, r.X + r.Width / 2, r.Y + r.Height / 2 - size * 0.62f, size, enabled ? Colors.Text : Colors.Dim, FontKind.Regular, Align.Center);
        if (Button(new RectangleF(r.Right - bw, r.Y, bw, r.Height), "▶", BtnStyle.Normal, enabled, size)) d = 1;
        return d;
    }

    public static int Wrap(int i, int n) => n == 0 ? 0 : ((i % n) + n) % n;

    public bool Toggle(RectangleF r, string label, bool value, float size = 12)
    {
        var box = new RectangleF(r.X, r.Y + (r.Height - 18) / 2, 18, 18);
        Fill(box, Colors.Hex("#0e141a")); Border(box, value ? Colors.Accent : Colors.ButtonBorder);
        if (value) Fill(new RectangleF(box.X + 4, box.Y + 4, 10, 10), Colors.Accent);
        Label(label, r.X + 26, r.Y + r.Height / 2 - size * 0.62f, size);
        if (Clicked(r)) { Click?.Invoke(); return !value; }
        return value;
    }

    public float Slider(RectangleF r, float v)
    {
        var track = new RectangleF(r.X, r.Y + r.Height / 2 - 3, r.Width, 6);
        Fill(track, Colors.Hex("#0a0e12")); Border(track, Colors.Line);
        Fill(new RectangleF(track.X, track.Y, track.Width * v, track.Height), Colors.Accent);
        float kx = r.X + r.Width * v;
        P.Circle(kx * S, (r.Y + r.Height / 2) * S, 8 * S, Colors.Accent);
        if (Enabled && In.Down && r.Contains(In.PressPos / S))
        {
            _dragging = false;
            v = Math.Clamp((Mouse.X - r.X) / r.Width, 0, 1);
        }
        return v;
    }

    public int Tabs(RectangleF r, string[] names, int sel, float size = 12)
    {
        float x = r.X;
        for (int i = 0; i < names.Length; i++)
        {
            float w = TextW(names[i], size, FontKind.Bold) + 26;
            if (Button(new RectangleF(x, r.Y, w, r.Height), names[i], i == sel ? BtnStyle.On : BtnStyle.Normal, true, size)) sel = i;
            x += w + 4;
        }
        return sel;
    }

    public int Swatches(RectangleF r, string[] colors, int sel)
    {
        float s = r.Height;
        for (int i = 0; i < colors.Length; i++)
        {
            var b = new RectangleF(r.X + i * (s + 4), r.Y, s, s);
            Fill(b, Colors.Hex(colors[i]));
            Border(b, i == sel ? Colors.Accent : Colors.Hex("#222222"), i == sel ? 2 : 1);
            if (Clicked(b)) { Click?.Invoke(); sel = i; }
        }
        return sel;
    }

    /// <summary>Single-line text field. Desktop edits inline; mobile opens the on-screen keyboard.</summary>
    public void TextField(string id, RectangleF r, string value, Action<string> commit, float size = 13, int maxLen = 30, string title = "Edit")
    {
        bool focused = FocusId == id;
        Fill(r, Colors.Hex("#0e141a"));
        Border(r, focused ? Colors.Accent : Colors.ButtonBorder);
        string shown = focused ? FocusText : value;
        Label(shown + (focused && (int)(_caretT * 2) % 2 == 0 ? "|" : ""), r.X + 6, r.Y + r.Height / 2 - size * 0.62f, size);
        if (Clicked(r))
        {
            if (Platform.IsMobile)
            {
                var t = Platform.PromptText(title, value);
                t.ContinueWith(task => { if (task.Result != null) _pendingCommits.Enqueue(() => commit(task.Result.Length > maxLen ? task.Result[..maxLen] : task.Result)); });
            }
            else { FocusId = id; FocusText = value ?? ""; _focusCommit = v => commit(v.Length > maxLen ? v[..maxLen] : v); _maxLen = maxLen; }
        }
        else if (focused && In.Pressed && !r.Contains(Mouse)) CommitFocus();
    }
    private int _maxLen = 30;
    private readonly Queue<Action> _pendingCommits = new();

    private void HandleFocusKeys()
    {
        while (_pendingCommits.Count > 0) _pendingCommits.Dequeue()();
        if (FocusId == null) { In.TextChars.Clear(); return; }
        foreach (var ch in In.TextChars)
        {
            if (ch == '\b') { if (FocusText.Length > 0) FocusText = FocusText[..^1]; }
            else if (ch == '\r' || ch == '\n') { CommitFocus(); break; }
            else if (!char.IsControl(ch) && FocusText.Length < _maxLen) FocusText += ch;
        }
        In.TextChars.Clear();
        if (In.KeyPressed(Keys.Escape)) { FocusId = null; }
        if (In.KeyPressed(Keys.Enter)) CommitFocus();
    }

    public void CommitFocus()
    {
        if (FocusId == null) return;
        var c = _focusCommit; var t = FocusText;
        FocusId = null; _focusCommit = null;
        c?.Invoke(t);
    }

    // ---------------- scrolling ----------------
    /// <summary>Begin a scrollable region. Returns the y offset to add to content positions. Call EndScroll afterwards.</summary>
    public float BeginScroll(string id, RectangleF r, float contentH)
    {
        float max = Math.Max(0, contentH - r.Height);
        float off = Math.Clamp(_scroll.GetValueOrDefault(id), 0, max);
        if (Enabled)
        {
            if (r.Contains(Mouse) && In.Wheel != 0) off = Math.Clamp(off - In.Wheel * 60, 0, max);
            if (In.Pressed && r.Contains(Mouse)) { _scrollDrag = id; _scrollDragStart = Mouse.Y; _scrollDragOffset = off; }
            if (_scrollDrag == id && In.Down && _dragging) off = Math.Clamp(_scrollDragOffset - (Mouse.Y - _scrollDragStart), 0, max);
            if (!In.Down && _scrollDrag == id) _scrollDrag = null;
        }
        _scroll[id] = off;
        var clip = _clipStack.Count > 0 ? RectangleF.Intersect(_clipStack.Peek(), r) : r;
        _clipStack.Push(clip);
        P.SetClip(new Rectangle((int)(clip.X * S), (int)(clip.Y * S), (int)Math.Ceiling(clip.Width * S), (int)Math.Ceiling(clip.Height * S)));
        if (max > 0)
        {
            float th = r.Height * r.Height / contentH;
            Fill(new RectangleF(r.Right - 4, r.Y + (r.Height - th) * off / max, 3, th), Colors.Accent * 0.5f);
        }
        return -off;
    }

    public void EndScroll()
    {
        _clipStack.Pop();
        if (_clipStack.Count > 0)
        {
            var c = _clipStack.Peek();
            P.SetClip(new Rectangle((int)(c.X * S), (int)(c.Y * S), (int)Math.Ceiling(c.Width * S), (int)Math.Ceiling(c.Height * S)));
        }
        else P.SetClip(null);
    }

    public void ResetScroll(string id) => _scroll.Remove(id);
}

/// <summary>Float rectangle (MonoGame only ships an int one).</summary>
public struct RectangleF
{
    public float X, Y, Width, Height;
    public RectangleF(float x, float y, float w, float h) { X = x; Y = y; Width = w; Height = h; }
    public float Right => X + Width;
    public float Bottom => Y + Height;
    public bool Contains(Vector2 p) => p.X >= X && p.Y >= Y && p.X < X + Width && p.Y < Y + Height;
    public RectangleF Inflate(float d) => new(X - d, Y - d, Width + 2 * d, Height + 2 * d);
    public static RectangleF Intersect(RectangleF a, RectangleF b)
    {
        float x0 = Math.Max(a.X, b.X), y0 = Math.Max(a.Y, b.Y), x1 = Math.Min(a.Right, b.Right), y1 = Math.Min(a.Bottom, b.Bottom);
        return new RectangleF(x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
    }
}
