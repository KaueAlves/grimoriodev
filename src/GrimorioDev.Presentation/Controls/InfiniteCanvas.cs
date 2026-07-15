using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GrimorioDev.Presentation.Controls;

public sealed record CardRenderData(
    Guid Id, double X, double Y, int ZIndex,
    double Width, double Height,
    string Title, string Content, bool IsPinned);

public enum LodLevel { Mini, Compact, Normal, Detailed, MaxDetail }

public sealed class CardMovedEventArgs : EventArgs
{
    public Guid CardId { get; }
    public double NewX { get; }
    public double NewY { get; }

    public CardMovedEventArgs(Guid cardId, double newX, double newY)
    {
        CardId = cardId;
        NewX = newX;
        NewY = newY;
    }
}

public class InfiniteCanvas : Canvas
{
    private Point _lastMousePos;
    private Point _panStart;
    private bool _isPanning;
    private bool _isDragging;
    private double _zoomLevel = 1.0;
    private readonly TranslateTransform _panTransform = new();
    private readonly ScaleTransform _zoomTransform = new();
    private readonly TransformGroup _transformGroup = new();
    private DrawingVisual? _gridVisual;
    private readonly List<DrawingVisual> _cardVisuals = new();
    private CardRenderData? _selectedCard;
    private CardRenderData? _draggedCard;
    private Point _dragOffset;
    private List<CardRenderData> _cachedCards = new();
    private const double MinZoom = 0.1;
    private const double MaxZoom = 10.0;
    private const double GridBaseSize = 40;

    public static readonly DependencyProperty CardsProperty =
        DependencyProperty.Register(nameof(Cards), typeof(IEnumerable<CardRenderData>), typeof(InfiniteCanvas),
            new FrameworkPropertyMetadata(null, OnCardsChanged));

    public IEnumerable<CardRenderData>? Cards
    {
        get => (IEnumerable<CardRenderData>?)GetValue(CardsProperty);
        set => SetValue(CardsProperty, value);
    }

    public double ZoomLevel => _zoomLevel;

    public event Action<double, double, double>? ViewportChanged;
    public event EventHandler<CardRenderData>? CardSelected;
    public event EventHandler<CardMovedEventArgs>? CardMoved;
    public event Action? CardDeselected;
    public event Action<double, double>? CanvasDoubleClicked;

