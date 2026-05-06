using Microsoft.AspNetCore.DataProtection;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Security;

internal sealed class CalendarSourceSecretProtector(IDataProtectionProvider dataProtectionProvider)
    : ICalendarSourceSecretProtector
{
    private readonly IDataProtector _protector = dataProtectionProvider
        .CreateProtector("ObfusCal.CalendarSourceInstances.SecretStore.v1");

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string protectedValue) => _protector.Unprotect(protectedValue);
}

