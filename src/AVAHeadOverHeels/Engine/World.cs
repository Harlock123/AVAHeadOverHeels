namespace AVAHeadOverHeels.Engine;

/// <summary>A small random number generator whose state can be copied.</summary>
public struct Dice
{
    private ulong _s;
    public Dice(int seed) => _s = (ulong)seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL;

    public double Next()
    {
        _s ^= _s << 13;
        _s ^= _s >> 7;
        _s ^= _s << 17;
        return (_s >> 11) * (1.0 / (1UL << 53));
    }

    public int Next(int n) => (int)(Next() * n);
}

/// <summary>Head, or Heels: which room, where in it (in squares), and how high.</summary>
public sealed class Hero
{
    public const double Half = 0.27, Gravity = 20, Tall = 1.0;
    public double X, Y, Z, Vz, Walk;
    public int Jdx, Jdy, FaceX = 0, FaceY = 1, Room;
    public bool Airborne, Dead, JumpPrev;
    /// <summary>Where this one came into its room (and starts again if killed).</summary>
    public (double X, double Y, double Z) Entry;
    public Hero Copy() => (Hero)MemberwiseClone();
}

public sealed class Guard
{
    public GuardDef Def;
    public double Pos;
    public int Dir = 1;
    public double X => Def.AlongX ? Pos : Def.Fixed;
    public double Y => Def.AlongX ? Def.Fixed : Pos;
    public Guard Copy() => (Guard)MemberwiseClone();
}

/// <summary>Everything in the castle that changes, kept together so it can be copied whole.</summary>
public sealed class World
{
    public const int HeadIndex = 0, HeelsIndex = 1;
    public CastleDef Castle = null!;
    /// <summary>Head and Heels.</summary>
    public Hero[] Bodies = [new(), new()];
    /// <summary>Which one is being moved (when they're apart).</summary>
    public int Active = HeelsIndex;
    /// <summary>Head riding on Heels.</summary>
    public bool Joined;
    /// <summary>They'll join on touching only after they've been apart: so splitting doesn't join them straight back.</summary>
    public bool JoinArmed = true;
    public bool SwapPrev;
    /// <summary>The room on screen: the one the moving character is in.</summary>
    public int Room;
    public List<Guard> Guards = [];
    public bool CrownTaken;
    public double Clock;
    public string Cause = "";
    public List<Sfx> Heard = [];
    /// <summary>A world used only to try moves out (by one way of getting about): no doors gone through, nothing joined.</summary>
    public bool Probe;
    public Ability? ProbeAbility;

    /// <summary>The one on the move: Heels when they're joined (Head rides on top).</summary>
    public Hero Hero => Bodies[Joined ? HeelsIndex : Active];
    public Hero Other => Bodies[Joined ? HeadIndex : 1 - Active];
    public Ability Ability => ProbeAbility ?? (Joined ? Ability.Joined : Active == HeadIndex ? Ability.Head : Ability.Heels);
    public RoomDef Here => Castle.Rooms[Room];

    public World Copy() => new()
    {
        Castle = Castle, Bodies = [Bodies[0].Copy(), Bodies[1].Copy()], Active = Active, Joined = Joined, JoinArmed = JoinArmed,
        SwapPrev = SwapPrev, Room = Room, Guards = Guards.Select(g => g.Copy()).ToList(), CrownTaken = CrownTaken, Clock = Clock,
        Cause = Cause, Probe = Probe, ProbeAbility = ProbeAbility,
    };
}
