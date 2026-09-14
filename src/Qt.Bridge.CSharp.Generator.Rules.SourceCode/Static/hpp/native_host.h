// Copyright (C) 2026 The Qt Company Ltd.
// SPDX-License-Identifier: LicenseRef-Qt-Commercial OR LGPL-3.0-only

#pragma once

namespace QtDotNet {
    const char *nativeHostAssemblyName();

    bool nativeHostManifestIsValid();
    bool nativeHostManifestIsPatched();
}
