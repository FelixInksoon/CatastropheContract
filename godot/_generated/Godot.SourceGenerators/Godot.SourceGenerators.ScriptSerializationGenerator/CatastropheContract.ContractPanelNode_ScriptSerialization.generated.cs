using Godot;
using Godot.NativeInterop;

namespace CatastropheContract {

partial class ContractPanelNode
{
    /// <inheritdoc/>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    protected override void SaveGodotObjectData(global::Godot.Bridge.GodotSerializationInfo info)
    {
        base.SaveGodotObjectData(info);
        info.AddProperty(PropertyName.@_categoryContainer, global::Godot.Variant.From<global::Godot.VBoxContainer>(this.@_categoryContainer));
        info.AddProperty(PropertyName.@_summaryLabel, global::Godot.Variant.From<global::Godot.RichTextLabel>(this.@_summaryLabel));
        info.AddProperty(PropertyName.@_savePresetButton, global::Godot.Variant.From<global::Godot.Button>(this.@_savePresetButton));
        info.AddProperty(PropertyName.@_clearButton, global::Godot.Variant.From<global::Godot.Button>(this.@_clearButton));
        info.AddProperty(PropertyName.@_headerLabel, global::Godot.Variant.From<global::Godot.Label>(this.@_headerLabel));
        info.AddProperty(PropertyName.@_subheaderLabel, global::Godot.Variant.From<global::Godot.Label>(this.@_subheaderLabel));
    }
    /// <inheritdoc/>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    protected override void RestoreGodotObjectData(global::Godot.Bridge.GodotSerializationInfo info)
    {
        base.RestoreGodotObjectData(info);
        if (info.TryGetProperty(PropertyName.@_categoryContainer, out var _value__categoryContainer))
            this.@_categoryContainer = _value__categoryContainer.As<global::Godot.VBoxContainer>();
        if (info.TryGetProperty(PropertyName.@_summaryLabel, out var _value__summaryLabel))
            this.@_summaryLabel = _value__summaryLabel.As<global::Godot.RichTextLabel>();
        if (info.TryGetProperty(PropertyName.@_savePresetButton, out var _value__savePresetButton))
            this.@_savePresetButton = _value__savePresetButton.As<global::Godot.Button>();
        if (info.TryGetProperty(PropertyName.@_clearButton, out var _value__clearButton))
            this.@_clearButton = _value__clearButton.As<global::Godot.Button>();
        if (info.TryGetProperty(PropertyName.@_headerLabel, out var _value__headerLabel))
            this.@_headerLabel = _value__headerLabel.As<global::Godot.Label>();
        if (info.TryGetProperty(PropertyName.@_subheaderLabel, out var _value__subheaderLabel))
            this.@_subheaderLabel = _value__subheaderLabel.As<global::Godot.Label>();
    }
}

}
