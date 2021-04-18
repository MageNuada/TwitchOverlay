using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ChatOverlay.Core
{
    public class LoadingView : Window
    {
        public LoadingView()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
