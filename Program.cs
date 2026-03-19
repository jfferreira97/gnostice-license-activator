using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;

class Program
{
    static void Main()
    {
        string licenseKey = ConfigurationManager.AppSettings["GnosticeLicenseKey"] ?? throw new InvalidOperationException("GnosticeLicenseKey is not set in App.config.");
        
        string dllPath = ConfigurationManager.AppSettings["GnosticeDllPath"] ?? throw new InvalidOperationException("GnosticeDllPath is not set in App.config.");
        
        string licFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GnosticeDocumentStudio", "GnosticeDocumentStudio.lic");

        // Skip unresolvable VS SDK dependencies
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => null;

        Console.WriteLine("Loading extension DLL...");
        var asm = Assembly.LoadFrom(dllPath);

        Type[] types;
        try   
        { 
            types = asm.GetTypes();
        }
        catch (ReflectionTypeLoadException ex) 
        { 
            types = ex.Types.Where(t => t != null).ToArray(); // try to salvage sucessfuly loaded types and proceed
        }

        var providerType = types.FirstOrDefault(t => t.Name == "GnosticeAuthenticatedLicenseProvider"); // Class that is explicitly needed to handle Gnostice Licensing
        
        if (providerType is null) 
        { 
            Console.WriteLine("ERROR: GnosticeAuthenticatedLicenseProvider not found."); 
            return; 
        }

        Console.WriteLine("Creating provider instance...");
        var provider = Activator.CreateInstance(providerType, nonPublic: true);

        // Try to get method "GnosticeAuthenticatedLicenseProvider.AtomicAuthenticateAndInstallKey" - needed for  
        var method = providerType.GetMethod("AtomicAuthenticateAndInstallKey", BindingFlags.Public | BindingFlags.Instance)
                  ?? providerType.BaseType?.GetMethod("AtomicAuthenticateAndInstallKey", BindingFlags.Public | BindingFlags.Instance);

        if (method == null) 
        { 
                Console.WriteLine("ERROR: AtomicAuthenticateAndInstallKey not found."); // Cannot proceed without this method
                return; 
        }

        Console.WriteLine($"Authenticating key: {licenseKey}");
        Console.WriteLine("Contacting http://gnosticeauth.azurewebsites.net ...");

        try
        {
            method.Invoke(provider, new object[] { licenseKey });
            
            bool success = File.Exists(licFile);

            Console.WriteLine( success ? $"\nSUCCESS. License file written to:\n{licFile}" : "\nAuthentication ran but .lic file was NOT created — check errors above.");
        }
        catch (TargetInvocationException ex)
        {
            Console.WriteLine($"\nERROR: {ex.InnerException?.Message ?? ex.Message}");
        }

        Console.WriteLine("\nPress any key to exit.");
        Console.ReadKey();
    }
}
