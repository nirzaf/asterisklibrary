using AsterNET.Manager;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace SmartPABXReceptionConsole2._0
{
    // Model for Login Credentials
    public class LoginCredentials
    {
        public string host { get; set; }
        public int login { get; set; }
        public string password { get; set; }
        public string ami_login { get; set; }
        public string ami_password { get; set; }
        public bool remember { get; set; }

        public bool available
        {
            get
            {
                if (PABX.PABXExtensions != null && PABX.PABXExtensions.Find(x => x.Number == login) != null)
                    return PABX.PABXExtensions.Find(x => x.Number == login).Available;
                else
                    return false;
            }
        }

        /// <summary>
        /// Ensure all required fields for login are filled
        /// </summary>
        public bool IsValid => host != null && host != "" && login.ToString() != null && host != "" && password != null && password != "";

        /// <summary>
        /// Alias for the login
        /// </summary>
        public int Extensioncode => login;
    }

    // Model for Extension
    public class Extension : ViewModel
    {
        public Extension()
        {
            Status = '0';
        }

        private int _number = -1;

        public int Number
        {
            get
            {
                return _number;
            }
            set
            {
                if (_number != value)
                {
                    _number = value;
                    NotifyPropertyChanged("Number");
                    // Load notes for that particular extension
                    LoadNotesAsync();
                    NotifyPropertyChanged("Notes");
                }
            }
        }

        private int _status;

        public int Status
        {
            get
            {
                return _status;
            }
            set
            {
                if (_status != value)
                {
                    _status = value;
                    NotifyPropertyChanged("Status"); // Update all status related properties with the status change
                    NotifyPropertyChanged("StatusText");
                    NotifyPropertyChanged("StatusVerbose");
                    NotifyPropertyChanged("StatusColor");
                }
            }
        }

        private int _groupId = 0;

        public int GroupId
        {
            get { return _groupId; }
            set
            {
                if (_groupId != value)
                {
                    _groupId = value;
                }
            }
        }

        public SolidColorBrush StatusColor
        {
            get
            {
                return PABX.ColorFromStatus(Status);
            }
        }

        public string Comment { get; set; }

        public string StatusVerbose
        {
            get
            {
                return $"Status : {PABX.FormatExtensionText(this)}";
            }
        }

        public string StatusText
        {
            get
            {
                return PABX.FormatExtensionText(this);
            }
        }

        public string FullTitle
        {
            get
            {
                return $"{Comment} ({Number})";
            }
        }

        public bool Available
        {
            get
            {
                return Status == 0;
            }
        }

        private List<ConsoleNote> _notes = new List<ConsoleNote>();

        /// <summary>
        /// Select all notes no matter if they're valid
        /// </summary>
        public IEnumerable<ConsoleNoteViewModel> NotesAll => _notes.Count > 0 ? _notes.Select(n => n.ViewModel) : new List<ConsoleNoteViewModel>();

        /// <summary>
        /// Select notes that are valid
        /// </summary>
        public List<ConsoleNoteViewModel> NotesValid => new List<ConsoleNoteViewModel>(NotesAll.Where(n => n.End.Date >= DateTime.Now.Date));

        public List<ConsoleNote> NoteSource
        {
            get => _notes;
            private set
            {
                if (value != _notes && value != null && value.Count != 0)
                {
                    _notes = value;
                    NotifyPropertyChanged("NotesAll");
                    NotifyPropertyChanged("NotesValid");
                    NotifyPropertyChanged("ShowNotes");
                    NotifyPropertyChanged("NotesHeading");
                }
            }
        }

        public Visibility ShowNotes => NotesValid.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        public string NotesHeading => NotesValid.Count > 0 ? "Recent Notes" : "Nothing To Show";

        private const bool NOTES_ENABLED = false;

        /// <summary>
        /// Load the notes for the particular extension
        /// </summary>
        public async void LoadNotesAsync(MainWindow window = null)
        {
            if (Number == -1)
                await Task.Delay(100);
            string extAddition = $"&ext={_number}";
            NoteSource = await REST<List<ConsoleNote>>.Get($"http://{PABX.CurrentUser.host}/if/windows_notes.php?u={PABX.CurrentUser.login}&p={PABX.CurrentUser.password}&type=GET{extAddition}");
            if (window != null)
                window.Dispatcher.Invoke(() => window.UpdateNoteSource());
        }
    }

    /// <summary>
    /// Extension groups are the same as the group system on the SmartPABX admin
    /// </summary>
    public class ExtensionGroup : ViewModel
    {
        private int _id;

        public int Id
        {
            get { return _id; }
            set
            {
                if (_id != value)
                {
                    _id = value;
                    NotifyPropertyChanged("GroupId");
                }
            }
        }

        private string _name;

        public string Name
        {
            get { return _name; }
            set
            {
                if (_name != value)
                {
                    _name = value;
                    NotifyPropertyChanged("Name");
                }
            }
        }
    }

    // Model for recent calls
    public class RecentCall : ViewModel
    {
        public string incoming { get; set; }
        public string outgoing { get; set; }
        public DateTime time { get; set; }
        private DateTime _duration = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day);

        public DateTime duration
        {
            get
            {
                return _duration;
            }
            set
            {
                if (_duration != value)
                {
                    _duration = value;
                    NotifyPropertyChanged("duration");
                    NotifyPropertyChanged("DurationFormatted");
                }
            }
        }

        public string DurationFormatted
        {
            get
            {
                return _duration.ToString("mm:ss");
            }
        }

        private string _notes = "";

        public string notes
        {
            get
            {
                return _notes;
            }
            set
            {
                if (_notes != value)
                {
                    _notes = value;
                    NotifyPropertyChanged("notes");
                }
            }
        }

        [JsonIgnore]
        public string Channel { get; set; }

        /// <summary>
        /// Run a smartlink if enabled
        /// </summary>
        public void SmartLink(SmartLinkOptions options = SmartLinkOptions.Both)
        {
            if (PABX.SmartLinkConfig.Enabled)
            {
                string link = PABX.SmartLinkConfig.Provider.Item2.Replace("{{number}}", outgoing);
                if (PABX.SmartLinkConfig.Context == SmartLinkOptions.Both || PABX.SmartLinkConfig.Context == options)
                    System.Diagnostics.Process.Start(link);
            }
        }
    }

    public class ConsoleNote
    {
        public int Id { get; set; }
        public int Extension { get; set; }
        public DateTime StartingTime { get; set; }
        public DateTime EndingTime { get; set; }
        public string Note { get; set; }

        [JsonIgnore]
        public ConsoleNoteViewModel ViewModel => new ConsoleNoteViewModel() { Note = this };
    }

    /// <summary>
    /// Model for downloading extension data from the PABX
    /// </summary>
    public class PABXExtensionStatus
    {
        public string comment { get; set; }
        public string status { get; set; }
        public string groupid { get; set; }
    }

    /// <summary>
    /// The model for enabling Smarlink functionality within the program
    /// </summary>
    public class SmartLinkSettings
    {
        public bool Enabled = false;
        public Tuple<string, string> Provider { get; set; }
        public SmartLinkOptions Context { get; set; }
        public bool AutoOpen = false;
    }

    // Class for CSV Importing
    public class Contact : ViewModel, INotifyCollectionChanged
    {
        public int Id { get; set; }
        private Dictionary<string, string> _details;

        public Dictionary<string, string> Details
        {
            get { return _details; }
        }

        [JsonIgnore]
        /// <summary>
        /// Retrieve details that are not empty nor null
        /// </summary>
        public Dictionary<string, string> DetailsFilled
        {
            get
            {
                return _details.Where(c => c.Value != "" && c.Value != null).ToDictionary(d => d.Key, d => d.Value);
            }
        }

        private bool _pinned = false;

        public bool Pinned
        {
            get
            {
                return _pinned;
            }
            set
            {
                if (value != _pinned)
                {
                    _pinned = value;
                    NotifyPropertyChanged("Pinned");
                }
            }
        }

        [JsonIgnore]
        public Visibility PinnedVisibility
        {
            get
            {
                return _pinned ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public Contact(Dictionary<string, string> details)
        {
            Id = PABX.ContactID();
            _details = details;
        }

        public void AddOrUpdate(KeyValuePair<string, string> data)
        {
            if (_details.ContainsKey(data.Key))
                _details[data.Key] = data.Value;
            else
                _details.Add(data.Key, data.Value);
            NotifyPropertyChanged("Details");
            NotifyPropertyChanged("Key");
            NotifyPropertyChanged("Value");
        }

        [JsonIgnore]
        public List<String> DetailsFormatted
        {
            get
            {
                var res = new List<string>();
                foreach (var item in Details)
                    res.Add($"{item.Key}: {item.Value}");
                return res;
            }
        }

        public string SafeDetail(string key)
        {
            if (_details.ContainsKey(key) && _details[key] != null)
                return _details[key];
            else
                return "";
        }

        [JsonIgnore]
        public string Name
        {
            get
            {
                string fullName = "";
                if (SafeDetail("Middle Name") == "")
                    fullName = $"{SafeDetail("First Name")} {SafeDetail("Last Name")}";
                else
                    fullName = $"{SafeDetail("First Name")} {SafeDetail("Middle Name")} {SafeDetail("Last Name")}";
                //fullName = fullName.Replace("  ", "");    //Why on earth is this here? It doesn't display well
                return fullName;
            }
        }

        [JsonIgnore]
        public List<string> PhoneNumbers => Details.Where(n => PABX.ValidateNumberDetail(n)).Select(p => p.Value).ToList();

        public Integrations.ContactSource Source = Integrations.ContactSource.Original;

        public event NotifyCollectionChangedEventHandler CollectionChanged;
    }

    public enum SmartLinkOptions
    {
        Incoming,
        Outgoing,
        Both
    };

    /// <summary>
    /// Simple settings store class
    /// </summary>
    public class SettingsStore
    {
        [JsonProperty]
        private Dictionary<string, string> _values { get; }

        public string Get(string key, string defaultValue = "")
        {
            if (!_values.ContainsKey(key) || _values[key] == "")
                _values[key] = defaultValue;
            return _values[key];
        }

        public void Set(string key, string value) => _values[key] = value;

        public SettingsStore(Dictionary<string, string> values)
        {
            _values = values;
        }

        public SettingsStore() : this(new Dictionary<string, string>())
        {
        }
    }

    /// <summary>
    /// A class used to safely open and close files relating to the SmartPABX
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class PABXFile<T>
    {
        private readonly static string _dir = "";

        /// <summary>
        /// Load an object from a file aysnchronously, removing the file if the data
        /// is invalid.
        /// </summary>
        /// <param name="filename">The file to load, excluding the default directory</param>
        /// <returns></returns>
        public async static Task<T> LoadObjectAsync(string filename)
        {
            try
            {
                if (Exists(filename))
                    return await Task.Run(() => JsonConvert.DeserializeObject<T>(PABX.BBSDecrypt(File.ReadAllText($"{_dir}{filename}"))));
                else
                    return default;
            }
            // If data is invalid, remove the file
            catch
            {
                MessageBox.Show($"Error loading {filename}, removing file...");
                File.Delete($"{_dir}{filename}");
                return default;
            }
        }

        public static void SaveObject(string filename, T obj)
        {
            var output = $"{_dir}{filename}";
            File.WriteAllText(output, PABX.BBSEncrypt(JsonConvert.SerializeObject(obj)));
        }

        public static bool Exists(string filename)
        {
            return File.Exists($"{_dir}{filename}");
        }
    }

    public class UserCallData
    {
        public string UserId;
        public List<RecentCall> RecentCalls = new List<RecentCall>();

        public UserCallData(string id, RecentCall call = null)
        {
            if (call != null)
                RecentCalls.Add(call);
            UserId = id;
        }

        public UserCallData() : this(PABX.CurrentUser.login.ToString())
        {
        }

        public int Count
        {
            get
            {
                return RecentCalls.Count();
            }
        }

        public void Add(RecentCall r)
        {
            if (!RecentCalls.Contains(r))
                RecentCalls.Add(r);
        }
    }

    public class UserContactData
    {
        public List<Contact> Contacts = new List<Contact>();

        public UserContactData(Contact contact = null)
        {
            if (contact != null)
                Contacts.Add(contact);
        }

        [JsonIgnore]
        public int Count
        {
            get
            {
                return Contacts.Count;
            }
        }

        public void Add(Contact c)
        {
            Contacts.Add(c);
        }
    }

    public class TaskManager : IDisposable
    {
#if DEBUG

        // Use a faster sync rate for debugging
        public const int REFRESH_RATE_INTEGRATIONS = 60000;

#else
        public const int REFRESH_RATE_INTEGRATIONS = 1800000;
#endif

        private int _failures = 0;
        private const int MAX_FAILURES = 5;

        //public const int REFRESH_RATE_INTEGRATIONS = 10600;
        private CancellationTokenSource _token = new CancellationTokenSource();

        /// <summary>
        /// Start integration sync
        /// </summary>
        public async void StartIntegrationTaskAsync(MainWindow _parent)
        {
            await Task.Run(async () =>
            {
                while (!_token.IsCancellationRequested)
                {
                    await Task.Delay(REFRESH_RATE_INTEGRATIONS, _token.Token);
                    try
                    {
                        await PABX.LoadSyncAsync();
                        Console.WriteLine("Synced...");
                        _parent.Dispatcher.Invoke(() => _parent.ReloadContacts());
                    }
                    catch (Exception e)
                    {
                        _failures++;
                        Console.WriteLine($"Background Sync failed {_failures} time/s...");
                        Console.WriteLine($"Exception in {e.Source}, {e.Message}");
                        if (_failures >= MAX_FAILURES)
                            this.Dispose();
                    }
                }
            });
        }

        public void Dispose()
        {
            _token.Cancel();
            Console.WriteLine("Disposing Sync Manager");
        }
    }

    /// <summary>
    /// Channel Data Model
    /// </summary>
    public class ChannelData
    {
        public string Channel { get; set; }
        public string Number { get; set; }
    }

    /// <summary>
    /// A simple REST Interaction object that works with C# types
    /// </summary>
    /// <typeparam name="T">The type which the REST client is dealing with</typeparam>
    public static class REST<T>
    {
        /// <summary>
        /// Serialize an HTTP GET request as a C# object
        /// </summary>
        /// <param name="route">The URI being queried</param>
        /// <param name="notify">If true, a messagebox with error details will be thrown if a failure occurs</param>
        /// <returns>The serialized result of the REST query</returns>
        public async static Task<T> Get(string route, bool notify = false)
        {
            string response = "";
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    response = await client.GetStringAsync(route);
#if DEBUG
                    response.Dump();
#endif
                    return JsonConvert.DeserializeObject<T>(response);
                }
            }
            catch (Exception e)
            {
                if (notify)
                    e.Warn();
                return default(T);
            }
        }

        /// <summary>
        /// Generic task used for posting data to a restfull API
        /// </summary>
        /// <param name="route">The url to post to including route parameters</param>
        /// <param name="data">The object to be serialized</param>
        /// <param name="headers">Any additional headers</param>
        /// <returns>HttpReponseMessage of the post</returns>
        public async static Task<HttpResponseMessage> Post(string route, T data, Dictionary<string, string> headers = null)
        {
            using (HttpClient client = new HttpClient())
            {
                headers = headers ?? new Dictionary<string, string>();
                foreach (KeyValuePair<string, string> h in headers)
                    client.DefaultRequestHeaders.Add(h.Key, h.Value);
                //client.DefaultRequestHeaders.Add("Content-Type", "application/json");
                return await client.PostAsync(route, new StringContent(JsonConvert.SerializeObject(data)));
            }
        }

        /// <summary>
        /// Generic task used for putting/updating data on a restfull API
        /// </summary>
        /// <param name="route">The url to post to including route parameters</param>
        /// <param name="data">The object to be serialized</param>
        /// <param name="headers">Any additional headers</param>
        /// <returns>HttpReponseMessage of the post</returns>
        public async static Task<HttpResponseMessage> Put(string route, T data, Dictionary<string, string> headers = null)
        {
            using (HttpClient client = new HttpClient())
            {
                headers = headers ?? new Dictionary<string, string>();
                foreach (KeyValuePair<string, string> h in headers)
                    client.DefaultRequestHeaders.Add(h.Key, h.Value);
                //client.DefaultRequestHeaders.Add("Content-Type", "application/json");
                return await client.PutAsync(route, new StringContent(JsonConvert.SerializeObject(data)));
            }
        }
    }

    public class Queue
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Strategy { get; set; }
        public string Context { get; set; }
        public int Timeout { get; set; }
        public string WrapUpTime { get; set; }
        public string AnnounceFrequency { get; set; }
        public string PeriodicAnnounce { get; set; }
        public string PeriodicAnnounceFrequency { get; set; }
        public string JoinEmpty { get; set; }
        public string Members { get; set; }
        public string InUse { get; set; }
        public int ServiceLevel { get; set; }
        public string MaxJoinTime { get; set; }

        [JsonProperty("login_code")]
        public string LoginCode { get; set; }

        [JsonProperty("logout_code")]
        public string LogoutCode { get; set; }

        public string MOH { get; set; }
        public string Announce { get; set; }

        [JsonProperty("moh_group")]
        public string MOHGroup { get; set; }
    }

    /// <summary>
    /// Queue Stats entry defines a model to be used for serializing entries when calling
    /// windows_queuedetailedstats.php
    /// </summary>
    public class QueueStatsEntry
    {
        public string QueueName { get; set; }
        public int Position { get; set; }
        public string Channel { get; set; }
        public string UniqueId { get; set; }
        public string CallerId { get; set; }
        public string CallerIdName { get; set; }
        public string ConnectedLineNum { get; set; }
        public int Wait { get; set; }
    }

    /// <summary>
    /// Used for managing the polling of the data
    /// </summary>
    public class PABXPollingManager : ViewModel
    {
        // Long Running task support
        private const int REFRESH_QUEUE_RATE = 5000;

        private CancellationTokenSource _token = new CancellationTokenSource();
        private MainWindow _main;
        private ManagerConnection _ami;

        public PABXPollingManager(MainWindow main, ManagerConnection ami)
        {
            _main = main;
            _ami = ami;
            LoadEntries();
            MaintainEntriesAsync();
        }

        private ObservableCollection<QueueStatsEntry> _entries = new ObservableCollection<QueueStatsEntry>();

        public ObservableCollection<QueueStatsEntry> Entries
        {
            get => _entries;
            set
            {
                if (value != _entries)
                {
                    _entries = value;
                    NotifyPropertyChanged("Entries");
                    foreach (var q in _main.QueueViewModels)
                        q.Entries = new ObservableCollection<QueueStatsEntry>(_entries.Where(e => e.QueueName == q.Name).OrderBy(e => e.Position).ToArray() ?? new QueueStatsEntry[0]);
                }
            }
        }

        /// <summary>
        /// A background task which will poll the queues every 5 seconds if the workplace tab is in view
        /// </summary>
        public async void MaintainEntriesAsync()
        {
            await Task.Run(async () =>
            {
                while (!_token.IsCancellationRequested)
                {
                    await Task.Delay(REFRESH_QUEUE_RATE, _token.Token);
                    try
                    {
                        if (PABX.CurrentUser != null)
                            _main.Dispatcher.Invoke(() =>
                            {
                                // Only update if main is in view and the workplace tab is selected
                                if (_main.MenuTab.SelectedIndex == 4 && _main.WindowState != WindowState.Minimized && _main.Visibility == Visibility.Visible)
                                {
                                    LoadEntries();
                                    LoadParkData();
                                }
                            });
                        else
                        {
                            // Do not run this polling device if logged out
                            throw new Exception("Logged out");
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Exception in {e.Source}, {e.Message}");
                        _token.Cancel();
                    }
                }
            });
        }

        /// <summary>
        /// Load QueueEntries from windows interface
        /// </summary>
        public async void LoadEntries()
        {
            Entries = new ObservableCollection<QueueStatsEntry>(await REST<IEnumerable<QueueStatsEntry>>.Get(PABX.FormatInterfaceQuery("windows_queuedetailedstats.php", "type=get&filter=entries")) ?? new QueueStatsEntry[0]);
        }

        public void LoadParkData()
        {
            _main.ParkingSpots.Clear();
            try
            {
                foreach (var p in PABX.PullParkingData(_ami))
                {
                    _main.ParkingSpots.Add(p);
                }
            }
            catch
            {
                _token.Cancel();
            }
        }
    }

    /// <summary>
    /// A class for encompassing parking logic
    /// </summary>
    public class ParkingSpot : ViewModel
    {
        private string _number;

        /// <summary>
        /// The extension number
        /// </summary>
        public string Number
        {
            get { return _number; }
            set
            {
                if (_number != value)
                {
                    _number = value;
                    NotifyPropertyChanged("Number");
                }
            }
        }

        private bool _inuse = false;

        /// <summary>
        /// Determines whether the ParkingSpot is available
        /// </summary>
        public bool InUse
        {
            get { return _inuse; }
            set
            {
                if (_inuse != value)
                {
                    _inuse = value;
                    NotifyPropertyChanged("InUse");
                    NotifyPropertyChanged("InUseText");
                    NotifyPropertyChanged("Color");
                }
            }
        }

        public string InUseText => InUse ? $"{ParkedNumber}" : "Open";

        private string _parkedNumber = "In Use";

        /// <summary>
        /// The phone number/extension parked in the parking spot
        /// </summary>
        public string ParkedNumber
        {
            get { return _parkedNumber; }
            set
            {
                if (_parkedNumber != value)
                {
                    NotifyPropertyChanged("ParkedNumber");
                    NotifyPropertyChanged("InUseText");
                    _parkedNumber = value;
                }
            }
        }

        public SolidColorBrush Color => InUse ? Application.Current.Resources["ExtColor1"] as SolidColorBrush : Application.Current.Resources["ExtColor8"] as SolidColorBrush;

        public ParkingSpot(string number)
        {
            Number = number;
        }
    }

    public enum ParkingSpots
    {
        First = 701,
        Second = 702,
        Third = 704
    }
}