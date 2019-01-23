using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
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
        private MediaPlayer ring;

        private LifeCycleEvents lce;
        private MmiCommunication mmic;

        public SpeechMod()
        {
            string sound_path = System.IO.Directory.GetCurrentDirectory()+ @"\msg_sound.wav";

            ring = new MediaPlayer();
            ring.Open(new Uri(sound_path));
            ring.Volume = 0.10;

            //init LifeCycleEvents..
            lce = new LifeCycleEvents("ASR", "FUSION", "speech-1", "acoustic", "command"); // LifeCycleEvents(string source, string target, string id, string medium, string mode)
            //mmic = new MmiCommunication("localhost",9876,"User1", "ASR");  //PORT TO FUSION - uncomment this line to work with fusion later
            mmic = new MmiCommunication("localhost", 8000, "User1", "ASR"); // MmiCommunication(string IMhost, int portIM, string UserOD, string thisModalityName)

            mmic.Send(lce.NewContextRequest());


            lena = new Tts();
            //lena.Speak("Bom dia. Eu sou a Lena.");
            //iniciate connection to socket

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


                    //Send data to server
                    if (!msg.Equals("")){
                        Console.WriteLine("Sending: "+msg);

                        var exNot = lce.ExtensionNotification(e.Result.Audio.StartTime + "", e.Result.Audio.StartTime.Add(e.Result.Audio.Duration) + "", e.Result.Confidence, msg);
                        mmic.Send(exNot);
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
            

            foreach (string t in tags)
            {
                if (messageNeedConfirmation(t))
                {
                    msgToSend = makeMSG(tags);
                    waitingConfirmation = true;
                    Console.WriteLine("\n\nWaiting confirmation: ");
                    
                }
                else if(t.Equals("YES"))
                {
                    Console.WriteLine("\n\nConfirmed: ");
                    waitingConfirmation = false;
                    string tmp = msgToSend;
                    msgToSend = null;
                    return tmp;
                }
                else if (t.Equals("NO"))
                {
                    Console.WriteLine("\n\nConfirmed: ");
                    waitingConfirmation = false;
                    msgToSend = null;
                    return "";
                }
            }

            if (waitingConfirmation)
            {
                return null;
            }
            else
            {
                msgToSend = null;
                return makeMSG(tags);
            }
        }

        private bool messageNeedConfirmation(string tag)
        {

            if (tag.Equals("EXIT"))
            {
                lena.Speak("Tem a certeza que quer desligar o VLC?");
                return true;
            }
            else if (tag.Equals("DELETE_FILE"))
            {
                lena.Speak("Tem a certeza que quer apagar o video?");
                return true;
            }
            else
                return false;
        }

      
    }
}
