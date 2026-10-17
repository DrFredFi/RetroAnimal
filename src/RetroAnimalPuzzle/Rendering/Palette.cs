using SkiaSharp;

namespace RetroAnimalPuzzle.Rendering;

/// <summary>Shared colour palette. Keep every hard-coded colour here so the art stays consistent.</summary>
internal static class Palette
{
    public static readonly SKColor Letterbox = SKColors.Black;

    public static readonly SKColor Background = new(0x1F, 0x4E, 0x2C);
    public static readonly SKColor BackgroundRelief = new(0x2A, 0x62, 0x38);
    public static readonly SKColor BackgroundShadow = new(0x12, 0x33, 0x1C);
    public static readonly SKColor BackgroundHighlight = new(0x3C, 0x7C, 0x4A);

    public static readonly SKColor Field = new(0x10, 0x30, 0x22);
    public static readonly SKColor FieldRelief = new(0x16, 0x3D, 0x2A);
    public static readonly SKColor FieldShadow = new(0x0A, 0x22, 0x18);
    public static readonly SKColor FieldHighlight = new(0x1E, 0x4A, 0x33);

    public static readonly SKColor FrameGreenLight = new(0x4E, 0x8F, 0x55);
    public static readonly SKColor FrameGreenDark = new(0x0C, 0x26, 0x14);

    public static readonly SKColor GoldLight = new(0xFF, 0xD2, 0x6A);
    public static readonly SKColor Gold = new(0xE0, 0x9A, 0x2E);
    public static readonly SKColor GoldDark = new(0x7A, 0x4A, 0x10);

    public static readonly SKColor NextLabel = new(0xFF, 0x6C, 0xC4);
    public static readonly SKColor HeartRed = new(0xE8, 0x1E, 0x2A);
    public static readonly SKColor HeartDark = new(0x8A, 0x0A, 0x12);
    public static readonly SKColor PlayerOneDigits = new(0xFF, 0x6E, 0x6E);
    public static readonly SKColor PlayerTwoDigits = new(0x5A, 0x8C, 0xFF);
    public static readonly SKColor TimerText = new(0xB8, 0xC4, 0xB8);
    public static readonly SKColor Outline = new(0x20, 0x10, 0x08);
}
