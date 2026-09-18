using Automation.Trading;

namespace Automation.Tests;

public class XPathTests
{
    [Fact]
    public void LiteralQuotesPlainTextWithApostrophes()
    {
        Assert.Equal("'Silver'", XPath.Literal("Silver"));
    }

    [Fact]
    public void LiteralSwitchesToDoubleQuotesWhenTheTextHoldsAnApostrophe()
    {
        Assert.Equal("\"Ligue 1 McDonald's (FRA 1)\"", XPath.Literal("Ligue 1 McDonald's (FRA 1)"));
    }

    [Fact]
    public void LiteralConcatenatesWhenTheTextHoldsBothQuoteKinds()
    {
        Assert.Equal("concat('a', \"'\", 'b\"c')", XPath.Literal("a'b\"c"));
    }
}
