using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Vortice.Direct3D11;
using Vortice.DXGI;
using WinRT;

namespace Memux.Perception;

/// <summary>
/// Windows.Graphics.Capture API-based window capture
/// Can capture windows even when occluded (behind other windows)
/// Modern replacement for GDI/DWM approaches for games
/// </summary>
public sealed class GraphicsCaptureCapture : IDisposable
{
    private readonly GraphicsCaptureItem _captureItem;
    private readonly Direct3D11CaptureFramePool _framePool;
    private readonly GraphicsCaptureSession _session;
    private readonly ID3D11Device _d3dDevice;
    private readonly IDirect3DDevice _device;
    private byte[]? _lastFrame;
    private int _lastWidth;
    private int _lastHeight;

    private GraphicsCaptureCapture(
        GraphicsCaptureItem captureItem,
        Direct3D11CaptureFramePool framePool,
        GraphicsCaptureSession session,
        ID3D11Device d3dDevice,
        IDirect3DDevice device)
    {
        _captureItem = captureItem;
        _framePool = framePool;
        _session = session;
        _d3dDevice = d3dDevice;
        _device = device;

        _framePool.FrameArrived += OnFrameArrived;
    }

    public static GraphicsCaptureCapture? TryCreateForWindow(IntPtr hwnd)
    {
        try
        {
            Console.WriteLine($"[GraphicsCapture] Attempting to create for window {hwnd:X}");
            
            // Create D3D11 device
            ID3D11Device d3dDevice;
            ID3D11DeviceContext context;
            Vortice.Direct3D.FeatureLevel createdLevel;
            Vortice.Direct3D11.D3D11.D3D11CreateDevice(
                null,
                Vortice.Direct3D.DriverType.Hardware,
                Vortice.Direct3D11.DeviceCreationFlags.BgraSupport,
                new[] { Vortice.Direct3D.FeatureLevel.Level_11_0, Vortice.Direct3D.FeatureLevel.Level_10_0 },
                out d3dDevice,
                out createdLevel,
                out context);

            Console.WriteLine($"[GraphicsCapture] D3D11 device created");

            // Wrap as WinRT Direct3D device
            var dxgiDevice = d3dDevice.QueryInterface<IDXGIDevice>();
            Console.WriteLine($"[GraphicsCapture] Got DXGI device: {dxgiDevice.NativePointer:X}");
            
            var hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out var devicePtr);
            if (hr != 0 || devicePtr == IntPtr.Zero)
            {
                Console.WriteLine($"[GraphicsCapture] CreateDirect3D11DeviceFromDXGIDevice failed: {hr:X}");
                return null;
            }
            
            Console.WriteLine($"[GraphicsCapture] Got WinRT device pointer: {devicePtr:X}");
            
            // Use WinRT marshaling to create proper IDirect3DDevice
            var device = MarshalInterface<IDirect3DDevice>.FromAbi(devicePtr);
            
            Console.WriteLine($"[GraphicsCapture] WinRT device wrapped, type: {device.GetType().FullName}");

            // Create capture item for window using activation factory
            var captureItem = CreateCaptureItemForWindow(hwnd);
            if (captureItem == null)
            {
                Console.WriteLine($"[GraphicsCapture] Failed to create capture item");
                return null;
            }

            Console.WriteLine($"[GraphicsCapture] Capture item created, size: {captureItem.Size.Width}x{captureItem.Size.Height}");

            // Create frame pool
            Console.WriteLine($"[GraphicsCapture] About to create frame pool with device type: {device.GetType().FullName}");
            Direct3D11CaptureFramePool framePool;
            try
            {
                framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                    device,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    2,
                    captureItem.Size);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GraphicsCapture] Frame pool creation failed: {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"[GraphicsCapture] Inner exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                throw;
            }

            Console.WriteLine($"[GraphicsCapture] Frame pool created");

            // Create session
            var session = framePool.CreateCaptureSession(captureItem);
            session.IsBorderRequired = false;
            session.IsCursorCaptureEnabled = false;
            session.StartCapture();

            Console.WriteLine($"[GraphicsCapture] Capture session started");

