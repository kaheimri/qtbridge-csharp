// Copyright (C) 2026 The Qt Company Ltd.
// SPDX-License-Identifier: LicenseRef-Qt-Commercial OR GPL-3.0-only

using System.Text;
using System.Collections;
using Microsoft.Build.Framework;
using Qt.Bridge.CSharp.Build.Tasks;

namespace Test_Qt.Bridge.CSharp.Build.Tasks
{
    using static PatchNativeHostManifest;

    [TestClass]
    public sealed class Test_PatchNativeHostManifest : TestBase
    {
        private const int TemplateOffset = 16;
        private const string AssemblyName = "Application.dll";

        // Non-zero filler, as the compiled template has it, so a test
        // can tell the bytes the task wrote from the bytes it left alone.
        private static byte[] CreateHost()
        {
            var host = new byte[TemplateOffset + TemplateSize + TemplateOffset];
            for (var i = 0; i < host.Length; ++i)
                host[i] = (byte)'F';
            Encoding.ASCII.GetBytes(Marker).CopyTo(host, TemplateOffset);
            return host;
        }

        private string WriteHost(byte[] host)
        {
            Directory.CreateDirectory(TempDirectory);
            var hostPath = Path.Combine(TempDirectory, "host.bin");
            File.WriteAllBytes(hostPath, host);
            return hostPath;
        }

        private static (PatchNativeHostManifest Task, TestBuildEngine Engine) CreateTask(
            string hostPath, string assemblyFileName = AssemblyName)
        {
            var engine = new TestBuildEngine();
            return (new PatchNativeHostManifest
            {
                BuildEngine = engine,
                HostPath = hostPath,
                AssemblyFileName = assemblyFileName
            }, engine);
        }

        [TestMethod]
        public void Execute_WritesTheManifestHeaderAndAssemblyName()
        {
            var hostPath = WriteHost(CreateHost());

            Assert.IsTrue(CreateTask(hostPath).Task.Execute());

            var host = File.ReadAllBytes(hostPath);
            Assert.AreEqual("QTBM", Encoding.ASCII.GetString(host, TemplateOffset, 4));
            Assert.AreEqual(1, host[TemplateOffset + ManifestVersionOffset]);
            Assert.AreEqual(0, host[TemplateOffset + ManifestVersionOffset + 1]);

            Assert.AreEqual(ManifestPayloadSize, host[TemplateOffset + ManifestPayloadSizeOffset]
                | (host[TemplateOffset + ManifestPayloadSizeOffset + 1] << 8));

            Assert.AreEqual(AssemblyName, Encoding.UTF8.GetString(host,
                TemplateOffset + AssemblyNameOffset, AssemblyName.Length));
            Assert.AreEqual(0, host[TemplateOffset + AssemblyNameOffset + AssemblyName.Length]);
        }

        [TestMethod]
        public void Execute_InjectsTheSdkPlaceholderFollowedByZeros()
        {
            var hostPath = WriteHost(CreateHost());

            Assert.IsTrue(CreateTask(hostPath).Task.Execute());

            var host = File.ReadAllBytes(hostPath);
            const int slot = TemplateOffset + ManifestSize;
            Assert.AreEqual(SdkPlaceholder,
                Encoding.ASCII.GetString(host, slot, SdkPlaceholder.Length));

            // The remainder of the slot has to be zero: HostWriter pads only within its 64-byte
            // search pattern, so a longer assembly name relies on these for its terminator.
            for (var i = slot + SdkPlaceholder.Length; i < TemplateOffset + TemplateSize; ++i)
                Assert.AreEqual(0, host[i], $"expected zero padding at {i}");
        }

        [TestMethod]
        public void Execute_LeavesTheRestOfTheHostUntouched()
        {
            var original = CreateHost();
            var hostPath = WriteHost(original);

            Assert.IsTrue(CreateTask(hostPath).Task.Execute());

            var host = File.ReadAllBytes(hostPath);
            Assert.AreEqual(original.Length, host.Length);
            for (var i = 0; i < TemplateOffset; ++i)
                Assert.AreEqual(original[i], host[i], $"byte {i} before the template changed");
            for (var i = TemplateOffset + TemplateSize; i < host.Length; ++i)
                Assert.AreEqual(original[i], host[i], $"byte {i} after the template changed");
        }

