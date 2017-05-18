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
using ESLTracker.Utils;
using ESLTracker.Utils.Messages;
using ESLTracker.ViewModels;
using ESLTracker.Utils.DeckFileReader;
using ESLTracker.DataModel;
using ESLTracker.Utils.TriggerChanceUpdater;
using System.IO;

namespace ESLTracker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        new public MainWindowViewModel DataContext
        {
            get
            {
                return (MainWindowViewModel)base.DataContext;
            }
            set
            {
                base.DataContext = value;
            }
        }

        public static bool UpdateOverlay { get; internal set; }

        public MainWindow()
        {
            InitializeComponent();
            CheckModifiedDlls();
            Task.Run( () =>  UpdateOverlayAsync(this));
            Task.Run(() => ProcessExternalFilesUpdate(this));
            Application.Current.MainWindow = this;
            TrackerFactory.DefaultTrackerFactory.GetMessanger().Register<ApplicationShowBalloonTip>(this, ShowBaloonRequested);

        }

        private static async Task UpdateOverlayAsync(MainWindow mainWindow)
        {
            IWinAPI winAPI = new WinAPI();
            mainWindow.Dispatcher.Invoke(() => {
                foreach (Window w in mainWindow.DataContext.OverlayWindows)
                {
                    w.Show();
                }
            });
            UpdateOverlay = true;
            while (UpdateOverlay)
            {
                mainWindow.Dispatcher.Invoke(() =>
                {
                    bool isAnyOverlayActive = mainWindow.DataContext.OverlayWindows.IsAnyActive();
                    foreach (IOverlayWindow window in mainWindow.DataContext.OverlayWindows)
                    {
                        ((IOverlayWindow)window).UpdateVisibilty(winAPI.IsGameActive(), winAPI.GetEslProcess() != null, mainWindow.IsActive, isAnyOverlayActive);
                    }
                });
                await Task.Delay(1000);
            }
            mainWindow.Dispatcher.Invoke(() => {
                foreach (Window w in mainWindow.DataContext.OverlayWindows)
                {
                    w.Hide();
                }
            });
            UpdateOverlay = false;
        }
        private static async Task ProcessExternalFilesUpdate(MainWindow mainWindow)
        {
            DeckFileReader dfr = TrackerFactory.DefaultTrackerFactory.GetTracker().dfr;
            if (TrackerFactory.DefaultTrackerFactory.GetTracker().ActiveDeck != null)
            {
                HashSet<CardInstance> cards = new HashSet<CardInstance>();
                new TriggerChanceUpdater(TrackerFactory.DefaultTrackerFactory.GetTracker().
                                            ActiveDeck.SelectedVersion.Cards, cards);
                mainWindow.Dispatcher.Invoke(() => DeckFileReader.UpdateGui(cards, false));
            }
            for (;;)
            {
                HashSet<CardInstance> cards = new HashSet<CardInstance>();
                HashSet<CardInstance> cards_silent = new HashSet<CardInstance>();
                bool reset = dfr.ReadSentFile(cards, cards_silent);
                if (cards.Count() == 0 && dfr.isGameStarted() == false)
                {
                    if (dfr.ReadDecks())
                    {
                        mainWindow.Dispatcher.Invoke(() =>
                        {
                            TrackerFactory.DefaultTrackerFactory.GetMessanger()
                            .Send(new DeckListResetFilters(), ControlMessangerContext.DeckList_DeckFilterControl);
                       });
                        TrackerFactory.DefaultTrackerFactory.GetFileManager().SaveDatabase();
                    }
                    Deck active_deck = dfr.UpdateActiveDeck();
                    if (active_deck != null)
                    {
                        mainWindow.Dispatcher.Invoke(() =>
                        {
                            TrackerFactory.DefaultTrackerFactory.GetTracker().ActiveDeck = active_deck;
                        });
                    }
                        
                }
                bool update = dfr.UpdateHandCount();
                if (cards.Count() > 0)
                {
                    await mainWindow.Dispatcher.Invoke(async () =>
                     {
                         // Play red
                         if (reset == false)
                         {
                             DeckFileReader.UpdateGui(cards, true);
                             await Task.Delay(300);
                         }
                         else
                             dfr.CleanupActiveDeck();

                         // Stop red and update fields
                         DeckFileReader.UpdateGui(cards, false);
                         // Play silent (trigger chances)
                         DeckFileReader.UpdateGui(cards_silent, false);
                         TrackerFactory.DefaultTrackerFactory.GetTracker().UpdateActiveDeck();
                     });
                }
                else if (update)
                    TrackerFactory.DefaultTrackerFactory.GetTracker().UpdateActiveDeck();

                await Task.Delay(dfr.isGameStarted() ? 100: 1000);
            }
        }

        private void CheckModifiedDlls()
        {
            DeckFileReader dfr = TrackerFactory.DefaultTrackerFactory.GetTracker().dfr;
            if (dfr.NeedToModifyDlls() == false)
                return;
            
            if (MessageBox.Show("This program needs to modify your game DLLs. "+
                "It's violates TES:L Terms of service. There are a bit risk of your account will be banned. "+
                "If you are afraid so - use original tracker from @MarioZG.", "Modify your DLLs?", MessageBoxButton.YesNo) == MessageBoxResult.Yes)

            {
                try { File.Copy(".\\Resources\\TES-L-Modifided-dll\\game-src.dll", dfr.game_src_lib, true); }
                catch (IOException e) {
                    MessageBox.Show(e.Message);
                    return;
                }

                MessageBox.Show("Restart your game now.");
            }
            else
            {
                System.Diagnostics.Process.Start("https://github.com/MarioZG/elder-scrolls-legends-tracker/releases");
                ((App)Application.Current).CloseApplication();
            }
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (! ((App)Application.Current).IsApplicationClosing)
            {
                if (Properties.Settings.Default.MinimiseOnClose)
                {
                    this.DataContext.WindowState = WindowState.Minimized;
                    this.DataContext.ShowInTaskBar = false;

                    e.Cancel = true;
                }
                else
                {
                    OverlayToolbar ot = this.DataContext.OverlayWindows.GetWindowByType<OverlayToolbar>();
                    if (ot.CanClose(this.DataContext.CommandExit))
                    {
                        this.DataContext.Exit(true);
                    }
                    else
                    {
                        e.Cancel = true;
                    }
                }
            }
        }

        internal void RestoreOverlay()
        {
            if (!UpdateOverlay)
            {
                Task.Run(() => UpdateOverlayAsync(this));
            }
        }

        private void mainWindow_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Normal)
            {
                this.Activate();
                this.Focus();
            }
        }

        private void ShowBaloonRequested(ApplicationShowBalloonTip baloonRequest)
        {
            this.taskBarIcon.ShowBalloonTip(baloonRequest.Title, baloonRequest.Message, Hardcodet.Wpf.TaskbarNotification.BalloonIcon.None);
        }
    }
}