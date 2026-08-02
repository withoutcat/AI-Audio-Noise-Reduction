$dllPath = Join-Path $PSScriptRoot "..\res\sdk\Shengwang_Native_SDK_for_Windows_FULL\sdk\x86_64\agora_rtc_sdk.dll"
if (-not (Test-Path -LiteralPath $dllPath)) {
  Write-Error "agora_rtc_sdk.dll not found: $dllPath"
  exit 1
}
$bytes = [System.IO.File]::ReadAllBytes($dllPath)
$text = [System.Text.Encoding]::ASCII.GetString($bytes)
# Find all printable strings that look like exported function names (agora/Rte/create related)
$matches = [regex]::Matches($text, '(?<![A-Za-z])(createAgora\w+|agora_\w+|Rte[A-Z]\w+)')
$unique = [System.Collections.Generic.HashSet[string]]::new()
foreach($m in $matches) { [void]$unique.Add($m.Value) }
$unique | Sort-Object
