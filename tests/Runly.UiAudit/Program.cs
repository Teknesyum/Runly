using System.Drawing.Imaging;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Runly.Core.Models;
using Runly.Core.Services;
using Runly.Settings;

namespace Runly.UiAudit;

internal sealed record Finding(string Screen, string Element, string State, string Fg, string Bg, double Ratio, double Threshold, string Kind, string Where);

internal static class Program
{
    private const double TextThreshold = 7.0;
    private const double IconThreshold = 3.0;
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly List<Finding> s_findings = new();
    private static readonly Dictionary<int, string> s_tokens = new();
    private static bool s_suppressGrid;
    private static string s_outDir = ".";
    private static string s_tag = "olcum";

    [STAThread]
    private static int Main(string[] args)
    {
        s_outDir = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
        s_tag = args.Length > 1 ? args[1] : "olcum";
        var only = args.Length > 2 ? args[2] : null;
        Directory.CreateDirectory(s_outDir);

        NeonTheme.EnableDarkMode();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        LoadTokens();

        var screens = new List<(string Name, Func<Form?> Build)>
        {
            ("ana", BuildMainForm),
            ("uzanti-ekle", () => Create("Runly.Settings.Dialogs.AddExtensionDialog")),
            ("kaldir-onay", () => Create("Runly.Settings.Dialogs.UninstallConfirmDialog")),
            ("yedek-geri", () => Create("Runly.Settings.Dialogs.RestoreBackupDialog", SampleBackups())),
            ("sonuc-basari", () => Create("Runly.Settings.Dialogs.ResultDialog", "Kurulum", true, new List<string> { ".py bağlandı", ".ps1 bağlandı" }, null, null)),
            ("sonuc-uyari", () => Create("Runly.Settings.Dialogs.ResultDialog", "Kurulum", true, new List<string> { ".py bağlandı" }, null, "Gezgin yeniden başlatılmalı.")),
            ("sonuc-hata", () => Create("Runly.Settings.Dialogs.ResultDialog", "Kurulum", false, new List<string>(), "Kayıt defterine yazılamadı.", null)),
            ("mesaj", () => Create("Runly.Settings.NeonMessageDialog", "Seçili uzantı silinsin mi?", "Runly", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1)),
            ("uygulama-sec", BuildChooseApplication),
        };

        Form? current = null;
        using var guard = new System.Windows.Forms.Timer { Interval = 500 };
        guard.Tick += (_, _) =>
        {
            foreach (var open in Application.OpenForms.Cast<Form>().ToList())
            {
                if (!ReferenceEquals(open, current) && open.Modal)
                {
                    open.DialogResult = DialogResult.Cancel;
                    open.Close();
                }
            }
        };
        guard.Start();
        var watchdog = new Thread(() => { Thread.Sleep(TimeSpan.FromMinutes(15)); Environment.Exit(3); }) { IsBackground = true };
        watchdog.Start();

        using var log = new StreamWriter(Path.Combine(s_outDir, $"olcum-{s_tag}.log"), false, new UTF8Encoding(false)) { AutoFlush = true };
        foreach (var (name, build) in screens)
        {
            if (only is not null && only != name)
            {
                continue;
            }

            try
            {
                var form = build();
                if (form is null)
                {
                    log.WriteLine($"{name}: kurulamadı (tür ya da kurucu yok)");
                    continue;
                }

                current = form;
                AuditScreen(name, form, log);
                form.Close();
                form.Dispose();
            }
            catch (Exception ex)
            {
                log.WriteLine($"{name}: HATA {ex.GetType().Name}: {ex.Message}");
                log.WriteLine(ex.StackTrace);
            }
        }

        WriteReport();
        var failures = s_findings.Count(f => f.Ratio < f.Threshold && f.State != "edilgen");
        log.WriteLine($"ölçüm {s_findings.Count} · eşik altı {failures}");
        Console.WriteLine($"ölçüm {s_findings.Count} · eşik altı {failures} · {s_outDir}");
        return failures == 0 ? 0 : 1;
    }

