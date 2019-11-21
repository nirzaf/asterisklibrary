using System;

namespace SmartPABXReceptionConsole2._0
{
    public class PABXDevConsole : ViewModel
    {
        private string _errorLog = "";

        public string ErrorLog
        {
            get => _errorLog;
            set
            {
                if (value != _errorLog)
                {
                    _errorLog += $"{DateTime.Now.ToLongTimeString()}: {value} \n\n";
                    NotifyPropertyChanged("ErrorLog");
                }
            }
        }

        public void ClearOutput()
        {
            _errorLog = "";
            NotifyPropertyChanged("ErrorLog");
        }
    }
}