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

using mmisharp;


namespace VLCKinect
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : Window
    {
        int TIME = 750;
        double CONFIDENCE = 0.6;

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
        private MediaPlayer ring;


        private LifeCycleEvents lce;
        private MmiCommunication mmic;

        public MainWindow()
        {
            

            //init LifeCycleEvents..
            lce = new LifeCycleEvents("KINECT", "FUSION", "gesture-1", "gesture", "command"); // LifeCycleEvents(string source, string target, string id, string medium, string mode)
            //mmic = new MmiCommunication("localhost",9876,"User1", "ASR");  //PORT TO FUSION - uncomment this line to work with fusion later
            mmic = new MmiCommunication("localhost", 8000, "User2", "ASR"); // MmiCommunication(string IMhost, int portIM, string UserOD, string thisModalityName)

            mmic.Send(lce.NewContextRequest());

            //this.kinect.IsAvailableChanged += this.Sensor_IsAvailableChanged;
            OnOpenSensor();
            InitializeComponent();
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
            db = new VisualGestureBuilderDatabase(@"IM2.gbd");

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
            this.gestureSource.AddGesture(db.AvailableGestures.Where(g => g.Name == "backward").Single());
            this.gestureSource.AddGesture(db.AvailableGestures.Where(g => g.Name == "forward").Single());
            this.gestureSource.AddGesture(db.AvailableGestures.Where(g => g.Name == "fullscreenOFF").Single());
            this.gestureSource.AddGesture(db.AvailableGestures.Where(g => g.Name == "fullscreenON").Single());
            this.gestureSource.AddGesture(db.AvailableGestures.Where(g => g.Name == "Pause").Single());
            this.gestureSource.AddGesture(db.AvailableGestures.Where(g => g.Name == "play").Single());
            this.gestureSource.AddGesture(db.AvailableGestures.Where(g => g.Name == "stop").Single());
            this.gestureSource.AddGesture(db.AvailableGestures.Where(g => g.Name == "volumeDOWN").Single());
            this.gestureSource.AddGesture(db.AvailableGestures.Where(g => g.Name == "volumeUP").Single());

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
            string message = "";
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
                                    int confidence =(int) result.Confidence;
                                    if (gesture.Name.Equals("backward") && (result.Confidence >= CONFIDENCE))
                                    {
                                        Console.WriteLine("backward " + result.Detected + " Gesture Confidence:" + result.Confidence);
                                        counter++;
                                        canReadDiscrete = false;
                                        timer.Start();

                                        message = makeMSG(new string[]{"BACKWARD"});
                                        trySend_msg(message, confidence);
                                    }

                                    if (gesture.Name.Equals("forward") && (result.Confidence >= CONFIDENCE))
                                    {
                                        Console.WriteLine("forward " + result.Detected + " Gesture Confidence:" + result.Confidence);
                                        counter++;
                                        canReadDiscrete = false;
                                        timer.Start();

                                        message = makeMSG(new string[] { "FORWARD" });
                                        trySend_msg(message, confidence);

                                    }

                                    if (gesture.Name.Equals("fullscreenOFF") && (result.Confidence >= CONFIDENCE))
                                    {
                                        Console.WriteLine("fullscreenOFF " + result.Detected + " Gesture Confidence:" + result.Confidence);
                                        counter++;
                                        canReadDiscrete = false;
                                        timer.Start();

                                        message = makeMSG(new string[] { "FULLSCREEN_MIN" });
                                        trySend_msg(message, confidence);
                                        
                                    }

                                    if (gesture.Name.Equals("fullscreenON") && (result.Confidence >= CONFIDENCE))
                                    {
                                        Console.WriteLine("fullscreenON " + result.Detected + " Gesture Confidence:" + result.Confidence);
                                        counter++;
                                        canReadDiscrete = false;
                                        timer.Start();

                                        message = makeMSG(new string[] { "FULLSCREEN_MAX" });
                                        trySend_msg(message, confidence);

                                    }

                                    if (gesture.Name.Equals("Pause") && (result.Confidence >= CONFIDENCE))
                                    {
                                        Console.WriteLine("Pause " + result.Detected + " Gesture Confidence:" + result.Confidence);
                                        counter++;
                                        canReadDiscrete = false;
                                        timer.Start();

                                        message = makeMSG(new string[] { "PAUSE" });
                                        trySend_msg(message, confidence);

                                    }

                                    if (gesture.Name.Equals("play") && (result.Confidence >= CONFIDENCE))
                                    {
                                        Console.WriteLine("play " + result.Detected + " Gesture Confidence:" + result.Confidence);
                                        counter++;
                                        canReadDiscrete = false;
                                        timer.Start();

                                        message = makeMSG(new string[] { "PLAY" });
                                        trySend_msg(message, confidence);

                                    }

                                    if (gesture.Name.Equals("stop") && (result.Confidence >= CONFIDENCE))
                                    {
                                        Console.WriteLine("stop " + result.Detected + " Gesture Confidence:" + result.Confidence);
                                        counter++;
                                        canReadDiscrete = false;
                                        timer.Start();

                                        message = makeMSG(new string[] { "STOP" });
                                        trySend_msg(message, confidence);

                                    }

                                    if (gesture.Name.Equals("volumeDOWN") && (result.Confidence >= CONFIDENCE))
                                    {
                                        Console.WriteLine("volumeDOWN " + result.Detected + " Gesture Confidence:" + result.Confidence);
                                        counter++;
                                        canReadDiscrete = false;
                                        timer.Start();

                                        message = makeMSG(new string[] { "VOLUME_DOWN" });
                                        trySend_msg(message, confidence);

                                    }

                                    if (gesture.Name.Equals("volumeUP") && (result.Confidence >= CONFIDENCE))
                                    {
                                        Console.WriteLine("volumeUP " + result.Detected + " Gesture Confidence:" + result.Confidence);
                                        counter++;
                                        canReadDiscrete = false;
                                        timer.Start();

                                        message = makeMSG(new string[] { "VOLUME_UP" });
                                        trySend_msg(message, confidence);

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
            connectSock.Start();
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

        private void trySend_msg(string msg, int confidence)
        {
            var exNot = lce.ExtensionNotification(0 + "", 0 + "", confidence , msg);
            mmic.Send(exNot);
        }
    }
}
