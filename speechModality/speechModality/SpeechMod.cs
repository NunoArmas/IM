using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using mmisharp;
using Microsoft.Speech.Recognition;

using System.Net;
using System.Net.Sockets;



namespace speechModality
{
    public class SpeechMod
    {
        private SpeechRecognitionEngine sre;
        private Grammar gr;
        public event EventHandler<SpeechEventArg> Recognized;
        protected virtual void onRecognized(SpeechEventArg msg)
        {
            EventHandler<SpeechEventArg> handler = Recognized;
            if (handler != null)
            {
                handler(this, msg);
            }
        }

        private LifeCycleEvents lce;
        private MmiCommunication mmic;

        private TcpClient client;
        private NetworkStream stream; 

        public SpeechMod()
        {

            //init LifeCycleEvents..
            //lce = new LifeCycleEvents("ASR", "FUSION","speech-1", "acoustic", "command"); // LifeCycleEvents(string source, string target, string id, string medium, string mode)
            ////mmic = new MmiCommunication("localhost",9876,"User1", "ASR");  //PORT TO FUSION - uncomment this line to work with fusion later
            //mmic = new MmiCommunication("localhost", 8000, "User1", "ASR"); // MmiCommunication(string IMhost, int portIM, string UserOD, string thisModalityName)

            //mmic.Send(lce.NewContextRequest());

            //load pt recognizer
            sre = new SpeechRecognitionEngine(new System.Globalization.CultureInfo("pt-PT"));
            gr = new Grammar(Environment.CurrentDirectory + "\\ptG.grxml", "rootRule");
            sre.LoadGrammar(gr);

            
            sre.SetInputToDefaultAudioDevice();
            sre.RecognizeAsync(RecognizeMode.Multiple);
            sre.SpeechRecognized += Sre_SpeechRecognized;
            sre.SpeechHypothesized += Sre_SpeechHypothesized;

        }

        private void Sre_SpeechHypothesized(object sender, SpeechHypothesizedEventArgs e)
        {
            onRecognized(new SpeechEventArg() { Text = e.Result.Text, Confidence = e.Result.Confidence, Final = false });
        }

        private void Sre_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {


            onRecognized(new SpeechEventArg(){Text = e.Result.Text, Confidence = e.Result.Confidence, Final = true});

            Console.Write(e.Result.Confidence);

            if (e.Result.Confidence >= 0.6)
            {
                //SEND
                // IMPORTANT TO KEEP THE FORMAT {"recognized":["SHAPE","COLOR"]}
                string json = "{ \"recognized\": [";
                foreach (var resultSemantic in e.Result.Semantics)
                {
                    json += "\"" + resultSemantic.Value.Value + "\", ";
                }
                json = json.Substring(0, json.Length - 2);
                json += "] }";

                //Send data to server
                trySend_msg(json);
            }

            //var exNot = lce.ExtensionNotification(e.Result.Audio.StartTime+"", e.Result.Audio.StartTime.Add(e.Result.Audio.Duration)+"",e.Result.Confidence, json);
            //mmic.Send(exNot);
        }

        private bool trySend_msg(string message)
        {
            bool result = false;
            try
            {
                client = new TcpClient("localhost", 8081);

                int byteCount = Encoding.ASCII.GetByteCount(message);
                byte[] sendData = new byte[byteCount];
                sendData = Encoding.ASCII.GetBytes(message);

                stream = client.GetStream(); //Opens up the network stream
                stream.Write(sendData, 0, sendData.Length); //Transmits data onto the stream
                stream.Close();
                client.Close();
                result = true;
            }
            catch (System.NullReferenceException) //Error if socket not open
            {
                //Adds debug to list box and shows message box
                Console.WriteLine("Connection not installised");
                Console.WriteLine("Failed to send data");
            }catch (System.Net.Sockets.SocketException)
            {
                Console.WriteLine("Connection Failed");
                Console.WriteLine("Connection failed");
            }
            return result;
        }
    }
}
