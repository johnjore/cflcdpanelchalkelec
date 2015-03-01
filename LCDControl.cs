/*
GUI Todo:
                Toggle backlight on/off
                If !auto brightness -> Enable Inc/Dec Brightness values. Disable buttons when in Automode
*/

using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Threading;
using System.Reflection;            //Debug information
using System.Collections.Generic;
using HidLibrary;
using centrafuse.Plugins;
using System.Linq;

namespace LCDControl
{
	/// <summary>
    /// Get/set LCD configuration
	/// </summary>
	public class LCDControl : CFPlugin
	{

#region Variables
		private const string PluginName = "LCDControl";
		private const string PluginPath = @"plugins\" + PluginName + @"\";
		private const string PluginPathSkins = PluginPath + @"Skins\";
		private const string PluginPathLanguages = PluginPath + @"Languages\";
		private const string PluginPathIcons = PluginPath + @"Icons\";
		private const string ConfigurationFile = "config.xml";
		private const string LogFile= "LCDControl.log";
        public static string LogFilePath = CFTools.AppDataPath + "\\Plugins\\" + PluginName + "\\" + LogFile;
        public static bool boolLogEvents = true;
        public static bool boolDebugLogEvents = false;
        
        //Supported devices. Language file?
        //VID,PID,ReportID,Model,MAX_BL
        public static object[][] aryLCDModels = new object[][] {
            new object[] { 0x4D8, 0x003F, 0, "DualVDS/FullHD+", 35},
            new object[] { 0x4D8, 0xF723, 1, "Single touch 7\"/10\"", 18},
            new object[] { 0x4D8, 0xF724, 1, "Multi touch 7\"/10\"", 18}
        };

        public static string[] aryBackLightOptions;         //Backlight Options. The order and position of the items within the array must not change
        public static string[] aryBrightnessOptions;        //Brightness Options. The order and position of the items within the array must not change

        private static int int_VID = 0;                     //Device to manage, VID. 0 = Auto detect
        private static int int_PID = 0;                     //Device to manage, PID. 0 = Auto detect
        private byte byteReportID = 0;                      //Report ID to use
        private byte intMAX_BL = 0;                         //MAX_BL to use
        private LCDStatus lcdstatus = new LCDStatus();      //LCDPanel current configuration

        //Commands
        Queue<LCDCommands> LCDCommand = new Queue<LCDCommands>();

        //Threads
        private Thread newLCDCompliance = null;
        private Thread newLCDStatusThread = null;
        private static bool _shouldStopThreads = false;
        private static bool boolNormalMode = true;          //Default to NORMAL mode
        
#endregion

#region Construction

		/// <summary>
		/// Default constructor (creates the plugin and sets its properties).
		/// </summary>
		public LCDControl()
		{
            // nothing special to do here
            // in a more advanced plugin, flags and such can be done here
            // Usually it is safe to just use the CF_initPlugin() override to do initialization
        }

#endregion

#region CFPlugin methods

		/// <summary>
		/// Initializes the plugin.  This is called from the main application
		/// when the plugin is first loaded.
		/// </summary>
		public override void CF_pluginInit()
		{
			try
			{
                //Plugin has a GUI
                CF_params.isGUI = true;

                // CF3_initPlugin() Will configure pluginConfig and pluginLang automatically
                this.CF3_initPlugin(PluginName, CF_params.isGUI);

                //Clear old values from log file
                CFTools.writeModuleLog("startup", LogFilePath);

                //Log current version of DLL for debug purposes
                WriteLog("Plugin Version: '" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString() + "'");
                WriteLog("CF Version: '" + System.Reflection.Assembly.GetCallingAssembly().GetName().Version.ToString() + "'");

                //From http://wiki.centrafuse.com/wiki/Application-Description.ashx
                this.CF_params.settingsDisplayDesc = this.pluginLang.ReadField("/APPLANG/SETUP/DESCRIPTION");

                // All controls should be created or Setup in CF_localskinsetup.
                this.CF_localskinsetup();

                LoadSettings();

                this.CF_events.CFPowerModeChanged += new CFPowerModeChangedEventHandler(OnPowerModeChanged); //Sleep support
			}
			catch(Exception errmsg) { CFTools.writeError(errmsg.ToString()); }
		}

