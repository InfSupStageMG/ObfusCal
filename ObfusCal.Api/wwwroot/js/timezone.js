globalThis.obfusCalTime = globalThis.obfusCalTime || {};

const obfusCalTimeCacheKey = "obfuscal.dashboard.timezone";
let obfusCalCachedTimeZone = null;

function readCachedTimeZoneFromStorage() {
    try {
        return globalThis.localStorage.getItem(obfusCalTimeCacheKey);
    } catch {
        return null;
    }
}

function writeCachedTimeZoneToStorage(timeZoneId) {
    if (!timeZoneId) {
        return;
    }

    try {
        globalThis.localStorage.setItem(obfusCalTimeCacheKey, timeZoneId);
    } catch {
        // Ignore storage failures (privacy mode, disabled storage, etc.)
    }
}

globalThis.obfusCalTime.getBrowserTimeZone = function () {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || null;
};

globalThis.obfusCalTime.getCachedOrBrowserTimeZone = function () {
    if (obfusCalCachedTimeZone) {
        return obfusCalCachedTimeZone;
    }

    const stored = readCachedTimeZoneFromStorage();
    if (stored) {
        obfusCalCachedTimeZone = stored;
        return stored;
    }

    const detected = globalThis.obfusCalTime.getBrowserTimeZone();
    if (detected) {
        obfusCalCachedTimeZone = detected;
        writeCachedTimeZoneToStorage(detected);
    }

    return detected || null;
};

