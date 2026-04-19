using System.Windows;

namespace BuhUchet
{
    public partial class LoginWindow : Window
    {
        private readonly UserService _userService = new();
        private bool _isRegisterMode = false;

        public LoginWindow()
        {
            InitializeComponent();
        }

        // ── Переключение режима Вход / Регистрация ──
        private void Switch_Click(object sender, RoutedEventArgs e)
        {
            _isRegisterMode = !_isRegisterMode;
            ErrorText.Text = "";

            if (_isRegisterMode)
            {
                TitleText.Text = "Регистрация";
                SubtitleText.Text = "Создайте новый аккаунт";
                ActionButton.Content = "Зарегистрироваться";
                SwitchLabel.Text = "Уже есть аккаунт? ";
                SwitchButton.Content = "Войти";
            }
            else
            {
                TitleText.Text = "Вход в систему";
                SubtitleText.Text = "Введите свои данные для входа";
                ActionButton.Content = "Войти";
                SwitchLabel.Text = "Нет аккаунта? ";
                SwitchButton.Content = "Зарегистрироваться";
            }
        }

        // ── Кнопка «Войти» или «Зарегистрироваться» ──
        private void Action_Click(object sender, RoutedEventArgs e)
        {
            var username = UsernameBox.Text.Trim();
            var password = PasswordBox.Password;
            ErrorText.Text = "";

            if (_isRegisterMode)
            {
                var (success, error) = _userService.Register(username, password);
                if (!success) { ErrorText.Text = error; return; }

                // После успешной регистрации — сразу входим
                MessageBox.Show("Аккаунт создан! Теперь вы можете войти.",
                    "Регистрация", MessageBoxButton.OK, MessageBoxImage.Information);
                Switch_Click(sender, e); // переключаемся на вход
            }
            else
            {
                var (success, error) = _userService.Login(username, password);
                if (!success) { ErrorText.Text = error; return; }

                DialogResult = true;
                Close();
            }
        }
    }
}