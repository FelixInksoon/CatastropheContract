using Godot;
using Godot.NativeInterop;

namespace CatastropheContract {

partial class MainFile
{
    /// <inheritdoc/>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    protected override void SaveGodotObjectData(global::Godot.Bridge.GodotSerializationInfo info)
    {
        base.SaveGodotObjectData(info);
        info.AddProperty(PropertyName.@_scanCooldown, global::Godot.Variant.From<double>(this.@_scanCooldown));
    }
    /// <inheritdoc/>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    protected override void RestoreGodotObjectData(global::Godot.Bridge.GodotSerializationInfo info)
    {
        base.RestoreGodotObjectData(info);
        if (info.TryGetProperty(PropertyName.@_scanCooldown, out var _value__scanCooldown))
            this.@_scanCooldown = _value__scanCooldown.As<double>();
    }
}

}
