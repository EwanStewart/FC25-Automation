using Automation.Sbc;

namespace Automation.Tests;

public class FormationReaderTests
{
    private static string Directory => FormationFixture.Read("directory.json");

    [Fact]
    public void ReadsASlideIntoALayoutOrderedBySlotIndex()
    {
        var reading = FormationReader.Read(Directory, [FormationFixture.Read("slide-f442.json")]);

        Assert.Equal(FormationCaptureOutcome.Captured, reading.Outcome);
        var layout = Assert.Single(reading.Layouts);
        Assert.Equal("f442", layout.Code);
        Assert.Equal(16, layout.FormationId);
        Assert.Equal("4-4-2", layout.DisplayName);
        Assert.Equal(["GK", "RB", "CB", "CB", "LB", "RM", "CM", "CM", "LM", "ST", "ST"], layout.Slots);
    }

    [Fact]
    public void KeepsEverySlideItWasGiven()
    {
        var reading = FormationReader.Read(Directory,
            [FormationFixture.Read("slide-f442.json"), FormationFixture.Read("slide-f5212.json")]);

        Assert.Equal(FormationCaptureOutcome.Captured, reading.Outcome);
        Assert.Equal(["f442", "f5212"], reading.Layouts.Select(layout => layout.Code));
        Assert.Empty(reading.Unresolved);
    }

    [Fact]
    public void SetsASlideAsideWhenTheDirectoryDoesNotNameIt()
    {
        var reading = FormationReader.Read(Directory,
            [FormationFixture.Read("slide-f442.json"), FormationFixture.Read("slide-unlisted.json")]);

        Assert.Equal(FormationCaptureOutcome.Partial, reading.Outcome);
        Assert.Equal(["f442"], reading.Layouts.Select(layout => layout.Code));
        Assert.Equal(["99"], reading.Unresolved);
        Assert.Contains("99", reading.Detail);
    }

    [Fact]
    public void SetsASlideAsideWhenItsSlotIndexesAreNotZeroToTen()
    {
        var reading = FormationReader.Read(Directory,
            [FormationFixture.Read("slide-f5212.json"), FormationFixture.Read("slide-gapped.json")]);

        Assert.Equal(FormationCaptureOutcome.Partial, reading.Outcome);
        Assert.Equal(["f5212"], reading.Layouts.Select(layout => layout.Code));
        Assert.Equal(["16"], reading.Unresolved);
    }

    [Fact]
    public void ReportsNothingObservedWhenNoSlideWasRead()
    {
        var reading = FormationReader.Read(Directory, []);

        Assert.Equal(FormationCaptureOutcome.NotObserved, reading.Outcome);
        Assert.Empty(reading.Layouts);
    }

    [Fact]
    public void ReportsAFailureWhenTheDirectoryCannotBeRead()
    {
        var reading = FormationReader.Read("{ not json", [FormationFixture.Read("slide-f442.json")]);

        Assert.Equal(FormationCaptureOutcome.Failed, reading.Outcome);
        Assert.Empty(reading.Layouts);
    }

    [Fact]
    public void DropsARepeatedSlideSoAWrappedWalkStaysClean()
    {
        var reading = FormationReader.Read(Directory,
            [FormationFixture.Read("slide-f442.json"), FormationFixture.Read("slide-f442.json")]);

        Assert.Equal(FormationCaptureOutcome.Captured, reading.Outcome);
        Assert.Equal(["f442"], reading.Layouts.Select(layout => layout.Code));
    }
}
