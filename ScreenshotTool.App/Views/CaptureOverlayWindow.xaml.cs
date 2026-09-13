using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenshotTool.Core.Capture;
using ScreenshotTool.Core.Win32;

namespace ScreenshotTool.App.Views;

public partial class CaptureOverlayWindow : Window
{
    private readonly VirtualScreenBounds _bounds;
    private readonly BitmapSource _frozenScreen;
    private readonly BitmapSource _pixelSource;
    private readonly CaptureSelectionController _selection = new();
    private readonly WindowDetectionService _windowDetection = new();
    private CaptureRegion? _selectionBeforeDrag;
    private CaptureRegion? _windowCandidateBeforeDrag;
    private CaptureRegion? _hoveredWindow;
    private HwndSource? _windowSource;
    private SelectionDragMode _dragMode;
    private ResizeEdges _resizeEdges;
    private CaptureRegion _dragStartRegion;
    private (int X, int Y) _dragStartPoint;
    private Rect _selectionViewRect = Rect.Empty;

    public CaptureOverlayWindow(BitmapSource frozenScreen, VirtualScreenBounds bounds)
    {
        InitializeComponent();
        _frozenScreen = frozenScreen;
        _pixelSource = new FormatConvertedBitmap(frozenScreen, PixelFormats.Bgra32, null, 0);
        _pixelSource.Freeze();
        FrozenDesktop.Source = frozenScreen;
        _bounds = bounds;
        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
        SizeChanged += (_, _) => RefreshDimLayer();
        Loaded += (_, _) =>
        {
            PositionOverScreen();
            RefreshDimLayer();
        };
    }

    public event EventHandler<CaptureCompletedEventArgs>? CaptureCompleted;
    public event EventHandler? CaptureCancelled;

    public CaptureRegion? SelectedRegion { get; private set; }

    private void OnSourceInitialized(object? sender, EventArgs eventArgs)
    {
        _windowSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _windowSource.AddHook(WindowMessageHook);
        var dpi = VisualTreeHelper.GetDpi(this);
        Left = _bounds.X / dpi.DpiScaleX;
        Top = _bounds.Y / dpi.DpiScaleY;
        Width = _bounds.Width / dpi.DpiScaleX;
        Height = _bounds.Height / dpi.DpiScaleY;
        PositionOverScreen();
    }

    private void OnClosed(object? sender, EventArgs eventArgs)
    {
        _windowSource?.RemoveHook(WindowMessageHook);
        _windowSource = null;
    }

    private IntPtr WindowMessageHook(IntPtr windowHandle, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == NativeMethods.WmWindowPosChanging && lParam != IntPtr.Zero)
        {
            var position = Marshal.PtrToStructure<WindowPosition>(lParam);
            position.X = _bounds.X;
            position.Y = _bounds.Y;
            position.Width = _bounds.Width;
            position.Height = _bounds.Height;
            position.Flags &= ~(NativeMethods.SwpNoMove | NativeMethods.SwpNoSize);
            Marshal.StructureToPtr(position, lParam, fDeleteOld: false);
        }

