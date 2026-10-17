using RetroAnimalPuzzle.Core;
using RetroAnimalPuzzle.Gameplay;
using SkiaSharp;

namespace RetroAnimalPuzzle.Rendering;

/// <summary>
/// Draws one <see cref="PlayerBoard"/> into a playfield rectangle: locked blocks (settling and eating animations
/// derived from the board's phase timers), the falling pair (sliding, spinning on itself after a rotation),
/// the optional ghost, the NEXT queue and the incoming-food bar. It reads state only; it never changes it.
/// </summary>
internal sealed class PlayfieldRenderer : IDisposable
{
    private readonly BlockArt _art;
    private readonly SKPaint _fill = new() { IsAntialias = true };
    private readonly SKPaint _stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private readonly SKPath _heart = HeartPath();
    private float[] _offsets = [];
    private bool[] _clearing = [];
    private bool[] _eaten = [];

    public PlayfieldRenderer(BlockArt art)
    {
        _art = art;
    }

    public static float TileSize(GameRules rules, SKRect field) => field.Width / rules.Columns;

    /// <summary>Centre of a grid cell in logical screen coordinates (fractional cells allowed).</summary>
    public static SKPoint CellCenter(GameRules rules, SKRect field, float x, float y)
    {
        float tile = TileSize(rules, field);
        return new SKPoint(field.Left + (x + 0.5f) * tile, field.Top + (y - rules.HiddenRows + 0.5f) * tile);
    }

    public static SKPoint CellCenter(GameRules rules, SKRect field, Position p) => CellCenter(rules, field, p.X, p.Y);

    public void Draw(SKCanvas canvas, PlayerBoard board, SKRect field, BoardVisuals visuals, double time, bool showGhost)
    {
        var rules = board.Rules;
        var grid = board.Grid;
        float tile = TileSize(rules, field);
        EnsureBuffers(grid);
        Array.Clear(_offsets);
        Array.Clear(_clearing);
        Array.Clear(_eaten);

        if (board.Phase == BoardPhase.Settling)
        {
            float moved = (float)(board.PhaseElapsed * rules.SettleCellsPerSecond);
            foreach (var fall in board.SettlingFalls)
            {
                _offsets[fall.To.Y * grid.Columns + fall.To.X] = Math.Max(0f, fall.Distance - moved);
            }
        }

        var clear = board.Phase == BoardPhase.Eating ? board.PendingClear : null;
        if (clear is not null)
        {
            foreach (var cell in clear.Cells)
            {
                _clearing[cell.Y * grid.Columns + cell.X] = true;
            }

            // Food already reached by its eater is gone.
            foreach (var group in clear.Groups)
            {
                foreach (var route in group.Routes)
                {
                    int eaten = EatTimeline.TilesEaten(rules, route, board.PhaseElapsed);
                    for (int i = 0; i < eaten; i++)
                    {
                        _eaten[route.Path[i].Y * grid.Columns + route.Path[i].X] = true;
                    }
                }
            }
        }

        float progress = (float)board.PhaseProgress;

        canvas.Save();
        canvas.ClipRect(field);
        DrawDangerMarks(canvas, board, field, tile, time);

        for (int y = 0; y < grid.Rows; y++)
        {
            for (int x = 0; x < grid.Columns; x++)
            {
                var block = grid[x, y];
                int index = y * grid.Columns + x;
                if (block.IsEmpty || _eaten[index])
                {
                    continue;
                }

                float left = field.Left + x * tile;
                float top = field.Top + (y - rules.HiddenRows - _offsets[index]) * tile;
                if (_clearing[index])
                {
                    if (block.IsFood && IsOnRoute(clear!, new Position(x, y)))
                    {
                        // Waiting to be eaten: a nervous little shake.
                        float shake = MathF.Sin((float)time * 45f + x * 3 + y) * 1.2f;
                        _art.Draw(canvas, block, left + shake, top, tile);
                    }
                    else if (!IsEater(clear!, new Position(x, y)))
                    {
                        DrawVanishing(canvas, block, left, top, tile, progress, time);
                    }

                    continue;
                }

                _art.Draw(canvas, block, left, top, tile);
            }
        }

        if (board.Current is { } piece)
        {
            if (showGhost && board.Ghost is { } ghost && ghost.Pivot != piece.Pivot)
            {
                DrawGhost(canvas, rules, field, tile, ghost.Pivot);
                DrawGhost(canvas, rules, field, tile, ghost.Satellite);
            }

            DrawFallingPair(canvas, board, piece, field, tile, visuals);
        }

        // Eaters are drawn last so they can hop over the food they are eating.
        if (clear is not null)
        {
            foreach (var group in clear.Groups)
            {
                foreach (var route in group.Routes)
                {
                    DrawEater(canvas, rules, field, tile, grid[route.Animal], route, board.PhaseElapsed, board.PhaseDuration, time);
                }
            }
        }

        canvas.Restore();

        if (board.IsToppedOut)
        {
            _fill.Color = new SKColor(0x10, 0x10, 0x18, 150);
            canvas.DrawRect(field, _fill);
        }
    }

