using SkiaSharp;

namespace RetroAnimalPuzzle.Rendering;

/// <summary>
/// Positions of every arena element in 800×600 logical coordinates, modelled on the classic
/// two-field layout: NEXT bars on top, a field on each side and a central panel with hearts,
/// mascot, stage plate, scores and timer.
/// </summary>
internal static class ArenaLayout
{
    public const float TileSize = 40f;
    public const int Columns = 6;
    public const int VisibleRows = 11;
    public const float PreviewTileSize = 34f;
    public const int PreviewTiles = 5;

    public static readonly SKRect LeftField = FieldAt(24f);
    public static readonly SKRect RightField = FieldAt(800f - 24f - Columns * TileSize);

    public static readonly SKRect LeftNext = new(14f, 12f, 14f + PreviewTiles * PreviewTileSize, 12f + PreviewTileSize);
    public static readonly SKPoint LeftNextLabel = new(LeftNext.Right + 40f, 38f);
    public static readonly SKRect RightNext = new(786f - PreviewTiles * PreviewTileSize, 12f, 786f, 12f + PreviewTileSize);
    public static readonly SKPoint RightNextLabel = new(RightNext.Left - 40f, 38f);

    public static readonly SKRect LeftHeart = new(316f, 58f, 382f, 120f);
    public static readonly SKRect RightHeart = new(418f, 58f, 484f, 120f);

    public static readonly SKRect MascotFrame = new(318f, 140f, 482f, 326f);
    public static readonly SKRect StagePlate = new(332f, 334f, 468f, 368f);
    public static readonly SKRect PlayerOneScore = new(300f, 388f, 482f, 424f);
    public static readonly SKRect PlayerTwoScore = new(318f, 432f, 500f, 468f);
    public static readonly SKPoint Timer = new(400f, 506f);

    /// <summary>Bars above each field where a heart shows how much food the opponent is sending.</summary>
    public static readonly SKRect LeftIncomingBar = new(LeftField.Left, 94f, LeftField.Right, 106f);
    public static readonly SKRect RightIncomingBar = new(RightField.Left, 94f, RightField.Right, 106f);

    /// <summary>Where the round-win stars sit, beside each heart.</summary>
    public static readonly SKPoint LeftWins = new(298f, 72f);
    public static readonly SKPoint RightWins = new(502f, 72f);

    private static SKRect FieldAt(float left) =>
        new(left, 132f, left + Columns * TileSize, 132f + VisibleRows * TileSize);
}
