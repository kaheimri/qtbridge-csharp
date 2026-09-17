// Copyright (C) 2026 The Qt Company Ltd.
// SPDX-License-Identifier: LicenseRef-Qt-Commercial OR LGPL-3.0-only

using System.Reflection;
using Qt.DotNet;

namespace Qt.Bridge.CodeGeneration.Rules.SourceCode
{
    using Extensions;
    using static Placeholders;

    public class StatusFile : Rule
    {
        public override int Priority => int.MinValue + 1;
        public override bool Matches(MemberInfo src) => src.IsRootNode();

        internal static IEnumerable<Type> NativeTypes => SourceGraph.NodeSet<Type>()
            .Where(type => !type.IsRootNode() && type.ExportAsSourceCode()
                && type.Assembly != TypeOf<TypeCast>()?.Assembly
                && type.Assembly != TypeOf<Adapter>()?.Assembly);

        public override Result Execute(MemberInfo __)
        {
            var typesToGenerate = NativeTypes.ToArray();
            var linkedResources = GenerateResources.Linked && GenerateResources.Resources.Count > 0;
            if (typesToGenerate.Length == 0 && !linkedResources)
                return Ok;

            _ = new FilePlaceholder(Status, Root, "source_code_status.txt")
            {
                Sorted = true,
                Content = typesToGenerate.Select(type => type.AssemblyQualifiedName)
                    .Concat(linkedResources ? ["Linked resources"] : [])
            };

            return Ok;
        }

        internal static bool CheckIn(MemberInfo src, string tag)
        {
            if (Root.GetPlaceholder(Status) is not { } status)
                return false;
            status += src switch
            {
                Type type => $"{type.AssemblyQualifiedName} | [{tag}]",
                _ => $"{src.ReflectedType.AssemblyQualifiedName} | {src} | [{tag}]"
            };
            return true;
        }
    }
}
