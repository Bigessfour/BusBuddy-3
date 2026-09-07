using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace BusBuddy.WPF.Utilities
{
    /// <summary>
    /// Utility class for diagnosing resource loading issues
    /// </summary>
    public static class ResourceDiagnosticUtility
    {
        /// <summary>
        /// Logs all embedded resources found in the specified assembly
        /// </summary>
        /// <param name="assemblyName">The name of the assembly to inspect</param>
        public static void LogAllEmbeddedResources(string assemblyName)
        {
            try
            {
                // Try to load the assembly
                Assembly? assembly = null;
                try
                {
                    assembly = Assembly.Load(assemblyName);
                }
                catch
                {
                    // If we can't load by name, try to find it among loaded assemblies
                    foreach (var loadedAssembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (loadedAssembly.GetName().Name == assemblyName)
                        {
                            assembly = loadedAssembly;
                            break;
                        }
                    }
                }

                if (assembly == null)
                {
                    Log.Warning("[RESOURCE_DIAGNOSTIC] Could not load assembly: {AssemblyName}", assemblyName);
                    return;
                }

                // Get all embedded resources
                string[] resources = assembly.GetManifestResourceNames();
                Log.Information("[RESOURCE_DIAGNOSTIC] Found {ResourceCount} resources in {AssemblyName}", resources.Length, assemblyName);

                StringBuilder sb = new StringBuilder();
                foreach (string resource in resources)
                {
                    sb.AppendLine($"  • {resource}");
                }

                Log.Information("[RESOURCE_DIAGNOSTIC] Resources in {AssemblyName}:\n{ResourceList}", assemblyName, sb.ToString());
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[RESOURCE_DIAGNOSTIC] Error inspecting resources in {AssemblyName}", assemblyName);
            }
        }

        /// <summary>
        /// Attempts to load a resource from an assembly and save it to a file for inspection
        /// </summary>
        /// <param name="assemblyName">The name of the assembly containing the resource</param>
        /// <param name="resourceName">The name of the resource to extract</param>
        /// <param name="outputPath">The path where the resource should be saved</param>
        /// <returns>True if successful, false otherwise</returns>
        public static bool ExtractResourceToFile(string assemblyName, string resourceName, string outputPath)
        {
            try
            {
                Assembly? assembly = Assembly.Load(assemblyName);
                if (assembly == null)
                {
                    Log.Warning("[RESOURCE_DIAGNOSTIC] Could not load assembly: {AssemblyName}", assemblyName);
                    return false;
                }

                using (Stream? resourceStream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (resourceStream == null)
                    {
                        Log.Warning("[RESOURCE_DIAGNOSTIC] Resource not found: {ResourceName}", resourceName);
                        return false;
                    }

                    using (FileStream fileStream = new FileStream(outputPath, FileMode.Create))
                    {
                        resourceStream.CopyTo(fileStream);
                    }
                }

                Log.Information("[RESOURCE_DIAGNOSTIC] Resource extracted to: {OutputPath}", outputPath);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[RESOURCE_DIAGNOSTIC] Error extracting resource {ResourceName} from {AssemblyName}", resourceName, assemblyName);
                return false;
            }
        }

        /// <summary>
        /// Logs information about all loaded assemblies in the current AppDomain
        /// </summary>
        public static void LogLoadedAssemblies()
        {
            try
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                Log.Information("[RESOURCE_DIAGNOSTIC] Found {AssemblyCount} loaded assemblies:", assemblies.Length);

                var syncfusionAssemblies = new List<Assembly>();
                StringBuilder sb = new StringBuilder();

                foreach (Assembly assembly in assemblies)
                {
                    string? name = assembly.GetName().Name;
                    string version = assembly.GetName().Version?.ToString() ?? "Unknown";
                    string location = string.IsNullOrEmpty(assembly.Location) ? "[Dynamic Assembly]" : assembly.Location;

                    if (name != null && name.StartsWith("Syncfusion.", StringComparison.Ordinal))
                    {
                        syncfusionAssemblies.Add(assembly);
                        sb.AppendLine($"  • {name} ({version}) - {location}");
                    }
                }

                Log.Information("[RESOURCE_DIAGNOSTIC] Syncfusion assemblies ({SyncfusionCount}):\n{SyncfusionList}", syncfusionAssemblies.Count, sb.ToString());
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[RESOURCE_DIAGNOSTIC] Error inspecting loaded assemblies");
            }
        }
    }
}
