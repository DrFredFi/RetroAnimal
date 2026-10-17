using RetroAnimalPuzzle.Application;
using RetroAnimalPuzzle.Audio;
using RetroAnimalPuzzle.Core;
using RetroAnimalPuzzle.Input;
using RetroAnimalPuzzle.Rendering;
using SkiaSharp;

namespace RetroAnimalPuzzle.UI;

/// <summary>Three illustrated pages: the eating rule, chains/coins/versus, and the controls.</summary>
internal sealed class HelpScreen : IScreen
{
    private const int PageCount = 3;
    private static readonly SKColor Yellow = new(0xFF, 0xE4, 0x5C);

    private readonly GameContext _context;
    private int _page;
    private double _time;

    public HelpScreen(GameContext context)
    {
        _context = context;
    }

    public void Update(double deltaSeconds, in InputState input)
    {
        _time += deltaSeconds;
        int dx = MenuList.Horizontal(input);
        if (dx != 0)
        {
            int page = Math.Clamp(_page + dx, 0, PageCount - 1);
            if (page != _page)
            {
                _page = page;
                _context.Audio.Play(Sound.MenuMove);
            }
        }

        if (input.WasPressed(GameAction.Back) || input.WasPressed(GameAction.Help) ||
            (input.WasPressed(GameAction.Confirm) && _page == PageCount - 1))
        {
            _context.Audio.Play(Sound.MenuBack);
            _context.Screens.Pop();
        }
        else if (input.WasPressed(GameAction.Confirm))
        {
            _page++;
            _context.Audio.Play(Sound.MenuMove);
        }
    }

    public void Render(SKCanvas canvas)
    {
        var ui = _context.Ui;
        _context.Arena.DrawBackground(canvas);
        var panel = new SKRect(40, 30, 760, 560);
        ui.Panel(canvas, panel);
        string[] titles = ["HOW TO PLAY", "CHAINS, COINS & VERSUS", "CONTROLS"];
        ui.OutlinedText(canvas, titles[_page], 400, 82, 32, Yellow, Palette.Outline, UiFont.Heading, 3);

        switch (_page)
        {
            case 0: RulesPage(canvas, ui); break;
            case 1: ChainsPage(canvas, ui); break;
            default: ControlsPage(canvas, ui); break;
        }

        string nav = _page < PageCount - 1 ? "Left / Right: page    Enter: next    Esc: back" : "Left / Right: page    Enter / Esc: back";
        ui.Text(canvas, $"{nav}      ({_page + 1}/{PageCount})", 400, 540, 15, Palette.TimerText, UiFont.Body);
    }

    private void RulesPage(SKCanvas canvas, UiRenderer ui)
    {
        float y = ui.Paragraph(canvas,
            "Pairs of blocks fall into your field. Move and rotate them so that each animal touches the food it loves. " +
            "When an animal touches its food (up, down, left or right - never diagonally), it eats the food and every " +
            "matching food connected to it, then leaves the field too. It works both ways: drop the animal on the food, " +
            "or the food next to the animal.",
            80, 125, 640, 18, SKColors.White);

        var rules = _context.Rules;
        float x = 100;
        y += 10;
        foreach (var animal in rules.Animals)
        {
            var food = rules.FoodFor(animal);
            bool chomp = (int)(_time * 3) % 2 == 0;
            _context.Art.Draw(canvas, Block.Of(animal), x, y, 52, chomp ? BlockPose.Chomp : BlockPose.Normal);
            ui.OutlinedText(canvas, "loves", x + 26, y + 72, 15, SKColors.White, Palette.Outline, UiFont.Playful, 2);
            _context.Art.Draw(canvas, Block.Of(food), x, y + 84, 52);
            ui.Text(canvas, animal.ToString(), x + 26, y + 156, 14, Yellow, UiFont.Heading);
            ui.Text(canvas, food.ToString(), x + 26, y + 174, 14, Palette.TimerText, UiFont.Body);
            x += 160;
        }

        ui.Paragraph(canvas,
            "Bigger groups of food score more. The game ends when the blocks reach the top of the field and no new pair can appear.",
            80, y + 214, 640, 17, Palette.TimerText);
    }

