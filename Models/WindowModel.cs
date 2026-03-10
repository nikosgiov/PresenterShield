using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace PresenterShield.Models
{
    public partial class WindowModel : ObservableObject
    {
        public IntPtr Handle { get; }
        
        [ObservableProperty]
        private string title;
        
        [ObservableProperty]
        private string className;

        [ObservableProperty]
        private bool isPrivate;

        [ObservableProperty]
        private byte opacity = 128;

        public uint ProcessId { get; }

        public WindowModel(IntPtr handle, uint processId, string title, string className)
        {
            Handle = handle;
            ProcessId = processId;
            this.title = title;
            this.className = className;
        }
    }
}
