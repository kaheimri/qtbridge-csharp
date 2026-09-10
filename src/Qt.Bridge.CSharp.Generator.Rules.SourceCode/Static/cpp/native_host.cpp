// Copyright (C) 2026 The Qt Company Ltd.
// SPDX-License-Identifier: LicenseRef-Qt-Commercial OR LGPL-3.0-only

#include <native_host.h>

#include <cstddef>

// The following lines make sure the template byte array survives optimization and
// linking, because the build task later needs to find it by scanning the finished
// executable.
//
// MSVC:
// #pragma comment(linker, ...) tells the compiler to embed an instruction for
// the linker: "keep this symbol." It prevents linker dead-code elimination
// from discarding the template array merely because it appears unused or becomes
// unused after optimization.
// MSVC needs the symbol's exact linker spelling, the leading underscore is present
// on x86 but not on x64 or ARM64.
//
// GCC and Clang:
// 'used' tells the compiler not to omit the variable during compilation, even if it
// cannot see an ordinary C++ use.
//
// The normal runtime accessor also references it, but this is an additional safety
// measure: the manifest must remain present for the build task's binary scan.
#if defined(_MSC_VER)
#  if defined(_M_IX86)
#    pragma comment(linker, "/include:_qtbNativeHostTemplate")
#  else
#    pragma comment(linker, "/include:qtbNativeHostTemplate")
#  endif
#  define QTB_RETAIN
#elif defined(__GNUC__) || defined(__clang__)
#  define QTB_RETAIN __attribute__((used))
#else
#  define QTB_RETAIN
#endif

namespace
{
    // The template holds two adjacent regions of equal size:
    //
    //   [0, 1024)      Qt Bridge manifest, written by the PatchNativeHostManifest build task
    //   [1024, 2048)   slot for the .NET SDK app host placeholder, injected by the same task
    //
    // One object rather than two arrays, because the task addresses the second region by a
    // fixed offset from the first. Separate objects may be reordered, aligned apart, or placed
    // in different sections, which would make that offset meaningless.
    constexpr std::size_t ManifestOffset = 0;
    constexpr std::size_t ManifestSize = 1024;
    constexpr std::size_t SdkSlotSize = 1024;
    constexpr std::size_t TemplateSize = ManifestSize + SdkSlotSize;
    constexpr std::size_t ManifestPayloadSize = 844;

    constexpr std::size_t AssemblyNameOffset = 8; // offset from the start
    constexpr std::size_t AssemblyNameSize = 256; // maximum length of the field

    bool matches(const volatile unsigned char *value, const unsigned char *expected,
                 const std::size_t size)
    {
        for (std::size_t i = 0; i < size; ++i) {
            if (value[i] != expected[i])
                return false;
        }
        return true;
    }

    // Magic bytes "QTBM" at offsets 0-3, format version 1 at offsets 4-5, and payload length
    // 844 at offsets 6-7, all integer fields little-endian uint16.
    bool isValidManifest(const volatile unsigned char *manifest)
    {
        // Signature for the Qt Bridge manifest: "QTBM" (Qt Bridge Manifest).
        constexpr unsigned char manifestMagic[] = { 'Q', 'T', 'B', 'M' };
        return matches(manifest, manifestMagic, sizeof(manifestMagic)) && manifest[4] == 1
                && manifest[5] == 0
                && manifest[6] == (ManifestPayloadSize & 0xff)
                && manifest[7] == (ManifestPayloadSize >> 8);
    }
}

#define QTB_FILL_16 'Q', 't', 'B', 'r', 'i', 'd', 'g', 'e', 'H', 'o', 's', 't', 'F', 'i', 'l', 'l'
#define QTB_FILL_64 QTB_FILL_16, QTB_FILL_16, QTB_FILL_16, QTB_FILL_16
#define QTB_FILL_256 QTB_FILL_64, QTB_FILL_64, QTB_FILL_64, QTB_FILL_64
#define QTB_FILL_1024 QTB_FILL_256, QTB_FILL_256, QTB_FILL_256, QTB_FILL_256

// Use C language linkage so the symbol name is not mangled by the C++ compiler.
// This keeps the symbol spelling stable for the MSVC retention directives above.
//
// The marker is a character list rather than a string literal on purpose: a literal may
// be pooled, leaving a second copy in the binary, and the task requires the marker to occur
// exactly once.
//
// Every byte of the template needs to be non-zero in the compiled binary, so that the
// toolchain cannot treat the tail as trailing zeros and leave it out of the file image.
// The build task writes zero padding where the manifest and the SDK placeholder require it.
extern "C" {

QTB_RETAIN volatile unsigned char qtbNativeHostTemplate[TemplateSize] = {

    // Layout of the manifest region:
    //   - 38-byte signature string ("QTBRIDGE_HOST_MANIFEST_V1__UNPATCHED__")
    //   - 26-byte padding to 64 bytes (digits + letters)
    //   - 960 bytes of non-zero filler (prevents toolchain trimming)
    'Q', 'T', 'B', 'R', 'I', 'D', 'G', 'E', '_', 'H', 'O', 'S', 'T', '_', 'M', 'A', 'N', 'I', 'F',
    'E', 'S', 'T', '_', 'V', '1', '_', '_', 'U', 'N', 'P', 'A', 'T', 'C', 'H', 'E', 'D', '_', '_',
    '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I',
    'J', 'K', 'L', 'M', 'N', 'O', 'P', QTB_FILL_256, QTB_FILL_256, QTB_FILL_256, QTB_FILL_64,
    QTB_FILL_64, QTB_FILL_64,

    // .NET SDK app host placeholder slot: filler only. The build task writes the hash.
    QTB_FILL_1024
};

} // extern "C"

namespace QtDotNet
{
    // Returns the managed assembly file name or nullptr if the host is still unpatched or its
    // manifest header is not valid. Only the manifest region is ever read, ignore the SDK slot.
    const char *nativeHostAssemblyName()
    {
        const volatile unsigned char *manifest = &qtbNativeHostTemplate[ManifestOffset];
        if (!isValidManifest(manifest))
            return nullptr;

        for (std::size_t i = 0; i < AssemblyNameSize; ++i) {
            if (manifest[AssemblyNameOffset + i] == 0) {
                return i == 0
                    ? nullptr // Empty name field -> invalid manifest
                    : const_cast<const char *>(reinterpret_cast<const volatile char *>(manifest))
                        + AssemblyNameOffset; // null-terminated assembly name
            }
        }
        return nullptr;
    }
}
