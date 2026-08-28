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
using System.Windows.Threading;
using InteractiveWorldMap.Models;
using InteractiveWorldMap.Services;
using InteractiveWorldMap.Utilities;
using InteractiveWorldMap.Views;
using IOPath = System.IO.Path;

namespace InteractiveWorldMap
{
    public partial class MainWindow
    {
        private bool _restoreThumbnailAfterPresentation;
        private bool _restoreDidacticAfterPresentation;

        // Auto-hide timer for the transient content status/warning banner (ContentStatusBanner).
        private DispatcherTimer? _contentStatusTimer;

        // Set (in MB) while opening a location whose image file tripped the heavy-file notice, so the
        // post-decode banner can switch from in-progress to past-tense wording. Null when none tripped.
        private double? _lastLargeImageMb;

        // Incremented by every ShowContentForLocation call. Opening awaits a close animation and an
        // image decode, so a second marker click can land mid-flight; the newer click wins and the
        // older run bails at its next resume point instead of assigning _activeSubwindow over the
        // top of it. Without this the overwritten window stays on screen with no way to close it,
        // holding its decoded bitmaps alive (Escape, outside-click and zoom-out all act on the field
        // only). UI-thread only, so a plain int needs no interlocking.
        private int _contentOpenGeneration;

        /// <summary>
        /// Shows the bottom-left content status banner with <paramref name="message"/>. When
        /// <paramref name="autoHideAfter"/> is provided the banner clears itself after that delay;
        /// otherwise it stays until <see cref="HideContentStatus"/> is called. Safe to call repeatedly.
        /// </summary>
        private void ShowContentStatus(string message, TimeSpan? autoHideAfter = null)
        {
            ContentStatusText.Text = message;
            ContentStatusBanner.Visibility = Visibility.Visible;

            _contentStatusTimer?.Stop();
            if (autoHideAfter is not { } delay)
                return;

            _contentStatusTimer ??= new DispatcherTimer();
            _contentStatusTimer.Interval = delay;
            _contentStatusTimer.Tick -= OnContentStatusTimerTick;
            _contentStatusTimer.Tick += OnContentStatusTimerTick;
            _contentStatusTimer.Start();
        }

        private void OnContentStatusTimerTick(object? sender, EventArgs e) => HideContentStatus();

        private void HideContentStatus()
        {
            _contentStatusTimer?.Stop();
            ContentStatusBanner.Visibility = Visibility.Collapsed;
            ContentStatusText.Text = string.Empty;
        }

        /// <summary>
        /// Raised by <see cref="IContentLoader.LargeImageDetected"/> (on the UI thread) when a heavy
        /// content image file is loading. The image is still shown (downscaled to the display); this
        /// just tells the user why opening takes a moment instead of appearing to hang.
        /// </summary>
        private void OnLargeContentImageDetected(string fileName, long bytes)
        {
            var megabytes = bytes / (1024.0 * 1024.0);
            _lastLargeImageMb = megabytes;
            ShowContentStatus($"Large image ({megabytes:F0} MB) — optimizing for display…");
        }

        private ContentSubwindow CreateContentSubwindow(Location location)
        {
            var window = new ContentSubwindow
            {
                AssociatedLocation = location,
                Owner = this,
                MaximizedBackgroundOpacity =
                    _visualConfig.MaximizedContentBackgroundOpacity
            };
            window.ApplyStyle(_visualConfig.ContentWindows);
            window.PresentationModeChanged += OnContentPresentationModeChanged;
            return window;
        }

