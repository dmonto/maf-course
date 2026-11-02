using Unit02.Common;

return await CourseApp.RunAsync(args, context =>
{
    // Illustrative teaching rates from the GitBook example, not current provider prices.
    const decimal inputRate = 0.40m, outputRate = 1.60m;
    decimal perCall = Costs.Estimate(1500, 250, inputRate, outputRate)!.Value;
    var report = new {
        rateSource = "hypothetical teaching assumptions", currency = "USD",
        inputRatePerMillion = inputRate, outputRatePerMillion = outputRate,
        inputTokensPerCall = 1500, outputTokensPerCall = 250, perCall,
        tenThousandCalls = perCall * 10_000,
        tenThousandCasesWithThreeCallsEach = perCall * 10_000 * 3,
        unavailableUsageEstimate = Costs.Estimate(null, 250, inputRate, outputRate),
        exclusions = new[] { "cached/reasoning adjustments", "tools", "search", "storage", "evaluation", "capacity", "local hardware" }
    };
    Json.Print(report);
    context.Save("v07-cost.json", report);
    return Task.CompletedTask;
});
