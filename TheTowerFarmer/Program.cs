using Serilog;

namespace TheTowerFarmer;

internal class Program
{
    static async Task Main(string[] args)
    {
        using var logger = new LoggerConfiguration()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [Bootstrap] {Message:l}{NewLine}{Exception}")
        .CreateLogger();

        try
        {
            var tokenSource = new CancellationTokenSource();
            var devices = await Android.GetDevicesAsync(tokenSource.Token);

            if (devices.Count == 0)
            {
                var attempts = 0;

                while (devices.Count == 0) 
                {
                    logger.Warning("No emulator detected. Restarting ADB server and trying again...");
                    await Android.StopAsync(tokenSource.Token);

                    devices = await Android.GetDevicesAsync(tokenSource.Token);
                    attempts++;

                    if (attempts == 3)
                    {
                        logger.Error("No emulator detected. Make sure the emulator supports ADB and it is enabled.");
                        return;
                    }
                }
            }

            var gameAutomation = new GameAutomation(devices[0]);
            await gameAutomation.RunAsync(tokenSource.Token);
        } 
        catch (Exception e) 
        {
            logger.Fatal(e, "Error during startup");
        } 
        finally
        {
            Console.ReadLine();
        }
    }
}
