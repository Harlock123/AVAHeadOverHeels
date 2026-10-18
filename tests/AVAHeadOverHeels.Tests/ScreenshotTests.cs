using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVAHeadOverHeels.Engine;
using AVAHeadOverHeels.Views;
using Controls = AVAHeadOverHeels.Engine.Controls;

namespace AVAHeadOverHeels.Tests;

/// <summary>
/// Draws the game in a real (headless) window at the moments worth showing. Set HOH_SHOTS to a folder to
/// save them as PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests
{
    private static (Window W, GameCanvas Canvas, GameEngine G) Open()
    {
        var g = new GameEngine(HighScoreTable.Load(Path.Combine(Path.GetTempPath(), "avaheadoverheels-ui-" + Guid.NewGuid().ToString("N")))) { Silent = true };
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.InvalidateVisual();
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("HOH_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVAHeadOverHeels-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    /// <summary>Lets the computer pilot play until something worth a picture is on screen.</summary>
    private static bool PlayUntil(GameEngine g, GameCanvas c, Func<GameEngine, bool> done, int maxSeconds = 150)
    {
        for (int t = 0; t < 60 * maxSeconds; t++)
        {
            if (done(g)) { c.Advance(0.016); return true; }
            g.Tick(g.Pilot.Decide(g));
            if (t % 3 == 0) c.Advance(1 / 20.0);
        }
        return false;
    }

    [AvaloniaFact]
    public void The_title_and_the_high_scores()
    {
        var (w, c, g) = Open();
        g.Idle(30);
        c.Advance(2.1);
        Shot(w, c, "title");
        g.HighScores.Insert("SPY", 31800, 5);
        g.HighScores.Insert("AVA", 9400, 1);
        g.Idle((int)(60 * (GameEngine.TitleIdleSeconds + GameEngine.AttractSeconds)) + 10);
        for (int i = 0; i < 60 * 60 && g.State != GameState.HighScores; i++) g.Idle(1);
        Shot(w, c, "high-scores");
    }

    private static void Play(int level, string name, Func<GameEngine, bool> moment, int seconds = 150)
    {
        var (w, c, g) = Open();
        g.Go(level);
        g.SetLives(20);
        g.Pilot.Reset();
        Assert.True(PlayUntil(g, c, moment, seconds), $"never saw the moment for {name}");
        Shot(w, c, name);
    }

    [AvaloniaFact]
    public void Heels_sets_out() => Play(0, "heels", g => g.World.Clock > 1.5 && !g.World.Hero.Airborne);

    [AvaloniaFact]
    public void Head_marooned()
    {
        var (w, c, g) = Open();
        g.Go(1);
        g.Tick(new Controls(Swap: true));
        for (int i = 0; i < 30; i++) { g.Tick(default); if (i % 3 == 0) c.Advance(0.05); }
        Assert.Equal(World.HeadIndex, g.World.Active);
        Shot(w, c, "island");
    }

    [AvaloniaFact]
    public void Heels_leaps_to_the_island() => Play(1, "leap", g => !g.World.Joined && g.World.Room == g.World.Castle.HeadStart && g.World.Hero.Airborne && g.World.Hero.Vz < 2);

    [AvaloniaFact]
    public void Together() => Play(2, "together", g => g.World.Joined && g.Message != null && g.World.Hero.Airborne == false && g.World.Clock > 1);

    [AvaloniaFact]
    public void Spiked_balls() => Play(3, "balls", g => g.World.Here.Balls.Count >= 3 && g.World.Hero.X is > 2 and < 7 && g.World.Hero.Y is > 2 and < 7);

    [AvaloniaFact]
    public void The_crown_in_sight() => Play(4, "crown", g => g.World.Joined && g.World.Room == g.World.Castle.CrownRoom && g.World.Hero.Airborne && g.World.Hero.Z > 1.2, 200);

    [AvaloniaFact]
    public void Crowned() => Play(0, "crowned", g => g.Phase == Phase.Crowned && g.PhaseTime > 0.5);

    [AvaloniaFact]
    public void Game_over()
    {
        var (w, c, g) = Open();
        g.Go();
        g.SetLives(1);
        Sim.Die(g.World, "SPIKED");
        for (int i = 0; i < 600 && !(g.State == GameState.GameOver && g.StateTime > 1.5); i++) { g.Tick(default); if (i % 3 == 0) c.Advance(0.05); }
        Shot(w, c, "game-over");
    }
}
