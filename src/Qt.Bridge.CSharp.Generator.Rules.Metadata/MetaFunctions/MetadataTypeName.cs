// Copyright (C) 2025 The Qt Company Ltd.
// SPDX-License-Identifier: LicenseRef-Qt-Commercial OR LGPL-3.0-only

namespace Qt.Bridge.CodeGeneration.MetaFunctions
{
    using Extensions;
    using static Traits;

    public class MetadataTypeName : BasicTypes
    {
        public override int Priority => base.Priority - 1;
        protected override string Eval(object src, Enum traits) => src switch
        {
            Type type when type.ExportAsMetadata() => (type, traits) switch
            {
                ({ IsEnum: true }, Name or Ns | Name or Name | Arg or Ns | Name | Arg)
                    => type.GetEnumUnderlyingType().MFn(traits),
                _ => null
            },
            _ => null
        };
    }
}
