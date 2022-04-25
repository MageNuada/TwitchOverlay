using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Newtonsoft.Json;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Api.V5.Models.Badges;
using TwitchLib.Api.V5.Models.Chat;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

namespace ChatOverlay.Core
{
    public class TimedMessage : ReactiveObject
    {
        public TimedMessage(ChatMessage message, Color background, IEnumerable<Bitmap> badges = null, Dictionary<string, object> emotes = null)
        {
            Message = message;
            if (long.TryParse(Message.TmiSentTs, out long ts))
                Time = DateTimeOffset.FromUnixTimeMilliseconds(ts).ToLocalTime();
            else
                Time = DateTime.Now;
            if (badges != null)
                Badges.AddRange(badges);
            Background = background;
            Emotes = emotes;
            RenderText();
        }

        private void AddText(string text, FormattedTextLine[] lines, ref int lineIndex, ref int currentLength)
        {
            int diff = text.Length + currentLength - lines[lineIndex].Length;

            if (diff <= 0)
            {
                if (!string.IsNullOrEmpty(text))
                    Elements.Add(text);
                currentLength += text.Length;
            }
            else
            {
                var part = text.Substring(0, text.Length - diff);
                AddText(part, lines, ref lineIndex, ref currentLength);

                part = text.Substring(text.Length - diff, diff);
                AddText(part, lines, ref lineIndex, ref currentLength);
            }

            if (lineIndex < lines.Length && currentLength == lines[lineIndex].Length)
            {
                lineIndex++;
                currentLength = 0;
            }
        }

        internal void RenderText()
        {
            try
            {
                Elements.Clear();
                double width = MainWindowViewModel.Instance.MessageWidth;

                FormattedText formattedText = new();
                formattedText.Constraint = new Size(width, 0);
                formattedText.TextWrapping = TextWrapping.Wrap;
                formattedText.FontSize = 14;
                formattedText.Typeface = new Typeface("Times New Roman", FontStyle.Normal, FontWeight.Normal);
                string text = Message.Message, preparedText = Message.Message;
                FormattedTextLine[] lines;
                int currentLength = 0;
                int lineIndex = 0;
                if (Emotes == null || Emotes.Count == 0)
                {
                    formattedText.Text = text;
                    lines = formattedText.GetLines().ToArray();
                    AddText(text, lines, ref lineIndex, ref currentLength);
                    return;
                }

                string emoteName = "";
                int count = 0;
                foreach (var emote in Emotes)
                {
                    text = text.Replace(emote.Key, "____");
                    int seekIndex = -1;
                    while ((seekIndex = preparedText.IndexOf(emote.Key, seekIndex + 1)) != -1)
                        count++;
                }
                formattedText.Text = text;
                lines = formattedText.GetLines().ToArray();
                string textElement;
                while (count-- > 0)
                {
                    var index = preparedText.Length;
                    foreach (var emote in Emotes)
                    {
                        var newIndex = preparedText.IndexOf(emote.Key);
                        if (newIndex == -1) continue;
                        if (newIndex > index) continue;
                        index = newIndex;
                        emoteName = emote.Key;
                    }

                    textElement = preparedText.Substring(0, index);
                    AddText(textElement, lines, ref lineIndex, ref currentLength);

                    preparedText = preparedText.Remove(0, index);
                    preparedText = preparedText.Remove(0, emoteName.Length);
                    var image = Emotes[emoteName];
                    if (image is byte[] bytes)
                    {
                        MemoryStream stream = new(bytes);
                        image = stream;
                    }
                    Elements.Add(image);
                    currentLength += 4;
                }

                AddText(preparedText, lines, ref lineIndex, ref currentLength);
            }
            catch (Exception e)
            {
                App.Log.Trace($"text render error: ", e);
            }
        }

        //[Reactive]
        //public Image RenderedText { get; set; }
        [Reactive]
        public Color Background { get; set; }
        public Dictionary<string, object> Emotes { get; }
        public ChatMessage Message { get; }
        public DateTimeOffset Time { get; }
        public AvaloniaList<Bitmap> Badges { get; } = new ();
        public AvaloniaList<object> Elements { get; } = new () { ResetBehavior = ResetBehavior.Reset };
    }

    public class MainWindowViewModel : ReactiveObject
    {
        private const string _clientId = "c0dsmc5zy90g7vfk524rc67w0fn9ah";
        private const string _secret = "doq72cbzjfc3pm24kcaqvs3jn8rmxj";
        private const string _oauth = "oauth:hfnhxsoodjl34ymddzgw1sh6jmx6b8";

