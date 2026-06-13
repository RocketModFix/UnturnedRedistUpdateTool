namespace UnturnedRedistUpdateTool.Publicization;

/// <summary>
/// Controls how <see cref="AssemblyPublicizer.Publicize"/> rewrites an assembly.
/// Defaults are tuned for the Unturned redist: skip virtual members so plugin
/// <c>protected override</c>s keep compiling, and skip compiler-generated members
/// to avoid name clashes when they are made public.
/// </summary>
public sealed class PublicizeOptions
{
    /// <summary>
    /// When <c>false</c> (default), virtual/abstract/override methods and virtual
    /// property accessors keep their original accessibility. This is the fix for
    /// RocketModFix.Unturned.Redist#56: publicizing e.g. <c>Command.execute</c>
    /// (protected abstract) would force every plugin override to be public, which
    /// the compiler rejects with "cannot change access rights" (CS0507).
    /// </summary>
    public bool IncludeVirtualMembers { get; set; }

    /// <summary>
    /// When <c>false</c> (default), members marked with
    /// <c>[CompilerGenerated]</c> (backing fields, closures, iterator state
    /// machines, ...) are left untouched. They are implementation details plugins
    /// should not bind to, and making them public can introduce name conflicts.
    /// </summary>
    public bool IncludeCompilerGeneratedMembers { get; set; }

    /// <summary>
    /// Reflection full names of members (or types) that must never be publicized,
    /// e.g. <c>SDG.Unturned.SomeType.someMember</c>. An escape hatch for one-off
    /// edge cases without forking the engine.
    /// </summary>
    public HashSet<string> DoNotPublicizeMembers { get; } = new(StringComparer.Ordinal);
}
