using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Threading;

namespace powercal
{
    /// <summary>
    /// Creates and runs batch file to patch calibration tokens at specified addresses
    /// </summary>
    class Ember
    {
        public string BatchFilePath { get { return _batch_file; } set { _batch_file = value; } }
        public string EmberBinPath { get { return _ember_bin_path; } set { _ember_bin_path = value; } }
        public string EmberExe { get { return _ember_exe; } set { _ember_exe = value; } }

        public int VAdress { get { return _vAddress; } set { _vAddress = value; } }
        public int IAdress { get { return _iAddress; } set { _iAddress = value; } }
        public int RefereceAdress { get { return _refAddress; } set { _refAddress = value; } }
        public int ACOffsetAdress { get { return _acOffsetAddress; } set { _acOffsetAddress = value; } }

        public int VRefereceValue { get { return _vRefValue; } set { _vRefValue = value; } }
        public int IRefereceValue { get { return _iRefValue; } set { _iRefValue = value; } }

        string _batch_file = "C:\\patchit.bat";
        private string _ember_exe = "em3xx_load";
        private string _ember_bin_path = "C:\\Program Files (x86)\\Ember\\ISA3 Utilities\\bin";

        private int _usb_port = 0;

        private int _vAddress = 0x08040980;
        private int _iAddress = 0x08040984;
        private int _refAddress = 0x08040988;
        private int _acOffsetAddress = 0x080409CC;

        // For Humpback:
        //To set the VREF to 240, the patch contains "@08080988=F0 @08080989=00"
        //To set the IREF to 15, the patch contains "@0808098A=0F @0808098B=00"
        private int _vRefValue = 0x0;
        private int _iRefValue = 0x0;

        /// <summary>
        /// Runs a calibartion batch file
        /// </summary>
        /// <returns></returns>
        public string RunCalibrationPatchBatch()
        {
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.FileName = _batch_file;
            p.Start();

            int n = 0;
            string error, output;
            while (!p.HasExited)
            {
                Thread.Sleep(1000);
                n++;
                if (n > 10)
                {
                    p.Kill();
                    output = p.StandardOutput.ReadToEnd();
                    error = p.StandardError.ReadToEnd();
                    string msg = string.Format("Timeout running {0}.\r\n", _batch_file);
                    if (output != null && output.Length > 0)
                        msg += string.Format("Output: {0}\r\n", output);
                    if (error != null && error.Length > 0)
                        msg += string.Format("Error: {0}\r\n", error);

                    throw new Exception(msg);
                }
            }

            error = p.StandardError.ReadToEnd();
            output = p.StandardOutput.ReadToEnd();
            int rc = p.ExitCode;
            if (rc != 0)
            {
                string msg = string.Format("Error running {0}.\r\n", _batch_file);
                msg += string.Format("RC: {0}\r\n", rc);
                if (error != null && error.Length > 0)
                    msg += string.Format("Error: {0}\r\n", error);

                throw new Exception(msg);
            }
            return output;
        }

        /// <summary>
        /// Creates a calibration batch file
        /// </summary>
        /// <param name="vrms">Vrms gain</param>
        /// <param name="irms">Irms gain</param>
        public void CreateCalibrationPatchBath(int vrms, int irms)
        {
            using (StreamWriter writer = File.CreateText(_batch_file))
            {
                string txt = string.Format("pushd \"{0}\"", _ember_bin_path);
                writer.WriteLine(txt);

                txt = string.Format("{0} --usb {1}", _ember_exe, _usb_port);
                writer.WriteLine(txt);

                txt = string.Format("{0} --patch ", _ember_exe);

                // vrms
                int start_addr = _vAddress;
                byte[] data = bit24IntToByteArray(vrms);
                foreach (byte b in data)
                {
                    txt += string.Format("@{0:X8}=", start_addr);
                    txt += string.Format("{0:X2} ", b);
                    start_addr++;
                }
                txt += string.Format("@{0:X8}=", start_addr);
                txt += string.Format("{0:X2} ", 0); // null

                // irms
                start_addr = _iAddress;
                data = bit24IntToByteArray(irms);
                foreach (byte b in data)
                {
                    txt += string.Format("@{0:X8}=", start_addr);
                    txt += string.Format("{0:X2} ", b);
                    start_addr++;
                }
                txt += string.Format("@{0:X8}=", start_addr);
                txt += string.Format("{0:X2} ", 0); // null

                // referece
                if (_vRefValue != 0x0)
                {
                    start_addr = _refAddress;
                    // vref
                    txt += string.Format("@{0:X8}=", start_addr++);
                    txt += string.Format("{0:X2} ", _vRefValue);
                    txt += string.Format("@{0:X8}=", start_addr++);
                    txt += string.Format("00 ");

                    // iref
                    txt += string.Format("@{0:X8}=", start_addr++);
                    txt += string.Format("{0:X2} ", _iRefValue);
                    txt += string.Format("@{0:X8}=", start_addr++);
                    txt += string.Format("00 ");
                }

                // ac offset
                start_addr = _acOffsetAddress;
                data = bit24IntToByteArray(0);
                foreach (byte b in data)
                {
                    txt += string.Format("@{0:X8}=", start_addr);
                    txt += string.Format("{0:X2} ", b);
                    start_addr++;
                }
                txt += string.Format("@{0:X8}=", start_addr);
                txt += string.Format("{0:X2} ", 0); // null

                writer.WriteLine(txt);

                txt = string.Format("popd");
                writer.WriteLine(txt);

                writer.Close();
            }
        }

        /// <summary>
        /// Breaks an int into 3 bytes
        /// </summary>
        /// <param name="value"></param>
        /// <returns>The last 3 bytes of int value</returns>
        byte[] bit24IntToByteArray(int value)
        {
            // Converts a 24bit value to a 3 byte array
            // Oreder by LSB to MSB
            byte[] vBytes = new byte[3] { 
                (byte)(value & 0xFF), 
                (byte)( (value >> 8) & 0xFF), 
                (byte)( (value >> 16) & 0xFF) };

            return vBytes;
        }
    }
}
