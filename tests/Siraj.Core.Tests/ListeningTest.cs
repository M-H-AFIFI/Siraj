using Siraj.Core.Adhan;
using Siraj.Core.Audio;
using Xunit;

namespace Siraj.Core.Tests;

public class ListeningTest
{
    [Fact]
    public void RadioStationsAndReciterCatalogAreValid()
    {
        Assert.Equal(24, RadioStations.All.Count);
        Assert.Equal(RadioStations.All.Count, RadioStations.All.Select(s => s.Id).Distinct().Count());
        Assert.All(RadioStations.All, s =>
        {
            Assert.True(Uri.TryCreate(s.StreamUrl, UriKind.Absolute, out _), s.Id);
            Assert.False(string.IsNullOrWhiteSpace(s.Name("ar")));
            Assert.False(string.IsNullOrWhiteSpace(s.Name("en")));
        });

        Assert.Equal(75, ListeningLibrary.AllReciters.Count);
        Assert.Equal(12, ListeningLibrary.AmbientSounds.Count);
        Assert.Single(ListeningLibrary.RareCollections);
        Assert.Equal(13, ListeningLibrary.RareCollections[0].Tracks.Count);
        Assert.Equal("img_makkah.jpg", ListeningLibrary.PortraitForAdhan("Makkah"));
    }

    [Fact]
    public void EveryAdhanVoiceHasPortrait()
    {
        Assert.NotEmpty(AdhanVoices.All);
        Assert.All(AdhanVoices.All, voice =>
        {
            var file = ListeningLibrary.PortraitForAdhan(voice.Id);
            Assert.False(string.IsNullOrWhiteSpace(file), voice.Id);
            Assert.Equal(file, ListeningLibrary.PortraitForAdhan(voice.Id.ToLowerInvariant()));
            Assert.Equal(file, ListeningLibrary.PortraitForAdhan(voice.Id.ToUpperInvariant()));
        });
    }
}
