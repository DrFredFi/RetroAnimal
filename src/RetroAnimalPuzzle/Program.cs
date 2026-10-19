using RetroAnimalPuzzle.Application;
using RetroAnimalPuzzle.Audio;
using RetroAnimalPuzzle.Input;
using RetroAnimalPuzzle.Persistence;
using RetroAnimalPuzzle.Rendering;
using RetroAnimalPuzzle.UI;
using SkiaSharp;

namespace RetroAnimalPuzzle;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args is ["--snapshot", var directory])
        {
            return RenderSnapshots(directory);
        }

        if (args is ["--gallery", var galleryPath])
        {
            return RenderGallery(galleryPath);
        }

        if (args is ["--bench", var benchPath])
        {
            return Benchmark(benchPath);
        }

        if (args is ["--icon", var iconPath])
        {
            return RenderIcon(iconPath);
        }

        ApplicationConfiguration.Initialize();
        using var game = new GameApplication();
        game.Run();
        return 0;
    }

    /// <summary>
    /// Developer aid: renders every screen off-screen to PNG files (no window, no audio, no saved data touched),
    /// including a simulated versus match and an endless game in progress.
    /// </summary>
    private static int RenderSnapshots(string directory)
    {
        Directory.CreateDirectory(directory);
        string sandbox = Path.Combine(Path.GetTempPath(), "rap-snapshot-" + Guid.NewGuid().ToString("N"));
        var settings = new SettingsService(sandbox);
        settings.Load();
        var saves = new SaveService(sandbox);
        saves.Load();
        var input = new InputManager(KeyboardBindings.CreateDefault());
        using var audio = new NullAudioService();
        using var context = new GameContext(input, audio, settings, saves, _ => { }, () => { });
        using var renderer = new GameRenderer();

        void Shot(string name, IScreen screen, double seconds, Func<int, InputState>? inputs = null)
        {
            using var manager = new ScreenManager();
            manager.Start(screen);
            int ticks = (int)(seconds * 60);
            for (int i = 0; i < ticks; i++)
            {
                manager.Update(1.0 / 60, inputs?.Invoke(i) ?? InputState.None);
            }

            using var surface = SKSurface.Create(new SKImageInfo(800, 600));
            renderer.Render(surface.Canvas, new SKSizeI(800, 600), manager, 0);
            Save(surface, Path.Combine(directory, name + ".png"));
        }

        var random = new Random(3);
        GameAction[] moves = [GameAction.MoveLeft, GameAction.MoveRight, GameAction.RotateClockwise];
        InputState RandomPlay(int tick) => tick % 9 == 0
            ? new InputState(ActionSet.Empty.With(GameAction.SoftDrop), ActionSet.Empty.With(moves[random.Next(moves.Length)]), ActionSet.Empty)
            : new InputState(ActionSet.Empty.With(GameAction.SoftDrop), ActionSet.Empty, ActionSet.Empty);

        Shot("01-title", new TitleScreen(context), 1.0);
        Shot("02-menu", new MainMenuScreen(context), 0.5);
        Shot("03-endless", new EndlessScreen(context), 25, RandomPlay);
        Shot("04-versus", new VersusScreen(context), 40, RandomPlay);
        Shot("05-help", new HelpScreen(context), 0.5);
        Shot("06-options", new OptionsScreen(context), 0.5);
        Shot("07-highscores", new HighScoresScreen(context), 0.5);
        var pause = new InputState(ActionSet.Empty, ActionSet.Empty.With(GameAction.Pause), ActionSet.Empty);
        Shot("08-pause", new EndlessScreen(context), 3, t => t == 120 ? pause : InputState.None);
        InputState BothPlay(int tick)
        {
            var right = RandomPlay(tick);
            var left = (tick + 4) % 11 == 0 ? ActionSet.Empty.With(GameAction.LeftPlayerMoveLeft) : ActionSet.Empty;
            return right with
            {
                Held = right.Held.With(GameAction.LeftPlayerSoftDrop),
                Pressed = right.Pressed.Union((tick + 4) % 23 == 0 ? left.With(GameAction.LeftPlayerRotateClockwise) : left),
            };
        }

        Shot("09-two-players", new VersusScreen(context, twoPlayers: true), 30, BothPlay);
        Shot("10-two-players-ready", new VersusScreen(context, twoPlayers: true), 0.5);
        Shot("11-controls", new ControlsScreen(context), 0.5);
        Shot("11b-controller", new GamepadControlsScreen(context), 0.5);
        var helpPage = new InputState(ActionSet.Empty, ActionSet.Empty.With(GameAction.MenuRight), ActionSet.Empty);
        Shot("12-help-controls", new HelpScreen(context), 0.5, t => t is 20 or 24 ? helpPage : InputState.None);
        Shot("13-help-versus", new HelpScreen(context), 0.5, t => t == 20 ? helpPage : InputState.None);

        try
        {
            Directory.Delete(sandbox, recursive: true);
        }
        catch (IOException)
        {
        }

        return 0;
    }

    /// <summary>Developer aid: average update and render cost per frame for each main screen, written to a text file.</summary>
    private static int Benchmark(string path)
    {
        string sandbox = Path.Combine(Path.GetTempPath(), "rap-bench-" + Guid.NewGuid().ToString("N"));
        var settings = new SettingsService(sandbox);
        settings.Load();
        var saves = new SaveService(sandbox);
        saves.Load();
        var input = new InputManager(KeyboardBindings.CreateDefault());
        using var audio = new NullAudioService();
        using var context = new GameContext(input, audio, settings, saves, _ => { }, () => { });
        using var renderer = new GameRenderer();
        using var surface = SKSurface.Create(new SKImageInfo(1024, 768));
        var lines = new List<string>();

        void Measure(string name, Func<IScreen> create)
        {
            using var manager = new ScreenManager();
            manager.Start(create());
            for (int i = 0; i < 60; i++) manager.Update(1.0 / 60, InputState.None);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            double update = 0;
            const int frames = 240;
            for (int i = 0; i < frames; i++)
            {
                var t0 = watch.Elapsed.TotalMilliseconds;
                manager.Update(1.0 / 60, InputState.None);
                update += watch.Elapsed.TotalMilliseconds - t0;
                renderer.Render(surface.Canvas, new SKSizeI(1024, 768), manager, 0);
                surface.Flush();
            }

            double total = watch.Elapsed.TotalMilliseconds;
            lines.Add($"{name,-10} total {total / frames:0.00} ms/frame (update {update / frames:0.000} ms)");
        }

        Measure("title", () => new TitleScreen(context));
        Measure("menu", () => new MainMenuScreen(context));
        Measure("endless", () => new EndlessScreen(context));
        Measure("versus", () => new VersusScreen(context));

        // CPU strength: each difficulty plays alone for three simulated minutes.
        foreach (var difficulty in Enum.GetValues<Gameplay.CpuDifficulty>())
        {
            var board = new Gameplay.PlayerBoard(context.Rules, 1234);
            var cpu = new Gameplay.CpuController(context.Rules, Gameplay.CpuSkill.For(difficulty, 1), 99);
            int ticks = 0;
            for (; ticks < 60 * 180 && !board.IsToppedOut; ticks++)
            {
                board.SpeedLevel = 1 + ticks / (60 * 25);
                board.Update(1.0 / 60, cpu.Think(board, 1.0 / 60));
            }

            lines.Add($"cpu {difficulty,-7} survived {ticks / 60}s, pieces {board.PieceSerial}, food {board.Score.FoodCleared}, " +
                $"best chain {board.Score.BestChain}, score {board.Score.Score}");
        }

        File.WriteAllLines(path, lines);
        return 0;
    }

    /// <summary>Developer aid: every block in both poses at two sizes, to review the art.</summary>
    private static int RenderGallery(string path)
    {
        Core.Block[] blocks =
        [
            .. Enum.GetValues<Core.AnimalType>().Select(Core.Block.Of),
            .. Enum.GetValues<Core.FoodType>().Select(Core.Block.Of),
            Core.Block.Coin,
        ];
        using var surface = SKSurface.Create(new SKImageInfo(blocks.Length * 90 + 20, 520));
        using var art = new BlockArt();
        surface.Canvas.Clear(Palette.Field);
        for (int i = 0; i < blocks.Length; i++)
        {
            float x = 10 + i * 90;
            art.Draw(surface.Canvas, blocks[i], x + 25, 20, 40);
            art.Draw(surface.Canvas, blocks[i], x, 80, 80);
            art.Draw(surface.Canvas, blocks[i], x, 180, 80, BlockPose.Chomp);
            art.Draw(surface.Canvas, blocks[i], x + 25, 275, 40, BlockPose.Chomp);

            // Falling-tile turn: a rabbit and a bamboo tile at increasing angles (0° .. 360°), like rotating.png.
            float angle = i / (float)blocks.Length * MathF.Tau;
            art.DrawTurning(surface.Canvas, Core.Block.Of(Core.AnimalType.Rabbit), x + 45, 370, 70, angle);
            art.DrawTurning(surface.Canvas, Core.Block.Of(Core.FoodType.Bamboo), x + 45, 460, 70, angle);
        }

        return Save(surface, path);
    }

    /// <summary>
    /// Developer aid: writes the application icon (the bamboo tile) as a multi-size .ico with PNG-compressed
    /// images. The result is checked in as <c>Assets/app.ico</c>; re-run this only if the tile art changes.
    /// </summary>
    private static int RenderIcon(string path)
    {
        int[] sizes = [16, 24, 32, 48, 64, 128, 256];
        using var art = new BlockArt();
        var images = new List<byte[]>();
        foreach (int size in sizes)
        {
            using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
            surface.Canvas.Clear(SKColors.Transparent);
            art.Draw(surface.Canvas, Core.Block.Of(Core.FoodType.Bamboo), 0, 0, size);
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            images.Add(data.ToArray());
        }

        // ICO layout: 6-byte header, one 16-byte directory entry per image, then the PNG payloads.
        using var file = File.Create(path);
        using var writer = new BinaryWriter(file);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)sizes.Length);
        int offset = 6 + 16 * sizes.Length;
        for (int i = 0; i < sizes.Length; i++)
        {
            writer.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
            writer.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write(images[i].Length);
            writer.Write(offset);
            offset += images[i].Length;
        }

        foreach (var png in images)
        {
            writer.Write(png);
        }

        return 0;
    }

    private static int Save(SKSurface surface, string path)
    {
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(path);
        data.SaveTo(file);
        return 0;
    }
}