    private static void AuditScreen(string name, Form form, StreamWriter log)
    {
        form.StartPosition = FormStartPosition.Manual;
        form.ShowInTaskbar = false;
        var virtualScreen = SystemInformation.VirtualScreen;
        form.Location = new Point(virtualScreen.Right + 400, virtualScreen.Top + 50);
        var handle = form.Handle;
        SetWindowLong(handle, GwlExStyle, GetWindowLong(handle, GwlExStyle) | WsExNoActivate);
        form.Show();
        for (var i = 0; i < 20; i++)
        {
            Pump();
        }

        form.PerformLayout();
        form.Refresh();
        Pump();
        SendMessage(handle, WmUpdateUiState, (nint)(UisClear | ((UisfHideFocus | UisfHideAccel) << 16)), 0);
        Pump();
        log.WriteLine($"{name}: {form.Width}x{form.Height} dpi={form.DeviceDpi} metrics={Metrics.Dpi}");

        MeasureCaption(name, form);
        foreach (var control in Walk(form))
        {
            if (!IsShown(control))
            {
                continue;
            }

            if (control is DataGridView grid)
            {
                log.WriteLine($"  {DateTime.Now:HH:mm:ss.fff} tablo {grid.Name} {grid.RowCount}");
                MeasureGrid(name, form, grid);
            }
            else if (TextOf(control) is { Length: > 0 })
            {
                log.WriteLine($"  {DateTime.Now:HH:mm:ss.fff} {control.GetType().Name} {Clip(TextOf(control)!)}");
                MeasureTextControl(name, form, control);
            }
        }

        form.ActiveControl = null;
        form.Refresh();
        Pump();
        using (var shot = Capture(form))
        {
            shot.Save(Path.Combine(s_outDir, $"{name}-100-{s_tag}.png"), ImageFormat.Png);
        }
    }

    private static void MeasureCaption(string screen, Form form)
    {
        var original = form.Text;
        if (!string.IsNullOrEmpty(original))
        {
            var captionHeight = Metrics.CaptionHeight;
            var region = new Rectangle(0, 0, form.ClientSize.Width - (Metrics.CaptionButtonWidth * 3), captionHeight);
            MeasureByDiff(screen, form, "başlık: " + Clip(original), "dinlenik", region,
                () => { form.Text = string.Empty; form.Invalidate(); },
                () => { form.Text = original; form.Invalidate(); });
        }

        foreach (var (field, bounds, label) in new[]
                 {
                     ("_closeHover", "CloseBounds", "başlık düğmesi ×"),
                     ("_maximizeHover", "MaximizeBounds", "başlık düğmesi □"),
                     ("_minimizeHover", "MinimizeBounds", "başlık düğmesi ─"),
                 })
        {
            var flag = FindField(form.GetType(), field);
            var rectProp = FindProperty(form.GetType(), bounds);
            if (flag is null || rectProp is null)
            {
                continue;
            }

            if ((field == "_maximizeHover" && !form.MaximizeBox) || (field == "_minimizeHover" && !form.MinimizeBox))
            {
                continue;
            }

            var rect = (Rectangle)rectProp.GetValue(form)!;
            foreach (var hover in new[] { false, true })
            {
                flag.SetValue(form, hover);
                form.Invalidate();
                Pump();
                using var shot = Capture(form);
                MeasureByMode(screen, label, hover ? "hover" : "dinlenik", ClientToWindow(form, rect), shot, IconThreshold, "simge");
            }

            flag.SetValue(form, false);
            form.Invalidate();
            Pump();
        }
    }

