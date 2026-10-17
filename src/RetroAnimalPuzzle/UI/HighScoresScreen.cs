using System.Globalization;
using RetroAnimalPuzzle.Application;
using RetroAnimalPuzzle.Audio;
using RetroAnimalPuzzle.Input;
using RetroAnimalPuzzle.Persistence;
using RetroAnimalPuzzle.Rendering;
using SkiaSharp;

namespace RetroAnimalPuzzle.UI;

/// <summary>Top-10 tables for both modes, side by side.</summary>
internal sealed class HighScoresScreen : IScreen
{
    private readonly GameContext _context;
    private double _time;

    public HighScoresScreen(GameContext context)
    {
        _context = context;
    }

    public void Update(double deltaSeconds, in InputState input)
    {
        _time += deltaSeconds;
        if (input.WasPressed(GameAction.Back) || input.WasPressed(GameAction.Confirm))
        {
            _context.Audio.Play(Sound.MenuBack);
            _context.Screens.Pop();
        }
    }

    public void Render(SKCanvas canvas)
    {
        var ui = _context.Ui;
        _context.Arena.DrawBackground(canvas);
        ui.Panel(canvas, new SKRect(30, 30, 770, 570));
        float pulse = 1f + 0.03f * MathF.Sin((float)_time * 4f);
        ui.OutlinedText(canvas, "HIGH SCORES", 400, 88, 38 * pulse, new SKColor(0xFF, 0xE4, 0x5C), Palette.Outline, UiFont.Heading, 4);

        Table(canvas, ui, GameMode.Endless, "ENDLESS", "LV", 60);
        Table(canvas, ui, GameMode.Versus, "VERSUS", "ST", 410);
        ui.Text(canvas, "Enter / Esc: back", 400, 552, 15, Palette.TimerText, UiFont.Body);
    }

    private void Table(SKCanvas canvas, UiRenderer ui, GameMode mode, string title, string levelLabel, float left)
    {
        var entries = _context.Saves.Data.For(mode).Take(SaveData.MaxEntriesPerMode).ToList();
        float right = left + 330;
        ui.OutlinedText(canvas, title, (left + right) / 2, 140, 24, SKColors.White, Palette.Outline, UiFont.Heading, 3);
        ui.Text(canvas, "#", left + 10, 172, 14, Palette.TimerText, UiFont.Heading, SKTextAlign.Left);
        ui.Text(canvas, "SCORE", left + 150, 172, 14, Palette.TimerText, UiFont.Heading, SKTextAlign.Right);
        ui.Text(canvas, levelLabel, left + 200, 172, 14, Palette.TimerText, UiFont.Heading, SKTextAlign.Right);
        ui.Text(canvas, "DATE", right - 10, 172, 14, Palette.TimerText, UiFont.Heading, SKTextAlign.Right);

        if (entries.Count == 0)
        {
            ui.Text(canvas, "No scores yet - go play!", (left + right) / 2, 240, 18, Palette.TimerText, UiFont.Playful);
            return;
        }

        float y = 204;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var color = i == 0 ? new SKColor(0xFF, 0xE4, 0x5C) : SKColors.White;
            ui.Text(canvas, $"{i + 1}.", left + 10, y, 18, color, UiFont.Digits, SKTextAlign.Left);
            ui.Text(canvas, e.Score.ToString(CultureInfo.InvariantCulture), left + 150, y, 18, color, UiFont.Digits, SKTextAlign.Right);
            ui.Text(canvas, e.Level.ToString(CultureInfo.InvariantCulture), left + 200, y, 18, color, UiFont.Digits, SKTextAlign.Right);
            ui.Text(canvas, e.Date.LocalDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), right - 10, y, 16, Palette.TimerText, UiFont.Digits, SKTextAlign.Right);
            y += 32;
        }
    }
}