        private TwitchAPI API { get; set; }

        private TwitchClient? _client = null;
        private GlobalBadgesResponse _globalBadges;
        private ChannelDisplayBadges _channelBadges;
        private ChannelBadges _channelBadges2;
        private readonly Dictionary<string, object> _emotes = new();
        private readonly Dictionary<string, Bitmap> _badges = new();
        private readonly object _lock = new(), _logLock = new();

        public static MainWindowViewModel Instance { get; private set; }

        public MainWindowViewModel()
        {
            Instance = this;

            this.WhenAnyValue(x => x.ChatWidth).Subscribe(x => { MessageWidth = x - 6; });
            ChatHeight = 550;
            ChatWidth = 300;
            MessageColor = Colors.WhiteSmoke;
            var color = Colors.Black;
            OverlayColor = new Color(80, color.R, color.G, color.B);
            VerticalAlignment = VerticalAlignment.Top;
            HorizontalAlignment = HorizontalAlignment.Left;
            Margin = Thickness.Parse("20");
            ShowSettingsOnStartUp = true;
            ChannelName = "";
            MessageTTL = 10;
            ShowMessageTime = true;
            SeparatorColor = Colors.Black;
            HighlightedMessageColor = Colors.DarkGoldenrod;
            HighlightedMessagesPattern = "client;@your_channel_name";
            TextShadow = false;

            Messages.AddRange(new[]
            {
                    new TimedMessage(new ChatMessage("client01","id0", "client", "client", HexConverter(Colors.Red),
                    System.Drawing.Color.Red, null, $"welcome to chat!",
                    TwitchLib.Client.Enums.UserType.Viewer, "my channel", "01234", true, 1, null, false, false,
                    false, false, false, false, false, TwitchLib.Client.Enums.Noisy.False, "", null, null, null, 0, 0),
                    Colors.Transparent
                    //,new [] {img, img }
                    ),
                    new TimedMessage(new ChatMessage("client01","id0", "client", "client", HexConverter(Colors.Red),
                    System.Drawing.Color.Red, null, $"client version {Version}!",
                    TwitchLib.Client.Enums.UserType.Viewer, "my channel", "01234", true, 1, null, false, false,
                    false, false, false, false, false, TwitchLib.Client.Enums.Noisy.False, "", null, null, null, 0, 0),
                    Colors.Transparent
                    //,new [] {img, img }
                    ),
                });

            if (Design.IsDesignMode)
            {
                //Bitmap img = new Bitmap("C://Temp//123.bmp");
                Messages.Add(new TimedMessage(new ChatMessage("client01", "id0", "client", "client", HexConverter(Colors.Red),
                System.Drawing.Color.Red, null, $"wefvcdv wgvwelnrvweknwefv wefvnefvw wefvne v234 vewe2rv v224rv vew avcasfvsd vsdfv sdfv sdf bsdgsdgb dsbsdbsd bsdgbsdf bsdbsdfd bsdbsdf bdsbsdfsdbf",
                TwitchLib.Client.Enums.UserType.Viewer, "my channel", "01234", true, 1, null, false, false,
                false, false, false, false, false, TwitchLib.Client.Enums.Noisy.False, "", null, null, null, 0, 0),
                HighlightedMessageColor
                //,new [] {img, img }
                ));
                return;
            }
        }

        private static string HexConverter(Color c)
        {
            return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
        }