    private static void MeasureTextControl(string screen, Form form, Control control)
    {
        var element = $"{control.GetType().Name}: {Clip(TextOf(control)!)}";
        var states = new List<(string State, Action Enter, Action Leave)> { ("dinlenik", () => { }, () => { }) };

        if (control is NeonButton)
        {
            var hover = FindField(control.GetType(), "_hover");
            if (hover is not null)
            {
                states.Add(("hover", () => hover.SetValue(control, true), () => hover.SetValue(control, false)));
                states.Add(("basılı", () => hover.SetValue(control, true), () => hover.SetValue(control, false)));
            }

            states.Add(("odak", () => control.Focus(), () => form.ActiveControl = null));
        }

        if (control is CheckBox or RadioButton)
        {
            states[0] = ((control is CheckBox { Checked: true } or RadioButton { Checked: true }) ? "seçili" : "boş", states[0].Enter, states[0].Leave);
        }

        if (control.Enabled && control is ButtonBase or TextBox or ComboBox)
        {
            states.Add(("edilgen", () => control.Enabled = false, () => control.Enabled = true));
        }

        foreach (var (state, enter, leave) in states)
        {
            var scrolls = ScrollState(control);
            enter();
            control.Invalidate();
            Pump();
            var region = VisibleRegion(form, control);
            if (region.Width <= 0 || region.Height <= 0)
            {
                leave();
                RestoreScroll(scrolls);
                continue;
            }

            var text = TextOf(control)!;
            var autoSize = control.AutoSize;
            var size = control.Size;
            MeasureByDiff(screen, form, element, state, region,
                () => { control.AutoSize = false; control.Size = size; SetText(control, string.Empty); control.Invalidate(); },
                () => { SetText(control, text); control.AutoSize = autoSize; control.Invalidate(); });
            leave();
            RestoreScroll(scrolls);
            control.Invalidate();
            Pump();
        }
    }

    private static void MeasureByDiff(string screen, Form form, string element, string state, Rectangle region, Action hide, Action show)
    {
        using var withText = Capture(form);
        hide();
        Pump();
        using var without = Capture(form);
        show();
        Pump();

        var result = Analyse(withText, without, region, chip: false);
        if (result is null)
        {
            return;
        }

        var (fg, bg, ratio) = result.Value;
        var threshold = element.StartsWith("başlık düğmesi", StringComparison.Ordinal) ? IconThreshold : TextThreshold;
        Add(screen, element, state, fg, bg, ratio, threshold, "yazı", region);
    }

    private static void MeasureByMode(string screen, string element, string state, Rectangle region, Bitmap shot, double threshold, string kind)
    {
        var pixels = Pixels(shot, region);
        if (pixels.Count == 0)
        {
            return;
        }

        var bg = pixels.GroupBy(p => p).OrderByDescending(g => g.Count()).First().Key;
        var fg = pixels.OrderByDescending(p => Contrast(p, bg)).First();
        Add(screen, element, state, fg, bg, Contrast(fg, bg), threshold, kind, region);
    }

