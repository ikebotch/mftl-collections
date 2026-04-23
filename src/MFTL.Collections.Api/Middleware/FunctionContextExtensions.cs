using Microsoft.Azure.Functions.Worker;

namespace MFTL.Collections.Api.Middleware;

public static class FunctionContextExtensions
{
    public static bool IsHttpTrigger(this FunctionContext context)
    {
        return context.FunctionDefinition.InputBindings.Values
            .Any(b => b.Type.Equals("httpTrigger", StringComparison.OrdinalIgnoreCase));
    }
}
