using SkiaSharp;

namespace RetroAnimalPuzzle.Rendering;

/// <summary>
/// Typefaces used by the game. Loaded from the Windows font folder when present (exact match, no
/// silent substitution), otherwise from the system font manager by family name.
/// </summary>
internal sealed class Fonts : IDisposable
{
    public Fonts()
    {
        Heading = Load("ariblk.ttf", "Arial Black", SKFontStyle.Bold);
        Playful = Load("comicbd.ttf", "Comic Sans MS", SKFontStyle.Bold);
        Digits = Load("consolab.ttf", "Consolas", SKFontStyle.Bold);
        Body = Load("arialbd.ttf", "Arial", SKFontStyle.Bold);
    }

    /// <summary>Chunky display face for titles, plates and announcements.</summary>
    public SKTypeface Heading { get; }

    /// <summary>Friendly face for menus and help text.</summary>
    public SKTypeface Playful { get; }

    public SKTypeface Digits { get; }

    public SKTypeface Body { get; }

    public void Dispose()
    {
        Heading.Dispose();
        Playful.Dispose();
        Digits.Dispose();
        Body.Dispose();
    }

    private static SKTypeface Load(string file, string family, SKFontStyle style)
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), file);
        if (File.Exists(path) && SKTypeface.FromFile(path) is { } typeface)
        {
            return typeface;
        }

        return SKTypeface.FromFamilyName(family, style) ?? SKTypeface.Default;
    }
}