    private static void MeasureGrid(string screen, Form form, DataGridView grid)
    {
        grid.CellPainting += SuppressForeground;
        var toggleCells = new List<DataGridViewCell>();
        var rows = new List<int>();
        var first = Math.Max(0, grid.FirstDisplayedScrollingRowIndex);
        for (var r = first; r < grid.RowCount && rows.Count < 8; r++)
        {
            if (grid.Rows[r].Displayed)
            {
                rows.Add(r);
            }
        }

        foreach (var r in rows)
        {
            foreach (DataGridViewCell cell in grid.Rows[r].Cells)
            {
                if (FindField(cell.GetType(), "_hover") is not null)
                {
                    toggleCells.Add(cell);
                }
            }
        }

        var hoverField = toggleCells.Count > 0 ? FindField(toggleCells[0].GetType(), "_hover") : null;
        var pressedField = toggleCells.Count > 0 ? FindField(toggleCells[0].GetType(), "_pressed") : null;

        void SetToggle(FieldInfo? field, bool value)
        {
            if (field is null) return;
            foreach (var cell in toggleCells) field.SetValue(cell, value);
        }

        var selectedRow = rows.Count > 0 ? rows[0] : -1;
        var states = new List<(string State, Action Enter, Action Leave)>
        {
            ("dinlenik", () => { grid.ClearSelection(); }, () => { }),
            ("hover", () => { grid.ClearSelection(); SetToggle(hoverField, true); }, () => SetToggle(hoverField, false)),
            ("basılı", () => { grid.ClearSelection(); SetToggle(pressedField, true); }, () => SetToggle(pressedField, false)),
        };

        if (selectedRow >= 0)
        {
            states.Add(("seçili", () => { grid.ClearSelection(); grid.Rows[selectedRow].Selected = true; }, () => grid.ClearSelection()));
            states.Add(("seçili+hover", () => { grid.ClearSelection(); grid.Rows[selectedRow].Selected = true; SetToggle(hoverField, true); }, () => { SetToggle(hoverField, false); grid.ClearSelection(); }));
            states.Add(("seçili+basılı", () => { grid.ClearSelection(); grid.Rows[selectedRow].Selected = true; SetToggle(pressedField, true); }, () => { SetToggle(pressedField, false); grid.ClearSelection(); }));
        }

        var gridName = string.IsNullOrEmpty(grid.Name) ? "tablo" : grid.Name;
        foreach (var (state, enter, leave) in states)
        {
            enter();
            grid.Invalidate();
            Pump();
            using var withText = Capture(form);
            s_suppressGrid = true;
            grid.Invalidate();
            Pump();
            using var without = Capture(form);
            s_suppressGrid = false;
            grid.Invalidate();
            Pump();

            var worst = new Dictionary<string, (Color Fg, Color Bg, double Ratio, Rectangle Region, double Threshold, string Kind)>();
            void Consider(string key, Rectangle cellRect, bool chip, double threshold, string kind)
            {
                var region = ClientToWindow(form, form.RectangleToClient(grid.RectangleToScreen(cellRect)));
                var result = Analyse(withText, without, region, chip);
                if (result is null) return;
                var (fg, bg, ratio) = result.Value;
                if (!worst.TryGetValue(key, out var current) || ratio < current.Ratio)
                {
                    worst[key] = (fg, bg, ratio, region, threshold, kind);
                }
            }

            if (state == "dinlenik" && grid.ColumnHeadersVisible)
            {
                foreach (DataGridViewColumn column in grid.Columns)
                {
                    if (column.Displayed)
                    {
                        Consider($"{gridName} başlık: {Clip(column.HeaderText)}", grid.GetCellDisplayRectangle(column.Index, -1, false), false, TextThreshold, "yazı");
                    }
                }
            }

            foreach (var r in rows)
            {
                var isSelectedRow = r == selectedRow && state.StartsWith("seçili", StringComparison.Ordinal);
                if (state.StartsWith("seçili", StringComparison.Ordinal) && !isSelectedRow)
                {
                    continue;
                }

                foreach (DataGridViewColumn column in grid.Columns)
                {
                    if (!column.Displayed)
                    {
                        continue;
                    }

                    var cell = grid.Rows[r].Cells[column.Index];
                    var typeName = cell.GetType().Name;
                    var isToggle = FindField(cell.GetType(), "_hover") is not null;
                    if ((state.EndsWith("hover", StringComparison.Ordinal) || state.EndsWith("basılı", StringComparison.Ordinal)) && !isToggle)
                    {
                        continue;
                    }

                    var chip = typeName is "NeonChipCell" or "NeonActionCell";
                    var icon = typeName == "NeonCheckCell";
                    var key = $"{gridName} · {Clip(column.HeaderText)} ({typeName}{(cell.ReadOnly ? ", salt okunur" : string.Empty)}{(cell.Value is true ? ", açık" : cell.Value is false ? ", kapalı" : string.Empty)})";
                    Consider(key, grid.GetCellDisplayRectangle(column.Index, r, false), chip, icon ? IconThreshold : TextThreshold, icon ? "simge" : chip ? "çip yazısı" : "yazı");
                }
            }

            foreach (var (key, value) in worst)
            {
                Add(screen, key, state, value.Fg, value.Bg, value.Ratio, value.Threshold, value.Kind, value.Region);
            }

            leave();
            grid.Invalidate();
            Pump();
        }

        grid.CellPainting -= SuppressForeground;
    }

