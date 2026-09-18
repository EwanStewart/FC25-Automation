namespace Automation.Flow;

public enum LoginStep
{
    Done,
    Continue,
    EnterPassword,
    EnterEmail,
    ClickLogin,
    Unsupported,
    Wait
}

public readonly record struct LoginScreen(
    bool HasNavigation,
    bool HasContinue,
    bool HasPassword,
    bool HasEmail,
    bool HasLoginButton,
    bool IsUnsupported);

public static class LoginFlow
{
    public static LoginStep NextStep(LoginScreen screen)
    {
        var result = LoginStep.Wait;

        if (screen.HasNavigation) result = LoginStep.Done;
        else if (screen.IsUnsupported) result = LoginStep.Unsupported;
        else if (screen.HasContinue) result = LoginStep.Continue;
        else if (screen.HasPassword) result = LoginStep.EnterPassword;
        else if (screen.HasEmail) result = LoginStep.EnterEmail;
        else if (screen.HasLoginButton) result = LoginStep.ClickLogin;

        return result;
    }
}

public static class BidRow
{
    private const string HIGHEST_BID_CLASS = "highest-bid";
    private const string OUTBID_CLASS = "outbid";

    public static bool IsRegistered(string classes)
    {
        var tokens = Tokens(classes);

        return tokens.Contains(HIGHEST_BID_CLASS) || tokens.Contains(OUTBID_CLASS);
    }

    public static bool IsOurs(string classes)
    {
        return Tokens(classes).Contains(HIGHEST_BID_CLASS);
    }

    private static HashSet<string> Tokens(string classes)
    {
        return classes.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
    }
}
