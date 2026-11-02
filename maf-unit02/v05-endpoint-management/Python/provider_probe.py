import asyncio
import json
import os
import sys
import time
from ipaddress import ip_address
from urllib.parse import urlsplit

from agent_framework import Agent
from agent_framework.openai import OpenAIChatCompletionClient
from azure.identity import AzureCliCredential, get_bearer_token_provider
from openai import AsyncOpenAI
from pathlib import Path


def load_root_env() -> None:
    root = next(p for p in Path(__file__).resolve().parents if (p / "Directory.Build.props").is_file())
    for raw in (root / ".env").read_text(encoding="utf-8").splitlines():
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        name, value = line.split("=", 1)
        name, value = name.strip(), value.strip()
        if len(value) >= 2 and value[0] == value[-1] and value[0] in {chr(34), chr(39)}:
            value = value[1:-1]
        if not os.getenv(name):
            os.environ[name] = value



def required(name: str) -> str:
    value = os.getenv(name, "").strip()
    if not value or "<" in value or ">" in value:
        raise ValueError(f"Set {name} to its assigned value.")
    return value


def endpoint(name: str, *, local: bool = False) -> str:
    value = required(name).rstrip("/") + "/"
    parts = urlsplit(value)
    if (parts.scheme not in {"http", "https"} or not parts.hostname
            or parts.username is not None or parts.password is not None
            or parts.query or parts.fragment):
        raise ValueError(f"Set {name} to a valid HTTP(S) endpoint.")
    if local:
        try:
            loopback = ip_address(parts.hostname).is_loopback
        except ValueError:
            loopback = parts.hostname.lower() == "localhost"
        if not loopback or parts.path != "/v1/":
            raise ValueError(f"{name} must use a loopback host and the /v1/ path.")
    elif parts.scheme != "https" or parts.path != "/":
        raise ValueError(f"{name} must be the HTTPS Azure resource root.")
    return value


async def main() -> None:
    load_root_env()
    provider = os.getenv("MAF_MODEL_PROVIDER", "foundry-local").strip().lower()
    question = " ".join(sys.argv[1:]) or (
        "Synthetic incident: payroll batch processing is delayed. "
        "No recovery time is available. Summarize it for support."
    )
    if len(question) > 2000:
        raise ValueError("Input must contain at most 2,000 characters.")

    credential = None
    try:
        if provider == "azure":
            model = required("AZURE_OPENAI_DEPLOYMENT_NAME")
            base_url = endpoint("AZURE_OPENAI_ENDPOINT") + "openai/v1/"
            credential = AzureCliCredential()
            token_provider = get_bearer_token_provider(
                credential, "https://cognitiveservices.azure.com/.default"
            )

            async def bearer() -> str:
                return await asyncio.to_thread(token_provider)

            api_key = bearer
        elif provider == "foundry-local":
            model = os.getenv("FOUNDRY_LOCAL_API_MODEL", "").strip() or required("FOUNDRY_LOCAL_MODEL")
            base_url = endpoint("FOUNDRY_LOCAL_BASE_URL", local=True)
            api_key = "notneeded"
        else:
            raise ValueError("MAF_MODEL_PROVIDER must be azure or foundry-local.")

        # Passing a configured SDK client keeps routing explicit when Azure
        # and OpenAI environment variables are present in the same terminal.
        async with AsyncOpenAI(base_url=base_url, api_key=api_key, max_retries=0) as sdk:
            client = OpenAIChatCompletionClient(model=model, async_client=sdk)
            agent = Agent(
                client=client,
                name="ProviderProbe",
                instructions=(
                    "Summarize the supplied synthetic incident in three short lines: "
                    "Service, Impact, Next step. Use only supplied facts. "
                    "Do not invent recovery times."
                ),
            )
            started = time.perf_counter()
            result = await asyncio.wait_for(
                agent.run(question, options={"max_tokens": 512}), timeout=45
            )
            if not result.text.strip():
                raise ValueError("No text returned; check the model and output budget.")
            print(result.text)
            print(json.dumps({
                "provider": provider,
                "requestedModel": model,
                "status": "ok",
                "elapsedMs": round((time.perf_counter() - started) * 1000),
                "usage": result.usage_details,
            }))
    finally:
        if credential is not None:
            credential.close()


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except Exception as exc:
        print(json.dumps({"status": "error", "errorType": type(exc).__name__}), file=sys.stderr)
        sys.exit(1)
