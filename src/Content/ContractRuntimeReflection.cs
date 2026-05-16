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
    private const int MaxTraversalDepth = 3;
    private const int MaxCollectionProbeItems = 16;
    private static readonly string[] DirectPlayerMemberNames =
    {
        "Player", "_player", "LocalPlayer", "CurrentPlayer", "Owner", "Character", "CurrentCharacter", "Hero", "ControlledCreature"
    };
    private static readonly string[] DirectCreatureMemberNames =
    {
        "Creature", "_creature", "Character", "CurrentCharacter", "ControlledCreature", "_controlledCreature",
        "CombatCreature", "_combatCreature", "Combatant", "_combatant", "Entity", "_entity", "Avatar", "_avatar"
    };
    private static readonly string[] PlayerCollectionMemberNames =
    {
        "Players", "_players", "Creatures", "_creatures", "AllCreatures", "_allCreatures", "FriendlyCreatures", "_friendlyCreatures",
        "Combatants", "_combatants", "Participants", "_participants", "Entities", "_entities"
    };
    private static readonly string[] NestedTraversalMemberNames =
    {
        "State", "_state", "CombatState", "_combatState", "Tracker", "_tracker", "Room", "_room", "Run", "_run", "Encounter", "_encounter"
    };
    private static readonly string[] StaticPlayerAssemblyNames = { "sts2" };
    private static readonly object StaticPlayerMembersLock = new();
    private static List<MemberInfo>? _staticPlayerMembers;

    public static object? TryGetPlayer(object context)
    {
        return TryGetPlayerFromCandidates(context);
    }

    public static object? TryGetPlayerFromCandidates(params object?[] candidates)
    {
        HashSet<object> visited = new(ReferenceEqualityComparer.Instance);
        foreach (object? candidate in candidates)
        {
            object? player = TryGetPlayerRecursive(candidate, 0, visited);
            if (player != null)
            {
                return player;
            }
        }

        return TryGetPlayerFromStaticMembers();
    }

    public static IEnumerable<object> TryGetEnemies(object? context)
    {
        object? player = context == null ? null : TryGetPlayer(context);
        return TryGetEnemiesRecursive(
            context,
            player,
            0,
            new HashSet<object>(ReferenceEqualityComparer.Instance),
            new HashSet<object>(ReferenceEqualityComparer.Instance));
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
        foreach (object candidate in EnumerateMutationTargets(source))
        {
            object? value = TryGetByNames(candidate, names);
            if (value == null)
            {
                continue;
            }

            try
            {
                return Convert.ToDouble(value);
            }
            catch
            {
            }
        }

        return null;
    }

    public static bool TrySetNumber(object source, double value, params string[] names)
    {
        foreach (object candidate in EnumerateMutationTargets(source))
        {
            Type type = candidate.GetType();
            foreach (string name in names)
            {
                PropertyInfo? property = AccessTools.Property(type, name);
                if (property != null && property.CanWrite)
                {
                    try
                    {
                        property.SetValue(candidate, Convert.ChangeType(value, property.PropertyType));
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
                        field.SetValue(candidate, Convert.ChangeType(value, field.FieldType));
                        return true;
                    }
                    catch
                    {
                    }
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

        object? resolvedTarget = TryResolveCreatureTarget(target, creatureType);
        if (resolvedTarget == null)
        {
            ModLogger.Debug($"TryApplyPower skipped: target type {target.GetType().FullName} is not a creature.");
            return false;
        }

        object powerSource = TryResolveCreatureTarget(source, creatureType) ?? resolvedTarget;
        List<MethodInfo> candidates = FindPowerApplyMethods(powerCmdType, resolvedTarget.GetType(), powerType, creatureType, cardModelType).ToList();
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
                resolvedTarget.GetType()
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
                    cachedPowerModel = TryCreatePowerInstance(powerType, resolvedTarget, powerSource, amount);
                    powerModelInitialized = true;
                }

                object?[] args = BuildApplyArgs(applyMethod, resolvedTarget, powerSource, powerType, cachedPowerModel, amount);
                object? invokeTarget = applyMethod.IsStatic ? null : resolvedTarget;

                ModLogger.Debug(
                    $"TryApplyPower invoking {DescribeMethod(applyMethod)} return={applyMethod.ReturnType.FullName} with power={powerType.FullName}, amount={amount}, target={resolvedTarget.GetType().FullName}, source={powerSource.GetType().FullName}");
                object? result = applyMethod.Invoke(invokeTarget, args);
                if (result is Task task)
                {
                    ModLogger.Debug($"TryApplyPower returned task for {powerType.FullName}. Status={task.Status} via {DescribeMethod(applyMethod)}");
                    if (task.IsFaulted)
                    {
                        Exception rootTaskException = task.Exception?.GetBaseException() ?? new InvalidOperationException("Power apply task faulted.");
                        throw rootTaskException;
                    }

                    AttachTaskLogging(task, powerType, resolvedTarget, applyMethod);
                    ModLogger.Info($"Post-apply power state for {resolvedTarget.GetType().FullName} (deferred): {DescribePowerState(resolvedTarget)}");
                    return true;
                }

                ModLogger.Info($"Post-apply power state for {resolvedTarget.GetType().FullName}: {DescribePowerState(resolvedTarget)}");
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

    public static bool TryHeal(object? target, object? source, double amount)
    {
        if (target == null || amount <= 0.001d)
        {
            return false;
        }

        Type? creatureType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Entities.Creatures.Creature");
        Type? creatureCmdType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Commands.CreatureCmd");
        if (creatureType == null)
        {
            ModLogger.Debug("TryHeal skipped: creature type was not found.");
            return false;
        }

        object? resolvedTarget = TryResolveCreatureTarget(target, creatureType);
        if (resolvedTarget == null)
        {
            ModLogger.Debug($"TryHeal skipped: target type {target.GetType().FullName} is not a creature.");
            return false;
        }

        object? resolvedSource = TryResolveCreatureTarget(source, creatureType) ?? resolvedTarget;
        Type targetType = resolvedTarget.GetType();
        List<MethodInfo> candidates = new();
        if (creatureCmdType != null)
        {
            candidates.AddRange(
                creatureCmdType
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .Where(method => IsCompatibleHealMethod(method, creatureType, requireStatic: true)));
        }

        candidates.AddRange(
            targetType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(method => IsCompatibleHealMethod(method, creatureType, requireStatic: false)));

        candidates = candidates
            .Distinct()
            .OrderBy(method => ScoreHealMethod(method, creatureType))
            .ThenBy(method => method.IsStatic ? 1 : 0)
            .ThenBy(method => method.GetParameters().Length)
            .ToList();

        if (candidates.Count == 0)
        {
            ModLogger.Debug("TryHeal found no compatible Heal methods.");
            return false;
        }

        List<string> failures = new();
        foreach (MethodInfo healMethod in candidates)
        {
            try
            {
                object?[] args = BuildHealArgs(healMethod, resolvedTarget, resolvedSource, amount);
                object? invokeTarget = healMethod.IsStatic ? null : resolvedTarget;
                object? result = healMethod.Invoke(invokeTarget, args);
                if (result is Task task)
                {
                    if (task.IsFaulted)
                    {
                        Exception rootTaskException = task.Exception?.GetBaseException() ?? new InvalidOperationException("Heal task faulted.");
                        throw rootTaskException;
                    }

                    task.GetAwaiter().GetResult();
                }

                return true;
            }
            catch (Exception exception)
            {
                Exception root = exception is TargetInvocationException targetInvocation && targetInvocation.InnerException != null
                    ? targetInvocation.InnerException
                    : exception;
                failures.Add($"{DescribeMethod(healMethod)} => {root.GetType().Name}: {root.Message}");
            }
        }

        ModLogger.Warn($"TryHeal failed for {resolvedTarget.GetType().FullName}. Attempts: {string.Join(" || ", failures)}");
        return false;
    }

    public static object? TryGetCreature(object? context)
    {
        Type? creatureType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Entities.Creatures.Creature");
        if (creatureType == null)
        {
            return null;
        }

        return TryResolveCreatureTarget(context, creatureType);
    }

    private static object? TryGetPlayerRecursive(object? context, int depth, HashSet<object> visited)
    {
        if (context == null || depth > MaxTraversalDepth || !visited.Add(context))
        {
            return null;
        }

        if (IsPlayerLike(context))
        {
            return context;
        }

        object? direct = TryGetByNames(context, DirectPlayerMemberNames);
        if (direct != null)
        {
            object? foundDirect = TryGetPlayerRecursive(direct, depth + 1, visited);
            if (foundDirect != null)
            {
                return foundDirect;
            }
        }

        object? collectionPlayer = TryGetPlayerFromCollections(context, depth, visited);
        if (collectionPlayer != null)
        {
            return collectionPlayer;
        }

        foreach (string nestedName in NestedTraversalMemberNames)
        {
            object? nested = TryGetByNames(context, nestedName);
            object? found = TryGetPlayerRecursive(nested, depth + 1, visited);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static object? TryGetPlayerFromCollections(object context, int depth, HashSet<object> visited)
    {
        foreach (string collectionName in PlayerCollectionMemberNames)
        {
            object? collection = TryGetByNames(context, collectionName);
            object? player = TryGetPlayerFromEnumerable(collection, depth, visited);
            if (player != null)
            {
                return player;
            }
        }

        return null;
    }

    private static object? TryGetPlayerFromEnumerable(object? value, int depth, HashSet<object> visited)
    {
        if (value is not IEnumerable enumerable || value is string)
        {
            return null;
        }

        int inspected = 0;
        foreach (object? item in enumerable)
        {
            if (item == null)
            {
                continue;
            }

            if (IsPlayerLike(item))
            {
                return item;
            }

            object? found = TryGetPlayerRecursive(item, depth + 1, visited);
            if (found != null)
            {
                return found;
            }

            inspected += 1;
            if (inspected >= MaxCollectionProbeItems)
            {
                break;
            }
        }

        return null;
    }

    private static IEnumerable<object> TryGetEnemiesRecursive(
        object? context,
        object? player,
        int depth,
        HashSet<object> visited,
        HashSet<object> yielded)
    {
        if (context == null || depth > MaxTraversalDepth || !visited.Add(context))
        {
            yield break;
        }

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
            if (yielded.Add(enemy))
            {
                yield return enemy;
            }
        }

        foreach (string nestedName in NestedTraversalMemberNames)
        {
            object? nested = TryGetByNames(context, nestedName);
            foreach (object enemy in TryGetEnemiesRecursive(nested, player, depth + 1, visited, yielded))
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
        return fullName.Contains(".Entities.Players.", StringComparison.OrdinalIgnoreCase)
            || fullName.EndsWith(".Player", StringComparison.OrdinalIgnoreCase)
            || fullName.Contains("Player", StringComparison.OrdinalIgnoreCase)
            || fullName.Contains("LocalPlayer", StringComparison.OrdinalIgnoreCase);
    }

    private static object? TryResolveCreatureTarget(object? candidate, Type creatureType)
    {
        return TryResolveCreatureTarget(candidate, creatureType, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    private static object? TryResolveCreatureTarget(object? candidate, Type creatureType, int depth, HashSet<object> visited)
    {
        if (candidate == null || depth > MaxTraversalDepth || !visited.Add(candidate))
        {
            return null;
        }

        if (creatureType.IsInstanceOfType(candidate))
        {
            return candidate;
        }

        object? direct = TryGetByNames(candidate, DirectCreatureMemberNames);
        if (direct != null)
        {
            object? resolved = TryResolveCreatureTarget(direct, creatureType, depth + 1, visited);
            if (resolved != null)
            {
                return resolved;
            }
        }

        foreach (string nestedName in NestedTraversalMemberNames)
        {
            object? nested = TryGetByNames(candidate, nestedName);
            object? resolved = TryResolveCreatureTarget(nested, creatureType, depth + 1, visited);
            if (resolved != null)
            {
                return resolved;
            }
        }

        return null;
    }

    private static IEnumerable<object> EnumerateMutationTargets(object source)
    {
        HashSet<object> yielded = new(ReferenceEqualityComparer.Instance);
        foreach (object candidate in EnumerateMutationTargetsRecursive(source, 0, yielded))
        {
            yield return candidate;
        }
    }

    private static IEnumerable<object> EnumerateMutationTargetsRecursive(object? source, int depth, HashSet<object> yielded)
    {
        if (source == null || depth > MaxTraversalDepth || !yielded.Add(source))
        {
            yield break;
        }

        yield return source;

        foreach (string name in DirectCreatureMemberNames)
        {
            object? nested = TryGetByNames(source, name);
            foreach (object candidate in EnumerateMutationTargetsRecursive(nested, depth + 1, yielded))
            {
                yield return candidate;
            }
        }

        foreach (string name in DirectPlayerMemberNames)
        {
            object? nested = TryGetByNames(source, name);
            foreach (object candidate in EnumerateMutationTargetsRecursive(nested, depth + 1, yielded))
            {
                yield return candidate;
            }
        }
    }

    private static object? TryGetPlayerFromStaticMembers()
    {
        foreach (MemberInfo member in GetStaticPlayerMembers())
        {
            object? value = TryGetStaticMemberValue(member);
            if (value == null)
            {
                continue;
            }

            object? direct = TryGetPlayerRecursive(value, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));
            if (direct != null)
            {
                return direct;
            }

            object? collectionPlayer = TryGetPlayerFromEnumerable(value, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));
            if (collectionPlayer != null)
            {
                return collectionPlayer;
            }
        }

        return null;
    }

    private static IEnumerable<MemberInfo> GetStaticPlayerMembers()
    {
        if (_staticPlayerMembers != null)
        {
            return _staticPlayerMembers;
        }

        lock (StaticPlayerMembersLock)
        {
            if (_staticPlayerMembers != null)
            {
                return _staticPlayerMembers;
            }

            List<MemberInfo> members = new();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string? assemblyName = assembly.GetName().Name;
                if (assemblyName == null || !StaticPlayerAssemblyNames.Contains(assemblyName, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (Type type in GetTypesSafe(assembly))
                {
                    AddMatchingStaticMembers(type, members);
                }
            }

            _staticPlayerMembers = members;
            ModLogger.Info($"Cached {_staticPlayerMembers.Count} static player member candidates for runtime lookup.");
            return _staticPlayerMembers;
        }
    }

    private static void AddMatchingStaticMembers(Type type, ICollection<MemberInfo> members)
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        foreach (PropertyInfo property in type.GetProperties(Flags))
        {
            if (property.GetMethod == null || property.GetIndexParameters().Length != 0)
            {
                continue;
            }

            if (!DirectPlayerMemberNames.Contains(property.Name, StringComparer.Ordinal))
            {
                continue;
            }

            members.Add(property);
        }

        foreach (FieldInfo field in type.GetFields(Flags))
        {
            if (!DirectPlayerMemberNames.Contains(field.Name, StringComparer.Ordinal))
            {
                continue;
            }

            members.Add(field);
        }
    }

    private static object? TryGetStaticMemberValue(MemberInfo member)
    {
        try
        {
            if (member is PropertyInfo property)
            {
                return property.GetValue(null);
            }

            if (member is FieldInfo field)
            {
                return field.GetValue(null);
            }
        }
        catch
        {
        }

        return null;
    }

    private static IEnumerable<Type> GetTypesSafe(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type != null)!;
        }
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
            .ThenBy(method => method.IsStatic ? 1 : 0)
            .ThenBy(method => method.GetParameters().Length);
    }

    private static bool IsCompatibleHealMethod(MethodInfo method, Type creatureType, bool requireStatic)
    {
        if (method.Name != "Heal" || method.IsStatic != requireStatic)
        {
            return false;
        }

        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length == 0)
        {
            return false;
        }

        bool hasCreature = parameters.Any(parameter => parameter.ParameterType.IsAssignableFrom(creatureType));
        bool hasAmount = parameters.Any(parameter => IsNumericLike(parameter.ParameterType));
        return hasCreature && hasAmount;
    }

    private static int ScoreHealMethod(MethodInfo method, Type creatureType)
    {
        int score = 0;
        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length > 0)
        {
            ParameterInfo first = parameters[0];
            if (first.ParameterType == creatureType)
            {
                score -= 4;
            }
            else if (first.ParameterType.IsAssignableFrom(creatureType))
            {
                score -= 3;
            }
        }

        if (parameters.Any(parameter => parameter.Name?.Contains("amount", StringComparison.OrdinalIgnoreCase) == true))
        {
            score -= 2;
        }

        return score;
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

    private static object?[] BuildHealArgs(MethodInfo healMethod, object target, object source, double amount)
    {
        ParameterInfo[] parameters = healMethod.GetParameters();
        object?[] args = new object?[parameters.Length];
        bool assignedTargetCreature = false;

        for (int i = 0; i < parameters.Length; i++)
        {
            ParameterInfo parameter = parameters[i];
            Type parameterType = parameter.ParameterType;
            string parameterName = parameter.Name ?? string.Empty;

            if (parameterType.IsAssignableFrom(target.GetType()))
            {
                if (!assignedTargetCreature || parameterName.Contains("target", StringComparison.OrdinalIgnoreCase) || parameterName.Contains("owner", StringComparison.OrdinalIgnoreCase))
                {
                    args[i] = target;
                    assignedTargetCreature = true;
                    continue;
                }

                args[i] = source;
                continue;
            }

            if (parameterType.IsAssignableFrom(source.GetType()))
            {
                args[i] = source;
                continue;
            }

            if (IsNumericLike(parameterType))
            {
                args[i] = ConvertNumericAmount(amount, parameterType);
                continue;
            }

            if (parameterType == typeof(bool))
            {
                args[i] = false;
                continue;
            }

            if (parameter.HasDefaultValue)
            {
                args[i] = parameter.DefaultValue;
                continue;
            }

            args[i] = parameterType.IsValueType ? Activator.CreateInstance(parameterType) : null;
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
