using dnlib.DotNet;
using Shouldly;
using UnturnedRedistUpdateTool.Publicization;
using UnturnedRedistUpdateTool.Tests.Helpers;
using Xunit;

namespace UnturnedRedistUpdateTool.Tests;

/// <summary>
/// Fixture with members at every interesting access level. The publicizer tests
/// rewrite a copy of this test assembly and inspect how each member is treated.
/// </summary>
public abstract class PublicizeFixtureBase
{
    private int _privateField = 42;

    private int PrivateMethod() => _privateField;

    protected abstract void ProtectedAbstractMethod();

    protected virtual int ProtectedVirtualMethod() => _privateField + PrivateMethod();

    public virtual void PublicVirtualMethod() { }
}

public class AssemblyPublicizerTests
{
    [Fact]
    public void SkipsVirtualMembersButPublicizesEverythingElse()
    {
        PublicizeAndInspectFixture(includeVirtualMembers: false, fixture =>
        {
            // Non-virtual private members are publicized so plugins can reach them.
            Field(fixture, "_privateField").IsPublic.ShouldBeTrue();
            Method(fixture, "PrivateMethod").IsPublic.ShouldBeTrue();

            // Virtual/abstract members keep their original accessibility, so a plugin's
            // `protected override` still matches the base (issue #56, CS0507).
            MethodDef abstractMethod = Method(fixture, "ProtectedAbstractMethod");
            abstractMethod.IsPublic.ShouldBeFalse();
            abstractMethod.IsFamily.ShouldBeTrue();

            MethodDef virtualMethod = Method(fixture, "ProtectedVirtualMethod");
            virtualMethod.IsPublic.ShouldBeFalse();
            virtualMethod.IsFamily.ShouldBeTrue();

            // Already-public virtual stays public.
            Method(fixture, "PublicVirtualMethod").IsPublic.ShouldBeTrue();
        });
    }

    [Fact]
    public void PublicizesVirtualMembersWhenIncludeVirtualMembersIsTrue()
    {
        PublicizeAndInspectFixture(includeVirtualMembers: true, fixture =>
        {
            // The toggle is honored: with virtual members included, they become public.
            Method(fixture, "ProtectedAbstractMethod").IsPublic.ShouldBeTrue();
            Method(fixture, "ProtectedVirtualMethod").IsPublic.ShouldBeTrue();
        });
    }

    private static void PublicizeAndInspectFixture(bool includeVirtualMembers, Action<TypeDef> inspect)
    {
        using var tempDir = new TempDir();
        var inputPath = typeof(PublicizeFixtureBase).Assembly.Location;
        var outputPath = Path.Combine(tempDir.Path, "publicized.dll");

        AssemblyPublicizer.Publicize(inputPath, outputPath, new PublicizeOptions
        {
            IncludeVirtualMembers = includeVirtualMembers,
            IncludeCompilerGeneratedMembers = false,
        });

        // Load from bytes so the file isn't held open. The module must stay alive
        // while inspecting: dnlib lazy-loads members from it on first access.
        using var module = ModuleDefMD.Load(File.ReadAllBytes(outputPath));
        var fixture = module.Find(typeof(PublicizeFixtureBase).FullName, isReflectionName: true)
                      ?? throw new InvalidOperationException("Fixture type not found in publicized assembly.");
        inspect(fixture);
    }

    private static FieldDef Field(TypeDef type, string name) =>
        type.Fields.Single(f => f.Name == name);

    private static MethodDef Method(TypeDef type, string name) =>
        type.Methods.Single(m => m.Name == name);
}
