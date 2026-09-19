namespace Automation.Catalogue;

public abstract class CatalogueException : Exception
{
    protected CatalogueException(string message) : base(message)
    {
    }

    protected CatalogueException(string message, Exception inner) : base(message, inner)
    {
    }
}

public sealed class CatalogueFormatException : CatalogueException
{
    public CatalogueFormatException(string message) : base(message)
    {
    }

    public CatalogueFormatException(string message, Exception inner) : base(message, inner)
    {
    }
}

public sealed class CatalogueConfigurationException : CatalogueException
{
    public CatalogueConfigurationException(string message) : base(message)
    {
    }
}

public sealed class CatalogueRequestException : CatalogueException
{
    public CatalogueRequestException(string message) : base(message)
    {
    }
}
