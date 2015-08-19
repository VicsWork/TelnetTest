using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MinimalisticTelnet;
using System.Text.RegularExpressions;
using System.Threading;
using System.Diagnostics;


namespace powercal
{
    enum BoardTypes { Zebrashark, Humpback, Hooktooth, Milkshark };

    class Calibrate
    {
        class Program
        {
            static void Main(string[] args)
            {

                BoardTypes board_type = BoardTypes.Humpback;

                double voltage_low_limit = 0.0;
                double voltage_reference = 0.0;
                double current_reference = 0.0;

                string cmd_prefix;

                switch (board_type)
                {
                    case BoardTypes.Humpback:
                        voltage_low_limit = 200;
                        voltage_reference = 240;
                        current_reference = 15;
                        cmd_prefix = "cs5490";
                        break;
                    case BoardTypes.Zebrashark:
                        voltage_low_limit = 80;
                        voltage_reference = 120;
                        current_reference = 15;
                        cmd_prefix = "cs5480";
                        break;
                    default:
                        cmd_prefix = "cs5490";
                        voltage_low_limit = 80;
                        voltage_reference = 120;
                        current_reference = 15;
                        break;
                }

                //create a new telnet connection
                TelnetConnection tc = new TelnetConnection("localhost", 4900);
                string datain = tc.Read();

                string msg = patch(board_type, 0x400000, 0x400000);
                Thread.Sleep(2000);
                datain = tc.Read();

                tc.WriteLine("version");
                Thread.Sleep(500);
                datain = tc.Read();
                updateOutputStatus(datain);

                tc.WriteLine( string.Format("cu {0}_pinfo", cmd_prefix) );
                Thread.Sleep(500);
                datain = tc.Read();
                updateOutputStatus(datain);

                string rawCurrentPattern = "Raw IRMS: ([0-9,A-F]{8})";
                string rawVoltagePattern = "Raw VRMS: ([0-9,A-F]{8})";
                double current_cs = 0.0;
                double voltage_cs = 0.0;
                int i = 0;
                int fail_count = 0;
                while(true)
                {
                    //tc.WriteLine("cu cs5480_start_conv");
                    //tc.WriteLine("cu cs5480_start_single_conv");
                    //Thread.Sleep(1000);

                    tc.WriteLine(string.Format("cu {0}_pload", cmd_prefix));
                    Thread.Sleep(500);
                    datain = tc.Read();
                    updateOutputStatus(datain);

                    if (datain.Length > 0)
                    {
                        Match on_off_match = Regex.Match(datain, "Changing OnOff .*");
                        if (on_off_match.Success)
                        {
                            msg = on_off_match.Value;
                            updateOutputStatus(msg);
                        }

                        Match match = Regex.Match(datain, rawCurrentPattern);
                        if (match.Groups.Count > 1)
                        {
                            string current_hexstr = match.Groups[1].Value;
                            int current_int = Convert.ToInt32(current_hexstr, 16);
                            current_cs = RegHex_ToDouble(current_int);
                            current_cs = current_cs * current_reference / 0.6;

                            voltage_cs = 0.0;
                            match = Regex.Match(datain, rawVoltagePattern);
                            if (match.Groups.Count > 1)
                            {
                                string voltage_hexstr = match.Groups[1].Value;
                                int volatge_int = Convert.ToInt32(voltage_hexstr, 16);
                                voltage_cs = RegHex_ToDouble(volatge_int);
                                voltage_cs = voltage_cs * voltage_reference / 0.6;
                            }

                            if (voltage_cs > voltage_low_limit)
                            {
                                i++;
                                msg = string.Format("Cirrus I = {0:F8}, V = {1:F8}, P = {2:F8}", current_cs, voltage_cs, current_cs * voltage_cs);
                                updateOutputStatus(msg);
                            }
                            else
                            {
                                fail_count++;
                            }
                            if (i > 1)
                                break;
                        }
                    }

                    Thread.Sleep(1000);
                }

                /// The meter measurements
                MultiMeter meter = new MultiMeter("COM1");
                meter.OpenComPort();
                meter.SetToRemote();

                meter.SetupForIAC();
                string current_meter_str = meter.Measure();
                current_meter_str = meter.Measure();
                double current_meter = Double.Parse(current_meter_str);

                meter.SetupForVAC();
                string voltage_meter_str = meter.Measure();
                voltage_meter_str = meter.Measure();
                double voltage_meter = Double.Parse(voltage_meter_str);

                meter.CloseSerialPort();

                msg = string.Format("Meter I = {0:F8}, V = {1:F8}, P = {2:F8}", current_meter, voltage_meter, current_meter * voltage_meter);
                updateOutputStatus(msg);

                // Gain calucalation
                double current_gain = current_meter/current_cs;
                //double current_gain = current_meter / current_cs;
                int current_gain_int = (int)(current_gain * 0x400000);
                msg = string.Format("Current Gain = {0:F8} (0x{1:X})", current_gain, current_gain_int);
                updateOutputStatus(msg);

                double voltage_gain = voltage_meter/voltage_cs;
                int voltage_gain_int = (int)(voltage_gain * 0x400000);
                msg = string.Format("Voltage Gain = {0:F8} (0x{1:X})", voltage_gain, voltage_gain_int);
                updateOutputStatus(msg);

                msg = patch(board_type, voltage_gain_int, current_gain_int);
                Thread.Sleep(2000);
                datain = tc.Read();
                updateOutputStatus(datain);

                tc.WriteLine(string.Format("cu {0}_pinfo", cmd_prefix));
                Thread.Sleep(500);
                datain = tc.Read();
                updateOutputStatus(datain);

                i = 0;
                while (true)
                {
                    tc.WriteLine(string.Format("cu {0}_pload", cmd_prefix));
                    Thread.Sleep(500);
                    datain = tc.Read();
                    Debug.WriteLine(datain);

                    if (datain.Length > 0)
                    {
                        Match on_off_match = Regex.Match(datain, "Changing OnOff .*");
                        if (on_off_match.Success)
                        {
                            msg = on_off_match.Value;
                            updateOutputStatus(msg);
                        }

                        Match match = Regex.Match(datain, rawCurrentPattern);
                        if (match.Groups.Count > 1)
                        {
                            string current_hexstr = match.Groups[1].Value;
                            int current_int = Convert.ToInt32(current_hexstr, 16);
                            current_cs = RegHex_ToDouble(current_int);
                            current_cs = current_cs * current_reference / 0.6;

                            voltage_cs = 0.0;
                            match = Regex.Match(datain, rawVoltagePattern);
                            if (match.Groups.Count > 1)
                            {
                                string voltage_hexstr = match.Groups[1].Value;
                                int volatge_int = Convert.ToInt32(voltage_hexstr, 16);
                                voltage_cs = RegHex_ToDouble(volatge_int);
                                voltage_cs = voltage_cs * voltage_reference / 0.6;
                            }

                            if (voltage_cs > voltage_low_limit)
                            {
                                i++;
                                msg = string.Format("Cirrus I = {0:F8}, V = {1:F8}, P = {2:F8}", current_cs, voltage_cs, current_cs * voltage_cs);
                                updateOutputStatus(msg);
                            }
                            if (i > 1)
                                break;
                        }
                    }

                    Thread.Sleep(1000);
                }
                tc.Close();

            }

