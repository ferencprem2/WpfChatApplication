using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ChatApp
{
    public partial class MainWindow : Window
    {
        TcpClient client;
        NetworkStream stream;
        Thread receiveThread;
        bool isConnected = false;

        public MainWindow()
        {
            InitializeComponent();
            this.Closing += MainWindow_Closing;
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isConnected)
            {
                string name = NameTextBox.Text.Trim();
                string serverIP = ServerIPTextBox.Text.Trim();

                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(serverIP))
                {
                    MessageBox.Show("Please enter your name and server IP.");
                    return;
                }

                try
                {
                    client = new TcpClient();
                    client.Connect(serverIP, 1010);
                    stream = client.GetStream();

                    byte[] nameBuffer = Encoding.UTF8.GetBytes(name);
                    stream.Write(nameBuffer, 0, nameBuffer.Length);

                    receiveThread = new Thread(ReceiveMessages);
                    receiveThread.Start();

                    isConnected = true;
                    ConnectButton.Content = "Disconnect";
                    NameTextBox.IsEnabled = false;
                    ServerIPTextBox.IsEnabled = false;
                    MessageTextBox.Focus();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error connecting to server: " + ex.Message);
                }
            }
            else
            {
                Disconnect();
            }
        }

        private void ReceiveMessages()
        {
            byte[] buffer = new byte[1024];
            try
            {
                while (isConnected)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                        Dispatcher.Invoke(() => HandleReceivedMessage(message));
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessagesTextBox.AppendText("Disconnected from server.\n");
                    Disconnect();
                });
            }
        }

        private void HandleReceivedMessage(string message)
        {
            if (message.StartsWith("MSG:"))
            {
                string msgText = message.Substring(4);
                MessagesTextBox.AppendText(msgText + "\n");
            }
            else if (message.StartsWith("PVT:"))
            {
                string msgText = message.Substring(4);
                MessagesTextBox.AppendText(msgText + " (Private)\n");
            }
            else if (message.StartsWith("USERS:"))
            {
                string usersText = message.Substring(6);
                string[] users = usersText.Split(',');
                UsersListBox.ItemsSource = users;
            }
            else if (message.StartsWith("SYS:"))
            {
                string msgText = message.Substring(4);
                MessagesTextBox.AppendText("System: " + msgText + "\n");
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (isConnected && !string.IsNullOrEmpty(MessageTextBox.Text))
            {
                string messageText = MessageTextBox.Text.Trim();
                string selectedUser = UsersListBox.SelectedItem as string;

                string messageToSend = "";

                if (!string.IsNullOrEmpty(selectedUser) && selectedUser != NameTextBox.Text)
                {
                    messageToSend = $"PVT:{selectedUser}:{messageText}";
                }
                else
                {
                    messageToSend = $"MSG:{messageText}";
                }

                byte[] buffer = Encoding.UTF8.GetBytes(messageToSend);
                try
                {
                    stream.Write(buffer, 0, buffer.Length);

                    if (!messageToSend.StartsWith("PVT:"))
                    {
                        MessagesTextBox.AppendText($"[You]: {messageText}\n");
                    }
                    else
                    {
                        MessagesTextBox.AppendText($"[Private to {selectedUser}]: {messageText}\n");
                    }

                    MessageTextBox.Clear();
                }
                catch (Exception ex)
                {
                    MessagesTextBox.AppendText("Error sending message: " + ex.Message + "\n");
                }
            }
        }

        private void Disconnect()
        {
            isConnected = false;
            if (stream != null)
            {
                stream.Close();
                stream = null;
            }
            if (client != null)
            {
                client.Close();
                client = null;
            }
            if (receiveThread != null && receiveThread.IsAlive)
            {
                receiveThread.Abort();
                receiveThread = null;
            }

            ConnectButton.Content = "Connect";
            NameTextBox.IsEnabled = true;
            ServerIPTextBox.IsEnabled = true;
            UsersListBox.ItemsSource = null;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            Disconnect();
        }
    }
}
