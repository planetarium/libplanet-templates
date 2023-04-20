Your Libplanet Node
===================

This is a blockchain node application project created using
[libplanet-templates] [.NET template].
It uses [Libplanet] at its code.

[libplanet-templates]: https://github.com/planetarium/libplanet-templates
[.NET template]: https://github.com/dotnet/templating/
[Libplanet]: https://libplanet.io/

Prerequisites
-------------

You need to install [.NET SDK] 6+. Follow the instruction to install
the .NET SDK on the [.NET download page][1].

[.NET SDK]: https://docs.microsoft.com/dotnet/core/sdk
[1]: https://dotnet.microsoft.com/download


Build
-----

```bash
$ dotnet build
```

If you want to build a docker image, You can create a standalone image
with the command below.
```bash
$ docker build . -t <IMAGE_TAG>
```

How to Run
----------

```bash
$ dotnet run --project PlanetNode
```

### About configuration
By default, this project produces and uses storage and settings via
`appsettings.json` and `PN_` prefixed environment variables. If you want to
change settings, please edit that files or set environment variables.

In sh/bash/zsh (Linux or macOS):

```sh
$ PN_StorePath="/tmp/planet-node" dotnet run --project PlanetNode
```

Or PowerShell (Windows):

```pwsh
PS > $Env:PN_StorePath="/tmp/planet-node"; dotnet run --project PlanetNode
```

### GraphQL
This project embeds a [GraphQL] server and [GraphQL Playground] by default,
backed by [GraphQL.NET]. You can check the current chain status on the
playground. (The playground is at <http://localhost:38080/ui/playground> by
default.)

To access the Libplanet explorer GraphQL queries, you would have to change the
endpoint to <http://localhost:38080/graphql/explorer>.

The following GraphQL query returns the last 10 blocks and transactions.

```graphql
query
{
  blockQuery
  {
    blocks (limit: 10 desc: true)
    {
      index
      hash
      timestamp

      transactions
      {
        id
        actions
        {
          inspection
        }
      }
    }
  }
}
```

Also, you can find a list of supported GraphQL query in the playground on the
sidebar.

See the [Libplanet.Explorer] project for more details.
Also, if you want to try scenario based tutorial, please check the
`TUTORIAL.md` file.

Publish
-------

If you want to pack this project, use [`dotnet publish`][dotnet publish] as below.

```bash
$ dotnet publish -c Release --self-contained -r linux-x64
$ ls -al PlanetNode/bin/Release/net6.0/linux-x64/publish/
```

[dotnet publish]: https://docs.microsoft.com/en-US/dotnet/core/tools/dotnet-publish

[GraphQL]: https://graphql.org/
[GraphQL Playground]: https://github.com/graphql/graphql-playground
[GraphQL.NET]: https://graphql-dotnet.github.io/
[Libplanet.Explorer]: https://github.com/planetarium/libplanet/tree/main/Libplanet.Explorer
