using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BuhUchet
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<JournalEntry> _journal = new();
        private ObservableCollection<Counterparty> _counterparties = new();
        private ObservableCollection<AccountPlan> _accounts = new();

        private readonly DataService _dataService = new();
        private readonly AutoCompleteService _autoComplete = new();
        private readonly string _dataPath = AppDataStore.DefaultPath;

        private bool _isDirty;
        private DateTime? _lastSavedAt;
        private DispatcherTimer? _saveDebounceTimer;

        private JournalEntry? _editingEntry;
        private Counterparty? _editingCp;
        private bool _isNewEntry;
        private bool _isNewCp;
        private int _nextId = 1;

        public MainWindow()
        {
            InitializeComponent();
            LoadData();
            BindGrids();
            SetupAutoComplete();
            NavigateTo("Journal");
            UpdateStatusBar();

            PickOsvFrom.SelectedDate = new DateTime(DateTime.Today.Year, 1, 1);
            PickOsvTo.SelectedDate = DateTime.Today;
        }

        private void LoadData()
        {
            var data = AppDataStore.LoadOrCreate(_dataPath);
            _journal = new ObservableCollection<JournalEntry>(data.Journal ?? new List<JournalEntry>());
            _counterparties = new ObservableCollection<Counterparty>(data.Counterparties ?? new List<Counterparty>());
            _accounts = new ObservableCollection<AccountPlan>(data.Accounts ?? new List<AccountPlan>());
            _nextId = _journal.Count > 0 ? _journal.Max(e => e.Id) + 1 : 1;
            _lastSavedAt = data.SavedAt;
            _isDirty = false;
        }

        private void BindGrids()
        {
            GridJournal.ItemsSource = _journal;
            GridCounterparties.ItemsSource = _counterparties;
            GridAccounts.ItemsSource = _accounts;
            UpdateJournalCount();
        }

        private void SetupAutoComplete()
        {
            _autoComplete.Attach(TxtCounterparty, () => _counterparties.Select(c => c.Name), ApplyCounterpartyToEntryByName);
            _autoComplete.Attach(TxtDebit, () => _accounts.Select(a => a.Code).Concat(_accounts.Select(a => a.Name)));
            _autoComplete.Attach(TxtCredit, () => _accounts.Select(a => a.Code).Concat(_accounts.Select(a => a.Name)));
            _autoComplete.Attach(TxtCpName, () => _counterparties.Select(c => c.Name));
            _autoComplete.Attach(TxtDescription, () => _journal.Select(e => e.Description).Distinct());

            TxtCounterparty.TextChanged += (_, _) => ApplyCounterpartyToEntryByName(TxtCounterparty.Text);
        }

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string section)
                NavigateTo(section);
        }

        private void NavigateTo(string section)
        {
            PanelJournal.Visibility = section == "Journal" ? Visibility.Visible : Visibility.Collapsed;
            PanelCounterparties.Visibility = section == "Counterparties" ? Visibility.Visible : Visibility.Collapsed;
            PanelAccounts.Visibility = section == "Accounts" ? Visibility.Visible : Visibility.Collapsed;
            PanelReports.Visibility = section == "Reports" ? Visibility.Visible : Visibility.Collapsed;

            BtnJournal.Style = (Style)FindResource(section == "Journal" ? "NavButtonActive" : "NavButton");
            BtnCounterparties.Style = (Style)FindResource(section == "Counterparties" ? "NavButtonActive" : "NavButton");
            BtnAccounts.Style = (Style)FindResource(section == "Accounts" ? "NavButtonActive" : "NavButton");
            BtnReports.Style = (Style)FindResource(section == "Reports" ? "NavButtonActive" : "NavButton");
        }

        private void BtnAddEntry_Click(object sender, RoutedEventArgs e)
        {
            _editingEntry = new JournalEntry { Id = _nextId, Date = DateTime.Today };
            _isNewEntry = true;
            ShowEntryForm(_editingEntry);
        }

        private void GridJournal_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridJournal.SelectedItem is not JournalEntry entry) return;
            _editingEntry = entry;
            _isNewEntry = false;
            ShowEntryForm(entry);
        }

        private void ShowEntryForm(JournalEntry entry)
        {
            PickDate.SelectedDate = entry.Date;
            TxtDebit.Text = entry.DebitAccount;
            TxtCredit.Text = entry.CreditAccount;
            TxtCounterparty.Text = entry.Counterparty;
            TxtCounterpartyInn.Text = entry.CounterpartyInn;
            TxtCounterpartyBank.Text = entry.CounterpartyBankAccount;
            TxtAmount.Text = entry.Amount > 0 ? entry.Amount.ToString("N2") : "";
            TxtDescription.Text = entry.Description;
            PanelEntryEdit.Visibility = Visibility.Visible;
        }

        private void BtnSaveEntry_Click(object sender, RoutedEventArgs e)
        {
            if (_editingEntry == null) return;
            if (PickDate.SelectedDate == null) { ShowStatus("Укажите дату операции", true); return; }
            if (string.IsNullOrWhiteSpace(TxtDebit.Text)) { ShowStatus("Укажите счёт дебета", true); return; }
            if (string.IsNullOrWhiteSpace(TxtCredit.Text)) { ShowStatus("Укажите счёт кредита", true); return; }
            if (!TryParseAmount(TxtAmount.Text, out decimal amount) || amount <= 0) { ShowStatus("Введите корректную сумму", true); return; }

            _editingEntry.Date = PickDate.SelectedDate.Value;
            _editingEntry.DebitAccount = TxtDebit.Text.Trim();
            _editingEntry.CreditAccount = TxtCredit.Text.Trim();
            _editingEntry.Counterparty = TxtCounterparty.Text.Trim();
            _editingEntry.CounterpartyInn = TxtCounterpartyInn.Text.Trim();
            _editingEntry.CounterpartyBankAccount = TxtCounterpartyBank.Text.Trim();
            _editingEntry.Amount = amount;
            _editingEntry.Description = TxtDescription.Text.Trim();

            if (_isNewEntry)
            {
                _journal.Add(_editingEntry);
                _nextId++;
                AutoRegisterCounterparty(_editingEntry.Counterparty);
            }

            PanelEntryEdit.Visibility = Visibility.Collapsed;
            _editingEntry = null;
            UpdateJournalCount();
            MarkDirty();
            ShowStatus("Операция сохранена");
        }

        private void BtnDeleteEntry_Click(object sender, RoutedEventArgs e)
        {
            if (_editingEntry == null) return;
            if (!_isNewEntry)
            {
                var r = MessageBox.Show($"Удалить операцию #{_editingEntry.Id}?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r != MessageBoxResult.Yes) return;
                _journal.Remove(_editingEntry);
                UpdateJournalCount();
                MarkDirty();
                ShowStatus("Операция удалена");
            }
            PanelEntryEdit.Visibility = Visibility.Collapsed;
            _editingEntry = null;
        }

        private void BtnCancelEntry_Click(object sender, RoutedEventArgs e)
        {
            PanelEntryEdit.Visibility = Visibility.Collapsed;
            _editingEntry = null;
            GridJournal.SelectedItem = null;
        }

        private void AutoRegisterCounterparty(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            if (_counterparties.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) return;
            _counterparties.Add(new Counterparty { Name = name, Type = "Прочее" });
        }

        private void ApplyCounterpartyToEntryByName(string name)
        {
            name = (name ?? "").Trim();
            var cp = _counterparties.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            TxtCounterpartyInn.Text = cp?.Inn ?? "";
            TxtCounterpartyBank.Text = cp?.BankAccount ?? "";
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
            => ApplyJournalFilter();

        private void JournalFilter_Changed(object sender, SelectionChangedEventArgs e)
            => ApplyJournalFilter();

        private void ApplyJournalFilter()
        {
            string q = TxtSearch.Text.Trim();
            DateTime? from = PickJournalFrom.SelectedDate?.Date;
            DateTime? to = PickJournalTo.SelectedDate?.Date;

            var filtered = _journal.Where(en =>
            {
                bool matchesText = string.IsNullOrWhiteSpace(q) ||
                    en.Counterparty.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    en.CounterpartyInn.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    en.CounterpartyBankAccount.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    en.Description.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    en.DebitAccount.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    en.CreditAccount.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    en.Date.ToString("dd.MM.yyyy").Contains(q);

                bool matchesFrom = !from.HasValue || en.Date.Date >= from.Value;
                bool matchesTo = !to.HasValue || en.Date.Date <= to.Value;
                return matchesText && matchesFrom && matchesTo;
            }).ToList();

            GridJournal.ItemsSource = filtered;
            UpdateJournalCount();
        }

        private void BtnAddCounterparty_Click(object sender, RoutedEventArgs e)
        {
            _editingCp = new Counterparty();
            _isNewCp = true;
            ShowCpForm(_editingCp);
        }

        private void GridCounterparties_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridCounterparties.SelectedItem is not Counterparty cp) return;
            _editingCp = cp;
            _isNewCp = false;
            ShowCpForm(cp);
        }

        private void ShowCpForm(Counterparty cp)
        {
            TxtCpName.Text = cp.Name;
            TxtCpInn.Text = cp.Inn;
            TxtCpBankAccount.Text = cp.BankAccount;
            TxtCpPersonalAccount.Text = cp.PersonalAccount;
            CbCpType.SelectedItem = CbCpType.Items.Cast<ComboBoxItem>().FirstOrDefault(i => i.Content?.ToString() == cp.Type);
            PanelCpEdit.Visibility = Visibility.Visible;
        }

        private void BtnSaveCp_Click(object sender, RoutedEventArgs e)
        {
            if (_editingCp == null) return;
            if (string.IsNullOrWhiteSpace(TxtCpName.Text)) { ShowStatus("Введите наименование контрагента", true); return; }

            bool isExisting = !_isNewCp;
            string oldName = _editingCp.Name;

            _editingCp.Name = TxtCpName.Text.Trim();
            _editingCp.Inn = TxtCpInn.Text.Trim();
            _editingCp.BankAccount = TxtCpBankAccount.Text.Trim();
            _editingCp.PersonalAccount = TxtCpPersonalAccount.Text.Trim();
            _editingCp.Type = (CbCpType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Прочее";

            if (_isNewCp) _counterparties.Add(_editingCp);

            // Если правили существующего контрагента — обновить связанные записи журнала,
            // чтобы ИНН/банк.счёт отображались сразу и сохранялись в data.json.
            if (isExisting)
                SyncJournalEntriesForCounterparty(oldName, _editingCp);

            PanelCpEdit.Visibility = Visibility.Collapsed;
            _editingCp = null;
            MarkDirty();
            ShowStatus("Контрагент сохранён");
        }

        private void SyncJournalEntriesForCounterparty(string oldName, Counterparty cp)
        {
            string newName = (cp.Name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(oldName) && string.IsNullOrWhiteSpace(newName)) return;

            foreach (var entry in _journal)
            {
                bool nameMatchesOld = !string.IsNullOrWhiteSpace(oldName) &&
                                      entry.Counterparty.Equals(oldName, StringComparison.OrdinalIgnoreCase);
                bool nameMatchesNew = !string.IsNullOrWhiteSpace(newName) &&
                                      entry.Counterparty.Equals(newName, StringComparison.OrdinalIgnoreCase);

                if (!nameMatchesOld && !nameMatchesNew) continue;

                // Если переименовали — подтянуть имя тоже
                if (nameMatchesOld && !string.IsNullOrWhiteSpace(newName))
                    entry.Counterparty = newName;

                entry.CounterpartyInn = cp.Inn ?? "";
                entry.CounterpartyBankAccount = cp.BankAccount ?? "";
            }

            // Если сейчас открыта форма записи — сразу обновить поля
            ApplyCounterpartyToEntryByName(TxtCounterparty.Text);
            ApplyJournalFilter();
        }

        private void BtnJournalReset_Click(object sender, RoutedEventArgs e)
        {
            PickJournalFrom.SelectedDate = null;
            PickJournalTo.SelectedDate = null;
            ApplyJournalFilter();
        }

        private void BtnDeleteCp_Click(object sender, RoutedEventArgs e)
        {
            if (_editingCp == null || _isNewCp) return;
            var r = MessageBox.Show($"Удалить «{_editingCp.Name}»?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;
            _counterparties.Remove(_editingCp);
            PanelCpEdit.Visibility = Visibility.Collapsed;
            _editingCp = null;
            MarkDirty();
            ShowStatus("Контрагент удалён");
        }

        private void BtnBuildOsv_Click(object sender, RoutedEventArgs e)
        {
            if (PickOsvFrom.SelectedDate == null || PickOsvTo.SelectedDate == null)
            {
                ShowStatus("Укажите период", true);
                return;
            }

            var from = PickOsvFrom.SelectedDate.Value;
            var to = PickOsvTo.SelectedDate.Value;
            var rows = _dataService.BuildOsv(_journal.ToList(), _accounts.ToList(), from, to);
            GridOsv.ItemsSource = rows;

            TxtOsvTotals.Text =
                $"ИТОГО: Оборот Дт = {rows.Sum(r => r.TurnDebit):N2}  |  " +
                $"Оборот Кт = {rows.Sum(r => r.TurnCredit):N2}  |  " +
                $"Сальдо кон. Дт = {rows.Sum(r => r.CloseDebit):N2}  |  " +
                $"Сальдо кон. Кт = {rows.Sum(r => r.CloseCredit):N2}";

            ShowStatus($"ОСВ сформирована: {rows.Count} счетов");
        }

        private void BtnExportOsv_Click(object sender, RoutedEventArgs e)
        {
            if (PickOsvFrom.SelectedDate == null || PickOsvTo.SelectedDate == null)
            {
                ShowStatus("Укажите период для экспорта", true);
                return;
            }

            var from = PickOsvFrom.SelectedDate.Value;
            var to = PickOsvTo.SelectedDate.Value;
            var rows = (GridOsv.ItemsSource as IEnumerable<OsvRow>)?.ToList() ??
                       _dataService.BuildOsv(_journal.ToList(), _accounts.ToList(), from, to);
            var usedCounterparties = _journal
                .Where(j => j.Date >= from && j.Date <= to && !string.IsNullOrWhiteSpace(j.Counterparty))
                .Select(j => j.Counterparty.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(name =>
                {
                    var cp = _counterparties.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    return new
                    {
                        Name = name,
                        Inn = cp?.Inn ?? "",
                        PersonalAccount = cp?.PersonalAccount ?? ""
                    };
                })
                .ToList();

            var reportData = new
            {
                Report = "ОСВ",
                PeriodFrom = from.ToString("yyyy-MM-dd"),
                PeriodTo = to.ToString("yyyy-MM-dd"),
                Counterparties = usedCounterparties,
                Rows = rows,
                Totals = new
                {
                    TurnDebit = rows.Sum(r => r.TurnDebit),
                    TurnCredit = rows.Sum(r => r.TurnCredit),
                    CloseDebit = rows.Sum(r => r.CloseDebit),
                    CloseCredit = rows.Sum(r => r.CloseCredit)
                }
            };

            var dlg = new SaveFileDialog
            {
                Filter = "JSON-файл (*.json)|*.json",
                Title = "Экспорт ОСВ",
                DefaultExt = "json",
                FileName = $"ОСВ_{from:yyyyMMdd}_{to:yyyyMMdd}"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                File.WriteAllText(dlg.FileName, JsonConvert.SerializeObject(reportData, Formatting.Indented));
                ShowStatus($"Экспортировано: {Path.GetFileName(dlg.FileName)}");
            }
            catch (Exception ex)
            {
                ShowStatus($"Ошибка экспорта: {ex.Message}", true);
            }
        }

        private void BtnLoadOsv_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "JSON-файл (*.json)|*.json",
                Title = "Загрузить отчёт ОСВ"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                string json = File.ReadAllText(dlg.FileName);
                var root = JObject.Parse(json);

                var rows = root["Rows"]?.ToObject<List<OsvRow>>() ?? new List<OsvRow>();
                GridOsv.ItemsSource = rows;

                DateTime parsedFrom;
                DateTime parsedTo;
                if (DateTime.TryParse(root["PeriodFrom"]?.ToString(), out parsedFrom))
                    PickOsvFrom.SelectedDate = parsedFrom;
                if (DateTime.TryParse(root["PeriodTo"]?.ToString(), out parsedTo))
                    PickOsvTo.SelectedDate = parsedTo;

                var totals = root["Totals"];
                decimal sumTurnDt = totals?["TurnDebit"]?.Value<decimal>() ?? rows.Sum(r => r.TurnDebit);
                decimal sumTurnCt = totals?["TurnCredit"]?.Value<decimal>() ?? rows.Sum(r => r.TurnCredit);
                decimal sumCloseDt = totals?["CloseDebit"]?.Value<decimal>() ?? rows.Sum(r => r.CloseDebit);
                decimal sumCloseCt = totals?["CloseCredit"]?.Value<decimal>() ?? rows.Sum(r => r.CloseCredit);

                TxtOsvTotals.Text =
                    $"ИТОГО: Оборот Дт = {sumTurnDt:N2}  |  " +
                    $"Оборот Кт = {sumTurnCt:N2}  |  " +
                    $"Сальдо кон. Дт = {sumCloseDt:N2}  |  " +
                    $"Сальдо кон. Кт = {sumCloseCt:N2}";

                ShowStatus($"Отчёт загружен: {Path.GetFileName(dlg.FileName)}");
            }
            catch (Exception ex)
            {
                ShowStatus($"Ошибка загрузки отчёта: {ex.Message}", true);
            }
        }

        private void MarkDirty()
        {
            _isDirty = true;
            UpdateStatusBar();
            ScheduleSave();
        }

        private void ScheduleSave()
        {
            _saveDebounceTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            _saveDebounceTimer.Stop();
            _saveDebounceTimer.Tick -= SaveDebounceTimer_Tick;
            _saveDebounceTimer.Tick += SaveDebounceTimer_Tick;
            _saveDebounceTimer.Start();
        }

        private void SaveDebounceTimer_Tick(object? sender, EventArgs e)
        {
            _saveDebounceTimer?.Stop();
            TrySaveData();
        }

        private void TrySaveData()
        {
            if (!_isDirty) return;
            try
            {
                var data = new SaveFile
                {
                    Journal = _journal.ToList(),
                    Counterparties = _counterparties.ToList(),
                    Accounts = _accounts.ToList(),
                    SavedAt = DateTime.Now
                };
                AppDataStore.Save(_dataPath, data);
                _isDirty = false;
                _lastSavedAt = data.SavedAt;
                UpdateStatusBar();
            }
            catch (Exception ex)
            {
                ShowStatus($"Ошибка автосохранения: {ex.Message}", true);
            }
        }

        private void UpdateStatusBar()
        {
            TxtFileName.Text = "data.json";
            TxtSavedAt.Text = _isDirty
                ? "● Есть несохранённые изменения (автосохранение)"
                : _lastSavedAt.HasValue ? $"Сохранено: {_lastSavedAt:dd.MM.yyyy HH:mm}" : "Сохранено";
        }

        private void UpdateJournalCount()
        {
            int total = _journal.Count;
            decimal sum = _journal.Sum(e => e.Amount);
            TxtJournalCount.Text = $"{total} записей  |  Итого: {sum:N2} руб.";
        }

        private void ShowStatus(string message, bool isError = false)
        {
            TxtStatus.Text = isError ? $"⚠ {message}" : $"✓ {message}";
            TxtStatus.Foreground = isError ? System.Windows.Media.Brushes.LightSalmon : System.Windows.Media.Brushes.LightGreen;
        }

        private static bool TryParseAmount(string text, out decimal amount)
        {
            text = (text ?? "").Trim().Replace(" ", "");
            if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.GetCultureInfo("ru-RU"), out amount)) return true;
            return decimal.TryParse(text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out amount);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            TrySaveData();
            base.OnClosing(e);
        }
    }
}
