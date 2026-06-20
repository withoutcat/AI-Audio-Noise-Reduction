$ErrorActionPreference = "Stop"

$path = "C:\Users\sunzi\.codex\config.toml"
$text = Get-Content -Raw -LiteralPath $path

$withoutOld = [regex]::Replace(
    $text,
    '(?ms)^model_provider\s*=.*?^\[model_providers\.custom\]\r?\n.*?(?=^\[|\z)',
    ''
)

$withoutOld = [regex]::Replace(
    $withoutOld,
    '(?m)^model\s*=\s*"qwen[^"]*"\r?\n|^disable_response_storage\s*=.*\r?\n|^model_reasoning_effort\s*=.*\r?\n|^approval_policy\s*=\s*"never"\r?\n|^approvals_reviewer\s*=\s*"user"\r?\n|^sandbox_mode\s*=\s*"danger-full-access"\r?\n',
    ''
)

$block = @'
# Default model/provider for Codex.
# DashScope exposes Qwen through an OpenAI-compatible Chat Completions API.
model_provider = "dashscope"
model = "qwen-plus"

[model_providers.dashscope]
name = "DashScope (Qwen)"
base_url = "https://dashscope.aliyuncs.com/compatible-mode/v1"
env_key = "DASHSCOPE_API_KEY"
wire_api = "chat"
requires_openai_auth = false

'@

Set-Content -LiteralPath $path -Value ($block + $withoutOld.TrimStart()) -Encoding UTF8

$cfg = Get-Content -Raw -LiteralPath $path
$checks = [ordered]@{
    ConfigHasGlobalDashscope = [regex]::IsMatch($cfg, '(?m)^model_provider\s*=\s*"dashscope"')
    ConfigHasQwenPlusModel = [regex]::IsMatch($cfg, '(?m)^model\s*=\s*"qwen-plus"')
    ConfigHasDashscopeProvider = [regex]::IsMatch($cfg, '(?m)^\[model_providers\.dashscope\]')
    ConfigHasNoInlineBearerToken = -not [regex]::IsMatch($cfg, '(?m)^experimental_bearer_token\s*=')
    UserEnvKeyPresent = [bool][Environment]::GetEnvironmentVariable("DASHSCOPE_API_KEY", "User")
}

[pscustomobject]$checks | Format-List
