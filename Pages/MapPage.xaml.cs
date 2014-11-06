/*
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
*/
﻿

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using System.Windows.Media;
using System.Device.Location;
using Lokki.Settings;
using Microsoft.Phone.Maps.Controls;
using FSecure.Lokki.Controls;
using FSecure.Utils;
using FSecure.Logging;
using FSecure.Lokki.ServerAPI;
using System.Windows.Threading;
using System.Threading.Tasks;
using FSecure.Lokki.Resources;
using Microsoft.Phone.Tasks;
using FSecure.Utils.ExtensionMethods;
using Microsoft.Phone.UserData;
using Windows.Phone.PersonalInformation;
using System.Windows.Media.Imaging;
using ringo_wp8;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Lokki.Location;
using FSecure.Lokki.Utils;
using System.Windows.Media.Animation;

namespace FSecure.Lokki.Pages
{
    /// <summary>
    /// Page for displaying the location of people
    /// 
    /// Tutorials:
    /// A welcome bubble is shown at first startup.
    /// 
    /// Add new people tutorial is shown after welcome bubble is closed
    /// and only current user is in People list
    /// and it has not been shown before.
    /// 
    /// People can see you tutorial is shown after successfully adding a person
    /// and it has not been shown before.
    /// 
    /// </summary>
    public partial class MapPage : PhoneApplicationPage
    {

        /// <summary>
        /// How much pins are transformed if they overlap.
        /// </summary>
        private const double OVERLAP_TRANSFORM = 60;

        /// <summary>
        /// Layer for displaying names of places
        /// </summary>
        private MapLayer PlacesLayer = new MapLayer();

        /// <summary>
        /// Layer for MapPushPins
        /// </summary>
        private MapLayer MapPushPinLayer = new MapLayer();

        /// <summary>
        /// Layer for callouts
        /// </summary>
        private MapLayer CalloutsLayer = new MapLayer();

        /// <summary>
        /// Overlay for callout dialog
        /// </summary>
        MapOverlay MapCalloutOverlay;

        /// <summary>
        /// Callout dialog
        /// </summary>
        MapCallout MapCallout = new MapCallout();

        /// <summary>
        /// Shortcut to SettingsManager.People. See RefreshAllPins.
        /// </summary>
        private ObservableCollection<Person> People = null;

        /// <summary>
        /// The timer used to refresh dashboard
        /// </summary>
        private DispatcherTimer DashboardTimer;

        /// <summary>
        /// References welcome tutorial bubble if visible
        /// </summary>
        private WelcomeBubble WelcomeTutorialBubble { get; set; }

        /// <summary>
        /// References add friend tutorial bubble if visible
        /// </summary>
        private AddFriendTutorialBubble AddFriendTutorialBubble { get; set; }

        /// <summary>
        /// References can see you tutorial bubble if visible
        /// </summary>
        private CanSeeYouTutorialBubble CanSeeYouTutorialBubble { get; set; }

        private bool IsFirstDashboardRequestComplete;

        /// <summary>
        /// Zoom the map to the location of user the first time the position is updated
        /// </summary>
        private bool IsInitialUserLocationShown { get; set; }

        private const int PIVOT_INDEX_MAP = 0;
        private const int PIVOT_INDEX_PLACES = 1;
        private const int PIVOT_INDEX_MY_PEOPLE = 2;

        /// <summary>
        /// How much to zoom when focusing on place
        /// </summary>
        private const double PLACE_FOCUS_ZOOM = 17;

        /// <summary>
        /// How much to zoom when focusing on person
        /// </summary>
        private const double PERSON_FOCUS_ZOOM = 17.5;

        /// <summary>
        /// Used to iterate over the current pins on the map.
        /// </summary>
        private IEnumerable<MapPushPin> Pins
        {
            get
            {
                foreach (MapOverlay overlay in MapPushPinLayer)
                {
                    yield return (overlay.Content as MapPushPin);
                }
            }
        }

        public MapPage()
        {
            InitializeComponent();

            MapControl.Layers.Add(PlacesLayer);
            MapControl.Layers.Add(MapPushPinLayer);
            MapControl.Layers.Add(CalloutsLayer);

            MapControl.Loaded += MapControl_Loaded;
            MapControl.Hold += MapControl_Hold;
            MapControl.ZoomLevelChanged += MapControl_UpdatePlacesVisibilityOnZoomLevelChange;

            ApplicationBar = new ApplicationBar();
            ApplicationBar.BackgroundColor = (Color)Resources["C6"];
            ApplicationBar.ForegroundColor = (Color)Resources["White"];

            ApplicationBar.Mode = ApplicationBarMode.Default;
            ApplicationBar.IsMenuEnabled = true;
            ApplicationBar.IsVisible = true;

            InitMenu();

            ContactsManager.Instance.UserInvited += ContactsManager_UserInvited;
        }

        void ContactsManager_UserInvited(object sender, EventArgs e)
        {
            ShowNewPeopleTutorial();
            GetDashboard();
        }

        #region Menu handling

