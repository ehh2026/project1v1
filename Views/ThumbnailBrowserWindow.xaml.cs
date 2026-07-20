using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Views;

/// <summary>
/// Displays thumbnails of all images for a location.
/// </summary>
public partial class ThumbnailBrowserWindow : Window
{
    public ObservableCollection<ThumbnailItem> Thumbnails { get; } = new ObservableCollection<ThumbnailItem>();
    
    public event EventHandler<int>? ThumbnailSelected;

    public ThumbnailBrowserWindow()
    {
        InitializeComponent();
        ThumbnailList.ItemsSource = Thumbnails;

        // Start with opacity 0 for animation
        Opacity = 0;
    }

    /// <summary>
    /// Applies configured popup background, border, and corner radius. Call before showing.
    /// </summary>
    public void ApplyStyle(ContentWindowConfig style)
    {
        if (style == null) return;

        RootBorder.Background = ContentWindowTheme.ToBrush(
            style.Popup.BackgroundColor, style.Popup.BackgroundOpacity, Color.FromRgb(0x1E, 0x1E, 0x1E));
        RootBorder.BorderBrush = ContentWindowTheme.ToBrush(style.Popup.BorderColor, Colors.White);
        RootBorder.BorderThickness = new Thickness(style.Popup.BorderThickness);
        RootBorder.CornerRadius = new CornerRadius(style.Popup.CornerRadius);
    }

    /// <summary>
    /// Loads thumbnails for the given images.
    /// </summary>
    public void LoadThumbnails(BitmapImage[] images, int selectedIndex)
    {
        Thumbnails.Clear();
        
        for (int i = 0; i < images.Length; i++)
        {
            Thumbnails.Add(new ThumbnailItem
            {
                Thumbnail = images[i],
                Index = i,
                IsSelected = i == selectedIndex
            });
        }
    }

    /// <summary>
    /// Updates which thumbnail is selected.
    /// </summary>
    public void SetSelectedIndex(int index)
    {
        for (int i = 0; i < Thumbnails.Count; i++)
        {
            Thumbnails[i].IsSelected = i == index;
        }
    }

    /// <summary>
    /// Positions the window to the right of the content window and matches its height.
    /// </summary>
    public void PositionRelativeTo(Window contentWindow)
    {
        // Match the height of the content window
        Height = contentWindow.Height;
        
        // Position to the right of the content window
        Left = contentWindow.Left + contentWindow.Width + 10;
        Top = contentWindow.Top;
        
        // Ensure it stays on screen
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        if (Left + Width > screenWidth)
        {
            // If it doesn't fit on the right, put it on the left
            Left = contentWindow.Left - Width - 10;
        }
        
        // Ensure minimum left position
        Left = Math.Max(0, Left);
    }

    /// <summary>
    /// Animates the window opening.
    /// </summary>
    public void AnimateOpen()
    {
        var duration = TimeSpan.FromMilliseconds(150);
        var fadeIn = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = duration
        };
        BeginAnimation(OpacityProperty, fadeIn);
    }

    /// <summary>
    /// Animates the window closing.
    /// </summary>
    public void AnimateClose(Action? onComplete = null)
    {
        var duration = TimeSpan.FromMilliseconds(100);
        var fadeOut = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = duration
        };
        
        fadeOut.Completed += (s, e) =>
        {
            Close();
            onComplete?.Invoke();
        };
        
        BeginAnimation(OpacityProperty, fadeOut);
    }

    private void Thumbnail_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button &&
            button.DataContext is ThumbnailItem item)
        {
            ThumbnailSelected?.Invoke(this, item.Index);
        }
    }
}

/// <summary>
/// Represents a thumbnail item in the browser.
/// </summary>
public class ThumbnailItem : System.ComponentModel.INotifyPropertyChanged
{
    private bool _isSelected;
    
    public BitmapImage? Thumbnail { get; set; }
    public int Index { get; set; }
    
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }
    
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}