        internal async Task Init()
        {
            try
            {
                await Task.Run(async () =>
                {
                    ConnectionCredentials credentials = new("nuadasunriser", _oauth);
                    var clientOptions = new ClientOptions
                    {
                        MessagesAllowedInPeriod = 750,
                        ThrottlingPeriod = TimeSpan.FromSeconds(30),
                        //UseSsl = true
                    };
                    API = new TwitchAPI();
                    API.Settings.ClientId = _clientId;
                    API.Settings.Secret = _secret;
                    API.Settings.AccessToken = _oauth;

                    WebSocketClient customClient = new(clientOptions);
                    _client = new TwitchClient(customClient);
                    _client.Initialize(credentials, ChannelName);

                    _client.OnLog += (s, e) =>
                    {
                        LogMessages.Add($"{e.DateTime:HH:mm:ss}: {e.Data}");
                        if (LogMessages.Count > 100) LogMessages.RemoveRange(0, 50);
                    };
                    _client.OnMessageReceived += Client_OnMessageReceived;
                    _client.OnNewSubscriber += (s, e) =>
                    {
                        lock (_lock)
                        {
                            var message = new ChatMessage(e.Channel, "id0000", "client", "client", HexConverter(Colors.Red), System.Drawing.Color.Red, null,
                            $"User {e.Subscriber.DisplayName} made subscription {e.Subscriber.SubscriptionPlan} to channel '{e.Channel}'!",
                            TwitchLib.Client.Enums.UserType.Viewer, "my channel", "01234", true, 1, null, false, false,
                            false, false, false, false, false, TwitchLib.Client.Enums.Noisy.False, "rawircmsg", null, null, null, 0, 0);
                            Color color = CheckPattern(message);
                            Messages.Add(new TimedMessage(message, color));
                        }
                        Workaround();
                    };
                    _client.OnJoinedChannel += (s, e) =>
                    {
                        lock (_lock)
                        {
                            var message = new ChatMessage(e.BotUsername, "id0000", "client", "client", HexConverter(Colors.Red), System.Drawing.Color.Red, null,
                            $"client joined channel '{e.Channel}'!",
                            TwitchLib.Client.Enums.UserType.Viewer, "my channel", "01234", true, 1, null, false, false,
                            false, false, false, false, false, TwitchLib.Client.Enums.Noisy.False, "rawircmsg", null, null, null, 0, 0);
                            Color color = CheckPattern(message);
                            Messages.Add(new TimedMessage(message, color));
                        }
                        Workaround();

                        Task.Run(() =>
                        {
                            var user = API.V5.Users.GetUserByNameAsync(ChannelName).Result;

                            _channelBadges = API.V5.Badges.GetSubscriberBadgesForChannelAsync(user.Matches[0].Id).Result;
                            _channelBadges2 = API.V5.Chat.GetChatBadgesByChannelAsync(user.Matches[0].Id).Result;
                        });
                    };
                    _client.OnConnected += (s, e) => { lock (_logLock) App.Log.Trace("netclient connected."); };
                    _client.OnError += (s, e) => { lock (_logLock) App.Log.Trace($"netclient error: ", e.Exception); };
                    _client.OnDisconnected += (s, e) => { lock (_logLock) App.Log.Trace($"netclient disconnected."); };
                    _client.OnConnectionError += (s, e) => { lock (_logLock) App.Log.Trace($"netclient connection error: {e.Error?.Message}"); };
                    _client.OnFailureToReceiveJoinConfirmation += (s, e) =>
                    {
                        lock (_logLock) App.Log.Trace($"netclient channel connection error: {e.Exception.Channel} {e.Exception.Details}");
                    };
                    _client.OnLeftChannel += (s, e) =>
                    {
                        lock (_logLock) App.Log.Trace($"netclient leave channel: {e.Channel}");
                    };
                    _client.OnNoPermissionError += (s, e) =>
                    {
                        lock (_logLock) App.Log.Trace($"netclient permissions error.");
                    };
                    _client.OnIncorrectLogin += (s, e) =>
                    {
                        lock (_logLock) App.Log.Trace($"netclient incorrect login:", e.Exception);
                    };
                    _client.OnChannelStateChanged += (s, e) =>
                    {
                        lock (_logLock) App.Log.Trace($"channel state shanged: {e.Channel} {e.ChannelState}");
                    };
                    _client.OnUserLeft += (s, e) =>
                    {
                        lock (_logLock) App.Log.Trace($"netclient leave user: {e.Username} {e.Channel}");
                    };
                    _client.OnRaidNotification += (s, e) =>
                    {
                        lock (_lock)
                        {
                            var message = new ChatMessage(e.Channel, "id0000", "client", "client", HexConverter(Colors.Red), System.Drawing.Color.Red, null,
                            $"User {e.RaidNotification.DisplayName} made rade!",
                            TwitchLib.Client.Enums.UserType.Viewer, "my channel", "01234", true, 1, null, false, false,
                            false, false, false, false, false, TwitchLib.Client.Enums.Noisy.False, "rawircmsg", null, null, null, 0, 0);
                            Color color = CheckPattern(message);
                            Messages.Add(new TimedMessage(message, color));
                        }
                        Workaround();
                    };

                    _client.Connect();

                    _globalBadges = await API.V5.Badges.GetGlobalBadgesAsync();
                });

                //working
                //var v1 = API.V5.Users.GetUserByNameAsync("ihatsutv").Result;
                //var v2 = API.V5.Badges.GetSubscriberBadgesForChannelAsync(v1.Matches[0].Id).Result;
                //var v3 = API.V5.Chat.GetChatBadgesByChannelAsync(v1.Matches[0].Id).Result;

                //not working
                //var v4 = API.V5.Users.GetUserEmotesAsync(v1.Matches[0].Id).Result;
                //var v5 = API.V5.Chat.GetChatEmoticonsBySetAsync().Result;
                //var i1 = e1.Channels.GetChannelInformationAsync("ihatsutv").Result;

                this.WhenAnyValue(x => x.HighlightedMessageColor).Subscribe(x =>
                {
                    lock (_lock)
                        foreach (var message in Messages)
                            if (message.Background != Colors.Transparent)
                                message.Background = x;
                });

                this.WhenAnyValue(x => x.MessagesInterval, x => x.ChatHeight, x => x.ChatWidth).Subscribe(x => Workaround());

                this.WhenAnyValue(x => x.ChatWidth).Subscribe(x =>
                {
                    lock (_lock)
                        foreach (var m in Messages)
                            m.RenderText();
                });

                _ = Task.Run(() =>
                {
                    while (true)
                    {
                        Thread.Sleep(100);
                        if (MessageTTL == 0) continue;
						Dispatcher.UIThread.InvokeAsync(() =>
						{
							lock (_lock)
							{
								int i = 0;
								while (i < Messages.Count && (DateTime.Now - Messages[i].Time).TotalSeconds > MessageTTL) i++;
								if (i > 0) Messages.RemoveRange(0, i);
								//while (Messages.Count > 0 && (DateTime.Now - Messages[0].Time).TotalSeconds > MessageTTL)
								//{
								//    Messages.RemoveAt(0);
								//}
							}
						});
                    }
                });
            }
            catch (Exception e)
            {
                lock (_logLock) App.Log.Trace($"main window init error: ", e);
            }
        }

