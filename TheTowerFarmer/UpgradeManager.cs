using OpenCvSharp;
using Serilog;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TheTowerFarmer;

internal class UpgradeManager(Android android, Vision vision, BroadcastChannel<Mat> frameProducer, ILogger logger)
{
    private readonly Dictionary<UpgradeWindows, string[]> windowTemplates = new()
    {
        { UpgradeWindows.Attack, [Templates.Battle.AttackUpgrade] },
        { UpgradeWindows.Defense, [Templates.Battle.DefenseUpgrade] },
        { UpgradeWindows.Utility, [Templates.Battle.UtilityUpgrade] },
    };

    private readonly Dictionary<UpgradeWindows, string> buttonTemplates = new()
    {
        { UpgradeWindows.Attack, Templates.Battle.AttackOn },
        { UpgradeWindows.Defense, Templates.Battle.DefenseOff },
        { UpgradeWindows.Utility, Templates.Battle.EcoOff },
    };

    private Dictionary<Upgrades, UpgradeOption> availableUpgrades = new();

    private static readonly string nonNumericCharacters = "[^0123456789.]";
    private static readonly Rect upgradeZone = new Rect(0, 1045, 900, 465);

    public async Task RunAsync(CancellationToken token)
    {
        try
        {
            await RefreshUpgradesAsync(UpgradeWindows.Attack, token);
            await RefreshUpgradesAsync(UpgradeWindows.Defense, token);
            await RefreshUpgradesAsync(UpgradeWindows.Utility, token);

            PrintUpgrades();

            while (!token.IsCancellationRequested)
            {
                await frameProducer.WaitAsync(token);
            }
        }
        catch (TaskCanceledException) { }
        catch (Exception ex) { logger.Error("Error in UpgradeManager", ex); }
    }

    public async Task ChangeUpgradeWindowAsync(UpgradeWindows upgrade, CancellationToken token)
    {
        if (upgrade == UpgradeWindows.None)
            return;

        var frame = await frameProducer.WaitAsync(token);
        var match = vision.FindTemplate(frame, buttonTemplates[upgrade]);

        if (match != null)
        {
            var point = match.Value;
            await android.TapAsync(point.X, point.Y, token);
        }
    }

    public async Task<UpgradeWindows> DetectUpgradeWindowAsync(CancellationToken token)
    {
        var frame = await frameProducer.WaitAsync(token);
        foreach (var (state, templatePaths) in windowTemplates)
        {
            foreach (var templatePath in templatePaths)
            {
                var match = vision.FindTemplate(frame, templatePath);

                if (match != null)
                    return state;
            }
        }

        return UpgradeWindows.None;
    }

    private async Task RefreshUpgradesAsync(UpgradeWindows window, CancellationToken token)
    {
        while (await DetectUpgradeWindowAsync(token) != window)
        {
            await ChangeUpgradeWindowAsync(window, token);
        }

        var frame = await frameProducer.WaitAsync(token);

        using var cropped = new Mat(frame, upgradeZone);
        var upgrades = vision.DetectUpgrades(cropped);

        foreach (var upgrade in upgrades)
        {
            if (!Enum.TryParse<Upgrades>(upgrade.Name.Replace("\n", "").Replace("/", "").Replace("%", ""), true, out var name))
            {
                logger.Warning("Unknown upgrade: {UpgradeName}", upgrade.Name);
                continue;
            }

            var values = upgrade.Value.Split("\n");

            if (values.Length != 3)
            {
                availableUpgrades[name] = new UpgradeOption(-1, -1, true);
                continue;
            }

            var amount = Regex.Replace(values[1], nonNumericCharacters, "");
            var cost = Regex.Replace(values[2], nonNumericCharacters, "");

            if (double.TryParse(amount, out var intAmount) && double.TryParse(cost, out var intCost))
            {
                availableUpgrades[name] = new UpgradeOption(intAmount, intCost);
            }
        }
    }

    private void PrintUpgrades()
    {
        var nameWidth = Math.Max(availableUpgrades.Keys.Max(u => u.ToString().Length), 7);
        var amountWidth = Math.Max(availableUpgrades.Values.Max(u => u.Amount.ToString().Length), 6);
        var costWidth = Math.Max(availableUpgrades.Values.Max(u => u.Cost.ToString().Length), 4);

        var stringBuilder = new StringBuilder();

        var header = $"| {"Upgrade".PadRight(nameWidth)} | {"Amount".PadRight(amountWidth)} | {"Cost".PadRight(costWidth)} |";

        stringBuilder.AppendLine(new string('-', header.Length));
        stringBuilder.AppendLine(header);
        stringBuilder.AppendLine(new string('-', header.Length));

        foreach (var (upgrade, option) in availableUpgrades)
        {
            var maskedAmount = option.IsMax ? "MAX" : option.Amount.ToString();
            var maskedCost = option.IsMax ? "-" : option.Cost.ToString();
            stringBuilder.AppendLine($"| {upgrade.ToString().PadRight(nameWidth)} | {maskedAmount.PadRight(amountWidth)} | {maskedCost.PadRight(costWidth)} |");
        }

        stringBuilder.AppendLine(new string('-', header.Length));
        logger.Information(Environment.NewLine + stringBuilder.ToString());
    }

    private class UpgradeOption(double amount, double cost, bool isMax = false)
    {
        public double Amount { get; set; } = amount;
        public double Cost { get; set; } = cost;
        public bool IsMax { get; set; } = isMax;
    }
}
