using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Controls.Primitives;
using System.Windows.Shapes;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Utilities;
using InteractiveWorldMap.Views;
using IOPath = System.IO.Path;

namespace InteractiveWorldMap
{
    public partial class MainWindow
    {
        public async void ShowContentForLocation(Location location)
        {
            try
            {
                _logger.LogInfo($"Opening content for location: {location.Name}");

                // Close existing subwindow and thumbnail browser if any
                if (_activeSubwindow != null)
                {
                    await CloseActiveSubwindowAsync();
                }
                
                if (_activeThumbnailBrowser != null)
                {
                    _activeThumbnailBrowser.Close();
                    _activeThumbnailBrowser = null;
                }
                
                if (_activeDidacticWindow != null)
                {
                    _activeDidacticWindow.Close();
                    _activeDidacticWindow = null;
                }

                // Load all images with translations for this location
                var allImagesWithTranslations = await _contentLoader.LoadAllLocationImagesWithTranslationsAsync(location);
                
                if (allImagesWithTranslations.Length == 0)
                {
                    // Show text message if no content available
                    var content = $"Content not available for {location.Name}";
                    
                    _activeSubwindow = new ContentSubwindow
                    {
                        AssociatedLocation = location,
                        Owner = this
                    };
                    
                    var markerPosition = MapDisplay.GetMapPosition(location.PixelX, location.PixelY, ImageWidth, ImageHeight);
                    _activeSubwindow.ShowContent(content, location.Name, markerPosition);
                }
                else
                {
                    // Show first image
                    await ShowImageAtIndexAsync(location, allImagesWithTranslations, 0);
                }

                _logger.LogInfo($"Content subwindow opened for: {location.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to show content for location {location.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows a specific image from a location's image collection.
        /// </summary>
        private async Task ShowImageAtIndexAsync(Location location, (BitmapImage Image, string? TranslationText)[] allImagesWithTranslations, int index)
        {
            var (image, translationText) = allImagesWithTranslations[index];
            
            // Create and show content subwindow
            _activeSubwindow = new ContentSubwindow
            {
                AssociatedLocation = location,
                Owner = this
            };

            var markerPosition = MapDisplay.GetMapPosition(location.PixelX, location.PixelY, ImageWidth, ImageHeight);
            _activeSubwindow.ShowContent(image, location.Name, markerPosition, translationText);

            // Load and show didactic text if available
            var didacticText = await _contentLoader.LoadDidacticTextAsync(location);
            if (!string.IsNullOrEmpty(didacticText))
            {
                _activeDidacticWindow = new DidacticTextWindow
                {
                    Owner = this
                };
                
                _activeDidacticWindow.SetContent(didacticText);
                _activeDidacticWindow.PositionRelativeTo(_activeSubwindow);
                _activeDidacticWindow.Show();
                _activeDidacticWindow.AnimateOpen();
                
                _logger.LogInfo($"Didactic text window opened for location: {location.Name}");
            }

            // If there are multiple images, show thumbnail browser
            if (allImagesWithTranslations.Length > 1)
            {
                var images = allImagesWithTranslations.Select(x => x.Image).ToArray();
                
                _activeThumbnailBrowser = new ThumbnailBrowserWindow
                {
                    Owner = this
                };
                
                _activeThumbnailBrowser.LoadThumbnails(images, index);
                
                // Position thumbnail browser to the right of content window
                // If didactic window exists, it's already on the left
                _activeThumbnailBrowser.PositionRelativeTo(_activeSubwindow);
                
                // Handle thumbnail selection
                _activeThumbnailBrowser.ThumbnailSelected += (s, selectedIndex) =>
                {
                    if (_activeSubwindow != null)
                    {
                        var (selectedImage, selectedTranslation) = allImagesWithTranslations[selectedIndex];
                        var newMarkerPosition = MapDisplay.GetMapPosition(location.PixelX, location.PixelY, ImageWidth, ImageHeight);
                        _activeSubwindow.ShowContent(selectedImage, location.Name, newMarkerPosition, selectedTranslation);
                        _activeThumbnailBrowser?.SetSelectedIndex(selectedIndex);
                    }
                };
                
                _activeThumbnailBrowser.Show();
                _activeThumbnailBrowser.AnimateOpen();
                
                _logger.LogInfo($"Thumbnail browser opened with {allImagesWithTranslations.Length} images");
            }
        }

        /// <summary>
        /// Closes the active content subwindow.
        /// </summary>
        public void CloseActiveSubwindow()
        {
            if (_activeSubwindow != null)
            {
                _logger.LogInfo("Closing active subwindow");
                var windowToClose = _activeSubwindow;
                _activeSubwindow = null;
                
                windowToClose.AnimateClose(() =>
                {
                    Focus(); // Return focus to main window
                });
            }
            
            // Also close thumbnail browser
            if (_activeThumbnailBrowser != null)
            {
                _activeThumbnailBrowser.Close();
                _activeThumbnailBrowser = null;
            }
            
            // Also close didactic window
            if (_activeDidacticWindow != null)
            {
                _activeDidacticWindow.Close();
                _activeDidacticWindow = null;
            }
        }

        /// <summary>
        /// Closes the active content subwindow asynchronously and waits for completion.
        /// </summary>
        private Task CloseActiveSubwindowAsync()
        {
            if (_activeSubwindow == null && _activeThumbnailBrowser == null && _activeDidacticWindow == null)
                return Task.CompletedTask;

            _logger.LogInfo("Closing active subwindow (async)");
            
            var tcs = new TaskCompletionSource<bool>();
            var windowToClose = _activeSubwindow;
            _activeSubwindow = null;

            // Close thumbnail browser immediately
            if (_activeThumbnailBrowser != null)
            {
                _activeThumbnailBrowser.Close();
                _activeThumbnailBrowser = null;
            }
            
            // Close didactic window immediately
            if (_activeDidacticWindow != null)
            {
                _activeDidacticWindow.Close();
                _activeDidacticWindow = null;
            }

            if (windowToClose != null)
            {
                windowToClose.AnimateClose(() =>
                {
                    Focus(); // Return focus to main window
                    tcs.SetResult(true);
                });
            }
            else
            {
                tcs.SetResult(true);
            }

            return tcs.Task;
        }

        /// <summary>
        /// Adds clusters to the map canvas using viewport coordinates.
        /// </summary>

    }
}
