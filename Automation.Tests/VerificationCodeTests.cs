using Automation.Flow;

namespace Automation.Tests;

public class VerificationCodeTests
{
    [Theory]
    [InlineData("Your EA Security Code is: 389839", "389839")]
    [InlineData("Fw: Your EA Security Code is: 389839", "389839")]
    [InlineData("FW: your ea security code is: 004211", "004211")]
    public void CodeComesFromAnEaSecurityCodeSubject(string subject, string expected)
    {
        Assert.Equal(expected, VerificationCode.FromSubject(subject));
    }

    [Theory]
    [InlineData("Your EA Security Code is: 38983")]
    [InlineData("Your EA Security Code is: 3898391")]
    [InlineData("Your order 389839 has shipped")]
    [InlineData("Your EA Security Code")]
    [InlineData("")]
    public void SubjectsWithoutASixDigitSecurityCodeYieldNothing(string subject)
    {
        Assert.Equal(string.Empty, VerificationCode.FromSubject(subject));
    }
}
