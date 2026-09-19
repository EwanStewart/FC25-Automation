using Automation.Sbc;

namespace Automation.Tests;

public class FormationsTests
{
    [Theory]
    [InlineData("f3142")]
    [InlineData("f343")]
    [InlineData("f41212")]
    [InlineData("f4141")]
    [InlineData("f4222")]
    [InlineData("f424")]
    [InlineData("f433")]
    [InlineData("f442")]
    [InlineData("f451")]
    [InlineData("f5212")]
    [InlineData("f532")]
    public void KnowsEveryFormationTheImportedChallengesAskFor(string formation)
    {
        Assert.True(Formations.IsKnown(formation));
        Assert.Equal(Formations.DEFAULT_SQUAD_SIZE, Formations.SlotPositions(formation).Count);
    }

    [Fact]
    public void CarriesEveryFormationTheWebAppOffers()
    {
        Assert.Equal(29, Formations.Codes.Count);
    }

    [Fact]
    public void GivesTheCapturedSlotOrderForTheActiveSquadFormation()
    {
        Assert.Equal(["GK", "RB", "CB", "CB", "LB", "RM", "CM", "CM", "LM", "ST", "ST"],
            Formations.SlotPositions("f442"));
    }

    [Fact]
    public void GivesTheCapturedSlotOrderForAFiveAtTheBackFormation()
    {
        Assert.Equal(["GK", "RB", "CB", "CB", "CB", "LB", "CDM", "CM", "CM", "ST", "ST"],
            Formations.SlotPositions("f532"));
    }

    [Fact]
    public void AcceptsAFormationCodeWithoutItsLeadingLetter()
    {
        Assert.Equal(Formations.SlotPositions("f433"), Formations.SlotPositions("433"));
    }

    [Fact]
    public void NamesTheFormationItDoesNotKnowRatherThanGuessing()
    {
        Assert.False(Formations.IsKnown("f9999"));

        var thrown = Assert.Throws<ArgumentException>(() => Formations.SlotPositions("f9999"));

        Assert.Contains("f9999", thrown.Message);
    }

    [Fact]
    public void UsesOnlyPositionsTheChemistryCalculatorUnderstands()
    {
        string[] known = ["GK", "RB", "CB", "LB", "CDM", "CM", "CAM", "RM", "LM", "RW", "LW", "ST"];

        Assert.All(Formations.Codes,
            code => Assert.All(Formations.SlotPositions(code), position => Assert.Contains(position, known)));
    }
}
