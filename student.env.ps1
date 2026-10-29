# Copy to student.env.ps1, replace placeholders, then dot-source the copy.
# Use the values assigned to you. Do not create resources or grant yourself roles.

# Azure context
$env:TENANT_ID = "7d384181-9169-4237-9c90-1cb3bfb04d94"
$env:SUBSCRIPTION_ID = "2bcbc61d-2a42-4622-800c-5fd9b3a2e909"
$env:RESOURCE_GROUP = "training-maf-nov2026"

# Foundry project: copy the project endpoint, not the resource endpoint.
$env:AZURE_AI_PROJECT_ENDPOINT = "https://proj-maf-nov2026.services.ai.azure.com/api/projects/proj-maf-nov2026"
$env:AZURE_AI_PROJECT_API_VERSION = "v1"

# OpenAI v1: no dated AZURE_OPENAI_API_VERSION or separate -responses endpoint.
$env:AZURE_OPENAI_ENDPOINT = "https://azoai-maf-nov2026.openai.azure.com/"
$env:AZURE_OPENAI_DEPLOYMENT_1 = "chat-fast"
# Leave empty to use the chat deployment; set a separate deployment if assigned.
$env:AZURE_OPENAI_RESPONSES_DEPLOYMENT = "chat-quality"
$env:AZURE_OPENAI_EMBEDDINGS_DEPLOYMENT = "embedding-lab"

# Precreated Search index and Blob container assigned by the coordinator.
$env:AZURE_AI_SEARCH_ENDPOINT = "https://srch-maf-nov2026.search.windows.net"
$env:AZURE_AI_SEARCH_INDEX_NAME = "maf-support-vector-index"
$env:AZURE_STORAGE_ACCOUNT = "stmafnov2026"
$env:AZURE_STORAGE_CONTAINER = "curso-maf-shared"
$env:AZURE_STORAGE_BLOB_ENDPOINT="https://stmafnov2026.blob.core.windows.net"

# Optional write checks: 1 creates and removes synthetic temporary resources.
# Ask the coordinator whether your assigned lab roles allow these checks.
$env:SMOKE_WRITE_TESTS = "0"

# Local-model lab only. Leave both empty to skip; use the assigned model name.
$env:OLLAMA_HOST = ""
$env:OLLAMA_MODEL = ""
# Example: $env:OLLAMA_HOST = "http://localhost:11434"
# Then:    ollama pull $env:OLLAMA_MODEL
