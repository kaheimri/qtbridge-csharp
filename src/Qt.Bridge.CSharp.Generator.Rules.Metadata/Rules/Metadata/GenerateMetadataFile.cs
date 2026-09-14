// Copyright (C) 2025 The Qt Company Ltd.
// SPDX-License-Identifier: LicenseRef-Qt-Commercial OR LGPL-3.0-only

using System.Reflection;

namespace Qt.Bridge.CodeGeneration.Rules.Metadata
{
    using static Placeholders;

    public class GenerateMetadataFile : Rule
    {
        public override bool Matches(MemberInfo src) => src.IsRootNode();
        public override Result Execute(MemberInfo _)
        {
            if (string.IsNullOrEmpty(GeneratorOptions.MetadataFileName))
                return Error("Type metadata file name is not configured");

            var json = new FilePlaceholder(MetadataFile, Root, GeneratorOptions.MetadataFileName)
            {
                IndentChars = "  ",
                CompactJson = true
            };

            Placeholder types = null;
            json += $@"
{{
  ""$schema"": ""https://code.qt.io/cgit/qt/qtbridge-csharp.git/plain/qt_bridge_metadata_schema.json"",
  ""types"": [
{json[types = new(MetadataTypes) { Sorted = true, Separator = ",", Indent = 2 }]}
  ]
}}
";

            json.WriteWhen = () => !types.IsEmpty;

            return Ok;
        }
    }
}
