using Avalonia.Input;
using AVAHeadOverHeels.Engine;

namespace AVAHeadOverHeels.Tests;

public class GameplayTests
{
    private static string TempFolder() => Path.Combine(Path.GetTempPath(), "avaheadoverheels-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>One bare room (doors on every side), with both of them in it.</summary>
    private static World Bare(Action<RoomDef>? build = null)
    {
        var r = new RoomDef { Index = 0, Doors = [true, true, true, true] };
        build?.Invoke(r);
        var c = new CastleDef { Cols = 1, Rows = 1, Rooms = [r], HeadStart = 0, HeelsStart = 0, CrownRoom = 0 };
        var w = new World();
        Sim.Start(w, c);
        w.Bodies[World.HeadIndex].X = 2.5;
        w.Bodies[World.HeadIndex].Y = 2.5;
        w.Bodies[World.HeelsIndex].X = 6.5;
        w.Bodies[World.HeelsIndex].Y = 6.5;
        return w;
    }

    private static void Run(World w, int steps, Controls c = default)
    {
        for (int i = 0; i < steps; i++) Sim.Step(w, c);
    }

    private static void Press(World w, Controls c)
    {
        Sim.Step(w, c);
        Sim.Step(w, default);
    }

    // ───────────────────────────── two ways of getting about ─────────────────────────────

    [Fact]
    public void Heels_runs_faster_than_head()
    {
        var w = Bare();
        double x = w.Hero.X;
        Run(w, 30, new Controls(Dx: -1));
        double heels = x - w.Hero.X;
        Press(w, new Controls(Swap: true));
        Assert.Equal(World.HeadIndex, w.Active);
        x = w.Hero.X;
        Run(w, 30, new Controls(Dx: 1));
        double head = w.Hero.X - x;
        Assert.InRange(heels, Ability.Heels.Speed / 2 - 0.05, Ability.Heels.Speed / 2 + 0.05);
        Assert.InRange(head, Ability.Head.Speed / 2 - 0.05, Ability.Head.Speed / 2 + 0.05);
    }

    [Fact]
    public void Head_jumps_onto_two_blocks_and_heels_only_onto_one()
    {
        foreach (var (who, height, onto) in new[] { (World.HeadIndex, 2, true), (World.HeelsIndex, 2, false), (World.HeelsIndex, 1, true) })
        {
            var w = Bare(r => { for (int y = 0; y < Field.N; y++) r.Block[4, y] = height; });
            w.Active = who;
            w.Hero.X = 3.5;
            w.Hero.Y = 3.5;
            Sim.Step(w, new Controls(Dx: 1, Jump: true));
            for (int i = 0; i < 120 && w.Hero.Airborne; i++) Sim.Step(w, new Controls(Dx: 1));
            Assert.Equal(onto, w.Hero.Z >= height - 0.01);
        }
    }

    [Fact]
    public void Heels_leaps_a_pit_two_wide_and_head_falls_in()
    {
        foreach (var (who, over) in new[] { (World.HeelsIndex, true), (World.HeadIndex, false) })
        {
            var w = Bare(r => { for (int y = 0; y < Field.N; y++) { r.Floor[3, y] = Floor.Pit; r.Floor[4, y] = Floor.Pit; } });
            w.Active = who;
            w.Hero.X = 2.5;
            w.Hero.Y = 2.5;
            Sim.Step(w, new Controls(Dx: 1, Jump: true));
            Run(w, 90);
            Assert.Equal(!over, w.Hero.Dead);
            if (over) Assert.True(w.Hero.X > 5);
        }
    }

    [Fact]
    public void Walking_into_the_other_joins_them_and_they_go_as_one_with_heels_speed_and_head_spring()
    {
        var w = Bare();
        w.Bodies[World.HeadIndex].X = 4.5;
        w.Bodies[World.HeadIndex].Y = 6.5;
        Run(w, 60, new Controls(Dx: -1));
        Assert.True(w.Joined);
        Assert.Contains(Sfx.Join, w.Heard);
        Assert.Same(Ability.Joined, w.Ability);
        var head = w.Bodies[World.HeadIndex];
        Assert.Equal(w.Hero.X, head.X, 3);
        Assert.Equal(w.Hero.Z + Sim.RideHeight, head.Z, 3);
        Sim.Step(w, new Controls(Jump: true));
        double top = 0;
        for (int i = 0; i < 90; i++) { Sim.Step(w, default); top = Math.Max(top, w.Hero.Z); }
        Assert.True(top > 2);
    }

    [Fact]
    public void Swap_while_joined_splits_them_and_they_do_not_join_again_until_they_have_been_apart()
    {
        var w = Bare();
        w.Bodies[World.HeadIndex].X = 5.5;
        w.Bodies[World.HeadIndex].Y = 6.5;
        Run(w, 30, new Controls(Dx: -1));
        Assert.True(w.Joined);
        Press(w, new Controls(Swap: true));
        Assert.False(w.Joined);
        Assert.Equal(World.HeadIndex, w.Active);
        Run(w, 10);
        Assert.False(w.Joined);
        Run(w, 60, new Controls(Dy: -1));
        Run(w, 80, new Controls(Dy: 1));
        Assert.True(w.Joined);
    }

    [Fact]
    public void Swapping_takes_the_screen_to_the_other_ones_room()
    {
        var c = CastleBook.Build(0);
        var w = new World();
        Sim.Start(w, c);
        Assert.Equal(c.HeelsStart, w.Room);
        Press(w, new Controls(Swap: true));
        Assert.Equal(c.HeadStart, w.Room);
        Assert.Equal(World.HeadIndex, w.Active);
    }

    [Fact]
    public void Through_a_door_into_the_next_room()
    {
        var c = CastleBook.Build(0);
        var w = new World();
        Sim.Start(w, c);
        int side = Enumerable.Range(0, 4).First(s => c.Rooms[c.HeelsStart].Doors[s]);
        var (x, y) = RoomDef.DoorCell((Side)side);
        w.Hero.X = x + 0.5;
        w.Hero.Y = y + 0.5;
        var (ox, oy) = RoomDef.Out((Side)side);
        Run(w, 30, new Controls(ox, oy));
        Assert.Equal(c.Through(c.HeelsStart, (Side)side), w.Room);
        Assert.Equal(w.Room, w.Hero.Room);
    }

    [Fact]
    public void Spikes_guards_and_pits_are_deadly()
    {
        var a = Bare(r => r.Floor[5, 6] = Floor.Spikes);
        Run(a, 60, new Controls(Dx: -1));
        Assert.Equal("SPIKED", a.Cause);
        var b = Bare(r => r.Guards.Add(new GuardDef(true, 6.5, 3.5, 3.5, 0, 0)));
        Run(b, 120, new Controls(Dx: -1));
        Assert.Equal("CAUGHT BY A GUARD", b.Cause);
        var p = Bare(r => r.Floor[5, 6] = Floor.Pit);
        Run(p, 60, new Controls(Dx: -1));
        Assert.Equal("FELL INTO THE DARK", p.Cause);
    }

    [Fact]
    public void A_death_and_the_one_on_the_move_starts_again_where_it_came_in()
    {
        var w = Bare(r => r.Floor[3, 6] = Floor.Spikes);
        w.Hero.Entry = (6.5, 6.5, 0);
        Run(w, 120, new Controls(Dx: -1));
        Assert.True(w.Hero.Dead);
        Sim.Restart(w);
        Assert.False(w.Hero.Dead);
        Assert.Equal(6.5, w.Hero.X, 3);
    }

    // ───────────────────────────── the game ─────────────────────────────

    private static GameEngine Engine() => new(HighScoreTable.Load(TempFolder())) { Silent = true };

    [Fact]
    public void The_crown_scores_and_the_next_castle_follows()
    {
        var g = Engine();
        g.Go();
        g.World.CrownTaken = true;
        g.Run(2);
        Assert.Equal(Phase.Crowned, g.Phase);
        Assert.Equal(1, g.Cleared);
        Assert.Equal(GameEngine.CrownPoints, g.Score);
        g.Run((int)(60 * GameEngine.CrownedSeconds) + 4);
        Assert.Equal(1, g.Level);
        Assert.False(g.World.CrownTaken);
    }

    [Fact]
    public void Meeting_up_is_announced_and_scores_once()
    {
        var g = Engine();
        var heard = new List<Sfx>();
        g.Silent = false;
        g.Sound += heard.Add;
        g.Go();
        var w = g.World;
        var head = w.Bodies[World.HeadIndex];
        head.Room = w.Room;
        head.X = w.Hero.X - 1;
        head.Y = w.Hero.Y;
        g.Run(40, new Controls(Dx: -1));
        Assert.True(w.Joined);
        Assert.Contains(Sfx.SayTogether, heard);
        Assert.Equal("HEAD AND HEELS TOGETHER!", g.Message);
        Assert.Equal(GameEngine.JoinPoints, g.Score);
    }

    [Fact]
    public void The_last_life_lost_is_game_over_then_the_high_scores()
    {
        var g = Engine();
        g.Go();
        g.SetLives(1);
        Sim.Die(g.World, "SPIKED");
        g.Run((int)(60 * GameEngine.DyingSeconds) + 4);
        Assert.Equal(GameState.GameOver, g.State);
    }

    [Fact]
    public void The_title_gives_way_to_the_demonstration_and_enter_starts_a_game()
    {
        var g = Engine();
        g.Idle((int)(60 * GameEngine.TitleIdleSeconds) + 5);
        Assert.Equal(GameState.Attract, g.State);
        g.Idle(60 * 10);
        g.Tick(default, start: true);
        Assert.Equal(GameState.Playing, g.State);
        Assert.Equal(GameEngine.Lives0, g.Lives);
    }

    [Fact]
    public void Paused_nothing_moves()
    {
        var g = Engine();
        g.Go();
        g.Run(10, new Controls(Dy: -1));
        g.Pause();
        double y = g.World.Hero.Y;
        g.Run(60, new Controls(Dy: -1));
        Assert.Equal(y, g.World.Hero.Y);
        g.Resume();
        g.Run(10, new Controls(Dy: -1));
        Assert.NotEqual(y, g.World.Hero.Y);
    }

    [Fact]
    public void The_keys_walk_jump_and_swap()
    {
        using var input = new InputHandler();
        foreach (var (key, check) in new (Key, Func<Controls, bool>)[]
                 {
                     (Key.Up, c => c.Dy == -1), (Key.Down, c => c.Dy == 1), (Key.Left, c => c.Dx == -1), (Key.Right, c => c.Dx == 1),
                     (Key.Space, c => c.Jump), (Key.C, c => c.Swap), (Key.Tab, c => c.Swap),
                 })
        {
            input.OnKeyDown(key);
            Assert.True(check(input.Player), key.ToString());
            input.OnKeyUp(key);
        }
        var w = Bare();
        input.OnKeyDown(Key.Left);
        double x = w.Hero.X;
        Run(w, 20, input.Player);
        Assert.True(w.Hero.X < x - 1);
    }
}
