using System;
using Newtonsoft.Json;

namespace Учёт_Вкладов
{
    public class Deposit
    {
        // Глобальная переменная для текущей ключевой ставки
        public static double CurrentKeyRate { get; set; } = 16.0;

        public Guid Id { get; set; } = Guid.NewGuid();
        public string Bank { get; set; }
        public string Owner { get; set; }
        public string Currency { get; set; }
        public decimal Amount { get; set; }

        // Флаг привязки к Ключевой Ставке
        public bool IsKeyRateLinked { get; set; }
        // Если привязки нет - здесь обычная ставка (например, 15)
        // Если привязка есть - здесь надбавка/вычет (например, -1.0 или +0.5)
        public double Rate { get; set; }

        public string PayoutType { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsClosed { get; set; }

        [JsonIgnore]
        public int DaysLeft => EndDate.HasValue ? (EndDate.Value.Date - DateTime.Today).Days : 9999;

        [JsonIgnore]
        public string EndDateString => EndDate.HasValue ? EndDate.Value.ToShortDateString() : "Бессрочно";

        // Расчет фактической номинальной ставки на данный момент
        [JsonIgnore]
        public double ActualNominalRate => IsKeyRateLinked ? (CurrentKeyRate + Rate) : Rate;

        // Красивое отображение в таблице
        [JsonIgnore]
        public string RateString => IsKeyRateLinked ? $"КС{(Rate > 0 ? "+" : "")}{(Rate != 0 ? Rate.ToString() : "")} ({ActualNominalRate}%)" : $"{Rate}%";

        // Эффективная ставка с учетом капитализации
        [JsonIgnore]
        public double EffectiveRate
        {
            get
            {
                double r = ActualNominalRate;
                if (PayoutType == "Ежемесячно (капитализация)")
                {
                    return (Math.Pow(1 + (r / 100.0 / 12.0), 12) - 1) * 100.0;
                }
                return r;
            }
        }

        [JsonIgnore]
        public string EffectiveRateString => $"{EffectiveRate:F2}";
    }
}