using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using CatastropheContract.Core;
using HarmonyLib;

namespace CatastropheContract.Content;

internal static class ContractRuntimeReflection
{
    public static object? TryGetPlayer(object context)
    {
        return TryGetPlayerRecursive(context, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    public static IEnumerable<object> TryGetEnemies(object? context)
    {
        return TryGetEnemiesRecursive(context, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    public static object? TryGetByNames(object source, params string[] names)
    {
        Type type = source.GetType();
        foreach (string name in names)
        {
            PropertyInfo? property = AccessTools.Property(type, name);
            if (property != null)
            {
                try
                {
                    object? value = property.GetValue(source);
                    if (value != null)
                    {
                        return value;
                    }
                }
                catch
                {
                }
            }

            FieldInfo? field = AccessTools.Field(type, name);
            if (field != null)
            {
                try
                {
                    object? value = field.GetValue(source);
                    if (value != null)
                    {
                        return value;
                    }
                }
                catch
                {
                }
            }
        }

        return null;
    }

    public static double? TryGetNumber(object source, params string[] names)
    {
        object? value = TryGetByNames(source, names);
        if (value == null)
        {
            return null;
        }

        try
        {
            return Convert.ToDouble(value);
        }
        catch
        {
            return null;
        }
    }

    public static bool TrySetNumber(object source, double value, params string[] names)
    {
        Type type = source.GetType();
        foreach (string name in names)
        {
            PropertyInfo? property = AccessTools.Property(type, name);
            if (property != null && property.CanWrite)
            {
                try
                {
                    property.SetValue(source, Convert.ChangeType(value, property.PropertyType));
                    return true;
                }
                catch
                {
                }
            }

            FieldInfo? field = AccessTools.Field(type, name);
            if (field != null)
            {
                try
                {
                    field.SetValue(source, Convert.ChangeType(value, field.FieldType));
                    return true;
                }
                catch
                {
                }
            }
        }

        return false;
    }

    public static bool TryApplyPower(object? target, object? source, double amount, params string[] powerTypeNames)
    {
        if (target == null)
        {
            ModLogger.Debug("TryApplyPower skipped: target was null.");
            return false;
        }

        Type? powerCmdType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Commands.PowerCmd");
        Type? creatureType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Entities.Creatures.Creature");
        Type? cardModelType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Models.CardModel");
        Type? powerType = TryResolveTypeByNames(powerTypeNames);
        if (powerCmdType == null || creatureType == null || cardModelType == null || powerType == null)
        {
            ModLogger.Debug(
                $"TryApplyPower setup miss. powerCmdType={powerCmdType?.FullName ?? "<null>"}, creatureType={creatureType?.FullName ?? "<null>"}, cardModelType={cardModelType?.FullName ?? "<null>"}, powerType={powerType?.FullName ?? "<null>"}");
            return false;
        }

        if (!creatureType.IsInstanceOfType(target))
        {
            ModLogger.Debug($"TryApplyPower skipped: target type {target.GetType().FullName} is not a creature.");
            return false;
        }

        object powerSource = source != null && creatureType.IsInstanceOfType(source) ? source : target;
        List<MethodInfo> candidates = FindPowerApplyMethods(powerCmdType, target.GetType(), powerType, creatureType, cardModelType).ToList();
        if (candidates.Count == 0)
        {
            string powerCmdCandidates = string.Join(
                " || ",
                powerCmdType
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .Where(method => method.Name == "Apply")
                    .Select(DescribeMethod));
            string creatureCandidates = string.Join(
                " || ",
                target.GetType()
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(method => method.Name == "ApplyPower")
                    .Select(DescribeMethod));
            ModLogger.Warn(
                $"TryApplyPower found no matching apply method for {powerType.FullName}. PowerCmd candidates: {powerCmdCandidates}. Creature candidates: {creatureCandidates}");
            return false;
        }

        ModLogger.Debug($"TryApplyPower candidates for {powerType.FullName}: {string.Join(" || ", candidates.Select(DescribeMethod))}");

        List<string> failures = new();
        object? cachedPowerModel = null;
        bool powerModelInitialized = false;

        foreach (MethodInfo applyMethod in candidates)
        {
            try
            {
                bool needsPowerModel = applyMethod.GetParameters().Any(parameter => parameter.ParameterType.IsAssignableFrom(powerType));
                if (needsPowerModel && !powerModelInitialized)
                {
                    cachedPowerModel = TryCreatePowerInstance(powerType, target, powerSource, amount);
                    powerModelInitialized = true;
                }

                object?[] args = BuildApplyArgs(applyMethod, target, powerSource, powerType, cachedPowerModel, amount);
                object? invokeTarget = applyMethod.IsStatic ? null : target;

                ModLogger.Debug(
                    $"TryApplyPower invoking {DescribeMethod(applyMethod)} return={applyMethod.ReturnType.FullName} with power={powerType.FullName}, amount={amount}, target={target.GetType().FullName}, source={powerSource.GetType().FullName}");
                object? result = applyMethod.Invoke(invokeTarget, args);
                if (result is Task task)
                {
                    ModLogger.Debug($"TryApplyPower returned task for {powerType.FullName}. Status={task.Status} via {DescribeMethod(applyMethod)}");
                    if (task.IsFaulted)
                    {
                        Exception rootTaskException = task.Exception?.GetBaseException() ?? new InvalidOperationException("Power apply task faulted.");
                        throw rootTaskException;
                    }

                    AttachTaskLogging(task, powerType, target, applyMethod);
                    ModLogger.Info($"Post-apply power state for {target.GetType().FullName} (deferred): {DescribePowerState(target)}");
                    return true;
                }

                ModLogger.Info($"Post-apply power state for {target.GetType().FullName}: {DescribePowerState(target)}");
                return true;
            }
            catch (Exception exception)
            {
                Exception root = exception is TargetInvocationException targetInvocation && targetInvocation.InnerException != null
                    ? targetInvocation.InnerException
                    : exception;
                failures.Add($"{DescribeMethod(applyMethod)} => {root.GetType().Name}: {root.Message}");
            }
        }

        ModLogger.Warn($"TryApplyPower failed for {powerType.FullName}. Attempts: {string.Join(" || ", failures)}");
        return false;
    }

    private static object? TryGetPlayerRecursive(object? context, int depth, HashSet<object> visited)
    {
        if (context == null || depth > 3 || !visited.Add(context))
        {
            return null;
        }

        object? direct = TryGetByNames(context, "Player", "_player", "LocalPlayer", "CurrentPlayer", "Owner");
        if (direct != null && IsPlayerLike(direct))
        {
            return direct;
        }

        foreach (string nestedName in new[] { "State", "_state", "CombatState", "_combatState", "Tracker", "_tracker", "Room", "_room" })
        {
            object? nested = TryGetByNames(context, nestedName);
            object? found = TryGetPlayerRecursive(nested, depth + 1, visited);
            if (found != null)
            {
                return found;
            }
        }

        return direct;
    }

    private static IEnumerable<object> TryGetEnemiesRecursive(object? context, int depth, HashSet<object> visited)
    {
        if (context == null || depth > 3 || !visited.Add(context))
        {
            yield break;
        }

        object? player = TryGetPlayerRecursive(context, depth, visited);
        object? direct = TryGetByNames(
            context,
            "Enemies",
            "_enemies",
            "Monsters",
            "_monsters",
            "EnemyCreatures",
            "_enemyCreatures",
            "Creatures",
            "_creatures");

        foreach (object enemy in EnumerateCreatures(direct, player))
        {
            yield return enemy;
        }

        foreach (string nestedName in new[] { "State", "_state", "CombatState", "_combatState", "Tracker", "_tracker", "Room", "_room" })
        {
            object? nested = TryGetByNames(context, nestedName);
            foreach (object enemy in TryGetEnemiesRecursive(nested, depth + 1, visited))
            {
                yield return enemy;
            }
        }
    }

    private static IEnumerable<object> EnumerateCreatures(object? value, object? player)
    {
        if (value is not IEnumerable enumerable)
        {
            yield break;
        }

        foreach (object? item in enumerable)
        {
            if (item == null)
            {
                continue;
            }

            if (player != null && ReferenceEquals(item, player))
            {
                continue;
            }

            if (IsPlayerLike(item))
            {
                continue;
            }

            yield return item;
        }
    }

    private static bool IsPlayerLike(object instance)
    {
        string fullName = instance.GetType().FullName ?? string.Empty;
        return fullName.Contains("Player", StringComparison.OrdinalIgnoreCase)
            || fullName.Contains("LocalPlayer", StringComparison.OrdinalIgnoreCase);
    }

    private static Type? TryResolveTypeByNames(params string[] names)
    {
        foreach (string name in names)
        {
            Type? type = AccessTools.TypeByName(name);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    private static object TryCreatePowerInstance(Type type, object target, object source, double amount)
    {
        object? canonical = TryGetCanonicalPowerModel(type);
        if (canonical != null)
        {
            object? clone = TryCloneModelInstance(canonical);
            if (clone != null)
            {
                ModLogger.Debug($"Using cloned canonical power model from ModelDb for {type.FullName}.");
                return clone;
            }

            ModLogger.Debug($"Using canonical power model from ModelDb for {type.FullName} without clone fallback.");
            return canonical;
        }

        ConstructorInfo[] constructors = type
            .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .OrderByDescending(ScoreConstructor)
            .ThenBy(ctor => ctor.GetParameters().Length)
            .ToArray();

        List<string> failures = new();
        foreach (ConstructorInfo constructor in constructors)
        {
            if (!TryBuildConstructorArgs(constructor, target, source, amount, out object?[]? args))
            {
                continue;
            }

            try
            {
                ModLogger.Debug($"Trying ctor for {type.FullName}: {DescribeConstructor(constructor)}");
                return constructor.Invoke(args);
            }
            catch (Exception exception)
            {
                Exception root = exception is TargetInvocationException targetInvocation && targetInvocation.InnerException != null
                    ? targetInvocation.InnerException
                    : exception;
                failures.Add($"{DescribeConstructor(constructor)} => {root.GetType().Name}: {root.Message}");
            }
        }

        ConstructorInfo? emptyCtor = AccessTools.Constructor(type, Type.EmptyTypes);
        if (emptyCtor != null)
        {
            try
            {
                return emptyCtor.Invoke(null);
            }
            catch (Exception exception)
            {
                Exception root = exception is TargetInvocationException targetInvocation && targetInvocation.InnerException != null
                    ? targetInvocation.InnerException
                    : exception;
                failures.Add($"{DescribeConstructor(emptyCtor)} => {root.GetType().Name}: {root.Message}");
            }
        }

        if (failures.Count > 0)
        {
            ModLogger.Warn($"Power ctor attempts for {type.FullName} failed: {string.Join(" || ", failures)}");
        }

        return FormatterServices.GetUninitializedObject(type);
    }

    private static object? TryGetCanonicalPowerModel(Type powerType)
    {
        Type? modelDbType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Models.ModelDb");
        if (modelDbType == null)
        {
            return null;
        }

        object? allPowers = TryGetStaticByNames(modelDbType, "AllPowers", "_allPowers");
        foreach (object candidate in EnumerateObjects(allPowers))
        {
            if (powerType.IsInstanceOfType(candidate))
            {
                return candidate;
            }
        }

        MethodInfo? getEntry = modelDbType
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .FirstOrDefault(method =>
                method.Name == "GetEntry"
                && method.GetParameters().Length == 1
                && method.ReturnType != typeof(void));
        if (getEntry != null)
        {
            foreach (string key in BuildPowerLookupKeys(powerType))
            {
                try
                {
                    object? candidate = getEntry.Invoke(null, new object?[] { key });
                    if (candidate != null && powerType.IsInstanceOfType(candidate))
                    {
                        return candidate;
                    }
                }
                catch
                {
                }
            }
        }

        return null;
    }

    private static IEnumerable<object> EnumerateObjects(object? container)
    {
        if (container == null)
        {
            yield break;
        }

        if (container is IEnumerable enumerable)
        {
            foreach (object? item in enumerable)
            {
                if (item == null)
                {
                    continue;
                }

                yield return item;
            }

            yield break;
        }

        object? values = TryGetByNames(container, "Values", "_values");
        if (values is IEnumerable valueEnumerable)
        {
            foreach (object? item in valueEnumerable)
            {
                if (item == null)
                {
                    continue;
                }

                yield return item;
            }
        }
    }

    private static IEnumerable<string> BuildPowerLookupKeys(Type powerType)
    {
        string name = powerType.Name;
        yield return name;

        if (name.EndsWith("Power", StringComparison.Ordinal))
        {
            yield return name[..^"Power".Length];
        }

        if (!string.IsNullOrWhiteSpace(powerType.FullName))
        {
            yield return powerType.FullName;
        }
    }

    private static object? TryGetStaticByNames(Type type, params string[] names)
    {
        foreach (string name in names)
        {
            PropertyInfo? property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (property != null)
            {
                try
                {
                    object? value = property.GetValue(null);
                    if (value != null)
                    {
                        return value;
                    }
                }
                catch
                {
                }
            }

            FieldInfo? field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (field != null)
            {
                try
                {
                    object? value = field.GetValue(null);
                    if (value != null)
                    {
                        return value;
                    }
                }
                catch
                {
                }
            }
        }

        return null;
    }

    private static object? TryCloneModelInstance(object model)
    {
        Type type = model.GetType();

        foreach (string methodName in new[] { "<Clone>$", "Clone", "MemberwiseClone" })
        {
            MethodInfo? method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null || method.GetParameters().Length != 0)
            {
                continue;
            }

            try
            {
                object? clone = method.Invoke(model, null);
                if (clone != null && type.IsInstanceOfType(clone))
                {
                    return clone;
                }
            }
            catch
            {
            }
        }

        MethodInfo? objectMemberwiseClone = typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance);
        if (objectMemberwiseClone != null)
        {
            try
            {
                object? clone = objectMemberwiseClone.Invoke(model, null);
                if (clone != null && type.IsInstanceOfType(clone))
                {
                    return clone;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static string DescribePowerState(object target)
    {
        object? powers = TryGetByNames(target, "AllPowers", "Powers", "_powers", "PowerStates", "_powerStates");
        if (powers is not IEnumerable enumerable)
        {
            return "<no enumerable powers found>";
        }

        List<string> summaries = new();
        foreach (object? item in enumerable)
        {
            if (item == null)
            {
                continue;
            }

            string typeName = item.GetType().FullName ?? item.GetType().Name;
            double? amount = TryGetNumber(item, "Amount", "_amount", "Stacks", "_stacks", "Value", "_value");
            summaries.Add(amount.HasValue ? $"{typeName}:{amount.Value:0.##}" : typeName);
        }

        return summaries.Count == 0 ? "<empty>" : string.Join(" | ", summaries);
    }

    private static bool TryBuildConstructorArgs(ConstructorInfo constructor, object target, object source, double amount, out object?[]? args)
    {
        ParameterInfo[] parameters = constructor.GetParameters();
        args = new object?[parameters.Length];
        bool assignedCreature = false;

        for (int i = 0; i < parameters.Length; i++)
        {
            ParameterInfo parameter = parameters[i];
            Type parameterType = parameter.ParameterType;
            string parameterName = parameter.Name ?? string.Empty;

            if (parameterType.IsInstanceOfType(target) || parameterType.IsAssignableFrom(target.GetType()))
            {
                if (!assignedCreature || parameterName.Contains("owner", StringComparison.OrdinalIgnoreCase) || parameterName.Contains("target", StringComparison.OrdinalIgnoreCase))
                {
                    args[i] = target;
                    assignedCreature = true;
                    continue;
                }
            }

            if (parameterType.IsInstanceOfType(source) || parameterType.IsAssignableFrom(source.GetType()))
            {
                args[i] = source;
                continue;
            }

            if (parameterType == typeof(int))
            {
                args[i] = Convert.ToInt32(amount);
                continue;
            }

            if (parameterType == typeof(float))
            {
                args[i] = (float)amount;
                continue;
            }

            if (parameterType == typeof(double))
            {
                args[i] = amount;
                continue;
            }

            if (parameterType == typeof(decimal))
            {
                args[i] = Convert.ToDecimal(amount);
                continue;
            }

            if (parameterType == typeof(bool))
            {
                args[i] = false;
                continue;
            }

            if (parameterType.IsEnum)
            {
                args[i] = Activator.CreateInstance(parameterType);
                continue;
            }

            if (parameter.HasDefaultValue)
            {
                args[i] = parameter.DefaultValue;
                continue;
            }

            if (!parameterType.IsValueType)
            {
                args[i] = null;
                continue;
            }

            try
            {
                args[i] = Activator.CreateInstance(parameterType);
            }
            catch
            {
                args = null;
                return false;
            }
        }

        return true;
    }

    private static int ScoreConstructor(ConstructorInfo constructor)
    {
        int score = 0;
        foreach (ParameterInfo parameter in constructor.GetParameters())
        {
            Type type = parameter.ParameterType;
            if (type.FullName?.Contains("Creature", StringComparison.OrdinalIgnoreCase) == true)
            {
                score += 4;
            }
            else if (type == typeof(int) || type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            {
                score += 3;
            }
            else if (type == typeof(bool))
            {
                score += 1;
            }
        }

        return score;
    }

    private static IEnumerable<MethodInfo> FindPowerApplyMethods(Type powerCmdType, Type targetType, Type powerType, Type creatureType, Type cardModelType)
    {
        IEnumerable<MethodInfo> powerCmdMethods = powerCmdType
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(method => method.Name == "Apply")
            .Select(method => TryMakeGenericApplyMethod(method, powerType) ?? method)
            .Where(method => IsCompatibleApplyMethod(method, powerType, creatureType, cardModelType, "Apply", requireStatic: true));

        IEnumerable<MethodInfo> creatureMethods = targetType
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(method => method.Name == "ApplyPower")
            .Select(method => TryMakeGenericApplyMethod(method, powerType) ?? method)
            .Where(method => IsCompatibleApplyMethod(method, powerType, creatureType, cardModelType, "ApplyPower", requireStatic: false));

        return powerCmdMethods
            .Concat(creatureMethods)
            .OrderBy(method => method.GetParameters().Any(parameter => parameter.ParameterType.IsAssignableFrom(powerType)) ? 1 : 0)
            .ThenBy(method => ScoreApplyMethod(method, creatureType))
            .ThenBy(method => method.IsStatic ? 0 : 1)
            .ThenBy(method => method.GetParameters().Length);
    }

    private static MethodInfo? TryMakeGenericApplyMethod(MethodInfo method, Type powerType)
    {
        if (!method.IsGenericMethodDefinition || method.GetGenericArguments().Length != 1)
        {
            return null;
        }

        try
        {
            return method.MakeGenericMethod(powerType);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsCompatibleApplyMethod(Type powerType, Type creatureType, Type cardModelType, string expectedName, bool requireStatic, MethodInfo method)
    {
        return IsCompatibleApplyMethod(method, powerType, creatureType, cardModelType, expectedName, requireStatic);
    }

    private static bool IsCompatibleApplyMethod(MethodInfo method, Type powerType, Type creatureType, Type cardModelType, string expectedName, bool requireStatic)
    {
        if (method.Name != expectedName || method.IsStatic != requireStatic)
        {
            return false;
        }

        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length == 0)
        {
            return false;
        }

        Type firstParameterType = parameters[0].ParameterType;
        if (firstParameterType != typeof(string)
            && firstParameterType != typeof(byte[])
            && typeof(IEnumerable).IsAssignableFrom(firstParameterType))
        {
            return false;
        }

        bool hasPowerModel = parameters.Any(parameter => parameter.ParameterType.IsAssignableFrom(powerType));
        bool hasCreature = parameters.Any(parameter => parameter.ParameterType.IsAssignableFrom(creatureType));
        bool hasAmount = parameters.Any(parameter => IsNumericLike(parameter.ParameterType));
        bool hasTargetLikeParameter = parameters.Any(parameter => IsTargetLikeParameter(parameter, creatureType));

        if (!hasCreature || !hasAmount || !hasTargetLikeParameter)
        {
            return false;
        }

        if (hasPowerModel)
        {
            return true;
        }

        return method.IsGenericMethod || method.ContainsGenericParameters == false;
    }

    private static bool IsTargetLikeParameter(ParameterInfo parameter, Type creatureType)
    {
        if (parameter.ParameterType.IsAssignableFrom(creatureType))
        {
            return true;
        }

        string name = parameter.Name ?? string.Empty;
        if (name.Contains("target", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        Type parameterType = parameter.ParameterType;
        if (parameterType == typeof(IEnumerable) || typeof(IEnumerable).IsAssignableFrom(parameterType))
        {
            return name.Contains("target", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static int ScoreApplyMethod(MethodInfo method, Type creatureType)
    {
        int score = 0;
        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length > 0)
        {
            ParameterInfo first = parameters[0];
            if (first.ParameterType == creatureType)
            {
                score -= 5;
            }
            else if (first.ParameterType.IsAssignableFrom(creatureType))
            {
                score -= 4;
            }
            else if (first.Name?.Contains("target", StringComparison.OrdinalIgnoreCase) == true)
            {
                score -= 2;
            }
            else if (typeof(IEnumerable).IsAssignableFrom(first.ParameterType))
            {
                score += 2;
            }
        }

        return score;
    }

    private static object?[] BuildApplyArgs(MethodInfo applyMethod, object target, object powerSource, Type powerType, object? powerModel, double amount)
    {
        ParameterInfo[] parameters = applyMethod.GetParameters();
        object?[] args = new object?[parameters.Length];
        bool assignedTargetCreature = false;

        for (int i = 0; i < parameters.Length; i++)
        {
            Type parameterType = parameters[i].ParameterType;
            if (parameterType.IsAssignableFrom(powerType))
            {
                args[i] = powerModel;
            }
            else if (parameterType.IsAssignableFrom(target.GetType()))
            {
                args[i] = assignedTargetCreature ? powerSource : target;
                assignedTargetCreature = true;
            }
            else if (IsNumericLike(parameterType))
            {
                args[i] = ConvertNumericAmount(amount, parameterType);
            }
            else if (parameterType == typeof(bool))
            {
                args[i] = false;
            }
            else
            {
                args[i] = null;
            }
        }

        return args;
    }

    private static bool IsNumericLike(Type type)
    {
        return type == typeof(decimal) || type == typeof(double) || type == typeof(float) || type == typeof(int);
    }

    private static object ConvertNumericAmount(double amount, Type targetType)
    {
        if (targetType == typeof(decimal))
        {
            return Convert.ToDecimal(amount);
        }

        if (targetType == typeof(float))
        {
            return (float)amount;
        }

        if (targetType == typeof(int))
        {
            return Convert.ToInt32(amount);
        }

        return amount;
    }

    private static void AttachTaskLogging(Task task, Type powerType, object target, MethodInfo applyMethod)
    {
        _ = task.ContinueWith(
            completedTask =>
            {
                if (completedTask.IsFaulted)
                {
                    Exception root = completedTask.Exception?.GetBaseException() ?? new InvalidOperationException("Power apply task faulted.");
                    ModLogger.Warn($"Deferred power task failed for {powerType.FullName} via {DescribeMethod(applyMethod)}: {root.GetType().Name}: {root.Message}");
                    return;
                }

                ModLogger.Info($"Deferred post-apply power state for {target.GetType().FullName}: {DescribePowerState(target)}");
            },
            TaskScheduler.Default);
    }

    private static string DescribeMethod(MethodInfo method)
    {
        string parameters = string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.Name + " " + parameter.Name));
        return $"{method.DeclaringType?.FullName}.{method.Name}({parameters})";
    }

    private static string DescribeConstructor(ConstructorInfo constructor)
    {
        string parameters = string.Join(", ", constructor.GetParameters().Select(parameter => parameter.ParameterType.Name + " " + parameter.Name));
        return $"{constructor.DeclaringType?.FullName}({parameters})";
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();

        public new bool Equals(object? x, object? y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(object obj)
        {
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