        /// <summary>
		/// This is called to setup the skin.  This will usually be called in CF_pluginInit.  It will 
        /// also called by the system when the resolution has been changed.
		/// </summary>
		public override void CF_localskinsetup()
		{
            WriteLog("CF_localskinsetup");

            // Read the skin file, controls will be automatically created
            // CF_localskinsetup() should always call CF3_initSection() first, with the exception of setting any
            // CF_displayHooks flags, which affect the behaviour of the CF3_initSection() call.
            this.CF3_initSection("LCDControl");
		}
        
		/// <summary>
		/// This is called by the system when it exits or the plugin has been deleted.
		/// </summary>
		public override void CF_pluginClose()
		{
            try
            {
                _shouldStopThreads = true;

                //Wait to die
                if (newLCDStatusThread != null && newLCDCompliance != null)
                {
                    int max_wait = 50;
                    while ((newLCDStatusThread.IsAlive == true || newLCDCompliance.IsAlive == true) && max_wait-- > 0)
                    {
                        System.Threading.Thread.Sleep(100); //Allow time to close threads before killing them
                    }

                    if (newLCDStatusThread.IsAlive)
                    {
                        WriteLog("newLCDSTatusThread is going to be terminated");
                        newLCDStatusThread.Abort();
                    }

                    if (newLCDCompliance.IsAlive)
                    {
                        WriteLog("newLCDCompliance is going to be terminated");
                        newLCDCompliance.Abort();
                    }
                }                
            }
            catch (Exception ex)
            {
                WriteLog("Failed to close(), " + ex.ToString());
            }

            base.CF_pluginClose(); // calls form Dispose() method
		}
		
		/// <summary>
		/// This is called by the system when a button with this plugin action has been clicked.
		/// </summary>
		public override void CF_pluginShow()
		{
            base.CF_pluginShow(); // sets form Visible property
		}

        /// <summary>
        /// This is called by the system when this plugin is minimized/exited (when screen is left).
        /// </summary>
        public override void CF_pluginHide()
        {
            base.CF_pluginHide(); // sets form !Visible property
        }

		/// <summary>
		/// This is called by the system when the plugin setup is clicked.
		/// </summary>
		/// <returns>Returns the dialog result.</returns>
		public override DialogResult CF_pluginShowSetup()
		{
            WriteLog("CF_pluginShowSetup");

            // Return DialogResult.OK for the main application to update from plugin changes.
			DialogResult returnvalue = DialogResult.Cancel;

			try
			{
				// Creates a new plugin setup instance. If you create a CFDialog or CFSetup you must
				// set its MainForm property to the main plugins MainForm property.
				Setup setup = new Setup(this.MainForm, this.pluginConfig, this.pluginLang);
				returnvalue = setup.ShowDialog();
				if(returnvalue == DialogResult.OK)
				{
                    LoadSettings();
				}
				setup.Close();
				setup = null;
			}
			catch(Exception errmsg) { CFTools.writeError(errmsg.ToString()); }

			return returnvalue;
		}
        
		/// <summary>
		/// This method is called by the system when it pauses all audio.
		/// </summary>
		public override void CF_pluginPause()
		{
            WriteLog("CF_pluginPause");
		}

        /// <summary>
		/// This is called by the system when it resumes all audio.
		/// </summary>
		public override void CF_pluginResume()
		{
            WriteLog("CF_pluginResume");
		}
        
		/// <summary>
		/// Used for plugin to plugin communication. Parameters can be passed into CF_Main_systemCommands
		/// with CF_Actions.PLUGIN, plugin name, plugin command, and a command parameter.
		/// </summary>
		/// <param name="command">The command to execute.</param>
		/// <param name="param1">The first parameter.</param>
		/// <param name="param2">The second parameter.</param>
		public override void CF_pluginCommand(string command, string param1, string param2)
		{
            WriteLog("CF_pluginCommand: " + command + " " + param1 + ", " + param2);
		}
        
