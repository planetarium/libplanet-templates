Libplanet Templates
===================

TODO: Needs to be documented.

Installing the templates:

~~~~ console
# Currently this template project is not available on NuGet...
git clone https://github.com/planetarium/libplanet-templates.git
dotnet new --install ./libplanet-templates/
~~~~

Creating a new project from a template:

~~~~ console
dotnet new planet-node --name MyNode
cd MyNode
dotnet build
~~~~