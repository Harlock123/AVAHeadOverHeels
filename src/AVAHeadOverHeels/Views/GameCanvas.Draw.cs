using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AVAHeadOverHeels.Engine;

namespace AVAHeadOverHeels.Views;

public sealed partial class GameCanvas
{
    private static readonly Dictionary<uint, IBrush> BrushCache = [];
    private static readonly Dictionary<(uint, double), Pen> PenCache = [];
    private static readonly Typeface Retro = new(new FontFamily("avares://AVAHeadOverHeels/Assets/Fonts#VT323"));

    public static IBrush Brush(uint argb)
    {
        if (!BrushCache.TryGetValue(argb, out var b))
            BrushCache[argb] = b = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
        return b;
    }

    private static Pen Pen(uint argb, double width = 1)
    {
        if (!PenCache.TryGetValue((argb, width), out var p))
            PenCache[(argb, width)] = p = new Pen(Brush(argb), width, lineCap: PenLineCap.Round);
        return p;
    }

    private const uint White = 0xFFF2F2FF, Yellow = 0xFFFFE14A, Red = 0xFFFF4A3A, Dim = 0xFF8A9AC8, Cyan = 0xFF6AE8FF, Orange = 0xFFFF9A30;

    private static void Text(DrawingContext dc, string text, Point at, double size, uint colour, bool center = false, bool right = false, bool shadow = true)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, Brush(colour));
        double x = center ? at.X - ft.Width / 2 : right ? at.X - ft.Width : at.X;
        if (shadow) dc.DrawText(new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, Brush(0xC0000000)), new Point(x + 2, at.Y - ft.Height / 2 + 2));
        dc.DrawText(ft, new Point(x, at.Y - ft.Height / 2));
    }

    private static void Poly(DrawingContext dc, uint fill, uint? edge, double width, params Point[] pts)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(pts[0], true);
            foreach (var p in pts.Skip(1)) c.LineTo(p);
            c.EndFigure(true);
        }
        dc.DrawGeometry(fill == 0 ? null : Brush(fill), edge is { } e ? Pen(e, width) : null, g);
    }

    private static uint Hash(int x, int y, int salt)
    {
        uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(salt * 83492791);
        h ^= h >> 13;
        h *= 0x5bd1e995;
        h ^= h >> 15;
        return h;
    }

    private static uint Alpha(uint colour, double a) => (uint)(Math.Clamp(a, 0, 1) * 255) << 24 | (colour & 0x00FFFFFF);


    private bool InGame => Engine.State is GameState.Attract or GameState.Playing or GameState.Paused;

    private const double TW = 38, TH = 19, BH = 38, OX = 512, OY = 120, ViewH = 560;

    private static readonly (uint Top, uint Left, uint Right, uint Floor1, uint Floor2, uint Wall)[] RoomStyles =
    [
        (0xFFB8B0A0, 0xFF8A8478, 0xFF6A6458, 0xFF5A5448, 0xFF4E4840, 0xFF3A4A6A),
        (0xFFA0B8B0, 0xFF6E8A82, 0xFF52685F, 0xFF48564F, 0xFF3E4A44, 0xFF5A3A4A),
        (0xFFC0A888, 0xFF967E60, 0xFF705C44, 0xFF5E4C3A, 0xFF524232, 0xFF2E4A3A),
        (0xFFA8A8C8, 0xFF7C7C9C, 0xFF5C5C78, 0xFF4C4C64, 0xFF424258, 0xFF5A4A2A),
    ];

    private static Point Iso(double x, double y, double z) => new(OX + (x - y) * TW, OY + (x + y) * TH - z * BH);

    private void DrawScreen(DrawingContext dc)
    {
        var g = Engine;
        dc.FillRectangle(Brush(0xFF000000), new Rect(0, 0, Field.Width, Field.Height));
        switch (g.State)
        {
            case GameState.Title: DrawTitle(dc); break;
            case GameState.HighScores: DrawHighScores(dc); break;
            case GameState.EnterInitials: DrawInitials(dc); break;
            case GameState.GameOver: DrawGameOver(dc); break;
            default:
                DrawRoom(dc, g.World);
                DrawHud(dc, g.World);
                if (g.Message is { } msg)
                {
                    double wdt = Measure(msg, 40) + 40;
                    dc.FillRectangle(Brush(0xC0000000), new Rect(512 - wdt / 2, 30, wdt, 56), 6);
                    Text(dc, msg, new Point(512, 58), 40, g.Phase == Phase.Dying ? Red : Yellow, center: true);
                }
                break;
        }
        if (g.State == GameState.Paused)
        {
            dc.FillRectangle(Brush(0x90000000), new Rect(0, 0, Field.Width, Field.Height));
            Text(dc, "PAUSED", new Point(512, 300), 80, Yellow, center: true);
            Text(dc, "ANY KEY TO CARRY ON - ESC TO QUIT", new Point(512, 360), 26, White, center: true);
        }
        if (g.State == GameState.Attract)
            Text(dc, "DEMONSTRATION - PRESS ENTER TO PLAY", new Point(512, ViewH - 14), 22, Yellow, center: true);
    }

    private static double Measure(string text, double size) =>
        new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, Brush(White)).Width;

    // ───────────────────────────── the room ─────────────────────────────

    private void DrawRoom(DrawingContext dc, World w)
    {
        var r = w.Here;
        var st = RoomStyles[r.Style % RoomStyles.Length];
        const int n = Field.N;
        // The two back walls, with arches where the doors are.
        Poly(dc, st.Wall, null, 0, Iso(0, 0, 0), Iso(n, 0, 0), Iso(n, 0, 3.2), Iso(0, 0, 3.2));
        Poly(dc, Shade(st.Wall, 0.8), null, 0, Iso(0, 0, 0), Iso(0, n, 0), Iso(0, n, 3.2), Iso(0, 0, 3.2));
        for (int i = 0; i < n; i++)
        {
            // Stone courses.
            dc.DrawLine(Pen(Shade(st.Wall, 0.7), 1), Iso(i, 0, 0), Iso(i, 0, 3.2));
            dc.DrawLine(Pen(Shade(st.Wall, 0.6), 1), Iso(0, i, 0), Iso(0, i, 3.2));
        }
        for (double z = 0.8; z < 3.2; z += 0.8)
        {
            dc.DrawLine(Pen(Shade(st.Wall, 0.7), 1), Iso(0, 0, z), Iso(n, 0, z));
            dc.DrawLine(Pen(Shade(st.Wall, 0.6), 1), Iso(0, 0, z), Iso(0, n, z));
        }
        if (r.Doors[(int)Side.North]) Arch(dc, Iso(Field.Mid, 0, 0), Iso(Field.Mid + 1, 0, 0), st);
        if (r.Doors[(int)Side.West]) Arch(dc, Iso(0, Field.Mid, 0), Iso(0, Field.Mid + 1, 0), st);
        // The floor.
        for (int x = 0; x < n; x++)
        for (int y = 0; y < n; y++)
        {
            var f = r.Floor[x, y];
            if (f == Floor.Pit && r.Block[x, y] == 0) continue;
            Poly(dc, (x + y) % 2 == 0 ? st.Floor1 : st.Floor2, Shade(st.Floor2, 0.8), 1, Iso(x, y, 0), Iso(x + 1, y, 0), Iso(x + 1, y + 1, 0), Iso(x, y + 1, 0));
            if (f == Floor.Spikes && r.Block[x, y] == 0)
                for (int k = 0; k < 4; k++)
                {
                    double sx = x + 0.25 + (k % 2) * 0.5, sy = y + 0.25 + (k / 2) * 0.5;
                    Poly(dc, 0xFFD0D0D8, 0xFF707078, 1, Iso(sx - 0.12, sy, 0), Iso(sx + 0.12, sy, 0), Iso(sx, sy, 0.5));
                }
        }
        // Doorways through the near walls: just the stones of the frame.
        foreach (var s in new[] { Side.East, Side.South })
            if (r.Doors[(int)s])
            {
                var (dx, dy) = RoomDef.DoorCell(s);
                var (ox, oy) = RoomDef.Out(s);
                double ex = s == Side.East ? n : dx, ey = s == Side.South ? n : dy;
                var a = s == Side.East ? Iso(ex, ey, 0) : Iso(ex, ey, 0);
                var b = s == Side.East ? Iso(ex, ey + 1, 0) : Iso(ex + 1, ey, 0);
                Poly(dc, 0x60FFE080, null, 0, a, b, new Point(b.X, b.Y + 4), new Point(a.X, a.Y + 4));
            }
        // Everything standing in the room, back to front.
        var things = new List<(double Depth, double Z, Action Draw)>();
        for (int x = 0; x < n; x++)
        for (int y = 0; y < n; y++)
            for (int k = 0; k < r.Block[x, y]; k++)
            {
                int bx = x, by = y, bz = k;
                things.Add((x + y + 1, k, () => Cube(dc, bx, by, bz, st, r.Crown && bx == Field.Mid && by == Field.Mid)));
            }
        if (r.Crown && !w.CrownTaken)
        {
            var (kx, ky, kz) = Sim.CrownAt;
            things.Add((kx + ky + 0.52, kz, () => CrownFigure(dc, Iso(kx, ky, kz), 1)));
        }
        foreach (var b in r.Balls)
        {
            var bb = b;
            things.Add((b.X + b.Y + 1.01, Sim.BallZ(w, b), () => Ball(dc, bb.X + 0.5, bb.Y + 0.5, Sim.BallZ(w, bb))));
        }
        foreach (var gd in w.Guards)
        {
            var gg = gd;
            things.Add((gd.X + gd.Y, 0, () => GuardFigure(dc, gg)));
        }
        var h = w.Hero;
        bool show = !h.Dead || (int)(Engine.PhaseTime * 10) % 2 == 0;
        if (w.Joined)
        {
            var head = w.Bodies[World.HeadIndex];
            if (show) things.Add((h.X + h.Y, h.Z, () => { HeelsFigure(dc, h, true); HeadFigure(dc, head, true, Iso(h.X, h.Y, h.Z + Sim.RideHeight)); }));
        }
        else
        {
            for (int i = 0; i < 2; i++)
            {
                var b = w.Bodies[i];
                if (b.Room != w.Room || i == w.Active && !show) continue;
                bool moving = i == w.Active;
                if (i == World.HeadIndex) things.Add((b.X + b.Y, b.Z, () => HeadFigure(dc, b, moving, Iso(b.X, b.Y, b.Z))));
                else things.Add((b.X + b.Y, b.Z, () => HeelsFigure(dc, b, moving)));
            }
        }
        foreach (var t in things.OrderBy(t => t.Depth).ThenBy(t => t.Z)) t.Draw();
    }

    private static uint Shade(uint c, double k)
    {
        uint r = (uint)Math.Min(255, ((c >> 16) & 0xFF) * k), gg = (uint)Math.Min(255, ((c >> 8) & 0xFF) * k), b = (uint)Math.Min(255, (c & 0xFF) * k);
        return 0xFF000000 | r << 16 | gg << 8 | b;
    }

    private static void Arch(DrawingContext dc, Point a, Point b, (uint Top, uint Left, uint Right, uint Floor1, uint Floor2, uint Wall) st)
    {
        double h = BH * 1.9;
        Poly(dc, 0xFF0A0A10, null, 0, a, b, new Point(b.X, b.Y - h), new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2 - h - 14), new Point(a.X, a.Y - h));
        dc.DrawLine(Pen(st.Top, 3), a, new Point(a.X, a.Y - h));
        dc.DrawLine(Pen(st.Top, 3), b, new Point(b.X, b.Y - h));
    }

    private static void Cube(DrawingContext dc, int x, int y, int z, (uint Top, uint Left, uint Right, uint Floor1, uint Floor2, uint Wall) st, bool plinth)
    {
        uint top = plinth ? 0xFF8A6A40 : st.Top, left = plinth ? 0xFF6A4A28 : st.Left, right = plinth ? 0xFF54381C : st.Right;
        Poly(dc, left, 0xFF202020, 1, Iso(x, y + 1, z), Iso(x + 1, y + 1, z), Iso(x + 1, y + 1, z + 1), Iso(x, y + 1, z + 1));
        Poly(dc, right, 0xFF202020, 1, Iso(x + 1, y, z), Iso(x + 1, y + 1, z), Iso(x + 1, y + 1, z + 1), Iso(x + 1, y, z + 1));
        Poly(dc, top, 0xFF202020, 1, Iso(x, y, z + 1), Iso(x + 1, y, z + 1), Iso(x + 1, y + 1, z + 1), Iso(x, y + 1, z + 1));
        // A carved line round each face.
        var c = Iso(x + 0.5, y + 0.5, z + 1);
        dc.DrawEllipse(null, Pen(Shade(top, 0.85), 1), c, TW * 0.45, TH * 0.45);
    }

    /// <summary>The crown: gold, with jewels, slowly turning.</summary>
    private void CrownFigure(DrawingContext dc, Point p, double k)
    {
        double bob = Math.Sin(_clock * 3) * 3 * k;
        double y = p.Y - 14 * k + bob, w = 20 * k * (0.75 + 0.25 * Math.Abs(Math.Cos(_clock * 1.5)));
        dc.DrawEllipse(Brush(0x40FFE060), null, new Point(p.X, y - 6 * k), 30 * k, 22 * k);
        Poly(dc, 0xFFFFD030, 0xFF8A6A10, 1.5, new Point(p.X - w, y + 8 * k), new Point(p.X - w, y - 10 * k), new Point(p.X - w / 2, y - 2 * k), new Point(p.X, y - 16 * k),
            new Point(p.X + w / 2, y - 2 * k), new Point(p.X + w, y - 10 * k), new Point(p.X + w, y + 8 * k));
        dc.FillEllipse(p.X, y + 2 * k, 3.5 * k, 0xFFE02040);
        dc.FillEllipse(p.X - w * 0.6, y + 3 * k, 2.5 * k, 0xFF40A0FF);
        dc.FillEllipse(p.X + w * 0.6, y + 3 * k, 2.5 * k, 0xFF40E060);
    }

    private void Ball(DrawingContext dc, double x, double y, double z)
    {
        var f = Iso(x, y, 0);
        dc.DrawEllipse(Brush(0x60000000), null, f, 16 - z * 4, 8 - z * 2);
        var p = Iso(x, y, z + 0.4);
        for (int i = 0; i < 8; i++)
        {
            double a = i * Math.PI / 4 + _clock;
            dc.DrawLine(Pen(0xFFD0D0D8, 2), p, new Point(p.X + Math.Cos(a) * 20, p.Y + Math.Sin(a) * 20));
        }
        dc.FillEllipse(p.X, p.Y, 13, 0xFF404048);
        dc.FillEllipse(p.X - 4, p.Y - 4, 4, 0xFF8A8A98);
    }

    private void GuardFigure(DrawingContext dc, Guard g)
    {
        var f = Iso(g.X, g.Y, 0);
        dc.DrawEllipse(Brush(0x50000000), null, f, 15, 7);
        double step = Math.Sin(_clock * 8 + g.Pos) * 4;
        uint metal = g.Def.Look switch { 0 => 0xFFA0A8B8u, 1 => 0xFFB89848u, _ => 0xFF8A6AA8u };
        dc.FillRectangle(Brush(Shade(metal, 0.6)), new Rect(f.X - 8 + step, f.Y - 18, 6, 18));
        dc.FillRectangle(Brush(Shade(metal, 0.6)), new Rect(f.X + 2 - step, f.Y - 18, 6, 18));
        dc.FillRectangle(Brush(metal), new Rect(f.X - 11, f.Y - 42, 22, 26), 4);
        dc.FillEllipse(f.X, f.Y - 50, 10, metal);
        dc.FillRectangle(Brush(0xFF101010), new Rect(f.X - 7, f.Y - 52, 14, 3));
        Poly(dc, 0xFFD03030, null, 0, new Point(f.X - 3, f.Y - 60), new Point(f.X + 3, f.Y - 60), new Point(f.X, f.Y - 72));
        // The spear.
        dc.DrawLine(Pen(0xFF6A4A2A, 3), new Point(f.X + 16, f.Y - 4), new Point(f.X + 16, f.Y - 66));
        Poly(dc, 0xFFD0D0D8, null, 0, new Point(f.X + 12, f.Y - 66), new Point(f.X + 20, f.Y - 66), new Point(f.X + 16, f.Y - 80));
    }

    /// <summary>Head: big ears, big eyes, a round snout, and little wings; dimmer when not the one on the move.</summary>
    private void HeadFigure(DrawingContext dc, Hero h, bool moving, Point f)
    {
        if (!h.Airborne || h.Z < 0.5) { var sh = Iso(h.X, h.Y, Math.Max(0, Math.Floor(h.Z + 0.01))); dc.DrawEllipse(Brush(0x50000000), null, sh, 13, 6); }
        double sx = (h.FaceX - h.FaceY) * 0.5, flap = h.Airborne ? Math.Sin(_clock * 30) * 6 : 0;
        uint fur = moving ? 0xFFE0A050 : 0xFFA88050, dark = Shade(fur, 0.6);
        // Wings.
        Poly(dc, 0xFFF0F0FF, 0xFF8A8AA0, 1, new Point(f.X - 10, f.Y - 22), new Point(f.X - 24, f.Y - 32 - flap), new Point(f.X - 14, f.Y - 16));
        Poly(dc, 0xFFF0F0FF, 0xFF8A8AA0, 1, new Point(f.X + 10, f.Y - 22), new Point(f.X + 24, f.Y - 32 - flap), new Point(f.X + 14, f.Y - 16));
        // Feet, body, head.
        dc.FillEllipse(f.X - 5, f.Y - 3, 4, dark);
        dc.FillEllipse(f.X + 5, f.Y - 3, 4, dark);
        dc.DrawEllipse(Brush(fur), Pen(dark, 1.5), new Point(f.X, f.Y - 20), 14, 15);
        dc.DrawEllipse(Brush(dark), null, new Point(f.X - 13, f.Y - 30), 5, 9);
        dc.DrawEllipse(Brush(dark), null, new Point(f.X + 13, f.Y - 30), 5, 9);
        dc.FillEllipse(f.X + sx * 6 - 5, f.Y - 24, 4.5, 0xFFFFFFFF);
        dc.FillEllipse(f.X + sx * 6 + 5, f.Y - 24, 4.5, 0xFFFFFFFF);
        dc.FillEllipse(f.X + sx * 8 - 5, f.Y - 24, 2, 0xFF101010);
        dc.FillEllipse(f.X + sx * 8 + 5, f.Y - 24, 2, 0xFF101010);
        dc.DrawEllipse(Brush(0xFFF0D0A0), null, new Point(f.X + sx * 7, f.Y - 15), 7, 5);
        dc.FillEllipse(f.X + sx * 9, f.Y - 17, 2.5, 0xFF301010);
    }

    /// <summary>Heels: all feet, a round body, a pouch, and a small head with a bright eye.</summary>
    private void HeelsFigure(DrawingContext dc, Hero h, bool moving)
    {
        var f = Iso(h.X, h.Y, h.Z);
        var sh = Iso(h.X, h.Y, Math.Max(0, Math.Floor(h.Z + 0.01)));
        dc.DrawEllipse(Brush(0x50000000), null, sh, 15, 7);
        double sx = (h.FaceX - h.FaceY) * 0.5, step = h.Airborne ? 0 : Math.Sin(h.Walk * 14) * 5;
        uint body = moving ? 0xFF50A0E0 : 0xFF5878A0, dark = Shade(body, 0.6);
        // The big feet.
        dc.DrawEllipse(Brush(0xFFD04030), Pen(0xFF601818, 1.5), new Point(f.X - 7 + step + sx * 4, f.Y - 4), 9, 5);
        dc.DrawEllipse(Brush(0xFFD04030), Pen(0xFF601818, 1.5), new Point(f.X + 7 - step + sx * 4, f.Y - 4), 9, 5);
        dc.DrawEllipse(Brush(body), Pen(dark, 1.5), new Point(f.X, f.Y - 20), 14, 13);
        dc.DrawEllipse(Brush(0xFFE0C080), Pen(0xFF8A6A30, 1), new Point(f.X + sx * 6, f.Y - 16), 7, 6);
        dc.DrawEllipse(Brush(body), Pen(dark, 1.5), new Point(f.X + sx * 3, f.Y - 33), 9, 8);
        dc.FillEllipse(f.X + sx * 8, f.Y - 35, 3.5, 0xFFFFFFFF);
        dc.FillEllipse(f.X + sx * 9, f.Y - 35, 1.8, 0xFF101010);
    }

    // ───────────────────────────── the scroll below ─────────────────────────────

    private void DrawHud(DrawingContext dc, World w)
    {
        var g = Engine;
        double y0 = ViewH;
        dc.FillRectangle(Brush(0xFF14101C), new Rect(0, y0, Field.Width, Field.Height - y0));
        dc.FillRectangle(Brush(0xFF6A4A8A), new Rect(0, y0, Field.Width, 4));
        // Who's on the move: Head, Heels, or both.
        void Portrait(double x, int who, string name)
        {
            bool on = w.Joined || w.Active == who;
            dc.DrawRectangle(Brush(on ? 0xFF2A2440 : 0xFF18141E), Pen(on ? Yellow : 0xFF3A3450, 3), new Rect(x - 60, y0 + 20, 120, 120), 8);
            var b = new Hero { FaceX = 0, FaceY = 1, Walk = on ? _clock : 0 };
            using (dc.PushTransform(Matrix.CreateScale(1.6, 1.6) * Matrix.CreateTranslation(x - 1.6 * OX, y0 + 120 - 1.6 * OY)))
            {
                if (who == World.HeadIndex) HeadFigure(dc, b, on, Iso(0, 0, 0)); else HeelsFigure(dc, b, on);
            }
            Text(dc, name, new Point(x, y0 + 158), 24, on ? Yellow : Dim, center: true);
        }
        Portrait(90, World.HeadIndex, "HEAD");
        Portrait(240, World.HeelsIndex, "HEELS");
        Text(dc, w.Joined ? "TOGETHER" : "APART - C TO SWAP", new Point(165, y0 + 190), 20, w.Joined ? 0xFF80FF80 : 0xFFB0A0D0, center: true);
        // The castle, the crowns, the score and the lives.
        Text(dc, w.Castle.Name, new Point(560, y0 + 34), 28, 0xFFE0D0FF, center: true);
        Text(dc, $"CROWNS {g.Cleared} OF {CastleBook.Count}", new Point(560, y0 + 70), 24, 0xFFFFD040, center: true);
        for (int i = 0; i < CastleBook.Count; i++)
        {
            using (dc.PushTransform(Matrix.CreateTranslation(0, 0)))
                if (i < g.Cleared) CrownFigure(dc, new Point(480 + i * 40, y0 + 128), 0.8);
                else dc.DrawEllipse(null, Pen(0xFF3A3450, 2), new Point(480 + i * 40, y0 + 112), 12, 12);
        }
        Text(dc, $"SCORE {g.Score:000000}", new Point(Field.Width - 30, y0 + 34), 28, Yellow, right: true);
        Text(dc, $"HIGH  {Math.Max(g.HighScores.TopScore, g.Score):000000}", new Point(Field.Width - 30, y0 + 66), 22, 0xFFB0A0D0, right: true);
        Text(dc, $"LIVES {Math.Max(0, g.Lives)}", new Point(Field.Width - 30, y0 + 98), 26, 0xFFFF8080, right: true);
        // Where they are: a small plan of the castle.
        var c = w.Castle;
        double cs = 14, mx = Field.Width - 30 - c.Cols * cs, my = y0 + 120;
        foreach (var r in c.Rooms)
        {
            uint fill = r.Index == w.Room ? 0xFFFFFFFF : r.Crown && !w.CrownTaken ? 0xFFFFD040 : 0xFF3A3450;
            dc.FillRectangle(Brush(fill), new Rect(mx + r.Col * cs, my + r.Row * cs, cs - 3, cs - 3));
        }
        if (!w.Joined)
        {
            var other = w.Other;
            var or = c.Rooms[other.Room];
            dc.DrawRectangle(null, Pen(0xFF80FF80, 2), new Rect(mx + or.Col * cs - 1, my + or.Row * cs - 1, cs - 1, cs - 1));
        }
    }

    // ───────────────────────────── the title and the end ─────────────────────────────

    private void DrawTitle(DrawingContext dc)
    {
        double cx = Field.Width / 2;
        dc.FillRectangle(Brush(0xFF100C18), new Rect(0, 0, Field.Width, Field.Height));
        string title = "AVA HEAD OVER HEELS";
        for (int i = 0; i < title.Length; i++)
        {
            double tx = cx - title.Length * 25 + 25 + i * 50, ty = 84 + Math.Sin(_clock * 3 + i * 0.5) * 6;
            Text(dc, title[i].ToString(), new Point(tx, ty), 92, i % 2 == 0 ? 0xFFFFD040 : 0xFF60C0FF, center: true);
        }
        Text(dc, "AFTER JON RITMAN AND BERNIE DRUMMOND'S 1987 GAME FOR OCEAN", new Point(cx, 146), 22, 0xFFB0A0D0, center: true);
        // The two of them: apart, then together.
        var head = new Hero { FaceX = 1, FaceY = 0, Walk = _clock };
        var heels = new Hero { FaceX = 0, FaceY = 1, Walk = _clock };
        using (dc.PushTransform(Matrix.CreateScale(2.6, 2.6) * Matrix.CreateTranslation(cx - 300 - 2.6 * OX, 400 - 2.6 * OY)))
            HeadFigure(dc, head, true, Iso(0, 0, 0));
        using (dc.PushTransform(Matrix.CreateScale(2.6, 2.6) * Matrix.CreateTranslation(cx - 2.6 * OX, 400 - 2.6 * OY)))
            HeelsFigure(dc, heels, true);
        using (dc.PushTransform(Matrix.CreateScale(2.6, 2.6) * Matrix.CreateTranslation(cx + 300 - 2.6 * OX, 400 - 2.6 * OY)))
        {
            HeelsFigure(dc, heels, true);
            HeadFigure(dc, head, true, Iso(0, 0, Sim.RideHeight));
        }
        Text(dc, "HEAD: JUMPS HIGH", new Point(cx - 300, 436), 22, 0xFFE0A050, center: true);
        Text(dc, "HEELS: RUNS AND LEAPS", new Point(cx, 436), 22, 0xFF60C0FF, center: true);
        Text(dc, "TOGETHER: BOTH", new Point(cx + 300, 436), 22, 0xFF80FF80, center: true);
        Text(dc, "THE EMPEROR HAS CAPTURED TWO SPIES AND LOCKED THEM APART IN HIS CASTLES. HEELS MUST FIND HEAD", new Point(cx, 492), 20, White, center: true);
        Text(dc, "(MAROONED ON AN ISLAND HE CANNOT JUMP FROM); THEN, TOGETHER, THEY CAN REACH EACH CASTLE'S CROWN.", new Point(cx, 518), 20, White, center: true);
        Text(dc, "WIN ALL FIVE CROWNS TO TOPPLE THE EMPEROR.", new Point(cx, 544), 20, White, center: true);
        Text(dc, "PRESS ENTER TO START", new Point(cx, 606), 40, (int)(_clock * 2) % 2 == 0 ? Yellow : Orange, center: true);
        Text(dc, "ARROWS: WALK (UP IS AWAY)   SPACE: JUMP   C OR TAB: SWAP / SPLIT   P: PAUSE   M: SOUND", new Point(cx, 664), 20, Dim, center: true);
        Text(dc, $"HIGH SCORE {Engine.HighScores.TopScore:000000}", new Point(cx, 712), 24, Cyan, center: true);
    }

    private void DrawGameOver(DrawingContext dc)
    {
        double cx = Field.Width / 2;
        bool won = Engine.Cleared >= CastleBook.Count;
        Text(dc, won ? "THE EMPEROR FALLS!" : "GAME OVER", new Point(cx, 220), won ? 90 : 110, won ? Yellow : Red, center: true);
        Text(dc, won ? "ALL FIVE CROWNS WON" : $"{Engine.Cleared} CROWN{(Engine.Cleared == 1 ? "" : "S")} WON", new Point(cx, 310), 34, White, center: true);
        var heels = new Hero { FaceY = 1, Walk = _clock };
        using (dc.PushTransform(Matrix.CreateScale(2.2, 2.2) * Matrix.CreateTranslation(cx - 2.2 * OX, 560 - 2.2 * OY)))
        {
            HeelsFigure(dc, heels, true);
            HeadFigure(dc, heels, true, Iso(0, 0, Sim.RideHeight));
        }
        Text(dc, $"SCORE {Engine.Score}", new Point(cx, 640), 44, Yellow, center: true);
    }

    private void DrawHighScores(DrawingContext dc)
    {
        var g = Engine;
        double cx = Field.Width / 2;
        Text(dc, "THE FINEST SPIES", new Point(cx, 120), 64, 0xFFFFE040, center: true);
        Text(dc, "RANK NAME    SCORE  CROWNS", new Point(cx, 190), 30, Dim, center: true);
        var entries = g.HighScores.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            bool mine = i == g.NewEntryIndex && (int)(_clock * 4) % 2 == 0;
            Text(dc, $"{i + 1,2}.  {e.Initials}  {e.Score,8}   {e.Stage,5}", new Point(cx, 236 + i * 42), 34, mine ? Red : i == 0 ? Yellow : White, center: true);
        }
        Text(dc, "PRESS ENTER TO PLAY", new Point(cx, 680), 30, White, center: true);
    }

    private void DrawInitials(DrawingContext dc)
    {
        var g = Engine;
        double cx = Field.Width / 2;
        Text(dc, "A NEW HIGH SCORE!", new Point(cx, 200), 64, Yellow, center: true);
        Text(dc, g.Score.ToString(), new Point(cx, 270), 52, White, center: true);
        Text(dc, "ENTER YOUR INITIALS", new Point(cx, 350), 34, Dim, center: true);
        if (g.Initials is { } entry)
        {
            string s = entry.Initials;
            for (int i = 0; i < s.Length; i++)
            {
                bool cur = i == entry.Cursor && (int)(_clock * 3) % 2 == 0;
                Text(dc, s[i].ToString(), new Point(cx - 60 + i * 60, 440), 80, cur ? Red : Yellow, center: true);
                dc.FillRectangle(Brush(i == entry.Cursor ? Red : Dim), new Rect(cx - 82 + i * 60, 480, 44, 4));
            }
        }
        Text(dc, "TYPE, OR UP/DOWN AND SPACE; ENTER WHEN DONE", new Point(cx, 560), 26, White, center: true);
    }
}

internal static class DrawingExtensions
{
    public static void FillEllipse(this DrawingContext dc, double x, double y, double r, uint colour) =>
        dc.DrawEllipse(GameCanvas.Brush(colour), null, new Point(x, y), r, r);
    public static void FillRectangle(this DrawingContext dc, IBrush brush, Rect rect, double radius) => dc.FillRectangle(brush, rect, (float)radius);
}