        public async void ShowContentForLocation(
            Location location,
            bool suppressNextContentActivation = false)
        {
            var generation = ++_contentOpenGeneration;

            try
            {
                _logger.LogInfo($"Opening content for location: {location.Name}");
                _lastLargeImageMb = null;
                // The "Loading content…" banner is a standalone guest-facing UX toggle (default off,
                // enable via ContentImages.ShowLoadingStatus). It is independent of the developer
                // large-image diagnostics, which are gated separately by EnableImageDiagnostics.
                if (_visualConfig.ContentImages.ShowLoadingStatus)
                    ShowContentStatus("Loading content…");

                // Close existing subwindow and thumbnail browser if any
                if (_activeSubwindow != null)
                {
                    await CloseActiveSubwindowAsync();
                    if (generation != _contentOpenGeneration)
                        return;
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
                if (generation != _contentOpenGeneration)
                    return;

                if (allImagesWithTranslations.Length == 0)
                {
                    // Show text message if no content available
                    var content = $"Content not available for {location.Name}";

                    _activeSubwindow = CreateContentSubwindow(location);
                    if (suppressNextContentActivation)
                        _activeSubwindow.SuppressNextContentActivation();

                    var markerPosition = MapDisplay.GetMapPosition(location.PixelX, location.PixelY, ImageWidth, ImageHeight);
                    _activeSubwindow.ShowContent(content, location.Name, markerPosition);
                }
                else
                {
                    // Show first image
                    await ShowImageAtIndexAsync(
                        location,
                        allImagesWithTranslations,
                        0,
                        generation,
                        suppressNextContentActivation);

                    // ShowImageAtIndexAsync bails silently when superseded, so without this the
                    // "opened" line below would claim an open that did not finish — and this log is
                    // the only record of what these interleaved runs actually did.
                    if (generation != _contentOpenGeneration)
                        return;
                }

                _logger.LogInfo($"Content subwindow opened for: {location.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to show content for location {location.Name}: {ex.Message}");
            }
            finally
            {
                // A superseded run leaves the banner alone: the newer open is still loading and owns it.
                if (generation == _contentOpenGeneration)
                {
                    // If the image file tripped the heavy-file notice, leave a brief past-tense confirmation
                    // (decode is done by now); otherwise clear the "Loading content…" banner.
                    if (_lastLargeImageMb is { } mb)
                        ShowContentStatus($"Large image ({mb:F0} MB) — optimized for display", TimeSpan.FromSeconds(4));
                    else
                        HideContentStatus();
                }
            }
        }

        /// <summary>
        /// Shows a specific image from a location's image collection. <paramref name="generation"/> is
        /// the caller's <see cref="_contentOpenGeneration"/> stamp; the didactic-text await below is
        /// another point where a newer open can take over, and this run must not add windows after it.
        /// </summary>
        private async Task ShowImageAtIndexAsync(
            Location location,
            (BitmapImage Image, string? TranslationText, string? CaptionText)[] allImagesWithTranslations,
            int index,
            int generation,
            bool suppressNextContentActivation = false)
        {
            var (image, translationText, captionText) = allImagesWithTranslations[index];

            // Create and show content subwindow
            _activeSubwindow = CreateContentSubwindow(location);
            if (suppressNextContentActivation)
                _activeSubwindow.SuppressNextContentActivation();

            var markerPosition = MapDisplay.GetMapPosition(location.PixelX, location.PixelY, ImageWidth, ImageHeight);
            _activeSubwindow.ShowContent(image, location.Name, markerPosition, translationText, captionText);

            // Load and show didactic text if available
            var didacticText = await _contentLoader.LoadDidacticTextAsync(location);
            if (generation != _contentOpenGeneration)
                return;

            if (!string.IsNullOrEmpty(didacticText))
            {
                _activeDidacticWindow = new DidacticTextWindow
                {
                    Owner = this
                };
                _activeDidacticWindow.ApplyStyle(_visualConfig.ContentWindows);

                _activeDidacticWindow.SetContent(didacticText, location.Name);
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
                _activeThumbnailBrowser.ApplyStyle(_visualConfig.ContentWindows);

                _activeThumbnailBrowser.LoadThumbnails(images, index);

                // Position thumbnail browser to the right of content window
                // If didactic window exists, it's already on the left
                _activeThumbnailBrowser.PositionRelativeTo(_activeSubwindow);

                // Handle thumbnail selection
                _activeThumbnailBrowser.ThumbnailSelected += (s, selectedIndex) =>
                {
                    if (_activeSubwindow == null)
                        return;

                    if (selectedIndex < 0 ||
                        selectedIndex >= allImagesWithTranslations.Length)
                    {
                        _logger.LogWarning(
                            $"Ignoring thumbnail selection outside loaded range for {location.Name}: {selectedIndex}");
                        return;
                    }

                    var (selectedImage, selectedTranslation, selectedCaption) = allImagesWithTranslations[selectedIndex];
                    if (selectedImage == null)
                    {
                        _logger.LogWarning(
                            $"Ignoring missing thumbnail image for {location.Name} at index {selectedIndex}");
                        return;
                    }

                    var newMarkerPosition = MapDisplay.GetMapPosition(location.PixelX, location.PixelY, ImageWidth, ImageHeight);
                    _activeSubwindow.ShowContent(selectedImage, location.Name, newMarkerPosition, selectedTranslation, selectedCaption);
                    _activeThumbnailBrowser?.SetSelectedIndex(selectedIndex);
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

            ResetCompanionPresentationState();
        }

        /// <summary>
        /// Closes the active content subwindow asynchronously and waits for completion.
        /// </summary>
        private Task CloseActiveSubwindowAsync()
        {
            if (_activeSubwindow == null && _activeThumbnailBrowser == null && _activeDidacticWindow == null)
            {
                ResetCompanionPresentationState();
                return Task.CompletedTask;
            }

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

            ResetCompanionPresentationState();

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

        private void OnContentPresentationModeChanged(
            object? sender,
            EventArgs e)
        {
            if (sender is not ContentSubwindow window ||
                !ReferenceEquals(window, _activeSubwindow))
            {
                return;
            }

            if (window.IsPresentationMode)
            {
                _restoreThumbnailAfterPresentation =
                    _activeThumbnailBrowser?.IsVisible == true;
                _restoreDidacticAfterPresentation =
                    _activeDidacticWindow?.IsVisible == true;

                if (_restoreThumbnailAfterPresentation)
                    _activeThumbnailBrowser!.Hide();

                if (_restoreDidacticAfterPresentation)
                    _activeDidacticWindow!.Hide();

                return;
            }

            if (_restoreThumbnailAfterPresentation &&
                _activeThumbnailBrowser != null)
            {
                _activeThumbnailBrowser.Show();
            }

            if (_restoreDidacticAfterPresentation &&
                _activeDidacticWindow != null)
            {
                _activeDidacticWindow.Show();
            }

            ResetCompanionPresentationState();
        }

        private void ResetCompanionPresentationState()
        {
            _restoreThumbnailAfterPresentation = false;
            _restoreDidacticAfterPresentation = false;
        }

        /// <summary>
        /// Adds clusters to the map canvas using viewport coordinates.
        /// </summary>

    }
}
