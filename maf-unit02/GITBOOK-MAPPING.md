# Mapping to Unit 2

Source: https://diego-montoliu.gitbook.io/maf-course/session-1/unit-2

Source repository blob at packaging: `106810494e3f98aab50cd03382e81f93ccea2e20` (`CursoMAF/session-1/unit-2.md`).

| Unit section | Blue example | Executable project |
| --- | --- | --- |
| Azure AI Foundry architecture | Identify the execution boundary | v01-foundry-architecture/BoundaryDemo.csproj |
| Integration with Azure OpenAI | Deployment changes without rewriting the task | v02-azure-openai/AzureDeployments.csproj |
| Hosted-model section (legacy GitHub Models heading) | Compare ticket classifiers in Foundry | v03-foundry-models/FoundryClassifiers.csproj |
| Connection to local models | Prepare the assigned local model | v04-foundry-local/LocalModel.csproj and Scripts/Prepare-FoundryLocal.ps1 |
| Endpoint management | Complete .NET connection probe | v05-endpoint-management/ProviderProbe.csproj |
| Endpoint management | Optional Python MAF reference | v05-endpoint-management/Python/provider_probe.py |
| Model versioning | Validate a candidate before switching | v06-model-versioning/ModelRegression.csproj |
| Consumption cost control | Call cost vs case cost | v07-consumption-cost/CostCalculator.csproj |
| Model observability | Explain a slow response | v08-model-observability/ObservabilityDemo.csproj |

The conceptual examples now have executable demonstrations. Shared connection code and configuration are factored into the root Shared directory rather than repeated in every Program.cs. The probe retains the same synthetic payroll task, endpoint distinctions, timeout/output limit, null-usage behavior and operational fields. It adds a distinct Foundry Models profile for the classifier comparison. The package's elapsedMs boundary is the SDK request; the inline page probe may time additional startup work.
