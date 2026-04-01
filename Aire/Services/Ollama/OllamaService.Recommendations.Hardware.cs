using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;

namespace Aire.Services
{
    public partial class OllamaService
    {
        /// <summary>
        /// Intermediate GPU record used while comparing adapters discovered via NVIDIA tooling, DXGI, or WMI.
        /// </summary>
        private record GpuCandidate(string Name, string Vendor, double VideoRamGb, bool IsDiscrete);

        [DllImport("kernel32.dll")]
        private static extern bool GetPhysicallyInstalledSystemMemory(out long totalMemoryInKilobytes);

        [DllImport("dxgi.dll")]
        private static extern int CreateDXGIFactory1(ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppFactory);

        /// <summary>
        /// Reads physically installed system memory in gigabytes.
        /// </summary>
        private static double GetInstalledRamGb()
        {
            try
            {
                if (GetPhysicallyInstalledSystemMemory(out var kb))
                    return kb / (1024.0 * 1024.0);
            }
            catch
            {
            }

            return 0;
        }

        /// <summary>
        /// Reads free space from the system drive in gigabytes.
        /// </summary>
        private static double GetSystemDriveFreeSpaceGb()
        {
            try
            {
                var root = Path.GetPathRoot(Environment.SystemDirectory);
                if (!string.IsNullOrWhiteSpace(root))
                {
                    var drive = new DriveInfo(root);
                    if (drive.IsReady)
                        return drive.AvailableFreeSpace / 1_073_741_824.0;
                }
            }
            catch
            {
            }

            return 0;
        }

        /// <summary>
        /// Buckets the machine into a coarse RAM tier used by the recommendation UI.
        /// </summary>
        private static string GetPerformanceTier(double ramGb)
        {
            if (ramGb <= 0) return "unknown";
            if (ramGb < 8) return "starter";
            if (ramGb < 16) return "balanced";
            if (ramGb < 32) return "strong";
            return "high-end";
        }

        /// <summary>
        /// Builds the friendly machine summary shown to users when recommending local models.
        /// </summary>
        private static string BuildSystemSummary(double ramGb, double freeDiskGb, double videoRamGb, string gpuName)
        {
            if (ramGb <= 0)
                return "Aire could not read your RAM, so recommendations are conservative.";

            var modelRange = ramGb switch
            {
                < 8 => "small local models should work best",
                < 16 => "small and medium local models should run comfortably",
                < 32 => "medium and some larger local models should run well",
                _ => "you can try larger local models, although size still matters"
            };

            var gpuLabel = videoRamGb > 0
                ? string.IsNullOrWhiteSpace(gpuName)
                    ? $"{videoRamGb:0.#} GB VRAM"
                    : $"{videoRamGb:0.#} GB VRAM on {gpuName}"
                : string.Empty;

            if (videoRamGb > 0 && freeDiskGb > 0)
                return $"Detected {ramGb:0.#} GB RAM, {gpuLabel}, and about {freeDiskGb:0.#} GB free disk space, so {modelRange}.";

            if (videoRamGb > 0)
                return $"Detected {ramGb:0.#} GB RAM and {gpuLabel}, so {modelRange}.";

            if (freeDiskGb > 0)
                return $"Detected {ramGb:0.#} GB RAM and about {freeDiskGb:0.#} GB free disk space, so {modelRange}.";

            return $"Detected {ramGb:0.#} GB RAM, so {modelRange}.";
        }

        /// <summary>
        /// Detects the best GPU candidate for Ollama recommendations.
        /// Prefers vendor-specific sources first, then falls back to generic Windows APIs.
        /// </summary>
        private static (double VideoRamGb, string PrimaryGpuName) GetInstalledVideoRamGb()
        {
            var nvidia = TryGetVideoRamViaNvidiaSmi();
            if (nvidia.VideoRamGb > 0)
                return nvidia;

            var dxgi = TryGetVideoRamViaDxgi();
            if (dxgi.VideoRamGb > 0)
                return dxgi;

            return TryGetVideoRamViaWmi();
        }

