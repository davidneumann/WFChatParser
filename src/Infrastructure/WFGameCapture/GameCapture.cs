/// This code is heavily influenced by Justin Harper (https://github.com/GigaPatches) and 
/// the following sample: https://github.com/sharpdx/SharpDX-Samples/blob/master/Desktop/Direct3D11.1/ScreenCapture/Program.cs

using Application.Interfaces;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace WFGameCapture
{
    public class GameCapture : IGameCapture
    {
        private OutputDuplication _duplicateOutput;
        private Texture2D _screenTexture;

        // For double buffering bitmaps (may not be required depending in your use case)
        private Bitmap _currentBitmap;
        private Bitmap _previousBitmap;

        public Rectangle DisplayBounds { get; private set; } = Rectangle.Empty;
        public Rectangle ClippingBounds { get; set; } = Rectangle.Empty;

        public Adapter1 Adapter { get; private set; }
        public Device Device { get; private set; }

        public GameCapture()
        {
            var factory = new Factory1();

            // change index to adjust which GPU to use
            // factory.Adapters1 will return an array of adapters
            Adapter = factory.GetAdapter1(0);
            Device = new Device(Adapter);

            // change index to adjust which monitor to use
            // adapter.Outputs lists possible outputs
            // adapter.GetOutputCount() number of outputs
            var output = Adapter.GetOutput(0);

            var output1 = output.QueryInterface<Output1>();
            var bounds = output.Description.DesktopBounds;
            DisplayBounds = Rectangle.FromLTRB(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);

            _duplicateOutput = output1.DuplicateOutput(Device);

            GetOutputAsBitmap().Dispose();
        }

        public Texture2D GetOutput(int timeout = 10)
        {
            try
            {
                var res = _duplicateOutput.TryAcquireNextFrame(timeout, out var frameInfoRef, out var screenResource);
                if (screenResource == null)
                    return _screenTexture;
                
                var clip = ClippingBounds == Rectangle.Empty ? DisplayBounds : ClippingBounds;

                if (_screenTexture == null || clip.Width != _screenTexture.Description.Width ||
                    clip.Height != _screenTexture.Description.Height)
                {
                    _screenTexture?.Dispose();
                    _screenTexture = CreateTexture(clip.Width, clip.Height);
                }

                // Copy the screen resource to our texture
                using (var tex = screenResource.QueryInterface<Texture2D>())
                {
                    Device.ImmediateContext.CopySubresourceRegion(tex, 0,
                        new ResourceRegion(clip.Left, clip.Top, 0, clip.Right, clip.Bottom, 1),
                        _screenTexture, 0);
                }

                screenResource.Dispose();
                _duplicateOutput.ReleaseFrame();

                // return it
                return _screenTexture;
            }
            catch (SharpDXException e) when (e.ResultCode == SharpDX.DXGI.ResultCode.WaitTimeout)
            {
                // Timed out getting a new frame, return previous frame
                return _screenTexture;
            }
        }

        public Bitmap GetOutputAsBitmap(int timeout = 10)
        {
            var texture = GetOutput(timeout);

            var clip = ClippingBounds == Rectangle.Empty ? DisplayBounds : ClippingBounds;

            var bitmap = _currentBitmap;

            // create bitmap
            try
            {
                if (bitmap == null || bitmap.Width != clip.Width || bitmap.Height != clip.Height)
                {
                    bitmap?.Dispose();
                    bitmap = new Bitmap(clip.Width, clip.Height, PixelFormat.Format32bppArgb);
                }
            }
            catch
            {
                bitmap?.Dispose();
                bitmap = new Bitmap(clip.Width, clip.Height, PixelFormat.Format32bppArgb);
            }

            var bounds = new Rectangle(0, 0, clip.Width, clip.Height);

            // Allows CPU access to a Texture2D
            var mapSource = Device.ImmediateContext.MapSubresource(texture, 0, MapMode.Read, MapFlags.None);

            var mapDest = bitmap.LockBits(bounds, ImageLockMode.WriteOnly, bitmap.PixelFormat);
            var sourcePtr = mapSource.DataPointer;
            var destPtr = mapDest.Scan0;
            for (var y = 0; y < clip.Height; y++)
            {
                Utilities.CopyMemory(destPtr, sourcePtr, clip.Width * 4);

                sourcePtr = IntPtr.Add(sourcePtr, mapSource.RowPitch);
                destPtr = IntPtr.Add(destPtr, mapDest.Stride);
            }

            // Unlock resources
            bitmap.UnlockBits(mapDest);
            Device.ImmediateContext.UnmapSubresource(texture, 0);

            _currentBitmap = _previousBitmap;
            _previousBitmap = bitmap;

            return bitmap;
        }

        private Texture2D CreateTexture(int width, int height)
        {
            // This texture is readable by the CPU, allowing us to grab its pixels
            var textureDesc = new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.Read,
                BindFlags = BindFlags.None,
                Format = Format.B8G8R8A8_UNorm,
                Width = width,
                Height = height,
                OptionFlags = ResourceOptionFlags.None,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Staging
            };

            return new Texture2D(Device, textureDesc);
        }

        public string GetTradeChatImage()
        {
            var image = GetOutputAsBitmap(100);
            image.Save("capture.png");
            return System.IO.Path.Combine(Environment.CurrentDirectory, "capture.png");
        }

        public string Debug()
        {
            return Adapter.Description.Description;
        }
    }
}
