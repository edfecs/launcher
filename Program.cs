using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Net.Http;
using System.Text.Json;
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

internal sealed class AppData
{
    public string ActiveProfileId { get; set; } = "";
    public List<UserProfile> Profiles { get; set; } = new();
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
    private Panel content = null!;
    private Panel hero = null!;
    private PictureBox gameTile = null!;
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
        BuildShell();
        ShowLibrary();
        Shown += async (_, _) => await RefreshArtworkAsync();
        refreshTimer = new System.Windows.Forms.Timer { Interval = 6 * 60 * 60 * 1000 };
        refreshTimer.Tick += async (_, _) => await RefreshArtworkAsync();
        refreshTimer.Start();
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
    }

    private void SaveData() =>
        File.WriteAllText(DataPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));

    private void BuildShell()
    {
        var top = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = bg };
        Controls.Add(top);
        var logo = new Label
        {
            Text = "FORTNITE  /  BOOSTER",
            Location = new Point(28, 21),
            Size = new Size(285, 34),
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = text
        };
        top.Controls.Add(logo);

        var search = new TextBox
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(Width - 560, 21),
            Size = new Size(230, 34),
            BackColor = panel,
            ForeColor = text,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "Поиск в библиотеке"
        };
        search.TextChanged += (_, _) =>
        {
            if (currentPage == "library" && hero != null)
            {
                bool show = string.IsNullOrWhiteSpace(search.Text) || "fortnite".Contains(search.Text.Trim(), StringComparison.OrdinalIgnoreCase);
                hero.Visible = show;
                gameTile.Visible = show;
            }
        };
        top.Controls.Add(search);

        profileButton = new Label
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(Width - 310, 20),
            Size = new Size(170, 36),
            BackColor = panel2,
            ForeColor = text,
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        profileButton.Click += (_, _) => ShowProfiles();
        top.Controls.Add(profileButton);

        var settings = MakeButton("⚙", 0, 19, 42, 38, panel2, text);
        settings.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        settings.Location = new Point(Width - 91, 19);
        settings.Font = new Font("Segoe UI Symbol", 15);
        settings.Click += (_, _) => ShowSettings();
        top.Controls.Add(settings);

        var divider = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Color.FromArgb(42, 47, 58) };
        Controls.Add(divider);

        var sidebar = new Panel { Dock = DockStyle.Left, Width = 220, BackColor = panel };
        Controls.Add(sidebar);
        var sideTitle = new Label
        {
            Text = "БИБЛИОТЕКА",
            Location = new Point(23, 27),
            Size = new Size(175, 22),
            ForeColor = muted,
            Font = new Font("Segoe UI", 8, FontStyle.Bold)
        };
        sidebar.Controls.Add(sideTitle);
        AddNav(sidebar, "▦   Главная", 68, () => ShowLibrary());
        AddNav(sidebar, "◈   Fortnite", 116, () => ShowGameDetails());
        AddNav(sidebar, "⚙   Настройки", 164, () => ShowSettings());
        var bottom = new Label
        {
            Text = "FORTNITE BOOSTER PRO\nBUILD 2.0  •  WINDOWS",
            Location = new Point(22, 760),
            Size = new Size(180, 44),
            ForeColor = Color.FromArgb(91, 101, 118),
            Font = new Font("Segoe UI", 7, FontStyle.Bold),
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom
        };
        sidebar.Controls.Add(bottom);

        content = new Panel { Dock = DockStyle.Fill, BackColor = bg, AutoScroll = true, Padding = new Padding(28, 24, 28, 25) };
        Controls.Add(content);
        content.BringToFront();
        sidebar.BringToFront();
        top.BringToFront();
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
        content.Controls.Clear();
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
            Size = new Size(Math.Max(700, content.ClientSize.Width - 58), 410),
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
        hero.Controls.Add(new Label
        {
            Text = "Новый сезон. Новые возможности. Твоя следующая победа.",
            Location = new Point(36, 165),
            Size = new Size(550, 30),
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 11)
        });
        var details = MakeButton("ОТКРЫТЬ СТРАНИЦУ  →", 35, 240, 205, 48, Color.White, Color.FromArgb(17, 25, 37));
        details.Click += (_, _) => ShowGameDetails();
        hero.Controls.Add(details);
        var play = MakeButton("▶  ИГРАТЬ", 252, 240, 145, 48, blue, Color.FromArgb(5, 20, 29));
        play.Click += (_, _) => LaunchFortnite();
        hero.Controls.Add(play);

        content.Controls.Add(new Label
        {
            Text = "МОИ ИГРЫ",
            Location = new Point(3, 475),
            Size = new Size(300, 28),
            ForeColor = text,
            Font = new Font("Segoe UI", 16, FontStyle.Bold)
        });
        content.Controls.Add(new Label
        {
            Text = "1 ИГРА  •  ОБЛОЖКА ОБНОВЛЯЕТСЯ АВТОМАТИЧЕСКИ",
            Location = new Point(4, 508),
            Size = new Size(480, 18),
            ForeColor = muted,
            Font = new Font("Segoe UI", 8, FontStyle.Bold)
        });

        gameTile = new PictureBox
        {
            Location = new Point(0, 540),
            Size = new Size(220, 255),
            BackColor = panel,
            SizeMode = PictureBoxSizeMode.Zoom,
            Cursor = Cursors.Hand
        };
        gameTile.Click += (_, _) => ShowGameDetails();
        content.Controls.Add(gameTile);
        if (cover != null) gameTile.Image = new Bitmap(cover);
        var tileName = new Label
        {
            Text = "FORTNITE",
            Location = new Point(12, 765),
            Size = new Size(195, 23),
            ForeColor = text,
            BackColor = panel,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        tileName.Click += (_, _) => ShowGameDetails();
        content.Controls.Add(tileName);

        coverStatus = new Label
        {
            Text = "АКТУАЛЬНАЯ ОБЛОЖКА",
            Location = new Point(12, 789),
            Size = new Size(200, 18),
            ForeColor = muted,
            BackColor = panel,
            Font = new Font("Segoe UI", 7, FontStyle.Bold)
        };
        content.Controls.Add(coverStatus);
        if (File.Exists(CoverPath) && cover == null) LoadCachedCover();
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
        content.Controls.Add(art);
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
            Text = "Fortnite — бесплатная игра от Epic Games. Установка и обновления выполняются официальным Epic Games Launcher.\\nBooster хранит твой путь к игре и настройки отдельно для каждого профиля.",
            Location = new Point(5, 546),
            Size = new Size(750, 58),
            ForeColor = muted,
            Font = new Font("Segoe UI", 9.5f)
        });
        var news = MakeButton("ОТКРЫТЬ НОВОСТИ FORTNITE  ↗", 4, 630, 245, 40, panel2, text);
        news.Click += (_, _) => OpenUrl("https://www.fortnite.com/news");
        content.Controls.Add(news);
    }

    private async Task RefreshArtworkAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, newsUrl);
            request.Headers.UserAgent.ParseAdd("FortniteBoosterPRO/2.0");
            using var response = await http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!doc.RootElement.TryGetProperty("data", out var dataNode) ||
                !dataNode.TryGetProperty("br", out var br) ||
                !br.TryGetProperty("motds", out var motds) ||
                motds.ValueKind != JsonValueKind.Array) return;

            string? imageUrl = null;
            foreach (var item in motds.EnumerateArray())
            {
                foreach (var key in new[] { "image", "tileImage" })
                {
                    if (item.TryGetProperty(key, out var image) && image.ValueKind == JsonValueKind.String)
                    {
                        var candidate = image.GetString();
                        if (!string.IsNullOrWhiteSpace(candidate) && candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        { imageUrl = candidate; break; }
                    }
                }
                if (imageUrl != null) break;
            }
            if (imageUrl == null) return;

            var bytes = await http.GetByteArrayAsync(imageUrl);
            if (bytes.Length < 10_000) return;
            string temp = CoverPath + ".tmp";
            await File.WriteAllBytesAsync(temp, bytes);
            using (var stream = new MemoryStream(bytes))
            using (var img = Image.FromStream(stream))
            {
                var newImage = new Bitmap(img);
                var old = cover;
                cover = newImage;
                old?.Dispose();
            }
            File.Copy(temp, CoverPath, true);
            File.Delete(temp);
            if (IsDisposed) return;
            if (InvokeRequired) BeginInvoke(new Action(UpdateArtworkOnScreen));
            else UpdateArtworkOnScreen();
        }
        catch
        {
            LoadCachedCover();
        }
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
        hero?.Invalidate();
        if (gameTile != null && !gameTile.IsDisposed)
        {
            var old = gameTile.Image;
            gameTile.Image = cover == null ? null : new Bitmap(cover);
            old?.Dispose();
        }
        if (coverStatus != null) coverStatus.Text = "ОБЛОЖКА ИЗ АКТУАЛЬНЫХ НОВОСТЕЙ";
    }

    private void PaintHero(object? sender, PaintEventArgs e)
    {
        var bounds = ((Control)sender!).ClientRectangle;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.HighQuality;
        if (cover != null)
        {
            float scale = Math.Max((float)bounds.Width / cover.Width, (float)bounds.Height / cover.Height);
            int w = (int)(cover.Width * scale), h = (int)(cover.Height * scale);
            g.DrawImage(cover, new Rectangle((bounds.Width - w) / 2, (bounds.Height - h) / 2, w, h));
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
        f.ShowDialog(this);
    }

    private Form CreateDialog(string title, int w, int h)
    {
        var f = new Form
        {
            Text = title,
            Size = new Size(w, h),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = bg,
            ForeColor = text,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            Font = new Font("Segoe UI", 9)
        };
        f.Controls.Add(new Label
        {
            Text = title, Location = new Point(24, 20), Size = new Size(w - 60, 32),
            ForeColor = text, Font = new Font("Segoe UI", 16, FontStyle.Bold)
        });
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
                "Fortnite не найден в профиле.\\nДа — открыть страницу установки Epic Games.\\nНет — выбрать EXE установленной игры.",
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