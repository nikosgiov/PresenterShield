using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Device = SharpDX.Direct3D11.Device;

namespace PresenterShield.Services
{
    public class ScreenMirrorService : IDisposable
    {
        private readonly object _lock = new object();
        private Device? _device;
        private OutputDuplication? _deskDupe;
        private Texture2D? _desktopImageTexture;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _captureTask;

        public event Action<BitmapSource>? FrameCaptured;
        public event Action<string>? MirroringError;
        
        public bool IsMirroring { get; private set; }

        public ScreenMirrorService()
        {
        }

        public void StartMirroring(int displayIndex = 0)
        {
            lock (_lock)
            {
                if (IsMirroring) return;

                try
                {
                    InitializeDuplication(displayIndex);
                    
                    _cancellationTokenSource = new CancellationTokenSource();
                    IsMirroring = true;
                    
                    _captureTask = Task.Run(() => CaptureLoop(_cancellationTokenSource.Token));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to start screen mirroring: {ex.Message}");
                    MirroringError?.Invoke($"Failed to start mirroring: {ex.Message}");
                    StopMirroring();
                }
            }
        }

        public void StopMirroring()
        {
            lock (_lock)
            {
                if (!IsMirroring) return;

                IsMirroring = false;
                
                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Cancel();
                }

                try
                {
                    // Wait for the task to complete gracefully
                    if (_captureTask != null && !_captureTask.IsCompleted)
                    {
                        _captureTask.Wait(1000);
                    }
                }
                catch { }

                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;

                CleanupDx();
            }
        }

        private void InitializeDuplication(int displayIndex)
        {
            CleanupDx();

            using (var factory = new Factory1())
            using (var adapter = factory.GetAdapter1(0))
            {
                _device = new Device(adapter);
                
                using (var output = adapter.GetOutput(displayIndex))
                using (var output1 = output.QueryInterface<Output1>())
                {
                    _deskDupe = output1.DuplicateOutput(_device);

                    var textureDesc = new Texture2DDescription
                    {
                        CpuAccessFlags = CpuAccessFlags.Read,
                        BindFlags = BindFlags.None,
                        Format = Format.B8G8R8A8_UNorm,
                        Width = output.Description.DesktopBounds.Right - output.Description.DesktopBounds.Left,
                        Height = output.Description.DesktopBounds.Bottom - output.Description.DesktopBounds.Top,
                        OptionFlags = ResourceOptionFlags.None,
                        MipLevels = 1,
                        ArraySize = 1,
                        SampleDescription = { Count = 1, Quality = 0 },
                        Usage = ResourceUsage.Staging
                    };

                    _desktopImageTexture = new Texture2D(_device, textureDesc);
                }
            }
        }

        private void CaptureLoop(CancellationToken cancellationToken)
        {
            // Target roughly 30 FPS to reduce CPU/GPU load (1000ms / 30 = ~33ms)
            const int targetFrameTimeMs = 33;
            var stopwatch = new Stopwatch();

            while (!cancellationToken.IsCancellationRequested)
            {
                stopwatch.Restart();

                try
                {
                    SharpDX.DXGI.Resource? screenResource = null;
                    OutputDuplicateFrameInformation frameInfo;

                    lock (_lock)
                    {
                        if (_deskDupe == null || _device == null || _desktopImageTexture == null || !IsMirroring)
                            break;

                        SharpDX.Result result = _deskDupe.TryAcquireNextFrame(50, out frameInfo, out screenResource);
                        
                        if (result.Success)
                        {
                            try 
                            {
                                if (screenResource != null)
                                {
                                    using (var screenTexture2D = screenResource.QueryInterface<Texture2D>())
                                    {
                                        _device.ImmediateContext.CopyResource(screenTexture2D, _desktopImageTexture);
                                    }

                                    var mapSource = _device.ImmediateContext.MapSubresource(_desktopImageTexture, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);
                                    
                                    try
                                    {
                                        int width = _desktopImageTexture.Description.Width;
                                        int height = _desktopImageTexture.Description.Height;
                                        
                                        var bitmapSource = CreateBitmapSourceFromRaw(mapSource.DataPointer, width, height, mapSource.RowPitch);
                                        
                                        if (bitmapSource != null && !cancellationToken.IsCancellationRequested)
                                        {
                                            bitmapSource.Freeze(); 
                                            FrameCaptured?.Invoke(bitmapSource);
                                        }
                                    }
                                    finally
                                    {
                                        _device.ImmediateContext.UnmapSubresource(_desktopImageTexture, 0);
                                    }
                                }
                            }
                            finally
                            {
                                screenResource?.Dispose();
                                _deskDupe.ReleaseFrame();
                            }
                        }
                        else if (result != SharpDX.DXGI.ResultCode.WaitTimeout)
                        {
                            if (result == SharpDX.DXGI.ResultCode.AccessLost)
                            {
                                Debug.WriteLine("Desktop duplication access lost.");
                                MirroringError?.Invoke("Remote desktop session ended or display settings changed.");
                            }
                            else
                            {
                                Debug.WriteLine($"Error acquiring frame: {result}");
                            }
                            break; 
                        }
                    }

                    // Throttle frame rate
                    int elapsed = (int)stopwatch.ElapsedMilliseconds;
                    if (elapsed < targetFrameTimeMs)
                    {
                        Thread.Sleep(targetFrameTimeMs - elapsed);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in capture loop: {ex.Message}");
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Thread.Sleep(500);
                    }
                }
            }

            lock (_lock)
            {
                IsMirroring = false;
            }
        }
        
        private BitmapSource? CreateBitmapSourceFromRaw(IntPtr dataPtr, int width, int height, int rowPitch)
        {
            try
            {
                var format = System.Windows.Media.PixelFormats.Bgra32;
                
                return BitmapSource.Create(
                    width,
                    height,
                    96, 
                    96, 
                    format,
                    null,
                    dataPtr,
                    height * rowPitch,
                    rowPitch);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to create BitmapSource: {ex.Message}");
                return null;
            }
        }

        private void CleanupDx()
        {
            // Note: Caller should hold _lock if calling from outside Start/Stop
            _deskDupe?.Dispose();
            _deskDupe = null;
            
            _desktopImageTexture?.Dispose();
            _desktopImageTexture = null;
            
            _device?.Dispose();
            _device = null;
        }

        public void Dispose()
        {
            StopMirroring();
        }
    }
}
