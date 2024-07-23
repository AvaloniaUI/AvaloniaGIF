﻿using System;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace Avalonia.Gif
{
    public class GifImage : Control
    {
        public static readonly StyledProperty<string> SourceUriRawProperty =
            AvaloniaProperty.Register<GifImage, string>("SourceUriRaw");

        public static readonly StyledProperty<Uri> SourceUriProperty =
            AvaloniaProperty.Register<GifImage, Uri>("SourceUri");

        public static readonly StyledProperty<Stream> SourceStreamProperty =
            AvaloniaProperty.Register<GifImage, Stream>("SourceStream");

        public static readonly StyledProperty<IterationCount> IterationCountProperty =
            AvaloniaProperty.Register<GifImage, IterationCount>("IterationCount", IterationCount.Infinite);

        private GifInstance gifInstance;

        public static readonly StyledProperty<bool> AutoStartProperty =
            AvaloniaProperty.Register<GifImage, bool>("AutoStart");

        public static readonly StyledProperty<StretchDirection> StretchDirectionProperty =
            AvaloniaProperty.Register<GifImage, StretchDirection>("StretchDirection");

        public static readonly StyledProperty<Stretch> StretchProperty =
            AvaloniaProperty.Register<GifImage, Stretch>("Stretch");

        private RenderTargetBitmap? backingRTB;
        private bool _hasNewSource;
        private object? _newSource;
        private Stopwatch _stopwatch;

        static GifImage()
        {
            SourceUriRawProperty.Changed.AddClassHandler<GifImage>(SourceChanged);
            SourceUriProperty.Changed.AddClassHandler<GifImage>(SourceChanged);
            SourceStreamProperty.Changed.AddClassHandler<GifImage>(SourceChanged);
            IterationCountProperty.Changed.AddClassHandler<GifImage>(IterationCountChanged);
            AutoStartProperty.Changed.AddClassHandler<GifImage>(AutoStartChanged);
            AffectsRender<GifImage>(SourceStreamProperty, SourceUriProperty, SourceUriRawProperty, StretchProperty);
            AffectsArrange<GifImage>(SourceStreamProperty, SourceUriProperty, SourceUriRawProperty, StretchProperty);
            AffectsMeasure<GifImage>(SourceStreamProperty, SourceUriProperty, SourceUriRawProperty, StretchProperty);
        }

        public Uri SourceUri
        {
            get => GetValue(SourceUriProperty);
            set => SetValue(SourceUriProperty, value);
        }

        public Stream SourceStream
        {
            get => GetValue(SourceStreamProperty);
            set => SetValue(SourceStreamProperty, value);
        }

        public IterationCount IterationCount
        {
            get => GetValue(IterationCountProperty);
            set => SetValue(IterationCountProperty, value);
        }

        public bool AutoStart
        {
            get => GetValue(AutoStartProperty);
            set => SetValue(AutoStartProperty, value);
        }

        public StretchDirection StretchDirection
        {
            get => GetValue(StretchDirectionProperty);
            set => SetValue(StretchDirectionProperty, value);
        }

        public Stretch Stretch
        {
            get => GetValue(StretchProperty);
            set => SetValue(StretchProperty, value);
        }

        private static void AutoStartChanged(GifImage? image, AvaloniaPropertyChangedEventArgs e)
        {
        }

        private static void IterationCountChanged(GifImage? image, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue is not IterationCount iterationCount)
                return;

            if (image != null) image.IterationCount = iterationCount;
        }

        public override void Render(DrawingContext context)
        {
            Dispatcher.UIThread.Post(InvalidateMeasure, DispatcherPriority.Background);
            Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Background);

            if (_hasNewSource)
            {
                StopAndDispose();
                gifInstance = new GifInstance(_newSource);
                gifInstance.IterationCount = IterationCount;
                backingRTB = new RenderTargetBitmap(gifInstance.GifPixelSize, new Vector(96, 96));
                _hasNewSource = false;

                _stopwatch ??= new Stopwatch();
                _stopwatch.Reset();


                return;
            }

            if (gifInstance is null || (gifInstance.CurrentCts?.IsCancellationRequested ?? true))
            {
                return;
            }

            if (!_stopwatch.IsRunning)
            {
                _stopwatch.Start();
            }

            var currentFrame = gifInstance.ProcessFrameTime(_stopwatch.Elapsed);

            if (currentFrame != null)
            {
                using var ctx = backingRTB.CreateDrawingContext();
                var ts = new Rect(currentFrame.Size);
                ctx.DrawImage(currentFrame, ts, ts);
            }

            if (backingRTB is not null && Bounds.Width > 0 && Bounds.Height > 0)
            {
                var viewPort = new Rect(Bounds.Size);
                var sourceSize = backingRTB.Size;

                var scale = Stretch.CalculateScaling(Bounds.Size, sourceSize, StretchDirection);
                var scaledSize = sourceSize * scale;
                var destRect = viewPort
                    .CenterRect(new Rect(scaledSize))
                    .Intersect(viewPort);

                var sourceRect = new Rect(sourceSize)
                    .CenterRect(new Rect(destRect.Size / scale));

                var interpolationMode = RenderOptions.GetBitmapInterpolationMode(this);

                context.DrawImage(backingRTB, sourceRect, destRect);
            }
        }

        /// <summary>
        /// Measures the control.
        /// </summary>
        /// <param name="availableSize">The available size.</param>
        /// <returns>The desired size of the control.</returns>
        protected override Size MeasureOverride(Size availableSize)
        {
            var source = backingRTB;
            var result = new Size();

            if (source != null)
            {
                result = Stretch.CalculateSize(availableSize, source.Size, StretchDirection);
            }

            return result;
        }

        /// <inheritdoc/>
        protected override Size ArrangeOverride(Size finalSize)
        {
            var source = backingRTB;

            if (source == null) return new Size();
            
            var sourceSize = source.Size;
            var result = Stretch.CalculateSize(finalSize, sourceSize);
            return result;
        }

        public void StopAndDispose()
        {
            gifInstance?.Dispose();
            backingRTB?.Dispose();
        }

        private static void SourceChanged(GifImage image, AvaloniaPropertyChangedEventArgs e)
        {
            if (image == null)
                return;

            if (e.NewValue is null)
            {
                return;
            }

            image._hasNewSource = true;
            image._newSource = e.NewValue;
            Dispatcher.UIThread.Post(image.InvalidateVisual, DispatcherPriority.Background);
        }
    }
}