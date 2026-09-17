// Copyright (C) 2026 The Qt Company Ltd.
// SPDX-License-Identifier: LicenseRef-Qt-Commercial OR LGPL-3.0-only

using System.Reflection;
using System.Xml.Linq;

namespace Qt.Bridge.CodeGeneration.Rules.SourceCode
{
    using Extensions;

    public class GenerateResources : Rule
    {
        public override int Priority => int.MinValue;
        public override bool Matches(MemberInfo src) => src.IsRootNode();

        internal static IReadOnlyList<QtResourceAttribute> Resources { get; private set; } = [];
        internal static bool Linked { get; private set; }

        public override void Reset()
        {
            Resources = [];
            Linked = false;
        }

        public override Result Execute(MemberInfo src)
        {
            const StringComparison ordinalIgnoreCase = StringComparison.OrdinalIgnoreCase;

            // Resolve and validate both backends before deciding whether a native build is needed.
            var groups = Root.Assembly.QtResourcesWithReferences(SourceGraph)
                .Where(r => string.Equals(r.AccessMode, "Default", ordinalIgnoreCase)
                    || string.Equals(r.AccessMode, "ManagedAndNative", ordinalIgnoreCase))
                .Where(r => r.SourcePath is { Length: > 0 } && r.Alias is { Length: > 0 })
                .GroupBy(r => r.Alias.Replace('\\', '/').TrimStart('/'), StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal).ToArray();

            var pathComparer = OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

            var newLine = Environment.NewLine;
            var collisions = groups.Where(g => g.Select(r => r.SourcePath)
                    .Distinct(pathComparer).Count() > 1)
                .Select(grouping => $"Duplicate resource alias '{grouping.Key}':{newLine}"
                    + string.Join(newLine, grouping
                        .Select(res => res.SourcePath + (string.IsNullOrEmpty(res.AssemblyId)
                            ? "" : $" ({res.AssemblyId})"))
                        .Distinct(pathComparer)
                        .OrderBy(path => path, pathComparer)
                        .Select(path => $"  {path}")))
                .ToArray();
            if (collisions.Length > 0)
                return Error(string.Join(newLine, collisions));

            Resources =
            [
                .. groups.Select(grouping =>
                {
                    var resource = grouping.First();
                    resource.Alias = grouping.Key;
                    return resource;
                })
            ];

            Linked = SourceGraph.ResourcePackaging == ResourcePackaging.Linked
                || (SourceGraph.ResourcePackaging == ResourcePackaging.Auto
                    && StatusFile.NativeTypes.Any());
            if (Linked || Resources.Count == 0)
                return Ok;

            var qrc = new XElement("RCC", new XElement("qresource", new XAttribute("prefix", "/"),
                Resources.Select(r => new XElement("file", new XAttribute("alias", r.Alias),
                    r.SourcePath.Replace('\\', '/')))));
            _ = new FilePlaceholder("ExternalResources", Root, "qt_bridge_resources.qrc") {
                Content = [qrc.ToString()]
            };
            return Ok;
        }
    }
}
