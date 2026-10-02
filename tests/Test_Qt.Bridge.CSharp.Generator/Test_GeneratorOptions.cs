// Copyright (C) 2026 The Qt Company Ltd.
// SPDX-License-Identifier: LicenseRef-Qt-Commercial OR GPL-3.0-only

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Qt.Bridge.CodeGeneration;
using Test_Qt.Bridge.CSharp.Generator.Support;

namespace Test_Qt.Bridge.CSharp.Generator
{
    [TestClass]
    public class Test_GeneratorOptions
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        [DataRow("--export-as", "metadata")]
        [DataRow("--export-as", "MeTaDaTa")]
        [DataRow("--export-as", "SOURCE")]
        [DataRow("--resource-packaging", "Auto")]
        [DataRow("--resource-packaging", "linked")]
        [DataRow("--resource-packaging", "EXTERNAL")]
        public async Task ValuesAreAcceptedInAnyCase(string option, string value)
        {
            var (exitCode, output) = await RunAsync(option, value);

            Assert.AreEqual(0, exitCode, output);
        }

        [TestMethod]
        [DataRow("--export-as", "foo", "Use 'metadata' or 'source'.")]
        [DataRow("--resource-packaging", "bar", "Use 'Auto', 'Linked' or 'External'.")]
        [DataRow("--resource-packaging", "1", "Use 'Auto', 'Linked' or 'External'.")]
        public async Task UnknownValuesNameTheOptionAndTheChoices(
            string option, string value, string choices)
        {
            var (exitCode, output) = await RunAsync(option, value);

            Assert.AreNotEqual(0, exitCode, output);
            Assert.Contains($"Invalid value '{value}' for {option}.", output);
            Assert.Contains(choices, output);
            Assert.Contains("Qt Bridge: ERROR:", output);
        }

        private async Task<(int, string)> RunAsync(string option, string value)
        {
            using var fixture = await TestCodeGenerator.GenerateAsync(
                sources: [
                    "public class Example { }"
                ],
                sourceRefs: [typeof(Qt.ExportAttribute).Assembly],
                ct: TestContext.CancellationTokenSource.Token);

            var info = new ProcessStartInfo("dotnet") {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            foreach (var arg in new[] {
                typeof(Rule).Assembly.Location,
                "--source", fixture.SourceAssembly.Location,
                "--ref", AppContext.BaseDirectory,
                option, value
            }) {
                info.ArgumentList.Add(arg);
            }

            using var process = Process.Start(info)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try {
                await process.WaitForExitAsync(timeout.Token);
            } catch (OperationCanceledException) {
                process.Kill(entireProcessTree: true);
                throw;
            }
            return (process.ExitCode, await output + await error);
        }
    }
}
