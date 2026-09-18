// Copyright (C) 2026 The Qt Company Ltd.
// SPDX-License-Identifier: LicenseRef-Qt-Commercial OR GPL-3.0-only

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test_Qt.Bridge.CSharp.Generator
{
    using Qt.Bridge;
    using Support;

    [TestClass]
    public class Test_MetadataEnumGeneration
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task Enum_ExportedAs_Int32()
        {
            const string source = """
            using Qt;

            [assembly: Export(Options = ExportAs.Metadata)]

            namespace Test
            {
                public enum State { Off, On }

                public class Device
                {
                    public State State { get; set; }

                    public void Assert(State s) => System.Diagnostics.Debug.Assert(State == s);
                }
            }
            """;

            using var result = await TestCodeGenerator.GenerateAsync([source],
                sourceRefs:
                [
                    typeof(Qt.ExportAttribute).Assembly,
                    typeof(Qt.Bridge.CodeGeneration.Rules.Metadata.GenerateProperty).Assembly
                ],
                ct: TestContext.CancellationTokenSource.Token);

            Assert.IsTrue(result.Sink.Files.TryGetValue(TestCodeGenerator.MetadataFileName, out var json),
                string.Join(", ", result.Sink.Files.Keys));
            var metadata = Metadata.FromJson(json);
            Assert.IsGreaterThan(0, metadata.Types.Length);

            Assert.IsTrue(metadata.TypesByName.TryGetValue("Test.State", out var enumState));
            Assert.IsNotNull(enumState);
            Assert.IsNotNull(enumState.Qt);
            Assert.IsNotNull(enumState.Qt.Enum);
            Assert.AreEqual(2, enumState.Qt.Enum.Count);
            Assert.AreEqual(0, enumState.Qt.Enum["Off"]);
            Assert.AreEqual(1, enumState.Qt.Enum["On"]);

            Assert.IsTrue(metadata.TypesByName.TryGetValue("Test.Device", out var classDevice));
            Assert.IsNotNull(classDevice);

            Assert.IsNotNull(classDevice.Methods);
            Assert.IsTrue(classDevice.MethodsByName.TryGetValue("Assert", out var methods));
            Assert.IsNotNull(methods);
            Assert.AreEqual(1, methods.Length);
            Assert.AreEqual(BasicType.Void, methods.First().Qt.ReturnType);
            Assert.AreEqual(1, methods.First().Qt.Parameters.Length);
            Assert.AreEqual(BasicType.QInt32, methods.First().Qt.Parameters[0].Type);

            Assert.IsNotNull(classDevice.Properties);
            Assert.IsTrue(classDevice.PropertiesByName.TryGetValue("State", out var propState));
            Assert.IsNotNull(propState);
            Assert.AreEqual(BasicType.QInt32, propState.Qt.Type);
        }

        [TestMethod]
        public async Task ByteBackedEnum_Rejected_WithWarning()
        {
            const string source = """
            using Qt;

            [assembly: Export(Options = ExportAs.Metadata)]

            namespace Test
            {
                public enum State : byte { Off, On }

                public class Device
                {
                    public State State { get; set; }

                    public void Assert(State s) => System.Diagnostics.Debug.Assert(State == s);

                    public int this[State s] => 0;
                }
            }
            """;

            using var result = await TestCodeGenerator.GenerateAsync([source],
                sourceRefs:
                [
                    typeof(Qt.ExportAttribute).Assembly,
                    typeof(Qt.Bridge.CodeGeneration.Rules.Metadata.GenerateProperty).Assembly
                ],
                ct: TestContext.CancellationTokenSource.Token);

            Assert.Contains(collection: Rules.Results, predicate: r => r.Warning
                && r.Source is Type t && t.Name == "State");
            Assert.Contains(collection: Rules.Results, predicate: r => r.Warning
                && r.Source is PropertyInfo p && p.ReflectedType.Name == "Device"
                && p.Name == "State");
            Assert.Contains(collection: Rules.Results, predicate: r => r.Warning
                && r.Source is MethodInfo m && m.ReflectedType.Name == "Device"
                && m.Name == "Assert");

            Assert.IsTrue(result.Sink.Files.TryGetValue(TestCodeGenerator.MetadataFileName, out var json),
                string.Join(", ", result.Sink.Files.Keys));
            var metadata = Metadata.FromJson(json);

            Assert.IsFalse(metadata.TypesByName.TryGetValue("Test.State", out var _));
            Assert.IsTrue(metadata.TypesByName.TryGetValue("Test.Device", out var classDevice));
            Assert.IsNotNull(classDevice?.MethodsByName);
            Assert.IsFalse(classDevice.MethodsByName.TryGetValue("Assert", out _));
            Assert.IsFalse(classDevice.MethodsByName.TryGetValue("get_Item", out _));
        }

        [TestMethod]
        public async Task AmbiguousOverload_Rejected_WithWarning()
        {
            const string source = """
            using Qt;

            [assembly: Export(Options = ExportAs.Metadata)]

            namespace Test
            {
                public enum Color { Red, Blue }
                public enum Size { Small, Large }

                public class Device
                {
                    public void SetProperty(Color value) { }
                    public void SetColor(Color value) { }
                    public void SetSize(Size value) { }
                }

                public class Brush : Device
                {
                    public void SetProperty(Size value) { }
                    public new void SetColor(Color value) { }
                    public new void SetSize(Size value) { }
                    public void Assert(Color value) { }
                    public void Assert(Size value) { }
                }
            }
            """;

            using var result = await TestCodeGenerator.GenerateAsync([source],
                sourceRefs:
                [
                    typeof(Qt.ExportAttribute).Assembly,
                    typeof(Qt.Bridge.CodeGeneration.Rules.Metadata.GenerateProperty).Assembly
                ],
                ct: TestContext.CancellationTokenSource.Token);

            Assert.Contains(collection: Rules.Results, predicate: r => r.Succeeded && !r.Warning
                && r.Source is MethodInfo m && m.ReflectedType.Name == "Brush"
                && m.Name == "SetProperty" && m.GetParameters()[0].ParameterType.Name == "Color");
            Assert.Contains(collection: Rules.Results, predicate: r => r.Warning
                && r.Source is MethodInfo m && m.ReflectedType.Name == "Brush"
                && m.Name == "SetProperty" && m.GetParameters()[0].ParameterType.Name == "Size");
            Assert.Contains(collection: Rules.Results, predicate: r => r.Warning
                && r.Source is MethodInfo m && m.ReflectedType.Name == "Brush"
                && m.Name == "Assert" && m.GetParameters()[0].ParameterType.Name == "Color");
            Assert.Contains(collection: Rules.Results, predicate: r => r.Warning
                && r.Source is MethodInfo m && m.ReflectedType.Name == "Brush"
                && m.Name == "Assert" && m.GetParameters()[0].ParameterType.Name == "Size");

            Assert.IsTrue(result.Sink.Files.TryGetValue(TestCodeGenerator.MetadataFileName, out var json),
                string.Join(", ", result.Sink.Files.Keys));
            var metadata = Metadata.FromJson(json);

            Assert.IsTrue(metadata.TypesByName.TryGetValue("Test.Device", out var classDevice));
            Assert.IsNotNull(classDevice?.MethodsByName);
            Assert.IsTrue(classDevice.MethodsByName.TryGetValue("SetProperty", out _));
            Assert.IsTrue(classDevice.MethodsByName.TryGetValue("SetColor", out _));
            Assert.IsTrue(classDevice.MethodsByName.TryGetValue("SetSize", out _));

            Assert.IsTrue(metadata.TypesByName.TryGetValue("Test.Brush", out var classBrush));
            Assert.IsNotNull(classBrush?.MethodsByName);
            Assert.IsFalse(classBrush.MethodsByName.TryGetValue("SetProperty", out _));
            Assert.IsTrue(classBrush.MethodsByName.TryGetValue("SetColor", out _));
            Assert.IsTrue(classBrush.MethodsByName.TryGetValue("SetSize", out _));
            Assert.IsFalse(classBrush.MethodsByName.TryGetValue("Assert", out _));
        }
    }
}
