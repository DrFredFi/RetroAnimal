using RetroAnimalPuzzle.Application;
using RetroAnimalPuzzle.Audio;
using RetroAnimalPuzzle.Core;
using RetroAnimalPuzzle.Input;
using RetroAnimalPuzzle.Rendering;
using SkiaSharp;

namespace RetroAnimalPuzzle.UI;

/// <summary>Attract screen: bouncing title, a parade of animals chasing their food, "PRESS ENTER".</summary>
internal sealed class TitleScreen : IScreen
{
    private readonly GameContext _context;
    private double _time;

    public TitleScreen(GameContext context)
    {
        _context = context;
    }

    public void OnEnter() => _context.Audio.PlayMusic(MusicTrack.Menu);

    public void Update(double deltaSeconds, in InputState input)
    {
        _time += deltaSeconds;
        if (input.WasPressed(GameAction.Confirm) || input.WasPressed(GameAction.HardDrop))
        {
            _context.Audio.Play(Sound.MenuSelect);
            _context.Screens.Replace(new MainMenuScreen(_context));
        }
        else if (input.WasPressed(GameAction.Help))
        {
            _context.Screens.Push(new HelpScreen(_context));
        }
    }

    public void Render(SKCanvas canvas)
    {
        var ui = _context.Ui;
        float t = (float)_time;
        _context.Arena.DrawBackground(canvas);
        TitleArt.DrawLogo(canvas, ui, 400, 150, t);

        // Parade: each animal chases its favourite food across the screen.
        var rules = _context.Rules;
        float spacing = 800f / rules.Animals.Count;
        for (int i = 0; i < rules.Animals.Count; i++)
        {
            float x = ((t * 70f + i * spacing) % (800f + 120f)) - 60f;
            float hop = MathF.Abs(MathF.Sin(t * 6f + i)) * 16f;
            var animal = rules.Animals[i];
            _context.Art.DrawCentered(canvas, Block.Of(animal), x, 330 - hop, 54,
                (int)(t * 4 + i) % 3 == 0 ? BlockPose.Chomp : BlockPose.Normal);
            _context.Art.DrawCentered(canvas, Block.Of(rules.FoodFor(animal)), x + 56, 336, 40);
        }

        _context.Mascot.Draw(canvas, new SKRect(330, 380, 470, 520), MascotMood.Cheer, _time);

        if ((int)(_time * 2) % 2 == 0)
        {
            ui.OutlinedText(canvas, "PRESS ENTER", 400, 552, 30, new SKColor(0xFF, 0xE4, 0x5C), Palette.Outline, UiFont.Heading, 4);
        }

        ui.Text(canvas, "F1: How to Play", 400, 584, 14, Palette.TimerText, UiFont.Body);
    }
}

/// <summary>The game logo, shared by the title and main menu screens.</summary>
internal static class TitleArt
{
    public static void DrawLogo(SKCanvas canvas, UiRenderer ui, float centerX, float baseline, float time, float scale = 1f)
    {
        string[] words = ["RETRO", "ANIMAL", "PUZZLE"];
        SKColor[] tops = [new(0xFF, 0xF0, 0x80), new(0xFF, 0xB0, 0xE0), new(0x9C, 0xF0, 0xFF)];
        SKColor[] bottoms = [new(0xFF, 0x9A, 0x1A), new(0xE8, 0x3C, 0x8C), new(0x2E, 0x8C, 0xF0)];
        float size = 56 * scale;
        float y = baseline - size * 1.05f;
        ui.GradientText(canvas, words[0], centerX, y - size * 0.95f + MathF.Sin(time * 2.2f) * 3 * scale, size * 0.7f, tops[0], bottoms[0], Palette.Outline, 5 * scale);
        float x = centerX - (ui.Measure(words[1], size, UiFont.Heading) + ui.Measure(" ", size, UiFont.Heading) + ui.Measure(words[2], size, UiFont.Heading)) / 2f;
        for (int w = 1; w <= 2; w++)
        {
            float width = ui.Measure(words[w], size, UiFont.Heading);
            float bob = MathF.Sin(time * 3f + w) * 4f * scale;
            ui.GradientText(canvas, words[w], x + width / 2f, y + bob + size * 0.35f, size, tops[w], bottoms[w], Palette.Outline, 6 * scale);
            x += width + ui.Measure(" ", size, UiFont.Heading);
        }
    }
}
