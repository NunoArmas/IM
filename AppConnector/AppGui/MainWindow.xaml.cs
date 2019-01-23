using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Linq;
using mmisharp;
using Newtonsoft.Json;

using System.Text;

using System.Timers;

using System.Net;
using System.Net.Sockets;

namespace AppGui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MmiCommunication mmiC;

        private TcpClient client;
        private NetworkStream stream;
        private Socket client_sock = null;

        Timer timer, connectSock;
        int TIME = 500;


        public MainWindow()
        {
            InitializeComponent();

           

            mmiC = new MmiCommunication("localhost",8000, "User1", "GUI");
            mmiC.Message += MmiC_Message;
            mmiC.Start();

            SetSocketTimer();

        }

        private void MmiC_Message(object sender, MmiEventArgs e)
        {
            Console.WriteLine(e.Message);
            var doc = XDocument.Parse(e.Message);
            var com = doc.Descendants("command").FirstOrDefault().Value;
            dynamic json = JsonConvert.DeserializeObject(com);

            Console.WriteLine("Hello");
            Console.WriteLine(json);

            //Shape _s = null;
            //switch ((string)json.recognized[0].ToString())
            //{
            //    case "SQUARE": _s = rectangle;
            //        break;
            //    case "CIRCLE": _s = circle;
            //        break;
            //    case "TRIANGLE": _s = triangle;
            //        break;
            //}

            //App.Current.Dispatcher.Invoke(() =>
            //{
            //    switch ((string)json.recognized[1].ToString())
            //    {
            //        case "GREEN":
            //            _s.Fill = Brushes.Green;
            //            break;
            //        case "BLUE":
            //            _s.Fill = Brushes.Blue;
            //            break;
            //        case "RED":
            //            _s.Fill = Brushes.Red;
            //            break;

            //        case "YELLOW":
            //            _s.Fill = Brushes.Yellow;
            //            break;
            //    }
            //});

        }



        private void OnConnectEvent(object sender, ElapsedEventArgs e)
        {
            try
            {

                if (!checkSocket())
                {
                    connectSocket();
                }
            }
            catch
            {
                Console.WriteLine("Failed to connect");
            }

            connectSock.Start();
        }

        void SetSocketTimer()
        {
            connectSock = new System.Timers.Timer(1000);

            connectSock.Elapsed += OnConnectEvent;
            connectSock.AutoReset = true;
            connectSock.Enabled = true;
        }

        private string makeMSG(string[] tags)
        {
            string json = "{ \"recognized\": [";
            foreach (string t in tags)
            {
                json += "\"" + t + "\", ";

            }
            json = json.Substring(0, json.Length - 2);
            json += "] }";

            return json;
        }

        private bool checkSocket()
        {
            bool result = true;

            return client != null && client_sock.Connected;

        }

        private void connectSocket()
        {
            try
            {
                client = new TcpClient("localhost", 8081);
                client_sock = client.Client;
            }
            catch
            {
                Console.WriteLine("Connection Failed");
                if (client != null)
                {
                    client.Close();
                }
                client = null;
            }
        }

        private bool trySend_msg(string message)
        {
            bool result = false;
            try
            {

                if (!checkSocket())
                {
                    connectSocket();
                    result = false;
                }
                else
                {
                    int byteCount = Encoding.ASCII.GetByteCount(message);
                    byte[] sendData = new byte[byteCount];
                    sendData = Encoding.ASCII.GetBytes(message);

                    stream = client.GetStream(); //Opens up the network stream
                    stream.Write(sendData, 0, sendData.Length); //Transmits data onto the stream

                    result = true;
                }
            }
            catch
            {
                Console.WriteLine("Connection not installised");
                Console.WriteLine("Failed to send data");
                result = false;
            }

            return result;
        }
    }
}