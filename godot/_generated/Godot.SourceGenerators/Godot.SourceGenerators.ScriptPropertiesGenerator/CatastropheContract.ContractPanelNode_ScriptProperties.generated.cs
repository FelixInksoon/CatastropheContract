using Godot;
using Godot.NativeInterop;

namespace CatastropheContract {

partial class ContractPanelNode
{
#pragma warning disable CS0109 // Disable warning about redundant 'new' keyword
    /// <summary>
    /// Cached StringNames for the properties and fields contained in this class, for fast lookup.
    /// </summary>
    public new class PropertyName : global::Godot.PanelContainer.PropertyName {
        /// <summary>
        /// Cached name for the '_categoryContainer' field.
        /// </summary>
        public new static readonly global::Godot.StringName @_categoryContainer = "_categoryContainer";
        /// <summary>
        /// Cached name for the '_summaryLabel' field.
        /// </summary>
        public new static readonly global::Godot.StringName @_summaryLabel = "_summaryLabel";
        /// <summary>
        /// Cached name for the '_savePresetButton' field.
        /// </summary>
        public new static readonly global::Godot.StringName @_savePresetButton = "_savePresetButton";
        /// <summary>
        /// Cached name for the '_clearButton' field.
        /// </summary>
        public new static readonly global::Godot.StringName @_clearButton = "_clearButton";
        /// <summary>
        /// Cached name for the '_headerLabel' field.
        /// </summary>
        public new static readonly global::Godot.StringName @_headerLabel = "_headerLabel";
        /// <summary>
        /// Cached name for the '_subheaderLabel' field.
        /// </summary>
        public new static readonly global::Godot.StringName @_subheaderLabel = "_subheaderLabel";
    }
    /// <inheritdoc/>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    protected override bool SetGodotClassPropertyValue(in godot_string_name name, in godot_variant value)
    {
        if (name == PropertyName.@_categoryContainer) {
            this.@_categoryContainer = global::Godot.NativeInterop.VariantUtils.ConvertTo<global::Godot.VBoxContainer>(value);
            return true;
        }
        if (name == PropertyName.@_summaryLabel) {
            this.@_summaryLabel = global::Godot.NativeInterop.VariantUtils.ConvertTo<global::Godot.RichTextLabel>(value);
            return true;
        }
        if (name == PropertyName.@_savePresetButton) {
            this.@_savePresetButton = global::Godot.NativeInterop.VariantUtils.ConvertTo<global::Godot.Button>(value);
            return true;
        }
        if (name == PropertyName.@_clearButton) {
            this.@_clearButton = global::Godot.NativeInterop.VariantUtils.ConvertTo<global::Godot.Button>(value);
            return true;
        }
        if (name == PropertyName.@_headerLabel) {
            this.@_headerLabel = global::Godot.NativeInterop.VariantUtils.ConvertTo<global::Godot.Label>(value);
            return true;
        }
        if (name == PropertyName.@_subheaderLabel) {
            this.@_subheaderLabel = global::Godot.NativeInterop.VariantUtils.ConvertTo<global::Godot.Label>(value);
            return true;
        }
        return base.SetGodotClassPropertyValue(name, value);
    }
    /// <inheritdoc/>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    protected override bool GetGodotClassPropertyValue(in godot_string_name name, out godot_variant value)
    {
        if (name == PropertyName.@_categoryContainer) {
            value = global::Godot.NativeInterop.VariantUtils.CreateFrom<global::Godot.VBoxContainer>(this.@_categoryContainer);
            return true;
        }
        if (name == PropertyName.@_summaryLabel) {
            value = global::Godot.NativeInterop.VariantUtils.CreateFrom<global::Godot.RichTextLabel>(this.@_summaryLabel);
            return true;
        }
        if (name == PropertyName.@_savePresetButton) {
            value = global::Godot.NativeInterop.VariantUtils.CreateFrom<global::Godot.Button>(this.@_savePresetButton);
            return true;
        }
        if (name == PropertyName.@_clearButton) {
            value = global::Godot.NativeInterop.VariantUtils.CreateFrom<global::Godot.Button>(this.@_clearButton);
            return true;
        }
        if (name == PropertyName.@_headerLabel) {
            value = global::Godot.NativeInterop.VariantUtils.CreateFrom<global::Godot.Label>(this.@_headerLabel);
            return true;
        }
        if (name == PropertyName.@_subheaderLabel) {
            value = global::Godot.NativeInterop.VariantUtils.CreateFrom<global::Godot.Label>(this.@_subheaderLabel);
            return true;
        }
        return base.GetGodotClassPropertyValue(name, out value);
    }
    /// <summary>
    /// Get the property information for all the properties declared in this class.
    /// This method is used by Godot to register the available properties in the editor.
    /// Do not call this method.
    /// </summary>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    internal new static global::System.Collections.Generic.List<global::Godot.Bridge.PropertyInfo> GetGodotPropertyList()
    {
        var properties = new global::System.Collections.Generic.List<global::Godot.Bridge.PropertyInfo>();
        properties.Add(new(type: (global::Godot.Variant.Type)24, name: PropertyName.@_categoryContainer, hint: (global::Godot.PropertyHint)0, hintString: "", usage: (global::Godot.PropertyUsageFlags)4096, exported: false));
        properties.Add(new(type: (global::Godot.Variant.Type)24, name: PropertyName.@_summaryLabel, hint: (global::Godot.PropertyHint)0, hintString: "", usage: (global::Godot.PropertyUsageFlags)4096, exported: false));
        properties.Add(new(type: (global::Godot.Variant.Type)24, name: PropertyName.@_savePresetButton, hint: (global::Godot.PropertyHint)0, hintString: "", usage: (global::Godot.PropertyUsageFlags)4096, exported: false));
        properties.Add(new(type: (global::Godot.Variant.Type)24, name: PropertyName.@_clearButton, hint: (global::Godot.PropertyHint)0, hintString: "", usage: (global::Godot.PropertyUsageFlags)4096, exported: false));
        properties.Add(new(type: (global::Godot.Variant.Type)24, name: PropertyName.@_headerLabel, hint: (global::Godot.PropertyHint)0, hintString: "", usage: (global::Godot.PropertyUsageFlags)4096, exported: false));
        properties.Add(new(type: (global::Godot.Variant.Type)24, name: PropertyName.@_subheaderLabel, hint: (global::Godot.PropertyHint)0, hintString: "", usage: (global::Godot.PropertyUsageFlags)4096, exported: false));
        return properties;
    }
#pragma warning restore CS0109
}

}
