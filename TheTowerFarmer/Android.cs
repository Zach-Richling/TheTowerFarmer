using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheTowerFarmer
{
    internal class Android(string deviceSerial)
    {
        public static async Task<List<string>> GetDevicesAsync(CancellationToken token)
        {
            using var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "adb",
                    Arguments = "devices",
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(token);
            await process.WaitForExitAsync(token);

            return output.Split('\n')
                .Where(line => line.Contains("device") && !line.Contains("List"))
                .Select(line => line.Split('\t')[0].Trim())
                .ToList();
        }

        public static async Task StopAsync(CancellationToken token)
        {
            using var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "adb",
                    Arguments = "kill-server",
                    CreateNoWindow = true
                }
            };
            process.Start();
            await process.WaitForExitAsync(token);
        }

        public async Task<Mat> CaptureScreenAsync(CancellationToken token)
        {
            var args = $"-s {deviceSerial} exec-out screencap -p";
            using var process = new Process() 
            { 
                StartInfo = new ProcessStartInfo
                {
                    FileName = "adb",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();

            using var ms = new MemoryStream();
            await process.StandardOutput.BaseStream.CopyToAsync(ms, token);
            await process.WaitForExitAsync(token);
            
            return Cv2.ImDecode(ms.ToArray(), ImreadModes.Color);
        }

        public async Task TapAsync(int x, int y, CancellationToken token)
        {
            var args = $"-s {deviceSerial} shell input tap {x} {y}";
            var process = Process.Start("adb", args);
            await process.WaitForExitAsync(token);
        }
    }
}
