using System.Net;
using Cocona;
using GraphQL;
using GraphQL.MicrosoftDI;
using GraphQL.Server;
using GraphQL.Server.Transports.AspNetCore;
using GraphQL.SystemTextJson;
using Libplanet.Action;
using Libplanet.Crypto;
using Libplanet.Explorer.Interfaces;
using Libplanet.Explorer.Queries;
using Libplanet.Extensions.Cocona;
using Libplanet.Headless;
using Libplanet.Headless.Hosting;
using Libplanet.KeyStore;
using PlanetNode;
using PlanetNode.Action;
using PlanetNode.GraphTypes;
using Serilog;

using PlanetExplorerSchema = Libplanet.Explorer.Schemas.LibplanetExplorerSchema<Libplanet.Action.PolymorphicAction<PlanetNode.Action.BaseAction>>;

var app = CoconaApp.Create();

app.AddCommand((
    [Argument(Description = "The private key of the miner.  If not given, the node will not " +
        "mine blocks.  You can create a new private key with the `dotnet planet key create` " +
        "command.  You can list the created keys in your machine with `dotnet planet key` " +
        "command.")]
    Guid? minerKeyId,
    PassphraseParameters passphrase
) =>
{
    PrivateKey? minerKey = null;
    if (minerKeyId is { } keyId)
    {
        IKeyStore keyStore = Web3KeyStore.DefaultKeyStore;
        ProtectedPrivateKey protectedKey = keyStore.Get(keyId);
        while (minerKey is null)
        {
            try
            {
                minerKey = protectedKey.Unprotect(
                    passphrase.Take($"Passphrase ({keyId}): ")
                );
            }
            catch (IncorrectPassphraseException)
            {
                Console.Error.WriteLine("Incorrect passphrase.  Try again.");
            }
        }
    }
    else
    {
        // FIXME: These should be printed to stderr.
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.BackgroundColor = ConsoleColor.DarkRed;
        Console.WriteLine(new string('-', Console.WindowWidth));
        Console.WriteLine("WARNING: The miner key is not given.  The node will not mine " +
            "blocks.  If it is intended, ignore this message.  If you want to mine blocks, " +
            "you first need to create a new private key with the following command:");
        Console.WriteLine();
        Console.WriteLine("\tdotnet planet key create");
        Console.WriteLine();
        Console.WriteLine("... and then pass the key ID to this command with its argument.");
        Console.Write(new string('-', Console.WindowWidth));
        Console.ResetColor();
        Console.WriteLine();
        Thread.Sleep(3000);
    }

    // Get configuration
    string configPath = Environment.GetEnvironmentVariable("PN_CONFIG_FILE") ?? "appsettings.json";

    var configurationBuilder = new ConfigurationBuilder()
        .AddJsonFile(configPath)
        .AddEnvironmentVariables("PN_");
    IConfiguration config = configurationBuilder.Build();

    var loggerConf = new LoggerConfiguration()
       .ReadFrom.Configuration(config);
    Log.Logger = loggerConf.CreateLogger();

    var headlessConfig = new Configuration();
    config.Bind(headlessConfig);

    var builder = WebApplication.CreateBuilder(args);
    builder.Services
        .AddLibplanet<PolymorphicAction<BaseAction>>(builder =>
        {
            builder
                .UseConfiguration(headlessConfig)
                .UseBlockPolicy(BlockPolicySource.GetPolicy());

            if (minerKey is { } key)
            {
                builder.UseMiner(key);
            }
        })
        .AddGraphQL(builder =>
        {
            builder
                .AddSchema<Schema>()
                .AddSchema<PlanetExplorerSchema>()
                .AddGraphTypes(typeof(ExplorerQuery<PolymorphicAction<BaseAction>>).Assembly)
                .AddGraphTypes(typeof(Query).Assembly)
                .AddUserContextBuilder<ExplorerContextBuilder>()
                .AddSystemTextJson();
        })
        .AddCors()
        .AddSingleton<Schema>()
        .AddSingleton<Query>()
        .AddSingleton<Mutation>()
        .AddSingleton<GraphQLHttpMiddleware<Schema>>()
        .AddSingleton<GraphQLHttpMiddleware<PlanetExplorerSchema>>()
        .AddSingleton<IBlockChainContext<PolymorphicAction<BaseAction>>, ExplorerContext>();

    if (headlessConfig.GraphQLUri is { } graphqlUri)
    {
        builder.WebHost
            .ConfigureKestrel(options =>
            {
                options.Listen(IPAddress.Parse(graphqlUri.DnsSafeHost), graphqlUri.Port);
            });
    }

    using WebApplication app = builder.Build();
    app.UseCors(builder =>
    {
        builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
    app.UseRouting();

    if (headlessConfig.GraphQLUri is { LocalPath: { } localPath })
    {
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGraphQLPlayground();
        });
        app.UseGraphQL<Schema>(localPath);
        app.UseGraphQL<PlanetExplorerSchema>($"{localPath.TrimEnd('/')}/explorer");
    }

    app.Run();
});

app.Run();
