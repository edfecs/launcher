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

internal sealed class PremiumButton : Button
{
    private bool hovered;
    public PremiumButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        FlatAppearance.MouseDownBackColor = Color.Transparent;
        FlatAppearance.MouseOverBackColor = Color.Transparent;
        UseVisualStyleBackColor = false;
    }

    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? Color.Black);
        var rect = ClientRectangle;
        rect.Width -= 1; rect.Height -= 1;
        int radius = Math.Min(10, Math.Max(4, Height / 5));
        using var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
        path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
        path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
        path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
        path.CloseFigure();
        Color baseColor = Enabled ? BackColor : Color.FromArgb(45, 45, 55);
        Color top = hovered ? ControlPaint.Light(baseColor, .12f) : baseColor;
        using var brush = new LinearGradientBrush(rect, top, ControlPaint.Dark(baseColor, .06f), LinearGradientMode.Vertical);
        e.Graphics.FillPath(brush, path);
        using var border = new Pen(hovered ? Color.FromArgb(105, 255, 255, 255) : Color.FromArgb(28, 255, 255, 255), 1);
        e.Graphics.DrawPath(border, path);
        TextRenderer.DrawText(e.Graphics, Text, Font, Rectangle.Inflate(rect, -8, -2), ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

internal sealed class MainForm : Form
{
    private readonly Color bg = Color.FromArgb(10, 10, 16);
    private readonly Color panel = Color.FromArgb(20, 19, 30);
    private readonly Color panel2 = Color.FromArgb(29, 27, 42);
    private readonly Color text = Color.FromArgb(248, 246, 255);
    private readonly Color muted = Color.FromArgb(157, 151, 178);
    private readonly Color blue = Color.FromArgb(139, 92, 246);
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
    private System.Windows.Forms.Timer fadeTimer = null!;
    private int fadeFromIndex = -1;
    private float fadeProgress = 1f;
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
        fadeTimer = new System.Windows.Forms.Timer { Interval = 16 };
        fadeTimer.Tick += (_, _) =>
        {
            fadeProgress = Math.Min(1f, fadeProgress + 0.032f);
            if (fadeProgress >= 1f)
            {
                fadeTimer.Stop();
                fadeFromIndex = -1;
                UpdateBannerPresentation();
            }
            else
            {
                if (hero != null && !hero.IsDisposed) hero.Invalidate();
                if (gameHero != null && !gameHero.IsDisposed) gameHero.Invalidate();
            }
        };
        bannerTimer = new System.Windows.Forms.Timer { Interval = 8500 };
        bannerTimer.Tick += (_, _) => ChangeBanner(1);
        bannerTimer.Start();
        FormClosed += (_, _) =>
        {
            bannerTimer?.Stop(); bannerTimer?.Dispose();
            fadeTimer?.Stop(); fadeTimer?.Dispose();
            refreshTimer?.Stop(); refreshTimer?.Dispose();
            foreach (var image in banners) image.Dispose();
            cover?.Dispose();
            http.Dispose();
        };
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
        return new PremiumButton
        {
            Text = caption, Location = new Point(x, y), Size = new Size(w, h),
            BackColor = back, ForeColor = fore, Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand, FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleCenter
        };
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
        addGame.Click += async (_, _) => await AddGameAsync();
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
        // Dispose the complete old card (including its artwork and child controls)
        // before rebuilding, otherwise stale controls can remain layered underneath.
        foreach (Control oldCard in gameGrid.Controls.Cast<Control>().ToArray())
            oldCard.Dispose();
        gameGrid.Controls.Clear();

        foreach (var game in data.Games)
        {
            Image? artworkImage = null;
            if (game.IsFortnite && cover != null)
                artworkImage = new Bitmap(cover);
            else if (!string.IsNullOrWhiteSpace(game.ArtworkPath) && File.Exists(game.ArtworkPath))
            {
                try
                {
                    using var fs = new FileStream(game.ArtworkPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var source = Image.FromStream(fs);
                    artworkImage = new Bitmap(source);
                }
                catch { }
            }

            // One single, full-bleed tile: artwork is cropped to cover the entire card,
            // like the Epic Games library, instead of being letterboxed in a small PictureBox.
            var card = new Panel
            {
                Size = new Size(220, 270),
                BackColor = Color.FromArgb(25, 29, 38),
                Margin = new Padding(0, 0, 16, 16),
                Cursor = Cursors.Hand,
                Tag = game.Name
            };

            bool hovered = false;
            card.Paint += (_, e) =>
            {
                var g = e.Graphics;
                var bounds = card.ClientRectangle;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                if (artworkImage != null)
                {
                    float scale = Math.Max((float)bounds.Width / artworkImage.Width,
                                            (float)bounds.Height / artworkImage.Height);
                    int drawWidth = (int)Math.Ceiling(artworkImage.Width * scale);
                    int drawHeight = (int)Math.Ceiling(artworkImage.Height * scale);
                    var destination = new Rectangle((bounds.Width - drawWidth) / 2,
                                                     (bounds.Height - drawHeight) / 2,
                                                     drawWidth, drawHeight);
                    var oldClip = g.Clip;
                    g.SetClip(bounds);
                    g.DrawImage(artworkImage, destination);
                    g.Clip = oldClip;
                }
                else
                {
                    using var fallback = new LinearGradientBrush(bounds,
                        Color.FromArgb(53, 77, 115), Color.FromArgb(20, 24, 34),
                        LinearGradientMode.Vertical);
                    g.FillRectangle(fallback, bounds);
                }

                using (var shade = new LinearGradientBrush(bounds,
                    Color.FromArgb(0, 5, 8, 15), Color.FromArgb(238, 5, 8, 15),
                    LinearGradientMode.Vertical))
                    g.FillRectangle(shade, bounds);

                using var border = new Pen(hovered ? blue : Color.FromArgb(55, 255, 255, 255),
                                           hovered ? 2.2f : 1f);
                g.DrawRectangle(border, 1, 1, bounds.Width - 3, bounds.Height - 3);
            };
            card.Disposed += (_, _) => artworkImage?.Dispose();

            var gameName = new Label
            {
                Text = game.Name.ToUpperInvariant(),
                Location = new Point(12, 205),
                Size = new Size(196, 28),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                AutoEllipsis = true,
                Cursor = Cursors.Hand
            };
            var status = new Label
            {
                Text = game.IsFortnite
                    ? (File.Exists(Current.GamePath) ? "●  ГОТОВА К ЗАПУСКУ" : "○  НАСТРОИТЬ РАСПОЛОЖЕНИЕ")
                    : (File.Exists(game.ExePath) ? "●  ГОТОВА К ЗАПУСКУ" : "○  ПУТЬ НЕ НАЙДЕН"),
                Location = new Point(12, 237),
                Size = new Size(196, 20),
                ForeColor = Color.FromArgb(220, 225, 235),
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 7, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            card.Controls.Add(gameName);
            card.Controls.Add(status);

            void OpenGame()
            {
                if (game.IsFortnite) ShowGameDetails();
                else LaunchAddedGame(game);
            }

            card.Click += (_, _) => OpenGame();
            gameName.Click += (_, _) => OpenGame();
            status.Click += (_, _) => OpenGame();
            card.MouseEnter += (_, _) => { hovered = true; card.Invalidate(); };
            card.MouseLeave += (_, _) => { hovered = false; card.Invalidate(); };
            gameName.MouseEnter += (_, _) => { hovered = true; card.Invalidate(); };
            gameName.MouseLeave += (_, _) => { hovered = false; card.Invalidate(); };
            status.MouseEnter += (_, _) => { hovered = true; card.Invalidate(); };
            status.MouseLeave += (_, _) => { hovered = false; card.Invalidate(); };

            var menu = new ContextMenuStrip();
            menu.Items.Add("Открыть", null, (_, _) => OpenGame());
            if (!game.IsFortnite) menu.Items.Add("Удалить из библиотеки", null, (_, _) => RemoveGame(game));
            card.ContextMenuStrip = menu;
            gameName.ContextMenuStrip = menu;
            status.ContextMenuStrip = menu;
            gameGrid.Controls.Add(card);
        }

        gameGrid.ResumeLayout(true);
    }

    private async Task AddGameAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Выбери EXE-файл игры — Booster найдёт постер и иконку",
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
                using var scaled = new Bitmap(320, 420);
                using (var graphics = Graphics.FromImage(scaled))
                {
                    graphics.Clear(Color.FromArgb(22, 25, 32));
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    float scale = Math.Min(270f / bitmap.Width, 270f / bitmap.Height);
                    int w = Math.Max(1, (int)(bitmap.Width * scale)), h = Math.Max(1, (int)(bitmap.Height * scale));
                    graphics.DrawImage(bitmap, new Rectangle((320 - w) / 2, (420 - h) / 2, w, h));
                }
                scaled.Save(artworkPath, System.Drawing.Imaging.ImageFormat.Png);
            }
        }
        catch { artworkPath = ""; }

        var game = new GameEntry { Name = name, ExePath = exe, ArtworkPath = artworkPath };
        data.Games.Add(game);
        SaveData();
        ShowLibrary();

        string? onlineArtwork = await TryGetSteamArtworkAsync(name);
        if (!string.IsNullOrWhiteSpace(onlineArtwork) && !IsDisposed)
        {
            string oldArtwork = game.ArtworkPath;
            game.ArtworkPath = onlineArtwork;
            SaveData();
            if (!string.IsNullOrWhiteSpace(oldArtwork) && oldArtwork.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                try { if (File.Exists(oldArtwork)) File.Delete(oldArtwork); } catch { }
            }
            if (currentPage == "library") RenderGameCards();
        }
    }

    private async Task<string?> TryGetSteamArtworkAsync(string gameName)
    {
        try
        {
            string searchUrl = "https://store.steampowered.com/api/storesearch/?term=" +
                Uri.EscapeDataString(gameName) + "&l=english&cc=US";
            using var response = await http.GetAsync(searchUrl);
            response.EnsureSuccessStatusCode();
            using var searchDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!searchDoc.RootElement.TryGetProperty("items", out var items) ||
                items.ValueKind != JsonValueKind.Array || items.GetArrayLength() == 0) return null;
            int appId = items[0].TryGetProperty("id", out var idElement) ? idElement.GetInt32() : 0;
            if (appId <= 0) return null;

            using var detailsResponse = await http.GetAsync($"https://store.steampowered.com/api/appdetails?appids={appId}&cc=US&l=en");
            detailsResponse.EnsureSuccessStatusCode();
            using var detailsDoc = JsonDocument.Parse(await detailsResponse.Content.ReadAsStringAsync());
            if (!detailsDoc.RootElement.TryGetProperty(appId.ToString(), out var app) ||
                !app.TryGetProperty("data", out var dataElement) ||
                !dataElement.TryGetProperty("header_image", out var imageElement)) return null;
            string? imageUrl = imageElement.GetString();
            if (!IsHttpImageUrl(imageUrl)) return null;

            byte[] bytes = await http.GetByteArrayAsync(imageUrl!);
            if (bytes.Length < 4000 || bytes.Length > 12_000_000) return null;
            using var stream = new MemoryStream(bytes);
            using var decoded = Image.FromStream(stream);
            if (decoded.Width < 200 || decoded.Height < 100) return null;
            string path = Path.Combine(root, "game-" + Guid.NewGuid().ToString("N") + ".jpg");
            await File.WriteAllBytesAsync(path, bytes);
            return path;
        }
        catch { return null; }
    }

    private void RemoveGame(GameEntry game)
    {
        if (game.IsFortnite)
        {
            MessageBox.Show("Fortnite — основная игра лаунчера, её нельзя удалить из библиотеки.",
                "Fortnite Booster PRO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (MessageBox.Show($"Удалить «{game.Name}» из библиотеки? Файлы игры останутся на компьютере.",
            "Удаление игры", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        data.Games.Remove(game);
        SaveData();
        try
        {
            if (!string.IsNullOrWhiteSpace(game.ArtworkPath) &&
                game.ArtworkPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(game.ArtworkPath))
                File.Delete(game.ArtworkPath);
        }
        catch { }
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
            Location = new Point(2, 0), Size = new Size(500, 22),
            ForeColor = muted, Font = new Font("Segoe UI", 8, FontStyle.Bold)
        });

        var art = new Panel
        {
            Location = new Point(0, 36),
            Size = new Size(Math.Max(700, content.ClientSize.Width - 58), 340),
            BackColor = Color.FromArgb(30, 65, 95),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        art.Paint += PaintHero;
        gameHero = art;
        content.Controls.Add(art);

        art.Controls.Add(new Label
        {
            Text = "EPIC GAMES  /  БЕСПЛАТНАЯ ИГРА",
            Location = new Point(34, 28), Size = new Size(360, 20),
            ForeColor = Color.FromArgb(210, 230, 245), BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 8, FontStyle.Bold)
        });
        art.Controls.Add(new Label
        {
            Text = "FORTNITE",
            Location = new Point(35, 80), Size = new Size(500, 72),
            ForeColor = Color.White, BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 42, FontStyle.Bold)
        });
        art.Controls.Add(new Label
        {
            Text = "Сражайся, исследуй остров и побеждай вместе с друзьями.",
            Location = new Point(42, 160), Size = new Size(560, 30),
            ForeColor = Color.White, BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 10)
        });

        var installed = File.Exists(Current.GamePath);
        var state = new Label
        {
            Text = installed ? "ИГРА УСТАНОВЛЕНА  •  ГОТОВА К ЗАПУСКУ" : "ИГРА НЕ НАЙДЕНА  •  УКAЖИ ПУТЬ ИЛИ УСТАНОВИ ЕЁ",
            Location = new Point(4, 394), Size = new Size(600, 24),
            ForeColor = installed ? Color.FromArgb(105, 222, 157) : muted,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        content.Controls.Add(state);

        var install = MakeButton("УСТАНОВИТЬ", 0, 432, 165, 46, blue, Color.FromArgb(5, 20, 29));
        install.Click += (_, _) => OpenUrl(epicPage);
        content.Controls.Add(install);

        var play = MakeButton("ЗАПУСТИТЬ ИГРУ", 178, 432, 175, 46, Color.FromArgb(245, 247, 250), Color.FromArgb(17, 25, 37));
        play.Click += (_, _) => LaunchFortnite();
        content.Controls.Add(play);

        var configure = MakeButton("НАСТРОЙКИ ИГРЫ", 366, 432, 190, 46, panel2, text);
        configure.Click += (_, _) => ShowSettings();
        content.Controls.Add(configure);

        content.Controls.Add(new Label
        {
            Text = "ПОСЛЕДНИЕ ОБНОВЛЕНИЯ",
            Location = new Point(4, 510), Size = new Size(480, 30),
            ForeColor = text, Font = new Font("Segoe UI", 16, FontStyle.Bold)
        });
        content.Controls.Add(new Label
        {
            Text = "НОВОСТИ И МАТЕРИАЛЫ FORTNITE",
            Location = new Point(5, 543), Size = new Size(480, 18),
            ForeColor = muted, Font = new Font("Segoe UI", 8, FontStyle.Bold)
        });

        var newsGrid = new FlowLayoutPanel
        {
            Location = new Point(0, 574),
            Size = new Size(Math.Max(680, content.ClientSize.Width - 58), 250),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            AutoScroll = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.Transparent, Margin = Padding.Empty, Padding = new Padding(0, 4, 0, 8)
        };
        content.Controls.Add(newsGrid);
        RenderFortniteNews(newsGrid);

        var openNews = MakeButton("ВСЕ НОВОСТИ FORTNITE", 4, 845, 220, 40, panel2, text);
        openNews.Click += (_, _) => OpenUrl("https://www.fortnite.com/news");
        content.Controls.Add(openNews);
        var refreshNews = MakeButton("ОБНОВИТЬ НОВОСТИ", 236, 845, 220, 40, panel2, text);
        refreshNews.Click += async (_, _) => await RefreshArtworkAsync();
        content.Controls.Add(refreshNews);
    }

    private void RenderFortniteNews(FlowLayoutPanel host)
    {
        host.Controls.Clear();
        int count = Math.Min(banners.Count, bannerTitles.Count);
        if (count == 0)
        {
            var empty = new Label
            {
                Text = "Новости пока не загружены. Нажми «Обновить новости», чтобы получить актуальные материалы.",
                Size = new Size(560, 60), ForeColor = muted, Padding = new Padding(12),
                BackColor = panel
            };
            host.Controls.Add(empty);
            return;
        }

        for (int i = 0; i < count; i++)
        {
            int index = i;
            var card = new Panel
            {
                Size = new Size(250, 218), BackColor = panel,
                Margin = new Padding(0, 0, 14, 0), Cursor = Cursors.Hand
            };
            var picture = new PictureBox
            {
                Location = new Point(0, 0), Size = new Size(250, 142),
                SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(28, 31, 40),
                Image = new Bitmap(banners[i])
            };
            card.Controls.Add(picture);
            card.Controls.Add(new Label
            {
                Text = string.IsNullOrWhiteSpace(bannerTitles[i]) ? "НОВОСТИ FORTNITE" : bannerTitles[i].ToUpperInvariant(),
                Location = new Point(12, 151), Size = new Size(226, 52),
                ForeColor = text, Font = new Font("Segoe UI", 9, FontStyle.Bold),
                AutoEllipsis = true
            });
            card.Controls.Add(new Label
            {
                Text = "ОТКРЫТЬ НОВОСТИ  →",
                Location = new Point(12, 198), Size = new Size(220, 16),
                ForeColor = blue, Font = new Font("Segoe UI", 7, FontStyle.Bold)
            });
            card.Click += (_, _) => OpenUrl("https://www.fortnite.com/news");
            picture.Click += (_, _) => OpenUrl("https://www.fortnite.com/news");
            foreach (Control child in card.Controls)
                if (child is Label label) label.Click += (_, _) => OpenUrl("https://www.fortnite.com/news");
            host.Controls.Add(card);
        }
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
                await DownloadAndCacheBannersAsync(items.Take(10).ToList());

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
                    if (index >= 10) break;
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
            for (int i = 0; i < 10; i++)
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
        int next = (bannerIndex + delta + banners.Count) % banners.Count;
        if (next == bannerIndex) return;
        fadeFromIndex = bannerIndex;
        bannerIndex = next;
        fadeProgress = 0f;
        if (fadeTimer != null) { fadeTimer.Stop(); fadeTimer.Start(); }
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
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.SetClip(bounds);

        Image? current = banners.Count > 0 && bannerIndex >= 0 && bannerIndex < banners.Count ? banners[bannerIndex] : cover;
        Image? previous = fadeFromIndex >= 0 && fadeFromIndex < banners.Count ? banners[fadeFromIndex] : null;
        if (current != null)
        {
            if (previous != null && fadeProgress < 1f) float eased = 1f - (float)Math.Pow(1f - fadeProgress, 3);
            if (previous != null && fadeProgress < 1f) DrawCoverImage(g, previous, bounds, 1f - eased);
            DrawCoverImage(g, current, bounds, previous == null ? 1f : eased);
        }
        else
        {
            using var gradient = new LinearGradientBrush(bounds, Color.FromArgb(43, 98, 145), Color.FromArgb(15, 20, 34), 15);
            g.FillRectangle(gradient, bounds);
        }
        using var shade = new LinearGradientBrush(bounds, Color.FromArgb(205, 5, 10, 20),
            Color.FromArgb(12, 5, 10, 20), LinearGradientMode.Horizontal);
        g.FillRectangle(shade, bounds);
    }

    private static void DrawCoverImage(Graphics graphics, Image image, Rectangle bounds, float opacity)
    {
        float scale = Math.Max((float)bounds.Width / image.Width, (float)bounds.Height / image.Height);
        int width = (int)Math.Ceiling(image.Width * scale), height = (int)Math.Ceiling(image.Height * scale);
        var destination = new Rectangle((bounds.Width - width) / 2, (bounds.Height - height) / 2, width, height);
        using var attributes = new System.Drawing.Imaging.ImageAttributes();
        var matrix = new System.Drawing.Imaging.ColorMatrix { Matrix33 = Math.Clamp(opacity, 0f, 1f) };
        attributes.SetColorMatrix(matrix, System.Drawing.Imaging.ColorMatrixFlag.Default, System.Drawing.Imaging.ColorAdjustType.Bitmap);
        graphics.DrawImage(image, destination, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
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
        using var f = CreateDialog("BOOST CENTER  /  ПРОИЗВОДИТЕЛЬНОСТЬ", 1040, 790);
        f.MinimumSize = new Size(900, 700);
        f.BackColor = bg;

        var rail = new Panel
        {
            Location = new Point(0, 52), Size = new Size(228, 690),
            BackColor = Color.FromArgb(15, 14, 23), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left
        };
        f.Controls.Add(rail);
        rail.Controls.Add(new Label
        {
            Text = "УПРАВЛЕНИЕ\nПРОИЗВОДИТЕЛЬНОСТЬЮ",
            Location = new Point(22, 24), Size = new Size(185, 52),
            ForeColor = text, Font = new Font("Segoe UI", 11, FontStyle.Bold)
        });
        rail.Controls.Add(new Label
        {
            Text = "FORTNITE  /  WINDOWS 11",
            Location = new Point(22, 88), Size = new Size(185, 18),
            ForeColor = muted, Font = new Font("Segoe UI", 7, FontStyle.Bold)
        });
        var activeRail = new Panel { Location = new Point(0, 126), Size = new Size(3, 42), BackColor = blue };
        rail.Controls.Add(activeRail);
        string[] sections = { "ОБЗОР", "ИГРОВОЙ РЕЖИМ", "ФОНОВАЯ ЗАПИСЬ", "СИСТЕМНЫЕ ИНСТРУМЕНТЫ" };
        for (int i = 0; i < sections.Length; i++)
        {
            int y = 118 + i * 52;
            var nav = MakeButton(sections[i], 14, y, 200, 42,
                i == 0 ? panel2 : Color.FromArgb(15, 14, 23), i == 0 ? text : muted);
            nav.TextAlign = ContentAlignment.MiddleLeft;
            nav.Padding = new Padding(12, 0, 0, 0);
            nav.Click += (_, _) =>
            {
                activeRail.Top = nav.Top + 8;
                foreach (Control c in rail.Controls.OfType<Button>()) c.BackColor = Color.FromArgb(15, 14, 23);
                nav.BackColor = panel2;
            };
            rail.Controls.Add(nav);
        }

        var scroll = new Panel
        {
            Location = new Point(228, 52), Size = new Size(796, 690),
            BackColor = bg, AutoScroll = true, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        f.Controls.Add(scroll);

        var heading = new Label
        {
            Text = "ПРОФИЛЬ ПРОИЗВОДИТЕЛЬНОСТИ",
            Location = new Point(30, 24), Size = new Size(600, 34),
            ForeColor = text, Font = new Font("Segoe UI", 19, FontStyle.Bold)
        };
        scroll.Controls.Add(heading);
        scroll.Controls.Add(new Label
        {
            Text = "Настрой параметры Windows под игру. Никаких сомнительных твиков, отключения служб или разгона.",
            Location = new Point(32, 63), Size = new Size(710, 36),
            ForeColor = muted, Font = new Font("Segoe UI", 9)
        });

        var specs = DetectPcSpecs();
        var pcCard = new Panel { Location = new Point(30, 112), Size = new Size(710, 90), BackColor = panel };
        scroll.Controls.Add(pcCard);
        pcCard.Controls.Add(new Label
        {
            Text = "ТВОЯ СИСТЕМА", Location = new Point(16, 12), Size = new Size(170, 18),
            ForeColor = muted, Font = new Font("Segoe UI", 7, FontStyle.Bold)
        });
        pcCard.Controls.Add(new Label
        {
            Text = specs.Cpu, Location = new Point(16, 36), Size = new Size(300, 22),
            ForeColor = text, Font = new Font("Segoe UI", 9, FontStyle.Bold), AutoEllipsis = true
        });
        pcCard.Controls.Add(new Label
        {
            Text = specs.Gpu + "    /    " + specs.Ram,
            Location = new Point(330, 36), Size = new Size(355, 22),
            ForeColor = muted, Font = new Font("Segoe UI", 9), AutoEllipsis = true
        });

        scroll.Controls.Add(new Label
        {
            Text = "РЕЖИМ РАБОТЫ",
            Location = new Point(32, 226), Size = new Size(250, 20),
            ForeColor = muted, Font = new Font("Segoe UI", 8, FontStyle.Bold)
        });
        var presetHost = new FlowLayoutPanel
        {
            Location = new Point(30, 252), Size = new Size(710, 94),
            BackColor = Color.Transparent, WrapContents = false, FlowDirection = FlowDirection.LeftToRight
        };
        scroll.Controls.Add(presetHost);
        var presets = new[]
        {
            ("СТАБИЛЬНОСТЬ FPS", "Рекомендуемый профиль", true),
            ("СБАЛАНСИРОВАННЫЙ", "Обычная работа и игры", false),
            ("МИНИМУМ ЗАПИСИ", "Без фонового захвата", false)
        };
        var checks = new List<CheckBox>();
        var gameMode = new CheckBox { Checked = true };
        var disableDvr = new CheckBox { Checked = true };
        var captureOff = new CheckBox { Checked = false };

        var profileButtons = new List<Button>();
        foreach (var item in presets)
        {
            var tile = new Panel { Size = new Size(226, 78), BackColor = item.Item3 ? Color.FromArgb(45, 35, 68) : panel, Margin = new Padding(0, 0, 10, 0), Cursor = Cursors.Hand };
            tile.Controls.Add(new Label { Text = item.Item1, Location = new Point(12, 15), Size = new Size(204, 20), ForeColor = text, Font = new Font("Segoe UI", 8, FontStyle.Bold) });
            tile.Controls.Add(new Label { Text = item.Item2, Location = new Point(12, 40), Size = new Size(204, 18), ForeColor = muted, Font = new Font("Segoe UI", 8) });
            var select = MakeButton(item.Item3 ? "ВЫБРАН" : "ВЫБРАТЬ", 145, 48, 68, 22, item.Item3 ? blue : panel2, text);
            select.Font = new Font("Segoe UI", 6.5f, FontStyle.Bold);
            tile.Controls.Add(select);
            presetHost.Controls.Add(tile);
            profileButtons.Add(select);
            int index = profileButtons.Count - 1;
            void Choose()
            {
                foreach (var c in presetHost.Controls.OfType<Panel>())
                {
                    c.BackColor = panel;
                    var b = c.Controls.OfType<Button>().FirstOrDefault();
                    if (b != null) { b.Text = "ВЫБРАТЬ"; b.BackColor = panel2; }
                }
                tile.BackColor = Color.FromArgb(45, 35, 68);
                select.Text = "ВЫБРАН";
                select.BackColor = blue;
                if (index == 0) { gameMode.Checked = true; disableDvr.Checked = true; captureOff.Checked = false; }
                else if (index == 1) { gameMode.Checked = true; disableDvr.Checked = false; captureOff.Checked = false; }
                else { gameMode.Checked = true; disableDvr.Checked = true; captureOff.Checked = true; }
            }
            tile.Click += (_, _) => Choose();
            select.Click += (_, _) => Choose();
        }

        scroll.Controls.Add(new Label
        {
            Text = "НАСТРОЙКИ WINDOWS",
            Location = new Point(32, 372), Size = new Size(300, 20),
            ForeColor = muted, Font = new Font("Segoe UI", 8, FontStyle.Bold)
        });

        void AddToggle(string title, string desc, int y, CheckBox state, string tag)
        {
            var card = new Panel { Location = new Point(30, y), Size = new Size(710, 72), BackColor = panel };
            scroll.Controls.Add(card);
            card.Controls.Add(new Label
            {
                Text = title, Location = new Point(16, 12), Size = new Size(540, 22),
                ForeColor = text, Font = new Font("Segoe UI", 10, FontStyle.Bold)
            });
            card.Controls.Add(new Label
            {
                Text = desc, Location = new Point(16, 38), Size = new Size(555, 22),
                ForeColor = muted, Font = new Font("Segoe UI", 8)
            });
            state.Appearance = Appearance.Button;
            state.Text = state.Checked ? "ВКЛ" : "ВЫКЛ";
            state.TextAlign = ContentAlignment.MiddleCenter;
            state.Location = new Point(615, 20);
            state.Size = new Size(76, 32);
            state.FlatStyle = FlatStyle.Flat;
            state.FlatAppearance.BorderSize = 0;
            state.BackColor = state.Checked ? Color.FromArgb(66, 48, 105) : panel2;
            state.ForeColor = text;
            state.Font = new Font("Segoe UI", 8, FontStyle.Bold);
            state.CheckedChanged += (_, _) =>
            {
                state.Text = state.Checked ? "ВКЛ" : "ВЫКЛ";
                state.BackColor = state.Checked ? Color.FromArgb(66, 48, 105) : panel2;
            };
            card.Controls.Add(state);
        }
        AddToggle("Игровой режим Windows", "Позволяет Windows отдавать приоритет игре во время запуска.", 402, gameMode, "gamemode");
        AddToggle("Отключить фоновую запись", "Не записывать игровые моменты в фоне, если эта функция не используется.", 482, disableDvr, "dvr");
        AddToggle("Отключить захват клипов Xbox", "Отключает системный захват клипов. Не включай, если пользуешься записью.", 562, captureOff, "capture");

        var statusCard = new Panel { Location = new Point(30, 650), Size = new Size(710, 78), BackColor = Color.FromArgb(24, 32, 35) };
        scroll.Controls.Add(statusCard);
        var status = new Label
        {
            Text = "ГОТОВО К ПРИМЕНЕНИЮ  /  ИЗМЕНЕНИЯ ЕЩЁ НЕ ВНЕСЕНЫ",
            Location = new Point(16, 12), Size = new Size(675, 20),
            ForeColor = Color.FromArgb(127, 211, 171), Font = new Font("Segoe UI", 8, FontStyle.Bold)
        };
        statusCard.Controls.Add(status);
        statusCard.Controls.Add(new Label
        {
            Text = "Перед изменением каждого параметра создаётся резервная копия исходного значения.",
            Location = new Point(16, 39), Size = new Size(675, 20),
            ForeColor = muted, Font = new Font("Segoe UI", 8)
        });

        var apply = MakeButton("ПРИМЕНИТЬ ОПТИМИЗАЦИЮ", 30, 746, 710, 48, blue, Color.White);
        scroll.Controls.Add(apply);
        apply.Click += (_, _) =>
        {
            try
            {
                if (gameMode.Checked)
                {
                    SetUserDword(@"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1);
                    SetUserDword(@"Software\Microsoft\GameBar", "AllowAutoGameMode", 1);
                }
                else
                {
                    SetUserDword(@"Software\Microsoft\GameBar", "AutoGameModeEnabled", 0);
                    SetUserDword(@"Software\Microsoft\GameBar", "AllowAutoGameMode", 0);
                }
                SetUserDword(@"System\GameConfigStore", "GameDVR_Enabled", disableDvr.Checked ? 0 : 1);
                SetUserDword(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", captureOff.Checked ? 0 : 1);
                status.Text = "ОПТИМИЗАЦИЯ ПРИМЕНЕНА  /  ПАРАМЕТРЫ WINDOWS ОБНОВЛЕНЫ";
                status.ForeColor = Color.FromArgb(127, 211, 171);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось применить настройки: " + ex.Message, "Boost Center", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        var toolsTitle = new Label
        {
            Text = "БЫСТРЫЕ ИНСТРУМЕНТЫ",
            Location = new Point(32, 818), Size = new Size(300, 20),
            ForeColor = muted, Font = new Font("Segoe UI", 8, FontStyle.Bold)
        };
        scroll.Controls.Add(toolsTitle);
        var restore = MakeButton("ВОССТАНОВИТЬ WINDOWS", 30, 846, 225, 42, panel2, text);
        restore.Click += (_, _) =>
        {
            try { RestoreRegistryBackup(); status.Text = "ИСХОДНЫЕ ПАРАМЕТРЫ ВОССТАНОВЛЕНЫ"; status.ForeColor = Color.FromArgb(127, 211, 171); }
            catch (Exception ex) { MessageBox.Show("Ошибка восстановления: " + ex.Message); }
        };
        scroll.Controls.Add(restore);
        var task = MakeButton("ДИСПЕТЧЕР ЗАДАЧ", 267, 846, 220, 42, panel2, text);
        task.Click += (_, _) => { try { Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show(ex.Message); } };
        scroll.Controls.Add(task);
        var graphics = MakeButton("НАСТРОЙКИ ГРАФИКИ", 499, 846, 241, 42, panel2, text);
        graphics.Click += (_, _) => OpenUrl("ms-settings:display-graphics");
        scroll.Controls.Add(graphics);

        scroll.Controls.Add(new Label
        {
            Text = "Оптимизация не разгоняет оборудование и не отключает защиту Windows. Прирост FPS не гарантируется.",
            Location = new Point(32, 902), Size = new Size(710, 32), ForeColor = muted, Font = new Font("Segoe UI", 8)
        });
        f.ShowDialog(this);
    }

    private void RestoreRegistryBackup()
    {
        string backupPath = Path.Combine(root, "registry-backup.json");
        if (!File.Exists(backupPath))
        {
            MessageBox.Show("Резервная копия пока не создана.", "Fortnite Booster PRO",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var saved = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(backupPath))
            ?? new Dictionary<string, JsonElement>();
        foreach (var entry in saved)
        {
            string[] parts = entry.Key.Split('|', 2);
            if (parts.Length != 2) continue;
            string path = parts[0], name = parts[1];
            var item = entry.Value;
            bool existed = item.TryGetProperty("existed", out var existedValue) && existedValue.ValueKind == JsonValueKind.True;
            using var key = Registry.CurrentUser.CreateSubKey(path);
            if (!existed) { key.DeleteValue(name, false); continue; }
            if (!item.TryGetProperty("oldValue", out var oldValue) || oldValue.ValueKind == JsonValueKind.Null)
            {
                key.DeleteValue(name, false);
                continue;
            }
            object restored = oldValue.ValueKind switch
            {
                JsonValueKind.String => oldValue.GetString() ?? "",
                JsonValueKind.True => 1,
                JsonValueKind.False => 0,
                JsonValueKind.Number when oldValue.TryGetInt32(out int i) => i,
                JsonValueKind.Number when oldValue.TryGetInt64(out long l) => l,
                _ => oldValue.ToString()
            };
            RegistryValueKind kind = item.TryGetProperty("oldKind", out var kindValue) &&
                Enum.TryParse<RegistryValueKind>(kindValue.GetString(), out var parsedKind) ? parsedKind : RegistryValueKind.DWord;
            key.SetValue(name, restored, kind);
        }
        File.Delete(backupPath);
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
            string? oldKind = existed ? oldKey!.GetValueKind(name).ToString() : null;
            saved[keyName] = JsonSerializer.SerializeToElement(new { existed, oldValue, oldKind });
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