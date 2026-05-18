using System.Linq;
using System.Text;
using CatastropheContract.Core.Contracts;
using CatastropheContract.Core.UI;
using Godot;

namespace CatastropheContract;

public partial class ContractPanelNode : Control
{
    private ContractPanelViewModel? _viewModel;
    private CheckButton? _enableToggle;
    private Button? _openButton;
    private Label? _compactSummaryLabel;

    private Control? _overlayRoot;
    private PanelContainer? _selectionPanel;
    private VBoxContainer? _categoryContainer;
    private RichTextLabel? _summaryLabel;
    private Button? _savePresetButton;
    private Button? _clearButton;
    private Button? _closeButton;
    private Label? _headerLabel;
    private Label? _subheaderLabel;
    private OptionButton? _categoryPicker;

    public override void _Ready()
    {
        EnsureCompactUi();

        _enableToggle ??= GetNodeOrNull<CheckButton>("CompactPanel/CompactMargin/CompactVBox/HeaderRow/EnableToggle");
        _openButton ??= GetNodeOrNull<Button>("CompactPanel/CompactMargin/CompactVBox/HeaderRow/OpenButton");
        _compactSummaryLabel ??= GetNodeOrNull<Label>("CompactPanel/CompactMargin/CompactVBox/CompactSummaryLabel");

        if (_enableToggle != null)
        {
            _enableToggle.Text = "启用天灾合约";
            _enableToggle.Toggled += OnToggleChanged;
        }

        if (_openButton != null)
        {
            _openButton.Text = "配置词条";
            _openButton.Pressed += OnOpenPressed;
        }

        EnsureOverlay();
        HideOverlay();

        if (_viewModel != null)
        {
            SyncCompactState();
            Rebuild();
        }
    }

    public void Bind(ContractPanelViewModel viewModel)
    {
        _viewModel = viewModel;
        _viewModel.LoadLastPreset();
        SyncCompactState();
        Rebuild();
    }

