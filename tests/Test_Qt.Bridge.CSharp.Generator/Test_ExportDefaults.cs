// Copyright (C) 2026 The Qt Company Ltd.
// SPDX-License-Identifier: LicenseRef-Qt-Commercial OR GPL-3.0-only

using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Qt.Bridge.CodeGeneration.Extensions;
using Test_Qt.Bridge.CSharp.Generator.Support;

namespace Test_Qt.Bridge.CSharp.Generator
{
    [TestClass]
    public class Test_ExportDefaults
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        [DataRow("", Qt.ExportAs.Metadata, false)]
        [DataRow("", Qt.ExportAs.SourceCode, true)]
        [DataRow("[assembly: Qt.Export(Global = true, Options = Qt.ExportAs.Metadata)]",
            Qt.ExportAs.SourceCode, false)]
        [DataRow("[assembly: Qt.Export(Options = Qt.ExportAs.SourceCode)]", Qt.ExportAs.Metadata,
            true)]
        [DataRow("[Qt.Export(Options = Qt.ExportAs.Metadata)]", Qt.ExportAs.SourceCode, false)]
        [DataRow("[Qt.Export(Options = Qt.ExportAs.Default)]", Qt.ExportAs.Metadata, true)]
        public async Task ExplicitAttributesOverrideProjectFallback(
            string attribute, Qt.Options fallback, bool native)
        {
            using var result = await TestCodeGenerator.GenerateAsync(
                sources: [
                    attribute + " public class Example { public int Value { get; set; } }"
                ],
                sourceRefs: [
                    typeof(Qt.ExportAttribute).Assembly,
                    typeof(Qt.Bridge.CodeGeneration.Rules.Metadata.GenerateType).Assembly
                ],
                defaultExportOptions: fallback,
                ct: TestContext.CancellationTokenSource.Token);

            Assert.AreEqual(native, result.SourceAssembly.GetType("Example").ExportAsSourceCode());
            Assert.AreEqual(!native, result.Sink.Files.ContainsKey(TestCodeGenerator.MetadataFileName));
        }

        [TestMethod]
        public async Task ExplicitSourceTypeProducesMixedBuild()
        {
            using var result = await TestCodeGenerator.GenerateAsync(
                sources: [
                    """
                    public class DynamicType { public int Value { get; set; } }
                    [Qt.Export(Options = Qt.ExportAs.SourceCode)]
                    public class NativeType { public int Value { get; set; } }
                    """
                ],
                sourceRefs: [
                    typeof(Qt.ExportAttribute).Assembly,
                    typeof(Qt.Bridge.CodeGeneration.Rules.Metadata.GenerateType).Assembly
                ],
                defaultExportOptions: Qt.ExportAs.Metadata,
                ct: TestContext.CancellationTokenSource.Token);

            Assert.IsTrue(result.Sink.Files.ContainsKey("source_code_status.txt"));
            var json = result.Sink.Files[TestCodeGenerator.MetadataFileName];
            Assert.Contains("DynamicType", json);
            Assert.DoesNotContain("NativeType", json);
        }

        [TestMethod]
        public async Task ReferencedAssemblyChoicePrecedesApplicationGlobalAndProjectDefault()
        {
            using var library = await TestCodeGenerator.GenerateAsync(
                sources: [
                    """
                    [assembly: Qt.Export(Options = Qt.ExportAs.Metadata)]
                    public class LibraryType { }
                    """
                ],
                sourceRefs: [
                    typeof(Qt.ExportAttribute).Assembly,
                    typeof(Qt.Bridge.CodeGeneration.Rules.Metadata.GenerateType).Assembly
                ],
                ct: TestContext.CancellationTokenSource.Token);

            using var app = await TestCodeGenerator.GenerateAsync(
                sources: [
                    """
                    [assembly: Qt.Export(Global = true, Options = Qt.ExportAs.SourceCode)]
                    public class AppType { public LibraryType Library { get; set; } }
                    """
                ],
                sourceRefs: [
                    typeof(Qt.ExportAttribute).Assembly
                ],
                referencesWithAliases: [
                    ("global", library.SourceAssembly.Location)
                ],
                defaultExportOptions: Qt.ExportAs.SourceCode,
                ct: TestContext.CancellationTokenSource.Token);

            var appType = app.SourceAssembly.GetType("AppType");
            Assert.IsNotNull(appType);
            Assert.IsTrue(appType.ExportAsSourceCode());

            var libraryProperty = appType.GetProperty("Library");
            Assert.IsNotNull(libraryProperty);
            Assert.IsTrue(libraryProperty.PropertyType.ExportAsMetadata());
        }
    }
}
