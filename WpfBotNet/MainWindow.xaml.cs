
using System;
using System.Windows;

namespace WpfBotNet
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window

    {
        TelegramMessageClient client;
        public MainWindow()
        {
            InitializeComponent();

            client = new TelegramMessageClient(this);

            logList.ItemsSource = TelegramMessageClient.BotMessageLog;
        }

        private void btnMsgSendClick(object sender, RoutedEventArgs e)
        {
            client.SendMessage(txtMsgSend.Text, TargetSend.Text);
            txtMsgSend.Text = "";
        }

        private void btnJSONLoad_Click(object sender, RoutedEventArgs e)
        {
            client.LoadJSON();
        }

    }
}
