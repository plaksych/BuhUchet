using System.Windows;

namespace BuhUchet
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Создаём главное окно заранее, но не показываем
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;

            var loginWindow = new LoginWindow();
            bool? result = loginWindow.ShowDialog();

            if (result == true)
            {
                mainWindow.Show();
            }
            else
            {
                Shutdown();
            }
        }
    }
}