using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace BuhUchet
{
    public partial class MainWindow : Window
    {
        // ─── Данные ─────────────────────────────────────────────────────────
        private ObservableCollection<JournalEntry>  _journal         = new();
        private ObservableCollection<Counterparty>  _counterparties  = new();
        private ObservableCollection<AccountPlan>   _accounts        = new();

        private readonly DataService        _dataService    = new();
        private readonly AutoCompleteService _autoComplete  = new();

        private string? _currentFilePath;
        private bool    _isDirty;

        // Редактируемые объекты
        private JournalEntry? _editingEntry;
        private Counterparty? _editingCp;
        private bool          _isNewEntry;
        private bool          _isNewCp;
        private int           _nextId = 1;

        // ─── Инициализация ───────────────────────────────────────────────
        public MainWindow()
        {
            InitializeComponent();
            LoadDemoData();
            BindGrids();
            SetupAutoComplete();
            NavigateTo("Journal");
            UpdateStatusBar();

            // Дефолтный период ОСВ
            PickOsvFrom.SelectedDate = new DateTime(DateTime.Today.Year, 1, 1);
            PickOsvTo.SelectedDate   = DateTime.Today;
        }

        private void LoadDemoData()
        {
            var demo = DataService.DemoData();
            _journal        = new ObservableCollection<JournalEntry>(demo.Journal);
            _counterparties = new ObservableCollection<Counterparty>(demo.Counterparties);
            _accounts       = new ObservableCollection<AccountPlan>(demo.Accounts);
            _nextId = _journal.Count > 0 ? _journal.Max(e => e.Id) + 1 : 1;
        }

        private void BindGrids()
        {
            GridJournal.ItemsSource        = _journal;
            GridCounterparties.ItemsSource = _counterparties;
            GridAccounts.ItemsSource       = _accounts;
            UpdateJournalCount();
        }

        // ─── Автодополнение ──────────────────────────────────────────────
        private void SetupAutoComplete()
        {
            // Контрагент в журнале: по имени из справочника
            _autoComplete.Attach(TxtCounterparty, () => _counterparties.Select(c => c.Name));

            // Счета Дт/Кт: по коду из плана счетов
            _autoComplete.Attach(TxtDebit,  () => _accounts.Select(a => a.Code)
                                                             .Concat(_accounts.Select(a => a.Name)));
            _autoComplete.Attach(TxtCredit, () => _accounts.Select(a => a.Code)
                                                             .Concat(_accounts.Select(a => a.Name)));

            // Наименование контрагента в справочнике (обучает себя само)
            _autoComplete.Attach(TxtCpName, () => _counterparties.Select(c => c.Name));

            // Описание: из всех уже введённых описаний
            _autoComplete.Attach(TxtDescription, () => _journal.Select(e => e.Description).Distinct());
        }

        // ─── Навигация ───────────────────────────────────────────────────
        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
                NavigateTo(tag);
        }

        private void NavigateTo(string section)
        {
            PanelJournal.Visibility        = section == "Journal"        ? Visibility.Visible : Visibility.Collapsed;
            PanelCounterparties.Visibility = section == "Counterparties" ? Visibility.Visible : Visibility.Collapsed;
            PanelAccounts.Visibility       = section == "Accounts"       ? Visibility.Visible : Visibility.Collapsed;
            PanelReports.Visibility        = section == "Reports"        ? Visibility.Visible : Visibility.Collapsed;

            // Обновить стиль кнопок навигации
            BtnJournal.Style        = section == "Journal"        ? FindResource("NavButtonActive") as Style : FindResource("NavButton") as Style;
            BtnCounterparties.Style = section == "Counterparties" ? FindResource("NavButtonActive") as Style : FindResource("NavButton") as Style;
            BtnAccounts.Style       = section == "Accounts"       ? FindResource("NavButtonActive") as Style : FindResource("NavButton") as Style;
            BtnReports.Style        = section == "Reports"        ? FindResource("NavButtonActive") as Style : FindResource("NavButton") as Style;
        }

        // ─── ЖУРНАЛ: добавление ──────────────────────────────────────────
        private void BtnAddEntry_Click(object sender, RoutedEventArgs e)
        {
            _editingEntry = new JournalEntry
            {
                Id   = _nextId,
                Date = DateTime.Today
            };
            _isNewEntry = true;
            ShowEntryForm(_editingEntry);
        }

        private void GridJournal_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridJournal.SelectedItem is JournalEntry entry)
            {
                _editingEntry = entry;
                _isNewEntry   = false;
                ShowEntryForm(entry);
            }
        }

        private void ShowEntryForm(JournalEntry entry)
        {
            PickDate.SelectedDate   = entry.Date;
            TxtDebit.Text           = entry.DebitAccount;
            TxtCredit.Text          = entry.CreditAccount;
            TxtCounterparty.Text    = entry.Counterparty;
            TxtAmount.Text          = entry.Amount > 0 ? entry.Amount.ToString("N2") : "";
            TxtDescription.Text     = entry.Description;
            PanelEntryEdit.Visibility = Visibility.Visible;
            TxtDebit.Focus();
        }

        private void BtnSaveEntry_Click(object sender, RoutedEventArgs e)
        {
            if (_editingEntry == null) return;

            // Валидация
            if (PickDate.SelectedDate == null)
            { ShowStatus("Укажите дату операции", isError: true); return; }
            if (string.IsNullOrWhiteSpace(TxtDebit.Text))
            { ShowStatus("Укажите счёт дебета", isError: true); return; }
            if (string.IsNullOrWhiteSpace(TxtCredit.Text))
            { ShowStatus("Укажите счёт кредита", isError: true); return; }
            if (!decimal.TryParse(TxtAmount.Text.Replace(" ", "").Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out decimal amount) || amount <= 0)
            { ShowStatus("Введите корректную сумму", isError: true); return; }

            _editingEntry.Date          = PickDate.SelectedDate.Value;
            _editingEntry.DebitAccount  = TxtDebit.Text.Trim();
            _editingEntry.CreditAccount = TxtCredit.Text.Trim();
            _editingEntry.Counterparty  = TxtCounterparty.Text.Trim();
            _editingEntry.Amount        = amount;
            _editingEntry.Description   = TxtDescription.Text.Trim();

            if (_isNewEntry)
            {
                _journal.Add(_editingEntry);
                _nextId++;
                // Если контрагент новый — добавить в справочник
                AutoRegisterCounterparty(_editingEntry.Counterparty);
            }

            PanelEntryEdit.Visibility = Visibility.Collapsed;
            MarkDirty();
            UpdateJournalCount();
            ShowStatus($"Операция #{_editingEntry.Id} сохранена");
            _editingEntry = null;
        }

        private void BtnDeleteEntry_Click(object sender, RoutedEventArgs e)
        {
            if (_editingEntry == null) return;
            if (!_isNewEntry)
            {
                var r = MessageBox.Show($"Удалить операцию #{_editingEntry.Id}?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r != MessageBoxResult.Yes) return;
                _journal.Remove(_editingEntry);
                MarkDirty();
                UpdateJournalCount();
                ShowStatus($"Операция удалена");
            }
            PanelEntryEdit.Visibility = Visibility.Collapsed;
            _editingEntry = null;
        }

        private void BtnCancelEntry_Click(object sender, RoutedEventArgs e)
        {
            PanelEntryEdit.Visibility = Visibility.Collapsed;
            GridJournal.SelectedItem  = null;
            _editingEntry = null;
        }

        private void AutoRegisterCounterparty(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            if (!_counterparties.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                _counterparties.Add(new Counterparty { Name = name, Type = "Прочее" });
            }
        }

        // ─── ПОИСК по журналу ────────────────────────────────────────────
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string q = TxtSearch.Text.Trim().ToLower();
            if (string.IsNullOrWhiteSpace(q))
            {
                GridJournal.ItemsSource = _journal;
            }
            else
            {
                GridJournal.ItemsSource = _journal.Where(en =>
                    en.Counterparty.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    en.Description.Contains(q, StringComparison.OrdinalIgnoreCase)  ||
                    en.DebitAccount.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    en.CreditAccount.Contains(q, StringComparison.OrdinalIgnoreCase)||
                    en.Date.ToString("dd.MM.yyyy").Contains(q)
                ).ToList();
            }
            UpdateJournalCount();
        }

        // ─── КОНТРАГЕНТЫ ─────────────────────────────────────────────────
        private void BtnAddCounterparty_Click(object sender, RoutedEventArgs e)
        {
            _editingCp = new Counterparty();
            _isNewCp   = true;
            ShowCpForm(_editingCp);
        }

        private void GridCounterparties_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridCounterparties.SelectedItem is Counterparty cp)
            {
                _editingCp = cp;
                _isNewCp   = false;
                ShowCpForm(cp);
            }
        }

        private void ShowCpForm(Counterparty cp)
        {
            TxtCpName.Text       = cp.Name;
            TxtCpInn.Text        = cp.Inn;
            CbCpType.SelectedItem = CbCpType.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(i => i.Content.ToString() == cp.Type);
            PanelCpEdit.Visibility = Visibility.Visible;
            TxtCpName.Focus();
        }

        private void BtnSaveCp_Click(object sender, RoutedEventArgs e)
        {
            if (_editingCp == null) return;
            if (string.IsNullOrWhiteSpace(TxtCpName.Text))
            { ShowStatus("Введите наименование контрагента", isError: true); return; }

            _editingCp.Name = TxtCpName.Text.Trim();
            _editingCp.Inn  = TxtCpInn.Text.Trim();
            _editingCp.Type = (CbCpType.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Прочее";

            if (_isNewCp) _counterparties.Add(_editingCp);
            PanelCpEdit.Visibility = Visibility.Collapsed;
            MarkDirty();
            ShowStatus($"Контрагент «{_editingCp.Name}» сохранён");
            _editingCp = null;
        }

        private void BtnDeleteCp_Click(object sender, RoutedEventArgs e)
        {
            if (_editingCp == null || _isNewCp) return;
            var r = MessageBox.Show($"Удалить «{_editingCp.Name}»?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;
            _counterparties.Remove(_editingCp);
            PanelCpEdit.Visibility = Visibility.Collapsed;
            MarkDirty();
            ShowStatus("Контрагент удалён");
            _editingCp = null;
        }

        // ─── ОТЧЁТЫ / ОСВ ────────────────────────────────────────────────
        private void BtnBuildOsv_Click(object sender, RoutedEventArgs e)
        {
            if (PickOsvFrom.SelectedDate == null || PickOsvTo.SelectedDate == null)
            { ShowStatus("Укажите период", isError: true); return; }

            var from = PickOsvFrom.SelectedDate.Value;
            var to   = PickOsvTo.SelectedDate.Value;

            var rows = _dataService.BuildOsv(_journal.ToList(), _accounts.ToList(), from, to);
            GridOsv.ItemsSource = rows;

            // Итоги
            decimal sumTurnDt  = rows.Sum(r => r.TurnDebit);
            decimal sumTurnCt  = rows.Sum(r => r.TurnCredit);
            decimal sumCloseDt = rows.Sum(r => r.CloseDebit);
            decimal sumCloseCt = rows.Sum(r => r.CloseCredit);

            TxtOsvTotals.Text =
                $"ИТОГО: Оборот Дт = {sumTurnDt:N2}  |  " +
                $"Оборот Кт = {sumTurnCt:N2}  |  " +
                $"Сальдо кон. Дт = {sumCloseDt:N2}  |  " +
                $"Сальдо кон. Кт = {sumCloseCt:N2}";

            ShowStatus($"ОСВ сформирована: {rows.Count} счетов за {from:dd.MM.yyyy} – {to:dd.MM.yyyy}");
        }

        // ─── ФАЙЛОВЫЕ ОПЕРАЦИИ ────────────────────────────────────────────
        private void BtnNew_Click(object sender, RoutedEventArgs e)
        {
            if (_isDirty && !ConfirmDiscard()) return;
            _journal.Clear();
            _counterparties.Clear();
            _accounts = new ObservableCollection<AccountPlan>(DataService.DefaultAccounts());
            _nextId = 1;
            _currentFilePath = null;
            _isDirty = false;
            BindGrids();
            UpdateStatusBar();
            ShowStatus("Создан новый файл");
        }

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            if (_isDirty && !ConfirmDiscard()) return;
            var dlg = new OpenFileDialog
            {
                Filter = "Файлы БухУчёт (*.buh)|*.buh|JSON-файлы (*.json)|*.json|Все файлы (*.*)|*.*",
                Title  = "Открыть файл данных"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var save = _dataService.Load(dlg.FileName);
                _journal        = new ObservableCollection<JournalEntry>(save.Journal);
                _counterparties = new ObservableCollection<Counterparty>(save.Counterparties);
                _accounts       = new ObservableCollection<AccountPlan>(save.Accounts);
                _nextId = _journal.Count > 0 ? _journal.Max(j => j.Id) + 1 : 1;
                _currentFilePath = dlg.FileName;
                _isDirty = false;
                BindGrids();
                UpdateStatusBar();
                ShowStatus($"Файл открыт: {System.IO.Path.GetFileName(dlg.FileName)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка открытия файла:\n{ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_currentFilePath == null) { BtnSaveAs_Click(sender, e); return; }
            SaveToPath(_currentFilePath);
        }

        private void BtnSaveAs_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter           = "Файлы БухУчёт (*.buh)|*.buh|JSON-файлы (*.json)|*.json",
                Title            = "Сохранить файл данных",
                DefaultExt       = "buh",
                FileName         = "данные_бухучёт"
            };
            if (dlg.ShowDialog() != true) return;
            SaveToPath(dlg.FileName);
        }

        private void SaveToPath(string path)
        {
            try
            {
                var save = new SaveFile
                {
                    Journal        = _journal.ToList(),
                    Counterparties = _counterparties.ToList(),
                    Accounts       = _accounts.ToList()
                };
                _dataService.Save(path, save);
                _currentFilePath = path;
                _isDirty = false;
                UpdateStatusBar();
                ShowStatus($"Сохранено: {System.IO.Path.GetFileName(path)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения:\n{ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─── Вспомогательные ─────────────────────────────────────────────
        private void MarkDirty()
        {
            _isDirty = true;
            UpdateStatusBar();
        }

        private void UpdateStatusBar()
        {
            TxtFileName.Text = _currentFilePath != null
                ? System.IO.Path.GetFileName(_currentFilePath)
                : "Без имени (не сохранено)";

            TxtSavedAt.Text = _isDirty ? "● Есть несохранённые изменения" : "Сохранено";
        }

        private void UpdateJournalCount()
        {
            int total    = _journal.Count;
            decimal sum  = _journal.Sum(e => e.Amount);
            TxtJournalCount.Text = $"{total} записей  |  Итого: {sum:N2} руб.";
        }

        private void ShowStatus(string message, bool isError = false)
        {
            TxtStatus.Text = isError ? $"⚠ {message}" : $"✓ {message}";
            TxtStatus.Foreground = isError
                ? System.Windows.Media.Brushes.LightSalmon
                : System.Windows.Media.Brushes.LightGreen;
        }

        private bool ConfirmDiscard()
        {
            var r = MessageBox.Show(
                "Есть несохранённые изменения. Продолжить без сохранения?",
                "Несохранённые изменения",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            return r == MessageBoxResult.Yes;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_isDirty)
            {
                var r = MessageBox.Show(
                    "Сохранить изменения перед выходом?",
                    "Выход",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (r == MessageBoxResult.Cancel)      { e.Cancel = true; return; }
                if (r == MessageBoxResult.Yes)         BtnSave_Click(this, new RoutedEventArgs());
            }
            base.OnClosing(e);
        }
    }
}
