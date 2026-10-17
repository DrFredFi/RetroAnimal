using RetroAnimalPuzzle.Core;
using RetroAnimalPuzzle.Input;

namespace RetroAnimalPuzzle.Gameplay;

/// <summary>Phases of one player's board. Transitions are explicit and happen only inside <see cref="PlayerBoard.Update"/>.</summary>
public enum BoardPhase
{
    /// <summary>Short pause before the next pair appears.</summary>
    Spawning,

    /// <summary>A pair is falling under player (or CPU) control.</summary>
    Falling,

    /// <summary>Animals are eating; <see cref="PlayerBoard.PendingClear"/> holds what will disappear.</summary>
    Eating,

    /// <summary>Unsupported blocks are falling into place; <see cref="PlayerBoard.SettlingFalls"/> drives the animation.</summary>
    Settling,

    /// <summary>The board is full. Terminal.</summary>
    ToppedOut,
}

/// <summary>
/// One player's playfield and its deterministic state machine:
/// <c>Spawning → Falling → (lock) → Settling ⇄ Eating → [garbage → Settling ⇄ Eating] → Spawning</c>, or <c>ToppedOut</c>.
/// The grid is always the source of truth; animations only read the phase timers and the recorded falls/clears.
/// </summary>
public sealed class PlayerBoard
{
    private readonly GameRules _rules;
    private readonly PieceController _controller;
    private readonly Random _garbageRandom;
    private readonly List<GameEvent> _events = [];
    /// <summary>Progress towards the next row, in rows (0..1). Kept as a fraction, not as time, so changing
    /// speed (e.g. pressing Down) never converts time saved up at the slow speed into a sudden multi-row jump.</summary>
    private double _fallProgress;
    private double _lockTimer;
    private int _chain;
    private int _outgoingGarbage;
    private bool _garbageDroppedThisCycle;

    public PlayerBoard(GameRules rules, int seed)
    {
        _rules = rules;
        _controller = new PieceController(rules);
        _garbageRandom = new Random(unchecked(seed * 31 + 17));
        Grid = Grid.For(rules);
        Generator = new RandomPieceGenerator(rules, seed);
        Score = new ScoreSystem(rules);
        EnterPhase(BoardPhase.Spawning, 0);
    }

    public GameRules Rules => _rules;

    public Grid Grid { get; }

    public RandomPieceGenerator Generator { get; }

    public ScoreSystem Score { get; }

    public BoardPhase Phase { get; private set; }

    public double PhaseElapsed { get; private set; }

    public double PhaseDuration { get; private set; }

    /// <summary>0..1 progress through the current timed phase.</summary>
    public double PhaseProgress => PhaseDuration <= 0 ? 1 : Math.Clamp(PhaseElapsed / PhaseDuration, 0, 1);

    public Piece? Current { get; private set; }

    /// <summary>Increments every time a new pair spawns; lets observers detect a new piece.</summary>
    public int PieceSerial { get; private set; }

    /// <summary>Fraction (0..1) of the way the falling pair is towards the next row, for smooth drawing.</summary>
    public double FallProgress { get; private set; }

    public ClearResult? PendingClear { get; private set; }

    public IReadOnlyList<BlockFall> SettlingFalls { get; private set; } = [];

    public int CurrentChain => _chain;

    /// <summary>Speed level used for the fall interval (1 = slowest). Set by the session every tick.</summary>
    public int SpeedLevel { get; set; } = 1;

    public int IncomingGarbage { get; private set; }

    public bool IsToppedOut => Phase == BoardPhase.ToppedOut;

    /// <summary>Events raised during the most recent <see cref="Update"/>.</summary>
    public IReadOnlyList<GameEvent> Events => _events;

    public Piece? Ghost => Current is { } piece ? CollisionSystem.DropPosition(Grid, piece) : null;

    public PiecePair NextPair(int index) => Generator.Peek(index);

    public void AddIncomingGarbage(int amount) => IncomingGarbage += Math.Max(0, amount);

    /// <summary>Uses garbage this board is about to send to cancel garbage it is about to receive. Returns what is left to send.</summary>
    public int CancelIncoming(int outgoing)
    {
        int cancelled = Math.Min(outgoing, IncomingGarbage);
        IncomingGarbage -= cancelled;
        return outgoing - cancelled;
    }

    public int TakeOutgoingGarbage()
    {
        int amount = _outgoingGarbage;
        _outgoingGarbage = 0;
        return amount;
    }

    public void Update(double deltaSeconds, in InputState input)
    {
        _events.Clear();
        PhaseElapsed += deltaSeconds;

        switch (Phase)
        {
            case BoardPhase.Spawning when PhaseElapsed >= PhaseDuration:
                SpawnPiece();
                break;
            case BoardPhase.Falling:
                UpdateFalling(deltaSeconds, input);
                break;
            case BoardPhase.Eating when PhaseElapsed >= PhaseDuration:
                FinishEating();
                break;
            case BoardPhase.Settling when PhaseElapsed >= PhaseDuration:
                SettlingFalls = [];
                CheckForClears();
                break;
        }
    }

