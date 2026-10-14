using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WorkCalendar;

public partial class DayEditorWindow : Window
{
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    public DayEntry? Result { get; private set; }
    public bool Deleted { get; private set; }

    private readonly string _key;
    private readonly AppSettings _settings;
    private readonly List<Expense> _expenses = new();
    private bool _suppressHours;

    public DayEditorWindow(string key, DayEntry? existing, AppSettings settings)
    {
        InitializeComponent();
        _key = key;
        _settings = settings;

        var dt = DateTime.ParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        TitleText.Text = dt.ToString("dddd, d MMMM yyyy", Ru);

        var e = existing ?? new DayEntry
        {
            Worked = true,
            Mode = PayMode.Hourly,
            Rate = settings.HourlyRate,
            Start = settings.Start,
            End = settings.End,
            Hours = HoursBetween(settings.Start, settings.End),
        };

        WorkedChk.IsChecked = e.Worked;
        HourlyRb.IsChecked = e.Mode == PayMode.Hourly;
        ShiftRb.IsChecked = e.Mode == PayMode.Shift;
        RateBox.Text = e.Rate.ToString("0.##", Ru);
        StartBox.Text = e.Start;
        EndBox.Text = e.End;
        HoursBox.Text = e.Hours.ToString("0.##", Ru);
        NoteBox.Text = e.Note;
        foreach (var x in e.Expenses)
            _expenses.Add(new Expense { Name = x.Name, Amount = x.Amount });

        WorkedChk.Checked += (_, _) => UpdateCalc();
        WorkedChk.Unchecked += (_, _) => UpdateCalc();
        HourlyRb.Checked += (_, _) => OnModeChanged();
        ShiftRb.Checked += (_, _) => OnModeChanged();
        RateBox.TextChanged += (_, _) => UpdateCalc();
        HoursBox.TextChanged += (_, _) => UpdateCalc();
        StartBox.TextChanged += (_, _) => SyncHours();
        EndBox.TextChanged += (_, _) => SyncHours();
        AddExpBtn.Click += (_, _) => { _expenses.Add(new Expense()); RenderExp(); UpdateCalc(); };
        DeleteBtn.Click += (_, _) => { Deleted = true; DialogResult = true; };
        CancelBtn.Click += (_, _) => DialogResult = false;
        SaveBtn.Click += (_, _) => Save();

        RateLabel.Text = e.Mode == PayMode.Hourly ? "Ставка, за час" : "Ставка, за смену";
        HourlyPanel.Visibility = e.Mode == PayMode.Hourly ? Visibility.Visible : Visibility.Collapsed;
        RenderExp();
        UpdateCalc();
    }

    private static Brush Res(string key) => (Brush)Application.Current.Resources[key];

    private string Money(double v) => v.ToString("N0", Ru) + " " + _settings.Currency;

    private static double ParseNum(string s)
    {
        if (double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
            return v;
        return 0;
    }

    private static double HoursBetween(string a, string b)
    {
        if (!TimeSpan.TryParse(a, CultureInfo.InvariantCulture, out var s)) return 0;
        if (!TimeSpan.TryParse(b, CultureInfo.InvariantCulture, out var e)) return 0;
        var d = (e - s).TotalMinutes;
        if (d < 0) d += 24 * 60;
        return Math.Round(d / 60.0 * 100) / 100;
    }

    private void OnModeChanged()
    {
        bool hourly = HourlyRb.IsChecked == true;
        RateLabel.Text = hourly ? "Ставка, за час" : "Ставка, за смену";
        HourlyPanel.Visibility = hourly ? Visibility.Visible : Visibility.Collapsed;
        RateBox.Text = (hourly ? _settings.HourlyRate : _settings.ShiftRate).ToString("0.##", Ru);
        UpdateCalc();
    }

    private void SyncHours()
    {
        if (_suppressHours) return;
        var h = HoursBetween(StartBox.Text, EndBox.Text);
        _suppressHours = true;
        HoursBox.Text = h.ToString("0.##", Ru);
        _suppressHours = false;
        UpdateCalc();
    }

    private DayEntry Collect()
    {
        return new DayEntry
        {
            Worked = WorkedChk.IsChecked == true,
            Mode = HourlyRb.IsChecked == true ? PayMode.Hourly : PayMode.Shift,
            Rate = ParseNum(RateBox.Text),
            Start = StartBox.Text.Trim(),
            End = EndBox.Text.Trim(),
            Hours = ParseNum(HoursBox.Text),
            Note = NoteBox.Text,
            Expenses = _expenses
                .Where(x => !string.IsNullOrWhiteSpace(x.Name) || x.Amount != 0)
                .Select(x => new Expense { Name = x.Name.Trim(), Amount = x.Amount })
                .ToList(),
        };
    }

    private void UpdateCalc()
    {
        var e = Collect();
        double n = e.Net;
        EarnText.Text = Money(e.Earned);
        SpendText.Text = Money(e.Spent);
        NetText.Text = Money(n);
        NetText.Foreground = n >= 0 ? Res("Green") : Res("Red");
    }

    private void RenderExp()
    {
        ExpPanel.Children.Clear();
        if (_expenses.Count == 0)
        {
            ExpPanel.Children.Add(new TextBlock
            {
                Text = "Пока пусто — расходов нет.",
                Foreground = Res("Muted"),
                FontSize = 12,
                Margin = new Thickness(0, 2, 0, 2)
            });
            return;
        }

        for (int i = 0; i < _expenses.Count; i++)
        {
            int idx = i;
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });

            var name = new TextBox { Text = _expenses[i].Name, Margin = new Thickness(0, 0, 6, 0) };
            name.TextChanged += (_, _) => _expenses[idx].Name = name.Text;
            Grid.SetColumn(name, 0);
            grid.Children.Add(name);

            var amt = new TextBox
            {
                Text = _expenses[i].Amount == 0 ? "" : _expenses[i].Amount.ToString("0.##", Ru),
                Margin = new Thickness(0, 0, 6, 0)
            };
            amt.TextChanged += (_, _) => { _expenses[idx].Amount = ParseNum(amt.Text); UpdateCalc(); };
            Grid.SetColumn(amt, 1);
            grid.Children.Add(amt);

            var del = new Button { Content = "✕", Padding = new Thickness(0, 6, 0, 6) };
            del.Click += (_, _) => { _expenses.RemoveAt(idx); RenderExp(); UpdateCalc(); };
            Grid.SetColumn(del, 2);
            grid.Children.Add(del);

            ExpPanel.Children.Add(grid);
        }
    }

    private void Save()
    {
        var e = Collect();
        if (!e.Worked)
        {
            Deleted = true;
        }
        else
        {
            Result = e;
        }
        DialogResult = true;
    }
}
