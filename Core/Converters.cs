using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Linq;
using TwitchLib.Client.Models;

namespace ChatOverlay.Core
{
    public class IntervalConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is not double m) throw new NotSupportedException();
            return new Thickness(-5, -8 + m, 0, -5);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class MessageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is not ChatMessage m) throw new NotSupportedException();
            return $"{m.Message}";
            //return $"{(MainWindowViewModel.Instance.ShowMessageTime ? DateTime.Now.ToString("HH:mm:ss") : null)} {m.Message}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is not Color m) throw new NotSupportedException();
            if (parameter is string p)
            {
                //double.Parse(p, CultureInfo.InvariantCulture);
                if (double.TryParse(p, out var d))
                    return new SolidColorBrush(m, d);
            }

            return new SolidColorBrush(m);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if(value is not SolidColorBrush c) throw new DataValidationException("invalid data");
            return c.Color;
        }
    }

    public class AlphaConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var e = parameter as BindingBase;
            var c = e.DefaultAnchor.Target as StyledElement;
            var brushes = (c.DataContext as SettingsWindowViewModel).Colors;
            if (value is not Color m) throw new NotSupportedException();
            return brushes.FirstOrDefault(x => x.R == m.R && x.G == m.G && x.B == m.B);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var e = parameter as BindingBase;
            var c = e.DefaultAnchor.Target as StyledElement;
            byte transparency = (c.DataContext as SettingsWindowViewModel).Transparency;
            if (value is not Color m) throw new DataValidationException("invalid data");
            return new Color(transparency, m.R, m.G, m.B);
        }
    }

    public class MarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is not Thickness m) throw new NotSupportedException();
            return m.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if(value is not string s) throw new DataValidationException("invalid data");
            Thickness result = new();
            try
            {
                result = Thickness.Parse(s);
            }
            catch (Exception)
            {
                //throw new DataValidationException(e.Message);
            }
            return result;
        }
    }

    public class ThicknessConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Thickness);
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            var jObj = JObject.Load(reader);
            if (jObj == null) return new Thickness();
            return new Thickness(jObj["Left"].ToObject<double>(), jObj["Top"].ToObject<double>(),
                jObj["Right"].ToObject<double>(), jObj["Bottom"].ToObject<double>());
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}
