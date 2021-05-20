using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace ChatOverlay.Core
{
    public class SettingsWindowViewModel : ReactiveObject
    {
        public string Version { get; } = "0.4.2";

        public SettingsWindowViewModel()
        {
            if (Design.IsDesignMode)
                TargetViewModel = new MainWindowViewModel();

            Type colorType = typeof(Colors);
            // We take only static property to avoid properties like Name, IsSystemColor ...
            PropertyInfo[] propInfos = colorType.GetProperties(BindingFlags.Static | BindingFlags.DeclaredOnly | BindingFlags.Public);
            foreach (PropertyInfo propInfo in propInfos)
            {
                Colors.Add((Color)propInfo.GetValue(null));
            }
        }

        public SettingsWindowViewModel(MainWindowViewModel? viewModel) : this()
        {
            if (viewModel == null)
                throw new ArgumentNullException(nameof(viewModel));
            TargetViewModel = viewModel;
            Transparency = TargetViewModel.OverlayColor.A;
            ChannelName = TargetViewModel.ChannelName;

            if (Design.IsDesignMode) return;

            this.WhenAnyValue(x => x.Transparency).Subscribe(x =>
            {
                var m = TargetViewModel.OverlayColor;
                TargetViewModel.OverlayColor = new Color(x, m.R, m.G, m.B);
            });
        }

        private void ChangeChannelCommand()
        {
            if (Design.IsDesignMode) return;

            TargetViewModel.ChannelName = ChannelName;
            TargetViewModel.SetChannel();
        }

        [Reactive]
        public MainWindowViewModel TargetViewModel { get; set; }

        [Reactive]
        public byte Transparency { get; set; }

        [Reactive]
        public string ChannelName { get; set; }

        public EnumViewModel VerticalAligmentEnum { get; } = new EnumViewModel(typeof(VerticalAlignment));

        public EnumViewModel HorizontalAligmentEnum { get; } = new EnumViewModel(typeof(HorizontalAlignment));

        public List<Color> Colors { get; } = new List<Color>();
    }
}
