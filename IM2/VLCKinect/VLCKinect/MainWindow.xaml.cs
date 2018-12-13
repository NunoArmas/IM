using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

using Microsoft.Kinect.VisualGestureBuilder;
using Microsoft.Kinect;
using System.ComponentModel;
using System.Timers;

using System.Net;
using System.Net.Sockets;

namespace VLCKinect
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : Window
    {
        int TIME = 1000;

        private KinectSensor kinect = null;
        private String statusText = null;
        Body[] bodies;
        BodyFrameReader bodyReader;
        VisualGestureBuilderFrameSource gestureSource;
        VisualGestureBuilderFrameReader gestureReader;

        Gesture gFullScreenOff;
        Gesture gFullScreenOn;

        VisualGestureBuilderDatabase db;


        private TcpClient client;
        private NetworkStream stream;
        private Socket client_sock = null;


        System.Timers.Timer timer,connectSock;
        Boolean canReadDiscrete = true;
        int counter = 1;

        public MainWindow()
        {

            //this.kinect.IsAvailableChanged += this.Sensor_IsAvailableChanged;
            OnOpenSensor();
            InitializeComponent();
            SetTimer();
            OnLoadGestureFromDBd();
            OnOpenReaders();

            Closed += OnWindowClosing;

        }

        public void OnWindowClosing(object sender, EventArgs e)
        {
            // Handle closing logic, set e.Cancel as needed
            OnCloseReaders();
            OnCloseSensor();
            
        }

        void OnLoadGestureFromDBd()//object sender, RoutedEventArgs e)
        {
            //assuming the file exists
            db = new VisualGestureBuilderDatabase(@"IM.gbd");

            //Loads the Gesture into the variable gTest, I think
            //TODO: VERIFY THIS
            Console.WriteLine(db.AvailableGestures.ToArray().ToString());
            this.gFullScreenOff = db.AvailableGestures.Where(g => g.Name == "fullscreenOFF").Single();
            this.gFullScreenOn = db.AvailableGestures.Where(g => g.Name == "fullscreenON").Single();

        }

        //Opens the kinect sensor
        void OnOpenSensor ()//object sender, RoutedEventArgs e)
        {
            this.kinect = KinectSensor.GetDefault();
            this.kinect.Open();
        }

        //Close the kinect Sensor
        void OnCloseSensor()//object sender, RoutedEventArgs e)
        {
            this.kinect.Close();
            this.kinect = null;
        }

        void OnOpenReaders()//object sender, RoutedEventArgs e)
        {
            this.OnOpenBodyReaders();
            this.OpenGesureReader();
        }

        private void OnOpenBodyReaders()
        {
            if (this.bodies == null)
            {
                this.bodies = new Body[this.kinect.BodyFrameSource.BodyCount];
            }
            this.bodyReader = this.kinect.BodyFrameSource.OpenReader();
            this.bodyReader.FrameArrived += OnBodyFrameArrived;
        }

        private void OpenGesureReader()
        {
            this.gestureSource = new VisualGestureBuilderFrameSource(this.kinect, 0);

            Console.WriteLine(gestureSource.IsActive);
            //add 
            this.gestureSource.AddGesture(this.gFullScreenOff);
            this.gestureSource.AddGesture(this.gFullScreenOn);

            this.gestureSource.TrackingIdLost += OnTrackingIdLost;

            this.gestureReader = this.gestureSource.OpenReader();
            this.gestureReader.IsPaused = true;
            this.gestureReader.FrameArrived += OnGestureFrameArrived;
        }

        void OnCloseReaders()//object sender, RoutedEventArgs e)
        {
            if (this.gestureReader != null)
            {
                this.gestureReader.FrameArrived -= this.OnGestureFrameArrived;
                this.gestureReader.Dispose();
                this.gestureReader = null;
            }
            if (this.gestureSource != null)
            {
                this.gestureSource.TrackingIdLost -= this.OnTrackingIdLost;
                this.gestureSource.Dispose();
            }
            this.bodyReader.Dispose();
            this.bodyReader = null;
        }

        void OnTrackingIdLost (object sender, TrackingIdLostEventArgs e)
        {
            this.gestureReader.IsPaused = true;
            //this.moveableStop.Offset = 0.0f;
            //this.txtProgress.Text = string.Empty;
        }
        
        void OnBodyFrameArrived (object sender, BodyFrameArrivedEventArgs e)
        {
            using (var frame = e.FrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    frame.GetAndRefreshBodyData(this.bodies);

                    var trackedBody = this.bodies.Where(b => b.IsTracked).FirstOrDefault();
                    
                    if (trackedBody != null)
                    {
                        if (this.gestureReader.IsPaused)
                        {
                            this.gestureSource.TrackingId = trackedBody.TrackingId;
                            this.gestureReader.IsPaused = false;
                        }
                    }
                    else
                    {
                        this.OnTrackingIdLost(null, null);
                    }
                }
            }
        }

        void OnGestureFrameArrived (object sender, VisualGestureBuilderFrameArrivedEventArgs e)
        {
            using (var frame = e.FrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    var discretResult = frame.DiscreteGestureResults;
                    
                    

                    if (discretResult != null && canReadDiscrete )
                    {
                        
                        foreach(var gesture in this.gestureSource.Gestures)
                        {
                            if(gesture.GestureType == GestureType.Discrete)
                            {
                                DiscreteGestureResult result = null;
                                discretResult.TryGetValue(gesture, out result);
                                
                                if(result != null)
                                {
                                    if(gesture.Name.Equals("fullscreenOFF") && (result.Confidence >= 0.7))
                                    {
                                        Console.WriteLine("fullscreenOFF " + result.Detected + " Gesture Confidence:" + result.Confidence);
                                        counter++;
                                        canReadDiscrete = false;
                                        timer.Start();
                                        
                                    }
                                    if(gesture.Name.Equals("fullscreenON") && (result.Confidence >= 0.7))
                                    {
                                        Console.WriteLine("FullscreenON " + result.Detected + " Gesture Confidence:" + result.Confidence);
                                        counter++;
                                        canReadDiscrete = false;
                                        timer.Start();
                                    }
                                }
                            }
                        }
                    }

                }
            }
        }


        //timer event////////////////////////////////////////////////////////////////////////////////////
        void SetTimer()
        {
            timer = new System.Timers.Timer(TIME);

            timer.Elapsed += OnTimedEvent;
            timer.AutoReset = false;
            timer.Enabled = true;
        }

        void SetSocketTimer()
        {
            connectSock = new System.Timers.Timer(1000);

            connectSock.Elapsed += OnConnectEvent;
            connectSock.AutoReset = true;
            connectSock.Enabled = true;
        }

        private void OnTimedEvent(object sender, ElapsedEventArgs e)
        {
            canReadDiscrete = true;
            timer.Stop();
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

            timer.Start();
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