        /// <summary>
        /// Queries NVIDIA VRAM using nvidia-smi when available, which is typically more accurate than generic APIs.
        /// </summary>
        private static (double VideoRamGb, string PrimaryGpuName) TryGetVideoRamViaNvidiaSmi()
        {
            try
            {
                var candidates = new[]
                {
                    Path.Combine(Environment.SystemDirectory, "nvidia-smi.exe"),
                    @"C:\Program Files\NVIDIA Corporation\NVSMI\nvidia-smi.exe"
                };

                var executable = candidates.FirstOrDefault(File.Exists);
                if (string.IsNullOrWhiteSpace(executable))
                    return (0, string.Empty);

                var startInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = "--query-gpu=name,memory.total --format=csv,noheader,nounits",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                    return (0, string.Empty);

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(3000);

                var bestMb = 0.0;
                var bestName = string.Empty;

                foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = line.Split(',', 2, StringSplitOptions.TrimEntries);
                    if (parts.Length != 2)
                        continue;

                    if (!double.TryParse(parts[1], out var mb) || mb <= bestMb)
                        continue;

                    bestMb = mb;
                    bestName = parts[0];
                }

                return bestMb > 0 ? (bestMb / 1024.0, bestName) : (0, string.Empty);
            }
            catch
            {
                return (0, string.Empty);
            }
        }

        /// <summary>
        /// Enumerates adapters through DXGI and selects the best discrete GPU candidate.
        /// </summary>
        private static (double VideoRamGb, string PrimaryGpuName) TryGetVideoRamViaDxgi()
        {
            IDXGIFactory1? factory = null;
            try
            {
                var factoryGuid = typeof(IDXGIFactory1).GUID;
                var hr = CreateDXGIFactory1(ref factoryGuid, out var factoryObject);
                if (hr != 0 || factoryObject is not IDXGIFactory1 createdFactory)
                    return (0, string.Empty);

                factory = createdFactory;
                GpuCandidate? best = null;

                for (uint i = 0; ; i++)
                {
                    var result = factory.EnumAdapters1(i, out var adapter);
                    if (result != 0 || adapter == null)
                        break;

                    try
                    {
                        adapter.GetDesc1(out var desc);
                        var isSoftware = (desc.Flags & 2u) != 0;
                        if (isSoftware || desc.DedicatedVideoMemory == 0)
                            continue;

                        var candidate = new GpuCandidate(
                            desc.Description ?? string.Empty,
                            GetGpuVendorName(desc.VendorId),
                            desc.DedicatedVideoMemory / 1_073_741_824.0,
                            IsDiscreteAdapter(desc));

                        if (best == null || CompareGpuCandidates(candidate, best) > 0)
                            best = candidate;
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(adapter);
                    }
                }

                return best != null ? (best.VideoRamGb, best.Name) : (0, string.Empty);
            }
            catch
            {
                return (0, string.Empty);
            }
            finally
            {
                if (factory != null)
                    Marshal.ReleaseComObject(factory);
            }
        }

        /// <summary>
        /// Falls back to WMI-based GPU enumeration when more reliable vendor-specific paths are unavailable.
        /// </summary>
        private static (double VideoRamGb, string PrimaryGpuName) TryGetVideoRamViaWmi()
        {
            try
            {
                GpuCandidate? best = null;

                using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController");
                foreach (var obj in searcher.Get().OfType<ManagementObject>())
                {
                    var ramBytes = TryConvertToUInt64(obj["AdapterRAM"]);
                    if (ramBytes <= 0)
                        continue;

                    var name = obj["Name"]?.ToString() ?? string.Empty;
                    var candidate = new GpuCandidate(
                        name,
                        GuessVendorFromName(name),
                        ramBytes / 1_073_741_824.0,
                        GuessDiscreteFromName(name));

                    if (best == null || CompareGpuCandidates(candidate, best) > 0)
                        best = candidate;
                }

                return best != null ? (best.VideoRamGb, best.Name) : (0, string.Empty);
            }
            catch
            {
                return (0, string.Empty);
            }
        }

