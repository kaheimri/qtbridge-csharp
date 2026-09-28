// Copyright (C) 2026 The Qt Company Ltd.
// SPDX-License-Identifier: LicenseRef-Qt-Commercial OR GPL-3.0-only

using System;
using System.Diagnostics;
using System.IO;

[assembly: Qt.Generate(Packages = "Test", Libraries = "Qt6::Test")]

namespace Test_InboundMemLeak
{
    public static class Functions
    {
        private const int TargetSampleCount = 100;
        private const int WarmupPercent = 10;
        private static List<double> SampleCalls { get; } = new(100);
        private static List<double> SampleBytes { get; } = new(100);

        private static int CallCount { get; set; } = 0;
        private static int WarmupCalls { get; set; } = 0;
        private static int CallsPerSample { get; set; } = 1;

        private static long AllocatedBytes()
        {
            using var process = Process.GetCurrentProcess();
            return process.PrivateMemorySize64;
        }

        public static double Correlation()
        {
            var r = MathNet.Numerics.Statistics.Correlation.Pearson(SampleCalls, SampleBytes);
            // A constant memory sample has no variance, making Pearson correlation
            // undefined. It also cannot show a positive correlation with call count.
            return double.IsNaN(r) ? 0 : r;
        }

        public static int SampleCount() => SampleCalls.Count;

        public static double RetainedBytes()
        {
            if (SampleBytes.Count < 2)
                return 0;

            // Comparing windows makes this less sensitive to page-sized jumps in the
            // process working set than comparing the first and last samples directly.
            int windowSize = Math.Min(10, SampleBytes.Count / 2);
            double first = SampleBytes.Take(windowSize).Average();
            double last = SampleBytes.TakeLast(windowSize).Average();
            return last - first;
        }

        public static void ConfigureSampling(int totalCalls)
        {
            if (totalCalls < 2)
                throw new ArgumentOutOfRangeException(nameof(totalCalls));

            // Sampling by call count is deterministic and avoids delaying every inbound call
            // merely to spread measurements over wall-clock time. Ignore initial calls so
            // runtime/JIT startup growth is not mistaken for retained per-call allocations.
            CallCount = 0;
            WarmupCalls = totalCalls * WarmupPercent / 100;
            CallsPerSample = Math.Max(1,
                (totalCalls - WarmupCalls) / TargetSampleCount);
            SampleCalls.Clear();
            SampleBytes.Clear();
        }

        public static void InboundVoid()
        {
            CallCount++;
            if (CallCount <= WarmupCalls
                || (CallCount - WarmupCalls) % CallsPerSample != 0)
                return;

            Stopwatch delayTimer = Stopwatch.StartNew();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            while (delayTimer.Elapsed.TotalNanoseconds < 400000) ;

            SampleCalls.Add(CallCount);
            SampleBytes.Add(AllocatedBytes());
        }

        private static T Inbound<T>(T value)
        {
            InboundVoid();
            return value;
        }

        public static int InboundInt32() => Inbound(valueInt32);

        public static char InboundChar() => Inbound(valueChar);

        public static string InboundString() => Inbound(valueString);

        public static DateTime InboundDateTime() => Inbound(valueDateTime);

        public static Uri InboundUri() => Inbound(valueUri);

        public static object InboundObject() => Inbound(valueObject);

        private static readonly int valueInt32 = Environment.ProcessorCount;
        private static readonly char valueChar = Path.DirectorySeparatorChar;
        private static readonly string valueString = Environment.CommandLine;
        private static readonly DateTime valueDateTime = DateTime.Now;
        private static readonly Uri valueUri = new("https://qt.io");
        private static readonly object valueObject = new();
    }

    internal class Program
    {
        static int Main(string[] args)
        {
            return 0;
        }
    }
}
