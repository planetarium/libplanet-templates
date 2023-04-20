namespace Libplanet.Headless.Hosting;

using System;

public class ValidatorDriverConfiguration
{
    public TimeSpan MinimumBlockInterval { get; init; } = TimeSpan.FromSeconds(10.0);

    public double MinimumBlockIntervalSecs {
        get => (double)MinimumBlockInterval.TotalSeconds;
        init => MinimumBlockInterval = TimeSpan.FromSeconds(value);
    }
}
