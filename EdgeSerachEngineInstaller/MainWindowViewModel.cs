using EdgeSerachEngineInstaller.Installer;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace EdgeSerachEngineInstaller
{
    internal class MainWindowViewModel : BaseViewModel
    {

        #region Constructor

        public MainWindowViewModel()
        {
            Driver = new InstallerDriver();
            SearchEngineName = DefaultEngineName;
            SearchEngineShortcut = DefaultEngineShortcut;
            SearchEngineURL = DefaultEngineUrl;
            IsAddEnabled = true;
        }

        #endregion Constructor

        #region Constants

        private const string DefaultEngineName = "MySearchEngine";
        private const string DefaultEngineShortcut = "mysearchengine.com";
        private const string DefaultEngineUrl = "https://mysearchengine.com?query=%s";

        #endregion Constants

        #region Private Members

        private InstallerDriver Driver;

        #endregion Private Members

        #region Naming

        private string searchEngineName;
        public string SearchEngineName
        {
            get
            {
                return searchEngineName;
            }
            set
            {
                searchEngineName = value;
                OnPropertyChanged(nameof(SearchEngineName));
            }
        }

        private string searchEngineShortcut;
        public string SearchEngineShortcut
        {
            get
            {
                return searchEngineShortcut;
            }
            set
            {
                searchEngineShortcut = value;
                OnPropertyChanged(nameof(SearchEngineShortcut));
            }
        }

        private string searchEngineURL;
        public string SearchEngineURL
        {
            get
            {
                return searchEngineURL;
            }
            set
            {
                searchEngineURL = value;
                OnPropertyChanged(nameof(SearchEngineURL));
            }
        }

        private bool isAddEnabled;
        public bool IsAddEnabled
        {
            get
            {
                return isAddEnabled;
            }
            set
            {
                isAddEnabled = value;
                OnPropertyChanged(nameof(IsAddEnabled));
            }
        }

        #endregion Naming

        #region Commands

        #region Commands->Add

        private ICommand addCommand;
        public ICommand AddCommand
        {
            get
            {
                if (addCommand == null)
                {
                    addCommand = new RelayCommand(AddExecute);
                }
                return addCommand;
            }
        }
        private async void AddExecute(object _)
        {
            IsAddEnabled = false;
            await Install();
            IsAddEnabled = true;
        }

        #endregion Commands->Add

        #region Commands->Add

        private ICommand cancelCommand;
        public ICommand CancelCommand
        {
            get
            {
                if (cancelCommand == null)
                {
                    cancelCommand = new RelayCommand(CancelExecute);
                }
                return cancelCommand;
            }
        }
        private void CancelExecute(object _)
        {
            Driver.StopDriverAsync();
            Application.Current.Shutdown();
        }

        #endregion Commands->Add

        #endregion Commands

        #region Helper Methods

        /// <summary>
        /// Retrieves the path to the default user profile directory of the Microsoft Edge browser.
        /// </summary>
        /// <returns></returns>
        private static string GetEdgeDefaultUserProfilePath()
        {
            string userProfileDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string edgeProfilePath = Path.Combine(userProfileDir, "AppData", "Local", "Microsoft", "Edge", "User Data");

            if (Directory.Exists(edgeProfilePath))
            {
                return edgeProfilePath;
            }
            else
            {
                // we may want to throw an exception
                return "Default Edge profile directory not found.";
            }
        }
        /// <summary>
        /// Extract the index
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        private int GetIndex(string content)
        {
            content = content.Replace(SearchEngineURL, string.Empty);
            return int.Parse(content);
        }

        #endregion Helper Methods

        /// <summary>
        /// This method performs the installation procedure for the new search engine.
        /// 1. Create a new session.
        /// 2. Navigate to the search engines tab.
        /// 3. Find the add button and click it.
        /// 4. Insert the desired values.
        /// 5. Add the new engine.
        /// 6. Get the index of our search engine inside the table.
        /// 7. Make it the default search engine.
        /// 8. Clean up.
        /// </summary>
        /// <returns></returns>
        private async Task Install()
        {
            bool succeeded = false;
            try
            {
                // 1
                // Start WebDriver server and create new session
                await Driver.StartDriverAsync(GetEdgeDefaultUserProfilePath());

                // 2
                // Navigate to serach enginges tab
                await Driver.SetURLAsync("edge://settings/searchEngines");

                // 3
                // Get the "Add search engine" button key
                var addSearchEndgineButtonKey = await Driver.GetElementKeyByCssAsync("[aria-label='Add search engine']");

                // Open sub windows to insert new engine properties
                await Driver.PerformElementClickAsync(addSearchEndgineButtonKey);

                // 4
                // Get the "Search engine" input key
                var engineNameInputKey = await Driver.GetElementKeyByIdAsync("searchEngineName");
                // Get the "Shortcut" input key
                var engineKeywordInputKey = await Driver.GetElementKeyByIdAsync("searchEngineKeyword");
                // Get the "URL" input key
                var engineUrlInputKey = await Driver.GetElementKeyByIdAsync("searchEngineUrl");

                // Set Search engine input value
                await Driver.SetElementValueAsync(engineNameInputKey, SearchEngineName);

                // Set Shortcut input value
                await Driver.SetElementValueAsync(engineKeywordInputKey, SearchEngineShortcut);

                // Set URL input value
                await Driver.SetElementValueAsync(engineUrlInputKey, SearchEngineURL);

                // 5
                // Get form element key
                var formKey = await Driver.GetElementKeyByCssAsync("form");

                // Get the Add and Cancel buttons inside the form
                var buttonsInsideFormKeys = await Driver.GetElementsByCssAsync("button", formKey);

                // Add button key
                var addButtonKey = buttonsInsideFormKeys.ElementAt(0);

                // Add MyEngine
                await Driver.PerformElementClickAsync(addButtonKey);
                // Cancel button key
                //var cancelButtonKey = buttonsInsideFormKeys.ElementAt(1);

                // 6
                // JavaScript code to find our new search engine on the table and return the index
                string javascriptCodeReturnIndex = $"var element = document.querySelector('[data-rowid$=\"{SearchEngineURL}\"]');" +
                                        "if (element) {" +
                                            "var rowId = element.getAttribute('data-rowid');" +
                                            "return rowId;}";

                // Run the JavaScript code and return the inxdex
                var value = await Driver.ScriptAndGetAsync(javascriptCodeReturnIndex);

                // Extract the index from data-rowid attribute vlaue
                var index = GetIndex(value) - 2;

                // 7
                // JavaScript code to make our new search engine the default search engine by index
                var javaScriptCodeMakeDefaukt = $"window.chrome.send(\"setDefaultSearchEngine\", [{index}]);";

                // Run the script to make our search engione the default
                await Driver.ScriptAsync(javaScriptCodeMakeDefaukt);

                succeeded = true;
            }
            catch (Exception e)
            {
            }
            finally
            {
                // 8
                // Close the session
                //await Driver.CloseSessionAsync();
                // WebDriver shutdown 
                await Driver.StopDriverAsync();
                string resultMessage;
                if (succeeded)
                {
                    resultMessage = "Search Engine Installation Succeeded";
                }
                else
                {
                    resultMessage = "Search Engine Installation Failed";
                }
                MessageBox.Show(Application.Current.MainWindow, resultMessage);
            }
        }

    }

}