		/// <summary>
		/// Used for retrieving information from plugins. You can run CF_getPluginData with a plugin name,
		///	command, and parameter to retrieve information from other plugins running on the system.
		/// </summary>
		/// <param name="command">The command to execute.</param>
		/// <param name="param">The parameter.</param>
		/// <returns>Returns whatever is appropriate.</returns>
		public override string CF_pluginData(string command, string param)
		{
            WriteLog("CF_pluginData: " + command + " " + param);
            string retvalue = "";

            try
            {
                //Nothing to return yet
                /**/ //Add current status?
            }
            catch (Exception ex)
            {
                WriteLog("Failed to return data in CF_pluginData(), " + ex.ToString());
            }
            return retvalue;
		}
        
        /// <summary>
        /// Called on control clicks, down events, etc, if the control has a defined CML action parameter in the skin xml.
        /// </summary>
        /// <param name="id">The command to execute.</param>
        /// <param name="state">Button State.</param>
        /// <returns>Returns whatever is appropriate.</returns>
        public override bool CF_pluginCMLCommand(string id, string[] strparams, CF_ButtonState state, int zone)
        {
            if (state != CF_ButtonState.Click)
                return false;

            WriteLog("CF_pluginCMLCommand: " + id);

            switch (id.ToUpper())
            {
                /**/ //Add support for Auto on/off
                case "BTNMIN": LCDCommand.Enqueue(LCDCommands.Set_Brightness_Min); return true;
                case "BTNINC": LCDCommand.Enqueue(LCDCommands.Inc_Brightness); return true;
                case "BTNDEC": LCDCommand.Enqueue(LCDCommands.Dec_Brightness); return true;
                case "BTNMAX": LCDCommand.Enqueue(LCDCommands.Set_Brightness_Max); return true;
            }

            return false;
        }

        // Fired when the power mode of the operating system changes
        private void OnPowerModeChanged(object sender, CFPowerModeChangedEventArgs e)
        {
            try
            {
                //If suspending
                if (e.Mode == CFPowerModes.Suspend)
                {
                    WriteLog("OnPowerModeChanged 'Suspend/Sleep'");

                    //Sleep mode
                    boolNormalMode = false;

                    //Suspend the threads
                    try
                    {
                        /**/ //Not ideal to use Suspend() but for the most part, its stable... Replace when possible
                        if (newLCDStatusThread != null) if (newLCDStatusThread.IsAlive) newLCDStatusThread.Suspend();
                        if (newLCDCompliance != null) if (newLCDCompliance.IsAlive) newLCDCompliance.Suspend();
                    }
                    catch (Exception ex)
                    {
                        WriteLog("Failed to suspend threads, " + ex.ToString());
                    }

                    // Turn off backlight
                    BackLightOff();
                }

                //If resuming from sleep
                if (e.Mode == CFPowerModes.Resume)
                {
                    WriteLog("OnPowerModeChanged 'Resume'");

                    //Normal mode
                    boolNormalMode = true;

                    // Turn on backlight
                    BackLightOn();

                    WriteLog("newLCDStatusThread : " + newLCDStatusThread.IsAlive.ToString());
                    WriteLog("newLCDCompliance   : " + newLCDCompliance.IsAlive.ToString());

                    try
                    {
                        /**/ //Not ideal to use Resume() but for the most part, its stable... Replace when possible
                        if (newLCDStatusThread != null) newLCDStatusThread.Resume();
                        if (newLCDCompliance != null) newLCDCompliance.Resume();
                    }
                    catch (Exception ex)
                    {
                        WriteLog("Failed to resume threads, " + ex.ToString());
                    }

                    WriteLog("newLCDStatusThread : " + newLCDStatusThread.IsAlive.ToString());
                    WriteLog("newLCDCompliance   : " + newLCDCompliance.IsAlive.ToString());

                    //No need to wait, will re-configure itself as part of running threads
                }
            }
            catch (Exception ex)
            {
                WriteLog("Failed to process PowerModeChange : " + ex.ToString());
            }
            
            WriteLog("OnPowerModeChanged - end()");
            return;
        }

#endregion
		
#region System Functions