        return IntPtr.Zero;
    }

    private void PositionOverScreen()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (!NativeMethods.SetWindowPos(handle, new IntPtr(-1), _bounds.X, _bounds.Y, _bounds.Width, _bounds.Height, NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow))
        {
            NativeMethods.ThrowLastWin32Error("无法定位截图覆盖层。");
        }

        if (!NativeMethods.GetWindowRect(handle, out var rectangle) ||
            rectangle.Left != _bounds.X || rectangle.Top != _bounds.Y ||
            rectangle.Right - rectangle.Left != _bounds.Width || rectangle.Bottom - rectangle.Top != _bounds.Height)
        {
            throw new InvalidOperationException("截图覆盖层未能覆盖当前显示器。");
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        var viewPoint = eventArgs.GetPosition(this);
        var point = ToScreenPixels(viewPoint);
        if (eventArgs.ClickCount >= 2)
        {
            CaptureCancelled?.Invoke(this, EventArgs.Empty);
            eventArgs.Handled = true;
            return;
        }

        CaptureMouse();
        SelectionToolbar.Visibility = Visibility.Collapsed;
        if (SelectedRegion is { } selected)
        {
            _resizeEdges = HitTestResizeEdges(viewPoint);
            if (_resizeEdges != ResizeEdges.None || _selectionViewRect.Contains(viewPoint))
            {
                _dragMode = _resizeEdges == ResizeEdges.None ? SelectionDragMode.Move : SelectionDragMode.Resize;
                _dragStartRegion = selected.ClampTo(_bounds);
                _dragStartPoint = point;
                Cursor = CursorFor(_resizeEdges, inside: true);
                eventArgs.Handled = true;
                return;
            }
        }

        _dragMode = SelectionDragMode.Create;
        _selectionBeforeDrag = SelectedRegion;
        _windowCandidateBeforeDrag = _hoveredWindow;
        _selection.Begin(point.X, point.Y);
        Cursor = System.Windows.Input.Cursors.Cross;
    }

    private void OnSelectionMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.ClickCount >= 2)
        {
            CaptureCancelled?.Invoke(this, EventArgs.Empty);
            eventArgs.Handled = true;
            return;
        }
        if (SelectedRegion is not { } selected) return;

        var viewPoint = eventArgs.GetPosition(this);
        _resizeEdges = HitTestResizeEdges(viewPoint);
        _dragMode = _resizeEdges == ResizeEdges.None ? SelectionDragMode.Move : SelectionDragMode.Resize;
        _dragStartRegion = selected.ClampTo(_bounds);
        _dragStartPoint = ToScreenPixels(viewPoint);
        _selectionBeforeDrag = null;
        _windowCandidateBeforeDrag = null;
        SelectionToolbar.Visibility = Visibility.Collapsed;
        CaptureMouse();
        Cursor = CursorFor(_resizeEdges, inside: true);
        eventArgs.Handled = true;
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs eventArgs)
    {
        var viewPoint = eventArgs.GetPosition(this);
        UpdateMagnifier(viewPoint);
        if (!IsMouseCaptured)
        {
            if (SelectedRegion is null)
            {
                _hoveredWindow = _windowDetection.FindWindowAt(ToScreenPixels(viewPoint).X, ToScreenPixels(viewPoint).Y)?.ClampTo(_bounds);
                if (_hoveredWindow is { } candidate) UpdateVisual(candidate);
                else ResetSelectionVisual();
            }
            else
            {
                Cursor = CursorFor(HitTestResizeEdges(viewPoint), _selectionViewRect.Contains(viewPoint));
            }
            return;
        }

        var point = ToScreenPixels(viewPoint);
        if (_dragMode is SelectionDragMode.Move or SelectionDragMode.Resize)
        {
            SelectedRegion = _dragMode == SelectionDragMode.Move ? MoveSelection(point) : ResizeSelection(point);
            UpdateVisual(SelectedRegion.Value);
            return;
        }

        var region = _selection.Update(point.X, point.Y);
        if (!region.IsEmpty)
        {
            SelectedRegion = null;
            UpdateVisual(region);
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs eventArgs)
    {
        if (!IsMouseCaptured)
        {
            return;
        }

        ReleaseMouseCapture();
        var point = ToScreenPixels(eventArgs.GetPosition(this));
        if (_dragMode is SelectionDragMode.Move or SelectionDragMode.Resize)
        {
            if (SelectedRegion is { } adjusted)
            {
                UpdateVisual(adjusted);
                ShowSelectionToolbar(adjusted);
            }
            _dragMode = SelectionDragMode.None;
            _resizeEdges = ResizeEdges.None;
            Cursor = System.Windows.Input.Cursors.SizeAll;
            eventArgs.Handled = true;
            return;
        }

        if (_selection.Complete(point.X, point.Y) is { } region)
        {
            SelectedRegion = region.ClampTo(_bounds);
            UpdateVisual(SelectedRegion.Value);
            ShowSelectionToolbar(SelectedRegion.Value);
        }
        else if (_selectionBeforeDrag is { } previous)
        {
            SelectedRegion = previous.ClampTo(_bounds);
            UpdateVisual(SelectedRegion.Value);
            ShowSelectionToolbar(SelectedRegion.Value);
        }
        else if (_windowCandidateBeforeDrag is { } candidate)
        {
            SelectedRegion = candidate.ClampTo(_bounds);
            UpdateVisual(SelectedRegion.Value);
            ShowSelectionToolbar(SelectedRegion.Value);
        }
        else
        {
            ResetSelectionVisual();
        }

        _selectionBeforeDrag = null;
        _windowCandidateBeforeDrag = null;
        _dragMode = SelectionDragMode.None;
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Escape)
        {
            CaptureCancelled?.Invoke(this, EventArgs.Empty);
            eventArgs.Handled = true;
        }
        else if (eventArgs.Key == Key.C && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && SelectedRegion is { } region)
        {
            CaptureCompleted?.Invoke(this, new CaptureCompletedEventArgs(CaptureAction.Copy, region));
            eventArgs.Handled = true;
        }
        else if (eventArgs.Key == Key.Enter && SelectedRegion is { } enterRegion)
        {
            CaptureCompleted?.Invoke(this, new CaptureCompletedEventArgs(CaptureAction.Copy, enterRegion));
            eventArgs.Handled = true;
        }
        else if (SelectedRegion is { } selected && eventArgs.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            var amount = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
            var dx = eventArgs.Key == Key.Left ? -amount : eventArgs.Key == Key.Right ? amount : 0;
            var dy = eventArgs.Key == Key.Up ? -amount : eventArgs.Key == Key.Down ? amount : 0;
            SelectedRegion = (selected with { X = selected.X + dx, Y = selected.Y + dy }).ClampTo(_bounds);
            UpdateVisual(SelectedRegion.Value);
            ShowSelectionToolbar(SelectedRegion.Value);
            eventArgs.Handled = true;
        }
    }

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.ClickCount >= 2)
        {
            CaptureCancelled?.Invoke(this, EventArgs.Empty);
        }
        eventArgs.Handled = true;
    }

    private void OnToolbarOcrClicked(object sender, RoutedEventArgs e) => CompleteSelection(CaptureAction.Ocr);
    private void OnToolbarTranslateClicked(object sender, RoutedEventArgs e) => CompleteSelection(CaptureAction.Translate);
    private void OnToolbarPinClicked(object sender, RoutedEventArgs e) => CompleteSelection(CaptureAction.Pin);
    private void OnToolbarCopyClicked(object sender, RoutedEventArgs e) => CompleteSelection(CaptureAction.Copy);
    private void OnToolbarSaveClicked(object sender, RoutedEventArgs e) => CompleteSelection(CaptureAction.Save);

    private void CompleteSelection(CaptureAction action)
    {
        if (SelectedRegion is { } region)
            CaptureCompleted?.Invoke(this, new CaptureCompletedEventArgs(action, region));
    }

    private (int X, int Y) ToScreenPixels(System.Windows.Point point)
    {
        var x = _bounds.X + (int)Math.Round(point.X / ActualWidth * _bounds.Width);
        var y = _bounds.Y + (int)Math.Round(point.Y / ActualHeight * _bounds.Height);
        return (x, y);
    }

    private void RefreshDimLayer()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        if (SelectedRegion is { } region)
        {
            UpdateVisual(region);
            ShowSelectionToolbar(region);
        }
        else
        {
            DimLayer.Data = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight));
        }
    }

    private void ResetSelectionVisual()
    {
        SelectionBorder.Visibility = Visibility.Collapsed;
        SizeTip.Visibility = Visibility.Collapsed;
        SelectionToolbar.Visibility = Visibility.Collapsed;
        SetResizeHandlesVisibility(Visibility.Collapsed);
        _selectionViewRect = Rect.Empty;
        DimLayer.Data = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight));
    }

    private void UpdateVisual(CaptureRegion region)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var x = (region.X - _bounds.X) / (double)_bounds.Width * ActualWidth;
        var y = (region.Y - _bounds.Y) / (double)_bounds.Height * ActualHeight;
        var width = region.Width / (double)_bounds.Width * ActualWidth;
        var height = region.Height / (double)_bounds.Height * ActualHeight;
        _selectionViewRect = new Rect(x, y, width, height);
        var committed = SelectedRegion is { } selected && selected.Equals(region);
        SelectionBorder.Visibility = Visibility.Visible;
        SelectionBorder.IsHitTestVisible = committed;
        SelectionBorder.Width = width;
        SelectionBorder.Height = height;
        Canvas.SetLeft(SelectionBorder, x);
        Canvas.SetTop(SelectionBorder, y);
        if (committed) PositionResizeHandles(x, y, width, height);
        else SetResizeHandlesVisibility(Visibility.Collapsed);
        SizeText.Text = $"{region.Width} × {region.Height}";
        SizeTip.Visibility = Visibility.Visible;
        SizeTip.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(SizeTip, x);
        Canvas.SetTop(SizeTip, Math.Max(0, y - SizeTip.DesiredSize.Height - 6));
        DimLayer.Data = new CombinedGeometry(
            GeometryCombineMode.Exclude,
            new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)),
            new RectangleGeometry(new Rect(x, y, width, height)));
    }

    private void PositionResizeHandles(double x, double y, double width, double height)
    {
        SetResizeHandlesVisibility(Visibility.Visible);
        PositionHandle(TopLeftHandle, x, y);
        PositionHandle(TopRightHandle, x + width, y);
        PositionHandle(BottomLeftHandle, x, y + height);
        PositionHandle(BottomRightHandle, x + width, y + height);
        System.Windows.Controls.Panel.SetZIndex(TopLeftHandle, 10);
        System.Windows.Controls.Panel.SetZIndex(TopRightHandle, 10);
        System.Windows.Controls.Panel.SetZIndex(BottomLeftHandle, 10);
        System.Windows.Controls.Panel.SetZIndex(BottomRightHandle, 10);
    }

    private static void PositionHandle(FrameworkElement handle, double x, double y)
    {
        Canvas.SetLeft(handle, x - handle.Width / 2);
        Canvas.SetTop(handle, y - handle.Height / 2);
    }

    private void SetResizeHandlesVisibility(Visibility visibility)
    {
        TopLeftHandle.Visibility = visibility;
        TopRightHandle.Visibility = visibility;
        BottomLeftHandle.Visibility = visibility;
        BottomRightHandle.Visibility = visibility;
    }

    private void ShowSelectionToolbar(CaptureRegion region)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        var x = (region.X - _bounds.X) / (double)_bounds.Width * ActualWidth;
        var y = (region.Y - _bounds.Y) / (double)_bounds.Height * ActualHeight;
        var width = region.Width / (double)_bounds.Width * ActualWidth;
        var height = region.Height / (double)_bounds.Height * ActualHeight;
        _selectionViewRect = new Rect(x, y, width, height);
        SelectionToolbar.Visibility = Visibility.Visible;
        SelectionToolbar.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        var toolbarWidth = SelectionToolbar.DesiredSize.Width;
        var toolbarHeight = SelectionToolbar.DesiredSize.Height;
        var left = Math.Clamp(x + width - toolbarWidth, 0, Math.Max(0, ActualWidth - toolbarWidth));
        const double gap = 8;
        var top = y - toolbarHeight - gap;
        if (top < 0)
            top = Math.Min(Math.Max(0, ActualHeight - toolbarHeight), y + height + gap);
        Canvas.SetLeft(SelectionToolbar, left);
        Canvas.SetTop(SelectionToolbar, top);
        System.Windows.Controls.Panel.SetZIndex(SelectionToolbar, 20);
    }

    private void UpdateMagnifier(System.Windows.Point viewPoint)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var screenPoint = ToScreenPixels(viewPoint);
        var pixelX = Math.Clamp(screenPoint.X - _bounds.X, 0, _frozenScreen.PixelWidth - 1);
        var pixelY = Math.Clamp(screenPoint.Y - _bounds.Y, 0, _frozenScreen.PixelHeight - 1);
        const int sampleWidth = 21;
        const int sampleHeight = 14;
        var cropX = Math.Clamp(pixelX - sampleWidth / 2, 0, Math.Max(0, _frozenScreen.PixelWidth - sampleWidth));
        var cropY = Math.Clamp(pixelY - sampleHeight / 2, 0, Math.Max(0, _frozenScreen.PixelHeight - sampleHeight));
        var cropWidth = Math.Min(sampleWidth, _frozenScreen.PixelWidth);
        var cropHeight = Math.Min(sampleHeight, _frozenScreen.PixelHeight);
        MagnifierImage.Source = new CroppedBitmap(_frozenScreen, new Int32Rect(cropX, cropY, cropWidth, cropHeight));

        var pixel = new byte[4];
        _pixelSource.CopyPixels(new Int32Rect(pixelX, pixelY, 1, 1), pixel, 4, 0);
        var color = System.Windows.Media.Color.FromRgb(pixel[2], pixel[1], pixel[0]);
        CoordinateText.Text = $"({screenPoint.X}, {screenPoint.Y})";
        RgbText.Text = $"RGB: {color.R}, {color.G}, {color.B}";
        ColorSwatch.Background = new SolidColorBrush(color);

        HorizontalGuide.Visibility = Visibility.Visible;
        HorizontalGuide.X1 = 0;
        HorizontalGuide.X2 = ActualWidth;
        HorizontalGuide.Y1 = viewPoint.Y;
        HorizontalGuide.Y2 = viewPoint.Y;
        VerticalGuide.Visibility = Visibility.Visible;
        VerticalGuide.X1 = viewPoint.X;
        VerticalGuide.X2 = viewPoint.X;
        VerticalGuide.Y1 = 0;
        VerticalGuide.Y2 = ActualHeight;

        MagnifierPanel.Visibility = Visibility.Visible;
        MagnifierPanel.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        const double offset = 18;
        var left = viewPoint.X + offset;
        var top = viewPoint.Y + offset;
        if (left + MagnifierPanel.DesiredSize.Width > ActualWidth)
        {
            left = viewPoint.X - MagnifierPanel.DesiredSize.Width - offset;
        }

        if (top + MagnifierPanel.DesiredSize.Height > ActualHeight)
        {
            top = viewPoint.Y - MagnifierPanel.DesiredSize.Height - offset;
        }

        Canvas.SetLeft(MagnifierPanel, Math.Max(0, left));
        Canvas.SetTop(MagnifierPanel, Math.Max(0, top));
    }

    private ResizeEdges HitTestResizeEdges(System.Windows.Point point)
    {
        const double tolerance = 9;
        if (_selectionViewRect.IsEmpty ||
            point.X < _selectionViewRect.Left - tolerance || point.X > _selectionViewRect.Right + tolerance ||
            point.Y < _selectionViewRect.Top - tolerance || point.Y > _selectionViewRect.Bottom + tolerance)
            return ResizeEdges.None;

        var edges = ResizeEdges.None;
        if (Math.Abs(point.X - _selectionViewRect.Left) <= tolerance) edges |= ResizeEdges.Left;
        else if (Math.Abs(point.X - _selectionViewRect.Right) <= tolerance) edges |= ResizeEdges.Right;
        if (Math.Abs(point.Y - _selectionViewRect.Top) <= tolerance) edges |= ResizeEdges.Top;
        else if (Math.Abs(point.Y - _selectionViewRect.Bottom) <= tolerance) edges |= ResizeEdges.Bottom;
        return edges;
    }

    private CaptureRegion MoveSelection((int X, int Y) point)
    {
        var start = _dragStartRegion.ClampTo(_bounds);
        var maxX = _bounds.X + _bounds.Width - start.Width;
        var maxY = _bounds.Y + _bounds.Height - start.Height;
        var x = Math.Clamp(start.X + point.X - _dragStartPoint.X, _bounds.X, maxX);
        var y = Math.Clamp(start.Y + point.Y - _dragStartPoint.Y, _bounds.Y, maxY);
        return start with { X = x, Y = y };
    }

    private CaptureRegion ResizeSelection((int X, int Y) point)
    {
        var start = _dragStartRegion.ClampTo(_bounds);
        var left = start.X;
        var top = start.Y;
        var right = start.X + start.Width;
        var bottom = start.Y + start.Height;
        var x = Math.Clamp(point.X, _bounds.X, _bounds.X + _bounds.Width);
        var y = Math.Clamp(point.Y, _bounds.Y, _bounds.Y + _bounds.Height);
        if (_resizeEdges.HasFlag(ResizeEdges.Left)) left = Math.Min(x, right - 1);
        if (_resizeEdges.HasFlag(ResizeEdges.Right)) right = Math.Max(x, left + 1);
        if (_resizeEdges.HasFlag(ResizeEdges.Top)) top = Math.Min(y, bottom - 1);
        if (_resizeEdges.HasFlag(ResizeEdges.Bottom)) bottom = Math.Max(y, top + 1);
        return new CaptureRegion(left, top, right - left, bottom - top).ClampTo(_bounds);
    }

    private static System.Windows.Input.Cursor CursorFor(ResizeEdges edges, bool inside) => edges switch
    {
        ResizeEdges.Left or ResizeEdges.Right => System.Windows.Input.Cursors.SizeWE,
        ResizeEdges.Top or ResizeEdges.Bottom => System.Windows.Input.Cursors.SizeNS,
        ResizeEdges.Left | ResizeEdges.Top or ResizeEdges.Right | ResizeEdges.Bottom => System.Windows.Input.Cursors.SizeNWSE,
        ResizeEdges.Right | ResizeEdges.Top or ResizeEdges.Left | ResizeEdges.Bottom => System.Windows.Input.Cursors.SizeNESW,
        _ => inside ? System.Windows.Input.Cursors.SizeAll : System.Windows.Input.Cursors.Cross
    };

    private enum SelectionDragMode { None, Create, Move, Resize }

    [Flags]
    private enum ResizeEdges { None = 0, Left = 1, Top = 2, Right = 4, Bottom = 8 }
}
