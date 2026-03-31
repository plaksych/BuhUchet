using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BuhUchet
{
    public abstract class NotifyObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ─── Проводка (журнальная запись) ───────────────────────────────────────
    public class JournalEntry : NotifyObject
    {
        private int _id;
        private DateTime _date;
        private string _debitAccount  = "";
        private string _creditAccount = "";
        private string _counterparty  = "";
        private string _counterpartyInn = "";
        private string _counterpartyBankAccount = "";
        private string _description   = "";
        private decimal _amount;

        public int      Id             { get => _id;             set { _id = value;             OnPropertyChanged(); } }
        public DateTime Date           { get => _date;           set { _date = value;           OnPropertyChanged(); } }
        public string   DebitAccount   { get => _debitAccount;   set { _debitAccount = value;   OnPropertyChanged(); } }
        public string   CreditAccount  { get => _creditAccount;  set { _creditAccount = value;  OnPropertyChanged(); } }
        public string   Counterparty   { get => _counterparty;   set { _counterparty = value;   OnPropertyChanged(); } }
        public string   CounterpartyInn { get => _counterpartyInn; set { _counterpartyInn = value; OnPropertyChanged(); } }
        public string   CounterpartyBankAccount { get => _counterpartyBankAccount; set { _counterpartyBankAccount = value; OnPropertyChanged(); } }
        public string   Description    { get => _description;    set { _description = value;    OnPropertyChanged(); } }
        public decimal  Amount         { get => _amount;         set { _amount = value;         OnPropertyChanged(); } }
    }

    // ─── Контрагент ─────────────────────────────────────────────────────────
    public class Counterparty : NotifyObject
    {
        private string _name = "";
        private string _inn = "";
        private string _bankAccount = "";
        private string _personalAccount = "";
        private string _type = "";

        public string Name            { get => _name;            set { _name = value;            OnPropertyChanged(); } }
        public string Inn             { get => _inn;             set { _inn = value;             OnPropertyChanged(); } }
        public string BankAccount     { get => _bankAccount;     set { _bankAccount = value;     OnPropertyChanged(); } } // номер банковского счёта
        public string PersonalAccount { get => _personalAccount; set { _personalAccount = value; OnPropertyChanged(); } } // номер лицевого счёта
        public string Type            { get => _type;            set { _type = value;            OnPropertyChanged(); } } // Поставщик / Покупатель / Прочее
    }

    // ─── Счёт плана счетов ──────────────────────────────────────────────────
    public class AccountPlan : NotifyObject
    {
        private string _code = "";
        private string _name = "";
        private string _type = "";

        public string Code        { get => _code; set { _code = value; OnPropertyChanged(); } }
        public string Name        { get => _name; set { _name = value; OnPropertyChanged(); } }
        public string Type        { get => _type; set { _type = value; OnPropertyChanged(); } } // Актив / Пассив / АП
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
