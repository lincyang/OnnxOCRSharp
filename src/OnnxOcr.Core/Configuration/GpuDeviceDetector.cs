//-----------------------------------------------------------------------
// <copyright file="GpuDeviceDetector.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using System.Runtime.InteropServices;

namespace OnnxOcr.Core.Configuration;

public static class GpuDeviceDetector
{
    public static IReadOnlyList<GpuDeviceInfo> DetectCudaDevices()
    {
        var devices = new List<GpuDeviceInfo>();

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
            !RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return devices;
        }

        try
        {
            int deviceCount = 0;
            var result = CudaRuntimeApi.cudaGetDeviceCount(ref deviceCount);
            if (result != 0 || deviceCount <= 0)
                return devices;

            for (int i = 0; i < deviceCount; i++)
            {
                var props = new CudaDeviceProp();
                if (CudaRuntimeApi.cudaGetDeviceProperties(ref props, i) != 0)
                    continue;

                long freeMemory = 0, totalMemory = 0;
                CudaRuntimeApi.cudaMemGetInfo(ref freeMemory, ref totalMemory);

                devices.Add(new GpuDeviceInfo
                {
                    DeviceId = i,
                    Name = props.name,
                    TotalMemoryBytes = totalMemory,
                    FreeMemoryBytes = freeMemory,
                    ComputeCapabilityMajor = props.major,
                    ComputeCapabilityMinor = props.minor,
                    IsAvailable = freeMemory > 0,
                });
            }
        }
        catch
        {
            // CUDA runtime not available
        }

        return devices;
    }

    public static bool IsCudaAvailable()
    {
        try
        {
            int deviceCount = 0;
            return CudaRuntimeApi.cudaGetDeviceCount(ref deviceCount) == 0 && deviceCount > 0;
        }
        catch
        {
            return false;
        }
    }

    public static int GetRecommendedDevice()
    {
        var devices = DetectCudaDevices();
        if (devices.Count == 0)
            return -1;

        return devices
            .Where(d => d.IsAvailable)
            .OrderByDescending(d => d.FreeMemoryBytes)
            .Select(d => d.DeviceId)
            .FirstOrDefault();
    }

    public static IReadOnlyList<GpuDeviceInfo> DetectGpus()
    {
        var cudaDevices = DetectCudaDevices();
        if (cudaDevices.Count > 0)
            return cudaDevices;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                return DetectGpusViaWmi();
            }
            catch
            {
                // WMI failed, fall through to default list
            }
        }

        return new List<GpuDeviceInfo>
        {
            new() { DeviceId = 0, Name = "GPU 0", IsAvailable = true },
        };
    }

    private static IReadOnlyList<GpuDeviceInfo> DetectGpusViaWmi()
    {
        var devices = new List<GpuDeviceInfo>();
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return devices;

        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT Name, AdapterRAM FROM Win32_VideoController");
            int id = 0;
            foreach (System.Management.ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString() ?? "";
                var ram = obj["AdapterRAM"];
                long totalBytes = 0;
                if (ram != null && long.TryParse(ram.ToString(), out var parsed))
                    totalBytes = parsed;

                var isNvidia = name.IndexOf("nvidia", StringComparison.OrdinalIgnoreCase) >= 0;
                var isAmd = name.IndexOf("amd", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("radeon", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!isNvidia && !isAmd)
                    continue;

                devices.Add(new GpuDeviceInfo
                {
                    DeviceId = id,
                    Name = name,
                    TotalMemoryBytes = totalBytes,
                    FreeMemoryBytes = totalBytes,
                    IsAvailable = true,
                });
                id++;
            }
        }
        catch
        {
            // WMI not available
        }

        return devices;
    }

    private static class CudaRuntimeApi
    {
        private const string LibName = "cudart";

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int cudaGetDeviceCount(ref int count);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int cudaGetDeviceProperties(ref CudaDeviceProp prop, int device);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int cudaMemGetInfo(ref long free, ref long total);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int cudaSetDevice(int device);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CudaDeviceProp
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public byte[] nameBytes;
        public long totalGlobalMem;
        public long sharedMemPerBlock;
        public int regsPerBlock;
        public int warpSize;
        public long memPitch;
        public int maxThreadsPerBlock;
        public int major;
        public int minor;
        public int clockRate;
        public long totalConstMem;

        public string name
        {
            get
            {
                if (nameBytes == null)
                    return "";

                int len = 0;
                while (len < nameBytes.Length && nameBytes[len] != 0) len++;
                return System.Text.Encoding.UTF8.GetString(nameBytes, 0, len);
            }
        }
    }
}