    static InfiniteCanvas()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(InfiniteCanvas), new FrameworkPropertyMetadata(typeof(InfiniteCanvas)));
    }

    public InfiniteCanvas()
    {
        ClipToBounds = true;
        Background = Brushes.Transparent;

        _transformGroup.Children.Add(_zoomTransform);
        _transformGroup.Children.Add(_panTransform);
        RenderTransform = _transformGroup;

        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshCachedCards();
        CreateGridVisual();
        RenderCards();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderCards();
    }

    private static void OnCardsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (InfiniteCanvas)d;
        if (e.OldValue is INotifyCollectionChanged oldColl)
            oldColl.CollectionChanged -= canvas.OnCardsCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged newColl)
            newColl.CollectionChanged += canvas.OnCardsCollectionChanged;
        canvas.RefreshCachedCards();
        canvas.RenderCards();
    }

    private void OnCardsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshCachedCards();
        RenderCards();
    }

    private void RefreshCachedCards()
    {
        _cachedCards = Cards?.ToList() ?? new List<CardRenderData>();
    }

    private CardRenderData? HitTestCard(Point canvasPoint)
    {
        for (var i = _cachedCards.Count - 1; i >= 0; i--)
        {
            var c = _cachedCards[i];
            if (canvasPoint.X >= c.X && canvasPoint.X <= c.X + c.Width &&
                canvasPoint.Y >= c.Y && canvasPoint.Y <= c.Y + c.Height)
                return c;
        }
        return null;
    }

    private Point ScreenToCanvas(Point screen)
    {
        return new Point(
            (screen.X - _panTransform.X) / _zoomLevel,
            (screen.Y - _panTransform.Y) / _zoomLevel);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        var mousePos = e.GetPosition(this);
        var delta = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
        var newZoom = Math.Clamp(_zoomLevel * delta, MinZoom, MaxZoom);

        var scale = newZoom / _zoomLevel;
        var panX = mousePos.X - scale * (mousePos.X - _panTransform.X);
        var panY = mousePos.Y - scale * (mousePos.Y - _panTransform.Y);

        _zoomLevel = newZoom;
        _zoomTransform.ScaleX = _zoomLevel;
        _zoomTransform.ScaleY = _zoomLevel;
        _panTransform.X = panX;
        _panTransform.Y = panY;

        CreateGridVisual();
        RenderCards();
        NotifyViewportChanged();
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(this);

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            if (e.ClickCount >= 2)
            {
                var canvasPos = ScreenToCanvas(pos);
                var hit = HitTestCard(canvasPos);
                if (hit is null)
                {
                    CanvasDoubleClicked?.Invoke(canvasPos.X, canvasPos.Y);
                    e.Handled = true;
                    return;
                }
            }

            var canvasPosSingle = ScreenToCanvas(pos);
            var hitSingle = HitTestCard(canvasPosSingle);

            if (hitSingle is not null)
            {
                _selectedCard = hitSingle;
                _draggedCard = hitSingle;
                _dragOffset = new Point(canvasPosSingle.X - hitSingle.X, canvasPosSingle.Y - hitSingle.Y);
                _isDragging = true;
                CaptureMouse();
                Cursor = Cursors.SizeAll;
                CardSelected?.Invoke(this, hitSingle);
                RenderCards();
                e.Handled = true;
                return;
            }

            if (_selectedCard is not null)
            {
                _selectedCard = null;
                CardDeselected?.Invoke();
                RenderCards();
            }
        }

        if (e.RightButton == MouseButtonState.Pressed)
        {
            _panStart = pos;
            _isPanning = true;
            CaptureMouse();
            Cursor = Cursors.Hand;
            e.Handled = true;
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var currentPos = e.GetPosition(this);

        if (_isDragging && _draggedCard is not null && e.LeftButton == MouseButtonState.Pressed)
        {
            var canvasPos = ScreenToCanvas(currentPos);
            var newX = canvasPos.X - _dragOffset.X;
            var newY = canvasPos.Y - _dragOffset.Y;

            _draggedCard = _draggedCard with { X = newX, Y = newY };
            _selectedCard = _draggedCard;
            RenderCards();
            e.Handled = true;
            return;
        }

        if (_isPanning && e.RightButton == MouseButtonState.Pressed)
        {
            var dx = currentPos.X - _panStart.X;
            var dy = currentPos.Y - _panStart.Y;
            _panTransform.X += dx;
            _panTransform.Y += dy;
            _panStart = currentPos;
            CreateGridVisual();
            RenderCards();
            NotifyViewportChanged();
            e.Handled = true;
        }

        _lastMousePos = currentPos;
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        if (_isDragging && _draggedCard is not null && e.LeftButton == MouseButtonState.Released)
        {
            _isDragging = false;
            ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
            CardMoved?.Invoke(this, new CardMovedEventArgs(_draggedCard.Id, _draggedCard.X, _draggedCard.Y));
            _draggedCard = null;
            RenderCards();
            e.Handled = true;
        }

        if (_isPanning && e.RightButton == MouseButtonState.Released)
        {
            _isPanning = false;
            ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
            e.Handled = true;
        }
    }

    private void NotifyViewportChanged()
    {
        var left = -_panTransform.X / _zoomLevel;
        var top = -_panTransform.Y / _zoomLevel;
        ViewportChanged?.Invoke(left, top, _zoomLevel);
    }

    private void CreateGridVisual()
    {
        if (_gridVisual is not null)
            RemoveVisualChild(_gridVisual);

        _gridVisual = new DrawingVisual();
        var gridSpacing = GridBaseSize * _zoomLevel;
        if (gridSpacing < 5) gridSpacing = 5;

        using var dc = _gridVisual.RenderOpen();

        var offsetX = _panTransform.X % gridSpacing;
        var offsetY = _panTransform.Y % gridSpacing;

        var pen = new Pen(new SolidColorBrush(Color.FromArgb(30, 128, 128, 128)), 0.5);

        for (var x = offsetX; x < ActualWidth + Math.Abs(_panTransform.X) * 2; x += gridSpacing)
        {
            if (x >= 0 && x <= ActualWidth)
                dc.DrawLine(pen, new Point(x, 0), new Point(x, ActualHeight));
        }

        for (var y = offsetY; y < ActualHeight + Math.Abs(_panTransform.Y) * 2; y += gridSpacing)
        {
            if (y >= 0 && y <= ActualHeight)
                dc.DrawLine(pen, new Point(0, y), new Point(ActualWidth, y));
        }

        pen.Freeze();
        AddVisualChild(_gridVisual);
    }

    private void RenderCards()
    {
        foreach (var v in _cardVisuals)
            RemoveVisualChild(v);
        _cardVisuals.Clear();

        if (_cachedCards.Count == 0)
            return;

        var vpLeft = -_panTransform.X / _zoomLevel;
        var vpTop = -_panTransform.Y / _zoomLevel;
        var vpRight = vpLeft + ActualWidth / _zoomLevel;
        var vpBottom = vpTop + ActualHeight / _zoomLevel;

        var visible = _cachedCards
            .Where(c => c.X + c.Width >= vpLeft && c.X <= vpRight &&
                        c.Y + c.Height >= vpTop && c.Y <= vpBottom)
            .OrderBy(c => c.ZIndex)
            .ToList();

        using var pool = new DrawingVisualPool(_cardVisuals, visible.Count);

        var bgBrush = new SolidColorBrush(Color.FromRgb(30, 30, 30));
        var borderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
        var borderPen = new Pen(borderBrush, 1);
        var headerBg = new SolidColorBrush(Color.FromRgb(50, 50, 50));
        var titleBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220));
        var contentBrush = new SolidColorBrush(Color.FromRgb(180, 180, 180));
        var pinBrush = new SolidColorBrush(Color.FromRgb(255, 200, 50));
        var selectBrush = new SolidColorBrush(Color.FromArgb(60, 100, 200, 255));
        var selectPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 100, 200, 255)), 2);
        var ghostBrush = new SolidColorBrush(Color.FromArgb(40, 100, 100, 100));
        var typeface = new Typeface("Consolas");

        var selectedId = _selectedCard?.Id;

        var lod = _zoomLevel switch
        {
            < 0.3 => LodLevel.Mini,
            < 0.5 => LodLevel.Compact,
            < 1.5 => LodLevel.Normal,
            < 5.0 => LodLevel.Detailed,
            _     => LodLevel.MaxDetail
        };

        foreach (var card in visible)
        {
            var dv = pool.Next();
            using var dc = dv.RenderOpen();

            var renderX = card.X;
            var renderY = card.Y;

            var isSelected = card.Id == selectedId;

            if (isSelected && _draggedCard is not null && _draggedCard.Id == card.Id)
            {
                renderX = _draggedCard.X;
                renderY = _draggedCard.Y;

                var ghostSx = card.X * _zoomLevel + _panTransform.X;
                var ghostSy = card.Y * _zoomLevel + _panTransform.Y;
                var ghostSw = card.Width * _zoomLevel;
                var ghostSh = card.Height * _zoomLevel;
                dc.DrawRectangle(ghostBrush, null, new Rect(ghostSx, ghostSy, ghostSw, ghostSh));
            }

            var sx = renderX * _zoomLevel + _panTransform.X;
            var sy = renderY * _zoomLevel + _panTransform.Y;
            var sw = card.Width * _zoomLevel;
            var sh = card.Height * _zoomLevel;
            var radius = Math.Min(4 * _zoomLevel, sw * 0.1);

            var rect = new Rect(sx, sy, sw, sh);

            if (lod == LodLevel.Mini)
            {
                dc.DrawRectangle(isSelected ? selectBrush : bgBrush, borderPen, rect);
                dc.Pop();
                continue;
            }

            var headerH = Math.Min(24 * _zoomLevel, sh * 0.25);
            var headerRect = new Rect(sx, sy, sw, headerH);

            var clipGeom = new RectangleGeometry(rect, radius, radius);
            dc.PushClip(clipGeom);

            dc.DrawRectangle(bgBrush, borderPen, rect);
            dc.DrawRectangle(headerBg, null, headerRect);

            if (isSelected)
                dc.DrawRectangle(selectBrush, selectPen, rect);

            var titleFontSize = Math.Clamp(12 * _zoomLevel, 6, 48);
            var ft = new FormattedText(card.Title, System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, typeface, titleFontSize, titleBrush, 1.0);

            var textX = sx + 6 * _zoomLevel;
            var textY = sy + (headerH - ft.Height) / 2;
            dc.DrawText(ft, new Point(textX, textY));

            if (card.IsPinned)
            {
                var pinSize = 6 * _zoomLevel;
                dc.DrawEllipse(pinBrush, null, new Point(sx + sw - pinSize * 1.5, sy + headerH / 2), pinSize, pinSize);
            }

            if (card.Content.Length > 0 && lod >= LodLevel.Normal)
            {
                var contentFontSize = titleFontSize * (lod >= LodLevel.Detailed ? 1.0 : 0.8);
                var maxLines = lod >= LodLevel.MaxDetail ? 20 : lod >= LodLevel.Detailed ? 8 : 3;
                var contentFt = new FormattedText(card.Content, System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, typeface, contentFontSize, contentBrush, 1.0);

                if (contentFt.Height > sh - headerH - 12 * _zoomLevel)
                {
                    var lineHeight = contentFt.Height /
                        Math.Max(1, card.Content.Count(c => c == '\n') + 1);
                    var visibleLines = Math.Min(maxLines,
                        (int)((sh - headerH - 12 * _zoomLevel) / Math.Max(lineHeight, 1)));
                    var truncatedText = TruncateLines(card.Content, visibleLines);
                    contentFt = new FormattedText(truncatedText, System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, typeface, contentFontSize, contentBrush, 1.0);
                }

                var contentY = sy + headerH + 6 * _zoomLevel;
                dc.DrawText(contentFt, new Point(textX, contentY));
            }

            dc.Pop();
        }

        foreach (var dv in _cardVisuals)
            AddVisualChild(dv);
    }

    public void SelectCard(Guid cardId)
    {
        _selectedCard = _cachedCards.FirstOrDefault(c => c.Id == cardId);
        RenderCards();
    }

    public void DeselectAll()
    {
        _selectedCard = null;
        CardDeselected?.Invoke();
        RenderCards();
    }

    public void SetZoom(double zoom)
    {
        _zoomLevel = Math.Clamp(zoom, MinZoom, MaxZoom);
        _zoomTransform.ScaleX = _zoomLevel;
        _zoomTransform.ScaleY = _zoomLevel;
        CreateGridVisual();
        RenderCards();
        NotifyViewportChanged();
    }

    public void CenterOn(double x, double y)
    {
        _panTransform.X = ActualWidth / 2 - x * _zoomLevel;
        _panTransform.Y = ActualHeight / 2 - y * _zoomLevel;
        CreateGridVisual();
        RenderCards();
    }

    protected override int VisualChildrenCount => base.VisualChildrenCount + (_gridVisual is not null ? 1 : 0) + _cardVisuals.Count;

    protected override Visual GetVisualChild(int index)
    {
        var baseCount = base.VisualChildrenCount;
        if (index < baseCount)
            return base.GetVisualChild(index);

        index -= baseCount;
        if (_gridVisual is not null)
        {
            if (index == 0) return _gridVisual;
            index--;
        }

        return _cardVisuals[index];
    }

    private static string TruncateLines(string text, int maxLines)
    {
        if (maxLines <= 0) return string.Empty;
        var lineCount = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                lineCount++;
                if (lineCount >= maxLines)
                    return text[..i];
            }
        }
        return text;
    }
}

file sealed class DrawingVisualPool : IDisposable
{
    private readonly List<DrawingVisual> _target;
    private int _index;

    public DrawingVisualPool(List<DrawingVisual> target, int count)
    {
        _target = target;
        for (var i = 0; i < count; i++)
            target.Add(new DrawingVisual());
    }

    public DrawingVisual Next() => _target[_index++];

    public void Dispose() => _index = 0;
}
