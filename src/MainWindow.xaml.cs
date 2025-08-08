using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Configuration;
using System.ServiceProcess;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using MahApps.Metro.Controls;
using System.IO;
using System.Diagnostics;




namespace Service_Manager
{
    // Définition de la classe service
    public class myService : INotifyPropertyChanged // classe avec la notification de changement
    {
        public int serviceKey { get; set; }
        public string serviceId { get; set; }
        public string serviceName { get; set; }
        public string serviceIP { get; set; }
        public string serviceCat { get; set; }
        public string serviceTooltip { get; set; }
        public string serviceConf { get; set; }
        public string serviceLog { get; set; }
        // pour la propriété qui doit lancer l'alerte, on précise le get/set pour y intégrer la notif
        private string _serviceState;
        public string serviceState
        {
            get { return this._serviceState; }
            set
            {
                if (this._serviceState != value)
                {
                    this._serviceState = value;
                    this.NotifyPropertyChanged("serviceState");
                }
            }
        }


        // Constructeur
        public myService(int serviceKey, string serviceId, string serviceName, string serviceIP, string serviceCat, string serviceTooltip, string serviceConf, string serviceLog)
        {
            this.serviceKey = serviceKey; // pour index dans le tableau, pour gérer le cas avec deux services de même nom
            this.serviceId = serviceId;
            this.serviceName = serviceName;
            this.serviceIP = serviceIP;
            this.serviceState = "..."; // récup après initialisation
            this.serviceCat = serviceCat;
            this.serviceTooltip = serviceTooltip;
            this.serviceLog = serviceLog;
            this.serviceConf = serviceConf;
        }

