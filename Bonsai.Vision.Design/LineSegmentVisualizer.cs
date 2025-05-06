﻿using Bonsai;
using Bonsai.Dag;
using Bonsai.Design;
using Bonsai.Expressions;
using Bonsai.Vision;
using Bonsai.Vision.Design;
using OpenCV.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;

[assembly: TypeVisualizer(typeof(LineSegmentVisualizer), Target = typeof(LineSegment))]
[assembly: TypeVisualizer(typeof(LineSegmentVisualizer), Target = typeof(LineSegment[]))]

namespace Bonsai.Vision.Design
{
    /// <summary>
    /// Provides a type visualizer for a collection of line segments. If the input
    /// is a sequence of images, the visualizer will overlay each line segment on top of
    /// the original image.
    /// </summary>
    public class LineSegmentVisualizer : IplImageVisualizer
    {
        const float DefaultHeight = 480;
        const int DefaultThickness = 2;
        ObjectTextVisualizer textVisualizer;
        IDisposable inputHandle;
        IplImage input;
        IplImage canvas;

        internal static void Draw(IplImage image, object value)
        {
            if (image != null)
            {
                var color = image.Channels == 1 ? Scalar.Real(255) : Scalar.Rgb(255, 0, 0);
                var thickness = DefaultThickness * (int)Math.Ceiling(image.Height / DefaultHeight);
                if (value is IEnumerable<LineSegment> lines)
                {
                    foreach (var line in lines)
                    {
                        CV.Line(image, line.Start, line.End, color, thickness);
                    }
                }
                else
                {
                    var line = (LineSegment)value;
                    CV.Line(image, line.Start, line.End, color, thickness);
                }
            }
        }

        /// <inheritdoc/>
        public override void Show(object value)
        {
            if (textVisualizer != null) textVisualizer.Show(value);
            else
            {
                if (input != null)
                {
                    canvas = IplImageHelper.EnsureColorCopy(canvas, input);
                    Draw(canvas, value);
                    base.Show(canvas);
                }
            }
        }

        /// <inheritdoc/>
        public override void Load(IServiceProvider provider)
        {
            var imageInput = VisualizerHelper.ImageInput(provider);
            if (imageInput != null)
            {
                inputHandle = imageInput.Subscribe(value => input = (IplImage)value);
                base.Load(provider);
            }
            else
            {
                textVisualizer = new ObjectTextVisualizer();
                textVisualizer.Load(provider);
            }
        }

        /// <inheritdoc/>
        public override IObservable<object> Visualize(IObservable<IObservable<object>> source, IServiceProvider provider)
        {
            if (textVisualizer != null) return textVisualizer.Visualize(source, provider);
            else return base.Visualize(source, provider);
        }

        /// <inheritdoc/>
        public override void Unload()
        {
            if (canvas != null)
            {
                canvas.Close();
                canvas = null;
            }

            if (inputHandle != null)
            {
                inputHandle.Dispose();
                inputHandle = null;
            }

            if (textVisualizer != null)
            {
                textVisualizer.Unload();
                textVisualizer = null;
            }
            else base.Unload();
        }
    }
}
