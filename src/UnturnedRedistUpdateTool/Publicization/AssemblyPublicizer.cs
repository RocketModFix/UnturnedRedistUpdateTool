// -----------------------------------------------------------------------------
// The per-member publicization loop below is adapted from krafs/Publicizer
// (https://github.com/krafs/Publicizer), MIT. Copyright (c) krafs.
// See LICENSE.krafs.txt in this folder.
//
// It is reshaped from an MSBuild Task (operating on a consumer's references)
// into a standalone "load DLL -> rewrite visibility -> write DLL" API so the
// redist tool can ship a pre-publicized assembly. The decision logic
// (virtual / compiler-generated / do-not-publicize handling) is preserved.
// -----------------------------------------------------------------------------

using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Writer;

namespace UnturnedRedistUpdateTool.Publicization;

/// <summary>
/// Rewrites an assembly so its members become public, producing a redistributable
/// "publicized" copy that plugin authors can compile against.
/// </summary>
public static class AssemblyPublicizer
{
    /// <summary>
    /// Publicizes <paramref name="inputPath"/> and writes the result to
    /// <paramref name="outputPath"/> (which may differ from the input).
    /// </summary>
    /// <returns><c>true</c> if any member was modified.</returns>
    public static bool Publicize(string inputPath, string outputPath, PublicizeOptions? options = null)
    {
        options ??= new PublicizeOptions();

        // Load from bytes so we never hold a lock on the input file (allows
        // input == output) and don't depend on memory-mapped IO during write.
        using ModuleDef module = ModuleDefMD.Load(File.ReadAllBytes(inputPath));

        bool modified = PublicizeAssembly(module, options);

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var writerOptions = new ModuleWriterOptions(module)
        {
            // Some assemblies fail to round-trip without this; mirrors krafs.
            // https://github.com/krafs/Publicizer/issues/42
            MetadataOptions = new MetadataOptions(MetadataFlags.KeepOldMaxStack),
            Logger = DummyLogger.NoThrowInstance,
        };

        using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
        module.Write(fileStream, writerOptions);

        return modified;
    }

    private static bool PublicizeAssembly(ModuleDef module, PublicizeOptions options)
    {
        bool publicizedAnyMemberInAssembly = false;
        var doNotPublicizePropertyMethods = new HashSet<MethodDef>();

        foreach (TypeDef typeDef in module.GetTypes())
        {
            doNotPublicizePropertyMethods.Clear();

            bool publicizedAnyMemberInType = false;
            string typeName = typeDef.ReflectionFullName;

            bool explicitlyDoNotPublicizeType = options.DoNotPublicizeMembers.Contains(typeName);

            // PROPERTIES
            foreach (PropertyDef propertyDef in typeDef.Properties)
            {
                string propertyName = $"{typeName}.{propertyDef.Name}";

                if (options.DoNotPublicizeMembers.Contains(propertyName))
                {
                    if (propertyDef.GetMethod is MethodDef getter)
                    {
                        doNotPublicizePropertyMethods.Add(getter);
                    }
                    if (propertyDef.SetMethod is MethodDef setter)
                    {
                        doNotPublicizePropertyMethods.Add(setter);
                    }
                    continue;
                }

                if (explicitlyDoNotPublicizeType)
                {
                    continue;
                }

                if (!options.IncludeCompilerGeneratedMembers && IsCompilerGenerated(propertyDef))
                {
                    continue;
                }

                if (AssemblyEditor.PublicizeProperty(propertyDef, options.IncludeVirtualMembers))
                {
                    publicizedAnyMemberInType = true;
                }
            }

            // METHODS
            foreach (MethodDef methodDef in typeDef.Methods)
            {
                string methodName = $"{typeName}.{methodDef.Name}";

                if (doNotPublicizePropertyMethods.Contains(methodDef))
                {
                    continue;
                }

                if (options.DoNotPublicizeMembers.Contains(methodName))
                {
                    continue;
                }

                if (explicitlyDoNotPublicizeType)
                {
                    continue;
                }

                if (!options.IncludeCompilerGeneratedMembers && IsCompilerGenerated(methodDef))
                {
                    continue;
                }

                if (AssemblyEditor.PublicizeMethod(methodDef, options.IncludeVirtualMembers))
                {
                    publicizedAnyMemberInType = true;
                }
            }

            // FIELDS
            foreach (FieldDef fieldDef in typeDef.Fields)
            {
                string fieldName = $"{typeName}.{fieldDef.Name}";

                if (options.DoNotPublicizeMembers.Contains(fieldName))
                {
                    continue;
                }

                if (explicitlyDoNotPublicizeType)
                {
                    continue;
                }

                if (!options.IncludeCompilerGeneratedMembers && IsCompilerGenerated(fieldDef))
                {
                    continue;
                }

                if (AssemblyEditor.PublicizeField(fieldDef))
                {
                    publicizedAnyMemberInType = true;
                }
            }

            if (publicizedAnyMemberInType)
            {
                AssemblyEditor.PublicizeType(typeDef);
                publicizedAnyMemberInAssembly = true;
                continue;
            }

            if (explicitlyDoNotPublicizeType)
            {
                continue;
            }

            if (!options.IncludeCompilerGeneratedMembers && IsCompilerGenerated(typeDef))
            {
                continue;
            }

            // Whole-assembly publicization: a member-less or already-public-membered
            // type still has its own visibility promoted to public/nested-public.
            if (AssemblyEditor.PublicizeType(typeDef))
            {
                publicizedAnyMemberInAssembly = true;
            }
        }

        return publicizedAnyMemberInAssembly;
    }

    private static bool IsCompilerGenerated(IHasCustomAttribute memberDef)
    {
        return memberDef.CustomAttributes.Any(x => x.TypeFullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");
    }
}
