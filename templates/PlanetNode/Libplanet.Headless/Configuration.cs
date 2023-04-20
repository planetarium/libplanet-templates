namespace Libplanet.Headless;

using Libplanet.Headless.Hosting;

public class Configuration
{
    public Uri? GenesisBlockPath { get; init; }

    public Uri? StoreUri { get; init; }

    public Uri? GraphQLUri { get; init; }

    public NetworkConfiguration? Network { get; init; }

    public TimeSpan TxLifetime { get; init; } = TimeSpan.FromMinutes(180);

    public int TxLifetimeMins {
        get => (int)TxLifetime.TotalMinutes;
        init => TxLifetime = TimeSpan.FromMinutes(value);
    }

    public ValidatorDriverConfiguration ValidatorDriver { get; init; } =
        new ValidatorDriverConfiguration();
}