    /// <summary>
    /// The NEXT queue as a strip of tiles. The next pair sits at the end nearest the NEXT label and the following
    /// pairs queue up behind it; when a pair drops into the field the strip slides along to close the gap.
    /// </summary>
    /// <param name="labelOnRight">True for the left player's bar (label to the right of the tiles).</param>
    public void DrawNext(SKCanvas canvas, PlayerBoard board, SKRect bar, bool labelOnRight, BoardVisuals visuals)
    {
        float size = bar.Height;
        float slide = (1f - visuals.NextShiftProgress) * 2 * size;
        canvas.Save();
        canvas.ClipRect(bar);
        for (int pairIndex = 0; pairIndex < 3; pairIndex++)
        {
            var pair = board.NextPair(pairIndex);
            float pairLeft = labelOnRight
                ? bar.Right - (pairIndex + 1) * 2 * size - slide
                : bar.Left + pairIndex * 2 * size + slide;
            _art.Draw(canvas, pair.Pivot, pairLeft, bar.Top, size);
            _art.Draw(canvas, pair.Satellite, pairLeft + size, bar.Top, size);
        }

        canvas.Restore();
    }

    /// <summary>
    /// The bar above a field showing food about to arrive from the opponent: a heart that starts at the inner end
    /// (next to the centre panel) and travels outwards the more food is coming.
    /// </summary>
    /// <param name="outwardIsLeft">True for the left player (the heart moves left as the amount grows).</param>
    public void DrawIncomingBar(SKCanvas canvas, SKRect bar, BoardVisuals visuals, bool outwardIsLeft, double time)
    {
        float amount = visuals.IncomingShown;
        if (amount < 0.05f)
        {
            return;
        }

        const float FullBar = 24f;
        float k = Math.Clamp(amount / FullBar, 0f, 1f);
        float travel = bar.Width - bar.Height * 2;
        float x = outwardIsLeft ? bar.Right - bar.Height - k * travel : bar.Left + bar.Height + k * travel;

        // Filled part of the groove behind the heart.
        _fill.Color = new SKColor(0xFF, 0x50, 0x60, 120);
        canvas.DrawRoundRect(outwardIsLeft ? new SKRect(x, bar.Top + 2, bar.Right - 2, bar.Bottom - 2)
            : new SKRect(bar.Left + 2, bar.Top + 2, x, bar.Bottom - 2), 3, 3, _fill);

        float pulse = 1f + 0.12f * MathF.Sin((float)time * (6f + 10f * k));
        float size = (20f + 6f * k) * pulse;
        canvas.Save();
        canvas.Translate(x - size / 2, bar.MidY - size * 0.45f);
        canvas.Scale(size / 100f);
        _fill.Color = Palette.HeartDark;
        canvas.Save();
        canvas.Translate(4, 5);
        canvas.DrawPath(_heart, _fill);
        canvas.Restore();
        _fill.Color = Palette.HeartRed;
        canvas.DrawPath(_heart, _fill);
        _fill.Color = SKColors.White.WithAlpha(110);
        canvas.DrawOval(new SKRect(16, 10, 40, 28), _fill);
        canvas.Restore();
    }

    public void Dispose()
    {
        _fill.Dispose();
        _stroke.Dispose();
        _heart.Dispose();
    }

    private void EnsureBuffers(Grid grid)
    {
        int size = grid.Columns * grid.Rows;
        if (_offsets.Length != size)
        {
            _offsets = new float[size];
            _clearing = new bool[size];
            _eaten = new bool[size];
        }
    }

    /// <summary>Seconds for a falling tile to turn all the way round on itself.</summary>
    private const float FullTurnSeconds = 1.6f;

    /// <summary>
    /// Draws the falling pair at its smoothly sliding position. While falling, every tile keeps turning on its own
    /// vertical axis (fake 3-D, see <see cref="BlockArt.DrawTurning"/>); a rotation adds a quick extra half turn.
    /// </summary>
    private void DrawFallingPair(SKCanvas canvas, PlayerBoard board, Piece piece, SKRect field, float tile, BoardVisuals visuals)
    {
        var rules = board.Rules;
        float drop = (float)board.FallProgress;
        float spin = visuals.SpinProgress;
        float boost = spin < 1f ? spin * spin * (3 - 2 * spin) * 0.5f : 0f;
        float turns = (float)(visuals.Time / FullTurnSeconds) + boost;
        float angle = (turns - MathF.Floor(turns)) * MathF.Tau;

        float dx = visuals.PivotX - piece.Pivot.X;
        var pivot = CellCenter(rules, field, piece.Pivot.X + dx, piece.Pivot.Y + drop);
        var satellite = CellCenter(rules, field, piece.Satellite.X + dx, piece.Satellite.Y + drop);
        _art.DrawTurning(canvas, piece.Blocks.Pivot, pivot.X, pivot.Y, tile, angle);
        _art.DrawTurning(canvas, piece.Blocks.Satellite, satellite.X, satellite.Y, tile, angle);
    }