        ApplicationBarIconButton MenuButtonVisibility;
        private void InitMenu()
        {
            // Add people & places button
            ApplicationBarIconButton MenuButtonAdd = new ApplicationBarIconButton();
            MenuButtonAdd.IconUri = new Uri("/Assets/AppBarAdd.png", UriKind.Relative);
            MenuButtonAdd.Text = Localized.MenuButtonAdd;
            MenuButtonAdd.Click += MenuButtonAdd_Click;
            ApplicationBar.Buttons.Add(MenuButtonAdd);

            // Visibility button
            MenuButtonVisibility = new ApplicationBarIconButton();
            MenuButtonVisibility.IconUri = new Uri("/Assets/AppBarVisible.png", UriKind.Relative);
            MenuButtonVisibility.Text = Localized.MenuButtonVisible;
            MenuButtonVisibility.Click += VisibilityMenuButton_Click;
            ApplicationBar.Buttons.Add(MenuButtonVisibility);

            ApplicationBarMenuItem item;
            // My people
            /*
            var item = new ApplicationBarMenuItem(Localized.MenuMyPeople);
            item.Click += MenuItemMyPeople_Click;
            ApplicationBar.MenuItems.Add(item);
            */
            //ApplicationBar.MenuItems.Add(new ApplicationBarMenuItem("My places"));

            // Settings
            item = new ApplicationBarMenuItem(Localized.MenuSettings);
            item.Click += MenuItemSettings_Click;
            ApplicationBar.MenuItems.Add(item);

            // Help
            item = new ApplicationBarMenuItem(Localized.MenuHelp);
            item.Click += MenuItemHelp_Click;
            ApplicationBar.MenuItems.Add(item);

            // Tell a friend
            item = new ApplicationBarMenuItem(Localized.MenuTellAFriend);
            item.Click += MenuItemTellAFriend_Click;
            ApplicationBar.MenuItems.Add(item);

            // Products
            item = new ApplicationBarMenuItem(Localized.MenuProducts);
            item.Click += MenuItemProducts_Click;
            ApplicationBar.MenuItems.Add(item);

            // About
            item = new ApplicationBarMenuItem(Localized.MenuAbout);
            item.Click += MenuItemAbout_Click;
            ApplicationBar.MenuItems.Add(item);

#if STORE_SCREENSHOTS
            GenerateStorePeople();
#endif
        }

#if STORE_SCREENSHOTS

        Geolocation CreateLocation(double lat, double lon, double acc = 25, double diff = 0)
        {
            return new Geolocation
            (
                lat : lat,
                lon : lon,
                acc : acc,
                time : DateTimeOffset.UtcNow + TimeSpan.FromMinutes(diff)
            );
        }

        /** Generate people for store screenshot purposes */
        void GenerateStorePeople()
        {
            var currentUser = SettingsManager.CurrentUser;

            var newPeople = new ObservableCollection<Person>();

            Person person; 
            newPeople.Add(currentUser);

            // Liberty island
            currentUser.Position = CreateLocation(40.6900, -74.0464);

            newPeople.Add(new Person
            {
                Email = "daugter4@foo.bar",
                IsVisible = true,
                ICanSee = true,
                CanSeeMe = true,
                Position = CreateLocation(40.6906, -74.0451)
            });

            newPeople.Add(new Person
            {
                Email = "sister@foo.bar",
                IsVisible = true,
                ICanSee = true,
                CanSeeMe = true,
                Position = CreateLocation(40.6887, -74.0446)
            });

            newPeople.Add(new Person
            {
                Email = "wife@foo.bar",
                IsVisible = true,
                ICanSee = true,
                CanSeeMe = true,
                Position = CreateLocation(40.6897, -74.0439)
            });
            // End liberty island


            // Times new square
            newPeople.Add(new Person
            {
                Email = "sister2@foo.bar",
                IsVisible = true,
                ICanSee = true,
                CanSeeMe = true,
                Position = CreateLocation(40.7555, -73.9855)
            });

            newPeople.Add(new Person
            {
                Email = "son1@foo2.bar",
                ICanSee = true,
                IsVisible = true,
                CanSeeMe = true,
                Position = CreateLocation(40.7550, -73.9868)
            });

            newPeople.Add(new Person
            {
                Email = "father-in-law@foo.bar",
                ICanSee = true,
                IsVisible = true,
                CanSeeMe = true,
                Position = CreateLocation(40.7560, -73.9864)
            });

            newPeople.Add(new Person
            {
                Email = "mother-in-law@foo.bar",
                ICanSee = true,
                IsVisible = true,
                CanSeeMe = true,
                Position = CreateLocation(40.7557, -73.9873)
            });

            //var people = SettingsManager.People;
            SettingsManager.People = newPeople;
        }
#endif

        void MenuItemTellAFriend_Click(object sender, EventArgs e)
        {
            var share = new ShareLinkTask();
            share.LinkUri = new Uri(Localized.TellAFriendLink, UriKind.RelativeOrAbsolute);
            share.Message = Localized.TellAFriendMessage;
            share.Show();
        }

        void MenuItemHelp_Click(object sender, EventArgs e)
        {
            var browser = new WebBrowserTask();
            browser.Uri = new Uri(Localized.HelpLink, UriKind.RelativeOrAbsolute);
            browser.Show();
        }

        void MenuItemProducts_Click(object sender, EventArgs e)
        {
            var marketplaceSearchTask = new MarketplaceSearchTask();

            marketplaceSearchTask.ContentType = MarketplaceContentType.Applications;
            marketplaceSearchTask.SearchTerms = "F-Secure";

            marketplaceSearchTask.Show();
        }

        void MenuItemSettings_Click(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/Pages/SettingsPage.xaml", UriKind.Relative));
        }

        private void MenuItemAbout_Click(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/Pages/AboutPage.xaml", UriKind.Relative));
        }

        /*
        void MenuItemMyPeople_Click(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/Pages/MyPeoplePage.xaml", UriKind.Relative));
        }
        */
        void MapControl_Loaded(object sender, RoutedEventArgs e)
        {
            MapControl.Loaded -= MapControl_Loaded;

            Microsoft.Phone.Maps.MapsSettings.ApplicationContext.ApplicationId = "ea35fbb0-3129-414d-b441-83fef60db595";
            Microsoft.Phone.Maps.MapsSettings.ApplicationContext.AuthenticationToken = "UVzllrWcOsXSZfGtKLNQUg";
        }

