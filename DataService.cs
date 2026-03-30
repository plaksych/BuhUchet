using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace BuhUchet
{
    /// <summary>
    /// Сервис данных: сохранение/загрузка JSON, генерация отчётов.
    /// </summary>
    public class DataService
    {
        private static readonly JsonSerializerSettings _jsonSettings = new()
        {
            Formatting = Formatting.Indented,
            DateFormatString = "yyyy-MM-dd"
        };

        // ─── Сохранение ─────────────────────────────────────────────────────
        public void Save(string path, SaveFile data)
        {
            data.SavedAt = DateTime.Now;
            string json = JsonConvert.SerializeObject(data, _jsonSettings);
            File.WriteAllText(path, json);
        }

        // ─── Загрузка ────────────────────────────────────────────────────────
        public SaveFile Load(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Файл не найден: {path}");

            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<SaveFile>(json, _jsonSettings)
                   ?? new SaveFile();
        }

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

        // ─── Предустановленные данные (план счетов) ──────────────────────────
        public static List<AccountPlan> DefaultAccounts() => new()
        {
            new() { Code = "01",  Name = "Основные средства",                  Type = "Актив" },
            new() { Code = "02",  Name = "Амортизация ОС",                     Type = "Пассив" },
            new() { Code = "10",  Name = "Материалы",                          Type = "Актив" },
            new() { Code = "19",  Name = "НДС по приобретённым ценностям",     Type = "Актив" },
            new() { Code = "20",  Name = "Основное производство",              Type = "Актив" },
            new() { Code = "26",  Name = "Общехозяйственные расходы",          Type = "Актив" },
            new() { Code = "41",  Name = "Товары",                             Type = "Актив" },
            new() { Code = "43",  Name = "Готовая продукция",                  Type = "Актив" },
            new() { Code = "50",  Name = "Касса",                              Type = "Актив" },
            new() { Code = "51",  Name = "Расчётные счета",                    Type = "Актив" },
            new() { Code = "60",  Name = "Расчёты с поставщиками",             Type = "АП"    },
            new() { Code = "62",  Name = "Расчёты с покупателями",             Type = "АП"    },
            new() { Code = "66",  Name = "Краткосрочные кредиты",              Type = "Пассив" },
            new() { Code = "68",  Name = "Расчёты по налогам",                 Type = "АП"    },
            new() { Code = "69",  Name = "Расчёты по соц. страхованию",        Type = "АП"    },
            new() { Code = "70",  Name = "Расчёты с персоналом по оплате труда",Type = "Пассив"},
            new() { Code = "76",  Name = "Расчёты с прочими контрагентами",    Type = "АП"    },
            new() { Code = "80",  Name = "Уставный капитал",                   Type = "Пассив" },
            new() { Code = "84",  Name = "Нераспределённая прибыль",           Type = "АП"    },
            new() { Code = "90",  Name = "Продажи",                            Type = "АП"    },
            new() { Code = "91",  Name = "Прочие доходы и расходы",            Type = "АП"    },
            new() { Code = "99",  Name = "Прибыли и убытки",                   Type = "АП"    },
        };

        // ─── Демо-данные ─────────────────────────────────────────────────────
        public static SaveFile DemoData()
        {
            var counterparties = new List<Counterparty>
            {
                new() { Name = "Ремсервис ООО",         Inn = "7701234567", Type = "Поставщик" },
                new() { Name = "Ремонт-Плюс",           Inn = "7709876543", Type = "Поставщик" },
                new() { Name = "СтройМастер ЗАО",       Inn = "7712345678", Type = "Поставщик" },
                new() { Name = "АвтоТранс",             Inn = "7723456789", Type = "Покупатель" },
                new() { Name = "АвтоЛогистик",         Inn = "7734567890", Type = "Покупатель" },
                new() { Name = "Торговый дом «Север»",  Inn = "7745678901", Type = "Покупатель" },
                new() { Name = "МедГрупп",              Inn = "7756789012", Type = "Прочее"    },
                new() { Name = "ТехноСнаб",             Inn = "7767890123", Type = "Поставщик" },
            };

            var journal = new List<JournalEntry>
            {
                new() { Id=1, Date=new DateTime(2025,1,5),  DebitAccount="51", CreditAccount="62", Counterparty="АвтоТранс",          Amount=250000, Description="Оплата от покупателя" },
                new() { Id=2, Date=new DateTime(2025,1,10), DebitAccount="60", CreditAccount="51", Counterparty="Ремсервис ООО",       Amount=84000,  Description="Оплата поставщику за ремонт" },
                new() { Id=3, Date=new DateTime(2025,1,15), DebitAccount="10", CreditAccount="60", Counterparty="ТехноСнаб",           Amount=36500,  Description="Поступление материалов" },
                new() { Id=4, Date=new DateTime(2025,2,3),  DebitAccount="26", CreditAccount="70", Counterparty="",                    Amount=120000, Description="Начисление зарплаты АУП" },
                new() { Id=5, Date=new DateTime(2025,2,3),  DebitAccount="70", CreditAccount="51", Counterparty="",                    Amount=120000, Description="Выплата зарплаты" },
                new() { Id=6, Date=new DateTime(2025,2,20), DebitAccount="51", CreditAccount="62", Counterparty="Торговый дом «Север»",Amount=175000, Description="Получена предоплата" },
                new() { Id=7, Date=new DateTime(2025,3,1),  DebitAccount="60", CreditAccount="51", Counterparty="СтройМастер ЗАО",    Amount=55000,  Description="Оплата строительных работ" },
            };

            return new SaveFile
            {
                Journal        = journal,
                Counterparties = counterparties,
                Accounts       = DefaultAccounts()
            };
        }
    }
}
