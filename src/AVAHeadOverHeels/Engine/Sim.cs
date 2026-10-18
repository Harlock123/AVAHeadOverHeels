namespace AVAHeadOverHeels.Engine;

/// <summary>
/// The castle, step by step: Head or Heels walking and jumping among the blocks (a jump is a fixed arc), falling,
/// guards pacing, spiked balls bouncing, spikes and pits; doors into the next room; swapping from one to the other;
/// joining up, and splitting again; and the crown.
/// </summary>
public static class Sim
{
    private const double Dt = Field.Step, H = Hero.Half;
    public const double BallHeight = 1.7, JoinReach = 0.6, RideHeight = 0.9;

    public static void Start(World w, CastleDef c)
    {
        w.Castle = c;
        w.CrownTaken = false;
        w.Clock = 0;
        w.Cause = "";
        w.Joined = false;
        w.JoinArmed = true;
        // Head on the middle of the island; Heels near the south wall of the first room.
        var head = w.Bodies[World.HeadIndex] = new Hero { X = Field.Mid + 0.5, Y = Field.Mid + 0.5, Room = c.HeadStart };
        head.Entry = (head.X, head.Y, 0);
        var heels = w.Bodies[World.HeelsIndex] = new Hero { X = Field.Mid + 0.5, Y = Field.N - 1.5, Room = c.HeelsStart };
        heels.Entry = (heels.X, heels.Y, 0);
        w.Active = World.HeelsIndex;
        Show(w, heels.Room);
    }

    /// <summary>The room on screen: its guards at their posts.</summary>
    private static void Show(World w, int room)
    {
        w.Room = room;
        w.Guards = w.Here.Guards.Select(g => new Guard { Def = g, Pos = (g.Min + g.Max) / 2 }).ToList();
    }

    /// <summary>The one on the move into a room, through a door (just inside it).</summary>
    public static void Enter(World w, int room, Side by)
    {
        var old = w.Hero;
        var h = new Hero { JumpPrev = old.JumpPrev, Room = room };
        var (x, y) = RoomDef.DoorCell(by);
        h.X = x + 0.5;
        h.Y = y + 0.5;
        var (ox, oy) = RoomDef.Out(by);
        (h.FaceX, h.FaceY) = (-ox, -oy);
        h.Z = Math.Max(0, w.Castle.Rooms[room].Top((int)h.X, (int)h.Y));
        h.Entry = (h.X, h.Y, h.Z);
        w.Bodies[w.Joined ? World.HeelsIndex : w.Active] = h;
        Show(w, room);
        Ride(w);
    }

    /// <summary>After a death: back where the one on the move came into the room.</summary>
    public static void Restart(World w)
    {
        var old = w.Hero;
        var (x, y, z) = old.Entry;
        w.Bodies[w.Joined ? World.HeelsIndex : w.Active] = new Hero { X = x, Y = y, Z = z, Room = old.Room, Entry = old.Entry, FaceX = old.FaceX, FaceY = old.FaceY };
        Show(w, old.Room);
        w.Cause = "";
        Ride(w);
    }

    /// <summary>Joined, Head goes wherever Heels goes, on top.</summary>
    private static void Ride(World w)
    {
        if (!w.Joined) return;
        var heels = w.Bodies[World.HeelsIndex];
        var head = w.Bodies[World.HeadIndex];
        (head.X, head.Y, head.Z, head.Room, head.FaceX, head.FaceY, head.Airborne, head.Walk) = (heels.X, heels.Y, heels.Z + RideHeight, heels.Room, heels.FaceX, heels.FaceY, heels.Airborne, heels.Walk);
        head.Entry = heels.Entry;
    }

    public static void Step(World w, Controls c)
    {
        w.Clock += Dt;
        foreach (var g in w.Guards)
        {
            g.Pos += g.Dir * g.Def.Speed * Dt;
            if (g.Pos > g.Def.Max) { g.Pos = g.Def.Max; g.Dir = -1; }
            if (g.Pos < g.Def.Min) { g.Pos = g.Def.Min; g.Dir = 1; }
        }
        var h = w.Hero;
        if (h.Dead) return;
        if (c.Swap && !w.SwapPrev && !h.Airborne && !w.Probe) { w.SwapPrev = true; Swap(w); return; }
        w.SwapPrev = c.Swap;
        if (!h.Airborne) Ground(w, h, c);
        if (h.Airborne) Air(w, h);
        h.JumpPrev = c.Jump;
        if (!h.Dead) Touch(w, h);
        if (!h.Dead) Doors(w, h);
        if (!w.Probe) Join(w);
        Ride(w);
    }

