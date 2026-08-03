using Linewise.Application.Persistence;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Linewise.Tests.Persistence;
using Xunit;

namespace Linewise.Tests.Printing;

public sealed class PrintSettingsTests
{
    [Fact]
    public async Task AFreshInstallationCanPrintBeforeAnybodyConfiguresAnything()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        var settings = await GetAsync(database);

        // Defaults rather than null. Requiring a trip through a settings screen before the
        // first sheet can be printed is the sort of thing that stops a tool being adopted.
        Assert.Equal(PaperSize.A4, settings.PaperSize);
        Assert.Equal(PageOrientation.Landscape, settings.Orientation);
        Assert.Equal(12, settings.BaseFontPoints);
        Assert.False(settings.UseAccentColours);
    }

    [Fact]
    public async Task SettingsRoundTrip()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        await database.InScopeAsync<IConfigurationRepository>(repository =>
            repository.SavePrintSettingsAsync(new PrintSettings
            {
                Id = Guid.NewGuid(),
                CompanyName = "A Food Manufacturer",
                PaperSize = PaperSize.A3,
                Orientation = PageOrientation.Portrait,
                BaseFontPoints = 18,
                UseAccentColours = true,
                LogoPng = [0x89, 0x50, 0x4E, 0x47],
            }));

        var settings = await GetAsync(database);

        Assert.Equal("A Food Manufacturer", settings.CompanyName);
        Assert.Equal(PaperSize.A3, settings.PaperSize);
        Assert.Equal(18, settings.BaseFontPoints);
        Assert.True(settings.UseAccentColours);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], settings.LogoPng);
    }

    [Fact]
    public async Task SavingAgainReplacesRatherThanAccumulating()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var id = Guid.NewGuid();

        await database.InScopeAsync<IConfigurationRepository>(repository =>
            repository.SavePrintSettingsAsync(new PrintSettings { Id = id, BaseFontPoints = 14 }));

        await database.InScopeAsync<IConfigurationRepository>(repository =>
            repository.SavePrintSettingsAsync(new PrintSettings { Id = id, BaseFontPoints = 22 }));

        Assert.Equal(22, (await GetAsync(database)).BaseFontPoints);
    }

    private static Task<PrintSettings> GetAsync(TemporaryDatabase database) =>
        database.InScopeAsync<IConfigurationRepository, PrintSettings>(
            repository => repository.GetPrintSettingsAsync());
}
