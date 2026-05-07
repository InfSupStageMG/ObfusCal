using ObfusCal.Application.Obfuscation;

namespace ObfusCal.Api.Components.Pages;

public partial class CalendarOwnerDetail
{
    private async Task LoadProfilesAsync()
    {
        var profiles = await ObfuscationProfileService.GetProfilesAsync(Id);
        _profiles = profiles.Select(p => new ProfileViewModel
        {
            Context = p.Context,
            RemoveTitle = p.RemoveTitle,
            RemoveDescription = p.RemoveDescription,
            RemoveLocation = p.RemoveLocation,
            RemoveAttendees = p.RemoveAttendees,
            RoundTimes = p.RoundTimes,
            RoundingIntervalMinutes = p.RoundingIntervalMinutes,
            MergeBlocks = p.MergeBlocks
        }).ToList();

        foreach (var ctx in Enum.GetValues<ObfuscationAuditContext>())
        {
            if (_profiles.All(p => p.Context != ctx))
            {
                var def = ObfuscationProfileSettings.CreateDefault(ctx);
                _profiles.Add(new ProfileViewModel
                {
                    Context = ctx,
                    RemoveTitle = def.RemoveTitle,
                    RemoveDescription = def.RemoveDescription,
                    RemoveLocation = def.RemoveLocation,
                    RemoveAttendees = def.RemoveAttendees,
                    RoundTimes = def.RoundTimes,
                    RoundingIntervalMinutes = def.RoundingIntervalMinutes,
                    MergeBlocks = def.MergeBlocks
                });
            }
        }
    }

    private async Task SaveProfileAsync(ProfileViewModel profile)
    {
        _profileMessage = null;
        var settings = new ObfuscationProfileSettings(
            profile.Context,
            profile.RemoveTitle,
            profile.RemoveDescription,
            profile.RemoveLocation,
            profile.RemoveAttendees,
            profile.RoundTimes,
            profile.RoundingIntervalMinutes,
            profile.MergeBlocks);

        await ObfuscationProfileService.SetProfileAsync(Id, settings);
        _profileMessage = $"{profile.Context} profile saved.";
    }
}