    /// <summary>
    /// Swap: joined, they come apart (Head left standing where they were, now the one on the move); apart, the other
    /// one takes over, wherever they are.
    /// </summary>
    public static void Swap(World w)
    {
        if (w.Joined)
        {
            var heels = w.Bodies[World.HeelsIndex];
            var head = w.Bodies[World.HeadIndex];
            w.Joined = false;
            w.JoinArmed = false;
            (head.X, head.Y, head.Z, head.Airborne, head.Vz, head.Room) = (heels.X, heels.Y, heels.Z, false, 0, heels.Room);
            head.Entry = (head.X, head.Y, head.Z);
            heels.Entry = (heels.X, heels.Y, heels.Z);
            w.Active = World.HeadIndex;
            w.Heard.Add(Sfx.Split);
            return;
        }
        w.Active = 1 - w.Active;
        var now = w.Hero;
        now.Entry = (now.X, now.Y, now.Z);
        Show(w, now.Room);
        w.Heard.Add(Sfx.Swap);
    }

    /// <summary>Apart, and the one on the move walks into the other: Head climbs on Heels, and they go as one.</summary>
    private static void Join(World w)
    {
        if (w.Joined) return;
        var h = w.Hero;
        var o = w.Other;
        if (o.Room != h.Room) { w.JoinArmed = true; return; }
        double d = Math.Max(Math.Abs(o.X - h.X), Math.Abs(o.Y - h.Y));
        if (d > 1.0) w.JoinArmed = true;
        if (!w.JoinArmed || h.Airborne || d > JoinReach || Math.Abs(o.Z - h.Z) > 0.7) return;
        // Heels stays where Heels is; Head gets on top.
        w.Joined = true;
        var heels = w.Bodies[World.HeelsIndex];
        heels.Entry = (heels.X, heels.Y, heels.Z);
        Ride(w);
        w.Heard.Add(Sfx.Join);
    }

    /// <summary>The top of whatever is at a point: inside the room its square's, in a doorway the floor, else the wall.</summary>
    public static double TopAt(World w, double x, double y)
    {
        var r = w.Here;
        int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
        if (ix >= 0 && iy >= 0 && ix < Field.N && iy < Field.N) return r.Top(ix, iy);
        if (iy == Field.Mid && ix < 0 && r.Doors[(int)Side.West]) return 0;
        if (iy == Field.Mid && ix >= Field.N && r.Doors[(int)Side.East]) return 0;
        if (ix == Field.Mid && iy < 0 && r.Doors[(int)Side.North]) return 0;
        if (ix == Field.Mid && iy >= Field.N && r.Doors[(int)Side.South]) return 0;
        return 99;
    }

    /// <summary>The highest thing under the feet.</summary>
    public static double Support(World w, double x, double y)
    {
        double s = -100;
        foreach (double fx in new[] { x - H + 0.02, x + H - 0.02 })
        foreach (double fy in new[] { y - H + 0.02, y + H - 0.02 })
            s = Math.Max(s, TopAt(w, fx, fy));
        return s;
    }

    private static bool Blocked(World w, double x, double y, double z)
    {
        foreach (double fx in new[] { x - H, x + H })
        foreach (double fy in new[] { y - H, y + H })
            if (TopAt(w, fx, fy) > z + 0.05) return true;
        return false;
    }

    private static void Ground(World w, Hero h, Controls c)
    {
        if (Support(w, h.X, h.Y) < h.Z - 0.01)
        {
            // Off an edge.
            h.Airborne = true;
            h.Vz = 0;
            h.Jdx = h.Jdy = 0;
            return;
        }
        var a = w.Ability;
        int dx = c.Dx, dy = dx != 0 ? 0 : c.Dy;
        if (c.Jump && !h.JumpPrev)
        {
            h.Airborne = true;
            h.Vz = a.JumpSpeed;
            h.Jdx = dx;
            h.Jdy = dy;
            if (dx != 0 || dy != 0) (h.FaceX, h.FaceY) = (dx, dy);
            w.Heard.Add(Sfx.Jump);
            return;
        }
        if (dx == 0 && dy == 0) return;
        (h.FaceX, h.FaceY) = (dx, dy);
        h.Walk += Dt * a.Speed / 3;
        double nx = h.X + dx * a.Speed * Dt, ny = h.Y + dy * a.Speed * Dt;
        if (!Blocked(w, nx, ny, h.Z)) { h.X = nx; h.Y = ny; return; }
        // Against a wall beside a doorway: slid into line with it.
        double door = Field.Mid + 0.5;
        var r = w.Here;
        bool doorX = dx > 0 && r.Doors[(int)Side.East] || dx < 0 && r.Doors[(int)Side.West];
        bool doorY = dy > 0 && r.Doors[(int)Side.South] || dy < 0 && r.Doors[(int)Side.North];
        if (doorX && Math.Abs(h.Y - door) < 0.6 && Math.Abs(h.Y - door) > 0.01)
        {
            double ty = h.Y + Math.Sign(door - h.Y) * Math.Min(Math.Abs(door - h.Y), a.Speed * Dt);
            if (!Blocked(w, h.X, ty, h.Z)) h.Y = ty;
        }
        if (doorY && Math.Abs(h.X - door) < 0.6 && Math.Abs(h.X - door) > 0.01)
        {
            double tx = h.X + Math.Sign(door - h.X) * Math.Min(Math.Abs(door - h.X), a.Speed * Dt);
            if (!Blocked(w, tx, h.Y, h.Z)) h.X = tx;
        }
    }