    private static void SuppressForeground(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (!s_suppressGrid)
        {
            return;
        }

        e.Paint(e.ClipBounds, e.PaintParts & ~DataGridViewPaintParts.ContentForeground);
        e.Handled = true;
    }

    private static (Color Fg, Color Bg, double Ratio)? Analyse(Bitmap withText, Bitmap without, Rectangle region, bool chip)
    {
        region.Intersect(new Rectangle(0, 0, withText.Width, withText.Height));
        if (region.Width <= 0 || region.Height <= 0)
        {
            return null;
        }

        var a = Pixels(withText, region);
        var b = Pixels(without, region);
        var mask = new List<int>();
        for (var i = 0; i < a.Count; i++)
        {
            var d = Math.Abs(a[i].R - b[i].R) + Math.Abs(a[i].G - b[i].G) + Math.Abs(a[i].B - b[i].B);
            if (d > 24)
            {
                mask.Add(i);
            }
        }

        if (mask.Count < 6)
        {
            return null;
        }

        if (chip)
        {
            var width = region.Width;
            var minX = mask.Min(i => i % width) + 3;
            var maxX = mask.Max(i => i % width) - 3;
            var minY = mask.Min(i => i / width) + 3;
            var maxY = mask.Max(i => i / width) - 3;
            var inner = new List<Color>();
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    inner.Add(a[(y * width) + x]);
                }
            }

            if (inner.Count == 0)
            {
                inner = mask.Select(i => a[i]).ToList();
            }

