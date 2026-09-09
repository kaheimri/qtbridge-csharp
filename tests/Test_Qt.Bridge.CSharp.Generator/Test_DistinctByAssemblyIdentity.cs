// Copyright (C) 2026 The Qt Company Ltd.
// SPDX-License-Identifier: LicenseRef-Qt-Commercial OR GPL-3.0-only

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test_Qt.Bridge.CSharp.Generator
{
    using Qt.Bridge.CodeGeneration;
    using Support;

    [TestClass]
    public class Test_DistinctByAssemblyIdentity
    {
        private static string BuildToFile(string dir, string assemblyName)
        {
            var fullPath = Path.Combine(dir, assemblyName + ".dll");
            using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.ReadWrite,
                FileShare.Read);
            InMemoryAssemblyBuilder.Build(assemblyConfig: new AssemblyConfig
            {
                AssemblyName = assemblyName,
                TypeName = "Generated.Type",
                MethodName = "Run",
            },
            returnConfig: new ReturnConfig
            {
                EncodeType = returnType => returnType.Void()
            },
            parameterConfig: new ParameterConfig(),
            outputStream: fs);
            fs.Flush(true);
            return fullPath;
        }

        [TestMethod]
        public void SamePathIsNotADuplicate()
        {
            var dir = TestCodeGenerator.CreatePluginTempDirectory();
            var path = BuildToFile(dir, "OnlyCopy");

            var result = Generator.DistinctByAssemblyIdentity([path]).ToList();

            CollectionAssert.AreEquivalent(new[] { path }, result);
        }

        [TestMethod]
        public void KeepsOnlyTheMoreRecentlyBuiltCopyOfADuplicateAssembly()
        {
            var dirA = TestCodeGenerator.CreatePluginTempDirectory();
            var dirB = TestCodeGenerator.CreatePluginTempDirectory();

            // Same identity (name + default 1.0.0.0 version) built to two different
            // directories, mimicking a project resolved from both an AnyCPU and a
            // platform-specific output layout.
            var stalePath = BuildToFile(dirA, "Qt.DotNet.Adapter");
            var freshPath = BuildToFile(dirB, "Qt.DotNet.Adapter");

            File.SetLastWriteTimeUtc(stalePath, DateTime.UtcNow.AddMinutes(-10));
            File.SetLastWriteTimeUtc(freshPath, DateTime.UtcNow);

            var result = Generator.DistinctByAssemblyIdentity([stalePath, freshPath]).ToList();

            CollectionAssert.AreEquivalent(new[] { freshPath }, result);
        }

        [TestMethod]
        public void PassesThroughFilesThatAreNotDotNetAssemblies()
        {
            var dir = TestCodeGenerator.CreatePluginTempDirectory();
            var nativePath = Path.Combine(dir, "native.dll");
            File.WriteAllBytes(nativePath, [0x4D, 0x5A, 0x00, 0x01]); // bogus PE bytes

            var managedPath = BuildToFile(dir, "Managed");

            var result = Generator.DistinctByAssemblyIdentity([nativePath, managedPath]).ToList();

            CollectionAssert.AreEquivalent(new[] { nativePath, managedPath }, result);
        }
    }
}
