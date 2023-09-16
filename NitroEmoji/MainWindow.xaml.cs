using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using NitroEmoji.Client;
using NitroEmoji.Resize;

using WpfAnimatedGif;

namespace NitroEmoji;

public delegate double EmojiEvent(object sender, MouseEventArgs e);

public class GuildDisplay
{
    public GuildDisplay(PartialGuild p)
    {
        Title = p.name;
        Emojis = new ObservableCollection<Image>();
    }

    public string Title { get; set; }
    public ObservableCollection<Image> Emojis { get; set; }
    public bool IsExpanded { get; set; }

    public async Task AddEmoji(PartialEmoji e, DiscordClient source, MouseButtonEventHandler onClick,
        MouseEventHandler onMove)
    {
        Image img = new Image { Width = 48, Height = 48, ToolTip = ':' + e.name + ':', Tag = source.FromCache(e) };
        Emojis.Add(img);
        BitmapImage data = await source.EmojiFromCache(e);
        if (data == null)
        {
            return;
        }

        if (e.animated)
        {
            ImageBehavior.SetAnimatedSource(img, data);
        }
        else
        {
            img.Source = data;
        }

        img.MouseDown += onClick;
        img.MouseMove += onMove;
    }
}

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    public static RoutedCommand AcceptToken = new();
    public static RoutedCommand AddExtra = new();
    public static RoutedCommand DisplayHelp = new();

    private readonly DiscordClient C = new("cache");
    private readonly ObservableCollection<GuildDisplay> Servers = new();

    public MainWindow()
    {
        InitializeComponent();
        EmojiList.ItemsSource = Servers;
        AcceptToken.InputGestures.Add(new KeyGesture(Key.T, ModifierKeys.Control));
        DisplayHelp.InputGestures.Add(new KeyGesture(Key.F1));
    }

    private void HelpRequested(object sender, ExecutedRoutedEventArgs e)
    {
        string body =
            "Made with ♥ by Raffy E\n\nLogin using token: copy token and Ctrl+T\nAdd custom emoji: copy ID and Ctrl+N\nHelp/About: F1\n\nTo use an emoji in Discord, simply Drag and Drop the preferred emoji into a conversation window.";
        MessageBox.Show(body, "Help");
    }

    private bool ExtraExists()
    {
        return Servers.Count > 0 && Servers[0].Title == "Extra";
    }

    private async void AddExtraEmoji(object sender, ExecutedRoutedEventArgs e)
    {
        string id = Clipboard.GetText();
        if (!DiscordClient.IDValid(id))
        {
            StatusLabel.Content = "Invalid emoji ID";
            return;
        }

        MessageBoxResult x = MessageBox.Show("Is this emoji animated?", "New emoji", MessageBoxButton.YesNo);
        if (x == MessageBoxResult.Cancel)
        {
            return;
        }

        StatusLabel.Content = "Loading emoji...";
        PartialEmoji p = new PartialEmoji(id, "extra" + id, x == MessageBoxResult.Yes);

        GuildDisplay Extra;
        if (!ExtraExists())
        {
            Extra = new GuildDisplay(new PartialGuild("0", "Extra"));
            Servers.Insert(0, Extra);
        }
        else
        {
            Extra = Servers[0];
        }

        Extra.IsExpanded = true;

        await Extra.AddEmoji(p, C, EmojiClicked, EmojiDragged);
        if (p.animated)
        {
            await BulkResizer.ResizeGif(C.FromCache(p));
        }
        else
        {
            await BulkResizer.ResizePng(C.FromCache(p));
        }

        StatusLabel.Content = "Emoji added";
    }

    private void TokenChange(object sender, ExecutedRoutedEventArgs e)
    {
        string t = Clipboard.GetText().Trim('"');
        if (t.Length < 59)
        {
            StatusLabel.Content = "Invalid token";
            return;
        }

        AcceptToken.InputGestures.Clear();
        C.Token = t;
        LoginContainer.Visibility = Visibility.Hidden;
        LoadEmojis();
    }

    private async Task ResizeEmojis()
    {
        await BulkResizer.ResizeGifs(C.Cache);
        await BulkResizer.ResizePngs(C.Cache);
    }

    private void EmojiClicked(object sender, MouseEventArgs e)
    {
        Image img = sender as Image;
        StatusLabel.Content = img.ToolTip;
    }

    private void EmojiDragged(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        Image img = sender as Image;
        DragDrop.DoDragDrop(sender as DependencyObject,
            new DataObject(DataFormats.FileDrop, new string[1] { img.Tag.ToString() }),
            DragDropEffects.All);
    }

    private async Task DownloadEmojis()
    {
        foreach (PartialGuild g in C.Guilds)
        {
            GuildDisplay disp = new GuildDisplay(g);
            Servers.Add(disp);
            disp.IsExpanded = g.emojis.Count > 0;
            foreach (PartialEmoji e in g.emojis)
            {
                await disp.AddEmoji(e, C, EmojiClicked, EmojiDragged);
            }
        }
    }

    private async void LoadEmojis()
    {
        StatusLabel.Content = "Loading servers...";
        Progress.IsActive = true;
        Task<bool> GuildTask = C.GetGuilds();
        bool success = await GuildTask;
        if (!success)
        {
            Progress.IsActive = false;
            StatusLabel.Content = "Failed to load servers";
            return;
        }

        StatusLabel.Content = "Loading emojis...";
        Task<bool> LoadTask = C.LoadEmojis();
        success = await LoadTask;
        if (!success)
        {
            Progress.IsActive = false;
            StatusLabel.Content = "Failed to load emojis";
            return;
        }

        EmojiList.Visibility = Visibility.Visible;
        BrushConverter bc = new BrushConverter();
        StatusLabel.Background = bc.ConvertFrom("#BF000000") as Brush;
        await DownloadEmojis();
        StatusLabel.Content = "Resizing emojis...";
        await ResizeEmojis();
        Progress.IsActive = false;
        StatusLabel.Content = "Waiting";

        AddExtra.InputGestures.Add(new KeyGesture(Key.N, ModifierKeys.Control));
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        StatusLabel.Content = "Logging in...";
        Task<bool> Login = C.Login(EmailBox.Text, PasswordBox.Password);
        LoginButton.IsEnabled = false;
        bool success = await Login;
        if (!success)
        {
            StatusLabel.Content = "Login failed";
            LoginButton.IsEnabled = true;
        }
        else
        {
            AcceptToken.InputGestures.Clear();
            StatusLabel.Content = "Login successful";
            LoginContainer.Visibility = Visibility.Hidden;
            LoadEmojis();
        }
    }

    private void ClearDefault(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox)
        {
            PasswordBox t = sender as PasswordBox;
            if (t.Password == "Password")
            {
                t.Password = "";
            }
        }
        else
        {
            TextBox t = sender as TextBox;
            if (t.Text == "Email")
            {
                t.Text = "";
            }
        }
    }
}