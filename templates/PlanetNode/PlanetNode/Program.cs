using System.Net;
using Cocona;
using GraphQL;
using GraphQL.MicrosoftDI;
using GraphQL.Server;
using GraphQL.Server.Transports.AspNetCore;
using GraphQL.SystemTextJson;
using Libplanet.Action;
using Libplanet.Explorer.Interfaces;
using Libplanet.Explorer.Queries;
using Libplanet.Extensions.Cocona.Commands;
using Libplanet.Headless;
using Libplanet.Headless.Hosting;
using PlanetNode;
using PlanetNode.Action;
using PlanetNode.GraphTypes;
using Serilog;

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
        .AddLibplanet<PolymorphicAction<BaseAction>>(builder =>
        {
            builder
                .UseConfiguration(headlessConfig)
                .UseBlockPolicy(BlockPolicySource.GetPolicy());
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

app.AddSubCommand("key", x =>
{
    x.AddCommands<KeyCommand>();
});

app.Run();