    private void EnsureCompactUi()
    {
        if (_enableToggle != null && _openButton != null && _compactSummaryLabel != null)
        {
            return;
        }

        CustomMinimumSize = new Vector2(380, 104);
        Size = new Vector2(380, 104);

        PanelContainer compactPanel = new()
        {
            Name = "CompactPanel",
            Size = new Vector2(380, 104)
        };
        AddChild(compactPanel);

        MarginContainer margin = new() { Name = "CompactMargin" };
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        compactPanel.AddChild(margin);

        VBoxContainer body = new() { Name = "CompactVBox" };
        body.AddThemeConstantOverride("separation", 8);
        margin.AddChild(body);

        HBoxContainer header = new() { Name = "HeaderRow" };
        header.AddThemeConstantOverride("separation", 8);
        body.AddChild(header);

        _enableToggle = new CheckButton
        {
            Name = "EnableToggle",
            Text = "启用天灾合约"
        };
        header.AddChild(_enableToggle);

        _openButton = new Button
        {
            Name = "OpenButton",
            Text = "配置词条",
            Disabled = true
        };
        header.AddChild(_openButton);

        _compactSummaryLabel = new Label
        {
            Name = "CompactSummaryLabel",
            Text = "未启用",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        body.AddChild(_compactSummaryLabel);
    }

    private void EnsureOverlay()
    {
        if (_overlayRoot != null)
        {
            LayoutOverlay();
            return;
        }

        Window? root = GetTree()?.Root;
        if (root == null)
        {
            return;
        }

        ColorRect overlay = new()
        {
            Name = "CatastropheContractOverlay",
            Visible = false,
            Color = new Color(0f, 0f, 0f, 0.62f),
            MouseFilter = MouseFilterEnum.Stop,
            TopLevel = true,
            ZIndex = 3000
        };
        root.AddChild(overlay);
        _overlayRoot = overlay;

        _selectionPanel = new PanelContainer();
        overlay.AddChild(_selectionPanel);

        MarginContainer panelMargin = new();
        panelMargin.AddThemeConstantOverride("margin_left", 18);
        panelMargin.AddThemeConstantOverride("margin_top", 18);
        panelMargin.AddThemeConstantOverride("margin_right", 18);
        panelMargin.AddThemeConstantOverride("margin_bottom", 18);
        _selectionPanel.AddChild(panelMargin);

        VBoxContainer rootVBox = new();
        rootVBox.AddThemeConstantOverride("separation", 12);
        panelMargin.AddChild(rootVBox);

        HBoxContainer header = new();
        header.AddThemeConstantOverride("separation", 8);
        rootVBox.AddChild(header);

        _headerLabel = new Label
        {
            Text = "天灾合约",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _headerLabel.AddThemeFontSizeOverride("font_size", 26);
        header.AddChild(_headerLabel);

        _closeButton = new Button { Text = "关闭" };
        _closeButton.Pressed += OnClosePressed;
        header.AddChild(_closeButton);

        _subheaderLabel = new Label
        {
            Text = "按维度选择词条。每组词条只能选择一个等级，未实装词条会显示但不可选。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        rootVBox.AddChild(_subheaderLabel);

        HBoxContainer buttons = new();
        buttons.AddThemeConstantOverride("separation", 8);
        rootVBox.AddChild(buttons);

        _savePresetButton = new Button { Text = "保存预设" };
        _savePresetButton.Pressed += OnSavePresetPressed;
        buttons.AddChild(_savePresetButton);

        _clearButton = new Button { Text = "清空" };
        _clearButton.Pressed += OnClearPressed;
        buttons.AddChild(_clearButton);

        HBoxContainer categoryRow = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        categoryRow.AddThemeConstantOverride("separation", 8);
        rootVBox.AddChild(categoryRow);

        Label categoryLabel = new()
        {
            Text = "Category",
            VerticalAlignment = VerticalAlignment.Center
        };
        categoryRow.AddChild(categoryLabel);

        _categoryPicker = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _categoryPicker.ItemSelected += OnCategorySelected;
        categoryRow.AddChild(_categoryPicker);

        HSplitContainer body = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
        rootVBox.AddChild(body);

        ScrollContainer categoryScroll = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        body.AddChild(categoryScroll);

        _categoryContainer = new VBoxContainer();
        _categoryContainer.AddThemeConstantOverride("separation", 12);
        categoryScroll.AddChild(_categoryContainer);

        PanelContainer summaryPanel = new()
        {
            CustomMinimumSize = new Vector2(320, 0),
            SizeFlagsHorizontal = SizeFlags.Fill
        };
        body.AddChild(summaryPanel);

        MarginContainer summaryMargin = new();
        summaryMargin.AddThemeConstantOverride("margin_left", 12);
        summaryMargin.AddThemeConstantOverride("margin_top", 12);
        summaryMargin.AddThemeConstantOverride("margin_right", 12);
        summaryMargin.AddThemeConstantOverride("margin_bottom", 12);
        summaryPanel.AddChild(summaryMargin);

        _summaryLabel = new RichTextLabel
        {
            FitContent = true,
            BbcodeEnabled = false
        };
        summaryMargin.AddChild(_summaryLabel);

        LayoutOverlay();
    }

    private void LayoutOverlay()
    {
        if (_overlayRoot == null || _selectionPanel == null)
        {
            return;
        }

        Vector2 viewportSize = GetTree()?.Root?.Size ?? GetViewportRect().Size;
        _overlayRoot.Position = Vector2.Zero;
        _overlayRoot.Size = viewportSize;
        _overlayRoot.CustomMinimumSize = viewportSize;

        Vector2 panelSize = new(Mathf.Min(1360, viewportSize.X - 120), Mathf.Min(900, viewportSize.Y - 120));
        _selectionPanel.Position = (viewportSize - panelSize) / 2f;
        _selectionPanel.Size = panelSize;
        _selectionPanel.CustomMinimumSize = panelSize;
    }

    private void SyncCompactState()
    {
        if (_viewModel == null)
        {
            return;
        }

        if (_enableToggle != null)
        {
            _enableToggle.ButtonPressed = _viewModel.IsEnabled;
        }

        if (_openButton != null)
        {
            _openButton.Disabled = !_viewModel.IsEnabled;
        }

        RefreshCompactSummary();
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

        if (_categoryPicker != null)
        {
            _categoryPicker.Clear();
            int selectedIndex = 0;
            int index = 0;
            foreach (ContractCategoryMetadata category in ContractDatabase.AllCategories)
            {
                _categoryPicker.AddItem(category.EnglishName);
                if (category.Category == _viewModel.SelectedCategory)
                {
                    selectedIndex = index;
                }

                index++;
            }
            _categoryPicker.Select(selectedIndex);
        }

        ContractCategoryMetadata selectedCategory =
            ContractDatabase.AllCategories.FirstOrDefault(category => category.Category == _viewModel.SelectedCategory)
            ?? ContractDatabase.AllCategories.First();

        VBoxContainer section = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        section.AddThemeConstantOverride("separation", 10);
        _categoryContainer.AddChild(section);

        Label title = new()
        {
            Text = $"{selectedCategory.DisplayName}  {selectedCategory.EnglishName}",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        title.AddThemeFontSizeOverride("font_size", 18);
        section.AddChild(title);

        Label summary = new()
        {
            Text = selectedCategory.Summary,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        summary.Modulate = new Color(0.75f, 0.77f, 0.72f);
        section.AddChild(summary);

        foreach (ContractGroupDefinition group in ContractDatabase.GetGroupsByCategory(selectedCategory.Category))
        {
            section.AddChild(BuildGroupCard(group));
        }

        RefreshSummary();
    }

    private Control BuildGroupCard(ContractGroupDefinition group)
    {
        PanelContainer card = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        bool isFocused = _viewModel?.FocusedGroupId == group.Id;

        MarginContainer margin = new();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        card.AddChild(margin);

        VBoxContainer body = new();
        body.AddThemeConstantOverride("separation", 8);
        margin.AddChild(body);

        Button title = new()
        {
            Text = $"{group.DisplayName}  {group.EnglishName}",
            Flat = true,
            Alignment = HorizontalAlignment.Left,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        title.AddThemeFontSizeOverride("font_size", 18);
        title.Pressed += () => OnGroupHeaderPressed(group.Id);
        body.AddChild(title);

        Label summary = new()
        {
            Text = $"{group.Summary}  状态：{(group.IsImplemented ? "已实现" : "未实装")}",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        summary.Modulate = group.IsImplemented
            ? new Color(0.80f, 0.80f, 0.77f)
            : new Color(0.90f, 0.68f, 0.68f);
        body.AddChild(summary);

        if (!isFocused)
        {
            ContractDefinition? collapsedLevel = group.Levels
                .OrderBy(level => level.RiskValue)
                .FirstOrDefault(level => _viewModel?.GetSelectedTierForGroup(group.Id) == level.Id);
            if (collapsedLevel != null)
            {
                Label collapsedSelection = new()
                {
                    Text = $"宸查€夛細{collapsedLevel.LevelLabel}  [+{collapsedLevel.RiskValue}]",
                    AutowrapMode = TextServer.AutowrapMode.WordSmart
                };
                collapsedSelection.Modulate = new Color(1.0f, 0.93f, 0.72f);
                body.AddChild(collapsedSelection);
            }

            return card;
        }

        HBoxContainer tierButtons = new();
        tierButtons.AddThemeConstantOverride("separation", 8);
        body.AddChild(tierButtons);

        ContractDefinition? selectedLevel = group.Levels
            .OrderBy(level => level.RiskValue)
            .FirstOrDefault(level => _viewModel?.GetSelectedTierForGroup(group.Id) == level.Id)
            ?? group.Levels.OrderBy(level => level.RiskValue).FirstOrDefault();

        foreach (ContractDefinition contract in group.Levels.OrderBy(level => level.RiskValue))
        {
            Button button = new()
            {
                Text = $"{contract.LevelLabel}  风险+{contract.RiskValue}",
                ToggleMode = true,
                ButtonPressed = _viewModel?.GetSelectedTierForGroup(group.Id) == contract.Id,
                Disabled = !contract.IsImplemented,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            button.Pressed += () => OnTierPressed(group.Id, contract.Id);
            tierButtons.AddChild(button);
        }

        if (selectedLevel != null)
        {
            Label description = new()
            {
                Text = $"{selectedLevel.LevelLabel}: {selectedLevel.Summary}" + (selectedLevel.IsImplemented ? string.Empty : " [未实装]"),
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            description.Modulate = _viewModel?.GetSelectedTierForGroup(group.Id) == selectedLevel.Id
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

    private void RefreshCompactSummary()
    {
        if (_viewModel == null || _compactSummaryLabel == null)
        {
            return;
        }

        if (!_viewModel.IsEnabled)
        {
            _compactSummaryLabel.Text = "未启用";
            return;
        }

        _compactSummaryLabel.Text = $"已启用 | 挑战等级 {_viewModel.RiskLevel} | 词条 {_viewModel.SelectedContracts.Count}";
    }

    private void OnToggleChanged(bool pressed)
    {
        if (_viewModel == null)
        {
            return;
        }

        _viewModel.IsEnabled = pressed;
        _viewModel.SavePreset();

        if (!pressed)
        {
            HideOverlay();
        }

        SyncCompactState();
    }

    private void OnOpenPressed()
    {
        if (_viewModel == null || !_viewModel.IsEnabled)
        {
            return;
        }

        LayoutOverlay();
        Rebuild();
        ShowOverlay();
    }

    private void OnClosePressed()
    {
        HideOverlay();
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

    private void OnGroupHeaderPressed(string groupId)
    {
        if (_viewModel == null)
        {
            return;
        }

        _viewModel.FocusGroup(groupId);
        Rebuild();
    }

    private void OnCategorySelected(long index)
    {
        if (_viewModel == null)
        {
            return;
        }

        ContractCategoryMetadata[] categories = ContractDatabase.AllCategories.ToArray();
        if (index < 0 || index >= categories.Length)
        {
            return;
        }

        _viewModel.SetSelectedCategory(categories[index].Category);
        Rebuild();
    }

    private void OnSavePresetPressed()
    {
        _viewModel?.SavePreset();
        RefreshSummary();
        RefreshCompactSummary();
    }

    private void OnClearPressed()
    {
        _viewModel?.Clear();
        Rebuild();
    }

    private void ShowOverlay()
    {
        if (_overlayRoot != null)
        {
            _overlayRoot.Visible = true;
        }
    }

    private void HideOverlay()
    {
        if (_overlayRoot != null)
        {
            _overlayRoot.Visible = false;
        }
    }
}
