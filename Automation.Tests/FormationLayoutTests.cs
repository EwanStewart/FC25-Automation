using Automation.Sbc;

namespace Automation.Tests;

public class FormationLayoutTests
{
    [Fact]
    public void ParsesEveryCapturedFormation()
    {
        var layouts = FormationLayouts.Parse(FormationFixture.Read("formations.json"));

        Assert.Equal(2, layouts.Count);
        Assert.Equal("f442", layouts[0].Code);
        Assert.Equal(16, layouts[0].FormationId);
        Assert.Equal("4-4-2", layouts[0].DisplayName);
        Assert.Equal(["GK", "RB", "CB", "CB", "LB", "RM", "CM", "CM", "LM", "ST", "ST"], layouts[0].Slots);
        Assert.Equal(["GK", "RB", "CB", "CB", "CB", "LB", "CM", "CM", "CAM", "ST", "ST"], layouts[1].Slots);
    }

    [Fact]
    public void RejectsMalformedJson()
    {
        var thrown = Assert.Throws<FormationLayoutException>(() =>
            FormationLayouts.Parse(FormationFixture.Read("malformed.json")));

        Assert.Contains("could not be read", thrown.Message);
    }

    [Fact]
    public void RejectsAFormationWithoutACode()
    {
        var thrown = Assert.Throws<FormationLayoutException>(() =>
            FormationLayouts.Parse(FormationFixture.Read("missing-code.json")));

        Assert.Contains("no code", thrown.Message);
    }

    [Fact]
    public void RejectsAFormationWithTheWrongSlotCount()
    {
        var thrown = Assert.Throws<FormationLayoutException>(() =>
            FormationLayouts.Parse(FormationFixture.Read("short-slots.json")));

        Assert.Contains("f442", thrown.Message);
        Assert.Contains("10", thrown.Message);
    }

    [Fact]
    public void RejectsARepeatedCodeWhateverTheCasing()
    {
        var thrown = Assert.Throws<FormationLayoutException>(() =>
            FormationLayouts.Parse(FormationFixture.Read("duplicate-code.json")));

        Assert.Contains("twice", thrown.Message);
    }

    [Fact]
    public void WritesLayoutsBackInAFormItCanReadAgain()
    {
        var layouts = FormationLayouts.Parse(FormationFixture.Read("formations.json"));
        var round = FormationLayouts.Parse(FormationLayouts.ToJson(layouts, "2026-09-19", "fixture"));

        Assert.Equal(layouts.Select(layout => layout.Code), round.Select(layout => layout.Code));
        Assert.Equal(layouts.Select(layout => layout.FormationId), round.Select(layout => layout.FormationId));
        Assert.Equal(layouts.Select(layout => layout.DisplayName), round.Select(layout => layout.DisplayName));
        Assert.Equal(layouts.Select(layout => string.Join(" ", layout.Slots)),
            round.Select(layout => string.Join(" ", layout.Slots)));
    }
}