        public void SetChannel()
        {
            if (!_client.IsConnected) return;

            foreach (var c in _client.JoinedChannels)
                _client.LeaveChannel(c);

            if (!string.IsNullOrWhiteSpace(ChannelName))
                _client.JoinChannel(ChannelName, true);
        }

        private static void ExitCommand()
        {
            if (Application.Current.ApplicationLifetime is IControlledApplicationLifetime controlled)
            {
                controlled.Shutdown();
            }
        }

        private void Client_OnMessageReceived(object? sender, OnMessageReceivedArgs e)
        {
            List<Bitmap> badges = new();
            Dictionary<string, object> emotes = new();
            try
            {
#if LOGGING
                lock (_logLock) App.Log.Trace($"badges {_globalBadges == null} {_channelBadges == null}");
#endif
                using WebClient client = new();

                if (e.ChatMessage.Badges != null && _globalBadges != null && _channelBadges != null)
                    foreach (var b in e.ChatMessage.Badges)
                    {
                        var value = b.Value;
                        //в этом случае далее не находит индексов, что странно.
                        //возможно, в будущем стоит попробовать сделать выбор первого попавшегося.
                        //var index = e.ChatMessage.BadgeInfo.FindIndex(x => x.Key == b.Key);
                        //if (index != -1)
                        //    value = e.ChatMessage.BadgeInfo[index].Value;
                        TwitchLib.Api.V5.Models.Badges.Badge badge = null;
                        if (b.Key == "subscriber")
                        {
                            badge = _channelBadges.Sets.Subscriber;
                        }
                        if (badge == null && !_globalBadges.Sets.TryGetValue(b.Key, out badge)) continue;
                        if(badge == null || badge.Versions == null)
                        {
                            lock (_logLock) App.Log.Trace($"No badge for {b.Key}");
                            continue;
                        }
                        if (!badge.Versions.TryGetValue(value, out var versionedBadge)) continue;
                        if (versionedBadge == null) continue;
                        if (_badges.TryGetValue(versionedBadge.Image_Url_1x, out var image))
                            badges.Add(image);
                        else
                        {
                            var bytes = client.DownloadData(new Uri(versionedBadge.Image_Url_1x));
                            if (bytes == null) continue;
                            using MemoryStream s = new(bytes);
                            image = new(s);
                            badges.Add(image);
                            _badges.Add(versionedBadge.Image_Url_1x, image);
                        }
                    }

#if LOGGING
                lock (_logLock) App.Log.Trace($"Emotes: {e.ChatMessage.EmoteSet == null} {(e.ChatMessage.EmoteSet == null ? 0 : e.ChatMessage.EmoteSet.Emotes.Count)}");
#endif
                if (e.ChatMessage.EmoteSet != null)
                    foreach (var emote in e.ChatMessage.EmoteSet.Emotes)
                    {
#if LOGGING
                        lock (_logLock) App.Log.Trace(emote.Name);
#endif
                        if (!_emotes.ContainsKey(emote.Name))
                        {
                            bool isAnimated = !int.TryParse(emote.Id, out _);
                            var url = emote.ImageUrl;
                            if (isAnimated)
                                url = url.Replace("/v1/", "/v2/").Replace("/1.0", "/default/light/1.0");
                            var bytes = client.DownloadData(new Uri(url));
                            if (bytes == null) continue;
                            //temp fix
                            var slice = bytes[1..4];
                            if (slice.SequenceEqual(System.Text.Encoding.ASCII.GetBytes("png")) ||
                                slice.SequenceEqual(System.Text.Encoding.ASCII.GetBytes("PNG")))
                                isAnimated = false;
                            MemoryStream s = new(bytes);
                            _emotes[emote.Name] = isAnimated ? bytes : new Bitmap(s);
                        }
                        emotes[emote.Name] = _emotes[emote.Name];
                    }
            }
            catch (Exception ex)
            {
                lock (_logLock) App.Log.Trace("unexpected error ", ex);
            }

            lock (_lock)
            {
                Color color = CheckPattern(e.ChatMessage);
                Messages.Add(new TimedMessage(e.ChatMessage, color, badges, emotes));
                if (Messages.Count > 100)
                    Dispatcher.UIThread.InvokeAsync(() => { Messages.RemoveRange(0, 50); });
            }
            Workaround();
        }

