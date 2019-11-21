using SmartPABXReceptionConsole2._0.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace SmartPABXReceptionConsole2._0
{
    /// <summary>
    /// The type of setting
    /// </summary>
    public enum SettingType
    {
        /// <summary>
        /// Text input
        /// </summary>
        Text,

        /// <summary>
        /// Checkbox input
        /// </summary>
        Check,

        /// <summary>
        /// A "link" input which displays underline text that runs a closure upon click
        /// </summary>
        Link
    }

    /// <summary>
    /// A generic setting model used for easily mapping out the configuration of the program
    /// </summary>
    public class GenericSetting
    {
        public Tuple<string, string> NameValues;
        public SettingType Type = SettingType.Text;
        public Action Action = null;
    }

    /// <summary>
    /// The storage class for all PABX settings
    /// </summary>
    /// <remarks>
    /// This object is mainly used to populate the settings menu
    /// </remarks>
    public static class PABXSettings
    {
        public static List<GenericSetting> GeneralSettings = new List<GenericSetting>()
        {
            new GenericSetting()
            {
                NameValues = new Tuple<string, string>("show_welcome", "Show Welcome Page On Startup*"),
                Type = SettingType.Check
            },
            new GenericSetting()
            {
                NameValues = new Tuple<string, string>("welcome_message", "Welcome Message*"),
                Type = SettingType.Text
            },
            new GenericSetting()
            {
                NameValues = new Tuple<string, string>("View Administration Console", ""),
                Type = SettingType.Link,
                Action = new Action(() => System.Diagnostics.Process.Start($"https://{PABX.CurrentUser.host}"))
            },
            // Developer Settings
            new GenericSetting()
            {
                NameValues = new Tuple<string, string>("f12_devmenu", "Open Developer Log with F12"),
                Type = SettingType.Check
            },
            new GenericSetting()
            {
                NameValues = new Tuple<string, string>("startup_devmenu", "Open Developer Log on Startup"),
                Type = SettingType.Check
            }
        };

        public static List<GenericSetting> CallSettings = new List<GenericSetting>()
        {
            new GenericSetting()
            {
                NameValues = new Tuple<string, string>("focus_call", "Auto Focus on New Call Tabs"),
                Type = SettingType.Check
            },
            new GenericSetting()
            {
                NameValues = new Tuple<string, string>("autoclose_call", "Close Call Window on Hangup"),
                Type = SettingType.Check
            },
            new GenericSetting()
            {
                Type = SettingType.Link,
                NameValues = new Tuple<string, string>("Configure SmartLinks", ""),
                Action = new Action(() => new SmartLinkConfigForm(new SmartLinkProviderViewModel(PABX.SmartLinkProviders.First())).ShowDialog())
            }
        };

        /// <summary>
        /// Populate a settings stackpanel using a settings store, often contained in the static class PABXSettings
        /// </summary>
        /// <param name="store">A collection of Generic Settings used to fill the panel</param>
        /// <param name="output">A stackpanel which is filled with the settings data</param>
        public static void PopulateSettingStack(IEnumerable<GenericSetting> store, StackPanel output)
        {
            if (store != null && output != null)
            {
                foreach (var s in store)
                {
                    switch (s.Type)
                    {
                        case SettingType.Check:
                            output.Children.Add(new SettingsCheckControl(new SettingCheckViewModel(s.NameValues)));
                            break;

                        case SettingType.Link:
                            output.Children.Add(new SettingsLinkControl(s.NameValues.Item1, s.Action));
                            break;

                        default:
                            output.Children.Add(new SettingsTextControl(new SettingTextViewModel(s.NameValues)));
                            break;
                    }
                }
            }
        }
    }
}