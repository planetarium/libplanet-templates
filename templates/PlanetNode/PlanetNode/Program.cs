using Cocona;
using GraphQL;
using GraphQL.MicrosoftDI;
using GraphQL.Server;
using GraphQL.Server.Transports.AspNetCore;
using GraphQL.SystemTextJson;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Explorer.Interfaces;
using Libplanet.Explorer.Queries;
using Libplanet.Extensions.Cocona.Commands;
using Libplanet.Headless;
using Libplanet.Headless.Hosting;
using PlanetNode;
using PlanetNode.Action;
using PlanetNode.GraphTypes;
using Serilog;
using System.Collections.Immutable;
using System.Net;

using PlanetExplorerSchema = Libplanet.Explorer.Schemas.LibplanetExplorerSchema<Libplanet.Action.PolymorphicAction<PlanetNode.Action.BaseAction>>;

var app = CoconaApp.Create();

app.AddCommand(() =>
{
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
        .AddLibplanet<PolymorphicAction<BaseAction>>(
            configuration: headlessConfig,
            blockPolicy: BlockPolicySource.GetPolicy(),
            differentApvEncountered: null
        )
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
    app.UseEndpoints(endpoints =>
    {
        endpoints.MapGraphQLPlayground();
    });
    app.UseGraphQL<Schema>();
    app.UseGraphQL<PlanetExplorerSchema>("/graphql/explorer");

    app.Run();
});

app.AddSubCommand("key", x =>
{
    x.AddCommands<KeyCommand>();
});

app.Run();
