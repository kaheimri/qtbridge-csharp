// Copyright (C) 2026 The Qt Company Ltd.
// SPDX-License-Identifier: LicenseRef-Qt-Commercial OR GPL-3.0-only

using System.Diagnostics;
using System.Security;
using Qt.Bridge.CSharp.Build.Tasks;

namespace Test_Qt.Bridge.CSharp.Build.Tasks
{
    [TestClass]
    public sealed class Test_NativeDeployment : TestBase
    {
        [TestMethod]
        public void CMakeDeploymentDoesNotCopyTheNativeHostToTheApplicationOutput()
        {
            var cmakeDirectory = Path.Combine(TempDirectory, "tools");
            var projectPath = WriteProject();
            WriteFakeCMake(cmakeDirectory);

            var result = RunMsBuild(projectPath, cmakeDirectory);

            Assert.AreEqual(0, result.ExitCode, result.Output);
            var output = Path.Combine(TempDirectory, "output");
            Assert.IsFalse(File.Exists(Path.Combine(output, "TargetTestApp")));
            Assert.IsTrue(File.Exists(Path.Combine(output, "plugins", "platforms.txt")));

            var staged = Path.Combine(TempDirectory, "native", "build", "deploy");
            Assert.IsFalse(File.Exists(Path.Combine(staged, "TargetTestApp")));
            var deployed = File.ReadAllText(Path.Combine(output, "qtdeploy.txt"));
            Assert.Contains("plugins", deployed);
            Assert.DoesNotContain("TargetTestApp", deployed);
        }

        protected override string TempDirectoryName => "qtbridge-native-deployment-tests";

        private string WriteProject()
        {
            Directory.CreateDirectory(TempDirectory);
            var nativeHost = Path.Combine(TempDirectory, "native", "bin", "TargetTestApp");
            Directory.CreateDirectory(Path.GetDirectoryName(nativeHost)!);
            File.WriteAllText(nativeHost, "native host");

            var targets = Path.Combine(FindRepositoryRoot(), "build", "Qt.Bridge.targets");
            var taskAssembly = typeof(PrepareQmlBuildMetadata).Assembly.Location;
            var project = Path.Combine(TempDirectory, "NativeDeployment.proj");
            File.WriteAllText(project, $"""
                <Project>
                  <PropertyGroup>
                    <DesignTimeBuild>false</DesignTimeBuild>
                    <QtGenSourceCode>true</QtGenSourceCode>
                    <QtHostIsWindows>false</QtHostIsWindows>
                    <QtHostIsLinux>true</QtHostIsLinux>
                    <QtHostIsMacOS>false</QtHostIsMacOS>
                    <QtNativeBuildDir>native/build</QtNativeBuildDir>
                    <QtNativeHostPath>{XmlEscape(nativeHost)}</QtNativeHostPath>
                    <TargetDir>$(MSBuildProjectDirectory)/output/</TargetDir>
                    <QtBridgeBuildTasks>{XmlEscape(taskAssembly)}</QtBridgeBuildTasks>
                  </PropertyGroup>
                  <Import Project="{XmlEscape(targets)}" />
                  <Target Name="QtBridgeBuild" />
                </Project>
                """);
            return project;
        }

        private static void WriteFakeCMake(string directory)
        {
            Directory.CreateDirectory(directory);
            if (OperatingSystem.IsWindows()) {
                File.WriteAllText(Path.Combine(directory, "cmake.cmd"), """
                    @echo off
                    mkdir "%4\plugins"
                    > "%4\TargetTestApp" echo unpatched native host
                    > "%4\plugins\platforms.txt" echo deployed runtime
                    """);
                return;
            }

            var path = Path.Combine(directory, "cmake");
            File.WriteAllText(path, """
                #!/bin/sh
                mkdir -p "$4/plugins"
                printf 'unpatched native host' > "$4/TargetTestApp"
                printf 'deployed runtime' > "$4/plugins/platforms.txt"
                """);
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute);
        }

        private static BuildResult RunMsBuild(string project, string cmakeDirectory)
        {
            var info = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            info.Environment["PATH"] = cmakeDirectory + Path.PathSeparator
                + info.Environment["PATH"];
            info.ArgumentList.Add("msbuild");
            info.ArgumentList.Add(project);
            info.ArgumentList.Add("/t:QtBridgeDeploy_SourceCode_Linux_MacOS");
            info.ArgumentList.Add("/nologo");
            info.ArgumentList.Add("/v:minimal");

            using var process = Process.Start(info)
                ?? throw new InvalidOperationException("Failed to start dotnet msbuild.");
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return new BuildResult(process.ExitCode, output + error);
        }

        private sealed record BuildResult(int ExitCode, string Output);
    }
}
