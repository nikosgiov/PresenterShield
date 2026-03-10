using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PresenterShield.Models;
using PresenterShield.Services;
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
        private HashSet<string> _savedPrivateWindows;
        private DispatcherTimer _refreshTimer;

        [ObservableProperty]
        private ObservableCollection<WindowModel> windows = new();

        [ObservableProperty]
        private byte overlayOpacity = 128; // 50% opacity default

        [ObservableProperty]
        private bool isSessionActive;

        public MainViewModel()
        {
            _windowService = new WindowService();
            _configService = new ConfigService();
            _savedPrivateWindows = _configService.LoadPrivateWindowNames();
            RefreshWindows();

            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(2);
            _refreshTimer.Tick += (s, e) => RefreshWindows();
            _refreshTimer.Start();
        }

        partial void OnOverlayOpacityChanged(byte value)
        {
            if (IsSessionActive)
            {
                foreach (var w in Windows.Where(w => w.IsPrivate && !w.UseCustomOpacity))
                {
                    _windowService.UpdateOpacity(w, value);
                }
            }
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
                        _windowService.ApplyPrivacyOverlay(w, w.UseCustomOpacity ? w.Opacity : OverlayOpacity);
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
                            _windowService.ApplyPrivacyOverlay(window, window.UseCustomOpacity ? window.Opacity : OverlayOpacity);
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
                else if (e.PropertyName == nameof(WindowModel.Opacity) || e.PropertyName == nameof(WindowModel.UseCustomOpacity))
                {
                    if (IsSessionActive && window.IsPrivate)
                    {
                        _windowService.UpdateOpacity(window, window.UseCustomOpacity ? window.Opacity : OverlayOpacity);
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
                _windowService.ApplyPrivacyOverlay(w, w.UseCustomOpacity ? w.Opacity : OverlayOpacity);
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
        private void BringToFront(WindowModel window)
        {
            if (window != null)
            {
                _windowService.BringWindowToFront(window);
            }
        }
    }
}
