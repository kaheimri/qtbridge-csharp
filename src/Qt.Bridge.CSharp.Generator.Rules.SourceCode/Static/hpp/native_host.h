// Copyright (C) 2026 The Qt Company Ltd.
// SPDX-License-Identifier: LicenseRef-Qt-Commercial OR LGPL-3.0-only

#pragma once

#include <QByteArray>
#include <QString>

namespace QtDotNet {
    const char *nativeHostAssemblyName();
    QString nativeHostApplicationDirPath();

    bool nativeHostManifestIsValid();
    bool nativeHostManifestIsPatched();

    const char *nativeHostMetadataName();
    bool nativeHostVerifyMetadata(const QByteArray &metadata);
}