        async void MapControl_Hold(object sender, System.Windows.Input.GestureEventArgs e)
        {
            var geoPos = MapControl.ConvertViewportPointToGeoCoordinate(e.GetPosition(MapControl));

            var placeEditor = new PlaceEditBubble();
            placeEditor.Place = new Place("", "", geoPos.Latitude, geoPos.Longitude, 100);
            await placeEditor.Show();
        }

        async void MenuButtonAdd_Click(object sender, EventArgs e)
        {
            // Display people or place add dialog
            
            if (PivotControl.SelectedIndex == PIVOT_INDEX_PLACES)
            {
                FSLog.Info("Add new place");
                PivotControl.SelectedIndex = PIVOT_INDEX_MAP;

                var geoPos = UserCoordinate;
                MapControl.SetView(geoPos, PLACE_FOCUS_ZOOM);

                var placeEditor = new PlaceEditBubble();
                placeEditor.Place = new Place("", "", geoPos.Latitude, geoPos.Longitude, 100);
                await placeEditor.Show();
            }
            // Map or My People
            else
            {
                FSLog.Info("Show user chooser");
                ContactsManager.Instance.ShowInviteUserChooser();
            }
        }

        private GeoCoordinate UserCoordinate
        {
            get
            {
                var pos = SettingsManager.CurrentUser.Position;
                return new GeoCoordinate(pos.Latitude,
                                         pos.Longitude);
            }
        }

        /// <summary>
        /// Used to display notification when user changes visibility
        /// </summary>
        /// <param name="visible"></param>
        private void ShowVisibilityNotification(bool visible)
        {
            FSLog.Info("visible:", visible);

            var notif = new NotificationControl();

            if (visible)
            {
                notif.Text = Localized.NotificationYouAreVisible;
            }
            else
            {
                notif.Text = Localized.NotificationYouAreInvisible;
            }
            
            this.OverlayContainer.Children.Add(notif);
            notif.Bubble.Dismissed += (object s1, EventArgs e2) =>
            {
                FSLog.Info();
                OverlayContainer.Children.Remove(notif);
            };
            notif.Show();
        }

        async void VisibilityMenuButton_Click(object sender, EventArgs e)
        {
            await ToggleVisibility();
        }

        public async Task ToggleVisibility()
        {
            SystemTrayProgressIndicator.TaskCount++;
            try
            {

                var previous = SettingsManager.CurrentUser.IsVisible;
                // Toggle
                var visible = previous == false;

                // Change immediately to show something happened
                SettingsManager.CurrentUser.IsVisible = visible;
                Dispatcher.BeginInvoke(UpdateVisibilityIcon);

                // Send
                var resp = await ServerAPIManager.Instance.ChangeVisibility(visible);
                if (!resp.IsSuccessful)
                {
                    FSLog.Error("Failed to update visibility");
                    MessageBox.Show(Localized.ApiError);

                    SettingsManager.CurrentUser.IsVisible = previous;
                    Dispatcher.BeginInvoke(UpdateVisibilityIcon);
                }
                else
                {
                    ShowVisibilityNotification(visible);
                }

            }
            finally
            {
                SystemTrayProgressIndicator.TaskCount--;
            }
        }

        private void UpdateVisibilityIcon()
        {
            var visible = SettingsManager.CurrentUser.IsVisible;
            UpdateVisibilityIcon(visible);
        }

        private void UpdateVisibilityIcon(bool visible)
        {
            string icon;
            string text;

            if (visible)
            {
                icon = "AppBarVisible.png";
                text = Localized.MenuButtonVisible;
            }
            else
            {
                icon = "AppBarInvisible.png";
                text = Localized.MenuButtonInvisible;
            }

            MenuButtonVisibility.IconUri = new Uri("Assets/" + icon, UriKind.Relative);
            MenuButtonVisibility.Text = text;
        }


        #endregion

        #region Page navigation

        /** Close callout or dialogs if shown, close app otherwise */
        protected override void OnBackKeyPress(System.ComponentModel.CancelEventArgs e)
        {
            FSLog.Info();

            if (MapCallout != null && MapCallout.IsVisible)
            {
                CloseCalloutOverlay();
                e.Cancel = true;
                return;
            }

            base.OnBackKeyPress(e);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var colors = ((ResourceDictionary)ringo_wp8.App.Current.Resources["Colors"]);

            if (e.NavigationMode == NavigationMode.New)
            {
                // Clear login screen from back stack
                while (NavigationService.CanGoBack)
                {
                    NavigationService.RemoveBackEntry();
                }
            }

            SettingsManager.SettingChanged += SettingsManager_SettingChanged;

            UpdateVisibilityIcon();
            SetupMap();

            if (e.NavigationMode == NavigationMode.New)
            {
                (App.Current as App).ShowHeaderPanel();
                RefreshAllPins();

                ApplicationStates.IsDashboardRequestNeeded = true;
                ShowWelcomeTutorial();

                // Update places
                Dispatcher.BeginInvoke(async () =>
                {
                    var placesReply = await ServerAPIManager.Instance.GetPlaces();
                    if (!placesReply.IsSuccessful)
                    {
                        FSLog.Error("Failed to update places");
                    }
                });
            }

            if (ApplicationStates.IsDashboardRequestNeeded)
            {
                ApplicationStates.IsDashboardRequestNeeded = false;
                GetDashboard();
            }

            if (ApplicationStates.InvitationEMailStarted)
            {
                ApplicationStates.InvitationEMailStarted = false;

                FSLog.Info("Handling email invitation");
                //await InviteUserAsync(ApplicationStates.InvitationEMailChosen);

                var email = ApplicationStates.InvitationEMail;

                InviteContact(email, ApplicationStates.InvitationDisplayName);
            }

            AppRater.Check();

            CheckLocationService();

            SetProgressOfCurrentUsersPin(LocationService.Instance.CurrentMode);
            LocationService.Instance.ModeChanged += LocationService_ModeChanged;

        }


        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            LocationService.Instance.ModeChanged -= LocationService_ModeChanged;
            SettingsManager.SettingChanged -= SettingsManager_SettingChanged;
            MapControl.Tap -= MapControl_Tap;
        }

