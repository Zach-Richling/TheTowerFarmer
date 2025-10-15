namespace TheTowerFarmer;

using OpenCvSharp;

internal class Program
{
    static async Task Main(string[] args)
    {
        var devices = await Android.GetDevicesAsync(CancellationToken.None);
        var gameAutomation = new GameAutomation(devices[0]);
        await gameAutomation.RunAsync(CancellationToken.None);
    }
}
