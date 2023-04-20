using System.Collections.Immutable;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Crypto;
using Libplanet.Net;
using Libplanet.Net.Consensus;
using Libplanet.Net.Transports;
using Libplanet.Store;
using Microsoft.Extensions.Hosting;

namespace Libplanet.Headless.Hosting;

public class SwarmService<T> : BackgroundService, IDisposable
    where T : IAction, new()
{
    public enum BootstrapMode
    {
        Seed,
        Participant,
    }

    private readonly Swarm<T> _swarm;
    private readonly SwarmService<T>.BootstrapMode _mode;

    public SwarmService(Swarm<T> swarm, SwarmService<T>.BootstrapMode mode)
    {
        _swarm = swarm;
        _mode = mode;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_mode != BootstrapMode.Seed)
        {
            await _swarm.BootstrapAsync(cancellationToken: stoppingToken)
                .ConfigureAwait(false);
        }

        await _swarm.PreloadAsync(cancellationToken: stoppingToken).ConfigureAwait(false);
        await _swarm.StartAsync(cancellationToken: stoppingToken).ConfigureAwait(false);
    }
}
