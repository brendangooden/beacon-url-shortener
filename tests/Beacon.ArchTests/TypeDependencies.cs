using System.Reflection;
using System.Reflection.Emit;

namespace Beacon.ArchTests;

/// <summary>
/// Everything a type depends on, found by reflection plus an IL scan: base type, interfaces,
/// fields, properties, method and constructor signatures, locals, and every type, method, and field
/// the method bodies reference (so a call to another feature's static helper or a <c>new</c> of its
/// DTO inside a lambda is caught, not only signatures). Generic arguments and array/by-ref element
/// types are unwrapped.
/// </summary>
public static class TypeDependencies
{
    private const BindingFlags Declared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(o => o.Value);

    public static IReadOnlySet<Type> Of(Type type)
    {
        var found = new HashSet<Type>();

        Add(found, type.BaseType);
        foreach (var i in type.GetInterfaces())
        {
            Add(found, i);
        }

        foreach (var f in type.GetFields(Declared))
        {
            Add(found, f.FieldType);
        }

        foreach (var p in type.GetProperties(Declared))
        {
            Add(found, p.PropertyType);
        }

        foreach (var m in type.GetMethods(Declared))
        {
            Add(found, m.ReturnType);
            AddBody(found, m);
        }

        foreach (var c in type.GetConstructors(Declared))
        {
            AddBody(found, c);
        }

        found.Remove(type);
        return found;
    }

    /// <summary>The methods a method body calls (for "who maps routes" checks).</summary>
    public static IEnumerable<MethodBase> CalledMethods(MethodBase method) =>
        ReferencedMembers(method).OfType<MethodBase>();

    private static void AddBody(HashSet<Type> found, MethodBase method)
    {
        foreach (var p in method.GetParameters())
        {
            Add(found, p.ParameterType);
        }

        var body = SafeBody(method);
        if (body is null)
        {
            return;
        }

        foreach (var local in body.LocalVariables)
        {
            Add(found, local.LocalType);
        }

        foreach (var member in ReferencedMembers(method))
        {
            switch (member)
            {
                case Type t:
                    Add(found, t);
                    break;
                case FieldInfo f:
                    Add(found, f.DeclaringType);
                    Add(found, f.FieldType);
                    break;
                case MethodInfo mi:
                    Add(found, mi.DeclaringType);
                    Add(found, mi.ReturnType);
                    foreach (var arg in mi.IsGenericMethod ? mi.GetGenericArguments() : [])
                    {
                        Add(found, arg);
                    }

                    break;
                case ConstructorInfo ci:
                    Add(found, ci.DeclaringType);
                    break;
            }
        }
    }

    private static IEnumerable<MemberInfo> ReferencedMembers(MethodBase method)
    {
        var il = SafeBody(method)?.GetILAsByteArray();
        if (il is null)
        {
            yield break;
        }

        var typeArgs = method.DeclaringType is { IsGenericType: true } dt ? dt.GetGenericArguments() : null;
        var methodArgs = method.IsGenericMethod ? method.GetGenericArguments() : null;

        var pos = 0;
        while (pos < il.Length)
        {
            short value = il[pos++];
            if (value == 0xFE)
            {
                value = unchecked((short)(0xFE00 | il[pos++]));
            }

            if (!OpCodesByValue.TryGetValue(value, out var op))
            {
                yield break; // not an opcode we know: stop rather than misread the stream
            }

            switch (op.OperandType)
            {
                case OperandType.InlineMethod:
                case OperandType.InlineField:
                case OperandType.InlineType:
                case OperandType.InlineTok:
                    var token = BitConverter.ToInt32(il, pos);
                    var member = Resolve(method.Module, token, typeArgs, methodArgs);
                    if (member is not null)
                    {
                        yield return member;
                    }

                    pos += 4;
                    break;
                case OperandType.InlineSwitch:
                    var count = BitConverter.ToInt32(il, pos);
                    pos += 4 + (4 * count);
                    break;
                default:
                    pos += OperandSize(op.OperandType);
                    break;
            }
        }
    }

    private static int OperandSize(OperandType type) => type switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        _ => 4, // InlineBrTarget, InlineI, InlineSig, InlineString, ShortInlineR
    };

    private static MemberInfo? Resolve(Module module, int token, Type[]? typeArgs, Type[]? methodArgs)
    {
        try
        {
            return module.ResolveMember(token, typeArgs, methodArgs);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (TypeLoadException)
        {
            return null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private static MethodBody? SafeBody(MethodBase method)
    {
        try
        {
            return method.GetMethodBody();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static void Add(HashSet<Type> found, Type? type)
    {
        if (type is null)
        {
            return;
        }

        if (type.HasElementType)
        {
            Add(found, type.GetElementType());
            return;
        }

        if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            foreach (var arg in type.GetGenericArguments())
            {
                Add(found, arg);
            }

            type = type.GetGenericTypeDefinition();
        }

        if (!type.IsGenericParameter)
        {
            found.Add(type);
        }
    }
}
