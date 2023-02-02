namespace Libplanet.Headless;

public class MissingConfigurationFieldException : InvalidConfigurationException
{
    public MissingConfigurationFieldException(string fieldName)
        : this(fieldName, $"Missing configuration field: {fieldName}")
    {
    }

    public MissingConfigurationFieldException(string fieldName, string message)
        : base(message)
    {
        FieldName = fieldName;
    }

    public string FieldName { get; }
}
