namespace Libplanet.Headless;

public class ConflictingConfigurationException : InvalidConfigurationException
{
    public ConflictingConfigurationException(
        string message, string fieldName, string anotherFieldName)
        : base(message)
    {
        FieldName = fieldName;
        AnotherFieldName = anotherFieldName;
    }

    public string FieldName { get; }

    public string AnotherFieldName { get; }
}
