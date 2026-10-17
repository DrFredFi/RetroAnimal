namespace RetroAnimalPuzzle.Gameplay;

public enum CpuDifficulty
{
    Easy,
    Normal,
    Hard,
}

/// <summary>
/// How a computer opponent plays: how quickly it reacts and presses keys, how much random noise it adds
/// to its evaluation, how often it simply blunders, and how likely it is to hold Down once it is in position.
/// </summary>
public sealed record CpuSkill(
    double ReactionDelay,
    double ActionInterval,
    double Noise,
    double BlunderChance,
    double SoftDropChance)
{
    public static CpuSkill For(CpuDifficulty difficulty, int stage)
    {
        var baseSkill = difficulty switch
        {
            CpuDifficulty.Easy => new CpuSkill(0.65, 0.22, 40, 0.2, 0.55),
            CpuDifficulty.Hard => new CpuSkill(0.16, 0.07, 4, 0.0, 1.0),
            _ => new CpuSkill(0.40, 0.13, 15, 0.06, 0.9),
        };

        int steps = Math.Max(0, stage - 1);
        return baseSkill with
        {
            ReactionDelay = baseSkill.ReactionDelay * Math.Pow(0.88, steps),
            ActionInterval = Math.Max(0.04, baseSkill.ActionInterval * Math.Pow(0.9, steps)),
            Noise = baseSkill.Noise * Math.Pow(0.8, steps),
            BlunderChance = baseSkill.BlunderChance * Math.Pow(0.75, steps),
            SoftDropChance = Math.Min(1.0, baseSkill.SoftDropChance + 0.1 * steps),
        };
    }
}
