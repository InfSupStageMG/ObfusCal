globalThis.obfusCalTime = globalThis.obfusCalTime || {};

globalThis.obfusCalTime.getBrowserTimeZone = function () {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || null;
};

