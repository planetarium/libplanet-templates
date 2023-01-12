using GraphQL.Types;

namespace PlanetNode.GraphTypes;

public class PlanetNodeQuery : ObjectGraphType
{
    public PlanetNodeQuery()
    {
        Name = "PlanetNodeQuery";

        Field<ApplicationQuery>(
            "application",
            resolve: context => new { }
        );
    }
}
