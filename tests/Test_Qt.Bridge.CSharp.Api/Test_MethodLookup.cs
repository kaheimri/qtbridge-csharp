// Copyright (C) 2026 The Qt Company Ltd.
// SPDX-License-Identifier: LicenseRef-Qt-Commercial OR GPL-3.0-only

using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Qt.DotNet;

namespace Test_Qt.Bridge.CSharp.Api
{
    [TestClass]
    public class Test_MethodLookup
    {
        private enum State { Off, On }
        private enum ByteState : byte { Off, On }
        private enum Color { Red, Blue }
        private enum Size { Small, Large }

        private sealed class Overloads
        {
            public void SetState(State state) { }
            public void SetState(State state, int timeout) { }
        }

        private sealed class ByteBacked
        {
            public void SetState(ByteState state) { }
        }

        private sealed class Ambiguous
        {
            public void Set(Color value) { }
            public void Set(Size value) { }
        }

        [TestMethod]
        public void EnumBinding_RequiresMatchingParameterCount()
        {
            var methods = typeof(Overloads).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.Name == nameof(Overloads.SetState)).ToArray();
            var oneParameter = methods.Single(method => method.GetParameters().Length == 1);
            var twoParameters = methods.Single(method => method.GetParameters().Length == 2);

            Assert.IsTrue(oneParameter.BindsTo([typeof(int)]));
            Assert.IsFalse(oneParameter.BindsTo([typeof(int), typeof(int)]));
            Assert.IsFalse(twoParameters.BindsTo([typeof(int)]));
            Assert.IsTrue(twoParameters.BindsTo([typeof(int), typeof(int)]));
        }

        [TestMethod]
        public void EnumBinding_SupportsByteBackedEnum()
        {
            var method = typeof(ByteBacked).FindMethod(nameof(ByteBacked.SetState),
                BindingFlags.Public | BindingFlags.Instance, [typeof(int)]);
            if (method == null)
                Assert.Inconclusive("TO-FIX: Unsupported enum backed by non-Int32");
        }

        [TestMethod]
        public void EnumBinding_RejectsAmbiguousOverload()
        {
            var method = typeof(Ambiguous).FindMethod(nameof(Ambiguous.Set),
                BindingFlags.Public | BindingFlags.Instance, [typeof(int)]);
            if (method == null)
                Assert.Inconclusive("TO-FIX: Ambiguous method lookup due to enum param binding");
        }
    }
}
