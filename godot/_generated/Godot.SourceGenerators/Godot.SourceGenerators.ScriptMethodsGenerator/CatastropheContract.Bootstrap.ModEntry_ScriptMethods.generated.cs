using Godot;
using Godot.NativeInterop;

namespace CatastropheContract.Bootstrap {

partial class ModEntry
{
#pragma warning disable CS0109 // Disable warning about redundant 'new' keyword
    /// <summary>
    /// Cached StringNames for the methods contained in this class, for fast lookup.
    /// </summary>
    public new class MethodName : global::Godot.Node.MethodName {
        /// <summary>
        /// Cached name for the '_EnterTree' method.
        /// </summary>
        public new static readonly global::Godot.StringName @_EnterTree = "_EnterTree";
        /// <summary>
        /// Cached name for the '_ExitTree' method.
        /// </summary>
        public new static readonly global::Godot.StringName @_ExitTree = "_ExitTree";
    }
    /// <summary>
    /// Get the method information for all the methods declared in this class.
    /// This method is used by Godot to register the available methods in the editor.
    /// Do not call this method.
    /// </summary>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    internal new static global::System.Collections.Generic.List<global::Godot.Bridge.MethodInfo> GetGodotMethodList()
    {
        var methods = new global::System.Collections.Generic.List<global::Godot.Bridge.MethodInfo>(2);
        methods.Add(new(name: MethodName.@_EnterTree, returnVal: new(type: (global::Godot.Variant.Type)0, name: "", hint: (global::Godot.PropertyHint)0, hintString: "", usage: (global::Godot.PropertyUsageFlags)6, exported: false), flags: (global::Godot.MethodFlags)1, arguments: null, defaultArguments: null));
        methods.Add(new(name: MethodName.@_ExitTree, returnVal: new(type: (global::Godot.Variant.Type)0, name: "", hint: (global::Godot.PropertyHint)0, hintString: "", usage: (global::Godot.PropertyUsageFlags)6, exported: false), flags: (global::Godot.MethodFlags)1, arguments: null, defaultArguments: null));
        return methods;
    }
#pragma warning restore CS0109
    /// <inheritdoc/>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    protected override bool InvokeGodotClassMethod(in godot_string_name method, NativeVariantPtrArgs args, out godot_variant ret)
    {
        if (method == MethodName.@_EnterTree && args.Count == 0) {
            @_EnterTree();
            ret = default;
            return true;
        }
        if (method == MethodName.@_ExitTree && args.Count == 0) {
            @_ExitTree();
            ret = default;
            return true;
        }
        return base.InvokeGodotClassMethod(method, args, out ret);
    }
    /// <inheritdoc/>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    protected override bool HasGodotClassMethod(in godot_string_name method)
    {
        if (method == MethodName.@_EnterTree) {
           return true;
        }
        if (method == MethodName.@_ExitTree) {
           return true;
        }
        return base.HasGodotClassMethod(method);
    }
}

}
