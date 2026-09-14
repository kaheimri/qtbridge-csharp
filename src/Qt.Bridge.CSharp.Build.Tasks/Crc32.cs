// Copyright (C) 2026 The Qt Company Ltd.
// SPDX-License-Identifier: LicenseRef-Qt-Commercial OR LGPL-3.0-only

namespace Qt.Bridge.CSharp.Build.Tasks
{
    /// <summary>
    /// Modeled after the implementation in zlib, which is public domain.
    ///
    /// CRC-32 implementation with polynomial 0xedb88320, initial and final value 0xffffffff.
    /// The implementation needs to stay in sync with the implementation in native_host.cpp.
    /// </summary>
    internal static class Crc32
    {
        private static readonly uint[] Table = CreateTable();

        public static uint Compute(byte[] data, int offset, int count)
        {
            var crc = 0xffffffffu;
            for (var i = offset; i < offset + count; ++i)
                crc = Table[(crc ^ data[i]) & 0xff] ^ (crc >> 8);
            return crc ^ 0xffffffffu;
        }

        private static uint[] CreateTable()
        {
            var table = new uint[256];
            for (var i = 0u; i < 256u; ++i) {
                var value = i;
                for (var bit = 0; bit < 8; ++bit)
                    value = (value & 1) != 0 ? 0xedb88320u ^ (value >> 1) : value >> 1;
                table[i] = value;
            }
            return table;
        }
    }
}
