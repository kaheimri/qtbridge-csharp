// Copyright (C) 2026 The Qt Company Ltd.
// SPDX-License-Identifier: LicenseRef-Qt-Commercial OR LGPL-3.0-only

using System.Text;
using Microsoft.Build.Framework;

namespace Qt.Bridge.CSharp.Build.Tasks
{
    /// <summary>
    /// Writes the Qt Bridge manifest in the native host template. The manifest area
    /// is in the first region, and the .NET SDK apphost placeholder in the second.
    /// </summary>
    public sealed class PatchNativeHostManifest : Microsoft.Build.Utilities.Task
    {
        internal const int ManifestSize = 1024;
        private const int SdkSlotSize = 1024;
        internal const int TemplateSize = ManifestSize + SdkSlotSize;

        // Field offsets from the start of the manifest region. Name fields are
        // 256 bytes. The metadata and .rcc names are followed by raw 32 byte
        // SHA-256 checksum. The four-byte manifest checksum is part of the defined
        // payload, but covers only the preceding bytes.
        internal const int ManifestVersionOffset = 4;
        internal const int ManifestPayloadSizeOffset = 6;
        internal const int AssemblyNameOffset = 8;
        internal const int AssemblyNameSize = 256;

        // Note: explicitly omit the assembly checksum field

        private const int MetadataNameOffset = 264;
        private const int MetadataNameSize = 256;
        private const int MetadataChecksumOffset = 520;

        private const int RccNameOffset = 552;
        private const int RccNameSize = 256;
        private const int RccChecksumOffset = 808;

        private const int ManifestChecksumOffset = 840;
        private const int ManifestChecksumSize = 4;
        internal const int ManifestPayloadSize = 844;

        internal const string Marker = "QTBRIDGE_HOST_MANIFEST_V1__UNPATCHED__";
        private static readonly byte[] UnpatchedMarker = Encoding.ASCII.GetBytes(Marker);

        // Magic .NET SDK apphost placeholder, which is a 64-character hex string. Injected
        // in our own version of the apphost, rather than compiled in like the SDK's one.
        private const string EMBED_HASH_HI_PART_UTF8 = "c3ab8ff13720e8ad9047dd39466b3c89";
        private const string EMBED_HASH_LO_PART_UTF8 = "74e592c2fa383d4a3960714caef0c4f2";
        internal const string SdkPlaceholder = EMBED_HASH_HI_PART_UTF8 + EMBED_HASH_LO_PART_UTF8;
        private static readonly byte[] SdkPlaceholderBytes = Encoding.ASCII.GetBytes(SdkPlaceholder);

        [Required]
        public string HostPath { get; set; } = "";

        [Required]
        public string AssemblyFileName { get; set; } = "";

        public override bool Execute()
        {
            if (!File.Exists(HostPath)) {
                Log.LogError($"Native host not found: '{HostPath}'.");
                return false;
            }

            var assemblyName = Encoding.UTF8.GetBytes(AssemblyFileName);
            switch (assemblyName.Length) {
            case 0:
                Log.LogError("Native host assembly file name must not be empty.");
                return false;
            case >= AssemblyNameSize:
                Log.LogError($"Native host assembly file name must be shorter than "
                    + $"{AssemblyNameSize} UTF-8 bytes.");
                return false;
            }

            if (Array.IndexOf(assemblyName, (byte)0) >= 0) {
                Log.LogError("Native host assembly file name must not contain a NUL character.");
                return false;
            }

            try {
                return Patch(assemblyName);
            } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                Log.LogErrorFromException(ex, showStackTrace: false);
                return false;
            }
        }

        private bool Patch(byte[] assemblyName)
        {
            var hostBytes = File.ReadAllBytes(HostPath);
            var templateOffset = FindUniqueMarker(hostBytes);
            if (templateOffset < 0)
                return false;

            if (hostBytes.Length - templateOffset < TemplateSize) {
                Log.LogError("Native host template is truncated.");
                return false;
            }

            // Zero-initialize the entire template so that:
            //  - The assembly name is null-terminated if shorter than AssemblyNameSize
            //  - The SDK placeholder area is zero-padded beyond the magic .NET SDK marker
            var template = new byte[TemplateSize];

            // Qt manifest
            template[0] = (byte)'Q';
            template[1] = (byte)'T';
            template[2] = (byte)'B';
            template[3] = (byte)'M';
            // Format version 1 and the payload length, both little-endian uint16.
            template[ManifestVersionOffset] = 1;
            template[ManifestVersionOffset + 1] = 0;
            template[ManifestPayloadSizeOffset] = (byte)(ManifestPayloadSize & 0xff);
            template[ManifestPayloadSizeOffset + 1] = (byte)(ManifestPayloadSize >> 8);
            Buffer.BlockCopy(assemblyName, 0, template, AssemblyNameOffset, assemblyName.Length);

            // .NET SDK placeholder
            Buffer.BlockCopy(SdkPlaceholderBytes, 0, template, ManifestSize,
                SdkPlaceholderBytes.Length);

            using var stream = new FileStream(HostPath, FileMode.Open, FileAccess.Write,
                FileShare.None);
            stream.Seek(templateOffset, SeekOrigin.Begin);
            stream.Write(template, 0, template.Length);
            return true;
        }

        private int FindUniqueMarker(byte[] hostBytes)
        {
            var foundAt = -1;
            var limit = hostBytes.Length - UnpatchedMarker.Length;

            var nextStartIndex = NextMarkerStartIndex(hostBytes, 0, limit);
            while (nextStartIndex >= 0) {
                if (IsUnpatchedMarkerAt(hostBytes, nextStartIndex)) {
                    if (foundAt >= 0) {
                        Log.LogError("Native host contains more than one unpatched manifest marker.");
                        return -1;
                    }
                    foundAt = nextStartIndex;
                }
                nextStartIndex = NextMarkerStartIndex(hostBytes, nextStartIndex + 1, limit);
            }
            if (foundAt < 0)
                Log.LogError("Native host manifest marker was not found.");
            return foundAt;
        }

        private static int NextMarkerStartIndex(byte[] hostBytes, int from, int limit)
        {
            while (from <= limit) {
                var pos = Array.IndexOf(hostBytes, UnpatchedMarker[0], from);
                if (pos < 0 || pos > limit)
                    return -1;

                if (hostBytes[pos + 1] == UnpatchedMarker[1]
                    && hostBytes[pos + 2] == UnpatchedMarker[2]
                    && hostBytes[pos + 3] == UnpatchedMarker[3]) {
                    return pos;
                }
                from = pos + 1;
            }
            return -1;
        }

        private static bool IsUnpatchedMarkerAt(byte[] hostBytes, int offset)
        {
            for (var i = 0; i < UnpatchedMarker.Length; ++i) {
                if (hostBytes[offset + i] != UnpatchedMarker[i])
                    return false;
            }
            return true;
        }
    }
}