        /// <summary>
        /// Best-effort conversion for GPU memory values returned in mixed WMI types.
        /// </summary>
        private static ulong TryConvertToUInt64(object? value)
        {
            try
            {
                return value switch
                {
                    null => 0,
                    ulong u => u,
                    long l when l > 0 => (ulong)l,
                    uint ui => ui,
                    int i when i > 0 => (ulong)i,
                    string s when ulong.TryParse(s, out var parsed) => parsed,
                    _ => Convert.ToUInt64(value)
                };
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Ranks two GPU candidates so the recommendation system prefers the most useful adapter for local inference.
        /// </summary>
        private static int CompareGpuCandidates(GpuCandidate left, GpuCandidate right)
        {
            var discrete = left.IsDiscrete.CompareTo(right.IsDiscrete);
            if (discrete != 0)
                return discrete;

            var vram = left.VideoRamGb.CompareTo(right.VideoRamGb);
            if (vram != 0)
                return vram;

            return GetVendorPriority(left.Vendor).CompareTo(GetVendorPriority(right.Vendor));
        }

        private static int GetVendorPriority(string vendor)
            => vendor switch
            {
                "NVIDIA" => 4,
                "AMD" => 3,
                "Intel" => 2,
                _ => 1
            };

        private static string GetGpuVendorName(uint vendorId)
            => vendorId switch
            {
                0x10DE => "NVIDIA",
                0x1002 or 0x1022 => "AMD",
                0x8086 => "Intel",
                0x1414 => "Microsoft",
                0x5143 => "Qualcomm",
                _ => "Other"
            };

        private static bool IsDiscreteAdapter(DXGI_ADAPTER_DESC1 desc)
        {
            var name = desc.Description ?? string.Empty;
            if (desc.DedicatedVideoMemory >= 2UL * 1_073_741_824UL)
                return true;

            var vendor = GetGpuVendorName(desc.VendorId);
            return vendor is "NVIDIA" or "AMD"
                || name.Contains("Arc", StringComparison.OrdinalIgnoreCase)
                || name.Contains("RTX", StringComparison.OrdinalIgnoreCase)
                || name.Contains("RX ", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Radeon", StringComparison.OrdinalIgnoreCase);
        }

        private static string GuessVendorFromName(string name)
        {
            if (name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("GeForce", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Quadro", StringComparison.OrdinalIgnoreCase))
                return "NVIDIA";

            if (name.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Radeon", StringComparison.OrdinalIgnoreCase))
                return "AMD";

            if (name.Contains("Intel", StringComparison.OrdinalIgnoreCase))
                return "Intel";

            return "Other";
        }

        private static bool GuessDiscreteFromName(string name)
            => name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)
            || name.Contains("GeForce", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Quadro", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Radeon", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Arc", StringComparison.OrdinalIgnoreCase)
            || name.Contains("RTX", StringComparison.OrdinalIgnoreCase)
            || name.Contains("RX ", StringComparison.OrdinalIgnoreCase);

        [ComImport]
        [Guid("770aae78-f26f-4dba-a829-253c83d1b387")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDXGIFactory1
        {
            int SetPrivateData();
            int SetPrivateDataInterface();
            int GetPrivateData();
            int GetParent();
            int EnumAdapters(uint adapter, out object adapterObject);
            int MakeWindowAssociation(IntPtr windowHandle, uint flags);
            int GetWindowAssociation(out IntPtr windowHandle);
            int CreateSwapChain();
            int CreateSoftwareAdapter();
            int EnumAdapters1(uint adapter, out IDXGIAdapter1 adapterObject);
            int IsCurrent();
        }

        [ComImport]
        [Guid("29038f61-3839-4626-91fd-086879011a05")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDXGIAdapter1
        {
            int SetPrivateData();
            int SetPrivateDataInterface();
            int GetPrivateData();
            int GetParent();
            int EnumOutputs();
            int GetDesc(out DXGI_ADAPTER_DESC desc);
            int CheckInterfaceSupport(ref Guid interfaceName, out long umdVersion);
            int GetDesc1(out DXGI_ADAPTER_DESC1 desc);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DXGI_ADAPTER_DESC
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Description;
            public uint VendorId;
            public uint DeviceId;
            public uint SubSysId;
            public uint Revision;
            public nuint DedicatedVideoMemory;
            public nuint DedicatedSystemMemory;
            public nuint SharedSystemMemory;
            public LUID AdapterLuid;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DXGI_ADAPTER_DESC1
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Description;
            public uint VendorId;
            public uint DeviceId;
            public uint SubSysId;
            public uint Revision;
            public ulong DedicatedVideoMemory;
            public ulong DedicatedSystemMemory;
            public ulong SharedSystemMemory;
            public LUID AdapterLuid;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }
    }
}