    private void ChainsPage(SKCanvas canvas, UiRenderer ui)
    {
        float y = ui.Paragraph(canvas,
            "CHAINS: when food disappears, everything above it falls. If a falling block lands next to a hungry animal, " +
            "it is eaten too - and the score multiplier grows with every step of the chain (x2, x4, x6...).",
            80, 125, 640, 18, SKColors.White);

        // Mini diagram of a 2-step chain.
        float t = (float)(_time % 3.0);
        float baseX = 300, baseY = y + 10, s = 44;
        var art = _context.Art;
        art.Draw(canvas, Block.Of(AnimalType.Rabbit), baseX, baseY + s, s);
        if (t < 1.0f)
        {
            art.Draw(canvas, Block.Of(FoodType.Carrot), baseX + s, baseY, s);
            art.Draw(canvas, Block.Of(FoodType.Banana), baseX + s, baseY + s, s);
            art.Draw(canvas, Block.Of(AnimalType.Monkey), baseX + 2 * s, baseY + s, s, t > 0.5f ? BlockPose.Chomp : BlockPose.Normal);
        }
        else if (t < 2.0f)
        {
            float fall = Math.Min(1f, (t - 1f) * 3f) * s;
            art.Draw(canvas, Block.Of(FoodType.Carrot), baseX + s, baseY + fall, s);
            ui.OutlinedText(canvas, "1", baseX + 2.5f * s, baseY + s * 1.2f, 22, Yellow, Palette.Outline, UiFont.Heading);
        }
        else
        {
            art.Draw(canvas, Block.Of(AnimalType.Rabbit), baseX, baseY + s, s, BlockPose.Chomp);
            ui.OutlinedText(canvas, "CHAIN x2!", baseX + s, baseY + s * 0.4f, 24, Yellow, Palette.Outline, UiFont.Heading);
        }

        y = baseY + 2 * s + 30;
        art.Draw(canvas, Block.Coin, 84, y - 6, 40);
        y = ui.Paragraph(canvas,
            "COINS: a coin that lands on a block makes every block of that exact kind on the field disappear. " +
            "Land it on the most common food for a huge clean-up!",
            136, y + 12, 584, 17, SKColors.White);

        art.Draw(canvas, Block.Of(FoodType.Bone), 84, y + 4, 40);
        ui.Paragraph(canvas,
            "VERSUS: everything you eat is sent to the CPU as extra food that drops onto its field (chains send much more). " +
            "The heart on the bar above your field shows food on its way: the further it travels, the more is coming. " +
            "What you eat cancels it first. Top out the CPU to win the round; two rounds win the stage.",
            136, y + 22, 584, 17, SKColors.White);
    }

    private void ControlsPage(SKCanvas canvas, UiRenderer ui)
    {
        var bindings = _context.Input.Bindings;
        (string, GameAction?)[] rows =
        [
            ("Move left", GameAction.MoveLeft),
            ("Move right", GameAction.MoveRight),
            ("Rotate clockwise", GameAction.RotateClockwise),
            ("Rotate counter-clockwise", GameAction.RotateCounterClockwise),
            ("Drop faster (hold: speeds up)", GameAction.SoftDrop),
            ("Pause", GameAction.Pause),
            ("Help", null),
            ("Fullscreen", null),
        ];
        string[] fixedKeys = ["F1", "F11 / Alt+Enter"];
        float y = 140;
        int fixedIndex = 0;
        foreach (var (label, action) in rows)
        {
            string key = action is { } a ? KeyboardBindings.KeyName(bindings.PrimaryKey(a)) : fixedKeys[fixedIndex++];
            if (action == GameAction.Pause) key += " / Esc";
            if (action == GameAction.RotateClockwise && bindings.ActionsFor(Keys.X).Contains(GameAction.RotateClockwise)) key += " / X";
            ui.Text(canvas, label, 160, y, 20, SKColors.White, UiFont.Playful, SKTextAlign.Left);
            ui.OutlinedText(canvas, key, 640, y, 20, Yellow, Palette.Outline, UiFont.Heading, 2, SKTextAlign.Right);
            y += 38;
        }

        ui.Text(canvas, "Gameplay keys can be changed in Options > Controls.", 400, y + 16, 16, Palette.TimerText, UiFont.Body);
    }
}