    private static void Air(World w, Hero h)
    {
        var a = w.Ability;
        double nx = h.X + h.Jdx * a.Speed * Dt, ny = h.Y + h.Jdy * a.Speed * Dt;
        // Against a block's side in mid-air, keep pressing on, and go over once risen above it.
        if ((h.Jdx != 0 || h.Jdy != 0) && !Blocked(w, nx, ny, h.Z)) { h.X = nx; h.Y = ny; }
        double vz = h.Vz - Hero.Gravity * Dt, nz = h.Z + vz * Dt;
        if (vz <= 0)
        {
            double s = Support(w, h.X, h.Y);
            if (s > -50 && nz <= s && h.Z >= s - 0.05)
            {
                h.Z = s;
                h.Vz = 0;
                h.Airborne = false;
                w.Heard.Add(Sfx.Land);
                return;
            }
        }
        h.Vz = vz;
        h.Z = nz;
        if (h.Z < -1.6) Die(w, "FELL INTO THE DARK");
    }

    public static double BallZ(World w, BallDef b) => BallHeight * Math.Abs(Math.Sin(Math.PI * (w.Clock / b.Period + b.Phase)));

    /// <summary>Where the crown sits: on its pillar in the middle of the crown room.</summary>
    public static (double X, double Y, double Z) CrownAt => (Field.Mid + 0.5, Field.Mid + 0.5, 2);

    private static void Touch(World w, Hero h)
    {
        var r = w.Here;
        int cx = (int)Math.Floor(h.X), cy = (int)Math.Floor(h.Y);
        if (!h.Airborne && h.Z < 0.05 && cx >= 0 && cy >= 0 && cx < Field.N && cy < Field.N && r.Block[cx, cy] == 0 && r.Floor[cx, cy] == Floor.Spikes)
        {
            Die(w, "SPIKED");
            return;
        }
        double tall = w.Joined ? Hero.Tall + RideHeight : Hero.Tall;
        foreach (var g in w.Guards)
            if (Math.Abs(g.X - h.X) < 0.6 && Math.Abs(g.Y - h.Y) < 0.6 && h.Z < 1.0) { Die(w, "CAUGHT BY A GUARD"); return; }
        if (!w.Probe)
            foreach (var b in r.Balls)
            {
                double bz = BallZ(w, b);
                if (Math.Abs(b.X + 0.5 - h.X) < 0.58 && Math.Abs(b.Y + 0.5 - h.Y) < 0.58 && h.Z < bz + 0.55 && h.Z + tall > bz) { Die(w, "A SPIKED BALL"); return; }
            }
        if (w.Probe || !r.Crown || w.CrownTaken) return;
        var (kx, ky, kz) = CrownAt;
        if (Math.Abs(kx - h.X) < 0.55 && Math.Abs(ky - h.Y) < 0.55 && Math.Abs(h.Z - kz) < 0.6)
        {
            w.CrownTaken = true;
            w.Heard.Add(Sfx.Crown);
        }
    }

    private static void Doors(World w, Hero h)
    {
        Side? through = h.Y < -0.3 ? Side.North : h.X > Field.N + 0.3 ? Side.East : h.Y > Field.N + 0.3 ? Side.South : h.X < -0.3 ? Side.West : null;
        if (through is not { } s) return;
        if (w.Probe) { h.Dead = true; w.Cause = "OUT"; return; }
        int next = w.Castle.Through(w.Room, s);
        if (next < 0) return;
        Enter(w, next, RoomDef.Opposite(s));
        w.Heard.Add(Sfx.Door);
    }

    public static void Die(World w, string cause)
    {
        var h = w.Hero;
        if (h.Dead) return;
        h.Dead = true;
        w.Cause = cause;
        if (!w.Probe) w.Heard.Add(Sfx.Die);
    }
}
