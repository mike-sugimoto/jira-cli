using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace JiraCli.ArchitectureTests;

/// <summary>
/// Reads an assembly with Mono.Cecil so rules see every method body, including the
/// compiler-generated nested types behind lambdas, iterators, and async state machines.
/// </summary>
internal static class IlScanner
{
    /// <summary>Every type in the assembly (nested ones included) whose outermost type matches the filter.</summary>
    public static IReadOnlyList<TypeDefinition> Types(Assembly assembly, Func<TypeDefinition, bool>? outermostFilter = null) =>
        Types(Module(assembly), outermostFilter);

    /// <summary>
    /// Reads the assembly's module. Cecil reads method bodies lazily, so the image is loaded into
    /// memory rather than read from a file stream that would have to stay open while callers enumerate.
    /// </summary>
    public static ModuleDefinition Module(Assembly assembly) =>
        ModuleDefinition.ReadModule(new MemoryStream(File.ReadAllBytes(assembly.Location)));

    /// <summary>Every type in the module (nested ones included) whose outermost type matches the filter.</summary>
    public static IReadOnlyList<TypeDefinition> Types(ModuleDefinition module, Func<TypeDefinition, bool>? outermostFilter = null)
    {
        return module.GetTypes()
            .Where(type => type.FullName != "<Module>")
            .Where(type => outermostFilter is null || outermostFilter(Outermost(type)))
            .ToArray();
    }

    /// <summary>Every instruction of every method body declared on the given type.</summary>
    public static IEnumerable<Instruction> Instructions(TypeDefinition type) =>
        type.Methods.Where(method => method.HasBody).SelectMany(method => method.Body.Instructions);

    public static TypeDefinition Outermost(TypeDefinition type)
    {
        while (type.DeclaringType is not null)
        {
            type = type.DeclaringType;
        }

        return type;
    }

    /// <summary>The full name of the type an instruction operand refers to, if any.</summary>
    public static string? ReferencedTypeName(Instruction instruction) => instruction.Operand switch
    {
        TypeReference type => type.FullName,
        MemberReference member => member.DeclaringType?.FullName,
        _ => null,
    };
}
