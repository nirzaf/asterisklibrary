using AsterNET.Manager;
using AsterNET.Manager.Action;
using AsterNET.Manager.Event;
using AsterNET.Manager.Response;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SmartPABXReceptionConsole2._0
{
    /// <summary>
    /// The core object of the program, contains static members used for providing the major pieces of functionality in the program
    /// </summary>
    public static class PABX
    {
        /// <summary>
        /// The list of extensions associated with the PABX
        /// </summary>
        public static List<Extension> PABXExtensions = new List<Extension>();

        public static List<UserCallData> UserCalls;
        public static Dictionary<string, UserContactData> UserContacts;
        public static Dictionary<string, List<ExtensionGroup>> UserExtensionGroupData = new Dictionary<string, List<ExtensionGroup>>();
        public static Dictionary<string, List<Integrations.IContactIntegration>> UserIntegrationSyncs = new Dictionary<string, List<Integrations.IContactIntegration>>();
        public static List<Queue> Queues = new List<Queue>();
        public static LoginCredentials CurrentUser;
        public static List<ChannelData> ChannelData = new List<ChannelData>();
        public static Dictionary<string, string> ChannelDetails = new Dictionary<string, string>();
        public static Dictionary<string, SettingsStore> Settings = new Dictionary<string, SettingsStore>();
        public static int CallCount = 0;

        public static PABXDevConsole DevConsole = new PABXDevConsole();

        private static SmartLinkSettings _smartLink = null;
        private static string _host;

        /// <summary>
        /// The version number of the application, is applied throughout the program in places such as the settings
        /// </summary>
        /// <remarks>
        /// The version in the setup project should be the same number
        /// </remarks>
        public const string APPLICATION_VERSION = "1.2.5.0";

        /// <summary>
        /// Use IN_DEV to exclude features that may not be ready to use with standard PABX systems
        /// </summary>
        /// <remarks>
        /// IN_DEV is turned on in debug by default
        /// </remarks>
        /// <summary>
        /// The number of pages that should be on the app at a minimum (including the welcome page)
        /// </summary>
        private const int AVAILABLE_PAGES = 5;

        public static SmartLinkSettings SmartLinkConfig
        {
            get
            {
                if (_smartLink == null)
                {
                    if (File.Exists("smartlink.json"))
                        _smartLink = JsonConvert.DeserializeObject<SmartLinkSettings>(File.ReadAllText("smartlink.json"));
                    else
                        _smartLink = new SmartLinkSettings();
                }
                return _smartLink;
            }
            set
            {
                _smartLink = value;
            }
        }

        /// <summary>
        /// Find the custom branding of the SmartPABX
        /// </summary>
        public static string Branding
        {
            get
            {
                return System.Configuration.ConfigurationManager.AppSettings["branding"];
            }
        }

        /// <summary>
        /// Check settings file to see if welcome is allowed
        /// </summary>
        public static bool HasWelcome => Boolean.Parse(Settings[CurrentUser.login.ToString()].Get("show_welcome", "true"));

        /// <summary>
        /// A dynamic property which returns the required minimum amount of tabs, takes into account has welcome
        /// </summary>
        public static int MinimumTabs => HasWelcome ? AVAILABLE_PAGES : AVAILABLE_PAGES - 1;

        public static Tuple<ManagerConnection, string> TryLogin(LoginCredentials creds, bool saveToFile = true)
        {
            ManagerConnection _ami = null;
            string resultString = pabxLogin(creds.host, creds.login.ToString(), creds.password);
            Console.WriteLine(resultString);
            bool result = !resultString.ToLower().Contains("incorrect");
            if (result) // Save credentials if valid
            {
                dynamic resultJson = JObject.Parse(resultString);
                creds.ami_login = resultJson.username;
                creds.ami_password = resultJson.password;
                Console.WriteLine("Retrieved AMI login Successfully");
                SaveCreds(creds);
                //MessageBox.Show("Host: " + creds.host + " AMI Login : " + creds.ami_login + " AMI PW: " + creds.ami_password);
                _ami = new ManagerConnection(creds.host, 5038, creds.ami_login, creds.ami_password);
                _ami.Login();
                _ami.UseASyncEvents = true;
            }
            return new Tuple<ManagerConnection, string>(_ami, (result != false).ToString());
        }

        //Login method with given credentials in the login form ("bbs.voippabx.com.au")
        private static string pabxLogin(string host, string login, string password)
        {
            if (host.Contains("https://") == false)
                host = "https://" + host;
            Console.WriteLine(host + "/if/windows_auth.php?u=" + login + "&p=" + password);
            using (HttpClient n = new HttpClient())
            {
                //Async method to retrive the Authentication API Cred from windows_auth.php and assign into var jason
                var json = n.GetStringAsync(host + "/if/windows_auth.php?u=" + login + "&p=" + password);
                //Convert the json fiie into string and assign to ValueOriginal
                string valueOriginal = Convert.ToString(json.Result);
                // MessageBox.Show(host+ "/if/windows_groups.php?u="+ login + "&p=" +password+"&type=GET");
                // Console.WriteLine($"{host}/if/windows_groups.php?u={login}&p={password}&type=GET");
                _host = host;
                // MessageBox.Show(json.Result);
                return json.Result;
            }
        }

        /// <summary>
        /// Format a string to be used in an interface query
        /// </summary>
        /// <param name="route">what route will be queried</param>
        /// <param name="routeOptions">what will be appended after the query</param>
        /// <returns></returns>
        public static string FormatInterfaceQuery(string route, string routeOptions)
        {
            try
            {
                return $"{_host}/if/{route}/?u={PABX.CurrentUser.login}&p={PABX.CurrentUser.password}&{routeOptions}";
            }
            catch (System.NullReferenceException)
            {
                return "Invalid Query, not all necessary fields have been filled";
            }
        }

        public static UserCallData CurrentUserCalls
        {
            get
            {
                if (UserCalls == null)
                    UserCalls = new List<UserCallData>();
                if (UserCalls.Find(u => u.UserId == CurrentUser.login.ToString()) == null) // Create if doesn't exist
                    UserCalls.Add(new UserCallData());
                return UserCalls.Find(u => u.UserId == CurrentUser.login.ToString());
            }
        }

        public static UserContactData CurrentUserContacts
        {
            get
            {
                if (UserContacts == null)
                    UserContacts = new Dictionary<string, UserContactData>();
                if (!UserContacts.ContainsKey(CurrentUser.login.ToString()))
                    UserContacts[CurrentUser.login.ToString()] = new UserContactData();
                return UserContacts[CurrentUser.login.ToString()];
            }
        }

        private static List<ExtensionGroup> _currentUserGroups;

        public static List<ExtensionGroup> CurrentUserGroups
        {
            get
            {
                if (_currentUserGroups == null)
                    _currentUserGroups = UserExtensionGroupData.ContainsKey(CurrentUser.ami_login) ? UserExtensionGroupData[CurrentUser.ami_login] : new List<ExtensionGroup>();
                return _currentUserGroups;
            }
            set
            {
                _currentUserGroups = value;
            }
        }

        private static bool Testing = true;

        /// <summary>
        /// Generate a LoginCredentials object using the values provided
        /// </summary>
        public static LoginCredentials AssignCredentials(string host, string login, string password, bool remember)
        {
            return new LoginCredentials()
            {
                host = host,
                login = Convert.ToInt32(login),
                password = password,
                remember = remember
            };
        }

        //public static NotifyIcon SystemTray;
        public static string ExecuteAMI(Socket connectSocket)
        {
            return "Not implemented";
        }

        public static LoginCredentials LoadCreds()
        {
            return PABXFile<LoginCredentials>.LoadObjectAsync("login_details.json").Result != null ? PABXFile<LoginCredentials>.LoadObjectAsync("login_details.json").Result : new LoginCredentials();
        }

        public static int ExtensionStatusID(string statusCode)
        {
            int output = -1;
            string[] compareStatus = statusCode.Split(' ');
            switch (compareStatus[0])
            {
                case "UNKNOWN":
                    output = -1;
                    break;

                case "OK":
                    output = 0;
                    break;

                case "INUSE":
                    output = 1;
                    break;

                case "BUSY":
                    output = 2;
                    break;

                case "RINGING":
                    output = 8;
                    break;
            }
            return output;
        }

        /// <summary>
        /// Converts a status code to readable text output, returns the output
        /// </summary>
        /// <param name="ext">The extension in question</param>
        public static string FormatExtensionText(Extension ext)
        {
            string output = "Unknown";
            switch (ext.Status.ToString())
            {
                case "-1": // not foundswitc
                    output = "Not found";
                    break;

                case "0": //Idle
                    output = "Idle";
                    break;

                case "1": //In use
                    output = "In use";
                    break;

                case "2": // Busy
                    output = "Busy";
                    break;

                case "4": //Unavailable
                    output = "Unavailable";
                    break;

                case "8": //Ringing
                    output = "Ringing";
                    break;

                case "16":
                    output = "On hold";
                    break;

                default:
                    break;
            }
            return output;
        }

        public static string GenerateCallFile(LoginCredentials creds, string outgoing)
        {
            return $@"Action : Originate
                Channel: SIP/{creds.login}
                Context: bbs-pabx
                Exten: {outgoing}
                Callerid: {creds.login}
                Priority: 1";
        }

        public static OriginateAction GenerateOriginateAction(LoginCredentials creds, string outgoing)
        {
            OriginateAction temp = new OriginateAction()
            {
                Channel = $"SIP/{creds.login}",
                Context = "bbs-pabx",
                Exten = outgoing.Replace(" ", ""),
                CallerId = creds.login.ToString(),
                Priority = "1",
                Timeout = 100000,
            };
            return temp;
        }

        public static string CallStatus(LoginCredentials creds)
        {
            string status = "Unknown";
            return status;
        }

        public static List<Contact> CSVToContacts(string filepath)
        {
            List<Contact> contacts = null;
            try
            {
                string csv = File.ReadAllText(filepath);
                if (csv.Contains(','))
                {
                    contacts = new List<Contact>();
                    Console.WriteLine(csv);
                    csv = csv.Replace("\r\n", "\n").Replace("\"", "");
                    Console.WriteLine(csv);
                    List<string> rows = csv.Split('\n').ToList();
                    List<string> keys = rows[0].Split(',').ToList();
                    if (rows.Count > 2)
                        rows.Remove(rows.Last());
                    for (int i = 1; i < rows.Count; i++)
                    {
                        Dictionary<string, string> res = new Dictionary<string, string>();
                        List<string> Values = rows[i].Split(',').ToList();
                        if (Values.Count > 0)
                        {
                            for (int j = 0; j < Values.Count; j++)
                                res[keys[j]] = Values[j];
                            contacts.Add(new Contact(res));
                        }
                    }
                }
            }
            catch
            { }
            return contacts;
        }

        public static void ContactToCSV(Contact contact)
        {
            string csvData = "";
            contact.Details.Keys.ToList().ForEach(k => csvData += $"{k},");
            csvData += "\r\n";
            contact.Details.Values.ToList().ForEach(d => csvData += $"{d},");
            SaveFileDialog f = new SaveFileDialog();
            f.Filter = "Comma Seperated Values File (*.csv) | *.csv";
            var res = f.ShowDialog();
            if (res.HasValue && res.Value)
                File.WriteAllText(f.FileName, csvData);
        }

        public static int ContactID()
        {
            if (CurrentUserContacts.Count == 0)
                return 0;
            else
                return CurrentUserContacts.Contacts.Max(c => c.Id) + 1;
        }

        public async static void SaveCreds(LoginCredentials creds)
        {
            string credString = JsonConvert.SerializeObject(creds);

            if (creds.remember == true)
            {
                if (File.Exists("login_details.bbs"))              //If the file exists
                {
                    var auth = await PABXFile<List<LoginCredentials>>.LoadObjectAsync("login_details.bbs");
                    bool exists = false;

                    foreach (LoginCredentials a in auth)
                        if (a.login == creds.login)
                            exists = true;
                    if (exists == false)        //Only add the login if it doesn't already exist
                        auth.Add(creds);

                    List<LoginCredentials> l = new List<LoginCredentials>();
                    foreach (var value in auth.OrderBy(i => i.login)) //Order the list of users numerically
                        l.Add(value);

                    PABXFile<List<LoginCredentials>>.SaveObject("login_details.bbs", l);
                }
                else                                                //If the file doesn't exist create it and write everything to it
                    File.WriteAllText("login_details.bbs", BBSEncrypt("[" + credString + "]"));
            }
        }

        /// <summary>
        /// Get extensions and statuses from AMI, should only be used on startup
        /// </summary>
        /// <param name="ami"></param>
        public static void UpdateExtensionList(ManagerConnection ami, ListView extensionList)
        {
            if (PABXExtensions.Count == 0)
                PullExtensions(ami);
            PABXExtensions.ForEach(ext =>
            {
                Label extLabel = new Label();
                extLabel.Content = ext.Number.ToString();
                extLabel.PointToScreen(new Point(0, 0));
                extensionList.Items.Add(extLabel.Content);
                //extLabel.bring();
            });
        }

        public static void PullExtensions(ManagerConnection ami)
        {
            List<string> _siplines = new List<string>();
            List<string> retrievedSips = new List<string>();
            Dictionary<string, PABXExtensionStatus> pabxStatuses = new Dictionary<string, PABXExtensionStatus>();
            //get initial extension list direct from PABX
            using (HttpClient n = new HttpClient())
            {
                string downloadString = "http://" + PABX.CurrentUser.host + "/if/windows_comms.php?u=" + PABX.CurrentUser.login + "&p=" + PABX.CurrentUser.password + "&extensionstatus=1";
                Console.WriteLine(downloadString);
                try
                {
                    string json = n.GetStringAsync(downloadString).Result; // use HTTPClient instead of WebClient, avoids crashing with larger PABX's. Result is required as it is a task
                    pabxStatuses = JsonConvert.DeserializeObject<Dictionary<string, PABXExtensionStatus>>(json);
                }
                catch
                {
                    MessageBox.Show("SmartPABX not up to date, please contact system admin.", "PABX Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            PABXExtensions.Clear();
            pabxStatuses.Keys.ToList().ForEach(e =>
            {
                Extension ext = new Extension();
                ext.Number = Convert.ToInt32(e);
                ext.Comment = pabxStatuses[e].comment;
                ext.Status = Convert.ToInt32(pabxStatuses[e].status);
                ext.GroupId = Convert.ToInt32(pabxStatuses[e].groupid);
                PABXExtensions.Add(ext);
            });
            DebuggingTools.VarDump(PABXExtensions);
        }

        public static int GetExtensionStatus(ManagerConnection ami, int number)
        {
            ExtensionStateAction a = new ExtensionStateAction()
            {
                Exten = number.ToString(),
                Context = "blf",
                ActionId = "1"
            };
            int res = 4;
            ExtensionStateResponse response = null;
            try
            {
                response = (ExtensionStateResponse)ami.SendAction(a);
                res = response.Status;
            }
            catch // if the response is invalid due to a broken extension, set the value to 0
            {
                Console.WriteLine("Bad cast, " + a.Exten);
            }
            return res;
        }

        public static ChannelData TakeChannel(ManagerConnection ami, int login, bool incoming = false, string outgoing = null)
        {
            var res = ChannelData.FirstOrDefault(c => c.Channel.Contains(login.ToString()));
            if (res != null)
            {
                ChannelData.Remove(res);
                return res;
            }
            else
            {
                CommandAction coreShowChannels = new CommandAction() { Command = "core show channels" };
                var channels = (CommandResponse)ami.SendAction(coreShowChannels, 10000);
                ChannelData manualRes = new ChannelData();
                channels.Result.Dump();
                // Get a string for the channel, works for both itnernal and external numbers
                string channelLine = channels.Result.FirstOrDefault(c => c.Contains("SIP/bbs") && c.Contains("AppDial((Outgoing Line))"))
                    ?? channels.Result.FirstOrDefault(c => c.Contains("SIP/bbs") && c.Contains($"{PABX.CurrentUser.login}@bbs-pabx") && c.Contains($"Dial(SIP/{PABX.CurrentUser.login}"))
                    ?? channels.Result.FirstOrDefault(c => c.Contains(outgoing));
                try
                {
                    manualRes.Channel = channelLine.Split(' ')[0];
                }
                catch (Exception e)
                {
                    e.Warn();
                }
                return manualRes;
            }
        }

        public static bool RedirectCall(ManagerConnection ami, string destination, string channel = null)
        {
            try
            {
                RedirectAction r = new RedirectAction()
                {
                    Channel = channel,
                    Exten = destination,
                    Context = "bbs-pabx",
                    Priority = 1
                };
                r.Dump();
                var res = (ami.SendAction(r));
                return res.IsSuccess();
            }
            catch
            {
                MessageBox.Show("Failed to transfer call, sorry for any inconveniences.", "Call Transfer Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public async static Task TransferChannel(ManagerConnection ami, string channel, string destination)
        {
            await Task.Run(() =>
            {
                RedirectAction r = new RedirectAction()
                {
                    Channel = channel,
                    Exten = destination,
                    Context = "bbs-pabx",
                    Priority = 1
                };
                var res = (ami.SendAction(r));
                res.Dump();
            });
        }

        /// <summary>
        /// Park a call on a parked channel and reteurn the associated extension
        /// </summary>
        /// <param name="ami">Manager connection</param>
        /// <param name="channel">The channel to park</param>
        /// <returns></returns>
        public async static Task<int> ParkChannel(ManagerConnection ami, string channel)
        {
            var parkList = new List<int>()
            {
                701,
                702,
                703,
                704,
                705
            };
            int open = -1;
            await Task.Run(() =>
            {
                foreach (var p in parkList)
                {
                    if (GetExtensionStatus(ami, p) == 0)
                    {
                        open = p;
                        break;
                    }
                }
            });
            if (open != -1)
                PABX.RedirectCall(ami, $"{open}", channel);
            return open;
        }

        // SmartLink Data Types
        public static Dictionary<string, string> SmartLinkProviders = new Dictionary<string, string>()
        {
            ["Custom"] = "",
            ["Google Search"] = "http://google.com/search?q={{number}}",
            ["Bing"] = "http://bing.com/search?q={{number}}",
            ["AU Caller"] = "http://www.aucaller.com/{{number}}",
            ["Salesforce"] = "https://ap4.salesforce.com/   ui/search/ui/UnifiedSearchResults?searchType=2&str={{number}}"
        };

        public static void SaveCallData()
        {
            PABXFile<List<UserCallData>>.SaveObject("recent.json", UserCalls);
        }

        public static async void LoadContacts()
        {
            UserContacts = await PABXFile<Dictionary<string, UserContactData>>.LoadObjectAsync("contacts.json");
            DebuggingTools.VarDump(UserContacts);
            if (UserContacts == null)
                UserContacts = new Dictionary<string, UserContactData>();
        }

        public static void SaveContacts()
        {
            PABXFile<Dictionary<string, UserContactData>>.SaveObject("contacts.json", UserContacts);
        }

        public static RecentCall CallFromList(string refText)
        {
            string _referenceText = refText.Replace("#", "");
            string[] refArray = _referenceText.Split(' ');
            return CurrentUserCalls.RecentCalls.Find(c => c.time.ToString() == $"{refArray[0]} {refArray[1]} {refArray[2]}" && c.outgoing == refArray[4]);
        }

        /// <summary>
        /// Validates a string to ensure it is a standard phone number
        /// </summary>
        /// <param name="phonenumber"></param>
        /// <returns></returns>
        public static bool ValidateNumber(string phonenumber)
        {
            bool valid = true;
            if (phonenumber == "" || phonenumber == null)  //Making it so that a number can't be empty
                valid = false;
            else
            {
                List<char> validCharacters = new List<char>() { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '+', '*' };
                valid = !phonenumber.Trim().Equals(CurrentUser.login);
                phonenumber = phonenumber.Replace(" ", "");
                phonenumber.ToCharArray().ToList().ForEach(n =>
                {
                    if (!validCharacters.Contains(n))
                        valid = false;
                });
            }
            return valid;
        }

        /// <summary>
        /// Validate a typical contact detail to ensure it's a valid phone number.
        /// This method is safer than ValidateNumber as it uses the common word of "phone"
        /// to ensure the detail's title is referencing a phone number as well.
        /// </summary>
        /// <param name="detail">A KeyValue pair taken from contact details</param>
        /// <returns></returns>
        public static bool ValidateNumberDetail(KeyValuePair<string, string> detail)
        {
            return ValidateNumber(detail.Value) && detail.Key.ToLower().Contains("phone");
        }

        public static string GetSipChannel(ManagerConnection _ami)
        {
            MonitorAction m = new MonitorAction() { Channel = "all" };
            var res = _ami.SendAction(m);
            Console.WriteLine($"Get Sip Channel : {res.Response} : {res.Message}");
            return "works";
        }

        public static void RunSmartLink(SmartLinkOptions type, string number)
        {
            if (PABX.SmartLinkConfig.Enabled)
                try
                {
                    switch (type)
                    {
                        case SmartLinkOptions.Incoming:
                            if (PABX.SmartLinkConfig.Context == SmartLinkOptions.Incoming || PABX.SmartLinkConfig.Context == SmartLinkOptions.Both)
                                System.Diagnostics.Process.Start(PABX.SmartLinkConfig.Provider.Item2.Replace("{{number}}", number));
                            break;

                        case SmartLinkOptions.Outgoing:
                            if (PABX.SmartLinkConfig.Context == SmartLinkOptions.Outgoing || PABX.SmartLinkConfig.Context == SmartLinkOptions.Both)
                                System.Diagnostics.Process.Start(PABX.SmartLinkConfig.Provider.Item2.Replace("{{number}}", number));
                            break;

                        default:
                            break;
                    }
                }
                catch
                {
                    MessageBox.Show("SmartLink Failed, printing provider to file and turning off.");
                    File.WriteAllText("smartlink_dump.txt", PABX.SmartLinkConfig.Provider.Item2);
                    PABX.SmartLinkConfig.Enabled = false;
                }
        }

        public static void UpdateCalls(RecentCall call)
        {
            if (CurrentUserCalls.RecentCalls.Count() > 50)
                CurrentUserCalls.RecentCalls.RemoveAt(0);
        }

        public static async void LoadCalls()
        {
            if (File.Exists("recent.json"))
                UserCalls = await PABXFile<List<UserCallData>>.LoadObjectAsync("recent.json");
            else
                UserCalls = new List<UserCallData>();
        }

        /// <summary>
        /// Load Extension Groups from PABX interface
        /// </summary>
        public static async void LoadGroups()
        {
            using (HttpClient client = new HttpClient())
            {
                var response = await client.GetStringAsync(FormatInterfaceQuery("windows_groups.php", "type=get"));
                UserExtensionGroupData[CurrentUser.ami_login] = JsonConvert.DeserializeObject<List<ExtensionGroup>>(response);
            }
        }

        /// <summary>
        /// Save Extension Groups to the default file
        /// </summary>
        public static void SaveGroups()
        {
            UserExtensionGroupData[CurrentUser.ami_login] = CurrentUserGroups;
            PABXFile<Dictionary<string, List<ExtensionGroup>>>.SaveObject("groups.json", UserExtensionGroupData);
        }

        /// <summary>
        /// Update a specific extensions group on the server end
        /// </summary>
        /// <param name="ext">Extension</param>
        public async static void UpdateExtensionsGroup(Extension ext)
        {
            using (HttpClient client = new HttpClient())
            {
                string url = PABX.FormatInterfaceQuery("windows_groups.php", $"type=update&ext={ext.Number}&val={ext.GroupId}");
                await client.GetStringAsync(url);
            }
        }

        /// <summary>
        /// Encrypt a string using a static key and TripleDES
        /// </summary>
        /// <param name="data">The string that needs to be encrypted</param>
        /// <returns>Encrypted string</returns>
        public static string BBSEncrypt(string data)
        {
            if (Testing)
                return data;
            else
            {
                MD5CryptoServiceProvider hashmd5 = new MD5CryptoServiceProvider();
                byte[] keyArray = hashmd5.ComputeHash(UTF8Encoding.UTF8.GetBytes("bbspabx"));
                TripleDESCryptoServiceProvider tdes = new TripleDESCryptoServiceProvider
                {
                    Mode = CipherMode.ECB,
                    Padding = PaddingMode.PKCS7,
                    Key = keyArray
                };
                try
                {
                    ICryptoTransform cTransform = tdes.CreateEncryptor();
                    byte[] resultArray = cTransform.TransformFinalBlock(Encoding.UTF8.GetBytes(data), 0, Encoding.UTF8.GetBytes(data).Length);
                    tdes.Clear();
                    return Convert.ToBase64String(resultArray, 0, resultArray.Length);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    return ex.Message;
                }
                finally
                {
                    hashmd5.Dispose();
                    tdes.Dispose();
                }
            }
        }

        /// <summary>
        /// Descrypt a string using a static key and TripleDES
        /// </summary>
        /// <param name="data">The encrypted string</param>
        /// <returns>Decrypted string</returns>
        public static string BBSDecrypt(string data)
        {
            if (Testing)
                return data;
            else
            {
                MD5CryptoServiceProvider hashmd5 = new MD5CryptoServiceProvider();
                byte[] keyArray = hashmd5.ComputeHash(Encoding.UTF8.GetBytes("bbspabx"));
                TripleDESCryptoServiceProvider tdes = new TripleDESCryptoServiceProvider
                {
                    Mode = CipherMode.ECB,
                    Padding = PaddingMode.PKCS7,
                    Key = keyArray
                };
                ICryptoTransform cTransform = tdes.CreateDecryptor();
                try
                {
                    byte[] b = Convert.FromBase64String(data);
                    byte[] resultArrayA = cTransform.TransformFinalBlock(Convert.FromBase64String(data), 0, Convert.FromBase64String(data).Length);
                    tdes.Clear();
                    return Encoding.UTF8.GetString(resultArrayA);
                }
                catch (FormatException e)
                {
                    MessageBox.Show(e.ToString());
                    File.Delete("login_details.bbs");   //If there's an illegal character then the file is probably corrupt or hasn't been encrypted properly
                    return e.Message;
                }
                finally
                {
                    hashmd5.Dispose();
                    tdes.Dispose();
                }
            }
        }

        /// <summary>
        /// Return color based on Extension status code
        /// </summary>
        public static SolidColorBrush ColorFromStatus(int Status)
        {
            SolidColorBrush newColor = Brushes.Transparent;
            switch (Status.ToString())
            {
                case "-1": //Not found
                           //newColor = Color.FromArgb(153, 0, 0);
                    newColor = Brushes.Gray;
                    break;

                case "0": //Idle
                    newColor = Application.Current.Resources["ExtColor0"] as SolidColorBrush;
                    //newColor = Color.Blue;
                    break;

                case "1": //In use
                          //newColor = Color.FromArgb(68, 195, 68);
                    newColor = Application.Current.Resources["ExtColor1"] as SolidColorBrush;
                    break;

                case "2": //??
                          //newColor = Color.FromArgb(230, 184, 0);
                    newColor = Brushes.RoyalBlue;
                    break;

                case "4": //Unavailable
                case "48":
                    newColor = newColor = Application.Current.Resources["MainAccent"] as SolidColorBrush;

                    //newColor = Color.LightGray;
                    break;

                case "8": //Ringing
                    newColor = newColor = Application.Current.Resources["ExtColor8"] as SolidColorBrush;
                    //newColor = Color.Green;
                    break;

                default:
                    break;
            }
            return newColor;
        }

        public static void UpdateContacts(Contact contact)
        {
            CurrentUserContacts.Add(contact);
        }

        public static List<Integrations.IContactIntegration> CurrentUserSyncs
        {
            get
            {
                if (UserIntegrationSyncs.ContainsKey(CurrentUser.login.ToString()))
                    return UserIntegrationSyncs[CurrentUser.login.ToString()];
                else
                    return new List<Integrations.IContactIntegration>();
            }
        }

        public async static Task LoadSyncAsync()
        {
            if (File.Exists($"integrations_{CurrentUser.login}.json"))
            {
                var integrationList = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText($"integrations_{CurrentUser.login}.json"));
                UserIntegrationSyncs[CurrentUser.login.ToString()] = new List<Integrations.IContactIntegration>();
                integrationList.ForEach(i =>
                {
                    switch (i)
                    {
                        case "Microsoft/Office 365 Account":
                            UserIntegrationSyncs[CurrentUser.login.ToString()].Add(new Integrations.Office365Client());
                            break;

                        case "Google Account":
                            UserIntegrationSyncs[CurrentUser.login.ToString()].Add(new Integrations.GoogleClient());
                            break;

                        case "Outlook Application":
                            UserIntegrationSyncs[CurrentUser.login.ToString()].Add(new Integrations.OutlookClient());
                            break;

                        default:
                            break;
                    }
                });
                // import contacts but only if there have been changes made
                foreach (var s in CurrentUserSyncs)
                    await s.SyncContacts();
            }
        }

        public static void SaveSyncs(List<string> _integrations)
        {
            File.WriteAllText($"integrations_{CurrentUser.login}.json", JsonConvert.SerializeObject(_integrations));
        }

        public static void SaveSmartLinks()
        {
            PABXFile<SmartLinkSettings>.SaveObject("smartlink.bbsconf", SmartLinkConfig);
        }

        public async static void LoadSmartLinks()
        {
            if (PABXFile<SmartLinkSettings>.Exists("smartlink.bbsconf"))
                SmartLinkConfig = await PABXFile<SmartLinkSettings>.LoadObjectAsync("smartlink.bbsconf");
        }

        /// <summary>
        /// Save credentials, calls, user details and extension groups
        /// </summary>
        public static void SaveAll()
        {
            PABX.SaveCreds(PABX.CurrentUser);
            PABX.SaveSettings();
            PABX.SaveContacts();
            PABX.SaveCallData();
            PABX.SaveGroups();
            PABX.SaveSmartLinks();
        }

        /// <summary>
        /// Load credentials, calls, user details and extension groups
        /// </summary>
        public static void LoadAll(bool includeCreds = false)
        {
            if (includeCreds)
                PABX.LoadCreds();
            PABX.LoadSettings();
            PABX.LoadContacts();
            PABX.LoadCalls();
            PABX.LoadGroups();
            PABX.LoadSmartLinks();
        }

        public static SettingsStore CurrentUserSettings => Settings[CurrentUser.login.ToString()];

        public static async void LoadSettings()
        {
            Settings = await PABXFile<Dictionary<string, SettingsStore>>.LoadObjectAsync("config.bbs") ?? new Dictionary<string, SettingsStore>();
            if (!Settings.ContainsKey(CurrentUser.login.ToString()))
                Settings[CurrentUser.login.ToString()] = new SettingsStore();
#if DEBUG
            Settings.Dump();
#endif
        }

        /// <summary>
        /// Save the settings object to a .bbs file
        /// </summary>
        public static void SaveSettings()
        {
            PABXFile<Dictionary<string, SettingsStore>>.SaveObject("config.bbs", Settings);
#if DEBUG
            Settings.Dump();
#endif
        }

        /// <summary>
        /// Dispose of all userdata for safe log outs
        /// </summary>
        public static void DisposeUserData()
        {
            PABX.CurrentUser = null;
            PABX.PABXExtensions = new List<Extension>();
            PABX.SmartLinkConfig = new SmartLinkSettings();
            PABX.UserIntegrationSyncs = new Dictionary<string, List<Integrations.IContactIntegration>>();
        }

        public static async Task LoadAllASync(bool includeCreds = false)
        {
            await Task.Run(() => LoadAll(includeCreds));
        }

        /// <summary>
        /// Take the generic login error of the interface and make it more user friendly
        /// </summary>
        /// <param name="loginError">The error message provided by the site</param>
        /// <returns></returns>
        public static string FormatLoginError(string loginError)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine(loginError);
#endif
            string res = "Login error occured, please ensure all details are correct";
            if (loginError.ToLower().Contains("username/password"))
                res = "Username or password incorrect.";
            if (loginError.ToLower().Contains("object reference"))
                res = "Error downloading user information, please ensure your PABX is updated to the latest version and you have a web password for this extension.";
            return res;
        }

        /// <summary>
        /// Extensions but ordered by extensions with notes first
        /// </summary>
        public static List<Extension> PABXExtensionsNoteSorted => (PABX.PABXExtensions.Where(x => x.NotesValid.Count > 0) ?? new List<Extension>()).OrderByDescending(x => x.NotesValid.Count).ToList();

        // Queue Functionality testing
        public static void TestQueueQuery(ManagerConnection ami)
        {
            ResponseEvents re;
            try
            {
                re = ami.SendEventGeneratingAction(new QueueStatusAction());
            }
            catch (EventTimeoutException e)
            {
                re = e.PartialResult;
            }
            DebuggingTools.Dump("Starting");
            foreach (ManagerEvent ev in re.Events)
            {
                if (ev is QueueParamsEvent)
                    ((QueueParamsEvent)ev).Dump();
                if (ev is QueueMemberEvent)
                    ((QueueMemberEvent)ev).Dump();
                if (ev is QueueEntryEvent)
                    ((QueueEntryEvent)ev).Dump();
            }
            DebuggingTools.Dump("Finished");
        }

        public async static Task PullQueueData()
        {
            Queues = await REST<List<Queue>>.Get(FormatInterfaceQuery("windows_queues.php", "type=get"));
        }

        /// <summary>
        /// Generate a collection of parking data
        /// </summary>
        public static IEnumerable<ParkingSpot> PullParkingData(ManagerConnection ami)
        {
            for (int i = 701; i < 706; i++)
            {
                ExtensionStateAction action = new ExtensionStateAction()
                {
                    Exten = i.ToString(),
                    Context = "blf",
                    ActionId = "1"
                };
                ExtensionStateResponse res = null;
                try
                {
                    res = (ExtensionStateResponse)ami.SendAction(action);
                }
                catch (Exception e)
                {
                    e.Warn();
                }
                if (res != null)
                    yield return new ParkingSpot(i.ToString()) { InUse = res.Status == 1 };
            }
        }

        /// <summary>
        /// A reference to the current call in progress
        /// </summary>
        public static Controls.CallControl CurrentCallWindow = null;

        /// <summary>
        /// A simple property to see if a call control is currently opened
        /// </summary>
        public static bool InCall => CurrentCallWindow != null;
    }
}