        #endregion

        #region Tutorials

        private void ShowWelcomeTutorial()
        {
            FSLog.Info();

            //SettingsManager.IsWelcomeBubbleShown = false;

            if (SettingsManager.IsWelcomeBubbleShown || WelcomeTutorialBubble != null)
            {
                FSLog.Info("Already shown");
                return;
            }

            WelcomeTutorialBubble = new WelcomeBubble();
            this.OverlayContainer.Children.Add(WelcomeTutorialBubble);
            // Point to about middle of the screen where the pin is
            WelcomeTutorialBubble.Bubble.PointAt = new Point(240, 272);
            WelcomeTutorialBubble.Bubble.Show();

            WelcomeTutorialBubble.Dismissed += (object s1, EventArgs e2) =>
            {
                OverlayContainer.Children.Remove(WelcomeTutorialBubble);
                WelcomeTutorialBubble = null;

                SettingsManager.IsWelcomeBubbleShown = true;

                if (IsFirstDashboardRequestComplete)
                {
                    ShowAddFriendTutorial();
                }
                else
                {
                    FSLog.Info("First dashboard request not complete");
                }
            };
        }

        private void ShowNewPeopleTutorial()
        {
            FSLog.Info();

            //SettingsManager.IsNewPeopleTutorialShown = false;
            //ApplicationStates.InvitationDisplayName = "test";

            if (SettingsManager.IsNewPeopleTutorialShown || CanSeeYouTutorialBubble != null)
            {
                FSLog.Info("Already shown");
                return;
            }

            // Set by headerpanel when looking for contact info
            if (string.IsNullOrWhiteSpace(ApplicationStates.InvitationDisplayName))
            {
                FSLog.Warning("No display name in application state");
                return;
            }

            CanSeeYouTutorialBubble = new CanSeeYouTutorialBubble();
            this.OverlayContainer.Children.Add(CanSeeYouTutorialBubble);
            // Point to about middle of the screen where the pin is
            CanSeeYouTutorialBubble.Bubble.PointerPosition = PointerHint.Bottom;

            //NewPeopleBubble.Bubble.PointedElement = HeaderPanel.AddContactButton;
            CanSeeYouTutorialBubble.Bubble.PointAt = new Point(200, 700);
            CanSeeYouTutorialBubble.Bubble.BubbleContainer.Margin = new Thickness(12, 0, 12, 0);
            CanSeeYouTutorialBubble.Names = ApplicationStates.InvitationDisplayName;
            CanSeeYouTutorialBubble.Bubble.Show();

            CanSeeYouTutorialBubble.Bubble.Dismissed += (object s1, EventArgs e2) =>
            {
                OverlayContainer.Children.Remove(CanSeeYouTutorialBubble);
                CanSeeYouTutorialBubble = null;

                SettingsManager.IsNewPeopleTutorialShown = true;
            };

            var closeTimer = FSCall.Delayed(() =>
            {
                if (CanSeeYouTutorialBubble != null)
                {
                    CanSeeYouTutorialBubble.Bubble.Dismiss();
                }
            }, TimeSpan.FromSeconds(10));

            CanSeeYouTutorialBubble.Bubble.Dismissing += (object sender, EventArgs e2) =>
            {
                FSLog.Debug();

                if (closeTimer != null)
                {
                    closeTimer.Stop();
                    closeTimer = null;
                }
            };

            // Closing with delay
            CanSeeYouTutorialBubble.Bubble.TapOutsideCloses = true;
        }

        private void ShowAddFriendTutorial()
        {
            //SettingsManager.IsAddFriendTutorialShown = false;

            FSLog.Info();

            if (SettingsManager.IsAddFriendTutorialShown
                || People.Count > 1
                || AddFriendTutorialBubble != null)
            {
                FSLog.Info("Already shown");
                return;
            }

            AddFriendTutorialBubble = new AddFriendTutorialBubble();
            this.OverlayContainer.Children.Add(AddFriendTutorialBubble);
            // Point to about middle of the screen where the pin is
            AddFriendTutorialBubble.Bubble.PointAt = new Point(240, 100);
            AddFriendTutorialBubble.Bubble.Show();

            AddFriendTutorialBubble.Dismissed += (object s1, BubbleButtonEventArgs e2) =>
            {
                OverlayContainer.Children.Remove(AddFriendTutorialBubble);
                AddFriendTutorialBubble = null;

                SettingsManager.IsAddFriendTutorialShown = true;

                if (e2.Button == BubbleButton.Yes)
                {
                    ContactsManager.Instance.ShowInviteUserChooser();
                }
            };
        }

        #endregion

        /// <summary>
        /// Used to display the progress icon on pin of the current user.
        /// Triggered when LocationService starts or stops resolving the current location.
        /// </summary>
        /// <param name="mode"></param>
        private void SetProgressOfCurrentUsersPin(ReportingMode mode)
        {
            // Find the pin of current user, make show progress
            foreach (MapOverlay overlay in MapPushPinLayer)
            {
                MapPushPin pin = overlay.Content as MapPushPin;
                if (pin.Person.IsCurrent)
                {
                    pin.IsInProgress = mode == ReportingMode.Reporting;
                    break;
                }
            }
        }

        void LocationService_ModeChanged(object sender, StatusEventArgs e)
        {
            FSLog.Info();

            SetProgressOfCurrentUsersPin(e.Mode);
        }

