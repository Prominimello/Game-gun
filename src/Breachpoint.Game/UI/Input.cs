using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace Breachpoint.UI;

public struct TouchPt
{
    public int Id;
    public Vector2 Pos;
    public TouchLocationState State;
}

/// <summary>
/// Per-frame input snapshot. Mouse and a single primary touch are merged into one "pointer"
/// so UI code works the same on desktop and mobile. Raw touches are exposed for gestures.
/// </summary>
public sealed class Input
{
    private MouseState _m, _pm;
    private KeyboardState _k, _pk;
    public Vector2 Pointer;
    public bool Down, Pressed, Released, RightPressed, RightReleased, RightDown, MiddleDown;
    public float Wheel;
    public bool TouchActive;          // true if the last pointer activity came from touch
    public readonly List<TouchPt> Touches = new();
    public readonly List<char> TextChars = new();
    public Vector2 PressPos;
    public double PressTime;
    public bool Consumed;             // set by UI when the pointer was used by a widget this frame
    private int _primaryTouch = -1;
    public double Time;

    public void Update(GameTime gt, bool active)
    {
        Time = gt.TotalGameTime.TotalSeconds;
        _pm = _m; _pk = _k;
        _m = active ? Mouse.GetState() : _pm;
        _k = active ? Keyboard.GetState() : default;
        Pressed = Released = RightPressed = RightReleased = false;
        Wheel = 0;
        Consumed = false;
        Touches.Clear();
        TouchCollection tc;
        try { tc = TouchPanel.GetState(); } catch { tc = default; }
        foreach (var t in tc) Touches.Add(new TouchPt { Id = t.Id, Pos = t.Position, State = t.State });

        if (Touches.Count > 0)
        {
            TouchActive = true;
            var primary = Touches.FirstOrDefault(t => t.Id == _primaryTouch);
            if (primary.Id != _primaryTouch || _primaryTouch < 0)
            {
                var p = Touches.FirstOrDefault(t => t.State == TouchLocationState.Pressed);
                if (p.State == TouchLocationState.Pressed) { _primaryTouch = p.Id; primary = p; }
            }
            if (_primaryTouch >= 0 && primary.Id == _primaryTouch)
            {
                Pointer = primary.Pos;
                if (primary.State == TouchLocationState.Pressed) { Pressed = true; Down = true; PressPos = Pointer; PressTime = Time; }
                else if (primary.State == TouchLocationState.Moved) Down = true;
                else if (primary.State == TouchLocationState.Released) { Released = true; Down = false; _primaryTouch = -1; }
            }
            return;
        }
        if (_primaryTouch >= 0) { Released = Down; Down = false; _primaryTouch = -1; return; }

        var mp = new Vector2(_m.X, _m.Y);
        if (mp != new Vector2(_pm.X, _pm.Y) || _m.LeftButton == ButtonState.Pressed) TouchActive = false;
        if (TouchActive) return;
        Pointer = mp;
        Down = _m.LeftButton == ButtonState.Pressed;
        Pressed = Down && _pm.LeftButton == ButtonState.Released;
        Released = !Down && _pm.LeftButton == ButtonState.Pressed;
        RightDown = _m.RightButton == ButtonState.Pressed;
        RightPressed = RightDown && _pm.RightButton == ButtonState.Released;
        RightReleased = !RightDown && _pm.RightButton == ButtonState.Pressed;
        MiddleDown = _m.MiddleButton == ButtonState.Pressed;
        Wheel = (_m.ScrollWheelValue - _pm.ScrollWheelValue) / 120f;
        if (Pressed) { PressPos = Pointer; PressTime = Time; }
    }

    public bool Key(Keys k) => _k.IsKeyDown(k);
    public bool KeyPressed(Keys k) => _k.IsKeyDown(k) && !_pk.IsKeyDown(k);
    public bool Shift => Key(Keys.LeftShift) || Key(Keys.RightShift);
    public bool Ctrl => Key(Keys.LeftControl) || Key(Keys.RightControl);
    public bool Alt => Key(Keys.LeftAlt) || Key(Keys.RightAlt);
}
