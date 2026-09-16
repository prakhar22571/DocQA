using Microsoft.SemanticKernel;

namespace DocQA;

public class FunctionInvocationLoggingFilter : IFunctionInvocationFilter
{
    private const int MaxResultLength = 200;

    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        var arguments = string.Join(", ", context.Arguments.Select(a => $"{a.Key}={a.Value}"));
        Console.WriteLine($"[FunctionInvocation] Invoking {context.Function.PluginName}.{context.Function.Name}({arguments})");

        await next(context);

        var result = context.Result.ToString() ?? string.Empty;
        if (result.Length > MaxResultLength)
        {
            result = result[..MaxResultLength] + "...";
        }

        Console.WriteLine($"[FunctionInvocation] Completed {context.Function.PluginName}.{context.Function.Name} -> {result}");
    }
}