        /// <summary>
        /// Displays error message if location service is disabled or not available at all
        /// </summary>
        private void CheckLocationService()
        {
            if (ApplicationStates.IsLocationServiceChecked) return;
            ApplicationStates.IsLocationServiceChecked = true;

            if (!GPSReporter.Instance.IsServiceEnabled)
            {
                MessageBox.Show(Localized.ErrorNoLocationService);
            }
        }

        /// <summary>
        /// Setup cartographic mode for the map
        /// </summary>
        private void SetupMap()
        {
            MapControl.Tap += MapControl_Tap;

            MapMode mapmode = SettingsManager.CartographicMode;
            switch (mapmode)
            {
                case MapMode.Aerial:
                    MapControl.CartographicMode = MapCartographicMode.Aerial;
                    break;

                case MapMode.Terrain:
                    MapControl.CartographicMode = MapCartographicMode.Terrain;
                    break;

                case MapMode.Hybrid:
                    MapControl.CartographicMode = MapCartographicMode.Hybrid;
                    break;

                case MapMode.Default:
                case MapMode.Road:
                default:
                    MapControl.CartographicMode = MapCartographicMode.Road;
                    break;
            }
        }

        /// <summary>
        /// Closes current callout
        /// </summary>
        private void CloseCalloutOverlay()
        {
            if (MapCallout != null)
            {
                FSLog.Info("Closing overlay");

                MapCallout.IsVisible = false;
            }
        }

        /// <summary>
        /// Tapping on the map closes callout and overlapping effect.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void MapControl_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            FSLog.Info();

            // This is to not move map on first location result
            // if user has done something already.
            IsInitialUserLocationShown = true;

            CloseCalloutOverlay();

