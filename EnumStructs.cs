/*
 * Strucs and enums
*/

namespace LCDControl
{
    public enum LCDCommands : byte
    {
        Get_Configuration       =   0,
        Toggle_AutoBrightness   =   2,
        Set_Brightness_Max      =   4,
        Set_Brightness_Min      =   8,
        Toggle_Backlight        =  16,
        Set_Brightness_Value    =  32,
        Dec_Brightness          =  64,
        Inc_Brightness          = 128
    }


    //Attributes of the current status of the LCD Panel
    class LCDStatus
    {
        public int backlight_on { get; set; }              // Backlight on or off
        public int auto_brightness_on { get; set; }        // Auto brightness on or off
        public int current_backlight_level { get; set; }    // Current backlight level
        public int current_ambientlight_level { get; set; } // Current ambientlight level
    }
}