        private void BackLightOff()
        {
            WriteLog("Turning off backlight");
            LCDCommand.Enqueue(LCDCommands.Get_Configuration);
            LCDIO();

            int max_wait = 50;
            while (lcdstatus.backlight_on == 1 && max_wait-- > 0)
            {
                LCDCommand.Enqueue(LCDCommands.Toggle_Backlight);
                LCDIO();
                WriteLog("Max_Wait: " + max_wait.ToString());
            }
            WriteLog("Backlight : " + lcdstatus.backlight_on.ToString());
        }

        private void BackLightOn()
        {
            WriteLog("Turning on backlight");
            LCDCommand.Enqueue(LCDCommands.Get_Configuration);
            LCDIO();

            int max_wait = 50;
            while (lcdstatus.backlight_on == 0 && max_wait-- > 0)
            {
                LCDCommand.Enqueue(LCDCommands.Toggle_Backlight);
                LCDIO();
                WriteLog("Max_Wait: " + max_wait.ToString());
            }
            WriteLog("Backlight : " + lcdstatus.backlight_on.ToString());
        }
        
        private void LoadSettings()
        {
            //Display name
            this.CF_params.displayName = this.pluginLang.ReadField("/APPLANG/LCDCONTROL/DISPLAYNAME");
            WriteLog("Display name : " + this.CF_params.displayName);

            //Log Events?
            try
            {
                boolLogEvents = Boolean.Parse(this.pluginConfig.ReadField("/APPCONFIG/LOGEVENTS"));
            }
            catch (Exception ex)
            {
                WriteLog("Failed to parse LOGEVENTS, " + ex.ToString());
                boolLogEvents = false;
            }

            //Log Debug Events?
            try
            {
                boolDebugLogEvents = Boolean.Parse(this.pluginConfig.ReadField("/APPCONFIG/DEBUGLOGEVENTS"));
            }
            catch (Exception ex)
            {
                WriteLog("Failed to parse DEBUGLOGEVENTS, " + ex.ToString());
                boolDebugLogEvents = false;
            }

            //Options
            try
            {
                aryBackLightOptions = this.pluginLang.ReadField("/APPLANG/SETUP/BACKLIGHTOPTIONS").Split('|');
                aryBrightnessOptions = this.pluginLang.ReadField("/APPLANG/SETUP/BRIGHTNESSOPTIONS").Split('|');
            }
            catch (Exception ex)
            {
                WriteLog("Failed to parse Backlight/Brightness Options, " + ex.ToString());

                //Set Arrays to something we can use...
                aryBackLightOptions[0] = "Off";
                aryBackLightOptions[1] = "On";
                aryBackLightOptions[2] = "No Action";
                aryBrightnessOptions[0] = "Manual";
                aryBrightnessOptions[1] = "Auto";
                aryBrightnessOptions[2] = "No Action";
            }

            //VID/PID - What device are we managing?
            try
            {
                //Get VID & PID
                int_VID = int.Parse(this.pluginConfig.ReadField("/APPCONFIG/VID"));
                int_PID = int.Parse(this.pluginConfig.ReadField("/APPCONFIG/PID"));

                //Auto?
                if (int_VID == 0 && int_PID == 0) FindLCDDevice();
            }
            catch (Exception ex)
            {
                WriteLog("Failed to parse VID/PID, " + ex.ToString());

                //First time we start. Try Auto mode...
                int_VID = 0;
                int_PID = 0;
                FindLCDDevice();
            }
            finally
            {
                /**/ //Does not work with multiple LCD Panels...
                //Work with this device
                if (int_VID != 0 && int_PID != 0)
                {
                    //Find  ReportID to use
                    for (byte i = 0; i < LCDControl.aryLCDModels.Length; i++)
                    {
                        int tmp_VID = (int)(LCDControl.aryLCDModels[i][0]);
                        int tmp_PID = (int)(LCDControl.aryLCDModels[i][1]);
                        byte tmp_ReportID = (byte)((int)(LCDControl.aryLCDModels[i][2]));
                        byte tmp_MAX_BL= (byte)((int)(LCDControl.aryLCDModels[i][4]));

                        if (tmp_VID == int_VID && tmp_PID == int_PID)
                        {
                            byteReportID = tmp_ReportID;
                            intMAX_BL = tmp_MAX_BL;
                            break;
                        }
                    }
                }

                WriteLog("VID:PID = 0x" + int_VID.ToString("x4").ToUpper() + ":0x" + int_PID.ToString("x4").ToUpper() + ", " + "Report ID: " + byteReportID.ToString() + ", Max_BL: " + intMAX_BL.ToString());
            }

            try
            {
                //Create and start worker thread
                if (newLCDStatusThread == null)
                {
                    newLCDStatusThread = new Thread(DoWorkRefreshData);
                    newLCDStatusThread.Start();
                }
                if (newLCDStatusThread.IsAlive == false) WriteLog("Background worker thread is NOT running"); //else WriteLog("Background worker thread is running");

                //Create and start compliance thread
                if (newLCDCompliance == null)
                {
                    newLCDCompliance = new Thread(DoWorkComplianceCheck);
                    newLCDCompliance.Start();
                }
                if (newLCDCompliance.IsAlive == false) WriteLog("Background Compliance thread is NOT running"); //else WriteLog("Background compliance thread is running");
            }
            catch (Exception ex)
            {
                WriteLog("Failed to start the threads, " + ex.ToString());
            }
            
            WriteLog("All done");
        }


