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
        private Tts lena;

        public SpeechMod()
        {
            lena = new Tts();
            //lena.Speak("Bom dia. Eu sou a Lena.");
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

        private string[] getTags(SemanticValue s)
        {
            List<string> tags = new List<string>();

            foreach (var result in s)
            {
                string value = (string)result.Value.Value;

                tags.Add(value);
            }

            return tags.ToArray();
        }

        private void Sre_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {


            onRecognized(new SpeechEventArg(){Text = e.Result.Text, Confidence = e.Result.Confidence, Final = true});

            if (e.Result.Confidence >= 0.6)
            {
                string[] tags = getTags(e.Result.Semantics);
                string msg = needsConfirmation(tags);

                if (msg != null)
                {
                    Console.WriteLine("\n\nConfirmed!!");

                    //Send data to server
                    if (!msg.Equals("")){
                        Console.WriteLine("Sending: "+msg);
                        trySend_msg(msg);
                    }
                }
            }

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


        private bool waitingConfirmation = false;
        private string msgToSend = null;

        private string needsConfirmation(string[] tags)
        {
            Console.WriteLine("\n\nConfirmation: ");

            foreach (string t in tags)
            {
                if (t.Equals("EXIT")){
                    msgToSend = makeMSG(tags);
                    waitingConfirmation = true;
                    return null;
                }
                else if(t.Equals("YES"))
                {
                    waitingConfirmation = false;
                    return msgToSend;
                }
                else if (t.Equals("NO"))
                {
                    waitingConfirmation = false;
                    return "";
                }
            }

            if (waitingConfirmation)
            {
                return null;
            }
            else
            {
                return makeMSG(tags);
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

                lena.Speak("Conexão com o servidor bem sucedida.");
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
                    lena.Speak("A mensagem n±ao foi enviada");
                }
                else
                {
                    int byteCount = Encoding.ASCII.GetByteCount(message);
                    byte[] sendData = new byte[byteCount];
                    sendData = Encoding.ASCII.GetBytes(message);

                    stream = client.GetStream(); //Opens up the network stream
                    stream.Write(sendData, 0, sendData.Length); //Transmits data onto the stream
                    result = true;
                    lena.Speak("Mensagem Enviada");
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
