using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Net.Http;
using Microsoft.Win32;

namespace FortniteBoosterPRO;

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
    private readonly Color background = Color.FromArgb(15, 16, 20);
    private readonly Color surface = Color.FromArgb(27, 29, 35);
    private readonly Color foreground = Color.FromArgb(245, 246, 249);
    private readonly Color secondary = Color.FromArgb(173, 178, 190);
    private readonly Color cyan = Color.FromArgb(64, 194, 224);
    private readonly string appFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FortniteBoosterPRO");
    private readonly string imageUrl = "https://cdn1.epicgames.com/offer/fn/FNBR_40-40_C7S2_Venison_EGS_Launcher_Blade_1200x1600_1200x1600-858fcbb83fb34b6ea6cb558a07fa4b7c";
    private readonly string gamePage = "https://store.epicgames.com/p/fortnite";
    private Image? coverImage;
    private Panel hero = null!;
    private PictureBox tileImage = null!;
    private Label status = null!;
    private string gameExe = "";

    public MainForm()
    {
        Directory.CreateDirectory(appFolder);
        Text = "Fortnite Booster PRO";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1440, 900);
        MinimumSize = new Size(1120, 760);
        BackColor = background;
        ForeColor = foreground;
        Font = new Font("Segoe UI", 10);
        DoubleBuffered = true;
        gameExe = FindFortniteExe();
        BuildInterface();
        Shown += async (_, _) => await LoadArtworkAsync();
    }

    private void BuildInterface()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = background, Padding = new Padding(30, 0, 30, 0) };
        Controls.Add(header);

        var brand = new Label
        {
            Text = "FORTNITE BOOSTER",
            Location = new Point(28, 20),
            Size = new Size(260, 38),
            Font = new Font("Segoe UI", 17, FontStyle.Bold),
            ForeColor = foreground
        };
        header.Controls.Add(brand);

        var nav = new Label
        {
            Text = "ИГРЫ",
            Location = new Point(330, 26),
            Size = new Size(70, 28),
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = foreground,
            TextAlign = ContentAlignment.MiddleCenter
        };
        header.Controls.Add(nav);
        header.Controls.Add(new Panel { Location = new Point(340, 58), Size = new Size(48, 3), BackColor = cyan });

        var search = new TextBox
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(Width - 520, 20),
            Size = new Size(245, 34),
            BackColor = surface,
            ForeColor = foreground,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "⌕  Поиск в библиотеке",
            Font = new Font("Segoe UI", 9)
        };
        search.TextChanged += (_, _) =>
        {
            bool match = "fortnite".Contains(search.Text.Trim(), StringComparison.OrdinalIgnoreCase) || search.Text.Trim().Length == 0;
            hero.Visible = match;
            tileImage.Visible = match;
            status.Visible = match;
        };
        header.Controls.Add(search);

        var add = MakeButton("УКАЗАТЬ ИГРУ", 0, 19, 132, 38, surface, foreground);
        add.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        add.Location = new Point(Width - 250, 19);
        add.Click += (_, _) => ChooseGameExe();
        header.Controls.Add(add);

        var boost = MakeButton("BOOST", 0, 19, 94, 38, cyan, Color.FromArgb(8, 19, 24));
        boost.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        boost.Location = new Point(Width - 105, 19);
        boost.Click += (_, _) => ShowBoost();
        header.Controls.Add(boost);

        var separator = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Color.FromArgb(42, 44, 51) };
        Controls.Add(separator);

        var body = new Panel { Dock = DockStyle.Fill, BackColor = background, Padding = new Padding(30, 24, 30, 24), AutoScroll = true };
        Controls.Add(body);
        body.BringToFront();
        header.BringToFront();

        body.Controls.Add(new Label
        {
            Text = "ТВОЯ ИГРОВАЯ БИБЛИОТЕКА",
            Location = new Point(2, 0),
            Size = new Size(500, 24),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = secondary
        });

        hero = new Panel
        {
            Location = new Point(0, 36),
            Size = new Size(body.ClientSize.Width - 60, 440),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.FromArgb(35, 55, 75)
        };
        hero.Paint += PaintHero;
        body.Controls.Add(hero);

        var eyebrow = new Label
        {
            Text = "EPIC GAMES  •  FREE TO PLAY",
            Location = new Point(42, 54),
            Size = new Size(420, 24),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = Color.FromArgb(175, 231, 246),
            BackColor = Color.Transparent
        };
        hero.Controls.Add(eyebrow);

        var gameTitle = new Label
        {
            Text = "FORTNITE",
            Location = new Point(38, 104),
            Size = new Size(570, 76),
            Font = new Font("Segoe UI", 43, FontStyle.Black),
            ForeColor = Color.White,
            BackColor = Color.Transparent
        };
        hero.Controls.Add(gameTitle);

        var description = new Label
        {
            Text = "Высаживайся на остров, собирай снаряжение и сражайся за победу.\\nТвой игровой центр и безопасная оптимизация — в одном месте.",
            Location = new Point(43, 194),
            Size = new Size(530, 58),
            Font = new Font("Segoe UI", 11),
            ForeColor = Color.FromArgb(235, 239, 246),
            BackColor = Color.Transparent
        };
        hero.Controls.Add(description);

        var play = MakeButton("▶   ИГРАТЬ", 42, 292, 170, 50, Color.White, Color.FromArgb(20, 25, 32));
        play.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        play.Click += (_, _) => LaunchFortnite();
        hero.Controls.Add(play);

        var store = MakeButton("СТРАНИЦА ИГРЫ", 225, 292, 170, 50, Color.FromArgb(95, 45, 48, 58), Color.White);
        store.Click += (_, _) => OpenUrl(gamePage);
        hero.Controls.Add(store);

        var libraryTitle = new Label
        {
            Text = "В БИБЛИОТЕКЕ",
            Location = new Point(2, 500),
            Size = new Size(400, 28),
            Font = new Font("Segoe UI", 15, FontStyle.Bold),
            ForeColor = foreground
        };
        body.Controls.Add(libraryTitle);

        var librarySub = new Label
        {
            Text = "1 ИГРА  •  ТОЛЬКО FORTNITE",
            Location = new Point(3, 532),
            Size = new Size(400, 20),
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            ForeColor = secondary
        };
        body.Controls.Add(librarySub);

        var tile = new Panel
        {
            Location = new Point(0, 565),
            Size = new Size(210, 245),
            BackColor = surface,
            Cursor = Cursors.Hand
        };
        body.Controls.Add(tile);

        tileImage = new PictureBox
        {
            Location = new Point(0, 0),
            Size = new Size(210, 195),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(43, 74, 104),
            Cursor = Cursors.Hand
        };
        tile.Controls.Add(tileImage);

        tile.Controls.Add(new Label
        {
            Text = "Fortnite",
            Location = new Point(12, 201),
            Size = new Size(185, 23),
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = foreground
        });
        status = new Label
        {
            Text = gameExe.Length > 0 ? "УСТАНОВЛЕНА" : "НАСТРОИТЬ РАСПОЛОЖЕНИЕ",
            Location = new Point(12, 224),
            Size = new Size(190, 17),
            Font = new Font("Segoe UI", 7, FontStyle.Bold),
            ForeColor = gameExe.Length > 0 ? Color.FromArgb(116, 220, 160) : secondary
        };
        tile.Controls.Add(status);
        tile.Click += (_, _) => LaunchFortnite();
        tileImage.Click += (_, _) => LaunchFortnite();

        var footer = new Label
        {
            Text = "FORTNITE BOOSTER PRO     •     WINDOWS 11",
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
            Location = new Point(4, 830),
            Size = new Size(450, 20),
            ForeColor = Color.FromArgb(105, 110, 122),
            Font = new Font("Segoe UI", 8, FontStyle.Bold)
        };
        body.Controls.Add(footer);
    }

    private Button MakeButton(string caption, int x, int y, int width, int height, Color back, Color fore)
    {
        var button = new Button
        {
            Text = caption,
            Location = new Point(x, y),
            Size = new Size(width, height),
            BackColor = back,
            ForeColor = fore,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(back, .08f);
        return button;
    }

    private async Task LoadArtworkAsync()
    {
        string cache = Path.Combine(appFolder, "fortnite-cover.jpg");
        try
        {
            if (!File.Exists(cache) || new FileInfo(cache).Length < 20_000)
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(20);
                http.DefaultRequestHeaders.UserAgent.ParseAdd("FortniteBoosterPRO/1.2");
                byte[] bytes = await http.GetByteArrayAsync(imageUrl);
                await File.WriteAllBytesAsync(cache, bytes);
            }
            using var stream = new MemoryStream(await File.ReadAllBytesAsync(cache));
            using var loaded = Image.FromStream(stream);
            coverImage = new Bitmap(loaded);
            hero.Invalidate();
            tileImage.Image = new Bitmap(coverImage);
        }
        catch
        {
            // The UI remains usable offline; the cover falls back to the built-in gradient.
        }
    }

    private void PaintHero(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.HighQuality;
        var bounds = hero.ClientRectangle;
        if (coverImage != null)
        {
            float scale = Math.Max((float)bounds.Width / coverImage.Width, (float)bounds.Height / coverImage.Height);
            int w = (int)(coverImage.Width * scale), h = (int)(coverImage.Height * scale);
            g.DrawImage(coverImage, new Rectangle((bounds.Width - w) / 2, (bounds.Height - h) / 2, w, h));
        }
        else
        {
            using var bgBrush = new LinearGradientBrush(bounds, Color.FromArgb(39, 91, 135), Color.FromArgb(19, 24, 39), 20f);
            g.FillRectangle(bgBrush, bounds);
        }
        using var overlay = new LinearGradientBrush(bounds,
            Color.FromArgb(235, 8, 13, 23), Color.FromArgb(30, 8, 13, 23), LinearGradientMode.Horizontal);
        g.FillRectangle(overlay, bounds);
        using var bottom = new LinearGradientBrush(bounds,
            Color.FromArgb(0, 8, 13, 23), Color.FromArgb(115, 8, 13, 23), LinearGradientMode.Vertical);
        g.FillRectangle(bottom, bounds);
    }

    private async void ChooseGameExe()
    {
        using var dialog = new OpenFileDialog { Title = "Выбери FortniteClient-Win64-Shipping.exe", Filter = "Fortnite executable (*.exe)|*.exe" };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        gameExe = dialog.FileName;
        status.Text = "УСТАНОВЛЕНА";
        status.ForeColor = Color.FromArgb(116, 220, 160);
        MessageBox.Show("Путь к Fortnite сохранён.", "Fortnite Booster PRO", MessageBoxButtons.OK, MessageBoxIcon.Information);
        await Task.CompletedTask;
    }

    private string FindFortniteExe()
    {
        string[] candidates =
        {
            @"C:\Program Files\Epic Games\Fortnite\FortniteGame\Binaries\Win64\FortniteClient-Win64-Shipping.exe",
            @"D:\Epic Games\Fortnite\FortniteGame\Binaries\Win64\FortniteClient-Win64-Shipping.exe",
            @"C:\Program Files\Epic Games\Fortnite\FortniteGame\Binaries\Win64\FortniteLauncher.exe"
        };
        return candidates.FirstOrDefault(File.Exists) ?? "";
    }

    private void LaunchFortnite()
    {
        try
        {
            if (File.Exists(gameExe))
            {
                Process.Start(new ProcessStartInfo(gameExe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(gameExe)! });
                return;
            }
            var result = MessageBox.Show(
                "Путь к Fortnite пока не указан. Открыть игру через Epic Games Launcher?\\nЕсли игра не установлена, откроется её страница.",
                "Запуск Fortnite", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
                Process.Start(new ProcessStartInfo("com.epicgames.launcher://apps/Fortnite?action=launch&silent=true") { UseShellExecute = true });
            else if (result == DialogResult.No)
                OpenUrl(gamePage);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Не удалось запустить Fortnite: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }

    private void ShowBoost()
    {
        using var form = new Form
        {
            Text = "Fortnite Booster PRO — BOOST",
            Size = new Size(560, 390),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = background,
            ForeColor = foreground,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false
        };
        form.Controls.Add(new Label { Text = "ПРОФИЛЬ СТАБИЛЬНОСТИ FPS", Location = new Point(24, 22), Size = new Size(480, 32), Font = new Font("Segoe UI", 15, FontStyle.Bold) });
        form.Controls.Add(new Label { Text = "Ryzen 7 5700X  •  RTX 4060  •  16 GB RAM  •  Windows 11", Location = new Point(25, 63), Size = new Size(490, 24), ForeColor = secondary });
        form.Controls.Add(new Label { Text = "Включает Windows Game Mode и отключает фоновую запись Game DVR. Перед изменениями создаётся резервная копия параметров текущего пользователя.", Location = new Point(25, 105), Size = new Size(490, 70), ForeColor = foreground });
        var apply = MakeButton("ПРИМЕНИТЬ BOOST", 25, 205, 485, 48, cyan, Color.FromArgb(8, 19, 24));
        apply.Click += (_, _) =>
        {
            try
            {
                SetUserDword(@"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1);
                SetUserDword(@"Software\Microsoft\GameBar", "AllowAutoGameMode", 1);
                SetUserDword(@"System\GameConfigStore", "GameDVR_Enabled", 0);
                SetUserDword(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0);
                MessageBox.Show("Профиль применён. Перезагрузи ПК перед проверкой Fortnite.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        };
        form.Controls.Add(apply);
        form.Controls.Add(new Label { Text = "Не отключает службы Windows и не разгоняет комплектующие. Прирост FPS не гарантируется.", Location = new Point(25, 270), Size = new Size(490, 42), ForeColor = secondary });
        form.ShowDialog(this);
    }

    private void SetUserDword(string path, string name, int value)
    {
        string backupPath = Path.Combine(appFolder, "registry-backup.json");
        var backup = File.Exists(backupPath) ? File.ReadAllText(backupPath) : "{}";
        using var document = System.Text.Json.JsonDocument.Parse(backup);
        var values = new Dictionary<string, object?>();
        foreach (var property in document.RootElement.EnumerateObject())
            values[property.Name] = property.Value.Clone();
        string id = path + "|" + name;
        if (!values.ContainsKey(id))
        {
            using var key = Registry.CurrentUser.OpenSubKey(path);
            bool existed = key?.GetValueNames().Contains(name) == true;
            object? previous = existed ? key!.GetValue(name) : null;
            values[id] = new { existed, previous };
            File.WriteAllText(backupPath, System.Text.Json.JsonSerializer.Serialize(values));
        }
        using var writable = Registry.CurrentUser.CreateSubKey(path);
        writable.SetValue(name, value, RegistryValueKind.DWord);
    }
}