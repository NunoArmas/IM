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

        private TcpClient client;
        private NetworkStream stream;
        private Socket client_sock=null;

        public SpeechMod()
        {
            //iniciate connection to socket
            connectSocket();

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
                Console.WriteLine(json);

                //Send data to server
                Console.WriteLine(trySend_msg(json));
            }

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
                }
             

                int byteCount = Encoding.ASCII.GetByteCount(message);
                byte[] sendData = new byte[byteCount];
                sendData = Encoding.ASCII.GetBytes(message);

                stream = client.GetStream(); //Opens up the network stream
                stream.Write(sendData, 0, sendData.Length); //Transmits data onto the stream
                result = true;

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
