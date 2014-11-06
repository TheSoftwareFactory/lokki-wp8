/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿
///

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using System.Windows.Media;
using FSecure.Utils;
using FSecure.Logging;
using System.Windows.Media.Animation;

namespace FSecure.Lokki.Controls
{
    public enum PointerHint
    {
        None,
        Top,
        Bottom
    }
     
    /// <summary>
    /// Bubble control for tips, etc
    /// </summary>
    public partial class BubbleControl : UserControl
    {

        /// <summary>
        /// Triggered after close animation.
        /// </summary>
        public event EventHandler Dismissed;
        
        /// <summary>
        /// Triggered when dismissing animation is started
        /// </summary>
        public event EventHandler Dismissing;

        /// Used to detect if dismissal has already been triggered
        Storyboard DismissAnimationStory;
 
        private PointerHint _PointerPosition;
        public PointerHint PointerPosition
        {
            get
            {
                return _PointerPosition;
            }
            set
            {
                _PointerPosition = value;
                if (value == PointerHint.None)
                {
                    PointerRectangle.Visibility = Visibility.Collapsed;
                }
                else
                {
                    PointerRectangle.Visibility = Visibility.Visible;
                }
            }
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
          "Text",
          typeof(string),
          typeof(BubbleControl),
          new PropertyMetadata(null)
        );


        /// <summary>
        /// The text shown on bubble
        /// </summary>
        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty ChildProperty = DependencyProperty.Register(
          "Child",
          typeof(FrameworkElement),
          typeof(BubbleControl),
          new PropertyMetadata(null)
        );

        /// <summary>
        /// The content of bubble
        /// </summary>
        public FrameworkElement Child
        {
            get { return (FrameworkElement)GetValue(ChildProperty); }
            set { SetValue(ChildProperty, value); }
        }

        /// <summary>
        /// Flag to trigger animation only after control is properly loaded
        /// </summary>
        private bool IsLoaded = false;

        private WeakReference<FrameworkElement> _PointedElement;

        public bool TapOutsideCloses { get; set; }

        /// <summary>
        /// The element the bubble is pointing at.
        /// </summary>
        public FrameworkElement PointedElement
        {
            get
            {
                FrameworkElement targetElement;
                if (_PointedElement == null || !_PointedElement.TryGetTarget(out targetElement))
                {
                    return null;
                }
                return targetElement;
            }
            set
            {
                _PointedElement = new WeakReference<FrameworkElement>(value);

                if (IsLoaded && value != null)
                {
                    Animate();
                }
            }
        }

        Point? _PointAt;
        /// <summary>
        /// Returns the position of pointer. Uses position of PointedElement if set,
        /// otherwise uses the given value.
        /// </summary>
        public Point PointAt
        {
            get
            {
                if (_PointAt.HasValue)
                {
                    return _PointAt.Value;
                }
                else if (_PointedElement != null)
                {
                    var targetElement = PointedElement;
                    var p = new Point(targetElement.RenderSize.Width / 2, 0);
                    return targetElement.TransformToVisual(this.LayoutRoot).Transform(p);
                }

                return new Point(0, 0);
            }
            set
            {
                PointedElement = null;
                _PointAt = value;
            }
        }

        public BubbleControl()
        {
            InitializeComponent();
            this.Text = "";

            this.DataContext = this;

            this.BubbleContainer.RenderTransform = new TranslateTransform();
            this.PointerRectangle.RenderTransform = new TranslateTransform();

            var bg = this.LayoutRoot.Background as SolidColorBrush;
            bg.Opacity = 0.01; // a bit transparent to catch tap

            this.Loaded += BubbleControl_Loaded;
            this.SizeChanged += BubbleControl_SizeChanged;
        }

        void BubbleControl_LayoutUpdated(object sender, EventArgs e)
        {
            this.LayoutUpdated -= BubbleControl_LayoutUpdated;
            Animate();
        }

        /// <summary>
        /// Update if size\orientation is changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void BubbleControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.LayoutRoot.Width = e.NewSize.Width;
            this.LayoutRoot.Height = e.NewSize.Height;

            this.LayoutUpdated += BubbleControl_LayoutUpdated;
            this.UpdateLayout();
        }

        void BubbleControl_Loaded(object sender, RoutedEventArgs e)
        {
            IsLoaded = true;
            this.Loaded -= BubbleControl_Loaded;

            if ((this.PointedElement != null || this.PointAt != null)
                && Visibility == Visibility.Visible)
            {
                Animate();
            }
        }

        public void Show()
        {
            Visibility = Visibility.Visible;
            BubbleContainer.UpdateLayout();

            // Move away from view to avoid flashing before positioning in Animate
            var pointerTransform = this.PointerRectangle.RenderTransform as TranslateTransform;
            var bubbleTransform = this.BubbleContainer.RenderTransform as TranslateTransform;

            bubbleTransform.Y = -this.LayoutRoot.ActualHeight;
            pointerTransform.Y = bubbleTransform.Y;

            if (IsLoaded)
            {
                Animate();
            }
        }

        public void Dismiss()
        {
            if (DismissAnimationStory != null)
            {
                FSLog.Debug("Already dismissing");
                return;
            }

            if (Dismissing != null)
            {
                Dismissing(this, new EventArgs());
            }

            // Hide the bubble control with fade
            DismissAnimationStory = FSAnim.Fade(this.LayoutRoot,
                to: 0,
                start: true,
                onCompletion: () =>
                {
                    this.Visibility = Visibility.Collapsed;
                    IsLoaded = false;

                    if (Dismissed != null)
                    {
                        Dismissed(this, new EventArgs());
                    }
                    DismissAnimationStory = null;
                });
        }

        private void Animate()
        {
            FrameworkElement targetElement = this.PointedElement;
            if (targetElement == null && !_PointAt.HasValue || Visibility == Visibility.Collapsed)
            {
                FSLog.Debug("No element or coordinate set");
                return;
            }

            // Find position for the bubble control
            var position = this.PointAt;//targetElement.TransformToVisual(this.LayoutRoot).Transform(new Point());

            var pointerTransform = this.PointerRectangle.RenderTransform as TranslateTransform;
            var bubbleTransform = this.BubbleContainer.RenderTransform as TranslateTransform;

            // Set horizontal target position to center of target element
            double targetX = position.X;

            // pointer at bottom            
            if (PointerPosition == PointerHint.Bottom)
            {
                bubbleTransform.Y = position.Y
                    - this.BubbleContainer.ActualHeight;
                //- this.PointerRectangle.ActualHeight / 2
                ;

                if (targetElement != null)
                {
                    bubbleTransform.Y -= targetElement.ActualHeight;
                }

                pointerTransform.Y = bubbleTransform.Y + BubbleContainer.ActualHeight;
            }
            else
            {
                bubbleTransform.Y = position.Y + this.PointerRectangle.ActualHeight * 0.40;
                pointerTransform.Y = bubbleTransform.Y;
            }

            pointerTransform.X = targetX;

        }

        /// <summary>
        /// Close the control with fade animation
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LayoutRoot_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            if (TapOutsideCloses)
            {
                Dismiss();
            }
        }

    }
}
