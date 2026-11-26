using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Net.Http;
using System.Text.Json;
using System.Management;
using Microsoft.Win32;

namespace FortniteBoosterPRO;

internal sealed class UserProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Игрок";
    public string EpicNickname { get; set; } = "";
    public string GamePath { get; set; } = "";
    public string LaunchOptions { get; set; } = "";
    public string PerformanceMode { get; set; } = "Сбалансированный";
}

internal sealed class GameEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Игра";
    public string ExePath { get; set; } = "";
    public string ArtworkPath { get; set; } = "";
    public bool IsFortnite { get; set; }
}
internal sealed class AppData
{
    public string ActiveProfileId { get; set; } = "";
    public List<UserProfile> Profiles { get; set; } = new();
    public List<GameEntry> Games { get; set; } = new();
}

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed class MainForm : Form
{
    private readonly Color bg = Color.FromArgb(13, 15, 20);
    private readonly Color panel = Color.FromArgb(22, 25, 32);
    private readonly Color panel2 = Color.FromArgb(30, 34, 43);
    private readonly Color text = Color.FromArgb(242, 245, 250);
    private readonly Color muted = Color.FromArgb(145, 154, 169);
    private readonly Color blue = Color.FromArgb(39, 183, 235);
    private readonly string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FortniteBoosterPRO");
    private string DataPath => Path.Combine(root, "profiles.json");
    private string CoverPath => Path.Combine(root, "fortnite-live.jpg");
    private readonly string newsUrl = "https://fortnite-api.com/v2/news?language=ru";
    private readonly string epicPage = "https://store.epicgames.com/p/fortnite";
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(18) };
    private AppData data = new();
    private UserProfile Current => data.Profiles.First(p => p.Id == data.ActiveProfileId);
    private Image? cover;
    private readonly List<Image> banners = new();
    private readonly List<string> bannerTitles = new();
    private int bannerIndex;
    private System.Windows.Forms.Timer bannerTimer = null!;
    private Label bannerHeadline = null!;
    private Label bannerCounter = null!;
    private Panel content = null!;
    private Panel hero = null!;
    private Panel? gameHero;
    private FlowLayoutPanel? gameGrid;
    private Label profileButton = null!;
    private Label coverStatus = null!;
    private System.Windows.Forms.Timer refreshTimer = null!;
    private string currentPage = "library";

    public MainForm()
    {
        Directory.CreateDirectory(root);
        Text = "Fortnite Booster PRO";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1460, 920);
        MinimumSize = new Size(1120, 760);
        BackColor = bg;
        ForeColor = text;
        Font = new Font("Segoe UI", 9.5f);
        DoubleBuffered = true;
        LoadData();
        LoadCachedBanners();
        BuildShell();
        try
        {
            string desktopShortcut = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "Fortnite Booster PRO.lnk");
            if (!File.Exists(desktopShortcut)) CreateDesktopShortcut();
        }
        catch { }
        ShowLibrary();
        Shown += async (_, _) => await RefreshArtworkAsync();
        refreshTimer = new System.Windows.Forms.Timer { Interval = 6 * 60 * 60 * 1000 };
        refreshTimer.Tick += async (_, _) => await RefreshArtworkAsync();
        refreshTimer.Start();
        bannerTimer = new System.Windows.Forms.Timer { Interval = 8500 };
        bannerTimer.Tick += (_, _) =>
        {
            if (banners.Count < 2) return;
            bannerIndex = (bannerIndex + 1) % banners.Count;
            UpdateBannerPresentation();
        };
        bannerTimer.Start();
    }

    private void LoadData()
    {
        try
        {
            if (File.Exists(DataPath))
                data = JsonSerializer.Deserialize<AppData>(File.ReadAllText(DataPath)) ?? new();
        }
        catch { data = new(); }
        if (data.Profiles.Count == 0)
        {
            var p = new UserProfile { Name = "Основной профиль", GamePath = FindFortniteExe() };
            data.Profiles.Add(p);
            data.ActiveProfileId = p.Id;
            SaveData();
        }
        if (!data.Profiles.Any(p => p.Id == data.ActiveProfileId))
            data.ActiveProfileId = data.Profiles[0].Id;
        if (data.Games == null) data.Games = new List<GameEntry>();
        if (!data.Games.Any(g => g.IsFortnite))
            data.Games.Insert(0, new GameEntry { Name = "Fortnite", ExePath = Current.GamePath, ArtworkPath = CoverPath, IsFortnite = true });
    }

    private void SaveData() =>
        File.WriteAllText(DataPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));

    private void BuildShell()
    {
        // Fixed two-column shell: navigation and page content never overlap.
        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, BackColor = bg, ColumnCount = 2, RowCount = 2,
            Margin = Padding.Empty, Padding = Padding.Empty
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var top = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(10, 12, 18), Margin = Padding.Empty };
        try
        {
            using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("FortniteBoosterPRO.Assets.FortniteBoosterPRO.png");
            if (stream != null)
            {
                using var image = Image.FromStream(stream);
                top.Controls.Add(new PictureBox
                {
                    Image = new Bitmap(image), Location = new Point(24, 10), Size = new Size(54, 54),
                    SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent
                });
            }
        }
        catch { }

        top.Controls.Add(new Label
        {
            Text = "FORTNITE BOOSTER PRO", Location = new Point(88, 21), Size = new Size(270, 34),
            Font = new Font("Segoe UI", 15, FontStyle.Bold), ForeColor = text, BackColor = Color.Transparent
        });

        var search = new TextBox
        {
            Size = new Size(220, 34), BackColor = panel, ForeColor = text,
            BorderStyle = BorderStyle.FixedSingle, PlaceholderText = "⌕  Поиск в библиотеке",
            Font = new Font("Segoe UI", 9)
        };
        search.TextChanged += (_, _) =>
        {
            if (currentPage == "library" && hero != null)
            {
                bool show = string.IsNullOrWhiteSpace(search.Text) ||
                    "fortnite".Contains(search.Text.Trim(), StringComparison.OrdinalIgnoreCase);
                hero.Visible = show;
                if (gameGrid != null)
                    foreach (Control card in gameGrid.Controls)
                    {
                        string name = card.Tag as string ?? "";
                        card.Visible = string.IsNullOrWhiteSpace(search.Text) ||
                            name.Contains(search.Text.Trim(), StringComparison.OrdinalIgnoreCase);
                    }
            }
        };
        top.Controls.Add(search);

        profileButton = new Label
        {
            Size = new Size(170, 36), BackColor = panel2, ForeColor = text,
            TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        profileButton.Click += (_, _) => ShowProfiles();
        top.Controls.Add(profileButton);

        var settings = MakeButton("⚙", 0, 19, 42, 38, panel2, text);
        settings.Font = new Font("Segoe UI Symbol", 15);
        settings.Click += (_, _) => ShowSettings();
        top.Controls.Add(settings);

        var boost = MakeButton("BOOST", 0, 19, 88, 38, blue, Color.FromArgb(5, 20, 29));
        boost.Click += (_, _) => ShowBoost();
        top.Controls.Add(boost);

        void LayoutHeader()
        {
            settings.Location = new Point(top.ClientSize.Width - 58, 19);
            boost.Location = new Point(top.ClientSize.Width - 158, 19);
            profileButton.Location = new Point(top.ClientSize.Width - 350, 20);
            search.Location = new Point(top.ClientSize.Width - 590, 21);
        }
        top.Resize += (_, _) => LayoutHeader();
        LayoutHeader();

        var sidebar = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(19, 22, 29), Margin = Padding.Empty };
        sidebar.Controls.Add(new Label
        {
            Text = "БИБЛИОТЕКА", Location = new Point(23, 27), Size = new Size(175, 22),
            ForeColor = muted, Font = new Font("Segoe UI", 8, FontStyle.Bold)
        });
        AddNav(sidebar, "▦   Главная", 68, () => ShowLibrary());
        AddNav(sidebar, "◈   Fortnite", 116, () => ShowGameDetails());
        AddNav(sidebar, "▣   Мой ПК", 164, () => ShowPcSpecs());
        AddNav(sidebar, "⚙   Настройки", 212, () => ShowSettings());
        sidebar.Controls.Add(new Label
        {
            Text = "FORTNITE BOOSTER PRO\nBUILD 2.1  •  WINDOWS",
            Location = new Point(22, 0), Size = new Size(180, 44),
            ForeColor = Color.FromArgb(111, 121, 143), Font = new Font("Segoe UI", 7, FontStyle.Bold),
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom
        });

        content = new Panel
        {
            Dock = DockStyle.Fill, BackColor = bg, AutoScroll = true,
            Padding = new Padding(28, 24, 28, 25), Margin = Padding.Empty
        };

        shell.Controls.Add(top, 0, 0);
        shell.SetColumnSpan(top, 2);
        shell.Controls.Add(sidebar, 0, 1);
        shell.Controls.Add(content, 1, 1);
        Controls.Add(shell);

        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        }
        catch { }
        UpdateProfileButton();
    }

    private void AddNav(Control parent, string title, int y, Action action)
    {
        var b = MakeButton(title, 12, y, 194, 40, panel, muted);
        b.TextAlign = ContentAlignment.MiddleLeft;
        b.Padding = new Padding(12, 0, 0, 0);
        b.Click += (_, _) => action();
        parent.Controls.Add(b);
    }

    private void UpdateProfileButton()
    {
        if (profileButton != null)
            profileButton.Text = "●   " + Current.Name + "   ▾";
    }

    private Button MakeButton(string caption, int x, int y, int w, int h, Color back, Color fore)
    {
        var b = new Button
        {
            Text = caption,
            Location = new Point(x, y),
            Size = new Size(w, h),
            BackColor = back,
            ForeColor = fore,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(back, .10f);
        return b;
    }

    private void ResetContent()
    {
        foreach (Control child in content.Controls.Cast<Control>().ToArray())
            child.Dispose();
        content.Controls.Clear();
        hero = null!;
        gameHero = null;
        gameGrid = null;
        content.AutoScrollPosition = Point.Empty;
    }

    private void ShowLibrary()
    {
        currentPage = "library";
        ResetContent();
        content.Controls.Add(new Label
        {
            Text = "ТВОЯ БИБЛИОТЕКА",
            Location = new Point(4, 0),
            Size = new Size(500, 25),
            ForeColor = muted,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        });

        hero = new Panel
        {
            Location = new Point(0, 40),
            Size = new Size(Math.Max(700, content.ClientSize.Width - 58), 390),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.FromArgb(30, 64, 100),
            Cursor = Cursors.Hand
        };
        hero.Paint += PaintHero;
        hero.Click += (_, _) => ShowGameDetails();
        content.Controls.Add(hero);

        var kicker = new Label
        {
            Text = "EPIC GAMES  /  FREE TO PLAY",
            Location = new Point(34, 35),
            Size = new Size(350, 22),
            ForeColor = Color.FromArgb(183, 230, 250),
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 8, FontStyle.Bold)
        };
        hero.Controls.Add(kicker);
        hero.Controls.Add(new Label
        {
            Text = "FORTNITE",
            Location = new Point(30, 83),
            Size = new Size(520, 72),
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 43, FontStyle.Bold)
        });
        bannerHeadline = new Label
        {
            Text = "Новый сезон. Новые возможности. Твоя следующая победа.",
            Location = new Point(36, 165),
            Size = new Size(590, 48),
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 11)
        };
        hero.Controls.Add(bannerHeadline);

        bannerCounter = new Label
        {
            Text = "01 / 01",
            Location = new Point(0, 0),
            Size = new Size(74, 30),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(150, 8, 11, 18),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        hero.Controls.Add(bannerCounter);
        var previousBanner = MakeButton("‹", 0, 0, 38, 38, Color.FromArgb(190, 12, 16, 25), Color.White);
        previousBanner.Font = new Font("Segoe UI", 20, FontStyle.Bold);
        previousBanner.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        previousBanner.Click += (_, _) => ChangeBanner(-1);
        hero.Controls.Add(previousBanner);
        var nextBanner = MakeButton("›", 0, 0, 38, 38, Color.FromArgb(190, 12, 16, 25), Color.White);
        nextBanner.Font = new Font("Segoe UI", 20, FontStyle.Bold);
        nextBanner.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        nextBanner.Click += (_, _) => ChangeBanner(1);
        hero.Controls.Add(nextBanner);
        var refreshBanner = MakeButton("↻  ОБНОВИТЬ БАННЕРЫ", 0, 0, 174, 34, Color.FromArgb(180, 12, 16, 25), Color.White);
        refreshBanner.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        refreshBanner.Click += async (_, _) => await RefreshArtworkAsync();
        hero.Controls.Add(refreshBanner);

        void LayoutBannerControls()
        {
            int right = hero.ClientSize.Width - 22;
            bannerCounter.Location = new Point(right - 74, 20);
            nextBanner.Location = new Point(right - 38, 62);
            previousBanner.Location = new Point(right - 82, 62);
            refreshBanner.Location = new Point(right - 174, hero.ClientSize.Height - 52);
        }
        hero.Resize += (_, _) => LayoutBannerControls();
        LayoutBannerControls();

        var details = MakeButton("ОТКРЫТЬ СТРАНИЦУ  →", 35, 250, 205, 48, Color.White, Color.FromArgb(17, 25, 37));
        details.Click += (_, _) => ShowGameDetails();
        hero.Controls.Add(details);
        var play = MakeButton("▶  ИГРАТЬ", 252, 250, 145, 48, blue, Color.FromArgb(5, 20, 29));
        play.Click += (_, _) => LaunchFortnite();
        hero.Controls.Add(play);

        content.Controls.Add(new Label
        {
            Text = "МОИ ИГРЫ", Location = new Point(3, 475), Size = new Size(300, 28),
            ForeColor = text, Font = new Font("Segoe UI", 16, FontStyle.Bold)
        });
        content.Controls.Add(new Label
        {
            Text = $"{data.Games.Count} ИГРЫ  •  ОБЛОЖКИ ИЗ ИГР И НОВОСТЕЙ",
            Location = new Point(4, 508), Size = new Size(480, 18),
            ForeColor = muted, Font = new Font("Segoe UI", 8, FontStyle.Bold)
        });
        var addGame = MakeButton("+  ДОБАВИТЬ ИГРУ", 0, 470, 180, 38, blue, Color.FromArgb(5, 20, 29));
        addGame.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        addGame.Click += (_, _) => AddGame();
        content.Controls.Add(addGame);
        void PositionAddGame() => addGame.Location = new Point(Math.Max(350, content.ClientSize.Width - 220), 470);
        content.Resize += (_, _) => PositionAddGame();
        PositionAddGame();
        gameGrid = new FlowLayoutPanel
        {
            Location = new Point(0, 535), Size = new Size(Math.Max(680, content.ClientSize.Width - 58), 290),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            AutoScroll = true, WrapContents = true, FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty
        };
        content.Controls.Add(gameGrid);
        RenderGameCards();
        if (File.Exists(CoverPath) && cover == null) LoadCachedCover();
    }

    private void RenderGameCards()
    {
        if (gameGrid == null || gameGrid.IsDisposed) return;
        gameGrid.SuspendLayout();
        foreach (Control oldCard in gameGrid.Controls)
            foreach (Control child in oldCard.Controls)
                if (child is PictureBox pb) { var previous = pb.Image; pb.Image = null; previous?.Dispose(); }
        gameGrid.Controls.Clear();
        foreach (var game in data.Games)
        {
            var card = new Panel { Size = new Size(220, 220), BackColor = panel, Margin = new Padding(0, 0, 16, 16), Cursor = Cursors.Hand, Tag = game.Name };
            var artwork = new PictureBox
            {
                Location = new Point(0, 0), Size = new Size(220, 142), BackColor = Color.FromArgb(29, 34, 44),
                SizeMode = PictureBoxSizeMode.Zoom, Cursor = Cursors.Hand
            };
            Image? image = null;
            if (game.IsFortnite && cover != null)
                image = new Bitmap(cover);
            else if (!string.IsNullOrWhiteSpace(game.ArtworkPath) && File.Exists(game.ArtworkPath))
            {
                try { using var fs = new FileStream(game.ArtworkPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); using var source = Image.FromStream(fs); image = new Bitmap(source); } catch { }
            }
            artwork.Image = image;
            card.Controls.Add(artwork);
            var gameName = new Label
            {
                Text = game.Name.ToUpperInvariant(), Location = new Point(12, 151), Size = new Size(196, 25),
                ForeColor = text, BackColor = panel, Font = new Font("Segoe UI", 10, FontStyle.Bold), AutoEllipsis = true, Cursor = Cursors.Hand
            };
            card.Controls.Add(gameName);
            var status = new Label
            {
                Text = game.IsFortnite ? (File.Exists(Current.GamePath) ? "ГОТОВА К ЗАПУСКУ" : "НАСТРОИТЬ РАСПОЛОЖЕНИЕ") :
                    (File.Exists(game.ExePath) ? "ГОТОВА К ЗАПУСКУ" : "ПУТЬ НЕ НАЙДЕН"),
                Location = new Point(12, 181), Size = new Size(196, 20), ForeColor = muted, BackColor = panel,
                Font = new Font("Segoe UI", 7, FontStyle.Bold)
            };
            card.Controls.Add(status);
            void OpenGame() { if (game.IsFortnite) ShowGameDetails(); else LaunchAddedGame(game); }
            card.Click += (_, _) => OpenGame(); artwork.Click += (_, _) => OpenGame();
            gameName.Click += (_, _) => OpenGame(); status.Click += (_, _) => OpenGame();
            gameGrid.Controls.Add(card);
        }
        gameGrid.ResumeLayout();
    }

    private void AddGame()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Выбери EXE-файл игры — аватарка подтянется автоматически из иконки",
            Filter = "Игры и приложения (*.exe)|*.exe", CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        string exe = dialog.FileName;
        if (data.Games.Any(g => string.Equals(g.ExePath, exe, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("Эта игра уже есть в библиотеке.", "Fortnite Booster PRO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string name = Path.GetFileNameWithoutExtension(exe);
        try
        {
            var version = FileVersionInfo.GetVersionInfo(exe);
            if (!string.IsNullOrWhiteSpace(version.ProductName)) name = version.ProductName.Trim();
            else if (!string.IsNullOrWhiteSpace(version.FileDescription)) name = version.FileDescription.Trim();
        }
        catch { }
        string artworkPath = Path.Combine(root, "game-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(exe);
            if (icon != null)
            {
                using var bitmap = icon.ToBitmap();
                using var scaled = new Bitmap(256, 256);
                using (var graphics = Graphics.FromImage(scaled))
                {
                    graphics.Clear(Color.Transparent);
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    float scale = Math.Min(220f / bitmap.Width, 220f / bitmap.Height);
                    int w = Math.Max(1, (int)(bitmap.Width * scale)), h = Math.Max(1, (int)(bitmap.Height * scale));
                    graphics.DrawImage(bitmap, new Rectangle((256 - w) / 2, (256 - h) / 2, w, h));
                }
                scaled.Save(artworkPath, System.Drawing.Imaging.ImageFormat.Png);
            }
        }
        catch { artworkPath = ""; }
        data.Games.Add(new GameEntry { Name = name, ExePath = exe, ArtworkPath = artworkPath });
        SaveData();
        ShowLibrary();
    }

    private void LaunchAddedGame(GameEntry game)
    {
        try
        {
            if (!File.Exists(game.ExePath))
            {
                MessageBox.Show("Файл игры не найден. Удали игру из библиотеки и добавь её заново.", game.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Process.Start(new ProcessStartInfo(game.ExePath) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(game.ExePath)! });
        }
        catch (Exception ex) { MessageBox.Show("Не удалось запустить " + game.Name + ": " + ex.Message); }
    }

    private void ShowGameDetails()
    {
        currentPage = "game";
        ResetContent();
        content.Controls.Add(new Label
        {
            Text = "БИБЛИОТЕКА  /  FORTNITE",
            Location = new Point(2, 0),
            Size = new Size(500, 22),
            ForeColor = muted,
            Font = new Font("Segoe UI", 8, FontStyle.Bold)
        });

        var art = new Panel
        {
            Location = new Point(0, 36),
            Size = new Size(Math.Max(700, content.ClientSize.Width - 58), 330),
            BackColor = Color.FromArgb(30, 65, 95),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        art.Paint += PaintHero;
        gameHero = art;
        content.Controls.Add(art);
        art.Controls.Add(new Label
        {
            Text = "EPIC GAMES  /  FREE TO PLAY",
            Location = new Point(34, 28), Size = new Size(300, 20),
            ForeColor = Color.FromArgb(210, 230, 245), BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 8, FontStyle.Bold)
        });
        art.Controls.Add(new Label
        {
            Text = "FORTNITE",
            Location = new Point(35, 80),
            Size = new Size(500, 72),
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 42, FontStyle.Bold)
        });
        art.Controls.Add(new Label
        {
            Text = "Battle Royale  •  Windows  •  Free to Play",
            Location = new Point(42, 160),
            Size = new Size(500, 28),
            ForeColor = Color.White,
            BackColor = Color.Transparent
        });

        var installed = File.Exists(Current.GamePath);
        var state = new Label
        {
            Text = installed ? "●  ИГРА ГОТОВА К ЗАПУСКУ" : "○  УКAЖИ ПУТЬ К ИГРЕ ИЛИ УСТАНОВИ ЕЁ",
            Location = new Point(4, 390),
            Size = new Size(520, 24),
            ForeColor = installed ? Color.FromArgb(105, 222, 157) : muted,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        content.Controls.Add(state);

        var install = MakeButton("↓   УСТАНОВИТЬ", 0, 430, 180, 48, blue, Color.FromArgb(5, 20, 29));
        install.Click += (_, _) => OpenUrl(epicPage);
        content.Controls.Add(install);

        var play = MakeButton("▶   ИГРАТЬ", 194, 430, 145, 48, panel2, text);
        play.Click += (_, _) => LaunchFortnite();
        content.Controls.Add(play);

        var configure = MakeButton("⚙   НАСТРОИТЬ", 353, 430, 165, 48, panel2, text);
        configure.Click += (_, _) => ShowSettings();
        content.Controls.Add(configure);

        content.Controls.Add(new Label
        {
            Text = "ОБ ИГРЕ",
            Location = new Point(4, 510),
            Size = new Size(300, 25),
            ForeColor = text,
            Font = new Font("Segoe UI", 14, FontStyle.Bold)
        });
        content.Controls.Add(new Label
        {
            Text = "Fortnite — бесплатная игра от Epic Games. Установка и обновления выполняются официальным Epic Games Launcher.\nBooster хранит твой путь к игре и настройки отдельно для каждого профиля.",
            Location = new Point(5, 546),
            Size = new Size(750, 58),
            ForeColor = muted,
            Font = new Font("Segoe UI", 9.5f)
        });
        var news = MakeButton("ОТКРЫТЬ НОВОСТИ FORTNITE  ↗", 4, 630, 245, 40, panel2, text);
        news.Click += (_, _) => OpenUrl("https://www.fortnite.com/news");
        content.Controls.Add(news);
        var refreshArt = MakeButton("↻  ОБНОВИТЬ БАННЕРЫ", 260, 630, 210, 40, panel2, text);
        refreshArt.Click += async (_, _) => await RefreshArtworkAsync();
        content.Controls.Add(refreshArt);
    }

    private async Task RefreshArtworkAsync()
    {
        try
        {
            var items = new List<(string Title, string Url)>();
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, newsUrl);
                request.Headers.UserAgent.ParseAdd("FortniteBoosterPRO/2.3");
                using var response = await http.SendAsync(request);
                response.EnsureSuccessStatusCode();
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                CollectNewsArtwork(doc.RootElement, items);
            }
            catch { }

            if (items.Count > 0)
                await DownloadAndCacheBannersAsync(items.Take(6).ToList());

            if (cover == null)
            {
                if (File.Exists(CoverPath)) LoadCachedCover();
                else
                {
                    var fallback = await TryGetOfficialPageArtworkAsync();
                    if (!string.IsNullOrWhiteSpace(fallback))
                        await DownloadAndSaveArtworkAsync(fallback);
                }
            }
            if (banners.Count == 0) LoadCachedBanners();
            UpdateArtworkOnScreen();
        }
        catch
        {
            // Keep the last good artwork and cached carousel if the network is unavailable.
            if (cover == null && File.Exists(CoverPath)) LoadCachedCover();
            if (banners.Count == 0) LoadCachedBanners();
        }
    }

    private static void CollectNewsArtwork(JsonElement element, List<(string Title, string Url)> results)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            string? title = null;
            string? imageUrl = null;
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String) continue;
                string? value = property.Value.GetString();
                if (property.Name.Equals("title", StringComparison.OrdinalIgnoreCase) ||
                    property.Name.Equals("headline", StringComparison.OrdinalIgnoreCase))
                    title = value;
                if (property.Name.Equals("image", StringComparison.OrdinalIgnoreCase) ||
                    property.Name.Equals("tileImage", StringComparison.OrdinalIgnoreCase) ||
                    property.Name.Equals("imageUrl", StringComparison.OrdinalIgnoreCase) ||
                    property.Name.Equals("featuredImage", StringComparison.OrdinalIgnoreCase) ||
                    property.Name.Equals("background", StringComparison.OrdinalIgnoreCase) ||
                    property.Name.Equals("banner", StringComparison.OrdinalIgnoreCase))
                {
                    if (IsHttpImageUrl(value)) imageUrl = value;
                }
            }

            if (!string.IsNullOrWhiteSpace(imageUrl) &&
                !results.Any(x => x.Url.Equals(imageUrl, StringComparison.OrdinalIgnoreCase)))
                results.Add((string.IsNullOrWhiteSpace(title) ? "НОВОСТИ FORTNITE" : title.Trim(), imageUrl!));

            foreach (var property in element.EnumerateObject())
                CollectNewsArtwork(property.Value, results);
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                CollectNewsArtwork(item, results);
        }
    }

    private async Task DownloadAndCacheBannersAsync(List<(string Title, string Url)> items)
    {
        var newImages = new List<Image>();
        var newTitles = new List<string>();
        try
        {
            int index = 0;
            foreach (var item in items)
            {
                try
                {
                    byte[] bytes = await http.GetByteArrayAsync(item.Url);
                    if (bytes.Length < 5000 || bytes.Length > 15_000_000) continue;
                    using var stream = new MemoryStream(bytes);
                    using var decoded = Image.FromStream(stream);
                    if (decoded.Width < 320 || decoded.Height < 140) continue;
                    var image = new Bitmap(decoded);
                    string path = Path.Combine(root, $"fortnite-banner-{index}.jpg");
                    string temp = path + ".tmp";
                    await File.WriteAllBytesAsync(temp, bytes);
                    File.Copy(temp, path, true);
                    File.Delete(temp);
                    newImages.Add(image);
                    newTitles.Add(item.Title);
                    index++;
                    if (index >= 6) break;
                }
                catch { }
            }

            if (newImages.Count == 0) return;
            foreach (var old in banners) old.Dispose();
            banners.Clear();
            bannerTitles.Clear();
            banners.AddRange(newImages);
            bannerTitles.AddRange(newTitles);
            bannerIndex = 0;

            // Keep a separate, persistent tile cover from the first current news image.
            var firstPath = Path.Combine(root, "fortnite-banner-0.jpg");
            if (File.Exists(firstPath))
            {
                File.Copy(firstPath, CoverPath, true);
                using var fs = new FileStream(CoverPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var img = Image.FromStream(fs);
                var newCover = new Bitmap(img);
                var oldCover = cover;
                cover = newCover;
                oldCover?.Dispose();
            }
            UpdateBannerPresentation();
            UpdateArtworkOnScreen();
        }
        catch
        {
            foreach (var image in newImages) image.Dispose();
            if (banners.Count == 0) LoadCachedBanners();
        }
    }

    private void LoadCachedBanners()
    {
        try
        {
            var loaded = new List<Image>();
            var titles = new List<string>();
            for (int i = 0; i < 6; i++)
            {
                string path = Path.Combine(root, $"fortnite-banner-{i}.jpg");
                if (!File.Exists(path)) break;
                try
                {
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var img = Image.FromStream(fs);
                    loaded.Add(new Bitmap(img));
                    titles.Add("АКТУАЛЬНЫЕ НОВОСТИ FORTNITE");
                }
                catch { break; }
            }
            if (loaded.Count > 0)
            {
                foreach (var old in banners) old.Dispose();
                banners.Clear();
                bannerTitles.Clear();
                banners.AddRange(loaded);
                bannerTitles.AddRange(titles);
                bannerIndex = 0;
                if (cover == null && File.Exists(CoverPath)) LoadCachedCover();
                UpdateBannerPresentation();
            }
        }
        catch { }
    }

    private void ChangeBanner(int delta)
    {
        if (banners.Count < 2) return;
        bannerIndex = (bannerIndex + delta + banners.Count) % banners.Count;
        UpdateBannerPresentation();
    }

    private void UpdateBannerPresentation()
    {
        if (IsDisposed) return;
        if (bannerHeadline != null && !bannerHeadline.IsDisposed &&
            bannerIndex >= 0 && bannerIndex < bannerTitles.Count &&
            !string.IsNullOrWhiteSpace(bannerTitles[bannerIndex]))
            bannerHeadline.Text = bannerTitles[bannerIndex];
        if (bannerCounter != null && !bannerCounter.IsDisposed)
            bannerCounter.Text = $"{bannerIndex + 1:00} / {Math.Max(1, banners.Count):00}";
        if (hero != null && !hero.IsDisposed) hero.Invalidate();
        if (gameHero != null && !gameHero.IsDisposed) gameHero.Invalidate();
    }

    private static bool IsHttpImageUrl(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
         value.StartsWith("http://", StringComparison.OrdinalIgnoreCase));

    private async Task<string?> TryGetOfficialPageArtworkAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://www.fortnite.com/");
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 FortniteBoosterPRO/2.2");
            using var response = await http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            string html = await response.Content.ReadAsStringAsync();

            foreach (string marker in new[] { "property=" + '"' + "og:image" + '"', "name=" + '"' + "twitter:image" + '"' })
            {
                int markerIndex = html.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (markerIndex < 0) continue;
                int tagStart = html.LastIndexOf("<meta", markerIndex, StringComparison.OrdinalIgnoreCase);
                int tagEnd = html.IndexOf('>', markerIndex);
                if (tagStart < 0 || tagEnd < 0) continue;
                string tag = html.Substring(tagStart, tagEnd - tagStart + 1);
                int contentIndex = tag.IndexOf("content=" + '"', StringComparison.OrdinalIgnoreCase);
                if (contentIndex < 0) continue;
                contentIndex += 9;
                int valueEnd = tag.IndexOf('"', contentIndex);
                if (valueEnd <= contentIndex) continue;
                string url = System.Net.WebUtility.HtmlDecode(tag.Substring(contentIndex, valueEnd - contentIndex));
                if (IsHttpImageUrl(url)) return url;
            }
        }
        catch { }
        return null;
    }

    private async Task DownloadAndSaveArtworkAsync(string imageUrl)
    {
        var bytes = await http.GetByteArrayAsync(imageUrl);
        if (bytes.Length < 5_000) return;

        // Validate the image before replacing the cached copy.
        using var stream = new MemoryStream(bytes);
        using var decoded = Image.FromStream(stream);
        var newImage = new Bitmap(decoded);

        string temp = CoverPath + ".tmp";
        await File.WriteAllBytesAsync(temp, bytes);
        try
        {
            File.Copy(temp, CoverPath, true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }

        var old = cover;
        cover = newImage;
        old?.Dispose();

        if (IsDisposed) return;
        if (InvokeRequired) BeginInvoke(new Action(UpdateArtworkOnScreen));
        else UpdateArtworkOnScreen();
    }

    private void LoadCachedCover()
    {
        try
        {
            using var fs = new FileStream(CoverPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var img = Image.FromStream(fs);
            cover?.Dispose();
            cover = new Bitmap(img);
            UpdateArtworkOnScreen();
        }
        catch { }
    }

    private void UpdateArtworkOnScreen()
    {
        if (IsDisposed) return;
        if (hero != null && !hero.IsDisposed) hero.Invalidate();
        if (gameGrid != null && !gameGrid.IsDisposed) RenderGameCards();
    }

    private void PaintHero(object? sender, PaintEventArgs e)
    {
        var bounds = ((Control)sender!).ClientRectangle;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.HighQuality;
        Image? activeBanner = banners.Count > 0 && bannerIndex < banners.Count ? banners[bannerIndex] : cover;
        if (activeBanner != null)
        {
            float scale = Math.Max((float)bounds.Width / activeBanner.Width, (float)bounds.Height / activeBanner.Height);
            int w = (int)(activeBanner.Width * scale), h = (int)(activeBanner.Height * scale);
            var previousClip = g.Clip;
            g.SetClip(bounds);
            g.DrawImage(activeBanner, new Rectangle((bounds.Width - w) / 2, (bounds.Height - h) / 2, w, h));
            g.Clip = previousClip;
        }
        else
        {
            using var gradient = new LinearGradientBrush(bounds, Color.FromArgb(43, 98, 145), Color.FromArgb(15, 20, 34), 15);
            g.FillRectangle(gradient, bounds);
        }
        using var shade = new LinearGradientBrush(bounds, Color.FromArgb(210, 5, 10, 20), Color.FromArgb(15, 5, 10, 20), LinearGradientMode.Horizontal);
        g.FillRectangle(shade, bounds);
    }

    private void ShowProfiles()
    {
        using var f = CreateDialog("ПРОФИЛИ ИГРОКОВ", 600, 520);
        var list = new ListBox
        {
            Location = new Point(24, 65), Size = new Size(535, 245),
            BackColor = panel2, ForeColor = text, BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 10)
        };
        foreach (var p in data.Profiles)
            list.Items.Add(new ProfileListItem(p));
        list.SelectedIndex = data.Profiles.FindIndex(p => p.Id == data.ActiveProfileId);
        f.Controls.Add(list);

        var add = MakeButton("+  НОВЫЙ ПРОФИЛЬ", 24, 335, 170, 42, blue, Color.FromArgb(5, 20, 29));
        add.Click += (_, _) =>
        {
            string name = AskText("Название профиля", "Новый профиль");
            if (string.IsNullOrWhiteSpace(name)) return;
            var p = new UserProfile { Name = name.Trim() };
            data.Profiles.Add(p);
            SaveData();
            list.Items.Add(new ProfileListItem(p));
            list.SelectedIndex = list.Items.Count - 1;
        };
        f.Controls.Add(add);

        var rename = MakeButton("ПЕРЕИМЕНОВАТЬ", 204, 335, 155, 42, panel2, text);
        rename.Click += (_, _) =>
        {
            if (list.SelectedItem is not ProfileListItem item) return;
            string name = AskText("Новое имя профиля", item.Profile.Name);
            if (string.IsNullOrWhiteSpace(name)) return;
            item.Profile.Name = name.Trim();
            SaveData();
            int index = list.SelectedIndex;
            list.Items[index] = new ProfileListItem(item.Profile);
            UpdateProfileButton();
        };
        f.Controls.Add(rename);

        var remove = MakeButton("УДАЛИТЬ", 369, 335, 100, 42, panel2, Color.FromArgb(255, 135, 135));
        remove.Click += (_, _) =>
        {
            if (list.SelectedItem is not ProfileListItem item || data.Profiles.Count < 2) return;
            if (MessageBox.Show("Удалить профиль " + item.Profile.Name + "?", "Подтверждение", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            data.Profiles.Remove(item.Profile);
            if (data.ActiveProfileId == item.Profile.Id) data.ActiveProfileId = data.Profiles[0].Id;
            SaveData();
            list.Items.Remove(item);
            UpdateProfileButton();
        };
        f.Controls.Add(remove);

        var activate = MakeButton("ПЕРЕКЛЮЧИТЬСЯ", 24, 395, 535, 48, blue, Color.FromArgb(5, 20, 29));
        activate.Click += (_, _) =>
        {
            if (list.SelectedItem is not ProfileListItem item) return;
            data.ActiveProfileId = item.Profile.Id;
            SaveData();
            UpdateProfileButton();
            f.Close();
            if (currentPage == "game") ShowGameDetails(); else ShowLibrary();
        };
        f.Controls.Add(activate);
        f.Controls.Add(new Label
        {
            Text = "Это локальные профили Booster. Вход и пароли Epic Games здесь не хранятся.",
            Location = new Point(25, 463), Size = new Size(535, 22), ForeColor = muted,
            Font = new Font("Segoe UI", 8)
        });
        f.ShowDialog(this);
    }

    private sealed class ProfileListItem
    {
        public UserProfile Profile { get; }
        public ProfileListItem(UserProfile profile) => Profile = profile;
        public override string ToString() => (Profile.Id == "" ? "" : "●  ") + Profile.Name +
            (string.IsNullOrWhiteSpace(Profile.EpicNickname) ? "" : "   ·   " + Profile.EpicNickname);
    }

    private void ShowSettings()
    {
        using var f = CreateDialog("НАСТРОЙКИ ПРОФИЛЯ", 660, 600);
        var p = Current;
        AddSettingLabel(f, "НАЗВАНИЕ ПРОФИЛЯ", 24, 65);
        var profileName = AddTextBox(f, p.Name, 24, 90, 590);
        AddSettingLabel(f, "НИК EPIC GAMES (ТОЛЬКО ДЛЯ ОТОБРАЖЕНИЯ)", 24, 140);
        var epic = AddTextBox(f, p.EpicNickname, 24, 165, 590);
        AddSettingLabel(f, "РАСПОЛОЖЕНИЕ FORTNITE", 24, 215);
        var path = AddTextBox(f, p.GamePath, 24, 240, 470);
        var browse = MakeButton("ОБЗОР", 506, 239, 108, 34, panel2, text);
        browse.Click += (_, _) =>
        {
            using var d = new OpenFileDialog { Title = "Выбери FortniteClient-Win64-Shipping.exe", Filter = "Fortnite executable (*.exe)|*.exe" };
            if (d.ShowDialog() == DialogResult.OK) path.Text = d.FileName;
        };
        f.Controls.Add(browse);
        AddSettingLabel(f, "ПАРАМЕТРЫ ЗАПУСКА (НЕОБЯЗАТЕЛЬНО)", 24, 290);
        var args = AddTextBox(f, p.LaunchOptions, 24, 315, 590);
        AddSettingLabel(f, "ПРОФИЛЬ ПРОИЗВОДИТЕЛЬНОСТИ", 24, 365);
        var mode = new ComboBox
        {
            Location = new Point(24, 390), Size = new Size(590, 35),
            DropDownStyle = ComboBoxStyle.DropDownList, BackColor = panel2, ForeColor = text,
            FlatStyle = FlatStyle.Flat
        };
        mode.Items.AddRange(new object[] { "Сбалансированный", "Минимум фоновых отвлечений", "Стандартный Windows" });
        mode.SelectedItem = p.PerformanceMode;
        if (mode.SelectedIndex < 0) mode.SelectedIndex = 0;
        f.Controls.Add(mode);

        var save = MakeButton("СОХРАНИТЬ НАСТРОЙКИ", 24, 455, 590, 48, blue, Color.FromArgb(5, 20, 29));
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(profileName.Text)) { MessageBox.Show("Укажи название профиля."); return; }
            p.Name = profileName.Text.Trim();
            p.EpicNickname = epic.Text.Trim();
            p.GamePath = path.Text.Trim();
            p.LaunchOptions = args.Text.Trim();
            p.PerformanceMode = mode.SelectedItem?.ToString() ?? "Сбалансированный";
            SaveData();
            UpdateProfileButton();
            f.Close();
            if (currentPage == "game") ShowGameDetails();
        };
        f.Controls.Add(save);
        var shortcut = MakeButton("⌑  СОЗДАТЬ ЯРЛЫК НА РАБОЧЕМ СТОЛЕ", 24, 515, 590, 38, panel2, text);
        shortcut.Click += (_, _) =>
        {
            try
            {
                CreateDesktopShortcut();
                MessageBox.Show("Ярлык Fortnite Booster PRO создан на рабочем столе.", "Готово",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show("Не удалось создать ярлык: " + ex.Message, "Ошибка"); }
        };
        f.Controls.Add(shortcut);
        f.ShowDialog(this);
    }

    private void CreateDesktopShortcut()
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string shortcutPath = Path.Combine(desktop, "Fortnite Booster PRO.lnk");
        Type shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("Компонент создания ярлыков Windows недоступен.");
        dynamic shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("Не удалось запустить компонент Windows Script Host.");
        dynamic link = shell.CreateShortcut(shortcutPath);
        link.TargetPath = Application.ExecutablePath;
        link.WorkingDirectory = AppContext.BaseDirectory;
        link.Description = "Fortnite Booster PRO";
        link.IconLocation = Application.ExecutablePath + ",0";
        link.Save();
    }

    private Form CreateDialog(string title, int w, int h)
    {
        var f = new Form
        {
            Text = title, Size = new Size(w, h), StartPosition = FormStartPosition.CenterParent,
            BackColor = bg, ForeColor = text, FormBorderStyle = FormBorderStyle.None,
            MaximizeBox = false, MinimizeBox = false, Font = new Font("Segoe UI", 9),
            ShowInTaskbar = false
        };
        var header = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Color.FromArgb(20, 23, 31) };
        header.Controls.Add(new Label
        {
            Text = title, Location = new Point(22, 13), Size = new Size(w - 90, 27),
            ForeColor = text, Font = new Font("Segoe UI", 11, FontStyle.Bold)
        });
        var close = MakeButton("×", w - 48, 8, 36, 36, Color.FromArgb(20, 23, 31), muted);
        close.Font = new Font("Segoe UI", 16);
        close.Click += (_, _) => f.Close();
        header.Controls.Add(close);
        f.Controls.Add(header);
        f.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Color.FromArgb(53, 59, 73) });
        return f;
    }

    private void AddSettingLabel(Control parent, string label, int x, int y) =>
        parent.Controls.Add(new Label { Text = label, Location = new Point(x, y), Size = new Size(590, 20), ForeColor = muted, Font = new Font("Segoe UI", 8, FontStyle.Bold) });

    private TextBox AddTextBox(Control parent, string value, int x, int y, int width)
    {
        var box = new TextBox
        {
            Text = value, Location = new Point(x, y), Size = new Size(width, 34),
            BackColor = panel2, ForeColor = text, BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9)
        };
        parent.Controls.Add(box);
        return box;
    }

    private string AskText(string title, string initial)
    {
        using var f = CreateDialog(title, 440, 190);
        var input = AddTextBox(f, initial, 24, 70, 375);
        var ok = MakeButton("СОХРАНИТЬ", 24, 112, 375, 38, blue, Color.FromArgb(5, 20, 29));
        string result = "";
        ok.Click += (_, _) => { result = input.Text; f.DialogResult = DialogResult.OK; f.Close(); };
        f.Controls.Add(ok);
        return f.ShowDialog(this) == DialogResult.OK ? result : "";
    }

    private string FindFortniteExe()
    {
        string[] paths =
        {
            @"C:\Program Files\Epic Games\Fortnite\FortniteGame\Binaries\Win64\FortniteClient-Win64-Shipping.exe",
            @"D:\Epic Games\Fortnite\FortniteGame\Binaries\Win64\FortniteClient-Win64-Shipping.exe",
            @"C:\Program Files\Epic Games\Fortnite\FortniteGame\Binaries\Win64\FortniteLauncher.exe"
        };
        return paths.FirstOrDefault(File.Exists) ?? "";
    }
    

    private sealed record PcSpecs(string Cpu, string Gpu, string Ram, string Windows, string Architecture, string Motherboard);

    private PcSpecs DetectPcSpecs()
    {
        string ReadWmi(string className, string property)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT " + property + " FROM " + className);
                foreach (ManagementObject item in searcher.Get())
                {
                    var value = item[property]?.ToString();
                    if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
                }
            }
            catch { }
            return "Не удалось определить";
        }

        string cpu = ReadWmi("Win32_Processor", "Name");
        string gpu = ReadWmi("Win32_VideoController", "Name");
        string board = ReadWmi("Win32_BaseBoard", "Product");
        string os = ReadWmi("Win32_OperatingSystem", "Caption");
        string version = ReadWmi("Win32_OperatingSystem", "Version");
        string ram;
        try
        {
            string memoryText = ReadWmi("Win32_ComputerSystem", "TotalPhysicalMemory");
            ram = ulong.TryParse(memoryText, out ulong bytes)
                ? $"{Math.Round(bytes / 1024d / 1024d / 1024d)} ГБ"
                : "Не удалось определить";
        }
        catch { ram = "Не удалось определить"; }

        return new PcSpecs(cpu, gpu, ram, os + " (" + version + ")",
            Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit", board);
    }

    private void ShowPcSpecs()
    {
        using var f = CreateDialog("ХАРАКТЕРИСТИКИ ТВОЕГО ПК", 650, 540);
        var specs = DetectPcSpecs();
        f.Controls.Add(new Label
        {
            Text = "ДАННЫЕ СЧИТЫВАЮТСЯ АВТОМАТИЧЕСКИ ИЗ WINDOWS",
            Location = new Point(25, 65), Size = new Size(570, 22),
            ForeColor = blue, Font = new Font("Segoe UI", 8, FontStyle.Bold)
        });

        void Row(string caption, string value, int y)
        {
            f.Controls.Add(new Label { Text = caption, Location = new Point(26, y), Size = new Size(150, 25), ForeColor = muted, Font = new Font("Segoe UI", 9, FontStyle.Bold) });
            f.Controls.Add(new Label { Text = value, Location = new Point(185, y), Size = new Size(420, 42), ForeColor = text, Font = new Font("Segoe UI", 9), AutoEllipsis = true });
            f.Controls.Add(new Panel { Location = new Point(25, y + 39), Size = new Size(575, 1), BackColor = panel2 });
        }

        Row("ПРОЦЕССОР", specs.Cpu, 105);
        Row("ВИДЕОКАРТА", specs.Gpu, 165);
        Row("ОПЕРАТИВНАЯ ПАМЯТЬ", specs.Ram, 225);
        Row("WINDOWS", specs.Windows, 285);
        Row("АРХИТЕКТУРА", specs.Architecture, 345);
        Row("МАТЕРИНСКАЯ ПЛАТА", specs.Motherboard, 405);

        var refresh = MakeButton("↻  ОБНОВИТЬ ДАННЫЕ", 25, 465, 575, 42, blue, Color.FromArgb(5, 20, 29));
        refresh.Click += (_, _) => { f.Close(); ShowPcSpecs(); };
        f.Controls.Add(refresh);
        f.ShowDialog(this);
    }

    private void ShowBoost()
    {
        using var f = CreateDialog("ПРОФИЛЬ СТАБИЛЬНОСТИ FPS", 590, 410);
        f.Controls.Add(new Label
        {
            Text = "Профиль подбирается по автоматически определённым характеристикам ПК. Открой раздел «Мой ПК» для проверки.",
            Location = new Point(25, 68), Size = new Size(530, 25), ForeColor = muted
        });
        f.Controls.Add(new Label
        {
            Text = "Профиль включает Windows Game Mode и отключает фоновую запись Game DVR для текущего пользователя.\nПеред изменениями создаётся резервная копия исходных значений.",
            Location = new Point(25, 112), Size = new Size(530, 65), ForeColor = text
        });
        var apply = MakeButton("ПРИМЕНИТЬ БЕЗОПАСНЫЙ BOOST", 25, 205, 530, 48, blue, Color.FromArgb(5, 20, 29));
        apply.Click += (_, _) =>
        {
            try
            {
                SetUserDword(@"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1);
                SetUserDword(@"Software\Microsoft\GameBar", "AllowAutoGameMode", 1);
                SetUserDword(@"System\GameConfigStore", "GameDVR_Enabled", 0);
                SetUserDword(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0);
                MessageBox.Show("Параметры применены. Перезагрузи Windows перед сравнительным тестом FPS.", "Fortnite Booster PRO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Ошибка"); }
        };
        f.Controls.Add(apply);
        f.Controls.Add(new Label
        {
            Text = "Без разгона, отключения служб и сомнительных твиков. Прирост FPS не гарантируется.",
            Location = new Point(25, 278), Size = new Size(530, 42), ForeColor = muted
        });
        f.ShowDialog(this);
    }

    private void SetUserDword(string path, string name, int value)
    {
        string backupPath = Path.Combine(root, "registry-backup.json");
        Dictionary<string, JsonElement> saved = new();
        if (File.Exists(backupPath))
        {
            try { saved = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(backupPath)) ?? new(); }
            catch { }
        }
        string keyName = path + "|" + name;
        if (!saved.ContainsKey(keyName))
        {
            using var oldKey = Registry.CurrentUser.OpenSubKey(path);
            bool existed = oldKey?.GetValueNames().Contains(name) == true;
            object? oldValue = existed ? oldKey!.GetValue(name) : null;
            saved[keyName] = JsonSerializer.SerializeToElement(new { existed, oldValue });
            File.WriteAllText(backupPath, JsonSerializer.Serialize(saved, new JsonSerializerOptions { WriteIndented = true }));
        }
        using var key = Registry.CurrentUser.CreateSubKey(path);
        key.SetValue(name, value, RegistryValueKind.DWord);
    }

    private void LaunchFortnite()
    {
        try
        {
            if (File.Exists(Current.GamePath))
            {
                var info = new ProcessStartInfo(Current.GamePath) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(Current.GamePath)! };
                if (!string.IsNullOrWhiteSpace(Current.LaunchOptions)) info.Arguments = Current.LaunchOptions;
                Process.Start(info);
                return;
            }
            var choice = MessageBox.Show(
                "Fortnite не найден в профиле.\nДа — открыть страницу установки Epic Games.\nНет — выбрать EXE установленной игры.",
                "Fortnite", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (choice == DialogResult.Yes) OpenUrl(epicPage);
            else if (choice == DialogResult.No)
            {
                using var d = new OpenFileDialog { Title = "Выбери FortniteClient-Win64-Shipping.exe", Filter = "Fortnite executable (*.exe)|*.exe" };
                if (d.ShowDialog() == DialogResult.OK)
                {
                    Current.GamePath = d.FileName;
                    SaveData();
                    ShowGameDetails();
                }
            }
        }
        catch (Exception ex) { MessageBox.Show("Не удалось запустить Fortnite: " + ex.Message); }
    }

    private void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }
}