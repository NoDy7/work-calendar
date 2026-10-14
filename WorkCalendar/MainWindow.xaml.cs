using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace WorkCalendar;

public partial class MainWindow : Window
{
    private static readonly Brush BrBg = (Brush)Application.Current.Resources["Bg"];
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");
    private static readonly string[] Months =
    {
        "Январь", "Февраль", "Март", "Апрель", "Май", "Июнь",
        "Июль", "Август", "Сентябрь", "Октябрь", "Ноябрь", "Декабрь"
    };
    private static readonly string[] Weekdays = { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" };

    private AppState state = Store.Load();
    private int _year;
    private int _month;

    public MainWindow()
    {
        InitializeComponent();
        var today = DateTime.Today;
        _year = today.Year;
        _month = today.Month;

        PrevBtn.Click += (_, _) => { _month--; if (_month < 1) { _month = 12; _year--; } Render(); };
        NextBtn.Click += (_, _) => { _month++; if (_month > 12) { _month = 1; _year++; } Render(); };
        TodayBtn.Click += (_, _) => { var t = DateTime.Today; _year = t.Year; _month = t.Month; Render(); };
        SettingsBtn.Click += (_, _) => OpenSettings();
        ExportBtn.Click += (_, _) => DoExport();
        ImportBtn.Click += (_, _) => DoImport();

        BuildWeekHeader();
        Render();
    }

    private static Brush Res(string key) => (Brush)Application.Current.Resources[key];

    private string Money(double v) => v.ToString("N0", Ru) + " " + state.Settings.Currency;

    private void BuildWeekHeader()
    {
        WeekHeader.Children.Clear();
        foreach (var w in Weekdays)
        {
            WeekHeader.Children.Add(new TextBlock
            {
                Text = w,
                Foreground = Res("Muted"),
                FontSize = 12,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 4, 0, 4)
            });
        }
    }

    private void Render()
    {
        MonthLabel.Text = Months[_month - 1] + " " + _year;
        BuildCalendar();
        RenderSummary();
    }

    private void BuildCalendar()
    {
        DayGrid.Children.Clear();
        var first = new DateTime(_year, _month, 1);
        int startIdx = ((int)first.DayOfWeek + 6) % 7; // Пн = 0
        var start = first.AddDays(-startIdx);
        var today = DateTime.Today;

        for (int i = 0; i < 42; i++)
        {
            var dt = start.AddDays(i);
            var key = dt.ToString("yyyy-MM-dd");
            bool outMonth = dt.Month != _month;
            state.Days.TryGetValue(key, out var entry);
            DayGrid.Children.Add(BuildDayCell(dt, key, entry, outMonth, dt.Date == today));
        }
    }

    private Border BuildDayCell(DateTime dt, string key, DayEntry? e, bool outMonth, bool isToday)
    {
        bool worked = e != null && e.Worked;
        var border = new Border
        {
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6),
            Margin = new Thickness(3),
            Background = Res(worked ? "Worked" : "Panel2"),
            BorderBrush = isToday ? Res("Accent") : Res("Line"),
        };
        if (outMonth) border.Opacity = 0.35;

        var sp = new StackPanel();

        var head = new DockPanel();
        var dayNum = new TextBlock
        {
            Text = dt.Day.ToString(),
            FontSize = 13,
            Foreground = worked ? Res("Text") : Res("Muted"),
        };
        if (worked) dayNum.FontWeight = FontWeights.SemiBold;
        var check = new TextBlock { Text = "✔", Foreground = Res("Green"), FontSize = 12 };
        DockPanel.SetDock(check, Dock.Right);
        head.Children.Add(check);
        head.Children.Add(dayNum);
        sp.Children.Add(head);

