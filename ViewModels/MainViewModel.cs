using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PresenterShield.Models;
using PresenterShield.Services;
using System.Collections.ObjectModel;
using System.Linq;

namespace PresenterShield.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly WindowService _windowService;
        private readonly ConfigService _configService;
        private HashSet<string> _savedPrivateWindows;

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
        }

        [RelayCommand]
        private void RefreshWindows()
        {
            if (IsSessionActive) return;
            
            var openWindows = _windowService.GetOpenWindows();
            
            var currentPrivates = Windows.Where(w => w.IsPrivate).Select(w => w.Handle).ToHashSet();

            Windows.Clear();
            foreach (var w in openWindows)
            {
                if (currentPrivates.Contains(w.Handle) || _savedPrivateWindows.Contains(w.Title))
                {
                    w.IsPrivate = true;
                }
                Windows.Add(w);
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
                _windowService.ApplyPrivacyOverlay(w, OverlayOpacity);
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
    }
}
