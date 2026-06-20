$dllPath = "c:\Users\sunzi\OneDrive\projects\AI-audio-noise-reduction\src\NoiseReduction.SdkPoC\bin\Debug\net10.0-windows\agora_rtc_sdk.dll"
$bytes = [System.IO.File]::ReadAllBytes($dllPath)
$text = [System.Text.Encoding]::ASCII.GetString($bytes)
# Find all printable strings that look like exported function names (agora/Rte/create related)
$matches = [regex]::Matches($text, '(?<![A-Za-z])(createAgora\w+|agora_\w+|Rte[A-Z]\w+)')
$unique = [System.Collections.Generic.HashSet[string]]::new()
foreach($m in $matches) { [void]$unique.Add($m.Value) }
$unique | Sort-Object
