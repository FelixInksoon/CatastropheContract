using Godot;
using Godot.NativeInterop;

namespace CatastropheContract {

partial class ContractPanelNode
{
#pragma warning disable CS0109 // Disable warning about redundant 'new' keyword
    /// <summary>
    /// Cached StringNames for the methods contained in this class, for fast lookup.
    /// </summary>
    public new class MethodName : global::Godot.PanelContainer.MethodName {
        /// <summary>
        /// Cached name for the '_Ready' method.
        /// </summary>
        public new static readonly global::Godot.StringName @_Ready = "_Ready";
        /// <summary>
        /// Cached name for the 'Rebuild' method.
        /// </summary>
        public new static readonly global::Godot.StringName @Rebuild = "Rebuild";
        /// <summary>
        /// Cached name for the 'RefreshSummary' method.
        /// </summary>
        public new static readonly global::Godot.StringName @RefreshSummary = "RefreshSummary";
        /// <summary>
        /// Cached name for the 'OnTierPressed' method.
        /// </summary>
        public new static readonly global::Godot.StringName @OnTierPressed = "OnTierPressed";
        /// <summary>
        /// Cached name for the 'OnSavePresetPressed' method.
        /// </summary>
        public new static readonly global::Godot.StringName @OnSavePresetPressed = "OnSavePresetPressed";
        /// <summary>
        /// Cached name for the 'OnClearPressed' method.
        /// </summary>
        public new static readonly global::Godot.StringName @OnClearPressed = "OnClearPressed";
    }
    /// <summary>
    /// Get the method information for all the methods declared in this class.
    /// This method is used by Godot to register the available methods in the editor.
    /// Do not call this method.
    /// </summary>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    internal new static global::System.Collections.Generic.List<global::Godot.Bridge.MethodInfo> GetGodotMethodList()
    {
        var methods = new global::System.Collections.Generic.List<global::Godot.Bridge.MethodInfo>(6);
        methods.Add(new(name: MethodName.@_Ready, returnVal: new(type: (global::Godot.Variant.Type)0, name: "", hint: (global::Godot.PropertyHint)0, hintString: "", usage: (global::Godot.PropertyUsageFlags)6, exported: false), flags: (global::Godot.MethodFlags)1, arguments: null, defaultArguments: null));
        methods.Add(new(name: MethodName.@Rebuild, returnVal: new(type: (global::Godot.Variant.Type)0, name: "", hint: (global::Godot.PropertyHint)0, hintString: "", usage: (global::Godot.PropertyUsageFlags)6, exported: false), flags: (global::Godot.MethodFlags)1, arguments: null, defaultArguments: null));
        methods.Add(new(name: MethodName.@RefreshSummary, returnVal: new(type: (global::Godot.Variant.Type)0, name: "", hint: (global::Godot.PropertyHint)0, hintString: "", usage: (global::Godot.PropertyUsageFlags)6, exported: false), flags: (global::Godot.MethodFlags)1, arguments: null, defaultArguments: null));
        methods.Add(new(name: MethodName.@OnTierPressed, returnVal: new(type: (global::Godot.Variant.Type)0, name: "", hint: (global::Godot.PropertyHint)0, hintString: "", usage: (global::Godot.PropertyUsageFlags)6, exported: false), flags: (global::Godot.MethodFlags)1, arguments: new() { new(type: (global::Godot.Variant.Type)4, name: "groupId", hint: (global::Godot.PropertyHint)0, hintString: "", usage: (global::Godot.PropertyUsageFlags)6, exported: false), new(type: (global::Godot.Variant.Type)4, name: "contractId", hint: (global::Godot.PropertyHint)0, hintString: "", usage: (global::Godot.PropertyUsageFlags)6, exported: false),  }, defaultArguments: null));
        methods.Add(new(name: MethodName.@OnSavePresetPressed, returnVal: new(type: (global::Godot.Variant.Type)0, name: "", hint: (global::Godot.PropertyHint)0, hintString: "", usage: (global::Godot.PropertyUsageFlags)6, exported: false), flags: (global::Godot.MethodFlags)1, arguments: null, defaultArguments: null));
        methods.Add(new(name: MethodName.@OnClearPressed, returnVal: new(type: (global::Godot.Variant.Type)0, name: "", hint: (global::Godot.PropertyHint)0, hintString: "", usage: (global::Godot.PropertyUsageFlags)6, exported: false), flags: (global::Godot.MethodFlags)1, arguments: null, defaultArguments: null));
        return methods;
    }
#pragma warning restore CS0109
    /// <inheritdoc/>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    protected override bool InvokeGodotClassMethod(in godot_string_name method, NativeVariantPtrArgs args, out godot_variant ret)
    {
        if (method == MethodName.@_Ready && args.Count == 0) {
            @_Ready();
            ret = default;
            return true;
        }
        if (method == MethodName.@Rebuild && args.Count == 0) {
            @Rebuild();
            ret = default;
            return true;
        }
        if (method == MethodName.@RefreshSummary && args.Count == 0) {
            @RefreshSummary();
            ret = default;
            return true;
        }
        if (method == MethodName.@OnTierPressed && args.Count == 2) {
            @OnTierPressed(global::Godot.NativeInterop.VariantUtils.ConvertTo<string>(args[0]), global::Godot.NativeInterop.VariantUtils.ConvertTo<string>(args[1]));
            ret = default;
            return true;
        }
        if (method == MethodName.@OnSavePresetPressed && args.Count == 0) {
            @OnSavePresetPressed();
            ret = default;
            return true;
        }
        if (method == MethodName.@OnClearPressed && args.Count == 0) {
            @OnClearPressed();
            ret = default;
            return true;
        }
        return base.InvokeGodotClassMethod(method, args, out ret);
    }
    /// <inheritdoc/>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    protected override bool HasGodotClassMethod(in godot_string_name method)
    {
        if (method == MethodName.@_Ready) {
           return true;
        }
        if (method == MethodName.@Rebuild) {
           return true;
        }
        if (method == MethodName.@RefreshSummary) {
           return true;
        }
        if (method == MethodName.@OnTierPressed) {
           return true;
        }
        if (method == MethodName.@OnSavePresetPressed) {
           return true;
        }
        if (method == MethodName.@OnClearPressed) {
           return true;
        }
        return base.HasGodotClassMethod(method);
    }
}

}
