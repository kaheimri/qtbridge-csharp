// Copyright (C) 2026 The Qt Company Ltd.
// SPDX-License-Identifier: LicenseRef-Qt-Commercial OR LGPL-3.0-only

#pragma once

#include <QByteArray>

namespace QtDotNet {
    const char *nativeHostAssemblyName();

    bool nativeHostManifestIsValid();
    bool nativeHostManifestIsPatched();

    const char *nativeHostMetadataName();
    bool nativeHostVerifyMetadata(const QByteArray &metadata);
}
