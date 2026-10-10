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

#pragma warning disable CS0067 // never invoked: only the metadata shape matters here
    // Field-like events: their add/remove accessors are [CompilerGenerated].
    private event Action? PrivateEvent;

    internal static event Action? InternalStaticEvent;

    protected virtual event Action? ProtectedVirtualEvent;
#pragma warning restore CS0067
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
    public void PublicizesFieldLikeEventAccessorsButNotTheirBackingFields()
    {
        PublicizeAndInspectFixture(includeVirtualMembers: false, fixture =>
        {
            // Non-public field-like events become usable (`+=`/`-=`) from plugins even
            // though their accessors are compiler-generated.
            Method(fixture, "add_PrivateEvent").IsPublic.ShouldBeTrue();
            Method(fixture, "remove_PrivateEvent").IsPublic.ShouldBeTrue();
            Method(fixture, "add_InternalStaticEvent").IsPublic.ShouldBeTrue();
            Method(fixture, "remove_InternalStaticEvent").IsPublic.ShouldBeTrue();

            // The compiler-generated backing fields stay private, so the event name is
            // not ambiguous with a same-named public field (CS0229).
            Field(fixture, "PrivateEvent").IsPrivate.ShouldBeTrue();
            Field(fixture, "InternalStaticEvent").IsPrivate.ShouldBeTrue();

            // Virtual event accessors keep their access, like other virtual members (#56).
            Method(fixture, "add_ProtectedVirtualEvent").IsFamily.ShouldBeTrue();
            Method(fixture, "remove_ProtectedVirtualEvent").IsFamily.ShouldBeTrue();
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
