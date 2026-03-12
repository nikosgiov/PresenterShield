using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PresenterShield.Models;
using PresenterShield.Services;
using PresenterShield.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;

namespace PresenterShield.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly WindowService _windowService;
        private readonly ConfigService _configService;
        private readonly ScreenMirrorService _mirrorService;
        private MirrorWindow? _mirrorWindow;
        private HashSet<string> _savedPrivateWindows;
        private DispatcherTimer _refreshTimer;

        [ObservableProperty]
        private ObservableCollection<WindowModel> windows = new();

        [ObservableProperty]
        private bool isSessionActive;

        [ObservableProperty]
        private bool isMirroringActive;

        public MainViewModel()
        {
            _windowService = new WindowService();
            _configService = new ConfigService();
            _mirrorService = new ScreenMirrorService();
            _mirrorService.MirroringError += OnMirroringError;
            _savedPrivateWindows = _configService.LoadPrivateWindowNames();
            RefreshWindows();

            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(2);
            _refreshTimer.Tick += (s, e) => RefreshWindows();
            _refreshTimer.Start();
        }

        private void OnMirroringError(string message)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (IsMirroringActive)
                {
                    ToggleMirroring(); // Stop and cleanup
                }
                System.Windows.MessageBox.Show(message, "Screen Mirroring Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            });
        }

        private void RefreshWindows()
        {
            var openWindows = _windowService.GetOpenWindows();
            var openWindowHandles = openWindows.Select(w => w.Handle).ToHashSet();
            var currentWindows = Windows.ToList();

            foreach (var w in currentWindows)
            {
                if (!openWindowHandles.Contains(w.Handle))
                {
                    w.PropertyChanged -= Window_PropertyChanged;
                    Windows.Remove(w);
                    if (IsSessionActive && w.IsPrivate)
                    {
                        _windowService.RemovePrivacyOverlay(w);
                    }
                }
            }

            var currentHandles = Windows.Select(w => w.Handle).ToHashSet();
            foreach (var w in openWindows)
            {
                if (!currentHandles.Contains(w.Handle))
                {
                    if (_savedPrivateWindows.Contains(w.Title))
                    {
                        w.IsPrivate = true;
                    }
                    w.PropertyChanged += Window_PropertyChanged;
                    Windows.Add(w);

                    if (IsSessionActive && w.IsPrivate)
                    {
                        _windowService.ApplyPrivacyOverlay(w, w.Opacity);
                    }
                }
            }
        }

        private void Window_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is WindowModel window)
            {
                if (e.PropertyName == nameof(WindowModel.IsPrivate))
                {
                    if (window.IsPrivate)
                    {
                        _savedPrivateWindows.Add(window.Title);
                        if (IsSessionActive)
                        {
                            _windowService.ApplyPrivacyOverlay(window, window.Opacity);
                        }
                    }
                    else
                    {
                        _savedPrivateWindows.Remove(window.Title);
                        if (IsSessionActive)
                        {
                            _windowService.RemovePrivacyOverlay(window);
                        }
                    }
                    _configService.SavePrivateWindowNames(_savedPrivateWindows);
                }
                else if (e.PropertyName == nameof(WindowModel.Opacity))
                {
                    if (IsSessionActive && window.IsPrivate)
                    {
                        _windowService.UpdateOpacity(window, window.Opacity);
                    }
                }
            }
        }

        [RelayCommand]
        private void StartSession()
        {
            if (IsSessionActive) return;

            var privateWindows = Windows.Where(w => w.IsPrivate).ToList();
            if (!privateWindows.Any()) return;

            _savedPrivateWindows = privateWindows.Select(w => w.Title).ToHashSet();
            _configService.SavePrivateWindowNames(_savedPrivateWindows);

            foreach (var w in privateWindows)
            {
                _windowService.ApplyPrivacyOverlay(w, w.Opacity);
            }

            IsSessionActive = true;
        }

        [RelayCommand]
        private void StopSession()
        {
            if (!IsSessionActive) return;

            foreach (var w in Windows.Where(w => w.IsPrivate))
            {
                _windowService.RemovePrivacyOverlay(w);
            }

            IsSessionActive = false;
        }

        [RelayCommand]
        private void ToggleMirroring()
        {
            if (IsMirroringActive)
            {
                // Stop Mirroring
                _mirrorService.StopMirroring();
                _mirrorWindow?.Close();
                _mirrorWindow = null;
                IsMirroringActive = false;
            }
            else
            {
                // Start Mirroring
                var screens = System.Windows.Forms.Screen.AllScreens;
                if (screens.Length > 1)
                {
                    // Find the first non-primary screen to project onto
                    var targetScreen = screens.FirstOrDefault(s => !s.Primary) ?? screens[1];
                    
                    _mirrorWindow = new MirrorWindow(_mirrorService, targetScreen);
                    _mirrorWindow.Show();
                    
                    // Start capturing the primary screen (Display 0)
                    _mirrorService.StartMirroring(0);
                    IsMirroringActive = true;
                }
                else
                {
                    System.Windows.MessageBox.Show("A secondary monitor is required for screen mirroring to project onto.", "No Secondary Display Detected", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
            }
        }

        [RelayCommand]
        private void BringToFront(WindowModel window)
        {
            if (window != null)
            {
                _windowService.BringWindowToFront(window);
            }
        }
    }
}