            return new GraphicsCaptureCapture(captureItem, framePool, session, d3dDevice, device);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GraphicsCapture] Failed to create: {ex.Message}");
            return null;
        }
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        try
        {
            using var frame = sender.TryGetNextFrame();
            if (frame == null)
            {
                Console.WriteLine("[GraphicsCapture] Frame is null");
                return;
            }

            var surfaceTexture = frame.Surface;
            var desc = frame.ContentSize;

            // Get the D3D11 texture via COM interop using WinRT marshaling
            var surfacePtr = MarshalInspectable<IDirect3DSurface>.FromManaged(surfaceTexture);
            
            // Query for IDirect3DDxgiInterfaceAccess
            var accessGuid = typeof(IDirect3DDxgiInterfaceAccess).GUID;
            var hr = Marshal.QueryInterface(surfacePtr, ref accessGuid, out var accessPtr);
            if (hr != 0 || accessPtr == IntPtr.Zero)
            {
                Console.WriteLine($"[GraphicsCapture] QueryInterface for IDirect3DDxgiInterfaceAccess failed: {hr:X}");
                return;
            }

            // Call GetInterface via vtable
            var texturePtr = GetDxgiInterface(accessPtr, typeof(ID3D11Texture2D).GUID);
            Marshal.Release(accessPtr);
            
            if (texturePtr == IntPtr.Zero)
            {
                Console.WriteLine($"[GraphicsCapture] GetInterface for ID3D11Texture2D failed");
                return;
            }

            var texture = new ID3D11Texture2D(texturePtr);

            // Create staging texture - build fresh desc to avoid incompatible fields
            var texDesc = texture.Description;
            var stagingDesc = new Vortice.Direct3D11.Texture2DDescription
            {
                Width = texDesc.Width,
                Height = texDesc.Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = texDesc.Format,
                SampleDescription = new Vortice.DXGI.SampleDescription(1, 0),
                Usage = Vortice.Direct3D11.ResourceUsage.Staging,
                BindFlags = Vortice.Direct3D11.BindFlags.None,
                CPUAccessFlags = Vortice.Direct3D11.CpuAccessFlags.Read,
                MiscFlags = Vortice.Direct3D11.ResourceOptionFlags.None
            };

            using var staging = _d3dDevice.CreateTexture2D(stagingDesc);
            var deviceContext = _d3dDevice.ImmediateContext;
            deviceContext.CopyResource(staging, texture);

            var mapped = deviceContext.Map(staging, 0, Vortice.Direct3D11.MapMode.Read, Vortice.Direct3D11.MapFlags.None);
            try
            {
                int width = unchecked((int)texDesc.Width);
                int height = unchecked((int)texDesc.Height);
                int pitch = unchecked((int)mapped.RowPitch);
                int tightStride = width * 4;

                var buffer = new byte[height * tightStride];
                unsafe
                {
                    byte* srcBase = (byte*)mapped.DataPointer;
                    fixed (byte* dstBase = buffer)
                    {
                        for (int y = 0; y < height; y++)
                        {
                            byte* src = srcBase + (y * pitch);
                            byte* dst = dstBase + (y * tightStride);
                            Buffer.MemoryCopy(src, dst, tightStride, tightStride);
                        }
                    }
                }

                _lastFrame = buffer;
                _lastWidth = width;
                _lastHeight = height;
            }
            finally
            {
                deviceContext.Unmap(staging, 0);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GraphicsCapture] Frame processing failed: {ex.Message}");
        }
    }

    public (byte[] data, int width, int height)? TryCaptureFrame()
    {
        if (_lastFrame == null)
        {
            Console.WriteLine("[GraphicsCapture] TryCaptureFrame: No frame available yet");
            return null;
        }
        return (_lastFrame, _lastWidth, _lastHeight);
    }

    public void Dispose()
    {
        try { _session?.Dispose(); } catch { }
        try { _framePool?.Dispose(); } catch { }
        try { _device?.Dispose(); } catch { }
        try { _d3dDevice?.Dispose(); } catch { }
    }

    [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    private static GraphicsCaptureItem? CreateCaptureItemForWindow(IntPtr hwnd)
    {
        try
        {
            // Get the activation factory for GraphicsCaptureItem
            var interopGuid = typeof(IGraphicsCaptureItemInterop).GUID;
            var activatableClassId = "Windows.Graphics.Capture.GraphicsCaptureItem";
            
            // Create HSTRING manually
            var hr = WindowsCreateString(activatableClassId, (uint)activatableClassId.Length, out var hString);
            if (hr != 0)
            {
                Console.WriteLine($"[GraphicsCapture] WindowsCreateString failed: {hr:X}");
                return null;
            }

            hr = RoGetActivationFactory(hString, ref interopGuid, out var factoryPtr);
            WindowsDeleteString(hString);
            
            if (hr != 0 || factoryPtr == IntPtr.Zero)
            {
                Console.WriteLine($"[GraphicsCapture] RoGetActivationFactory failed: {hr:X}");
                return null;
            }

            Console.WriteLine($"[GraphicsCapture] Factory pointer: {factoryPtr:X}");

            // Try QueryInterface for the interop interface
            var iid = typeof(IGraphicsCaptureItemInterop).GUID;
            hr = Marshal.QueryInterface(factoryPtr, ref iid, out var interopPtr);
            
            if (hr != 0 || interopPtr == IntPtr.Zero)
            {
                Console.WriteLine($"[GraphicsCapture] QueryInterface for IGraphicsCaptureItemInterop failed: {hr:X}");
                Marshal.Release(factoryPtr);
                return null;
            }

            Console.WriteLine($"[GraphicsCapture] Got interop pointer: {interopPtr:X}");

            // Call CreateForWindow directly via the vtable
            Console.WriteLine($"[GraphicsCapture] Calling CreateForWindow via vtable...");
            hr = CreateForWindowViaVTable(interopPtr, hwnd, out var itemPointer);
            
            Marshal.Release(interopPtr);
            Marshal.Release(factoryPtr);
            
            if (hr != 0 || itemPointer == IntPtr.Zero)
            {
                Console.WriteLine($"[GraphicsCapture] CreateForWindow failed: {hr:X}");
                return null;
            }

            Console.WriteLine($"[GraphicsCapture] CreateForWindow returned: {itemPointer:X}");
            
            var captureItem = GraphicsCaptureItem.FromAbi(itemPointer);
            Marshal.Release(itemPointer);
            return captureItem;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GraphicsCapture] CreateCaptureItemForWindow failed: {ex.Message}");
            return null;
        }
    }

    [DllImport("api-ms-win-core-winrt-string-l1-1-0.dll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = true)]
    private static extern int WindowsCreateString(
        [MarshalAs(UnmanagedType.LPWStr)] string sourceString,
        uint length,
        out IntPtr hString);

    [DllImport("api-ms-win-core-winrt-string-l1-1-0.dll", ExactSpelling = true, PreserveSig = true)]
    private static extern int WindowsDeleteString(IntPtr hString);

    [DllImport("api-ms-win-core-winrt-l1-1-0.dll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = true)]
    private static extern int RoGetActivationFactory(
        IntPtr activatableClassId,
        ref Guid iid,
        out IntPtr factory);

    // IGraphicsCaptureItemInterop::CreateForWindow
    // VTable: IUnknown (3 methods) + CreateForWindow (index 3) + CreateForMonitor (index 4)
    private static int CreateForWindowViaVTable(IntPtr interopPtr, IntPtr hwnd, out IntPtr captureItem)
    {
        unsafe
        {
            // Get the vtable pointer
            var vtable = *(IntPtr*)interopPtr;
            // CreateForWindow is at index 3 (after QueryInterface, AddRef, Release)
            var createForWindowPtr = *((IntPtr*)vtable + 3);
            
            // Call the function: HRESULT CreateForWindow(HWND hwnd, REFIID riid, void** result)
            // IID for IGraphicsCaptureItem: 79C3F95B-31F7-4EC2-A464-632EF5D30760
            var iid = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");
            Console.WriteLine($"[GraphicsCapture] Using IID: {iid}");
            var createForWindow = Marshal.GetDelegateForFunctionPointer<CreateForWindowDelegate>(createForWindowPtr);
            return createForWindow(interopPtr, hwnd, ref iid, out captureItem);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateForWindowDelegate(IntPtr thisPtr, IntPtr hwnd, ref Guid riid, out IntPtr result);

    // Helper to call IDirect3DDxgiInterfaceAccess::GetInterface via vtable
    private static IntPtr GetDxgiInterface(IntPtr accessPtr, Guid iid)
    {
        unsafe
        {
            var vtable = *(IntPtr*)accessPtr;
            // GetInterface is at index 3 (after IUnknown methods)
            var getInterfacePtr = *((IntPtr*)vtable + 3);
            var getInterface = Marshal.GetDelegateForFunctionPointer<GetDxgiInterfaceDelegate>(getInterfacePtr);
            var hr = getInterface(accessPtr, ref iid, out var result);
            return hr == 0 ? result : IntPtr.Zero;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetDxgiInterfaceDelegate(IntPtr thisPtr, ref Guid iid, out IntPtr ppv);

    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        IntPtr CreateForWindow([In] IntPtr window);
        IntPtr CreateForMonitor([In] IntPtr monitor);
    }

    [ComImport]
    [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDirect3DDxgiInterfaceAccess
    {
        IntPtr GetInterface([In] Guid iid);
    }
}