    private void UpdateFalling(double deltaSeconds, in InputState input)
    {
        var piece = Current!.Value;
        var result = _controller.Apply(Grid, ref piece, deltaSeconds, input);
        if (result.Moved)
        {
            _events.Add(new GameEvent(GameEventType.PieceMoved));
        }

        if (result.Rotated)
        {
            _events.Add(new GameEvent(GameEventType.PieceRotated));
        }

        if (result.HardDrop)
        {
            var dropped = CollisionSystem.DropPosition(Grid, piece);
            int rows = dropped.Pivot.Y - piece.Pivot.Y;
            Score.AddDrop(rows, hardDrop: true);
            _events.Add(new GameEvent(GameEventType.HardDrop, rows));
            Current = dropped;
            Lock();
            return;
        }

        double interval = _rules.FallInterval(SpeedLevel);
        if (result.SoftDropping)
        {
            interval = Math.Min(interval, _rules.SoftDropInterval(result.SoftDropHeldSeconds));
        }

        _fallProgress += deltaSeconds / interval;
        bool grounded = CollisionSystem.IsGrounded(Grid, piece);
        while (!grounded && _fallProgress >= 1)
        {
            piece = piece.MovedBy(0, 1);
            _fallProgress -= 1;
            if (result.SoftDropping)
            {
                Score.AddDrop(1, hardDrop: false);
            }

            grounded = CollisionSystem.IsGrounded(Grid, piece);
        }

        Current = piece;
        if (grounded)
        {
            _fallProgress = Math.Min(_fallProgress, 1);
            _lockTimer += deltaSeconds;
            double lockDelay = result.SoftDropping ? _rules.LockDelay * 0.3 : _rules.LockDelay;
            FallProgress = 0;
            if (_lockTimer >= lockDelay)
            {
                Lock();
            }
        }
        else
        {
            _lockTimer = 0;
            FallProgress = Math.Clamp(_fallProgress, 0, 1);
        }
    }

    private void SpawnPiece()
    {
        var piece = Piece.Spawn(Generator.Next(), _rules);
        if (!CollisionSystem.Fits(Grid, piece))
        {
            TopOut();
            return;
        }

        Current = piece;
        PieceSerial++;
        _controller.ResetSoftDrop();
        _fallProgress = 0;
        _lockTimer = 0;
        FallProgress = 0;
        EnterPhase(BoardPhase.Falling, 0);
        _events.Add(new GameEvent(GameEventType.PieceSpawned));
    }

    private void Lock()
    {
        var piece = Current!.Value;
        Grid.Place(piece.Pivot, piece.Blocks.Pivot);
        Grid.Place(piece.Satellite, piece.Blocks.Satellite);
        Current = null;
        FallProgress = 0;
        _chain = 0;
        _garbageDroppedThisCycle = false;
        _events.Add(new GameEvent(GameEventType.PieceLanded));
        SettleOrCheck(GravitySystem.Apply(Grid));
    }

    private void SettleOrCheck(List<BlockFall> falls)
    {
        if (falls.Count == 0)
        {
            CheckForClears();
            return;
        }

        int distance = falls.Max(f => f.Distance);
        SettlingFalls = falls;
        EnterPhase(BoardPhase.Settling, distance / _rules.SettleCellsPerSecond);
    }

    private void CheckForClears()
    {
        var result = MatchDetector.Find(Grid, _rules);
        if (!result.IsEmpty)
        {
            _chain++;
            int points = Score.AddClear(result, _chain);
            int garbage = MatchResolver.GarbageFor(result, _chain, _rules);
            _outgoingGarbage += garbage;
            PendingClear = result;
            EnterPhase(BoardPhase.Eating, _rules.EatDuration(result));
            _events.Add(new GameEvent(GameEventType.Eat, points, _chain, result));
            if (garbage > 0)
            {
                _events.Add(new GameEvent(GameEventType.GarbageSent, garbage, _chain));
            }

            return;
        }

        if (_chain > 0)
        {
            _events.Add(new GameEvent(GameEventType.ChainEnded, _chain, _chain));
            _chain = 0;
        }

        if (IncomingGarbage > 0 && !_garbageDroppedThisCycle)
        {
            DropGarbage();
            return;
        }

        if (Grid.HasBlocksInHiddenRows())
        {
            TopOut();
            return;
        }

        EnterPhase(BoardPhase.Spawning, _rules.SpawnDelay);
    }

    private void FinishEating()
    {
        MatchDetector.Apply(Grid, PendingClear!);
        PendingClear = null;
        SettleOrCheck(GravitySystem.Apply(Grid));
    }

    private void DropGarbage()
    {
        _garbageDroppedThisCycle = true;
        int amount = Math.Min(IncomingGarbage, _rules.MaxGarbagePerDrop);
        IncomingGarbage -= amount;

        var columns = Enumerable.Range(0, Grid.Columns).ToArray();
        _garbageRandom.Shuffle(columns);
        for (int i = 0; i < amount; i++)
        {
            int x = columns[i % Grid.Columns];
            int y = Grid.HiddenRows - 1 - i / Grid.Columns;
            if (y >= 0 && Grid.IsFree(x, y))
            {
                var animal = _rules.Animals[_garbageRandom.Next(_rules.Animals.Count)];
                Grid.Place(new Position(x, y), Block.Of(_rules.FoodFor(animal)));
            }
        }

        _events.Add(new GameEvent(GameEventType.GarbageDropped, amount));
        SettleOrCheck(GravitySystem.Apply(Grid));
    }

    private void TopOut()
    {
        Current = null;
        EnterPhase(BoardPhase.ToppedOut, 0);
        _events.Add(new GameEvent(GameEventType.ToppedOut));
    }

    private void EnterPhase(BoardPhase phase, double duration)
    {
        Phase = phase;
        PhaseElapsed = 0;
        PhaseDuration = duration;
    }
}
