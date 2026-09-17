// Copyright (C) 2026 The Qt Company Ltd.
// SPDX-License-Identifier: LicenseRef-Qt-Commercial OR GPL-3.0-only

using System;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Qt.Bridge.CodeGeneration;
using Test_Qt.Bridge.CSharp.Generator.Support;

namespace Test_Qt.Bridge.CSharp.Generator
{
    [TestClass]
    public class Test_ResourcePackaging
    {
        public TestContext TestContext { get; set; }

        private const string Resource = """
            [assembly: Qt.Bridge.QtResource(SourcePath = "C:/assets/a&b.txt",
                Alias = "assemblies/App/a&b.txt")]
            """;

        [TestMethod]
        [DataRow(ResourcePackaging.Auto, Qt.ExportAs.Metadata, false, true)]
        [DataRow(ResourcePackaging.Auto, Qt.ExportAs.SourceCode, true, false)]
        [DataRow(ResourcePackaging.Linked, Qt.ExportAs.Metadata, true, false)]
        [DataRow(ResourcePackaging.Linked, Qt.ExportAs.SourceCode, true, false)]
        [DataRow(ResourcePackaging.External, Qt.ExportAs.Metadata, false, true)]
        [DataRow(ResourcePackaging.External, Qt.ExportAs.SourceCode, true, true)]
        public async Task PackagingAndTypeExportAreIndependent(
            ResourcePackaging packaging,
            Qt.Options export,
            bool native,
            bool external)
        {
            using var result = await TestCodeGenerator.GenerateAsync(
                sources: [
                    (export == Qt.ExportAs.Metadata
                        ? "[assembly: Qt.Export(Global = true, Options = Qt.ExportAs.Metadata)]"
                        : "") + Resource + " public class Example { }"
                ],
                sourceRefs: [
                    typeof(Qt.ExportAttribute).Assembly,
                    typeof(Qt.Bridge.CodeGeneration.Rules.Metadata.GenerateType).Assembly
                ],
                resourcePackaging: packaging,
                ct: TestContext.CancellationTokenSource.Token);

            Assert.AreEqual(native, result.Sink.Files.ContainsKey("source_code_status.txt"));
            Assert.AreEqual(external, result.Sink.Files.ContainsKey("qt_bridge_resources.qrc"));
            if (native) {
                var cmake = result.Sink.Files["source/CMakeLists.txt"];
                Assert.AreEqual(!external, cmake.Contains("qt_add_resources"));
            }

            if (external) {
                var root = XDocument.Parse(result.Sink.Files["qt_bridge_resources.qrc"]).Root;
                Assert.IsNotNull(root);
                var resource = root.Element("qresource");
                Assert.IsNotNull(resource);
                var file = resource.Element("file");
                Assert.IsNotNull(file);
                Assert.AreEqual("assemblies/App/a&b.txt", (string)file.Attribute("alias"));
                Assert.AreEqual("C:/assets/a&b.txt", file.Value);
            }
        }

        [TestMethod]
        [DataRow(ResourcePackaging.Auto)]
        [DataRow(ResourcePackaging.Linked)]
        [DataRow(ResourcePackaging.External)]
        public async Task ManagedOnlyDoesNotCreatePackOrNativeBuild(ResourcePackaging packaging)
        {
            using var result = await TestCodeGenerator.GenerateAsync(
                sources: [
                    """
                    [assembly: Qt.Export(Global = true, Options = Qt.ExportAs.Metadata)]
                    [assembly: Qt.Bridge.QtResource(SourcePath = "asset.txt", Alias = "asset.txt",
                        AccessMode = "ManagedOnly")]
                    public class Example { }
                    """
                ],
                sourceRefs: [
                    typeof(Qt.ExportAttribute).Assembly
                ],
                resourcePackaging: packaging,
                ct: TestContext.CancellationTokenSource.Token);

            Assert.IsFalse(result.Sink.Files.ContainsKey("source_code_status.txt"));
            Assert.IsFalse(result.Sink.Files.ContainsKey("qt_bridge_resources.qrc"));
        }

        [TestMethod]
        public async Task MetadataOnlyStillChecksAliasCollisions()
        {
            var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
                TestCodeGenerator.GenerateAsync(
                    sources: [
                        Resource + """
                            [assembly: Qt.Export(Global = true, Options = Qt.ExportAs.Metadata)]
                            [assembly: Qt.Bridge.QtResource(SourcePath = "C:/assets/a&b.txt",
                                Alias = "assemblies/App/a&b.txt")]
                            [assembly: Qt.Bridge.QtResource(SourcePath = "other.txt",
                                Alias = "assemblies/App/a&b.txt")]
                            public class Example { }
                        """
                    ],
                    sourceRefs: [
                        typeof(Qt.ExportAttribute).Assembly
                    ],
                    ct: TestContext.CancellationTokenSource.Token));

            Assert.Contains("Duplicate resource alias", error.Message);
            Assert.DoesNotContain("C:/assets/a&b.txt (", error.Message);
            Assert.AreEqual(1, error.Message.Split("C:/assets/a&b.txt").Length - 1);
        }

        [TestMethod]
        public async Task MixedTypesCanUseExternalResources()
        {
            using var result = await TestCodeGenerator.GenerateAsync(
                sources: [
                    Resource + """
                        [assembly: Qt.Export(Global = true, Options = Qt.ExportAs.Metadata)]
                        public class DynamicType { }
                        [Qt.Export(Options = Qt.ExportAs.SourceCode)]
                        public class NativeType { }
                    """
                ],
                sourceRefs: [
                    typeof(Qt.ExportAttribute).Assembly,
                    typeof(Qt.Bridge.CodeGeneration.Rules.Metadata.GenerateType).Assembly
                ],
                resourcePackaging: ResourcePackaging.External,
                ct: TestContext.CancellationTokenSource.Token);

            Assert.IsTrue(result.Sink.Files.ContainsKey(TestCodeGenerator.MetadataFileName));
            Assert.IsTrue(result.Sink.Files.ContainsKey("source_code_status.txt"));
            Assert.IsTrue(result.Sink.Files.ContainsKey("qt_bridge_resources.qrc"));
            Assert.DoesNotContain("qt_add_resources", result.Sink.Files["source/CMakeLists.txt"]);
        }
    }
}
