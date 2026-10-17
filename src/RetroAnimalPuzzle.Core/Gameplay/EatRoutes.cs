using RetroAnimalPuzzle.Core;

namespace RetroAnimalPuzzle.Gameplay;

/// <summary>The food tiles one eating animal walks over, in order.</summary>
public sealed record EaterRoute(Position Animal, IReadOnlyList<Position> Path);

/// <summary>
/// Plans how the animals of an <see cref="EatGroup"/> walk over the food they eat: every food tile goes to the
/// nearest eater (ties to the first), and each eater walks its tiles greedily, preferring a tile next to where
/// it stands, otherwise the closest one. Deterministic, so the board's phase timing and the renderer agree.
/// </summary>
public static class EatRoutes
{
    public static IReadOnlyList<EaterRoute> Plan(IReadOnlyList<Position> animals, IReadOnlyList<Position> food)
    {
        var assigned = new List<Position>[animals.Count];
        for (int i = 0; i < animals.Count; i++)
        {
            assigned[i] = [];
        }

        foreach (var cell in food)
        {
            int best = 0;
            for (int i = 1; i < animals.Count; i++)
            {
                if (Distance(animals[i], cell) < Distance(animals[best], cell))
                {
                    best = i;
                }
            }

            if (animals.Count > 0)
            {
                assigned[best].Add(cell);
            }
        }

        var routes = new EaterRoute[animals.Count];
        for (int i = 0; i < animals.Count; i++)
        {
            routes[i] = new EaterRoute(animals[i], Walk(animals[i], assigned[i]));
        }

        return routes;
    }

    private static List<Position> Walk(Position start, List<Position> cells)
    {
        var remaining = new List<Position>(cells);
        var path = new List<Position>(cells.Count);
        var current = start;
        while (remaining.Count > 0)
        {
            int best = 0;
            for (int i = 1; i < remaining.Count; i++)
            {
                if (Better(current, remaining[i], remaining[best]))
                {
                    best = i;
                }
            }

            current = remaining[best];
            path.Add(current);
            remaining.RemoveAt(best);
        }

        return path;
    }

    private static bool Better(Position from, Position candidate, Position incumbent)
    {
        int a = Distance(from, candidate);
        int b = Distance(from, incumbent);
        if (a != b)
        {
            return a < b;
        }

        return candidate.Y != incumbent.Y ? candidate.Y < incumbent.Y : candidate.X < incumbent.X;
    }

    private static int Distance(Position a, Position b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
}
