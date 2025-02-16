using System.Drawing;
using System.Windows.Forms;

namespace OMNI.Forms;

public class OverlayCaptureForm : Form
{
    private Point _dragStartPoint;
    private bool _isDragging = false;
    private bool _isResizing = false;
    private readonly Action<Rectangle> _onCapture;
    private readonly Label _infoLabel;
    private readonly Button _captureButton;
    private const int RESIZE_BORDER = 5;
    private ResizeDirection _currentResizeDirection;

    private enum ResizeDirection
    {
        None,
        Top, Bottom,
        Left, Right,
        TopLeft, TopRight,
        BottomLeft, BottomRight
    }

    public OverlayCaptureForm(Action<Rectangle> onCapture)
    {
        _onCapture = onCapture;
        _infoLabel = new Label();
        _captureButton = new Button();
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        // Form settings
        this.FormBorderStyle = FormBorderStyle.None;
        this.BackColor = Color.Black;
        this.Opacity = 0.3;
        this.Size = new Size(200, 50);
        this.MinimumSize = new Size(100, 50);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.TopMost = true;
        this.ShowInTaskbar = false;

        // Capture button setup
        _captureButton.Text = "Capture";
        _captureButton.Dock = DockStyle.Bottom;
        _captureButton.Height = 25;
        _captureButton.BackColor = Color.White;
        _captureButton.FlatStyle = FlatStyle.Flat;
        _captureButton.Cursor = Cursors.Hand;

        // Info label setup
        _infoLabel.Text = "Drag to move, edges to resize";
        _infoLabel.Dock = DockStyle.Fill;
        _infoLabel.TextAlign = ContentAlignment.MiddleCenter;
        _infoLabel.ForeColor = Color.White;
        _infoLabel.Font = new Font(this.Font.FontFamily, 9, FontStyle.Bold);

        // Add close button
        var closeButton = new Button
        {
            Text = "✕",
            Size = new Size(20, 20),
            Location = new Point(this.Width - 25, 2),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        // Event handlers
        this.MouseDown += Form_MouseDown;
        this.MouseMove += Form_MouseMove;
        this.MouseUp += Form_MouseUp;
        _infoLabel.MouseDown += Form_MouseDown;
        _infoLabel.MouseMove += Form_MouseMove;
        _infoLabel.MouseUp += Form_MouseUp;

        _captureButton.Click += (s, e) =>
        {
            _onCapture(this.Bounds);
            this.Close();
        };

        closeButton.Click += (s, e) => this.Close();

        // Key handler for escape
        this.KeyPreview = true;
        this.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape)
                this.Close();
        };

        // Add controls
        this.Controls.Add(closeButton);
        this.Controls.Add(_captureButton);
        this.Controls.Add(_infoLabel);
    }

    private ResizeDirection GetResizeDirection(Point mousePosition)
    {
        if (mousePosition.X <= RESIZE_BORDER)
        {
            if (mousePosition.Y <= RESIZE_BORDER) return ResizeDirection.TopLeft;
            if (mousePosition.Y >= Height - RESIZE_BORDER) return ResizeDirection.BottomLeft;
            return ResizeDirection.Left;
        }
        if (mousePosition.X >= Width - RESIZE_BORDER)
        {
            if (mousePosition.Y <= RESIZE_BORDER) return ResizeDirection.TopRight;
            if (mousePosition.Y >= Height - RESIZE_BORDER) return ResizeDirection.BottomRight;
            return ResizeDirection.Right;
        }
        if (mousePosition.Y <= RESIZE_BORDER) return ResizeDirection.Top;
        if (mousePosition.Y >= Height - RESIZE_BORDER) return ResizeDirection.Bottom;
        return ResizeDirection.None;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_isDragging && !_isResizing)
        {
            ResizeDirection direction = GetResizeDirection(e.Location);
            switch (direction)
            {
                case ResizeDirection.TopLeft:
                case ResizeDirection.BottomRight:
                    this.Cursor = Cursors.SizeNWSE;
                    break;
                case ResizeDirection.TopRight:
                case ResizeDirection.BottomLeft:
                    this.Cursor = Cursors.SizeNESW;
                    break;
                case ResizeDirection.Left:
                case ResizeDirection.Right:
                    this.Cursor = Cursors.SizeWE;
                    break;
                case ResizeDirection.Top:
                case ResizeDirection.Bottom:
                    this.Cursor = Cursors.SizeNS;
                    break;
                default:
                    this.Cursor = Cursors.SizeAll;
                    break;
            }
        }
    }

    private void Form_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _currentResizeDirection = GetResizeDirection(e.Location);
            if (_currentResizeDirection != ResizeDirection.None)
            {
                _isResizing = true;
            }
            else
            {
                _isDragging = true;
            }
            _dragStartPoint = this.PointToScreen(e.Location);
        }
    }

    private void Form_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_isResizing)
        {
            ResizeForm(e);
        }
        else if (_isDragging)
        {
            Point currentScreenPos = PointToScreen(e.Location);
            Location = new Point(
                Location.X + (currentScreenPos.X - _dragStartPoint.X),
                Location.Y + (currentScreenPos.Y - _dragStartPoint.Y)
            );
            _dragStartPoint = currentScreenPos;
        }
    }

    private void ResizeForm(MouseEventArgs e)
    {
        Point currentScreenPos = PointToScreen(e.Location);
        int dx = currentScreenPos.X - _dragStartPoint.X;
        int dy = currentScreenPos.Y - _dragStartPoint.Y;

        switch (_currentResizeDirection)
        {
            case ResizeDirection.Left:
            case ResizeDirection.TopLeft:
            case ResizeDirection.BottomLeft:
                Width = Math.Max(MinimumSize.Width, Width - dx);
                Location = new Point(Location.X + dx, Location.Y);
                break;
            case ResizeDirection.Right:
            case ResizeDirection.TopRight:
            case ResizeDirection.BottomRight:
                Width = Math.Max(MinimumSize.Width, Width + dx);
                break;
        }

        switch (_currentResizeDirection)
        {
            case ResizeDirection.Top:
            case ResizeDirection.TopLeft:
            case ResizeDirection.TopRight:
                Height = Math.Max(MinimumSize.Height, Height - dy);
                Location = new Point(Location.X, Location.Y + dy);
                break;
            case ResizeDirection.Bottom:
            case ResizeDirection.BottomLeft:
            case ResizeDirection.BottomRight:
                Height = Math.Max(MinimumSize.Height, Height + dy);
                break;
        }

        _dragStartPoint = currentScreenPos;
    }

    private void Form_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _isDragging = false;
            _isResizing = false;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        this.Opacity = 0.1;
        using (var timer = new System.Windows.Forms.Timer())
        {
            timer.Interval = 100;
            timer.Tick += (s, e) =>
            {
                this.Opacity = 0.3;
                timer.Stop();
            };
            timer.Start();
        }
    }
}