        private void DoWorkComplianceCheck()
        {
            WriteLog("Start of 'DoWorkComplianceCheck' thread");
            int intSleep = 2000;
            bool boolDone = true;       //All done?

            Thread.Sleep(intSleep); // Sleep so initial check can complete first. /**/ //Change to use a flag from WorkerThread instead. Sleeptimers are not reliable...

            try
            {
                while (!_shouldStopThreads)
                {
                    WriteDebugLog("Compliance Check Running in NORMAL mode: " + boolNormalMode.ToString());

                    boolDone = true;

                    //Backlight
                    int intDesired_Backlight = 0xFF;
                    if (boolNormalMode)
                    {
                        intDesired_Backlight = int.Parse(this.pluginConfig.ReadField("/APPCONFIG/BACKLIGHTNORMAL"));
                    }
                    else
                    {
                        intDesired_Backlight = int.Parse(this.pluginConfig.ReadField("/APPCONFIG/BACKLIGHTSLEEP"));
                    }

                    switch (intDesired_Backlight)
                    {
                        case 0:
                        case 1:
                            if (lcdstatus.backlight_on != intDesired_Backlight)
                            {
                                WriteLog("Non-Compliant - Backlight");
                                LCDCommand.Enqueue(LCDCommands.Toggle_Backlight);
                                boolDone = false;
                            }
                            break;
                        default:
                            //WriteLog("Do nothing with Backlight");
                            break;
                    }

                    //Brightness
                    int intDesired_Brightness = 0xFF;
                    if (boolNormalMode)
                    {
                        intDesired_Brightness = int.Parse(this.pluginConfig.ReadField("/APPCONFIG/BRIGHTNESSNORMAL"));
                    }
                    else
                    {
                        intDesired_Brightness = int.Parse(this.pluginConfig.ReadField("/APPCONFIG/BRIGHTNESSSLEEP"));
                    }

                    switch (intDesired_Brightness)
                    {
                        case 0:
                            int intDesired_BrightnessLevel = 0xFF;
                            if (boolNormalMode)
                            {
                                intDesired_BrightnessLevel = int.Parse(this.pluginConfig.ReadField("/APPCONFIG/BRIGHTNESSNORMALLEVEL"));
                            }
                            else
                            {
                                intDesired_BrightnessLevel = int.Parse(this.pluginConfig.ReadField("/APPCONFIG/BRIGHTNESSSLEEPLEVEL"));
                            }

                            byte byteTmpDesired = (byte)Math.Floor((Convert.ToDouble(intDesired_BrightnessLevel) / 100 * (double)intMAX_BL));
                            if (lcdstatus.current_backlight_level != byteTmpDesired)
                            {
                                WriteLog("Non-Compliant - Backlight Level. Current: " + lcdstatus.current_backlight_level.ToString() + ", Desired: " + byteTmpDesired.ToString());
                                LCDCommand.Enqueue(LCDCommands.Set_Brightness_Value);
                                boolDone = false;
                            }
                            goto case 1;
                        case 1:
                            if (lcdstatus.auto_brightness_on != intDesired_Brightness)
                            {
                                WriteLog("Non-Compliant - AutoBrightness");
                                LCDCommand.Enqueue(LCDCommands.Toggle_AutoBrightness);
                                boolDone = false;
                            }
                            break;
                        default:
                            //WriteLog("Do nothing");
                            break;
                    }

                    //Are we compliant?
                    if (boolDone)
                    {
                        WriteDebugLog("Compliant");
                    }

                    Thread.Sleep(intSleep); // Sleep before repeating
                }
            }
            catch (Exception ex)
            {
                WriteLog("'DoWorkComplianceCheck()' Thread Crashed, " + ex.ToString());
            }

            WriteLog("End of 'DoWorkComplianceCheck' thread");
        }


