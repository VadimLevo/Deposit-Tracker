using System;
using Newtonsoft.Json;

namespace Учёт_Вкладов
{
    public class Deposit
    {
        public static double CurrentKeyRate { get; set; } = 16.0;

        public Guid Id { get; set; } = Guid.NewGuid();
        public string Bank { get; set; } = "";
        public string Owner { get; set; } = "";
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "RUB";

        public double Rate { get; set; }
        public bool IsKeyRateLinked { get; set; }

        public string PayoutType { get; set; } = "";
        public DateTime StartDate { get; set; } = DateTime.Today;
        public DateTime? EndDate { get; set; } = DateTime.Today.AddYears(1);

        public bool IsClosed { get; set; }
        public bool IsPerpetual { get; set; }
        public bool IsOPIF { get; set; }

        // === НОВЫЕ СВОЙСТВА ДЛЯ СВЕРКИ ===
        public DateTime LastReconciled { get; set; } = DateTime.Today; // Сохраняется в базу

        [JsonIgnore]
        public bool IsReconciledSession { get; set; } // Галочка (сбрасывается при закрытии)

        [JsonIgnore]
        public string LastReconciledString => LastReconciled.ToShortDateString();
        // ===================================

        [JsonIgnore]
        public string RateString
        {
            get
            {
                if (IsOPIF) return "ОПИФ";
                if (IsKeyRateLinked) return $"КС {(Rate >= 0 ? "+" : "")}{Rate:F2}% ({(CurrentKeyRate + Rate):F2}%)";
                return $"{Rate:F2}%";
            }
        }

        [JsonIgnore]
        public string EffectiveRateString
        {
            get
            {
                if (IsOPIF) return "-";
                return $"{CalculateEffectiveRate():F2}%";
            }
        }

        [JsonIgnore]
        public string EndDateString
        {
            get
            {
                if (IsOPIF || IsPerpetual || !EndDate.HasValue) return "Бессрочно";
                return EndDate.Value.ToShortDateString();
            }
        }

        [JsonIgnore]
        public string DaysLeft
        {
            get
            {
                if (IsOPIF || IsPerpetual || !EndDate.HasValue) return "-";
                int days = (EndDate.Value - DateTime.Today).Days;
                return days < 0 ? "Просрочен" : days.ToString();
            }
        }

        public double CalculateEffectiveRate()
        {
            if (IsOPIF) return 0;

            double nominalRate = IsKeyRateLinked ? CurrentKeyRate + Rate : Rate;

            if (PayoutType == "Ежемесячно (капитализация)")
            {
                double ratePerMonth = nominalRate / 100 / 12;
                return (Math.Pow(1 + ratePerMonth, 12) - 1) * 100;
            }

            return nominalRate;
        }
    }
}