﻿using System;
using System.Drawing;
using System.Drawing.Imaging;
using Application.Interfaces;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace WFGameCapture
{
    public class DShowCapture : IDisposable, IGameCapture
    {
        private readonly VideoCapture _capture;
        private readonly Mat _mat;
        private readonly Bitmap _bitmap;

        public DShowCapture(int width, int height)
        {
            _capture = new VideoCapture(CaptureDevice.DShow)
            {
                FrameWidth = width,
                FrameHeight = height
            };

            _mat = new Mat();
            _bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        }

        public Bitmap GetFrame()
        {
            _capture.Read(_mat);
            _mat.ToBitmap(_bitmap);
            return _bitmap;
        }

        public void Dispose()
        {
            _capture?.Dispose();
            _mat?.Dispose();
            _bitmap?.Dispose();
        }

        public string GetTradeChatImage(string outputPath)
        {
            var frame = GetFrame();
            frame.Save(outputPath);
            return outputPath;
        }
    }
}