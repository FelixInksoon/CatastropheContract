using System.Linq;
using System.Text;
using CatastropheContract.Core.Contracts;
using CatastropheContract.Core.UI;
using Godot;

namespace CatastropheContract;

public partial class ContractPanelNode : PanelContainer
{
    private ContractPanelViewModel? _viewModel;
    private VBoxContainer? _categoryContainer;
    private RichTextLabel? _summaryLabel;
    private Button? _savePresetButton;
    private Button? _clearButton;
    private Label? _headerLabel;
    private Label? _subheaderLabel;

    public override void _Ready()
    {
        _categoryContainer = GetNodeOrNull<VBoxContainer>("%CategoryContainer");
        _summaryLabel = GetNodeOrNull<RichTextLabel>("%SummaryLabel");
        _savePresetButton = GetNodeOrNull<Button>("%SavePresetButton");
        _clearButton = GetNodeOrNull<Button>("%ClearButton");
        _headerLabel = GetNodeOrNull<Label>("%HeaderLabel");
        _subheaderLabel = GetNodeOrNull<Label>("%SubheaderLabel");

        if (_savePresetButton != null)
        {
            _savePresetButton.Pressed += OnSavePresetPressed;
        }

        if (_clearButton != null)
        {
            _clearButton.Pressed += OnClearPressed;
        }
    }

    public void Bind(ContractPanelViewModel viewModel)
    {
        _viewModel = viewModel;
        _viewModel.LoadLastPreset();

        if (_headerLabel != null)
        {
            _headerLabel.Text = "天灾合约";
        }

        if (_subheaderLabel != null)
        {
            _subheaderLabel.Text = "按维度挑选词条，每组词条只能选择一个等级。挑战等级会实时累加。";
        }

        Rebuild();
    }

    private void Rebuild()
    {
        if (_viewModel == null || _categoryContainer == null || _summaryLabel == null)
        {
            return;
        }

        foreach (Node child in _categoryContainer.GetChildren())
        {
            child.QueueFree();
        }

        foreach (ContractCategoryMetadata category in ContractDatabase.AllCategories)
        {
            VBoxContainer section = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            section.AddThemeConstantOverride("separation", 10);
            _categoryContainer.AddChild(section);

            Label title = new()
            {
                Text = $"{category.DisplayName}  {category.EnglishName}",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            title.AddThemeFontSizeOverride("font_size", 22);
            section.AddChild(title);

            Label summary = new()
            {
                Text = category.Summary,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            summary.Modulate = new Color(0.75f, 0.77f, 0.72f);
            section.AddChild(summary);

            foreach (ContractGroupDefinition group in ContractDatabase.GetGroupsByCategory(category.Category))
            {
                section.AddChild(BuildGroupCard(group));
            }
        }

        RefreshSummary();
    }

    private Control BuildGroupCard(ContractGroupDefinition group)
    {
        PanelContainer card = new();
        card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        MarginContainer margin = new();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        card.AddChild(margin);

        VBoxContainer body = new();
        body.AddThemeConstantOverride("separation", 8);
        margin.AddChild(body);

        Label title = new()
        {
            Text = $"{group.DisplayName}  {group.EnglishName}",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        title.AddThemeFontSizeOverride("font_size", 18);
        body.AddChild(title);

        Label summary = new()
        {
            Text = group.Summary,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        summary.Modulate = new Color(0.80f, 0.80f, 0.77f);
        body.AddChild(summary);

        HBoxContainer tierButtons = new();
        tierButtons.AddThemeConstantOverride("separation", 8);
        body.AddChild(tierButtons);

        foreach (ContractDefinition contract in group.Levels.OrderBy(level => level.RiskValue))
        {
            Button button = new()
            {
                Text = $"{contract.LevelLabel}  风险+{contract.RiskValue}",
                ToggleMode = true,
                ButtonPressed = _viewModel?.GetSelectedTierForGroup(group.Id) == contract.Id,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            button.Pressed += () => OnTierPressed(group.Id, contract.Id);
            tierButtons.AddChild(button);
        }

        foreach (ContractDefinition contract in group.Levels.OrderBy(level => level.RiskValue))
        {
            Label description = new()
            {
                Text = $"{contract.LevelLabel}: {contract.Summary}",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            description.Modulate = _viewModel?.GetSelectedTierForGroup(group.Id) == contract.Id
                ? new Color(1.0f, 0.93f, 0.72f)
                : new Color(0.72f, 0.74f, 0.72f);
            body.AddChild(description);
        }

        return card;
    }

    private void RefreshSummary()
    {
        if (_viewModel == null || _summaryLabel == null)
        {
            return;
        }

        StringBuilder builder = new();
        builder.AppendLine($"挑战等级: {_viewModel.RiskLevel}");
        builder.AppendLine($"已选词条: {_viewModel.SelectedContracts.Count}");
        builder.AppendLine();

        foreach (string id in _viewModel.SelectedContracts.OrderBy(id => id))
        {
            ContractDefinition? definition = ContractDatabase.TryGet(id);
            if (definition != null)
            {
                builder.AppendLine($"• {definition.DisplayName} {definition.LevelLabel}  [+{definition.RiskValue}]");
            }
        }

        if (_viewModel.Conflicts.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("冲突提示:");
            foreach (string conflict in _viewModel.Conflicts)
            {
                builder.AppendLine($"- {conflict}");
            }
        }

        _summaryLabel.Text = builder.ToString();
    }

    private void OnTierPressed(string groupId, string contractId)
    {
        if (_viewModel == null)
        {
            return;
        }

        string? selected = _viewModel.GetSelectedTierForGroup(groupId);
        if (selected == contractId)
        {
            _viewModel.DeselectTier(contractId);
        }
        else
        {
            _viewModel.SelectTier(contractId);
        }

        Rebuild();
    }

    private void OnSavePresetPressed()
    {
        _viewModel?.SavePreset();
        RefreshSummary();
    }

    private void OnClearPressed()
    {
        _viewModel?.Clear();
        Rebuild();
    }
}
