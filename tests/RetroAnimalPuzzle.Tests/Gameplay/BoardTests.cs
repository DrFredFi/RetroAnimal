using RetroAnimalPuzzle.Core;
using RetroAnimalPuzzle.Gameplay;
using RetroAnimalPuzzle.Input;

namespace RetroAnimalPuzzle.Tests.Gameplay;

public class BoardTests
{
    private const double Tick = 1.0 / 60;
    // These tests use hard drop as a quick way to land pieces; the default rules disable it.
    private static readonly GameRules Rules = GameRules.Default with { HardDropEnabled = true };
    private static readonly InputState HardDrop =
        new(ActionSet.Empty, ActionSet.Empty.With(GameAction.HardDrop), ActionSet.Empty);

    private static void RunUntil(PlayerBoard board, Func<PlayerBoard, bool> condition, InputState input = default, int maxTicks = 5000)
    {
        for (int i = 0; i < maxTicks; i++)
        {
            if (condition(board))
            {
                return;
            }

            board.Update(Tick, input);
        }

        Assert.Fail($"Condition not reached; board is in phase {board.Phase}.");
    }

    [Fact]
    public void NormalSpawn_DoesNotEndTheGame()
    {
        var board = new PlayerBoard(Rules, seed: 1);

        board.Update(Tick, InputState.None);

        Assert.Equal(BoardPhase.Falling, board.Phase);
        Assert.NotNull(board.Current);
        Assert.False(board.IsToppedOut);
    }

    [Fact]
    public void GameEnds_WhenNewPieceCannotSpawn()
    {
        var board = new PlayerBoard(Rules, seed: 1);
        board.Grid.Place(new Position(Rules.SpawnColumn, Rules.HiddenRows), Block.Of(AnimalType.Dog));

        board.Update(Tick, InputState.None);

        Assert.True(board.IsToppedOut);
        Assert.Contains(board.Events, e => e.Type == GameEventType.ToppedOut);
    }

    [Fact]
    public void GameEnds_WhenBlocksRemainAboveTheVisibleField()
    {
        var board = new PlayerBoard(Rules, seed: 3);
        board.Generator.Script(new PiecePair(Block.Of(AnimalType.Dog), Block.Of(AnimalType.Panda)));
        // Fill the spawn column right up to (but excluding) the spawn cell.
        for (int y = Rules.HiddenRows + 1; y < Rules.TotalRows; y++)
        {
            board.Grid.Place(new Position(Rules.SpawnColumn, y), Block.Of(AnimalType.Rabbit));
        }

        board.Update(Tick, InputState.None);
        Assert.Equal(BoardPhase.Falling, board.Phase);

        // Stand the pair upright: its upper block now pokes into the hidden rows, and it cannot fall.
        var rotate = new InputState(ActionSet.Empty, ActionSet.Empty.With(GameAction.RotateCounterClockwise), ActionSet.Empty);
        board.Update(Tick, rotate);
        Assert.Equal(Orientation.Up, board.Current!.Value.Orientation);

        board.Update(Tick, HardDrop);
        RunUntil(board, b => b.Phase is BoardPhase.ToppedOut or BoardPhase.Falling);

        Assert.True(board.IsToppedOut);
    }

    [Fact]
    public void LandingPair_LocksIntoTheGrid()
    {
        var board = new PlayerBoard(Rules, seed: 2);
        board.Generator.Script(new PiecePair(Block.Of(AnimalType.Dog), Block.Of(AnimalType.Panda)));
        board.Update(Tick, InputState.None);

        board.Update(Tick, HardDrop);
        RunUntil(board, b => b.Phase == BoardPhase.Falling);

        int bottom = Rules.TotalRows - 1;
        Assert.Equal(Block.Of(AnimalType.Dog), board.Grid[Rules.SpawnColumn, bottom]);
        Assert.Equal(Block.Of(AnimalType.Panda), board.Grid[Rules.SpawnColumn + 1, bottom]);
    }

    [Fact]
    public void FoodLandingNextToAnimal_IsEaten()
    {
        var board = new PlayerBoard(Rules, seed: 4);
        int bottom = Rules.TotalRows - 1;
        board.Grid.Place(new Position(Rules.SpawnColumn - 1, bottom), Block.Of(AnimalType.Monkey));
        board.Generator.Script(new PiecePair(Block.Of(FoodType.Banana), Block.Of(FoodType.Bamboo)));
        board.Update(Tick, InputState.None);

        board.Update(Tick, HardDrop);

        Assert.Equal(BoardPhase.Eating, board.Phase);
        Assert.Contains(board.Events, e => e.Type == GameEventType.Eat && e.Chain == 1);
        RunUntil(board, b => b.Phase == BoardPhase.Falling);
        Assert.True(board.Grid[Rules.SpawnColumn - 1, bottom].IsEmpty);
        Assert.True(board.Grid[Rules.SpawnColumn, bottom].IsEmpty);
        Assert.Equal(Block.Of(FoodType.Bamboo), board.Grid[Rules.SpawnColumn + 1, bottom]);
        Assert.True(board.Score.Score > 0);
    }

    [Fact]
    public void SplitPair_FallsApart()
    {
        var board = new PlayerBoard(Rules, seed: 5);
        int bottom = Rules.TotalRows - 1;
        board.Grid.Place(new Position(Rules.SpawnColumn, bottom), Block.Of(AnimalType.Dog));
        board.Grid.Place(new Position(Rules.SpawnColumn, bottom - 1), Block.Of(AnimalType.Dog));
        board.Generator.Script(new PiecePair(Block.Of(FoodType.Carrot), Block.Of(FoodType.Carrot)));
        board.Update(Tick, InputState.None);

        board.Update(Tick, HardDrop);

        Assert.Equal(BoardPhase.Settling, board.Phase);
        RunUntil(board, b => b.Phase == BoardPhase.Falling);
        Assert.Equal(Block.Of(FoodType.Carrot), board.Grid[Rules.SpawnColumn, bottom - 2]);
        Assert.Equal(Block.Of(FoodType.Carrot), board.Grid[Rules.SpawnColumn + 1, bottom]);
    }

    [Fact]
    public void Garbage_IsDroppedBeforeTheNextPiece()
    {
        var board = new PlayerBoard(Rules, seed: 6);
        board.Update(Tick, InputState.None);
        board.AddIncomingGarbage(5);

        board.Update(Tick, HardDrop);
        RunUntil(board, b => b.Phase == BoardPhase.Falling);

        Assert.Equal(0, board.IncomingGarbage);
        Assert.True(board.Grid.CountBlocks() >= 2); // the pair, plus garbage unless it was eaten
    }

    [Fact]
    public void Board_AlwaysTerminates_WithRandomPlay()
    {
        var board = new PlayerBoard(Rules, seed: 42);
        var random = new Random(7);
        GameAction[] actions = [GameAction.MoveLeft, GameAction.MoveRight, GameAction.RotateClockwise, GameAction.HardDrop];

        for (int i = 0; i < 200_000 && !board.IsToppedOut; i++)
        {
            var input = random.Next(4) == 0
                ? new InputState(ActionSet.Empty, ActionSet.Empty.With(actions[random.Next(actions.Length)]), ActionSet.Empty)
                : InputState.None;
            board.Update(Tick, input);
        }

        Assert.True(board.IsToppedOut);
        Assert.True(board.Score.Score >= 0);
    }
}
