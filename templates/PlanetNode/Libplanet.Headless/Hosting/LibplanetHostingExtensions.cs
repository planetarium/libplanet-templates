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

        if (build.ValidatorDriverConfiguration is { } validatorDriverConfiguration)
        {
            services.AddSingleton<ValidatorDriverConfiguration>(validatorDriverConfiguration);
        }

        if (build.ValidatorPrivateKey is { } validatorPrivateKey)
        {
            services.AddSingleton<ValidatorPrivateKey>(new ValidatorPrivateKey(validatorPrivateKey));
        }

        if (build.Swarm is { } swarm && build.BootstrapMode is { } bootstrapMode)
        {
            services.AddSingleton<Swarm<T>>(swarm);
            services.AddSingleton(typeof(SwarmService<T>.BootstrapMode), bootstrapMode);
            services.AddHostedService<SwarmService<T>>();
        }
        else if (build.ValidatorPrivateKey is not null)
        {
            services.AddHostedService<SoloValidationService<T>>();
        }

        return services;
    }
}
