using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Xml;
using System.Net.Sockets;
using System.Net;
using System.Text;

namespace lucidcode.LucidScribe.Plugin.Hypnodyne.ZMax
{
    public class ZMaxChangedEventArgs : EventArgs
    {
        public ZMaxChangedEventArgs(int EEG)
        {
            this.EEG = EEG;
        }

        public int EEG { get; set; }
    }

    public static class Device
    {
        private static Thread serverThread;
        private static Boolean running = true;
        private static int Port;

        public static EventHandler<ZMaxChangedEventArgs> ZMaxEEGRChanged;
        public static EventHandler<ZMaxChangedEventArgs> ZMaxEEGLChanged;

        private static bool m_boolInitialized;
        private static bool m_boolInitError;
        public static String Algorithm;
        public static int Threshold;

        private static double m_dblX = 500;
        private static double m_dblY = 500;
        private static double m_dblZ = 500;
        private static double m_dblRaw = 500;

        private static bool ClearDisplay;
        private static bool ClearHighscore;
        private static double DisplayValue = 500;
        private static double HighscoreValue = 500;
        private static PortForm formPort = new PortForm();

        public static Boolean TCMP = false;

        public static Boolean Initialize()
        {
            try
            {
                if (!m_boolInitialized && !m_boolInitError)
                {                    
                    if (formPort.ShowDialog() == DialogResult.OK)
                    {
                        try
                        {
                            Algorithm = formPort.Algorithm;
                            Threshold = formPort.Threshold;
                            TCMP = formPort.TCMP;
                            Port = Convert.ToInt32(formPort.SelectedPort);
                            m_boolInitialized = true;
                            serverThread = new Thread(new ThreadStart(startListening));
                            serverThread.Start();

                            formPort.Show();
                        }
                        catch (Exception ex)
                        {
                            if (!m_boolInitError)
                            {
                                MessageBox.Show(ex.Message, "LucidScribe.InitializePlugin()", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                            m_boolInitError = true;
                        }
                    }
                    else
                    {
                        m_boolInitError = true;
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                m_boolInitError = true;
                MessageBox.Show(ex.Message, "LucidScribe.InitializePlugin()", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private static void startListening()
        {
            try
            {
                TcpClient tcpClient = new TcpClient("127.0.0.1", Port);
                
                NetworkStream stream = tcpClient.GetStream();
                byte[] request = System.Text.Encoding.UTF8.GetBytes("HELLO\r\n");
                stream.Write(request, 0, request.Length);
                stream.Flush();
                request = System.Text.Encoding.UTF8.GetBytes("IDLEMODE_SENDBYTES 1 3 900 0 00-26-0B-03-0E-37-98\r\n");
                stream.Write(request, 0, request.Length);
                stream.Flush();

                while (running)
                {
                    byte[] readBuffer = new byte[tcpClient.ReceiveBufferSize];
                    int bytesSent = 0;
                    using (var writer = new MemoryStream())
                    {
                        while (stream.DataAvailable)
                        {
                            int numberOfBytesRead = stream.Read(readBuffer, 0, readBuffer.Length);
                            if (numberOfBytesRead <= 0)
                            {
                                break;
                            }
                            bytesSent += numberOfBytesRead;
                            writer.Write(readBuffer, 0, numberOfBytesRead);
                        }
                        String data = Encoding.UTF8.GetString(writer.ToArray());
                        if (data != "")
                        {
                            formPort.UpdateData(data);
                            processData(data);
                        }
                    }

                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                m_boolInitError = true;
                running = false;
                MessageBox.Show(ex.Message, "LucidScribe.Listen()", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void processData(String data)
        {
            try
            {
                string[] stringSeparators = new string[] { "\r\n", "\r", "\n", "\n\r" };
                string[] lines = data.Split(stringSeparators, StringSplitOptions.None);
                for (int i = 0; i < lines.Length; i++)
                {
                    String line = lines[i];
                    if (line == "" || !line.StartsWith("D")) continue;

                    String dataline = line.Substring(1);
                    string[] parts = dataline.Split('.');
                    byte[] buf = HexToBytes(parts[1].Replace("-", ""));
                    if (buf.Length == 0) continue;

                    int packet_type = buf[0];
                    if ((packet_type >= 1) && (packet_type <= 11))
                    {
                        if (buf.Length == 40)
                        {
                            int eegrv = buf[1] * 256 + buf[2];
                            int eeglv = buf[3] * 256 + buf[4];
                            int dx = buf[5] * 256 + buf[6];
                            int dy = buf[7] * 256 + buf[8];
                            int dz = buf[9] * 256 + buf[10];
                            double scaled_eegr = ScaleEEG(eegrv);
                            double scaled_eegl = ScaleEEG(eeglv);
                            double scaled_dx = ScaleAccel(dx);
                            double scaled_dy = ScaleAccel(dy);
                            double scaled_dz = ScaleAccel(dz);

                            scaled_eegr += 500;
                            scaled_eegl += 500;

                            m_dblX = (scaled_dx + 2) * 250;
                            m_dblY = (scaled_dy + 2) * 250;
                            m_dblZ = (scaled_dz + 2) * 250;

                            if (ClearDisplay)
                            {
                                ClearDisplay = false;
                                DisplayValue = 0;
                            }

                            if (ClearHighscore)
                            {
                                ClearHighscore = false;
                                DisplayValue = 0;
                            }

                            double raw = (scaled_eegr + scaled_eegl) / 2;
                            if (raw >= HighscoreValue)
                            {
                                HighscoreValue = (scaled_eegr + scaled_eegl) / 2;
                            }

                            DisplayValue = (scaled_eegr + scaled_eegl) / 2;

                            if (ZMaxEEGRChanged != null)
                            {
                                ZMaxEEGRChanged(null, new ZMaxChangedEventArgs(Convert.ToInt32(scaled_eegr)));
                            }
                            if (ZMaxEEGLChanged != null)
                            {
                                ZMaxEEGLChanged(null, new ZMaxChangedEventArgs(Convert.ToInt32(scaled_eegl)));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // running = false;
                // MessageBox.Show(ex.Message + data, "LucidScribe.processData()", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static double ScaleEEG(int x)
        {
            double y = x;
            y = y - 32768;
            y = y * (3952);
            y = y / 65536;
            return y;
        }

        private static double ScaleAccel(int x)
        {
            double y = x;
            y = y * 4 / 4096 - 2;
            return y;
        }

        public static byte[] HexToBytes(this string str)
        {
            if (str.Length == 0 || str.Length % 2 != 0) return new byte[0];

            byte[] buffer = new byte[str.Length / 2];
            char c;
            for (int bx = 0, sx = 0; bx < buffer.Length; ++bx, ++sx)
            {
                c = str[sx];
                buffer[bx] = (byte)((c > '9' ? (c > 'Z' ? (c - 'a' + 10) : (c - 'A' + 10)) : (c - '0')) << 4);

                c = str[++sx];
                buffer[bx] |= (byte)(c > '9' ? (c > 'Z' ? (c - 'a' + 10) : (c - 'A' + 10)) : (c - '0'));
            }

            return buffer;
        }

        public static void Dispose()
        {
            running = false;
        }

        public static Double GetEEG()
        {
            double temp = DisplayValue;
            ClearDisplay = true;
            return DisplayValue;
        }

        public static Double GetHighscore()
        {
            double temp = HighscoreValue;
            ClearHighscore = true;
            return HighscoreValue;
        }

        public static Double GetREM()
        {
            return 0;
        }

        public static Double GetX()
        {
            return m_dblX;
        }

        public static Double GetY()
        {
            return m_dblY;
        }

        public static Double GetZ()
        {
            return m_dblZ;
        }
    }

    namespace EEG
    {
        public class PluginHandler : lucidcode.LucidScribe.Interface.LucidPluginBase
        {
            public override string Name
            {
                get { return "ZMax EEG"; }
            }
            public override bool Initialize()
            {
                return Device.Initialize();
            }
            public override double Value
            {
                get
                {
                    double dblValue = Device.GetEEG();
                    if (dblValue > 999) { dblValue = 999; }
                    if (dblValue < 0) { dblValue = 0; }
                    return dblValue;
                }
            }
        }
    }

    namespace EEGR
    {
        public class PluginHandler : lucidcode.LucidScribe.Interface.ILluminatedPlugin
        {
            public string Name
            {
                get { return "ZMax EEG R"; }
            }
            public bool Initialize()
            {
                bool initialized = Device.Initialize();
                Device.ZMaxEEGRChanged += ZMaxEEGRChanged;
                return initialized;
            }

            public event Interface.SenseHandler Sensed;
            public void ZMaxEEGRChanged(object sender, ZMaxChangedEventArgs e)
            {
                if (ClearTicks)
                {
                    ClearTicks = false;
                    TickCount = "";
                }
                TickCount += e.EEG + ",";

                if (ClearBuffer)
                {
                    ClearBuffer = false;
                    BufferData = "";
                }
                BufferData += e.EEG + ",";
            }

            public void Dispose()
            {
                Device.Dispose();
            }

            public Boolean isEnabled = false;
            public Boolean Enabled
            {
                get
                {
                    return isEnabled;
                }
                set
                {
                    isEnabled = value;
                }
            }

            public Color PluginColor = Color.White;
            public Color Color
            {
                get
                {
                    return Color;
                }
                set
                {
                    Color = value;
                }
            }

            private Boolean ClearTicks = false;
            public String TickCount = "";
            public String Ticks
            {
                get
                {
                    ClearTicks = true;
                    return TickCount;
                }
                set
                {
                    TickCount = value;
                }
            }

            private Boolean ClearBuffer = false;
            public String BufferData = "";
            public String Buffer
            {
                get
                {
                    ClearBuffer = true;
                    return BufferData;
                }
                set
                {
                    BufferData = value;
                }
            }

            int lastHour;
            public int LastHour
            {
                get
                {
                    return lastHour;
                }
                set
                {
                    lastHour = value;
                }
            }
        }
    }

    namespace EEGL
    {
        public class PluginHandler : lucidcode.LucidScribe.Interface.ILluminatedPlugin
        {
            public string Name
            {
                get { return "ZMax EEG L"; }
            }
            public bool Initialize()
            {
                bool initialized = Device.Initialize();
                Device.ZMaxEEGLChanged += ZMaxEEGLChanged;
                return initialized;
            }

            public event Interface.SenseHandler Sensed;
            public void ZMaxEEGLChanged(object sender, ZMaxChangedEventArgs e)
            {
                if (ClearTicks)
                {
                    ClearTicks = false;
                    TickCount = "";
                }
                TickCount += e.EEG + ",";

                if (ClearBuffer)
                {
                    ClearBuffer = false;
                    BufferData = "";
                }
                BufferData += e.EEG + ",";
            }

            public void Dispose()
            {
                Device.Dispose();
            }

            public Boolean isEnabled = false;
            public Boolean Enabled
            {
                get
                {
                    return isEnabled;
                }
                set
                {
                    isEnabled = value;
                }
            }

            public Color PluginColor = Color.White;
            public Color Color
            {
                get
                {
                    return Color;
                }
                set
                {
                    Color = value;
                }
            }

            private Boolean ClearTicks = false;
            public String TickCount = "";
            public String Ticks
            {
                get
                {
                    ClearTicks = true;
                    return TickCount;
                }
                set
                {
                    TickCount = value;
                }
            }

            private Boolean ClearBuffer = false;
            public String BufferData = "";
            public String Buffer
            {
                get
                {
                    ClearBuffer = true;
                    return BufferData;
                }
                set
                {
                    BufferData = value;
                }
            }

            int lastHour;
            public int LastHour
            {
                get
                {
                    return lastHour;
                }
                set
                {
                    lastHour = value;
                }
            }
        }
    }

    namespace RapidEyeMovement
    {
        public class PluginHandler : lucidcode.LucidScribe.Interface.LucidPluginBase
        {
            List<int> m_arrHistory = new List<int>();
            public override string Name
            {
                get { return "ZMax REM"; }
            }
            public override bool Initialize()
            {
                return Device.Initialize();
            }
            public override double Value
            {
                get
                {
                    double dblEEG = Device.GetEEG();
                    if (dblEEG > 999) { dblEEG = 999; }
                    if (dblEEG < 0) { dblEEG = 0; }

                    if (Device.Algorithm == "REM Detector")
                    {
                        // Update the mem list
                        m_arrHistory.Add(Convert.ToInt32(dblEEG));
                        if (m_arrHistory.Count > 512) { m_arrHistory.RemoveAt(0); }

                        // Check for blinks
                        int intBlinks = 0;
                        bool boolBlinking = false;

                        int intBelow = 0;
                        int intAbove = 0;

                        bool boolDreaming = false;
                        foreach (Double dblValue in m_arrHistory)
                        {
                            if (dblValue > Device.Threshold)
                            {
                                intAbove += 1;
                                intBelow = 0;
                            }
                            else
                            {
                                intBelow += 1;
                                intAbove = 0;
                            }

                            if (!boolBlinking)
                            {
                                if (intAbove >= 1)
                                {
                                    boolBlinking = true;
                                    intBlinks += 1;
                                    intAbove = 0;
                                    intBelow = 0;
                                }
                            }
                            else
                            {
                                if (intBelow >= 28)
                                {
                                    boolBlinking = false;
                                    intBelow = 0;
                                    intAbove = 0;
                                }
                                else
                                {
                                    if (intAbove >= 12)
                                    {
                                        // reset
                                        boolBlinking = false;
                                        intBlinks = 0;
                                        intBelow = 0;
                                        intAbove = 0;
                                    }
                                }
                            }

                            if (intBlinks > 6)
                            {
                                boolDreaming = true;
                                break;
                            }

                            if (intAbove > 12)
                            { // reset
                                boolBlinking = false;
                                intBlinks = 0;
                                intBelow = 0;
                                intAbove = 0; ;
                            }
                            if (intBelow > 80)
                            { // reset
                                boolBlinking = false;
                                intBlinks = 0;
                                intBelow = 0;
                                intAbove = 0; ;
                            }
                        }

                        if (boolDreaming)
                        {
                            return 888;
                        }

                        if (intBlinks > 10) { intBlinks = 10; }
                        return intBlinks * 100;
                    }
                    else if (Device.Algorithm == "Motion Detector")
                    {
                        if (dblEEG >= Device.Threshold)
                        {
                            return 888;
                        }
                    }

                    return 0;
                }
            }

        }
    }

    namespace TCMP
    {
        public class PluginHandler : lucidcode.LucidScribe.Interface.LucidPluginBase, lucidcode.LucidScribe.TCMP.ITransConsciousnessPlugin
        {

            public override string Name
            {
                get
                {
                    return "ZMax TCMP";
                }
            }

            public override bool Initialize()
            {
                try
                {
                    return Device.Initialize();
                }
                catch (Exception ex)
                {
                    throw (new Exception("The '" + Name + "' plugin failed to initialize: " + ex.Message));
                }
            }

            private static String Morse = "";
            Dictionary<char, String> Code = new Dictionary<char, String>()
          {
              {'A' , ".-"},
              {'B' , "-..."},
              {'C' , "-.-."},
              {'D' , "-.."},
              {'E' , "."},
              {'F' , "..-."},
              {'G' , "--."},
              {'H' , "...."},
              {'I' , ".."},
              {'J' , ".---"},
              {'K' , "-.-"},
              {'L' , ".-.."},
              {'M' , "--"},
              {'N' , "-."},
              {'O' , "---"},
              {'P' , ".--."},
              {'Q' , "--.-"},
              {'R' , ".-."},
              {'S' , "..."},
              {'T' , "-"},
              {'U' , "..-"},
              {'V' , "...-"},
              {'W' , ".--"},
              {'X' , "-..-"},
              {'Y' , "-.--"},
              {'Z' , "--.."},
              {'0' , "-----"},
              {'1' , ".----"},
              {'2' , "..----"},
              {'3' , "...--"},
              {'4' , "....-"},
              {'5' , "....."},
              {'6' , "-...."},
              {'7' , "--..."},
              {'8' , "---.."},
              {'9' , "----."},
          };

            List<int> m_arrHistory = new List<int>();
            Boolean FirstTick = false;
            Boolean SpaceSent = true;
            int TicksSinceSpace = 0;
            Boolean Started = false;
            int PreliminaryTicks = 0;

            public override double Value
            {
                get
                {
                    if (!Device.TCMP) { return 0; }

                    double tempValue = Device.GetEEG();
                    if (tempValue > 999) { tempValue = 999; }
                    if (tempValue < 0) { tempValue = 0; }

                    if (!Started)
                    {
                        PreliminaryTicks++;
                        if (PreliminaryTicks > 10)
                        {
                            Started = true;
                        }

                        return 0;
                    }

                    int signalLength = 0;
                    int dotHeight = 500;
                    int dashHeight = 900;

                    // Update the mem list
                    String signal = "";

                    if (!FirstTick && (tempValue > dotHeight))
                    {
                        m_arrHistory.Add(Convert.ToInt32(tempValue));
                    }

                    if (!FirstTick && m_arrHistory.Count > 0)
                    {
                        m_arrHistory.Add(Convert.ToInt32(tempValue));
                    }

                    if (FirstTick && (tempValue > dotHeight))
                    {
                        FirstTick = false;
                    }

                    if (!SpaceSent & m_arrHistory.Count == 0)
                    {
                        TicksSinceSpace++;
                        if (TicksSinceSpace > 32)
                        {
                            // Send the space key
                            Morse = " ";
                            SendKeys.Send(" ");
                            SpaceSent = true;
                            TicksSinceSpace = 0;
                        }
                    }

                    if (!FirstTick && m_arrHistory.Count > 32)
                    {
                        int nextOffset = 0;
                        do
                        {
                            int fivePointValue = 0;
                            for (int i = nextOffset; i < m_arrHistory.Count; i++)
                            {
                                for (int x = i; x < m_arrHistory.Count; x++)
                                {
                                    if (m_arrHistory[x] > fivePointValue)
                                    {
                                        fivePointValue = m_arrHistory[x];
                                    }

                                    if (m_arrHistory[x] < 300)
                                    {
                                        nextOffset = x + 1;
                                        break;
                                    }

                                    if (x == m_arrHistory.Count - 1)
                                    {
                                        nextOffset = -1;
                                    }
                                }

                                if (fivePointValue >= dashHeight)
                                {
                                    signal += "-";
                                    signalLength++;
                                    break;
                                }
                                else if (fivePointValue >= dotHeight)
                                {
                                    signal += ".";
                                    signalLength++;
                                    break;
                                }

                                if (i == m_arrHistory.Count - 1)
                                {
                                    nextOffset = -1;
                                }

                            }

                            if (nextOffset < 0 | nextOffset == m_arrHistory.Count)
                            {
                                break;
                            }

                        } while (true);

                        m_arrHistory.RemoveAt(0);

                        // Check if the signal is morse
                        try
                        {
                            // Make sure that we have a signal
                            if (signal != "")
                            {
                                var myValue = Code.First(x => x.Value == signal);
                                Morse = myValue.Key.ToString();
                                SendKeys.Send(myValue.Key.ToString());
                                signal = "";
                                m_arrHistory.Clear();
                                SpaceSent = false;
                                TicksSinceSpace = 0;
                            }
                        }
                        catch (Exception ex)
                        {
                            String err = ex.Message;
                        }
                    }

                    if (m_arrHistory.Count > 0)
                    { return 888; }

                    return 0;
                }
            }

            string lucidcode.LucidScribe.TCMP.ITransConsciousnessPlugin.MorseCode
            {
                get
                {
                    String temp = Morse;
                    Morse = "";
                    return temp;
                }
            }

            public override void Dispose()
            {
                Device.Dispose();
            }

        }
    }

    namespace X
    {
        public class PluginHandler : lucidcode.LucidScribe.Interface.LucidPluginBase
        {
            public override string Name
            {
                get { return "ZMax X"; }
            }
            public override bool Initialize()
            {
                return Device.Initialize();
            }
            public override double Value
            {
                get
                {
                    double dblValue = Device.GetX();
                    if (dblValue > 999) { dblValue = 999; }
                    return dblValue;
                }
            }
        }
    }

    namespace Y
    {
        public class PluginHandler : lucidcode.LucidScribe.Interface.LucidPluginBase
        {
            public override string Name
            {
                get { return "ZMax Y"; }
            }
            public override bool Initialize()
            {
                return Device.Initialize();
            }
            public override double Value
            {
                get
                {
                    double dblValue = Device.GetY();
                    if (dblValue > 999) { dblValue = 999; }
                    return dblValue;
                }
            }
        }
    }

    namespace Z
    {
        public class PluginHandler : lucidcode.LucidScribe.Interface.LucidPluginBase
        {
            public override string Name
            {
                get { return "ZMax Z"; }
            }
            public override bool Initialize()
            {
                return Device.Initialize();
            }
            public override double Value
            {
                get
                {
                    double dblValue = Device.GetZ();
                    if (dblValue > 999) { dblValue = 999; }
                    return dblValue;
                }
            }
        }
    }

}