        // on déclare l'évènement qui alerte l'UI du changement
        // l'enregistrement local du handler permet de gérer le cas ou la prop deviendrait null entre le test et l'utilisation (cas possible ne multi-threading par exemple)
        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged(string propName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propName));
        }

    }

    /// <summary>
    /// Logique d'interaction pour MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow, INotifyPropertyChanged
    {
        #region Gestion debug et Notify
        //on créé un object lock
        private static object _lock = new object();

        // gestion debug , on créé la propriété (et non le champ, non "bindable" en wpf)
        private bool _debug;
        public bool debug
        {
            get { return _debug; }
            set
            {
                _debug = value;
                this.NotifyPropertyChanged("debug");
            }
        }
        // on déclare l'évènement qui alerte l'UI du changement
        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged(string propName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propName));
        }
        // puis on défini le traitement pour un affichage type
        private void logIt(string txtLog)
        {
            Application.Current.Dispatcher.Invoke(new Action(delegate { textBox.Text += string.Format("{0:yyyy-MM-dd HH:mm.ss}", DateTime.Now) + " - " + txtLog + Environment.NewLine; }));
        }

        // On créé l'objet collection surveillé :
        ObservableCollection<myService> serviceCollection = new ObservableCollection<myService>();
        private CollectionViewSource cvs;
        public MainWindow()
        {
            debug = ConfigurationManager.AppSettings["debug"].Equals("1"); // récup débug true/false avec conversions tring => bool
            InitializeComponent();
            BindingOperations.EnableCollectionSynchronization(serviceCollection, _lock);

            cvs = new CollectionViewSource();
            cvs.Source = serviceCollection;
            dataGrid.ItemsSource = cvs.View;
        }
        #endregion Gestion debug et Notify


        // Au chargement
        private void dataGrid_Loaded(object sender, RoutedEventArgs e)
        {
            if (serviceCollection.Count == 0)
            {
                // On récupère les objets
                string[] serviceList = ConfigurationManager.AppSettings.AllKeys
                                .Where(key => key.StartsWith("Service"))
                                .Select(key => key.Substring(7) + ";" + ConfigurationManager.AppSettings[key])
                                .ToArray();

                foreach (var stringService in serviceList.Select((value, i) => new { i, value }))
                {
                    string[] key = stringService.value.Split(';');
                    int keyId = stringService.i; // un Id indépendant pour la gestion via IHM;
                                                 // gestion des clés optionnelles
                    string valTooltip = "", valCat = "", pathConf = "", pathLog = "";
                    if (ConfigurationManager.AppSettings.AllKeys.Contains("Tooltip_" + key[0])) valTooltip = ConfigurationManager.AppSettings["Tooltip_" + key[0]];
                    if (ConfigurationManager.AppSettings.AllKeys.Contains("Cat_" + key[0]))
                    {
                        valCat = ConfigurationManager.AppSettings["Cat_" + key[0]];
                    }
                    if (ConfigurationManager.AppSettings.AllKeys.Contains("Log_" + key[0])) pathLog = ConfigurationManager.AppSettings["Log_" + key[0]];
                    if (ConfigurationManager.AppSettings.AllKeys.Contains("Conf_" + key[0])) pathConf = ConfigurationManager.AppSettings["Conf_" + key[0]];
                    serviceCollection.Add(new myService(keyId, key[1], key[2], key[3], valCat, valTooltip, pathConf, pathLog));
                }

                // Populate category filter
                var categories = new List<string> { "All" };
                categories.AddRange(serviceCollection.Select(s => s.serviceCat).Distinct().Where(c => !string.IsNullOrEmpty(c)).OrderBy(c => c));
                categoryFilterComboBox.ItemsSource = categories;
                categoryFilterComboBox.SelectedIndex = 0;
            }

            status_check();
        }


        /// <summary>
        /// Récupération et traduction des états des services Windows (via un Task)
        /// </summary>
        /// <param name="serviceCible">Permet de ne mettre à jour qu'un service ciblé</param>
        private void status_check(int serviceCible = 999)
        {
            if (serviceCible == 999) logIt("Etat des services en cours de mise à jour.");
            foreach (var item in serviceCollection)
            {
                var currentService = item as myService;
                if (serviceCible == 999 || serviceCible == currentService.serviceKey) // si tous ou si celui demandé
                {
                    // lancement en arrière plan 
                    Task.Run(() =>
                    {
                        try
                        {
                            // Si le service existe, on vérifie le status
                            if (ServiceController.GetServices(currentService.serviceIP).Any(serviceController => serviceController.ServiceName.Equals(currentService.serviceId)))
                            {
                                ServiceController sc = new ServiceController(currentService.serviceId, currentService.serviceIP);
                                switch (sc.Status)
                                {
                                    case ServiceControllerStatus.Running:
                                        currentService.serviceState = "Démarré";
                                        break;
                                    case ServiceControllerStatus.Stopped:
                                        currentService.serviceState = "Arrêté";
                                        break;
                                    case ServiceControllerStatus.Paused:
                                        currentService.serviceState = "En pause";
                                        break;
                                    case ServiceControllerStatus.StopPending:
                                        currentService.serviceState = "Arrêt en cours";
                                        break;
                                    case ServiceControllerStatus.StartPending:
                                        currentService.serviceState = "Démarrage...";
                                        break;
                                    default:
                                        currentService.serviceState = "Inconnu";
                                        break;
                                }
                            }
                            else
                            {
                                currentService.serviceState = "Non trouvé";
                                logIt(currentService.serviceName + " sur " + currentService.serviceIP + " : service non présent!");
                            }
                        }
                        catch (Exception) // si pas trouvé/accessible...
                        {
                            currentService.serviceState = "Non trouvé";
                            logIt(currentService.serviceName + " sur " + currentService.serviceIP + " : droits insuffisants ou serveur inconnu!");
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Event sur boutton Démarrer : démarre le ou les services sélectionnés
        /// </summary>
        private void buttonStart_Click(object sender, RoutedEventArgs e)
        {
            // Récupération de la sélection
            var selected = dataGrid.SelectedItems;

            // ... parcours
            foreach (var item in selected)
            {
                var selectedService = item as myService;

                if ((ServiceController.GetServices(selectedService.serviceIP).Any(serviceController => serviceController.ServiceName.Equals(selectedService.serviceId)))
                    && selectedService.serviceState == "Arrêté")
                {
                    selectedService.serviceState = "Démarrage...";
                    ServiceController sc = new ServiceController(selectedService.serviceId, selectedService.serviceIP);
                    // On lance et on surveille en arrière plan pour ne pas geler l'UI
                    Task.Run(() => {
                        try
                        {
                            sc.Start();
                            TimeSpan timeout = TimeSpan.FromMilliseconds(50000);
                            sc.WaitForStatus(ServiceControllerStatus.Running, timeout);
                            logIt(selectedService.serviceName + " démarré.");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Le service " + selectedService.serviceName + " n'a pas pu être démarré." + Environment.NewLine + " Erreur : " + ex.ToString());
                        }
                        status_check(selectedService.serviceKey); // et on fini en mettant à jour le status
                    });
                }
                else
                    logIt("Impossible de démarrer le service " + selectedService.serviceName + ", non trouvé");
            }
        }

        /// <summary>
        /// Reset des états puis lancement de status_check()
        /// </summary>
        private void buttonRefresh_click(object sender, RoutedEventArgs e)
        {
            foreach (var item in serviceCollection)
            {
                item.serviceState = "...";
            }
            status_check();
        }

        private void buttonStop_Click(object sender, RoutedEventArgs e)
        {
            // Récupération de la sélection
            var selected = dataGrid.SelectedItems;
            // ... parcours
            foreach (var item in selected)
            {
                var selectedService = item as myService;
                if ((ServiceController.GetServices(selectedService.serviceIP).Any(serviceController => serviceController.ServiceName.Equals(selectedService.serviceId)))
                    && selectedService.serviceState == "Démarré")
                {
                    selectedService.serviceState = "Arrêt en cours...";
                    ServiceController sc = new ServiceController(selectedService.serviceId, selectedService.serviceIP);
                    // On lance et on surveille en arrière plan pour ne pas geler l'UI
                    Task.Run(() => {
                        try
                        {
                            sc.Stop();
                            TimeSpan timeout = TimeSpan.FromMilliseconds(50000);
                            sc.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                            logIt(selectedService.serviceName + " arrêté.");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Le service " + selectedService.serviceName + " n'a pas pu être arrêté." + Environment.NewLine + " Erreur : " + ex.ToString());
                        }
                        status_check(selectedService.serviceKey); // et on fini en mettant à jour le status
                    });
                }
                else
                    logIt("Impossible d'arrêter le service " + selectedService.serviceName + ", non trouvé");

            }
        }

        private void buttonRestart_Click(object sender, RoutedEventArgs e)
        {
            // Récupération de la sélection
            var selected = dataGrid.SelectedItems;
            // ... parcours
            foreach (var item in selected)
            {
                var selectedService = item as myService;
                if ((ServiceController.GetServices(selectedService.serviceIP).Any(serviceController => serviceController.ServiceName.Equals(selectedService.serviceId)))
                    && selectedService.serviceState == "Démarré")
                {
                    selectedService.serviceState = "Arrêt en cours...";
                    ServiceController sc = new ServiceController(selectedService.serviceId, selectedService.serviceIP);
                    // On lance et on surveille en arrière plan pour ne pas geler l'UI
                    Task.Run(() => {
                        try
                        {
                            sc.Stop();
                            TimeSpan timeout = TimeSpan.FromMilliseconds(50000);
                            sc.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                            logIt(selectedService.serviceName + " arrêté, redémarrage demandé.");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Le service " + selectedService.serviceName + " n'a pas pu être arrêté." + Environment.NewLine + " Erreur : " + ex.ToString());
                        }
                        status_check(selectedService.serviceKey); // et on fini en mettant à jour le status
                        Thread.Sleep(1000);
                        try
                        {
                            sc.Start();
                            TimeSpan timeout = TimeSpan.FromMilliseconds(50000);
                            sc.WaitForStatus(ServiceControllerStatus.Running, timeout);
                            logIt(selectedService.serviceName + " démarré.");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Le service " + selectedService.serviceName + " n'a pas pu être démarré." + Environment.NewLine + " Erreur : " + ex.ToString());
                        }
                        status_check(selectedService.serviceKey); // et on fini en mettant à jour le status
                    });
                }
                else
                    logIt("Impossible de redémarrer le service " + selectedService.serviceName + ", non trouvé");
            }
        }

        /// <summary>
        /// Clic sur bouton d'info
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            WindowInfo wininfo = new WindowInfo();
            wininfo.Owner = Application.Current.MainWindow;
            wininfo.WindowStartupLocation = WindowStartupLocation;
            wininfo.Show();
        }

        private void buttonAdd_Click(object sender, RoutedEventArgs e)
        {
            WindowConfig winConfig = new WindowConfig();
            winConfig.Owner = Application.Current.MainWindow;
            winConfig.WindowStartupLocation = WindowStartupLocation;
            winConfig.ShowDialog();
            RefreshDataGrid();
        }

        public void RefreshDataGrid()
        {
            serviceCollection.Clear();
            cvs.View.Filter = null;
            categoryFilterComboBox.ItemsSource = null;
            dataGrid_Loaded(dataGrid, new RoutedEventArgs());
        }

        private void categoryFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (categoryFilterComboBox.SelectedItem != null)
            {
                string selectedCategory = categoryFilterComboBox.SelectedItem.ToString();
                if (selectedCategory == "All")
                {
                    cvs.View.Filter = null;
                }
                else
                {
                    cvs.View.Filter = item =>
                    {
                        myService service = item as myService;
                        return service.serviceCat == selectedCategory;
                    };
                }
            }
        }

        private void ConfigMenu_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            if (menuItem != null)
            {
                string filePath = menuItem.CommandParameter as string;
                if (!string.IsNullOrEmpty(filePath))
                {
                    if (File.Exists(filePath))
                    {
                        Process.Start(filePath);
                    }
                    else
                    {
                        MessageBox.Show(string.Format("Le fichier n'a pas été trouvé. Chemin du fichier : {0}", filePath), "Fichier non trouvé", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void LogMenu_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            if (menuItem != null)
            {
                string filePath = menuItem.CommandParameter as string;
                if (!string.IsNullOrEmpty(filePath))
                {
                    if (File.Exists(filePath))
                    {
                        Process.Start(filePath);
                    }
                    else
                    {
                        MessageBox.Show(string.Format("Le fichier n'a pas été trouvé. Chemin du fichier : {0}", filePath), "Fichier non trouvé", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void EditMenu_Click(object sender, RoutedEventArgs e)
        {
            myService selectedService = dataGrid.SelectedItem as myService;
            if (selectedService != null)
            {
                // The service key in App.config is not the same as serviceKey in myService object.
                // We need to find the key in App.config that corresponds to the selected service.
                // The serviceKey in myService is just an index in the collection, which is not reliable.
                // A better approach would be to find the key based on the service name and IP, but that's not guaranteed to be unique.
                // The current implementation of adding a service finds the next available ID.
                // The serviceId is the name of the service, serviceName is the title.
                // Let's find the key in App.config. The value is "serviceId;serviceName;serviceIP".

                string[] serviceList = ConfigurationManager.AppSettings.AllKeys
                                        .Where(key => key.StartsWith("Service"))
                                        .ToArray();

                string keyToEdit = "";
                foreach (var key in serviceList)
                {
                    string[] serviceValues = ConfigurationManager.AppSettings[key].Split(';');
                    if (serviceValues.Length >= 3 && serviceValues[0] == selectedService.serviceId && serviceValues[2] == selectedService.serviceIP)
                    {
                        keyToEdit = key;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(keyToEdit))
                {
                    int serviceKey = int.Parse(keyToEdit.Substring(7));
                    WindowConfig winConfig = new WindowConfig(selectedService, serviceKey);
                    winConfig.Owner = Application.Current.MainWindow;
                    winConfig.WindowStartupLocation = WindowStartupLocation;
                    winConfig.ShowDialog();
                    RefreshDataGrid();
                }
                else
                {
                    MessageBox.Show("Could not find the service in the configuration file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DeleteMenu_Click(object sender, RoutedEventArgs e)
        {
            myService selectedService = dataGrid.SelectedItem as myService;
            if (selectedService != null)
            {
                MessageBoxResult result = MessageBox.Show(string.Format("Etes-vous sûr de vouloir supprimer le service '{0}'?", selectedService.serviceName), "Confirmer la suppression", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    string[] serviceList = ConfigurationManager.AppSettings.AllKeys
                                            .Where(key => key.StartsWith("Service"))
                                            .ToArray();

                    string keyToDelete = "";
                    foreach (var key in serviceList)
                    {
                        string[] serviceValues = ConfigurationManager.AppSettings[key].Split(';');
                        if (serviceValues.Length >= 3 && serviceValues[0] == selectedService.serviceId && serviceValues[2] == selectedService.serviceIP)
                        {
                            keyToDelete = key;
                            break;
                        }
                    }

                    if (!string.IsNullOrEmpty(keyToDelete))
                    {
                        try
                        {
                            int serviceKey = int.Parse(keyToDelete.Substring(7));
                            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                            AppSettingsSection appSettings = config.AppSettings;

                            appSettings.Settings.Remove("Service" + serviceKey);
                            appSettings.Settings.Remove("Tooltip_" + serviceKey);
                            appSettings.Settings.Remove("Cat_" + serviceKey);
                            appSettings.Settings.Remove("Log_" + serviceKey);
                            appSettings.Settings.Remove("Conf_" + serviceKey);

                            config.Save(ConfigurationSaveMode.Modified);
                            ConfigurationManager.RefreshSection("appSettings");

                            RefreshDataGrid();
                        }
                        catch (ConfigurationErrorsException ex)
                        {
                            MessageBox.Show("Erreur d'écriture du fichier de configuration: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Impossible de trouver le service dans le fichier de configuration.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}
