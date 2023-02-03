namespace Libplanet.Headless.Hosting;

using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Net;
using Libplanet.Store;
using Microsoft.Extensions.DependencyInjection;

public static class LibplanetServicesExtensions
{
    public static IServiceCollection AddLibplanet<T>(
        this IServiceCollection services,
        Action<ILibplanetBuilder<T>> configure
    )
        where T : IAction, new()
    {
        var builder = new LibplanetBuilder<T>();
        configure(builder);
        InstantiatedNodeComponents<T> build = builder.Build();

        services.AddSingleton<IBlockPolicy<T>>(build.BlockChain.Policy);
        services.AddSingleton<IStagePolicy<T>>(build.BlockChain.StagePolicy);
        services.AddSingleton<IStore>(build.Store);
        services.AddSingleton<IStateStore>(build.StateStore);
        services.AddSingleton<BlockChain<T>>(build.BlockChain);
        services.AddSingleton<Swarm<T>>(build.Swarm);
        services.AddSingleton(typeof(SwarmService<T>.Mode), build.SwarmMode);
        services.AddHostedService<SwarmService<T>>();

        if (build.MinerPrivateKey is { } minerPrivateKey)
        {
            services.AddHostedService(provider =>
                new MinerService<T>(
                    provider.GetRequiredService<BlockChain<T>>(),
                    minerPrivateKey
                )
            );
        }

        return services;
    }
}
