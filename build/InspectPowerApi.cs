using System;
using System.Linq;
using System.Reflection;

static class InspectPowerApi
{
    static int Main()
    {
        try
        {
            Assembly sts2 = Assembly.LoadFrom(@"E:\SteamLibrary\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64\sts2.dll");
            DumpType(sts2, "MegaCrit.Sts2.Core.Models.Powers.ThornsPower");
            DumpType(sts2, "MegaCrit.Sts2.Core.Commands.PowerCmd");
            DumpType(sts2, "MegaCrit.Sts2.Core.Entities.Creatures.Creature");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return 1;
        }
    }

    static void DumpType(Assembly asm, string fullName)
    {
        Console.WriteLine($"TYPE {fullName}");
        Type? type = asm.GetType(fullName, throwOnError: false);
        if (type == null)
        {
            Console.WriteLine("  <not found>");
            return;
        }

        foreach (ConstructorInfo ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            Console.WriteLine($"  CTOR {type.Name}({string.Join(", ", ctor.GetParameters().Select(DescribeParameter))})");
        }

        foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            Console.WriteLine($"  PROP {prop.PropertyType.FullName} {prop.Name} writable={prop.CanWrite}");
        }

        foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            Console.WriteLine($"  FIELD {field.FieldType.FullName} {field.Name}");
        }

        foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly).OrderBy(m => m.Name))
        {
            Console.WriteLine($"  METHOD {method.ReturnType.FullName} {method.Name}({string.Join(", ", method.GetParameters().Select(DescribeParameter))})");
        }
    }

    static string DescribeParameter(ParameterInfo parameter)
    {
        return $"{parameter.ParameterType.FullName} {parameter.Name}";
    }
}
