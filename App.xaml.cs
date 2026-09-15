using System;
using System.Windows;

namespace StandalonePicturator;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try 
        {
            // Bắt đầu vẽ giao diện lên màn hình
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            // Bắt bằng được nguyên nhân tàng hình đánh sập app
            MessageBox.Show(ex.ToString(), "Lỗi khởi động giao diện XAML", MessageBoxButton.OK, MessageBoxImage.Error);
            Current.Shutdown();
        }
    }
}