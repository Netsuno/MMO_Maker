using System.Text.Json;

namespace Frog.LoadHarness;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "--emit-test-certs")
        {
            try
            {
                LoadTestCertificates.Write(args[1]);
                Console.WriteLine("wrote test CA/leaf PEMs to " + Path.GetFullPath(args[1]));
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("error: " + ex.Message);
                return 2;
            }
        }

        LoadHarnessOptions options;
        try
        {
            options = LoadHarnessOptions.Parse(args);
        }
        catch (LoadHarnessHelpException)
        {
            Console.WriteLine(LoadHarnessRunner.Usage);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("error: " + ex.Message);
            Console.Error.WriteLine(LoadHarnessRunner.Usage);
            return 2;
        }

        try
        {
            var report = await LoadHarnessRunner.RunAsync(options).ConfigureAwait(false);
            Console.WriteLine(JsonSerializer.Serialize(report, LoadHarnessRunner.JsonOptions));
            if (report.Client.HelloOk <= 0)
            {
                Console.Error.WriteLine("load harness: no Hello received");
                return 1;
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("load harness failed: " + ex);
            return 1;
        }
    }
}
