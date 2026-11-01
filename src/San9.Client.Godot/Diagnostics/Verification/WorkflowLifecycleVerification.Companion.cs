using Godot;
using San9.Core.Domain;

namespace San9.Client.Godot;

public partial class WorkflowLifecycleVerification
{
    private void CheckCompanionDraft()
    {
        CompanionSettings? saved = null;
        int writes = 0, closes = 0;
        bool failSave = false;
        var view = new CompanionSettingsView
        {
            SaveAndApply = settings =>
            {
                if (failSave) throw new IOException("injected save failure");
                saved = settings;
                writes++;
            },
            Closed = () => closes++,
        };
        AddChild(view);
        try
        {
            var buttons = view.FindChildren("*", "Button", true, false).OfType<Button>().ToArray();
            var numbers = view.FindChildren("*", "SpinBox", true, false).OfType<SpinBox>().ToArray();
            var revenue = numbers.Single(n => n.TooltipText == "普通电脑势力的季度资金收入倍率。");
            var harvest = numbers.Single(n => n.TooltipText == "普通电脑势力的七月兵粮收获倍率。");
            var battle = numbers.Single(n => n.TooltipText.StartsWith("攻城交战造成的民心", StringComparison.Ordinal));
            var preset = buttons.Single(b => b.Text == "预设配置");
            var reset = buttons.Single(b => b.Text == "原版配置");
            var toggle = buttons.First(b => b.ToggleMode);

            view.Back();
            Check(writes == 0 && closes == 1, "Unchanged companion settings were saved.");
            revenue.GetLineEdit().Text = "175";
            harvest.GetLineEdit().Text = "225";
            toggle.ButtonPressed = true;
            failSave = true;
            view.Back();
            Check(writes == 0 && closes == 1, "A failed companion save closed the draft.");
            failSave = false;
            view.Back();
            Check(saved is { ComputerRevenuePercent: 175, ComputerHarvestPercent: 225, PlayerAutoDomesticEnabled: true },
                "Editing another control or applying numeric fields lost pending companion input.");

            battle.GetLineEdit().Text = "75";
            preset.EmitSignal(BaseButton.SignalName.Pressed);
            view.Back();
            Check(saved is { ComputerRevenuePercent: 200, ComputerHarvestPercent: 200, OfficerAbilityCap: 125,
                NoRetreatCaptureBonus: 0, CityBattleDamagePercent: 75, DisableComputerPrisonerExecution: true,
                DisablePlayerPrisonerExecution: true,
                ConfigureTacticsOnFormationSelection: true, BattleDeathProtectionThreshold: 125,
                AllowCommanderBattleDeaths: true, DuelDeathCoefficient: 0, InfiniteLifespan: true } &&
                saved.ComputerStrategyPermissions is { Alienate: false, Burn: false, FalseReport: true } &&
                saved.PlayerDelegatedStrategyPermissions == saved.ComputerStrategyPermissions,
                $"The companion preset discarded a retained numeric draft or applied incorrect policies: {saved}.");

            reset.EmitSignal(BaseButton.SignalName.Pressed);
            view.Back();
            Check(writes == 2 && closes == 4, "Restoring the original settings wrote an unchanged draft.");
            GD.Print("COMPANION_DRAFT_OK pending-input,preset,reset,save-failure");
        }
        finally { view.Free(); }
    }
}