            ResetOverlappingTransforms();
        }

        public void InviteContact(string email, string displayname)
        {
            Dispatcher.BeginInvoke(async () =>
            {
                var result = await ContactsManager.Instance.SearchContactWithEmail(email);
                if (result.Results == null || result.Results.Count() == 0)
                {
                    var r = MessageBox.Show(
                    Localized.InvitingEmailNotFound,
                    Localized.Inviting,
                    MessageBoxButton.OKCancel);

                    if (r == MessageBoxResult.OK)
                    {
                        var saveEmailTask = new SaveEmailAddressTask();
                        saveEmailTask.Email = email;
                        saveEmailTask.Completed += SaveContactTask_Completed;
                        saveEmailTask.Show();
                    }
                    return;
                }

                FSLog.Info("Sending request");
                await ContactsManager.Instance.InviteUserAsync(result.Filter);
            });
        }

        /// <summary>
        /// Called after user has saved or canceled adding email to contact.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void SaveContactTask_Completed(object sender, TaskEventArgs e)
        {
            FSLog.Info(e.TaskResult);

            (sender as SaveEmailAddressTask).Completed -= SaveContactTask_Completed;

            // Invite even if user didn't add email address

            Dispatcher.BeginInvoke(async () =>
                await ContactsManager.Instance.InviteUserAsync(ApplicationStates.InvitationEMail));

        }

        /** Get dashboard with given delay, re-scheduled automatically with 30 second delay */
        private void GetDashboard(long timeoutSeconds = 0)
        {
#if STORE_SCREENSHOTS
            return;
#endif
            var timeout = TimeSpan.FromSeconds(timeoutSeconds);

            if (DashboardTimer != null)
            {
                // Prevent duplicates
                DashboardTimer.Stop();
            }

            DashboardTimer = FSCall.Delayed(async () =>
            {
                SystemTrayProgressIndicator.TaskCount++;
                SetProgressOfOtherUserPins(true);
                try
                {
                    var reply = await ServerAPIManager.Instance.GetDashboard();
                    if (!reply.IsSuccessful)
                    {
                        if (reply.HttpStatus == HttpStatusCode.Unauthorized)
                        {
                            FSLog.Warning(reply.HttpStatus, "user unauthorized, clearing tokens");
                            ServerAPIManager.Instance.ClearAuthentication();

                            MessageBox.Show(Localized.ApiError401);
                            NavigationService.Navigate(new Uri("/Pages/SignupPage.xaml", UriKind.Relative));
                            // Do not re-schedule timer
                            return;
                        }

                        FSLog.Error("Request failed");
                    }

                    // Show if user closed welcome bubble before the dashboard request finished.
                    if (!IsFirstDashboardRequestComplete
                        && SettingsManager.IsWelcomeBubbleShown)
                    {
                        FSLog.Info("Welcome bubble shown, show add friend tutorial");
                        ShowAddFriendTutorial();
                    }
                    //UpdateVisibilityIcon();

                    // TODO: Configurable
                    GetDashboard(30);

                }
                finally
                {
                    SystemTrayProgressIndicator.TaskCount--;
                    SetProgressOfOtherUserPins(false);
                    IsFirstDashboardRequestComplete = true;
                }

            }, timeout);
        }

        /** Sets progress status for people other than current user */
        private void SetProgressOfOtherUserPins(bool show)
        {
            foreach (MapOverlay overlay in MapPushPinLayer)
            {
                var pin = overlay.Content as MapPushPin;
                if (pin.Person.IsCurrent) continue;

                pin.IsInProgress = show;
            }
        }

        /** Remove existing pins and add new */
        private void RefreshAllPins()
        {
            RefreshAllPlaces();

            foreach (MapOverlay overlay in MapPushPinLayer)
            {
                var pin = (overlay.Content as MapPushPin);
                pin.Person.PropertyChanged -= Person_PropertyChanged;
                pin.IconContainer.Tap -= MapPushPin_Tap;
            }

            MapPushPinLayer.Clear();

            // Add current user last and on top
            foreach (Person person in SettingsManager.People)
            {
                if (person.IsCurrent) continue;

                AddUserPin(person);
            }
            AddUserPin(SettingsManager.CurrentUser);

            if (People != null)
            {
                People.CollectionChanged -= People_CollectionChanged;
            }

            People = SettingsManager.People;
            People.CollectionChanged += People_CollectionChanged;
        }

        /** Detect changes with list of people. Usually if dashboard request adds new contact. */
        void People_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Add new PINs
            if (e.Action == NotifyCollectionChangedAction.Add
                || e.Action == NotifyCollectionChangedAction.Replace)
            {
                FSLog.Info("Adding {0} new PINs".FormatInvariant(e.NewItems.Count));
                foreach (Person p in e.NewItems)
                {
                    RefreshPin(p);
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                FSLog.Info("a PIN was removed:", e.OldItems.Count);
                // Shouldn't happen so not trying to do nicely. Just refresh all.
                RefreshAllPins();
            }
        }

        void Person_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Position" || e.PropertyName == "IsVisible")
            {
                // Run in UI thread
                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    RefreshPin((Person)sender);
                });
            }
        }

        void SettingsManager_SettingChanged(object sender, SettingsChangedEventArgs e)
        {
            if (e.Key == "People" && People != SettingsManager.People)
            {
                // If new instance
                Deployment.Current.Dispatcher.BeginInvoke(RefreshAllPins);
            }
        }

        /// <summary>
        /// Refresh pins visibility and position.
        /// </summary>
        /// <param name="person"></param>
        private void RefreshPin(Person person)
        {
            FSLog.Debug("Refreshing pin of", person.BackendId);

            if (string.IsNullOrWhiteSpace(person.BackendId))
            {
                FSLog.Info("Backend ID not set yet");
                return;
            }

            if (person.IsCurrent)
            {
                // Refresh
                UpdateVisibilityIcon(person.IsVisible);
            }

            var overlay = GetOverlay(person);
            if (overlay != null)
            {
                var center = new GeoCoordinate(
                                    person.Position.Latitude,
                                    person.Position.Longitude);

                overlay.GeoCoordinate = center;

                if (person.IsCurrent && !IsInitialUserLocationShown)
                {
                    IsInitialUserLocationShown = true;
                    MapControl.SetView(center, MapControl.ZoomLevel);
                }

                // Update callout position
                if (MapCalloutOverlay != null
                    && MapCallout != null
                    && MapCallout.IsVisible
                    && MapCallout.Model != null)
                {
                    if (MapCallout.Model.Person.BackendId.Equals(person.BackendId))
                    {
                        FSLog.Info("Updating callout position");

                        // Considering the callout to be 'focused' if shown
                        // so zoom the map to it if changes
                        MapControl.SetView(center, MapControl.ZoomLevel);
                        MapCalloutOverlay.GeoCoordinate = center;
                    }
                }
                return;
            }

            // Not found, add new
            AddUserPin(person);

        }

        /// <summary>
        /// Add a new pin for the given person.
        /// </summary>
        /// <param name="person">Person to add the pin for</param>
        private void AddUserPin(Person person)
        {
            var center = new GeoCoordinate(person.Position.Latitude, person.Position.Longitude);

            // The control to display
            var pin = new MapPushPin();
            pin.Person = person;
            pin.IconContainer.Tap += MapPushPin_Tap;

            // Overlay positions to screen based on geocoordinate
            MapOverlay pinOverlay = new MapOverlay();
            pinOverlay.Content = pin;
            pinOverlay.GeoCoordinate = center;
            pinOverlay.PositionOrigin = new Point(0.5, 0.5);

            if (person.IsCurrent)
            {
                MapPushPinLayer.Add(pinOverlay);
                // Need to refresh if user re-installs and the visibility is false
                UpdateVisibilityIcon(person.IsVisible);
            }
            else
            {
                MapPushPinLayer.Insert(0, pinOverlay);
            }

            person.PropertyChanged += Person_PropertyChanged;

            if (!IsInitialUserLocationShown && person.IsCurrent)
            {
                MapControl.Center = center;
#if DEBUG
#if STORE_SCREENSHOTS
                MapControl.ZoomLevel = 10;
#else
                MapControl.ZoomLevel = 5;
#endif

#else
                MapControl.ZoomLevel = 13;
#endif
            }
        }

        /// <summary>
        /// Find the overlay which is displaying pin for the person.
        /// </summary>
        /// <param name="pin"></param>
        /// <returns></returns>
        private MapOverlay GetOverlay(Person person)
        {
            foreach (var overlay in MapPushPinLayer)
            {
                var pin = overlay.Content as MapPushPin;
                if (pin == null)
                {
                    FSLog.Error("null pin, bad overlay!");
                    continue;
                }

                if (string.IsNullOrEmpty(pin.Person.BackendId))
                {
                    continue;
                }

                if (pin.Person == person
                    || pin.Person.BackendId.Equals(person.BackendId, StringComparison.InvariantCulture))
                {
                    return overlay;
                }
            }

            return null;
        }

        /// <summary>
        /// Triggered when user taps a PIN.
        /// When tapping on PIN of another user, displays MapCallout 
        /// with information and communication actions.
        /// 
        /// If pin is overlapping or being overlapped by other pins,
        /// an animation is created showing the hidden pins.
        /// 
        /// Overlapping effect is removed with animation if user chooses a pin, 
        /// zooms or taps the map.
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void MapPushPin_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            // Don't move map if user has done something already
            IsInitialUserLocationShown = true;

            var currentPin = (sender as FrameworkElement).DataContext as MapPushPin;
            var person = currentPin.Person;
            var center = new GeoCoordinate(person.Position.Latitude, person.Position.Longitude);

            // Set ZIndex of others            
            foreach (MapPushPin otherPin in Pins)
            {
                if (currentPin == otherPin) continue;
                otherPin.ZIndex = 0;
            }
            currentPin.ZIndex = 1;

            if (ResetOverlappingTransforms() || !HandleOverlapping(currentPin))
            {
                // Show callout if no overlapping 
                // or one selected from re-arranged overlapped pins
                if (MapCallout.Model != null)
                {
                    // To release event handlers
                    MapCallout.Model.Dispose();
                }

                MapCallout.Model = new PersonViewModel(person);

                if (MapCalloutOverlay == null)
                {
                    MapCalloutOverlay = new MapOverlay();
                    CalloutsLayer.Add(MapCalloutOverlay);
                }

                MapCalloutOverlay.Content = MapCallout;
                MapCalloutOverlay.GeoCoordinate = center;
                MapCalloutOverlay.PositionOrigin = new Point(0.5, 0.5);

                MapCallout.IsVisible = true;

                // Prevent propagation to MapControl.Tap, which closes the callout
                e.Handled = true;
            }

            MapControl.SetView(center, MapControl.ZoomLevel);

        }

        #region Routines handling pin overlapping

        /// <summary>
        /// If pins are overlapping, creates animation which removes the overlap effect.
        /// </summary>
        /// <returns>true if pins are overlapping</returns>
        private bool ResetOverlappingTransforms()
        {
            MapControl.ZoomLevelChanged -= MapControl_ZoomLevelChanged;

            var overlapping = false;
            var story = new Storyboard();

            foreach (MapPushPin pin in Pins)
            {
                var transform = pin.RenderTransform as TranslateTransform;
                if (transform.X != 0 || transform.Y != 0)
                {
                    FSAnim.Translate(transform, toX: 0, toY: 0, story: story);
                    overlapping = true;
                }
            }
            story.Begin();
            return overlapping;
        }


        /// <summary>
        /// If pins are overlapping, re-arrange them so that user can see them 
        /// </summary>
        /// <param name="currentPin">The pin to check if it overlaps other pins</param>
        /// <returns>true if overlapping, false if not.</returns>
        private bool HandleOverlapping(MapPushPin currentPin)
        {
            var story = new Storyboard();
            int index = 1;
            foreach (var tuple in GetOverlapping(currentPin))
            {

                // Move to left or right
                double amount = (index % 2) == 0 ? -OVERLAP_TRANSFORM : OVERLAP_TRANSFORM;
                amount = (Math.Ceiling(index * 0.5) * amount);

                FSAnim.Translate(tuple.Item1.RenderTransform,
                    toX: -tuple.Item2.X + amount,
                    toY: -tuple.Item2.Y,
                    story: story);
                index++;

                tuple.Item1.ZIndex = 9999 - index;
            }

            currentPin.ZIndex = 9999;

            if (index != 1)
            {
                story.Begin();
                MapControl.ZoomLevelChanged -= MapControl_ZoomLevelChanged;
                story.Completed += OverlappingStory_Completed;
            }

            return index != 1;
        }

        /// <summary>
        /// Starts tracking zoom changes to reset overlapping if user
        /// starts zooming the map.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OverlappingStory_Completed(object sender, EventArgs e)
        {
            (sender as Storyboard).Completed -= OverlappingStory_Completed;

            ZoomChanges = 0;
            MapControl.ZoomLevelChanged += MapControl_ZoomLevelChanged;

        }

        /// <summary>
        /// Counter for zoom level changes to prevent race events reseting
        /// the overlapping too soon.
        /// </summary>
        private int ZoomChanges = 0;

        /// <summary>
        /// Used to reset overlapping if user zooms the map.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void MapControl_ZoomLevelChanged(object sender, MapZoomLevelChangedEventArgs e)
        {
            // Avoid race events
            if (ZoomChanges > 3)
            {
                ResetOverlappingTransforms();
            }

            ZoomChanges++;
        }

        /// <summary>
        /// Iterate over pins that are overlapping or being overlapped by given pin.
        /// </summary>
        /// <param name="currentPin"></param>
        /// <returns></returns>
        private IEnumerable<Tuple<MapPushPin, Point>> GetOverlapping(MapPushPin currentPin)
        {
            foreach (MapPushPin pin in Pins)
            {
                if (currentPin == pin) continue;

                // Negative is down or left, positive up or right
                var p = pin.TransformToVisual(currentPin).Transform(new Point());
                //FSLog.Info(p);

                if (Math.Abs(p.X) < OVERLAP_TRANSFORM && Math.Abs(p.Y) < OVERLAP_TRANSFORM)
                {
                    yield return new Tuple<MapPushPin, Point>(pin, p);
                }
            }
        }

        #endregion

        #region Routines for places

        public const double SHOW_PLACE_NAMES_ZOOMLEVEL = 10;

        bool _IsPlaceNamesShown = true;
        private void CheckPlaceVisibility()
        {
            bool show = _IsPlaceNamesShown;
            if (MapControl.ZoomLevel >= SHOW_PLACE_NAMES_ZOOMLEVEL)
            {
                show = true;
            }
            else
            {
                show = false;
            }

            if (show == _IsPlaceNamesShown) return;
            _IsPlaceNamesShown = show;

            foreach (MapOverlay overlay in PlacesLayer)
            {
                (overlay.Content as FrameworkElement).Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            }

        }

        void MapControl_UpdatePlacesVisibilityOnZoomLevelChange(object sender, MapZoomLevelChangedEventArgs e)
        {
            CheckPlaceVisibility();
        }

        Dictionary<Place, MapElement> PlaceRadiusOverlays = new Dictionary<Place, MapElement>();
        Dictionary<Place, MapOverlay> PlaceNameOverlays = new Dictionary<Place, MapOverlay>();
        private void RefreshAllPlaces()
        {
            MapControl.MapElements.Clear();
            PlacesLayer.Clear();
            _IsPlaceNamesShown = true;

            foreach (var place in SettingsManager.Places)
            {
                AddPlace(place);
            }

            CheckPlaceVisibility();

            SettingsManager.Places.CollectionChanged += Places_CollectionChanged;
        }

        void Places_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (var item in e.OldItems)
                {
                    var place = item as Place;
                    RemovePlace(place);
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (var item in e.NewItems)
                {
                    var place = item as Place;
                    AddPlace(place);
                }
            }
        }

        /// <summary>
        /// Use to remove a place from map. Doesn't remove from settings.
        /// </summary>
        /// <param name="place"></param>
        private void RemovePlace(Place place)
        {
            if (PlaceRadiusOverlays.ContainsKey(place))
            {
                // Remove circle overlay
                var element = PlaceRadiusOverlays[place];
                MapControl.MapElements.Remove(element);
                PlaceRadiusOverlays.Remove(place);

                // Remove name overlay
                MapOverlay overlay = PlaceNameOverlays[place];
                PlacesLayer.Remove(overlay);
                PlaceNameOverlays.Remove(place);
            }
            else
            {
                FSLog.Warning("No circle for place:", place.Id);
            }
        }

        /// <summary>
        /// Use to add a place to map. Doesn't add to settings.
        /// </summary>
        /// <param name="place"></param>
        private void AddPlace(Place place)
        {
            var center = new GeoCoordinate();
            center.Latitude = place.Latitude;
            center.Longitude = place.Longitude;

            // Add circle overlay
            var circle = AddRadiusOverlay(center, place.Radius, (Color)Resources["Main"]);
            PlaceRadiusOverlays[place] = circle;

            // Add name overlay
            var text = new MapPlaceName();
            text.DataContext = place;

            MapOverlay overlay = new MapOverlay();
            overlay.GeoCoordinate = center;
            overlay.Content = text;
            overlay.PositionOrigin = new Point(0.5, 0.5);
            PlaceNameOverlays[place] = overlay;

            PlacesLayer.Add(overlay);
        }

        /// <summary>
        /// Creates a circle polygon using the haversine formula to display accuracy radius
        /// </summary>
        /// <param name="center"></param>
        /// <param name="radiusInMeters"></param>
        /// <param name="fillColor"></param>
        /// 
        private MapPolygon AddRadiusOverlay(GeoCoordinate center,
            double radiusInMeters, Color fillColor)
        {

            fillColor.A = (byte)(0xFF * 0.5);

            MapPolygon circle = new MapPolygon();
            circle.FillColor = fillColor;

            GeoCoordinateCollection polygonPoints = new GeoCoordinateCollection();
            const double earthRadius = 6371;
            var latRad = center.Latitude.ToRadians();
            var lonRad = center.Longitude.ToRadians();
            var angularDistance = radiusInMeters / 1000 / earthRadius;

            // More steps => less edgy but slower. 
            // The difference between 1 stepAngle and 2 is not noticeable
            // but half the points. 5 starts to show.
            for (int stepAngle = 0; stepAngle <= 360; stepAngle += 2)
            {
                var stepRad = ((double)stepAngle).ToRadians();
                var stepLat = Math.Asin(Math.Sin(latRad) * Math.Cos(angularDistance)
                                + Math.Cos(latRad) * Math.Sin(angularDistance) * Math.Cos(stepRad));
                var stepLon = lonRad + Math.Atan2(Math.Sin(stepRad) * Math.Sin(angularDistance)
                                * Math.Cos(latRad), Math.Cos(angularDistance)
                                - Math.Sin(latRad) * Math.Sin(stepLat));

                var pt = new GeoCoordinate(stepLat.ToDegrees(),
                                           stepLon.ToDegrees());
                polygonPoints.Add(pt);
            }
            circle.Path = polygonPoints;

            MapControl.MapElements.Add(circle);

            return circle;
        }

        private void PlacesList_PlaceSelected(object sender, PlaceSelectedEventArgs e)
        {
            FSLog.Info();

            // Show map
            this.PivotControl.SelectedIndex = 0;

            // Zoom to place
            MapControl.SetView(
                new GeoCoordinate(e.Place.Latitude, e.Place.Longitude),
                PLACE_FOCUS_ZOOM);
        }

        #endregion

        /// Lazy load pivots except map for faster startup
        private void PivotControl_LoadingPivotItem(object sender, PivotItemEventArgs e)
        {
            FSLog.Info(e.Item);

            if (e.Item.Content != null)
            {
                // Content already loaded
                return;
            }

            // Don't zoom any more
            IsInitialUserLocationShown = true;

            if (e.Item == PivotControl.Items[PIVOT_INDEX_PLACES])
            {
                var list = new PlacesList();
                e.Item.Content = list;
                list.PlaceSelected += PlacesList_PlaceSelected;
            }
            else if (e.Item == PivotControl.Items[PIVOT_INDEX_MY_PEOPLE])
            {
                var list = new MyPeopleList();
                e.Item.Content = list;
                list.PersonSelected += PeopleList_PersonSelected;
            }
        }

        void PeopleList_PersonSelected(object sender, PersonSelectedEventArgs e)
        {
         
            FSLog.Info();

            if (!e.Person.IsVisible || !e.Person.ICanSee)
            {
                FSLog.Info("User not visible, don't show position");
                return;
            }

            // Show map
            this.PivotControl.SelectedIndex = 0;

            // Zoom to place
            MapControl.SetView(
                new GeoCoordinate(e.Person.Position.Latitude, e.Person.Position.Longitude),
                PERSON_FOCUS_ZOOM);
        
        }
    }
}