        [TestMethod]
        public void Execute_RemovesTheUnpatchedMarker()
        {
            var hostPath = WriteHost(CreateHost());

            Assert.IsTrue(CreateTask(hostPath).Task.Execute());

            var host = Encoding.ASCII.GetString(File.ReadAllBytes(hostPath));
            Assert.DoesNotContain(Marker, host);
        }

        [TestMethod]
        public void Execute_FailsWhenTheMarkerIsMissing()
        {
            var host = CreateHost();
            host[TemplateOffset] = (byte)'X';
            var (task, engine) = CreateTask(WriteHost(host));

            Assert.IsFalse(task.Execute());
            Assert.HasCount(1, engine.Errors);
            Assert.Contains("was not found", engine.Errors[0].Message!);
        }

        [TestMethod]
        public void Execute_FailsWhenTheMarkerOccursTwice()
        {
            var host = new byte[TemplateOffset + 2 * TemplateSize];
            var marker = Encoding.ASCII.GetBytes(Marker);
            marker.CopyTo(host, TemplateOffset);
            marker.CopyTo(host, TemplateOffset + TemplateSize);
            var (task, engine) = CreateTask(WriteHost(host));

            Assert.IsFalse(task.Execute());
            Assert.HasCount(1, engine.Errors);
            Assert.Contains("more than one", engine.Errors[0].Message!);
        }

        [TestMethod]
        public void Execute_FailsWhenTheTemplateIsTruncated()
        {
            // Marker present, but fewer than 2048 bytes follow it.
            var host = new byte[TemplateOffset + TemplateSize - 1];
            Encoding.ASCII.GetBytes(Marker).CopyTo(host, TemplateOffset);
            var (task, engine) = CreateTask(WriteHost(host));

            Assert.IsFalse(task.Execute());
            Assert.HasCount(1, engine.Errors);
            Assert.Contains("truncated", engine.Errors[0].Message!);
        }

        [TestMethod]
        public void Execute_FailsOnAnEmptyAssemblyFileName()
        {
            var (task, engine) = CreateTask(WriteHost(CreateHost()), "");

            Assert.IsFalse(task.Execute());
            Assert.HasCount(1, engine.Errors);
        }

        [TestMethod]
        public void Execute_FailsOnAnAssemblyFileNameThatDoesNotFit()
        {
            var (task, engine) = CreateTask(WriteHost(CreateHost()),
                new string('a', AssemblyNameSize - ".dll".Length) + ".dll");

            Assert.IsFalse(task.Execute());
            Assert.HasCount(1, engine.Errors);
        }

        [TestMethod]
        public void Execute_AcceptsALongAssemblyFileNameThatStillFits()
        {
            var name = new string('a', AssemblyNameSize
                - ".dll".Length - 1) + ".dll";
            var hostPath = WriteHost(CreateHost());

            Assert.IsTrue(CreateTask(hostPath, name).Task.Execute());

            var host = File.ReadAllBytes(hostPath);
            Assert.AreEqual(name, Encoding.UTF8.GetString(host, TemplateOffset
                + AssemblyNameOffset, name.Length));
            Assert.AreEqual(0, host[TemplateOffset + AssemblyNameOffset + name.Length]);
        }

        [TestMethod]
        public void Execute_FailsWhenTheHostDoesNotExist()
        {
            Directory.CreateDirectory(TempDirectory);
            var (task, engine) = CreateTask(Path.Combine(TempDirectory, "does-not-exist.bin"));

            Assert.IsFalse(task.Execute());
            Assert.HasCount(1, engine.Errors);
        }

        protected override string TempDirectoryName => "qtbridge-native-host-manifest-task-tests";

        private sealed class TestBuildEngine : IBuildEngine
        {
            public List<BuildErrorEventArgs> Errors { get; } = [];
            public bool ContinueOnError => false;
            public int LineNumberOfTaskNode => 0;
            public int ColumnNumberOfTaskNode => 0;
            public string ProjectFileOfTaskNode => "Test.proj";
            public void LogErrorEvent(BuildErrorEventArgs e) => Errors.Add(e);
            public void LogWarningEvent(BuildWarningEventArgs e) { }
            public void LogMessageEvent(BuildMessageEventArgs e) { }
            public void LogCustomEvent(CustomBuildEventArgs e) { }
            public bool BuildProjectFile(string projectFileName, string[] targetNames,
                IDictionary globalProperties, IDictionary targetOutputs) => true;
        }
    }
}
