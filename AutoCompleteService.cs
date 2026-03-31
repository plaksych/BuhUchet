using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BuhUchet
{
    /// <summary>
    /// Сервис автодополнения: при нажатии Tab предлагает варианты из словаря.
    /// Работает с любым TextBox — просто вызовите Attach().
    /// </summary>
    public class AutoCompleteService
    {
        private readonly Dictionary<TextBox, AutoCompleteState> _states = new();

        // Подключить TextBox к сервису
        public void Attach(TextBox textBox, Func<IEnumerable<string>> sourceProvider, Action<string>? onSelected = null)
        {
            var state = new AutoCompleteState(sourceProvider, onSelected);
            _states[textBox] = state;

            textBox.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Tab)
                {
                    e.Handled = HandleTab(textBox, state);
                }
                else if (e.Key == Key.Escape)
                {
                    HidePopup(state);
                }
                else if (e.Key == Key.Down && state.Popup?.IsOpen == true)
                {
                    if (state.ListBox != null && state.ListBox.Items.Count > 0)
                    {
                        state.ListBox.Focus();
                        state.ListBox.SelectedIndex = 0;
                    }
                    e.Handled = true;
                }
            };

            textBox.TextChanged += (s, e) =>
            {
                // Автопоиск при вводе 2+ символов
                if (textBox.Text.Length >= 2)
                    ShowSuggestions(textBox, state);
                else
                    HidePopup(state);
            };

            textBox.LostFocus += (s, e) =>
            {
                // Скрыть через небольшую задержку, чтобы клик по списку успел сработать
                textBox.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                    () => HidePopup(state));
            };
        }

        // Обработка Tab
        private bool HandleTab(TextBox textBox, AutoCompleteState state)
        {
            string prefix = textBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(prefix)) return false;

            var matches = GetMatches(state, prefix).ToList();

            if (matches.Count == 1)
            {
                // Единственный вариант — вставить сразу
                textBox.Text = matches[0];
                textBox.CaretIndex = textBox.Text.Length;
                HidePopup(state);
                state.OnSelected?.Invoke(matches[0]);
                return true;
            }
            else if (matches.Count > 1)
            {
                ShowSuggestions(textBox, state);
                return true;
            }

            return false;
        }

        // Показать popup со списком вариантов
        private void ShowSuggestions(TextBox textBox, AutoCompleteState state)
        {
            var matches = GetMatches(state, textBox.Text.Trim()).ToList();
            if (matches.Count == 0) { HidePopup(state); return; }

            if (state.Popup == null)
                BuildPopup(textBox, state);

            state.ListBox!.ItemsSource = matches;
            state.Popup!.IsOpen = true;
        }

        private void HidePopup(AutoCompleteState state)
        {
            if (state.Popup != null)
                state.Popup.IsOpen = false;
        }

        // Создать Popup + ListBox
        private void BuildPopup(TextBox textBox, AutoCompleteState state)
        {
            var listBox = new ListBox
            {
                MaxHeight = 160,
                FontSize = 13,
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = System.Windows.Media.Brushes.LightGray,
                BorderThickness = new Thickness(1)
            };

            listBox.MouseLeftButtonUp += (s, e) =>
            {
                if (listBox.SelectedItem is string selected)
                {
                    textBox.Text = selected;
                    textBox.CaretIndex = textBox.Text.Length;
                    HidePopup(state);
                    state.OnSelected?.Invoke(selected);
                    textBox.Focus();
                }
            };

            listBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter && listBox.SelectedItem is string selected)
                {
                    textBox.Text = selected;
                    textBox.CaretIndex = textBox.Text.Length;
                    HidePopup(state);
                    state.OnSelected?.Invoke(selected);
                    textBox.Focus();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    HidePopup(state);
                    textBox.Focus();
                    e.Handled = true;
                }
            };

            var popup = new System.Windows.Controls.Primitives.Popup
            {
                PlacementTarget = textBox,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true,
                Child = listBox,
                Width = Math.Max(textBox.ActualWidth, 200)
            };

            state.Popup = popup;
            state.ListBox = listBox;
        }

        private IEnumerable<string> GetMatches(AutoCompleteState state, string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix)) return Enumerable.Empty<string>();
            return state.SourceProvider()
                .Where(s => s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s)
                .Take(10);
        }
    }

    // Состояние для каждого TextBox
    internal class AutoCompleteState
    {
        public Func<IEnumerable<string>> SourceProvider { get; }
        public Action<string>? OnSelected { get; }
        public System.Windows.Controls.Primitives.Popup? Popup { get; set; }
        public ListBox? ListBox { get; set; }

        public AutoCompleteState(Func<IEnumerable<string>> sourceProvider, Action<string>? onSelected)
        {
            SourceProvider = sourceProvider;
            OnSelected = onSelected;
        }
    }
}