    private void DrawGhost(SKCanvas canvas, GameRules rules, SKRect field, float tile, Position p)
    {
        var rect = new SKRect(field.Left + p.X * tile + 4, field.Top + (p.Y - rules.HiddenRows) * tile + 4,
            field.Left + (p.X + 1) * tile - 4, field.Top + (p.Y - rules.HiddenRows + 1) * tile - 4);
        _stroke.Color = SKColors.White.WithAlpha(90);
        _stroke.StrokeWidth = 2f;
        canvas.DrawRoundRect(rect, 6, 6, _stroke);
    }

    private void DrawVanishing(SKCanvas canvas, Block block, float left, float top, float tile, float progress, double time)
    {
        float cx = left + tile / 2;
        float cy = top + tile / 2;
        float scale;
        float rotation = 0;
        if (progress < 0.45f)
        {
            scale = 1f + 0.08f * MathF.Sin((float)time * 40f);
        }
        else
        {
            float k = (progress - 0.45f) / 0.55f;
            scale = Math.Max(0f, 1f - k);
            rotation = k * 160f;
        }

        _art.DrawCentered(canvas, block, cx, cy, tile * scale, BlockPose.Normal, 255, rotation);

        if (progress < 0.3f && ((int)(time * 20) & 1) == 0)
        {
            _fill.Color = SKColors.White.WithAlpha(120);
            canvas.DrawRoundRect(new SKRect(left + 2, top + 2, left + tile - 2, top + tile - 2), 6, 6, _fill);
        }
    }

    /// <summary>
    /// An eating animal: pops up, then hops from tile to tile along its route with its mouth open, chewing
    /// (mouth snapping open and shut) on each tile it lands on.
    /// </summary>
    private void DrawEater(SKCanvas canvas, GameRules rules, SKRect field, float tile, Block animal, EaterRoute route,
        double elapsed, double duration, double time)
    {
        var (x, y, hopping, hop) = EatTimeline.Position(rules, route, elapsed);
        var center = CellCenter(rules, field, x, y);
        float pop = (float)Math.Clamp(elapsed / rules.EatStartDelay, 0, 1);
        float scale = 1f + 0.4f * pop;
        center.Y -= hop * tile * 0.35f;

        BlockPose pose;
        if (elapsed < rules.EatStartDelay || hopping)
        {
            pose = BlockPose.Chomp;
        }
        else
        {
            // Chewing: snap the jaw open and shut, with a little squash on each bite.
            bool open = ((int)(time * 14) & 1) == 0;
            pose = open ? BlockPose.Chomp : BlockPose.Normal;
            scale *= open ? 1.04f : 0.96f;
        }

        _art.DrawCentered(canvas, animal, center.X, center.Y, tile * scale, pose);
    }

    private void DrawDangerMarks(SKCanvas canvas, PlayerBoard board, SKRect field, float tile, double time)
    {
        var rules = board.Rules;
        int height = Math.Max(board.Grid.ColumnHeight(rules.SpawnColumn), board.Grid.ColumnHeight(rules.SpawnColumn + 1));
        if (board.IsToppedOut || height < rules.VisibleRows - 2)
        {
            return;
        }

        byte alpha = (byte)(70 + 60 * MathF.Sin((float)time * 8f));
        _stroke.Color = new SKColor(0xFF, 0x30, 0x30, alpha);
        _stroke.StrokeWidth = 4f;
        for (int x = rules.SpawnColumn; x <= rules.SpawnColumn + 1; x++)
        {
            float l = field.Left + x * tile + 8;
            float t = field.Top + 8;
            canvas.DrawLine(l, t, l + tile - 16, t + tile - 16, _stroke);
            canvas.DrawLine(l + tile - 16, t, l, t + tile - 16, _stroke);
        }
    }

    private static bool IsEater(ClearResult clear, Position cell)
    {
        foreach (var group in clear.Groups)
        {
            foreach (var p in group.Animals)
            {
                if (p == cell) return true;
            }
        }

        return false;
    }

    private static bool IsOnRoute(ClearResult clear, Position cell)
    {
        foreach (var group in clear.Groups)
        {
            foreach (var route in group.Routes)
            {
                foreach (var p in route.Path)
                {
                    if (p == cell) return true;
                }
            }
        }

        return false;
    }

    /// <summary>Heart in a 100×90 box (same shape as the arena hearts).</summary>
    private static SKPath HeartPath()
    {
        var path = new SKPathBuilder();
        path.MoveTo(50f, 88f);
        path.CubicTo(20f, 66f, 0f, 48f, 2f, 26f);
        path.CubicTo(4f, 8f, 18f, 0f, 30f, 0f);
        path.CubicTo(40f, 0f, 47f, 6f, 50f, 14f);
        path.CubicTo(53f, 6f, 60f, 0f, 70f, 0f);
        path.CubicTo(82f, 0f, 96f, 8f, 98f, 26f);
        path.CubicTo(100f, 48f, 80f, 66f, 50f, 88f);
        path.Close();
        return path.Detach();
    }
}