            var fill = inner.GroupBy(p => p).OrderByDescending(g => g.Count()).First().Key;
            var ink = inner.OrderByDescending(p => Contrast(p, fill)).First();
            return (Snap(ink), fill, Contrast(Snap(ink), fill));
        }

        var fgIndex = mask.OrderByDescending(i => Contrast(a[i], b[i])).First();
        var fg = Snap(a[fgIndex]);
        var bgWorst = mask.Select(i => b[i]).OrderBy(p => Contrast(fg, p)).First();
        return (fg, bgWorst, Contrast(fg, bgWorst));
    }

    private static Color Snap(Color c)
    {
        Color best = c;
        var bestDistance = int.MaxValue;
        foreach (var (argb, _) in s_tokens)
        {
            var t = Color.FromArgb(argb);
            var d = Math.Abs(t.R - c.R) + Math.Abs(t.G - c.G) + Math.Abs(t.B - c.B);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = t;
            }
        }

        return bestDistance <= 40 ? best : c;
    }

    private static void Add(string screen, string element, string state, Color fg, Color bg, double ratio, double threshold, string kind, Rectangle region)
    {
        s_findings.Add(new Finding(screen, element, state, Hex(fg), Hex(bg), Math.Round(ratio, 2), threshold, kind,
            $"{region.X},{region.Y} {region.Width}x{region.Height}"));
    }

    private static void WriteReport()
    {
        var md = new StringBuilder();
        md.AppendLine($"# Canlı Kontrast Ölçümü — {s_tag}");
        md.AppendLine();
        md.AppendLine("Kaynak: `tests/Runly.UiAudit` (PrintWindow ile kendi penceresinden piksel örnekleme; yazılı ve yazısız iki kare farkı).");
        md.AppendLine();
        md.AppendLine("| Ekran | Öğe | Durum | Tür | Ön | Zemin | Oran | Eşik | Sonuç |");
        md.AppendLine("|---|---|---|---|---|---|---|---|---|");
        foreach (var f in s_findings.OrderBy(f => f.Ratio >= f.Threshold).ThenBy(f => f.Screen, StringComparer.Ordinal).ThenBy(f => f.Ratio))
        {
            md.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"| {f.Screen} | {f.Element.Replace("|", "/", StringComparison.Ordinal)} | {f.State} | {f.Kind} | {f.Fg}{TokenName(f.Fg)} | {f.Bg}{TokenName(f.Bg)} | {f.Ratio:0.00}:1 | {f.Threshold:0}:1 | {(f.Ratio >= f.Threshold ? "geçti" : f.State == "edilgen" ? "muaf" : "**KALDI**")} |"));
        }

        File.WriteAllText(Path.Combine(s_outDir, $"olcum-{s_tag}.md"), md.ToString(), new UTF8Encoding(false));
        var csv = new StringBuilder("ekran;oge;durum;tur;on;zemin;oran;esik\n");
        foreach (var f in s_findings)
        {
            csv.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{f.Screen};{f.Element.Replace(";", ",", StringComparison.Ordinal)};{f.State};{f.Kind};{f.Fg};{f.Bg};{f.Ratio:0.00};{f.Threshold:0}"));
        }

        File.WriteAllText(Path.Combine(s_outDir, $"olcum-{s_tag}.csv"), csv.ToString(), new UTF8Encoding(false));
    }

    private static string TokenName(string hex)
    {
        var c = ColorTranslator.FromHtml(hex);
        return s_tokens.TryGetValue(c.ToArgb(), out var name) ? $" ({name})" : string.Empty;
    }

    private static void LoadTokens()
    {
        foreach (var field in typeof(Palette).GetFields(BindingFlags.Static | BindingFlags.Public))
        {
            if (field.GetValue(null) is Color c && c.A == 255 && !s_tokens.ContainsKey(c.ToArgb()))
            {
                s_tokens[c.ToArgb()] = field.Name;
            }
        }
    }

    private static Form? BuildMainForm()
    {
        var type = typeof(Palette).Assembly.GetType("Runly.Settings.MainForm");
        if (type is null)
        {
            return null;
        }

        var configPath = Path.Combine(Path.GetTempPath(), "runly-uiaudit", "config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        var known = new Dictionary<Type, object>();
        var store = new ConfigStore(configPath);
        known[typeof(ConfigStore)] = store;
        known[typeof(Runly.Core.Abstractions.IConfigStore)] = store;
        known[typeof(RunlyConfig)] = store.Load();
        var logger = new FileLogger(false);
        known[typeof(Runly.Core.Abstractions.ILogger)] = logger;
        return (Form)Resolve(type, known)!;
    }

    private static Form? BuildChooseApplication()
    {
        var type = typeof(Palette).Assembly.GetType("Runly.Settings.Dialogs.ChooseApplicationDialog");
        var appType = typeof(Palette).Assembly.GetType("Runly.Settings.Discovery.InstalledApplication");
        if (type is null || appType is null)
        {
            return null;
        }

        var listType = typeof(List<>).MakeGenericType(appType);
        var apps = (System.Collections.IList)Activator.CreateInstance(listType)!;
        apps.Add(Activator.CreateInstance(appType, "code.exe", "Visual Studio Code", @"C:\Program Files\Microsoft VS Code\Code.exe", "uiaudit"));
        apps.Add(Activator.CreateInstance(appType, "notepad.exe", "Not Defteri", @"C:\Windows\System32\notepad.exe", "uiaudit"));
        var ctor = type.GetConstructors(Any).OrderByDescending(c => c.GetParameters().Length).First();
        var values = ctor.GetParameters().Select(p => p.Name switch
        {
            "extension" => ".py",
            "applications" => apps,
            "suggestedExecutables" => new List<string> { "code.exe" },
            "currentPath" => null,
            "usageHistory" => new List<string>(),
            _ when p.ParameterType.IsEnum => Enum.GetValues(p.ParameterType).GetValue(0),
            _ when p.HasDefaultValue => p.DefaultValue,
            _ => null,
        }).ToArray();
        return (Form)ctor.Invoke(values);
    }

    private static object SampleBackups()
    {
        var info = typeof(Runly.Core.Shell.BackupInfo);
        var list = new List<Runly.Core.Shell.BackupInfo>
        {
            new() { Path = @"C:\yedek\assoc-20260924-101500.reg", FileName = "assoc-20260924-101500.reg", CreatedUtc = new DateTime(2026, 9, 24, 10, 15, 0, DateTimeKind.Utc), SizeBytes = 4096 },
            new() { Path = @"C:\yedek\assoc-20260920-083000.reg", FileName = "assoc-20260920-083000.reg", CreatedUtc = new DateTime(2026, 9, 20, 8, 30, 0, DateTimeKind.Utc), SizeBytes = 3500 },
        };
        _ = info;
        return list;
    }

    private static Form? Create(string typeName, params object?[] args)
    {
        var type = typeof(Palette).Assembly.GetType(typeName);
        if (type is null)
        {
            return null;
        }

        var ctor = type.GetConstructors(Any).FirstOrDefault(c => c.GetParameters().Length == args.Length);
        return ctor is null ? null : (Form)ctor.Invoke(args);
    }

    private static object? Resolve(Type type, Dictionary<Type, object> known, int depth = 0)
    {
        if (known.TryGetValue(type, out var hit))
        {
            return hit;
        }

        if (depth > 6)
        {
            return null;
        }

        var concrete = type;
        if (type.IsInterface || type.IsAbstract)
        {
            concrete = new[] { typeof(ConfigStore).Assembly, typeof(Palette).Assembly }
                .SelectMany(a => a.GetTypes())
                .Where(t => !t.IsAbstract && !t.IsInterface && type.IsAssignableFrom(t))
                .OrderBy(t => t.Name.StartsWith("Fake", StringComparison.Ordinal) ? 1 : 0)
                .FirstOrDefault();
            if (concrete is null)
            {
                return null;
            }
        }

        var ctor = concrete.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .OrderBy(c => c.GetParameters().Count(p => !p.HasDefaultValue))
            .ThenByDescending(c => c.IsPublic)
            .FirstOrDefault();
        if (ctor is null)
        {
            return null;
        }

        var values = ctor.GetParameters().Select(p =>
            known.TryGetValue(p.ParameterType, out var v) ? v
            : p.HasDefaultValue ? p.DefaultValue
            : p.ParameterType == typeof(string) ? null
            : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType)
            : Resolve(p.ParameterType, known, depth + 1)).ToArray();
        var instance = ctor.Invoke(values);
        known[type] = instance;
        return instance;
    }

    private static IEnumerable<Control> Walk(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Walk(child))
            {
                yield return nested;
            }
        }
    }

    private static bool IsShown(Control control)
    {
        for (var c = control; c is not null && c is not Form; c = c.Parent)
        {
            if (!c.Visible)
            {
                return false;
            }
        }

        return control.Width > 0 && control.Height > 0;
    }

    private static string? TextOf(Control control) => control switch
    {
        NeonGroupPanel panel => panel.Title,
        ComboBox combo => combo.SelectedIndex >= 0 ? combo.GetItemText(combo.SelectedItem) : null,
        Label or ButtonBase or TextBox => control.Text,
        _ => null,
    };

    private static void SetText(Control control, string text)
    {
        switch (control)
        {
            case NeonGroupPanel panel:
                panel.Title = text;
                break;
            case ComboBox combo:
                if (text.Length == 0)
                {
                    combo.Tag ??= combo.SelectedIndex;
                    combo.SelectedIndex = -1;
                }
                else if (combo.Tag is int index)
                {
                    combo.SelectedIndex = index;
                    combo.Tag = null;
                }

                break;
            default:
                control.Text = text;
                break;
        }
    }

    private static Rectangle VisibleRegion(Form form, Control control)
    {
        var screen = control.RectangleToScreen(control.ClientRectangle);
        for (var c = control.Parent; c is not null; c = c.Parent)
        {
            screen.Intersect(c.RectangleToScreen(c.ClientRectangle));
        }

        return ClientToWindow(form, form.RectangleToClient(screen));
    }

    private static List<(ScrollableControl Owner, Point Position)> ScrollState(Control control)
    {
        var list = new List<(ScrollableControl, Point)>();
        for (var c = control.Parent; c is not null; c = c.Parent)
        {
            if (c is ScrollableControl { AutoScroll: true } scrollable)
            {
                list.Add((scrollable, new Point(-scrollable.AutoScrollPosition.X, -scrollable.AutoScrollPosition.Y)));
            }
        }

        return list;
    }

    private static void RestoreScroll(List<(ScrollableControl Owner, Point Position)> state)
    {
        foreach (var (owner, position) in state)
        {
            owner.AutoScrollPosition = position;
        }

        Pump();
    }

    private static Rectangle ClientToWindow(Form form, Rectangle client)
    {
        var origin = form.PointToScreen(Point.Empty);
        client.Offset(origin.X - form.Left, origin.Y - form.Top);
        return client;
    }

    private static Bitmap Capture(Form form)
    {
        var bitmap = new Bitmap(form.Width, form.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            var hdc = g.GetHdc();
            var ok = PrintWindow(form.Handle, hdc, PwRenderFullContent);
            g.ReleaseHdc(hdc);
            if (!ok)
            {
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            }
        }

        return bitmap;
    }

    private static List<Color> Pixels(Bitmap bitmap, Rectangle region)
    {
        region.Intersect(new Rectangle(0, 0, bitmap.Width, bitmap.Height));
        var list = new List<Color>(Math.Max(0, region.Width * region.Height));
        if (region.Width <= 0 || region.Height <= 0)
        {
            return list;
        }

        var data = bitmap.LockBits(region, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var row = new int[region.Width];
            for (var y = 0; y < region.Height; y++)
            {
                Marshal.Copy(data.Scan0 + (y * data.Stride), row, 0, region.Width);
                foreach (var argb in row)
                {
                    list.Add(Color.FromArgb(255, Color.FromArgb(argb)));
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return list;
    }

    private static double Luminance(Color c)
    {
        static double Channel(int v)
        {
            var s = v / 255.0;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(c.R)) + (0.7152 * Channel(c.G)) + (0.0722 * Channel(c.B));
    }

    private static double Contrast(Color a, Color b)
    {
        var la = Luminance(a);
        var lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private static string Clip(string text)
    {
        var line = text.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
        return line.Length <= 40 ? line : line[..40] + "…";
    }

    private static FieldInfo? FindField(Type? type, string name)
    {
        for (; type is not null; type = type.BaseType)
        {
            var field = type.GetField(name, Any);
            if (field is not null)
            {
                return field;
            }
        }

        return null;
    }

    private static PropertyInfo? FindProperty(Type? type, string name)
    {
        for (; type is not null; type = type.BaseType)
        {
            var property = type.GetProperty(name, Any);
            if (property is not null)
            {
                return property;
            }
        }

        return null;
    }

    private static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Application.DoEvents();
            Thread.Sleep(15);
        }
    }

    private const int GwlExStyle = -20;
    private const int WsExNoActivate = 0x08000000;
    private const uint PwRenderFullContent = 2;
    private const int WmUpdateUiState = 0x0128;
    private const int UisClear = 2;
    private const int UisfHideFocus = 1;
    private const int UisfHideAccel = 2;

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(nint hwnd, nint hdc, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(nint hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(nint hwnd, int index, int value);

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SendMessage(nint hwnd, int msg, nint wParam, nint lParam);
}