        private void DoWorkRefreshData()
        {
            WriteLog("Start of 'DoWorkRefreshData' thread");
            int intSleep = 1000;

            try
            {
                while (!_shouldStopThreads)
                {
                    WriteDebugLog("Wakeup 'DoWorkRefreshData'");

                    LCDIO();

                    //Update GUI if visible
                    if (this.Visible == true) UpdateGUI();

                    //WriteLog("'DoWorkRefreshData' going to sleep for " + intSleep + " ms");
                    if (LCDCommand.Count == 0) Thread.Sleep(intSleep); // Only sleep if no commands in queue, else sleep before repeating
                }
            }
            catch (Exception ex)
            {
                WriteLog("'DoWorkRefreshData()' Thread Crashed, " + ex.ToString());
            }

            WriteLog("End of 'DoWorkRefreshData' thread");
        }

        private void LCDIO()
        {
            //Run this by default:
            byte[] buf = { byteReportID, (byte)LCDCommands.Get_Configuration, 0 };

            //Change to this if we've got a queue of commands:
            if (LCDCommand.Count > 0)
            {
                buf[1] = (byte)LCDCommand.Dequeue();

                //Set Brightness?
                try
                {
                    if (buf[1] == (byte)LCDCommands.Set_Brightness_Value)
                    {
                        byte byteTmpDesired = (byte)Math.Floor((Convert.ToDouble(this.pluginConfig.ReadField("/APPCONFIG/BRIGHTNESSNORMALLEVEL")) / 100 * (double)intMAX_BL));
                        buf[2] = byteTmpDesired;
                    }
                }
                catch (Exception ex)
                {
                    WriteLog("Failed to convert BRIGHTNESSNORMALLEVEL, " + ex.ToString());
                }
            }

            //Echo back the command to run:
            WriteDebugLog("Command: " + buf[0].ToString() + " 0x" + buf[1].ToString("x2") + " " + buf[2].ToString());

            //Configure LCDPanel and get result
            HidDeviceData hdd = GetData(buf);

            //If Success getting data
            if (hdd != null)
            {
                try
                {
                    //Interpret data and split into single components
                    lcdstatus.backlight_on = (((hdd.Data[1] >> 7) & 1) == 1) ? 1 : 0;
                    lcdstatus.auto_brightness_on = (((hdd.Data[1] >> 6) & 1) == 1) ? 1 : 0;
                    lcdstatus.current_backlight_level = (hdd.Data[1] & 31);
                    lcdstatus.current_ambientlight_level = hdd.Data[2];
                }
                catch (Exception ex)
                {
                    WriteLog("Failed to decode return values, " + ex.ToString());
                }

                WriteDebugLog("0x" + hdd.Data[1].ToString("x2") + " 0x" + hdd.Data[2].ToString("x2") + ", " + lcdstatus.backlight_on.ToString() + " " + lcdstatus.auto_brightness_on.ToString() + " " + lcdstatus.current_backlight_level.ToString() + " " + lcdstatus.current_ambientlight_level.ToString());
            }
        }

        
        //Find a supported LCD Device
        private void FindLCDDevice()
        {
            WriteLog("FindLCDDevice() - Start");
                        
            //Enumerate all devices
            for (byte i = 0; i < LCDControl.aryLCDModels.Length; i++)
            {
                int tmp_VID = (int)(LCDControl.aryLCDModels[i][0]);
                int tmp_PID = (int)(LCDControl.aryLCDModels[i][1]);

                //All devices in system
                System.Collections.Generic.IEnumerable<HidLibrary.HidDevice> devices = HidDevices.Enumerate(tmp_VID, tmp_PID);

                //Found it?
                if (devices.Count() > 0)
                {
                    int_VID = tmp_VID;
                    int_PID = tmp_PID;
                                       
                    WriteLog("Found a supported LCD Panel, '" + (LCDControl.aryLCDModels[i][3]).ToString() + "'");
                    break;
                }
            }

            WriteLog("FindLCDDevice() - End");
            return;
        }


