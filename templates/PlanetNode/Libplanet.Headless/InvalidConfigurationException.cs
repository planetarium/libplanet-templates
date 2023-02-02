namespace Libplanet.Headless;

using System;

public abstract class InvalidConfigurationException : Exception
{
    public InvalidConfigurationException(string message)
        : base(message)
    {
    }

    public InvalidConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