        if (worked && e != null)
        {
            sp.Children.Add(new TextBlock
            {
                Text = "+ " + Money(e.Earned),
                Foreground = Res("Green"),
                FontSize = 12.5,
                Margin = new Thickness(0, 2, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            if (e.Spent > 0)
            {
                sp.Children.Add(new TextBlock
                {
                    Text = "− " + Money(e.Spent),
                    Foreground = Res("Red"),
                    FontSize = 12,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                });
            }
            double n = e.Net;
            sp.Children.Add(new TextBlock
            {
                Text = Money(n),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = n >= 0 ? Res("Green") : Res("Red"),
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 2, 0, 0),
            });
        }

        border.Child = sp;
        if (!outMonth)
        {
            border.Tag = key;
            border.Cursor = Cursors.Hand;
            border.MouseLeftButtonUp += DayCell_Click;
        }
        return border;
    }

    private void DayCell_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border b || b.Tag is not string key) return;
        state.Days.TryGetValue(key, out var entry);
        var dlg = new DayEditorWindow(key, entry, state.Settings) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            if (dlg.Deleted) state.Days.Remove(key);
            else if (dlg.Result != null) state.Days[key] = dlg.Result;
            Store.Save(state);
            Render();
        }
    }

    private void RenderSummary()
    {
        SummaryPanel.Children.Clear();
        double earn = 0, spend = 0, hours = 0;
        int worked = 0;
        int days = DateTime.DaysInMonth(_year, _month);
        for (int d = 1; d <= days; d++)
        {
            var key = new DateTime(_year, _month, d).ToString("yyyy-MM-dd");
            if (state.Days.TryGetValue(key, out var e) && e.Worked)
            {
                worked++;
                earn += e.Earned;
                spend += e.Spent;
                hours += e.Hours;
            }
        }
        double net = earn - spend;

        AddSummaryRow("Отработано дней", worked.ToString());
        AddSummaryRow("Всего часов", hours.ToString("#,##0.##", Ru));
        AddSummaryRow("Заработано", Money(earn), "Green");
        AddSummaryRow("Расходы", Money(spend), "Red");
        AddSummaryRow("Среднее в день", Money(worked > 0 ? net / worked : 0));

        SummaryPanel.Children.Add(new Border
        {
            Height = 1,
            Background = Res("Line"),
            Margin = new Thickness(0, 10, 0, 10)
        });

        var netRow = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
        netRow.Children.Add(new TextBlock
        {
            Text = "Чистый итог",
            Foreground = Res("Muted"),
            VerticalAlignment = VerticalAlignment.Center
        });
        var netVal = new TextBlock
        {
            Text = Money(net),
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Foreground = net >= 0 ? Res("Green") : Res("Red"),
        };
        DockPanel.SetDock(netVal, Dock.Right);
        netRow.Children.Add(netVal);
        SummaryPanel.Children.Add(netRow);

        SummaryPanel.Children.Add(new TextBlock
        {
            Text = net >= 0 ? "В профите 💪" : "В минус, блин 😬",
            Foreground = Res("Muted"),
            FontSize = 12,
            Margin = new Thickness(0, 8, 0, 0)
        });
    }

    private void AddSummaryRow(string label, string value, string? colorKey = null)
    {
        var row = new DockPanel { Margin = new Thickness(0, 6, 0, 6) };
        row.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Res("Muted"),
            VerticalAlignment = VerticalAlignment.Center
        });
        var val = new TextBlock
        {
            Text = value,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (colorKey != null) val.Foreground = Res(colorKey);
        DockPanel.SetDock(val, Dock.Right);
        row.Children.Add(val);
        SummaryPanel.Children.Add(row);
    }

    private void OpenSettings()
    {
        var w = new Window
        {
            Title = "Настройки",
            Width = 460,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            Background = BrBg,
        };
        var panel = new StackPanel { Margin = new Thickness(20) };

        var cur = AddField(panel, "Валюта", state.Settings.Currency);
        var hr = AddField(panel, "Ставка за час", state.Settings.HourlyRate.ToString("0.##", Ru));
        var sr = AddField(panel, "Ставка за смену", state.Settings.ShiftRate.ToString("0.##", Ru));
        var st = AddField(panel, "Смена — начало (ЧЧ:мм)", state.Settings.Start);
        var en = AddField(panel, "Смена — конец (ЧЧ:мм)", state.Settings.End);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        var cancel = new Button { Content = "Отмена", Padding = new Thickness(14, 7, 14, 7) };
        var save = new Button
        {
            Content = "Сохранить",
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(14, 7, 14, 7),
            Background = Res("Accent"),
            Foreground = Res("Dark"),
            FontWeight = FontWeights.SemiBold,
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        panel.Children.Add(buttons);
        w.Content = panel;

        cancel.Click += (_, _) => w.Close();
        save.Click += (_, _) =>
        {
            state.Settings.Currency = string.IsNullOrWhiteSpace(cur.Text) ? "₽" : cur.Text.Trim();
            state.Settings.HourlyRate = ParseNum(hr.Text);
            state.Settings.ShiftRate = ParseNum(sr.Text);
            state.Settings.Start = string.IsNullOrWhiteSpace(st.Text) ? "09:00" : st.Text.Trim();
            state.Settings.End = string.IsNullOrWhiteSpace(en.Text) ? "18:00" : en.Text.Trim();
            Store.Save(state);
            w.Close();
            Render();
        };
        w.ShowDialog();
    }

    private static TextBox AddField(Panel p, string label, string value)
    {
        p.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Res("Muted"),
            FontSize = 12,
            Margin = new Thickness(0, 8, 0, 4)
        });
        var tb = new TextBox { Text = value };
        p.Children.Add(tb);
        return tb;
    }

    private static double ParseNum(string s)
    {
        if (double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
            return v;
        return 0;
    }

    private void DoExport()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "JSON (*.json)|*.json",
            FileName = "work-calendar-" + DateTime.Today.ToString("yyyy-MM-dd") + ".json"
        };
        if (dlg.ShowDialog(this) == true)
        {
            try
            {
                Store.Export(dlg.FileName, state);
                MessageBox.Show(this, "Экспортировано:\n" + dlg.FileName, "Готово");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Ошибка экспорта");
            }
        }
    }

    private void DoImport()
    {
        var dlg = new OpenFileDialog { Filter = "JSON (*.json)|*.json" };
        if (dlg.ShowDialog(this) == true)
        {
            try
            {
                state = Store.Import(dlg.FileName);
                Store.Save(state);
                Render();
                MessageBox.Show(this, "Импортировано.", "Готово");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Ошибка импорта");
            }
        }
    }
}