        private void UpdateGUI()
        {
            WriteLog("UpdateGUI() - start");
            //Update screen
            try
            {
                this.CF_updateText("lblBacklight_On",       lcdstatus.backlight_on.ToString());
                this.CF_updateText("lblAutoBrightness_On",  lcdstatus.auto_brightness_on.ToString());
                this.CF_updateText("lblBacklight_Level",    lcdstatus.current_backlight_level.ToString());
                this.CF_updateText("lblAmbientlight_Level", lcdstatus.current_ambientlight_level.ToString());
            }
            catch (Exception ex)
            {
                WriteLog("Failed to update screen with temp data, " + ex.ToString());
            }

            WriteLog("UpdateGUI() - end");
        }

        //Get data from LCD panel
        public static HidDeviceData GetData(byte[] bCommand)
        {
            try
            {
                HidDevice LCDPanel = null;      //LCDPanel to work with
                HidDeviceData hdd = null;       //Return data

                //Find LCD Panel
                try
                {
                    LCDPanel = HidDevices.Enumerate(int_VID, int_PID).FirstOrDefault();
                }
                catch (Exception ex)
                {
                    WriteLog("Failed to find LCDPanel, " + ex.ToString());
                    return null;
                }

                //Open device
                try
                {
                    LCDPanel.OpenDevice();
                }
                catch (Exception ex)
                {
                    WriteLog("Failed to open USB device, " + ex.ToString());
                    return null;
                }

                //If device is 'open'
                if (LCDPanel.IsOpen == true)
                {
                    try
                    {
                        //Send command
                        //WriteLog("Send Command : " + bCommand[0].ToString() + " " + bCommand[1].ToString() + " " + bCommand[2].ToString());
                        LCDPanel.Write(bCommand);

                        //Read response
                        HidDeviceData hddTemp = LCDPanel.Read(200);

                        //If Success getting data
                        if (hddTemp.Status == HidDeviceData.ReadStatus.Success) // && LCDHidDevice.Description != "HID Keyboard Device")
                        {
                            //Assign return variable the data
                            hdd = hddTemp;
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteLog("Failed to get data from device, " + ex.ToString());
                        return null;
                    }

                    //Close port
                    try
                    {
                        //Close device
                        LCDPanel.CloseDevice();
                    }
                    catch (Exception ex)
                    {
                        WriteLog("Failed to close USB device, " + ex.ToString());
                        return null;
                    }

                    //We made it this far, return with the data
                    //WriteLog("Data Read: " + hdd.Data.Count().ToString());
                    return hdd;
                }
                else
                {
                    WriteLog("Failed to open connection to LCDPanel");
                    return null;
                }
            }
            catch (Exception ex)
            {
                WriteLog("GetData() crashed, " + ex.ToString());
            }

            //should not get to this if successful...
            return null;
        }

        public static void WriteLog(string msg)
        {
            try
            {
                if (boolLogEvents)
                    CFTools.writeModuleLog(msg, LogFilePath);
            }
            catch { }
        }

        public static void WriteDebugLog(string msg)
        {
            try
            {
                if (boolDebugLogEvents)
                    CFTools.writeModuleLog(msg, LogFilePath);
            }
            catch { }
        }


#endregion
    }
}
