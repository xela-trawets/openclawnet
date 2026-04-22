using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NCalc;
using OpenClawNet.Tools.Abstractions;

namespace OpenClawNet.Tools.Calculator;

/// <summary>
/// Safe arithmetic / boolean / built-in math expression evaluator powered by NCalc.
/// Closes the well-known gap where LLMs produce wrong arithmetic. Supports the
/// full NCalc expression grammar (operators, parentheses, Math functions like
/// Sqrt/Pow/Sin/Cos, comparisons, ternaries, if(), in(), etc.).
/// </summary>
public sealed class CalculatorTool : ITool
{
    private readonly ILogger<CalculatorTool> _logger;

    public CalculatorTool(ILogger<CalculatorTool> logger) => _logger = logger;

    public string Name => "calculator";

    public string Description =>
        "Evaluate a math or boolean expression using NCalc. Supports +, -, *, /, %, ^, parentheses, " +
        "comparisons, Math functions (Sqrt, Pow, Abs, Sin, Cos, Round, Min, Max, Log, Exp), " +
        "ternaries (a > b ? a : b), if(...), and in(x, ...). Use this whenever the question requires arithmetic.";

    public ToolMetadata Metadata => new()
    {
        Name = Name,
        Description = Description,
        ParameterSchema = JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "expression": {
                    "type": "string",
                    "description": "NCalc expression to evaluate, e.g. 'Sqrt(2) * Pow(3,4) + 5' or 'if(1 > 2, \"a\", \"b\")'"
                }
            },
            "required": ["expression"]
        }
        """),
        RequiresApproval = false,
        Category = "math",
        Tags = ["math", "calculator", "arithmetic", "expression"]
    };

    public Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var expression = input.GetStringArgument("expression");
        if (string.IsNullOrWhiteSpace(expression))
            return Task.FromResult(ToolResult.Fail(Name, "'expression' is required", sw.Elapsed));

        try
        {
            // EvaluateFunctionsAndParameters off (no callbacks) → no code execution surface.
            var expr = new Expression(expression, ExpressionOptions.NoCache)
            {
                CultureInfo = CultureInfo.InvariantCulture
            };
            var value = expr.Evaluate();
            sw.Stop();

            var rendered = value switch
            {
                null => "null",
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? "null"
            };
            return Task.FromResult(ToolResult.Ok(Name, $"{expression} = {rendered}", sw.Elapsed));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Calculator evaluation failed for expression: {Expression}", expression);
            return Task.FromResult(ToolResult.Fail(Name, $"Evaluation error: {ex.Message}", sw.Elapsed));
        }
    }
}