            static void updateOutputStatus(string txt)
            {
                string line = string.Format("{0:G}:\r\n {1}", DateTime.Now, txt);
                Console.WriteLine(line);
                Debug.WriteLine(line);
            }

            static string patch(BoardTypes board_type, int voltage_gain, int current_gain)
            {
                Ember ember = new Ember();
                ember.EmberBinPath = @"C:\Program Files (x86)\Ember\ISA3 Utilities\bin";
                ember.BatchFilePath = @"C:\Users\victormartin\.calibration\patchit.bat";
                string msg;
                switch (board_type)
                {
                    case (powercal.BoardTypes.Humpback):
                        ember.VAdress = 0x08080980;
                        ember.IAdress = 0x08080984;
                        ember.RefereceAdress = 0x08080988;
                        ember.ACOffsetAdress = 0x080809CC;

                        ember.VRefereceValue = 0xF0; // 240 V
                        ember.IRefereceValue = 0x0F; // 15 A

                        break;
                    case (powercal.BoardTypes.Zebrashark):
                    case (powercal.BoardTypes.Hooktooth):
                    case (powercal.BoardTypes.Milkshark):
                        ember.VAdress = 0x08040980;
                        ember.IAdress = 0x08040984;
                        ember.ACOffsetAdress = 0x080409CC;

                        ember.VRefereceValue = 0x78; // 120 V
                        ember.IRefereceValue = 0x0F; // 15 A

                        break;
                }
                ember.CreateCalibrationPatchBath(voltage_gain, current_gain);

                bool patchit_fail = false;
                string exception_msg = "";
                string coding_output = "";
                // Retry patch loop if fail
                while (true)
                {
                    patchit_fail = false;
                    exception_msg = "";
                    coding_output = "";
                    try
                    {
                        string output = ember.RunCalibrationPatchBatch();
                        if (output.Contains("ERROR:"))
                        {
                            patchit_fail = true;
                            exception_msg = "Patching error detected:\r\n";
                            exception_msg += output;
                        }
                        coding_output = output;
                    }
                    catch (Exception e)
                    {
                        patchit_fail = true;
                        exception_msg = "Patching exception detected:\r\n";
                        exception_msg += e.Message;
                    }

                    if (patchit_fail)
                    {
                        string retry_err_msg = exception_msg;
                        int max_len = 1000;
                        if (retry_err_msg.Length > max_len)
                            retry_err_msg = retry_err_msg.Substring(0, max_len) + "...";

                        msg = "Patching fail";
                        Console.WriteLine(msg);
                        Debug.WriteLine(msg);
                    }
                    else
                    {
                        break;
                    }

                }

                return coding_output;
            }


            /// <summary>
            /// Converts a 24bit hex (3 bytes) CS register value to a double
            /// </summary>
            /// <example>
            /// byte[] rx_data = new byte[3];
            /// rx_data[2] = 0x5c;
            /// rx_data[1] = 0x28;
            /// rx_data[0] = 0xf6;
            /// Should return midrange =~ 0.36
            /// </example>
            /// <param name="rx_data">data byte array byte[2] <=> MSB ... byte[0] <=> LSB</param>
            /// <returns>range 0 <= value < 1.0</returns>
            private static double RegHex_ToDouble(int data)
            {
                // Maximum 1 =~ 0xFFFFFF
                // Max rms 0.6 =~ 0x999999
                // Half rms 0.36 =~ 0x5C28F6
                double value = ((double)data) / 0x1000000; // 2^24
                return value;
            }

            private static double RegHex_ToDouble(string hexstr)
            {
                int val_int = Convert.ToInt32(hexstr, 16);
                return RegHex_ToDouble(val_int); ;
            }

        }
    }
}