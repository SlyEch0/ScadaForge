using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ScadaForge.Models;
using ScadaForge.ViewModels;

namespace ScadaForge.Views;

/// <summary>
/// Main IDE window. Renders the process graphic on a Canvas and handles object selection.
/// In a full designer this would be replaced by a proper visual tree of user controls,
/// but for the fast-path option B this keeps the rendering simple and reliable.
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel? Vm => DataContext as MainViewModel;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;

        // Initial render of all process objects
        RenderProcessObjects();

        // Keep the canvas in sync with simulation updates
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        timer.Tick += (_, _) => RenderProcessObjects();
        timer.Start();
    }

    private void RenderProcessObjects()
    {
        if (Vm is null) return;

        // Remove previous dynamic shapes (keep the static pipes/labels/legend)
        var toRemove = ProcessCanvas.Children
            .OfType<FrameworkElement>()
            .Where(c => c.Tag is GraphicObject)
            .ToList();

        foreach (var el in toRemove)
            ProcessCanvas.Children.Remove(el);

        foreach (var obj in Vm.Objects)
        {
            FrameworkElement visual = obj switch
            {
                Motor m => CreateMotorVisual(m),
                Tank t => CreateTankVisual(t),
                ControlValve v => CreateValveVisual(v),
                Instrument i => CreateInstrumentVisual(i),
                _ => new Rectangle { Width = 40, Height = 40, Fill = Brushes.Gray }
            };

            visual.Tag = obj;
            Canvas.SetLeft(visual, obj.X);
            Canvas.SetTop(visual, obj.Y);
            visual.MouseLeftButtonDown += Object_MouseLeftButtonDown;

            ProcessCanvas.Children.Add(visual);
        }
    }

    private FrameworkElement CreateMotorVisual(Motor m)
    {
        var border = new Border
        {
            Width = m.Width,
            Height = m.Height,
            Background = new SolidColorBrush(Color.FromRgb(0x31, 0x32, 0x44)),
            BorderBrush = m.IsSelected
                ? new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA))
                : new SolidColorBrush(Color.FromRgb(0x45, 0x47, 0x5A)),
            BorderThickness = new Thickness(m.IsSelected ? 2 : 1),
            CornerRadius = new CornerRadius(6),
            Cursor = Cursors.Hand
        };

        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(new TextBlock
        {
            Text = m.Name,
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        stack.Children.Add(new TextBlock
        {
            Text = m.State.ToUpperInvariant(),
            FontSize = 11,
            Foreground = m.State == "Running"
                ? new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1))
                : new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8)),
            HorizontalAlignment = HorizontalAlignment.Center
        });
        stack.Children.Add(new TextBlock
        {
            Text = $"{m.SpeedRpm:F0} RPM",
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(0xA6, 0xAD, 0xC8)),
            HorizontalAlignment = HorizontalAlignment.Center
        });

        border.Child = stack;
        return border;
    }

    private FrameworkElement CreateTankVisual(Tank t)
    {
        var border = new Border
        {
            Width = t.Width,
            Height = t.Height,
            Background = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x25)),
            BorderBrush = t.IsSelected
                ? new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA))
                : new SolidColorBrush(Color.FromRgb(0x45, 0x47, 0x5A)),
            BorderThickness = new Thickness(t.IsSelected ? 2 : 1),
            CornerRadius = new CornerRadius(4),
            Cursor = Cursors.Hand
        };

        // Simple level fill
        var levelHeight = t.Height * (t.LevelPercent / 100.0);
        var level = new Rectangle
        {
            Width = t.Width - 4,
            Height = levelHeight,
            Fill = new SolidColorBrush(Color.FromArgb(180, 0x89, 0xB4, 0xFA)),
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(2)
        };

        var grid = new Grid();
        grid.Children.Add(level);
        grid.Children.Add(new TextBlock
        {
            Text = t.Name.Split(' ').FirstOrDefault() ?? t.Name,
            FontSize = 10,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 4, 0, 0)
        });

        border.Child = grid;
        return border;
    }

    private FrameworkElement CreateValveVisual(ControlValve v)
    {
        var ellipse = new Ellipse
        {
            Width = 28,
            Height = 28,
            Stroke = v.IsSelected
                ? new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA))
                : new SolidColorBrush(Color.FromRgb(0xF9, 0xE2, 0xAF)),
            StrokeThickness = 2,
            Fill = v.IsOpen
                ? new SolidColorBrush(Color.FromArgb(60, 0xF9, 0xE2, 0xAF))
                : Brushes.Transparent,
            Cursor = Cursors.Hand
        };
        return ellipse;
    }

    private FrameworkElement CreateInstrumentVisual(Instrument i)
    {
        var border = new Border
        {
            Width = 56,
            Height = 24,
            Background = new SolidColorBrush(Color.FromRgb(0x31, 0x32, 0x44)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Cursor = Cursors.Hand
        };
        border.Child = new TextBlock
        {
            Text = i.Name,
            FontSize = 10,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        return border;
    }

    private void Object_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is GraphicObject obj && Vm is not null)
        {
            Vm.SelectObjectCommand.Execute(obj);
            e.Handled = true;
            RenderProcessObjects(); // refresh selection highlight
        }
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Click on empty canvas deselects
        if (e.OriginalSource == ProcessCanvas && Vm is not null)
        {
            Vm.SelectObjectCommand.Execute(null);
            RenderProcessObjects();
        }
    }
}
