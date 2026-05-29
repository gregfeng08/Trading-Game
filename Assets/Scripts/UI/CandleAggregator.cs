using System.Collections.Generic;
using System.Globalization;
using Game.API.DTO;

public enum ChartTimeframe { Week1, Month1, Month3, Year1, Year5 }

public static class CandleAggregator
{
    public static int LookbackCalendarDays(ChartTimeframe tf)
    {
        return tf switch
        {
            ChartTimeframe.Week1 => 12,
            ChartTimeframe.Month1 => 40,
            ChartTimeframe.Month3 => 110,
            ChartTimeframe.Year1 => 400,
            ChartTimeframe.Year5 => 1900,
            _ => 110
        };
    }

    public static int MaxBars(ChartTimeframe tf)
    {
        return tf switch
        {
            ChartTimeframe.Week1 => 5,
            ChartTimeframe.Month1 => 22,
            ChartTimeframe.Month3 => 65,
            ChartTimeframe.Year1 => 52,
            ChartTimeframe.Year5 => 60,
            _ => 65
        };
    }

    public static int MaxPortfolioPoints(ChartTimeframe tf)
    {
        return tf switch
        {
            ChartTimeframe.Week1 => 15,
            ChartTimeframe.Month1 => 66,
            ChartTimeframe.Month3 => 195,
            ChartTimeframe.Year1 => 260,
            ChartTimeframe.Year5 => 300,
            _ => 195
        };
    }

    public static PriceRowDTO[] Aggregate(PriceRowDTO[] daily, ChartTimeframe tf)
    {
        if (daily == null || daily.Length == 0) return daily;

        PriceRowDTO[] result = tf switch
        {
            ChartTimeframe.Year1 => AggregateWeekly(daily),
            ChartTimeframe.Year5 => AggregateMonthly(daily),
            _ => daily
        };

        int max = MaxBars(tf);
        if (result.Length > max)
        {
            var trimmed = new PriceRowDTO[max];
            System.Array.Copy(result, result.Length - max, trimmed, 0, max);
            return trimmed;
        }

        return result;
    }

    private static PriceRowDTO[] AggregateWeekly(PriceRowDTO[] daily)
    {
        var buckets = new List<List<PriceRowDTO>>();
        List<PriceRowDTO> current = null;
        int lastWeek = -1;
        int lastYear = -1;

        foreach (var d in daily)
        {
            if (!System.DateTime.TryParse(d.date, out var dt))
                continue;

            int year = dt.Year;
            int week = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                dt, CalendarWeekRule.FirstFourDayWeek, System.DayOfWeek.Monday);

            if (current == null || week != lastWeek || year != lastYear)
            {
                current = new List<PriceRowDTO>();
                buckets.Add(current);
                lastWeek = week;
                lastYear = year;
            }
            current.Add(d);
        }

        return MergeBuckets(buckets);
    }

    private static PriceRowDTO[] AggregateMonthly(PriceRowDTO[] daily)
    {
        var buckets = new List<List<PriceRowDTO>>();
        List<PriceRowDTO> current = null;
        int lastMonth = -1;
        int lastYear = -1;

        foreach (var d in daily)
        {
            if (!System.DateTime.TryParse(d.date, out var dt))
                continue;

            if (current == null || dt.Month != lastMonth || dt.Year != lastYear)
            {
                current = new List<PriceRowDTO>();
                buckets.Add(current);
                lastMonth = dt.Month;
                lastYear = dt.Year;
            }
            current.Add(d);
        }

        return MergeBuckets(buckets);
    }

    private static PriceRowDTO[] MergeBuckets(List<List<PriceRowDTO>> buckets)
    {
        var result = new PriceRowDTO[buckets.Count];
        for (int i = 0; i < buckets.Count; i++)
        {
            var b = buckets[i];
            double high = double.MinValue;
            double low = double.MaxValue;
            foreach (var d in b)
            {
                if (d.high_price > high) high = d.high_price;
                if (d.low_price < low) low = d.low_price;
            }
            result[i] = new PriceRowDTO
            {
                ticker_id = b[0].ticker_id,
                date = b[0].date,
                open_price = b[0].open_price,
                close_price = b[b.Count - 1].close_price,
                high_price = high,
                low_price = low
            };
        }
        return result;
    }
}