        private Color CheckPattern(ChatMessage chat)
        {
            var color = Colors.Transparent;
            if (!string.IsNullOrEmpty(HighlightedMessagesPattern))
            {
                foreach (var word in HighlightedMessagesPattern.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (chat.Username != word && !chat.Message.Contains(word))
                        continue;
                    color = HighlightedMessageColor;
                    break;
                }
            }

            return color;
        }

        private void Workaround()
        {
            Task.Run(() =>
            {
                Thread.Sleep(50);
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (TS is ScrollViewer sv)
                        lock (_lock)
                        {
                            sv.ScrollToEnd();
                            sv.PageDown();
                        }
                });
            });
        }

        public string Version { get; } = Assembly.GetCallingAssembly().GetName().Version.ToString();

        [Reactive, JsonProperty, JsonConverter(typeof(ThicknessConverter))]
        public Thickness Margin { get; set; }

        [Reactive, JsonProperty]
        public float ChatHeight { get; set; }

        [Reactive, JsonProperty]
        public float ChatWidth { get; set; }

        [Reactive]
        public Color MessageColor { get; set; }

        [Reactive, JsonProperty]
        public Color OverlayColor { get; set; }

        [Reactive, JsonProperty]
        public Color SeparatorColor { get; set; }

        [Reactive, JsonProperty]
        public string HighlightedMessagesPattern { get; set; }

        [Reactive, JsonProperty]
        public Color HighlightedMessageColor { get; set; }

        [Reactive, JsonProperty]
        public VerticalAlignment VerticalAlignment { get; set; }

        [Reactive, JsonProperty]
        public HorizontalAlignment HorizontalAlignment { get; set; }

        [Reactive, JsonProperty]
        public bool ShowSettingsOnStartUp { get; set; }

        [Reactive, JsonProperty]
        public string ChannelName { get; set; }

        [Reactive, JsonProperty]
        public double MessageTTL { get; set; }

        [Reactive, JsonProperty]
        public bool ShowMessageTime { get; set; }

        [Reactive, JsonProperty]
        public double MessagesInterval { get; set; }

        [Reactive, JsonProperty]
        public bool TextShadow { get; set; }

#region internal properties

        public AvaloniaList<TimedMessage> Messages { get; set; } = 
            new AvaloniaList<TimedMessage>() { ResetBehavior = ResetBehavior.Remove };

        public AvaloniaList<string> LogMessages { get; set; } =
            new AvaloniaList<string>() { ResetBehavior = ResetBehavior.Remove };

        [Reactive]
        public int ListSelectionStub { get; set; }

        [Reactive]
        public float MessageWidth { get; set; }

        [Reactive]
        internal Avalonia.Controls.Primitives.IScrollable? TS { get; set; }

#endregion
    }
}
