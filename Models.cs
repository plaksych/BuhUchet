using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BuhUchet
{
    // ─── Проводка (журнальная запись) ───────────────────────────────────────
    public class JournalEntry : INotifyPropertyChanged
    {
        private int _id;
        private DateTime _date;
        private string _debitAccount  = "";
        private string _creditAccount = "";
        private string _counterparty  = "";
        private string _description   = "";
        private decimal _amount;

        public int      Id             { get => _id;             set { _id = value;             OnPropertyChanged(); } }
        public DateTime Date           { get => _date;           set { _date = value;           OnPropertyChanged(); } }
        public string   DebitAccount   { get => _debitAccount;   set { _debitAccount = value;   OnPropertyChanged(); } }
        public string   CreditAccount  { get => _creditAccount;  set { _creditAccount = value;  OnPropertyChanged(); } }
        public string   Counterparty   { get => _counterparty;   set { _counterparty = value;   OnPropertyChanged(); } }
        public string   Description    { get => _description;    set { _description = value;    OnPropertyChanged(); } }
        public decimal  Amount         { get => _amount;         set { _amount = value;         OnPropertyChanged(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ─── Контрагент ─────────────────────────────────────────────────────────
    public class Counterparty
    {
        public string Name { get; set; } = "";
        public string Inn  { get; set; } = "";
        public string Type { get; set; } = ""; // Поставщик / Покупатель / Прочее
    }

    // ─── Счёт плана счетов ──────────────────────────────────────────────────
    public class AccountPlan
    {
        public string Code        { get; set; } = "";
        public string Name        { get; set; } = "";
        public string Type        { get; set; } = ""; // Актив / Пассив / АП
    }

    // ─── Строка ОСВ (оборотно-сальдовая ведомость) ──────────────────────────
    public class OsvRow
    {
        public string  Account     { get; set; } = "";
        public string  AccountName { get; set; } = "";
        public decimal OpenDebit   { get; set; }
        public decimal OpenCredit  { get; set; }
        public decimal TurnDebit   { get; set; }
        public decimal TurnCredit  { get; set; }
        public decimal CloseDebit  => Math.Max(0, OpenDebit  - OpenCredit  + TurnDebit  - TurnCredit);
        public decimal CloseCredit => Math.Max(0, OpenCredit - OpenDebit   + TurnCredit - TurnDebit);
    }

    // ─── Файл сохранения ────────────────────────────────────────────────────
    public class SaveFile
    {
        public List<JournalEntry> Journal       { get; set; } = new();
        public List<Counterparty> Counterparties { get; set; } = new();
        public List<AccountPlan>  Accounts       { get; set; } = new();
        public DateTime           SavedAt        { get; set; } = DateTime.Now;
    }
}
