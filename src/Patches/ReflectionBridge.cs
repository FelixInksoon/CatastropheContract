using System;
using System.Reflection;

namespace CatastropheContract.Patches;

internal static class ReflectionBridge
{
    public static string? TryReadCharacterId(object instance)
    {
        Type type = instance.GetType();

        foreach (string propertyName in new[] { "CharacterId", "SelectedCharacterId", "CurrentCharacterId" })
        {
            PropertyInfo? property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property?.GetValue(instance) is string stringValue && !string.IsNullOrWhiteSpace(stringValue))
            {
                return stringValue;
            }
        }

        foreach (string fieldName in new[] { "_characterId", "_selectedCharacterId", "characterId" })
        {
            FieldInfo? field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field?.GetValue(instance) is string stringValue && !string.IsNullOrWhiteSpace(stringValue))
            {
                return stringValue;
            }
        }

        return null;
    }
}
