using System.Collections.Generic;
using System.Linq;

namespace WorkCalendar;

public enum PayMode
{
    Hourly,
    Shift
}

public class Expense
{
    public string Name { get; set; } = "";
    public double Amount { get; set; }
}

public class DayEntry
{
    public bool Worked { get; set; }
    public PayMode Mode { get; set; } = PayMode.Hourly;
    public double Rate { get; set; }
    public string Start { get; set; } = "09:00";
    public string End { get; set; } = "18:00";
    public double Hours { get; set; }
    public List<Expense> Expenses { get; set; } = new();
    public string Note { get; set; } = "";

    public double Earned => !Worked ? 0 : (Mode == PayMode.Shift ? Rate : Rate * Hours);
    public double Spent => Expenses.Sum(e => e.Amount);
    public double Net => Earned - Spent;
}

public class AppSettings
{
    public string Currency { get; set; } = "₽";
    public double HourlyRate { get; set; } = 500;
    public double ShiftRate { get; set; } = 3000;
    public string Start { get; set; } = "09:00";
    public string End { get; set; } = "18:00";
}

public class AppState
{
    public AppSettings Settings { get; set; } = new();
    public Dictionary<string, DayEntry> Days { get; set; } = new();
}
