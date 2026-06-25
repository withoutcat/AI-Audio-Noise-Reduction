// NoiseReduction.InstallerHelper
// CLI tool stub — device rename removed due to COM interop instability
// (E_NOINTERFACE with IMMDevice on .NET 10.0). VB-CABLE detection is now
// handled by Inno Setup [Code] via registry scan.
//
// This tool is kept as a placeholder for potential future installer tasks.

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: detect | rename <suffix> <configDir>");
    return 1;
}

Console.WriteLine(@"{""message"":""InstallerHelper: this tool is a placeholder. All operations are no-ops.""}");
Console.Error.WriteLine("[InstallerHelper] Operation not available — device rename has been removed.");
return 0;
