/*
 * Todo:
 * When flipping between aryBrightnessOptions, setup GUI is not refreshed
*/

using System;
using System.Windows.Forms;
using System.Xml;

using centrafuse.Plugins;
using HidLibrary;

namespace LCDControl
{
    public class Setup : CFSetup
    {
#region Variables
        private const string PluginName = "LCDControl";
        private const string PluginPath = @"plugins\" + PluginName + @"\";
        private const string PluginPathLanguages = PluginPath + @"Languages\";
        private const string ConfigurationFile = "config.xml";
        private const string ConfigSection = "/APPCONFIG/";
        private const string LanguageSection = "/APPLANG/SETUP/";
        private const string LanguageControlSection = "/APPLANG/LCDCONTROL/";
#endregion

#region Construction

        // The setup constructor will be called each time this plugin's setup is opened from the CF Setting Page
        // This setup is opened as a dialog from the CF_pluginShowSetup() call into the main plugin application form.
        public Setup(ICFMain mForm, ConfigReader config, LanguageReader lang)
        {
            // Total configuration pages for each mode
            const sbyte NormalTotalPages = 2;
            const sbyte AdvancedTotalPages = 2;

            // MainForm must be set before calling any Centrafuse API functions
            this.MainForm = mForm;

            // pluginConfig and pluginLang should be set before calling CF_initSetup() so this CFSetup instance 
            // will internally save any changed settings.
            this.pluginConfig = config;
            this.pluginLang = lang;

            // When CF_initSetup() is called, the CFPlugin layer will call back into CF_setupReadSettings() to read the page
            // Note that this.pluginConfig and this.pluginLang must be set before making this call
            CF_initSetup(NormalTotalPages, AdvancedTotalPages);
        }

#endregion

#region CFSetup
        public override void CF_setupReadSettings(int currentpage, bool advanced)
        {
            /*
             * Number of configuration pages is defined in two constsants in Setup(...)
             * const sbyte NormalTotalPages = ;
             * const sbyte AdvancedTotalPages = ;
             */

            try
            {
                int i = CFSetupButton.One;

                if (currentpage == 1)
                {
                    // Update the Settings page title
                    this.CF_updateText("TITLE", this.pluginLang.ReadField("/APPLANG/SETUP/TITLE"));

                    // TEXT BUTTONS (1-4)
                    ButtonHandler[i] = new CFSetupHandler(SetDisplayName);
                    ButtonText[i] = this.pluginLang.ReadField("/APPLANG/SETUP/DISPLAYNAME");
                    ButtonValue[i++] = this.pluginLang.ReadField("/APPLANG/LCDCONTROL/DISPLAYNAME");

                    ButtonHandler[i] = new CFSetupHandler(SetLCDDetection);
                    ButtonText[i] = this.pluginLang.ReadField("/APPLANG/SETUP/LCDDETECTION");
                    for (sbyte j = 0; j < LCDControl.aryLCDModels.Length; j++)
                    {
                        //Default if not found in array
                        ButtonValue[i] = this.pluginLang.ReadField("/APPLANG/SETUP/AUTO");
                        
                        //Is device in array?
                        if (LCDControl.aryLCDModels[j][1].ToString() == this.pluginConfig.ReadField("/APPCONFIG/PID"))
                        {
                            ButtonValue[i] = (LCDControl.aryLCDModels[j][3]).ToString();
                            break;
                        }
                    }
                    i++;

                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";

                    ButtonHandler[i] = null;
                    ButtonText[i] = this.pluginLang.ReadField("/APPLANG/SETUP/VERSIONS");
                    ButtonValue[i++] = this.pluginLang.ReadField("/APPLANG/SETUP/PLUGIN") + " v: " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString() + ". CF v: " + System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString();

                    // BOOL BUTTONS (5-8)
                    ButtonHandler[i] = new CFSetupHandler(SetLogEvents);
                    ButtonText[i] = this.pluginLang.ReadField("/APPLANG/SETUP/LOGEVENTS");
                    ButtonValue[i++] = this.pluginConfig.ReadField("/APPCONFIG/LOGEVENTS");

                    ButtonHandler[i] = new CFSetupHandler(SetDebugLogEvents);
                    ButtonText[i] = this.pluginLang.ReadField("/APPLANG/SETUP/DEBUGLOGEVENTS");
                    ButtonValue[i++] = this.pluginConfig.ReadField("/APPCONFIG/DEBUGLOGEVENTS");

                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";
                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";               
                }
                if (currentpage == 2)
                {
                    // Update the Settings page title
                    this.CF_updateText("TITLE", this.pluginLang.ReadField("/APPLANG/SETUP/LCD"));

                    // TEXT BUTTONS
                    ButtonHandler[i] = new CFSetupHandler(SetBrightnessNormal);
                    ButtonText[i] = this.pluginLang.ReadField("/APPLANG/SETUP/BRIGHTNESSNORMAL");
                    ButtonValue[i++] = LCDControl.aryBrightnessOptions[int.Parse(this.pluginConfig.ReadField("/APPCONFIG/BRIGHTNESSNORMAL"))];

                    /**/ //Not updated when SetBrightnessNormal is updated
                    ButtonHandler[i] = new CFSetupHandler(SetBrightnessNormalLevel);
                    ButtonText[i] = this.pluginLang.ReadField("/APPLANG/SETUP/BRIGHTNESSNORMALLEVEL");
                    if (this.pluginConfig.ReadField("/APPCONFIG/BRIGHTNESSNORMAL") == "0")
                    {
                        ButtonValue[i] = this.pluginConfig.ReadField("/APPCONFIG/BRIGHTNESSNORMALLEVEL");
                    }
                    else
                    {
                        ButtonValue[i] = this.pluginLang.ReadField("/APPLANG/SETUP/NAFULL");
                    }
                    i++;

                    ButtonHandler[i] = new CFSetupHandler(SetBacklightNormal);
                    ButtonText[i] = this.pluginLang.ReadField("/APPLANG/SETUP/BACKLIGHTNORMAL");
                    ButtonValue[i++] = LCDControl.aryBackLightOptions[int.Parse(this.pluginConfig.ReadField("/APPCONFIG/BACKLIGHTNORMAL"))];

                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";
                    /*
                    ButtonHandler[i] = new CFSetupHandler(SetBrightnessSleep);
                    ButtonText[i] = this.pluginLang.ReadField("/APPLANG/SETUP/BRIGHTNESSSLEEP");
                    ButtonValue[i++] = LCDControl.aryBrightnessOptions[int.Parse(this.pluginConfig.ReadField("/APPCONFIG/BRIGHTNESSSLEEP"))];

                    //Not updated when SetBrightnessSleep is updated
                    ButtonHandler[i] = new CFSetupHandler(SetBrightnessSleepLevel);
                    ButtonText[i] = this.pluginLang.ReadField("/APPLANG/SETUP/BRIGHTNESSSLEEPLEVEL");
                    if (this.pluginConfig.ReadField("/APPCONFIG/BRIGHTNESSSLEEP") == "0")
                    {
                        ButtonValue[i] = this.pluginConfig.ReadField("/APPCONFIG/BRIGHTNESSSLEEPLEVEL");
                    }
                    else
                    {
                        ButtonValue[i] = this.pluginLang.ReadField("/APPLANG/SETUP/NAFULL");
                    }
                    i++;
                    */

                    // BOOL BUTTONS
                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";
                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";
                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";
                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";               
                }
                /*
                 * if (currentpage == 3)
                {
                    // Update the Settings page title
                    this.CF_updateText("TITLE", this.pluginLang.ReadField("/APPLANG/SETUP/BACKLIGHT"));

                    // TEXT BUTTONS (1-4)
                    ButtonHandler[i] = new CFSetupHandler(SetBacklightNormal);
                    ButtonText[i] = this.pluginLang.ReadField("/APPLANG/SETUP/BACKLIGHTNORMAL");
                    ButtonValue[i++] = LCDControl.aryBackLightOptions[int.Parse(this.pluginConfig.ReadField("/APPCONFIG/BACKLIGHTNORMAL"))];

                    
                    ButtonHandler[i] = new CFSetupHandler(SetBacklightSleep);
                    ButtonText[i] = this.pluginLang.ReadField("/APPLANG/SETUP/BACKLIGHTSLEEP");
                    ButtonValue[i++] = LCDControl.aryBackLightOptions[int.Parse(this.pluginConfig.ReadField("/APPCONFIG/BACKLIGHTSLEEP"))];
                    

                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";
                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";
                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";               

                    // BOOL BUTTONS
                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";
                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";
                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";
                    ButtonHandler[i] = null; ButtonText[i] = ""; ButtonValue[i++] = "";               
                }
            */
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }

#endregion

#region User Input Events

        //Display Name
        private void SetDisplayName(ref object value)
        {
            try
            {
                object tempobject;
                string resultvalue, resulttext;

                // Display OSK for user to type display name
                if (this.CF_systemDisplayDialog(CF_Dialogs.OSK, this.pluginLang.ReadField("/APPLANG/SETUP/DISPLAYNAME"), ButtonValue[(int)value], null, out resultvalue, out resulttext, out tempobject, null, true, true, true, true, false, false, 1) == DialogResult.OK)
                {
                    this.pluginLang.WriteField("/APPLANG/LCDCONTROL/DISPLAYNAME", resultvalue);

                    // Display new value on Settings Screen button
                    ButtonValue[(int)value] = resultvalue;
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }


        private void SetLCDDetection(ref object value)
        {
            try
            {
                object tempobject;
                string resultvalue, resulttext;

                // Create a listview with the number of items in the array + 1 extra
                CFControls.CFListViewItem[] textoptions = new CFControls.CFListViewItem[LCDControl.aryLCDModels.Length + 1];

                // Populate the list with the options
                textoptions[0] = new CFControls.CFListViewItem(this.pluginLang.ReadField("/APPLANG/SETUP/AUTO"), "0", -1, false);
                for (sbyte i = 1; i <= LCDControl.aryLCDModels.Length; i++)
                {
                    CFTools.writeLog(PluginName + ": Source: '" + (LCDControl.aryLCDModels[i - 1][3]).ToString() + "'");
                    textoptions[i] = new CFControls.CFListViewItem((LCDControl.aryLCDModels[i - 1][3]).ToString(), i.ToString(), -1, false);
                }

                // Display the options
                if (this.CF_systemDisplayDialog(CF_Dialogs.FileBrowser,
                   this.pluginLang.ReadField("/APPLANG/SETUP/SOURCE"),
                   this.pluginLang.ReadField("/APPLANG/SETUP/SOURCE"),
                   ButtonValue[(int)value], out resultvalue, out resulttext, out tempobject, textoptions, true, true, true, false, false, false, 1) == DialogResult.OK)
                {
                    //Auto?
                    if (resulttext.ToUpper() == this.pluginLang.ReadField("/APPLANG/SETUP/AUTO").ToUpper())
                    {
                        this.pluginConfig.WriteField("/APPCONFIG/PID", "0");
                        this.pluginConfig.WriteField("/APPCONFIG/VID", "0");
                        ButtonValue[(int)value] = this.pluginLang.ReadField("/APPLANG/SETUP/AUTO");
                    }
                    else
                    {
                        this.pluginConfig.WriteField("/APPCONFIG/VID", (LCDControl.aryLCDModels[int.Parse(resultvalue)-1][0]).ToString());
                        this.pluginConfig.WriteField("/APPCONFIG/PID", (LCDControl.aryLCDModels[int.Parse(resultvalue)-1][1]).ToString());
                        ButtonValue[(int)value] = (LCDControl.aryLCDModels[int.Parse(resultvalue) - 1][3]).ToString();
                    }                    
                }
            }
            catch (Exception errmsg)
            {
                CFTools.writeError(PluginName + ": Failed to handle SetLCDDetection(), " + errmsg.ToString());
            }
        }

        //Enable logging
        private void SetLogEvents(ref object value)
        {
            this.pluginConfig.WriteField("/APPCONFIG/LOGEVENTS", value.ToString());
        }

        //Enable Debug logging
        private void SetDebugLogEvents(ref object value)
        {
            this.pluginConfig.WriteField("/APPCONFIG/DEBUGLOGEVENTS", value.ToString());
        }

        private void SetBrightnessNormal(ref object value)
        {
            try
            {
                object tempobject;
                string resultvalue, resulttext;

                // Create a listview
                CFControls.CFListViewItem[] textoptions = new CFControls.CFListViewItem[LCDControl.aryBrightnessOptions.Length];

                // Populate the list with the options
                for (sbyte i = 0; i < LCDControl.aryBrightnessOptions.Length; i++)
                {
                    CFTools.writeLog(PluginName + ": Option: '" + (LCDControl.aryBrightnessOptions[i]).ToString() + "'");
                    textoptions[i] = new CFControls.CFListViewItem((LCDControl.aryBrightnessOptions[i]).ToString(), i.ToString(), -1, false);
                }

                // Display the options
                if (this.CF_systemDisplayDialog(CF_Dialogs.FileBrowser,
                   this.pluginLang.ReadField("/APPLANG/SETUP/SOURCE"),
                   this.pluginLang.ReadField("/APPLANG/SETUP/SOURCE"),
                   ButtonValue[(int)value], out resultvalue, out resulttext, out tempobject, textoptions, true, true, true, false, false, false, 1) == DialogResult.OK)
                {
                    //Action?
                    this.pluginConfig.WriteField("/APPCONFIG/BRIGHTNESSNORMAL", resultvalue.ToString(), true);

                    //Update GUI
                    ButtonValue[1] = this.pluginLang.ReadField("/APPLANG/SETUP/NAFULL");
                    ButtonValue[(int)value] = resulttext;                    
                }
            }
            catch (Exception errmsg)
            {
                CFTools.writeError(PluginName + ": Failed to handle SetBrightnessNormal(), " + errmsg.ToString());
            }            
        }

        private void SetBrightnessNormalLevel(ref object value)
        {
            try
            {
                if (this.pluginConfig.ReadField("/APPCONFIG/BRIGHTNESSNORMAL") == "0")
                {
                    string resultvalue, resulttext;

                    if (this.CF_systemDisplayDialog(CF_Dialogs.NumberPad, this.pluginConfig.ReadField("/APPCONFIG/BRIGHTNESSLEVEL"), out resultvalue, out resulttext) == DialogResult.OK)
                    {
                        //Parse the value
                        int intTemp = int.Parse(resultvalue);

                        //Sanity check
                        if (intTemp < 0) intTemp = 0;
                        if (intTemp > 100) intTemp = 100;

                        //Value is scrubbed, write it
                        this.pluginConfig.WriteField("/APPCONFIG/BRIGHTNESSNORMALLEVEL", intTemp.ToString());

                        // Display new value on Settings Screen button
                        ButtonValue[(int)value] = intTemp.ToString();
                    }
                }
                else
                {
                    CF_systemDisplayDialog(CF_Dialogs.OkBox, pluginLang.ReadField("/APPLANG/SETUP/NAFULL"));
                }
            }
            catch (Exception errmsg)
            {
                CFTools.writeError(PluginName + ": Failed to handle SetBrightnessNormalLevel(), " + errmsg.ToString());
            }
        }

        private void SetBrightnessSleep(ref object value)
        {
            try
            {
                object tempobject;
                string resultvalue, resulttext;

                // Create a listview
                CFControls.CFListViewItem[] textoptions = new CFControls.CFListViewItem[LCDControl.aryBrightnessOptions.Length];

                // Populate the list with the options
                for (sbyte i = 0; i < LCDControl.aryBrightnessOptions.Length; i++)
                {
                    CFTools.writeLog(PluginName + ": Option: '" + (LCDControl.aryBrightnessOptions[i]).ToString() + "'");
                    textoptions[i] = new CFControls.CFListViewItem((LCDControl.aryBrightnessOptions[i]).ToString(), i.ToString(), -1, false);
                }

                // Display the options
                if (this.CF_systemDisplayDialog(CF_Dialogs.FileBrowser,
                   this.pluginLang.ReadField("/APPLANG/SETUP/SOURCE"),
                   this.pluginLang.ReadField("/APPLANG/SETUP/SOURCE"),
                   ButtonValue[(int)value], out resultvalue, out resulttext, out tempobject, textoptions, true, true, true, false, false, false, 1) == DialogResult.OK)
                {
                    //Action?
                    this.pluginConfig.WriteField("/APPCONFIG/BRIGHTNESSSLEEP", resultvalue.ToString(), true);

                    //Update GUI
                    ButtonValue[(int)value] = resulttext;
                }
            }
            catch (Exception errmsg)
            {
                CFTools.writeError(PluginName + ": Failed to handle SetBrightnessSleep(), " + errmsg.ToString());
            }
        }

        private void SetBrightnessSleepLevel(ref object value)
        {
            try
            {
                if (this.pluginConfig.ReadField("/APPCONFIG/BRIGHTNESSSLEEP") == "0")
                {
                    string resultvalue, resulttext;

                    if (this.CF_systemDisplayDialog(CF_Dialogs.NumberPad, this.pluginConfig.ReadField("/APPCONFIG/BRIGHTNESSLEVEL"), out resultvalue, out resulttext) == DialogResult.OK)
                    {
                        //Parse the value
                        int intTemp = int.Parse(resultvalue);

                        //Sanity check
                        if (intTemp < 0) intTemp = 0;
                        if (intTemp > 100) intTemp = 100;

                        //Value is scrubbed, write it
                        this.pluginConfig.WriteField("/APPCONFIG/BRIGHTNESSSLEEPLEVEL", intTemp.ToString());

                        // Display new value on Settings Screen button
                        ButtonValue[(int)value] = intTemp.ToString();
                    }
                }
                else
                {
                    CF_systemDisplayDialog(CF_Dialogs.OkBox, pluginLang.ReadField("/APPLANG/SETUP/NAFULL"));
                }
            }
            catch (Exception errmsg)
            {
                CFTools.writeError(PluginName + ": Failed to handle SetBrightnessSleepLevel(), " + errmsg.ToString());
            }
        }

        private void SetBacklightNormal(ref object value)
        {
            try
            {
                object tempobject;
                string resultvalue, resulttext;

                // Create a listview
                CFControls.CFListViewItem[] textoptions = new CFControls.CFListViewItem[LCDControl.aryBackLightOptions.Length];

                // Populate the list with the options
                for (sbyte i = 0; i < LCDControl.aryBackLightOptions.Length; i++)
                {
                    CFTools.writeLog(PluginName + ": Option: '" + (LCDControl.aryBackLightOptions[i]).ToString() + "'");
                    textoptions[i] = new CFControls.CFListViewItem((LCDControl.aryBackLightOptions[i]).ToString(), i.ToString(), -1, false);
                }

                // Display the options
                if (this.CF_systemDisplayDialog(CF_Dialogs.FileBrowser,
                   this.pluginLang.ReadField("/APPLANG/SETUP/SOURCE"),
                   this.pluginLang.ReadField("/APPLANG/SETUP/SOURCE"),
                   ButtonValue[(int)value], out resultvalue, out resulttext, out tempobject, textoptions, true, true, true, false, false, false, 1) == DialogResult.OK)
                {
                    //Action?
                    this.pluginConfig.WriteField("/APPCONFIG/BACKLIGHTNORMAL", resultvalue.ToString());
                    
                    //Update GUI
                    ButtonValue[(int)value] = resulttext;
                }
            }
            catch (Exception errmsg)
            {
                CFTools.writeError(PluginName + ": Failed to handle SetBacklightNormal(), " + errmsg.ToString());
            }
        }

        private void SetBacklightSleep(ref object value)
        {
            try
            {
                object tempobject;
                string resultvalue, resulttext;

                // Create a listview
                CFControls.CFListViewItem[] textoptions = new CFControls.CFListViewItem[LCDControl.aryBackLightOptions.Length];

                // Populate the list with the options
                for (sbyte i = 0; i < LCDControl.aryBackLightOptions.Length; i++)
                {
                    CFTools.writeLog(PluginName + ": Option: '" + (LCDControl.aryBackLightOptions[i]).ToString() + "'");
                    textoptions[i] = new CFControls.CFListViewItem((LCDControl.aryBackLightOptions[i]).ToString(), i.ToString(), -1, false);
                }

                // Display the options
                if (this.CF_systemDisplayDialog(CF_Dialogs.FileBrowser,
                   this.pluginLang.ReadField("/APPLANG/SETUP/SOURCE"),
                   this.pluginLang.ReadField("/APPLANG/SETUP/SOURCE"),
                   ButtonValue[(int)value], out resultvalue, out resulttext, out tempobject, textoptions, true, true, true, false, false, false, 1) == DialogResult.OK)
                {
                    //Action?
                    this.pluginConfig.WriteField("/APPCONFIG/BACKLIGHTSLEEP", resultvalue.ToString());

                    //Update GUI
                    ButtonValue[(int)value] = resulttext;
                }
            }
            catch (Exception errmsg)
            {
                CFTools.writeError(PluginName + ": Failed to handle SetBacklightSleep(), " + errmsg.ToString());
            }
        }

#endregion

    }
}
