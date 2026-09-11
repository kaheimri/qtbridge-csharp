// Copyright (C) 2025 The Qt Company Ltd.
// SPDX-License-Identifier: LicenseRef-Qt-Commercial OR LGPL-3.0-only

namespace Qt.Bridge.CodeGeneration.Extensions
{
    using static Rule;

    public static class TypeExtensionsForMetadataGeneration
    {
        public static bool IsMetadataCompatible(this Type type)
        {
            ArgumentNullException.ThrowIfNull(type);
            return !type.IsEnum || Type.GetTypeCode(type.GetEnumUnderlyingType()) == TypeCode.Int32;
        }

        public static Type MetadataCompatibleType(this Type type)
        {
            ArgumentNullException.ThrowIfNull(type);
            if (!type.IsMetadataCompatible())
                return null;
            if (type.IsEnum)
                return TypeOf(type.GetEnumUnderlyingType());
            if (!type.IsBuiltIn())
                return TypeOf<object>();
            return type;
        }
    }
}
