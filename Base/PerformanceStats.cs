using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace Base
{
	public static class PerformanceStats
	{
		public static double GetCpuUsagePercentage()
		{
			var startTime = DateTime.UtcNow;
			var startCpuUsage = Process.GetProcesses().Sum(a => a.TotalProcessorTime.TotalMilliseconds);

			System.Threading.Thread.Sleep(500);

			var endTime = DateTime.UtcNow;
			var endCpuUsage = Process.GetProcesses().Sum(a => a.TotalProcessorTime.TotalMilliseconds);

			var cpuUsedMs = endCpuUsage - startCpuUsage;
			var totalMsPassed = (endTime - startTime).TotalMilliseconds;
			var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);

			return Math.Round(cpuUsageTotal * 100, 1);
		}

		public static double GetMemoryUsagePercentage()
		{
			//https://gunnarpeipman.com/dotnet-core-system-memory/

			var output = "";
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				var info = new ProcessStartInfo("free -m");
				info.FileName = "/bin/bash";
				info.Arguments = "-c \"free -m\"";
				info.RedirectStandardOutput = true;

				using (var process = Process.Start(info))
				{
					output = process.StandardOutput.ReadToEnd();
					Console.WriteLine(output);
				}

				var lines = output.Split("\n");
				var memory = lines[1].Split(" ", StringSplitOptions.RemoveEmptyEntries);

				double total = double.Parse(memory[1]);
				double used = double.Parse(memory[2]);

				return Math.Round(used / total, 1);
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				var info = new ProcessStartInfo();
				info.FileName = "wmic";
				info.Arguments = "OS get FreePhysicalMemory,TotalVisibleMemorySize /Value";
				info.RedirectStandardOutput = true;

				using (var process = Process.Start(info))
				{
					output = process.StandardOutput.ReadToEnd();
				}

				var lines = output.Trim().Split("\n");
				var freeMemoryParts = lines[0].Split("=", StringSplitOptions.RemoveEmptyEntries);
				var totalMemoryParts = lines[1].Split("=", StringSplitOptions.RemoveEmptyEntries);

				double total = Math.Round(double.Parse(totalMemoryParts[1]) / 1024, 0);
				double available = Math.Round(double.Parse(freeMemoryParts[1]) / 1024, 0);

				return Math.Round((total - available) / total, 1);
			}

			return -1;
		}
	}
}
