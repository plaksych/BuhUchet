using System;
using System.Collections.Generic;
using System.Linq;

namespace BuhUchet
{
    /// <summary>
    /// Генерация отчётов.
    /// </summary>
    public class DataService
    {
        // ─── ОСВ (оборотно-сальдовая ведомость) ─────────────────────────────
        public List<OsvRow> BuildOsv(
            List<JournalEntry> journal,
            List<AccountPlan> accounts,
            DateTime from, DateTime to)
        {
            // Собираем все задействованные счета
            var allCodes = journal
                .SelectMany(e => new[] { e.DebitAccount, e.CreditAccount })
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            var rows = new List<OsvRow>();

            foreach (var code in allCodes)
            {
                var name = accounts.FirstOrDefault(a => a.Code == code)?.Name ?? "—";

                // Обороты за период
                decimal turnDt = journal
                    .Where(e => e.Date >= from && e.Date <= to && e.DebitAccount == code)
                    .Sum(e => e.Amount);
                decimal turnCt = journal
                    .Where(e => e.Date >= from && e.Date <= to && e.CreditAccount == code)
                    .Sum(e => e.Amount);

                // Сальдо до периода (входящее)
                decimal preDt = journal
                    .Where(e => e.Date < from && e.DebitAccount == code)
                    .Sum(e => e.Amount);
                decimal preCt = journal
                    .Where(e => e.Date < from && e.CreditAccount == code)
                    .Sum(e => e.Amount);

                rows.Add(new OsvRow
                {
                    Account     = code,
                    AccountName = name,
                    OpenDebit   = preDt,
                    OpenCredit  = preCt,
                    TurnDebit   = turnDt,
                    TurnCredit  = turnCt
                });
            }

            return rows;
        }

        // ─── Отчёт по контрагенту ────────────────────────────────────────────
        public List<JournalEntry> FilterByCounterparty(
            List<JournalEntry> journal, string counterparty)
            => journal
                .Where(e => e.Counterparty.Equals(counterparty, StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.Date)
                .ToList();

    }
}
