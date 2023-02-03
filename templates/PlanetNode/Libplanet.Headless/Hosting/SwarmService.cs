using Libplanet.Action;
using Libplanet.Net;
using Microsoft.Extensions.Hosting;

namespace Libplanet.Headless.Hosting;

public class SwarmService<T> : BackgroundService, IDisposable
    where T : IAction, new()
{
    public enum Mode
    {
        StandaloneNode,
        Node,
    }

    private readonly Swarm<T> _swarm;
    private readonly SwarmService<T>.Mode _mode;

    public SwarmService(Swarm<T> swarm, Mode mode)
    {
        _swarm = swarm;
        _mode = mode;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_mode != Mode.StandaloneNode)
        {
            await _swarm.BootstrapAsync(cancellationToken: stoppingToken)
                .ConfigureAwait(false);
        }

        await _swarm.PreloadAsync(cancellationToken: stoppingToken).ConfigureAwait(false);
        await _swarm.StartAsync(cancellationToken: stoppingToken).ConfigureAwait(false);
    }
}
