// -----------------------------------------------------------------------------
// Vendored from krafs/Publicizer (https://github.com/krafs/Publicizer), MIT.
// Copyright (c) krafs. See LICENSE.krafs.txt in this folder.
//
// We vendor the engine (rather than reference the NuGet package) because
// Krafs.Publicizer ships as an MSBuild task that publicizes a *consumer's*
// references at build time; it does not expose an API for producing a
// redistributable, pre-publicized assembly. This file is unchanged from krafs
// except for the namespace.
// -----------------------------------------------------------------------------

using dnlib.DotNet;

namespace UnturnedRedistUpdateTool.Publicization;

/// <summary>
/// Low-level helpers for rewriting member visibility to public.
/// </summary>
internal static class AssemblyEditor
{
    internal static bool PublicizeType(TypeDef type)
    {
        TypeAttributes oldAttributes = type.Attributes;
        type.Attributes &= ~TypeAttributes.VisibilityMask;

        if (type.IsNested)
        {
            type.Attributes |= TypeAttributes.NestedPublic;
        }
        else
        {
            type.Attributes |= TypeAttributes.Public;
        }
        return type.Attributes != oldAttributes;
    }

    internal static bool PublicizeProperty(PropertyDef property, bool includeVirtual = true)
    {
        bool publicized = false;

        if (property.GetMethod is MethodDef getMethod)
        {
            publicized |= PublicizeMethod(getMethod, includeVirtual);
        }

        if (property.SetMethod is MethodDef setMethod)
        {
            publicized |= PublicizeMethod(setMethod, includeVirtual);
        }

        return publicized;
    }

    internal static bool PublicizeMethod(MethodDef method, bool includeVirtual = true)
    {
        // When includeVirtual is false we leave virtual/abstract/override members
        // at their original access. Publicizing them would force overrides to widen
        // access ("cannot change access rights", CS0507) and mismatch the original
        // assembly the game loads at runtime. See RocketModFix.Unturned.Redist#56.
        if (includeVirtual || !method.IsVirtual)
        {
            MethodAttributes oldAttributes = method.Attributes;
            method.Attributes &= ~MethodAttributes.MemberAccessMask;
            method.Attributes |= MethodAttributes.Public;
            return method.Attributes != oldAttributes;
        }
        return false;
    }

    internal static bool PublicizeField(FieldDef field)
    {
        FieldAttributes oldAttributes = field.Attributes;
        field.Attributes &= ~FieldAttributes.FieldAccessMask;
        field.Attributes |= FieldAttributes.Public;
        return field.Attributes != oldAttributes;
    }
}
