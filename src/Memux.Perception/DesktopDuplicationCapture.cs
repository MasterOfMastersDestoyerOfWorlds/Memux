using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Vortice.DXGI;
using Vortice.Direct3D;
using Vortice.Direct3D11;

namespace Memux.Perception;

internal sealed class DesktopDuplicationCapture : IDisposable
{
    private readonly IDXGIOutputDuplication _duplication;
    private readonly ID3D11Device _device;
    private readonly ID3D11DeviceContext _context;
    private readonly int _width;
    private readonly int _height;

    private DesktopDuplicationCapture(IDXGIOutputDuplication duplication, ID3D11Device device, ID3D11DeviceContext context, int width, int height)
    {
        _duplication = duplication;
        _device = device;
        _context = context;
        _width = width;
        _height = height;
    }

    public static DesktopDuplicationCapture? TryCreateForWindow(IntPtr window)
    {
        try
        {
            // For simplicity, duplicate the primary output.
            // Create DXGI factory
            IDXGIFactory1 factoryObj;
            DXGI.CreateDXGIFactory1(out factoryObj);
            using var factory = factoryObj;

            // Get adapter 0
            IDXGIAdapter1 adapterObj;
            factory.EnumAdapters1(0u, out adapterObj);
            using var adapter = adapterObj;

            // Create D3D11 device
            ID3D11Device device;
            ID3D11DeviceContext context;
            FeatureLevel createdLevel;
            D3D11.D3D11CreateDevice(
                adapter,
                DriverType.Unknown,
                DeviceCreationFlags.BgraSupport,
                new[] { FeatureLevel.Level_11_0, FeatureLevel.Level_10_0 },
                out device,
                out createdLevel,
                out context);

            // Duplicate output 0
            IDXGIOutput outputObj;
            adapter.EnumOutputs(0u, out outputObj);
            using var output = outputObj;
            using var output1 = output.QueryInterface<IDXGIOutput1>();
            using var outputDup = output1.DuplicateOutput(device);

            // Get output desc to size
            var desc = output.Description;
            int width = desc.DesktopCoordinates.Right - desc.DesktopCoordinates.Left;
            int height = desc.DesktopCoordinates.Bottom - desc.DesktopCoordinates.Top;

            return new DesktopDuplicationCapture(outputDup, device, context, width, height);
        }
        catch
        {
            return null;
        }
    }

    public (byte[] data, int width, int height)? TryCaptureFrame()
    {
        try
        {
            _duplication.AcquireNextFrame(16, out var frameInfo, out var resource);
            try
            {
                using var tex = resource.QueryInterface<ID3D11Texture2D>();
                var desc = tex.Description;
                // Stage to CPU readable by cloning desc and adjusting flags
                var stagingDesc = desc;
                stagingDesc.BindFlags = BindFlags.None;
                stagingDesc.CPUAccessFlags = CpuAccessFlags.Read;
                stagingDesc.Usage = ResourceUsage.Staging;
                // SampleDescription/OptionFlags keep defaults from desc; not required for staging
                using var staging = _device.CreateTexture2D(stagingDesc);
                _context.CopyResource(staging, tex);

                var dataBox = _context.Map(staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                try
                {
                    int pitch = unchecked((int)dataBox.RowPitch);
                    int width = unchecked((int)desc.Width);
                    int height = unchecked((int)desc.Height);
                    int tightStride = width * 4;
                    var buffer = new byte[height * tightStride];
                    unsafe
                    {
                        byte* srcBase = (byte*)dataBox.DataPointer;
                        fixed (byte* dstBase = buffer)
                        {
                            for (int y = 0; y < height; y++)
                            {
                                byte* src = srcBase + (y * pitch);
                                byte* dst = dstBase + (y * tightStride);
                                Buffer.MemoryCopy(src, dst, (long)tightStride, (long)tightStride);
                            }
                        }
                    }
                    return (buffer, width, height);
                }
                finally
                {
                    _context.Unmap(staging, 0);
                }
            }
            finally
            {
                _duplication.ReleaseFrame();
                resource?.Dispose();
            }
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        try { _duplication?.Dispose(); } catch { }
        try { _context?.Dispose(); } catch { }
        try { _device?.Dispose(); } catch { }
    }
}


