//-----------------------------------------------------------------------
// <copyright file="OnnxSessionFactory.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using Microsoft.ML.OnnxRuntime;
using OnnxOcr.Core.Configuration;

namespace OnnxOcr.Core.Inference;

public sealed class OnnxSessionFactory
{
    private readonly OcrOptions _options;

    public OnnxSessionFactory(OcrOptions options)
    {
        _options = options;
    }

    public InferenceSession Create(string modelPath)
    {
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException("Model file not found.", modelPath);
        }

        var sessionOptions = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
        };

        if (_options.CpuThreads > 0)
            sessionOptions.IntraOpNumThreads = _options.CpuThreads;

        if (_options.UseGpu)
        {
            ConfigureGpu(sessionOptions);
        }

        var session = new InferenceSession(modelPath, sessionOptions);

        var providers = GetSessionProviders(session);
        var deviceInfo = _options.UseGpu
            ? $" | GPU requested (id={_options.GpuId}, auto={_options.AutoSelectGpu})"
            : " | CPU mode";
        OcrLogger.Log($"[OnnxSessionFactory] Model={Path.GetFileName(modelPath)}, Providers=[{providers}]{deviceInfo}");

        return session;
    }

    private static string GetSessionProviders(InferenceSession session)
    {
        try
        {
            var prop = session.GetType().GetProperty("Providers");
            if (prop != null)
            {
                var val = prop.GetValue(session) as System.Collections.IEnumerable;
                if (val != null)
                {
                    var list = new List<string>();
                    foreach (var item in val)
                        list.Add(item?.ToString() ?? "");
                    return string.Join(", ", list);
                }
            }
        }
        catch { }
        return "unknown";
    }

    private void ConfigureGpu(SessionOptions sessionOptions)
    {
        var deviceId = ResolveGpuDeviceId();
        if (deviceId < 0)
        {
            OcrLogger.Log("[OnnxSessionFactory] GPU deviceId resolved to -1, skipping CUDA.");
            return;
        }

        try
        {
            OcrLogger.Log($"[OnnxSessionFactory] Attempting CUDA provider on device {deviceId}...");
            sessionOptions.AppendExecutionProvider_CUDA(deviceId);
            OcrLogger.Log($"[OnnxSessionFactory] CUDA provider appended successfully on device {deviceId}.");
        }
        catch (OnnxRuntimeException ex) when (IsCudaNotAvailable(ex))
        {
            OcrLogger.Log($"[OnnxSessionFactory] ERROR: OnnxRuntime CUDA provider unavailable - {ex.Message}");
        }
        catch (DllNotFoundException ex)
        {
            OcrLogger.Log($"[OnnxSessionFactory] ERROR: CUDA runtime DLL not found - {ex.Message}");
        }
        catch (Exception ex)
        {
            OcrLogger.Log($"[OnnxSessionFactory] ERROR: Failed to configure GPU - {ex.GetType().Name}: {ex.Message}");
        }
    }

    private int ResolveGpuDeviceId()
    {
        if (!_options.UseGpu)
        {
            OcrLogger.Log("[OnnxSessionFactory] UseGpu=false, returning -1");
            return -1;
        }

        if (_options.AutoSelectGpu)
        {
            var recommended = GpuDeviceDetector.GetRecommendedDevice();
            if (recommended >= 0)
            {
                OcrLogger.Log($"[OnnxSessionFactory] AutoSelectGpu=true, recommended device: {recommended}");
                return recommended;
            }

            OcrLogger.Log("[OnnxSessionFactory] Auto GPU selection found no CUDA device, returning -1");
            return -1;
        }

        // 用户手动指定 GPU ID，直接返回，让 AppendExecutionProvider_CUDA 自己验证
        OcrLogger.Log($"[OnnxSessionFactory] User specified GPU ID: {_options.GpuId}");
        return _options.GpuId;
    }

    private static bool IsCudaNotAvailable(OnnxRuntimeException ex)
    {
        var message = ex.Message.ToLowerInvariant();
        return message.Contains("cuda") ||
               message.Contains("gpu") ||
               message.Contains("execution provider");
    }
}
