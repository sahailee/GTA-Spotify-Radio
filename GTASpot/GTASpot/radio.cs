using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using GTA;
using NativeUI;
using GTA.Native;
using SpotifyAPI.Web;

namespace GTASpot
{
    /// <summary>
    /// Static logger class that allows direct logging of anything to a text file
    /// </summary>
    public static class Logger
    {
        public static void Log(object message)
        {
            File.AppendAllText("scripts/GTASpotify.log", DateTime.Now + " : " + message + Environment.NewLine);
        }
    }

    public class NoActiveDeviceException : Exception
    {
        public NoActiveDeviceException()
        {

        }

        public NoActiveDeviceException(string message) 
            : base(message)
        {

        }

        public NoActiveDeviceException(string message, Exception inner)
            : base(message, inner)
        {

        }
    }
    public class SpotifyRadio : Script
    {
        private bool isEngineOn;
        private bool isSpotifyRadio;
        private int volume;
        private string defaultPlaylistId;
        private SpotifyController spotify;

        private UIMenu mainMenu;
        private MenuPool modMenuPool;
        private UIMenuItem playPausePlayback;
        private UIMenuItem skipTrack;
        private UIMenuItem prevTrack;
        private UIMenuItem volumeLevel;
        private UIMenuItem displayTrackName;

        private UIMenuCheckboxItem shuffleButton;
        private UIMenu playlistSubMenu;
        private UIMenuListItem playlistList;

        private UIMenu activeDevices;
        private UIMenuListItem deviceList;
        private int deviceListIndex = 1;

        private UIMenuCheckboxItem moodButton;
        private bool matchMood;
        private int moodUpdateTime = 2000;
        private int defaultMoodUpdateTime = 2000; // 30 seconds
        private int time;
        private bool hasDevice;
        private MoodMatch moodMatcher;

        private Scaleform DashboardScaleform;

        private string radioName = "RADIO_47_SPOTIFY_RADIO";

        private ScriptSettings config;
        private Keys menuKey;
        public SpotifyRadio()
        {

            spotify = new SpotifyController();
            moodMatcher = new MoodMatch();
            isEngineOn = false;
            isSpotifyRadio = false;
            isEngineOn = false;
            matchMood = false;
            hasDevice = false;
            time = 0;

            File.Create("scripts/GTASpotify.log").Close();
            config = ScriptSettings.Load("scripts/GTASpotify.ini");
            menuKey = config.GetValue("Options", "MenuKey", Keys.F10);
            volume = config.GetValue<int>("Options", "Volume", 100);
            if(volume < 0 || volume > 100) {
                volume = 100;
            }
            defaultPlaylistId = config.GetValue<string>("Options", "DefaultPlaylist", "");
            spotify.GuaranteeLogin();
            if (spotify.obtainedSpotifyClient)
            {
                spotify.InitialSpotifyRequests(defaultPlaylistId);
                DisableRadioAds();
                SetupMenu();
                DashboardScaleform = new Scaleform("dashboard");
                KeyDown += OnKeyDown;
                Tick += OnTick;
            }
            else
            {
                Logger.Log("ERROR: Did not login to Spotify");
            }


        }

        /*
         * Sets up the menu when you press menu key
         */
        private void SetupMenu()
        {
            modMenuPool = new MenuPool();
            mainMenu = new UIMenu("Spotify Radio", "Control your music");
            modMenuPool.Add(mainMenu);

            playPausePlayback = new UIMenuItem("Play/Pause Track");
            skipTrack = new UIMenuItem("Skip Track");
            prevTrack = new UIMenuItem("Previous Track");
            volumeLevel = new UIMenuItem("Set Volume Level");
            displayTrackName = new UIMenuItem("Get Track Name");

            var current = spotify.GetCurrentPlayback();
            if(current == null)
            {
                shuffleButton = new UIMenuCheckboxItem("Shuffle Mode", false);
            }
            else
            {
                shuffleButton = new UIMenuCheckboxItem("Shuffle Mode", current.ShuffleState);
            }
            moodButton = new UIMenuCheckboxItem("Match Mood", false);


            mainMenu.AddItem(playPausePlayback);
            mainMenu.AddItem(skipTrack);
            mainMenu.AddItem(prevTrack);
            mainMenu.AddItem(volumeLevel);
            mainMenu.AddItem(shuffleButton);


            mainMenu.OnItemSelect += OnMainMenuItemSelect;
            mainMenu.OnCheckboxChange += OnCheckboxChange;
            SetupPlaylistMenu();
            SetupActiveDevicesMenu();
            mainMenu.AddItem(displayTrackName);
            mainMenu.AddItem(moodButton);

            mainMenu.RefreshIndex();

        }

        /*
         * Refreshes the playlists in the playlist sub menu
         */
        private void RefreshPlaylists(List<dynamic> playlistNames, List<string> playlistUris)
        {
            playlistNames.Clear();
            playlistUris.Clear();
            var page = spotify.GetCurrentPlaylist();
            playlistNames.Add("Saved Tracks");
            playlistUris.Add("");
            var allPages = spotify.Page(page);
            if (allPages != null)
            {
                foreach (var item in allPages)
                {
                    playlistNames.Add(item.Name);
                    playlistUris.Add(item.Uri);
                }
            }
            playlistList = new UIMenuListItem("Pick Playlist: ", playlistNames, 0);
        }

        /*
         * Sets up the submenu that displays user's playlist names
         * The playlist names are retrieved from API
         */
        private void SetupPlaylistMenu()
        {
            playlistSubMenu = modMenuPool.AddSubMenu(mainMenu, "Set Radio Playlist");

            UIMenuItem refresh = new UIMenuItem("Refresh Playlists");
            playlistSubMenu.AddItem(refresh);

            List<dynamic> playlistNames = new List<dynamic>();
            List<string> playlistUris = new List<string>();

            RefreshPlaylists(playlistNames, playlistUris);
            playlistSubMenu.AddItem(playlistList);

            UIMenuItem setPlaylist = new UIMenuItem("Confirm Selection");
            playlistSubMenu.AddItem(setPlaylist);

            playlistSubMenu.RefreshIndex();


            playlistSubMenu.OnItemSelect += (sender, item, index) =>
            {
                if (item == setPlaylist)
                {
                    int listIndex = playlistList.Index;
                    if(listIndex == 0)
                    {
                        spotify.ResumePlayback("");
                    }
                    else
                    {
                        spotify.ResumePlayback(playlistUris[listIndex]);
                    }
                    time = (int)(moodUpdateTime - GTA.Game.FPS);
                    modMenuPool.CloseAllMenus();
                }
                else if(item == refresh)
                {
                    RefreshPlaylists(playlistNames, playlistUris);
                    playlistSubMenu.RemoveItemAt(deviceListIndex);
                    playlistSubMenu.RemoveItemAt(deviceListIndex);
                    playlistSubMenu.AddItem(playlistList);
                    playlistSubMenu.AddItem(setPlaylist);
                    playlistSubMenu.RefreshIndex();
                }
            };
        }

        /*
         * Refreshes the menu that holds the list of devices
         */
        private void RefreshDeviceList(List<dynamic> deviceNames, List<string> deviceIDs)
        {
            deviceNames.Clear();
            deviceIDs.Clear();
            var devices = spotify.GetDevices();
            if (devices == null || devices.Devices.Count == 0)
            {
                deviceNames.Add("");
                deviceIDs.Add("");
            }
            else 
            {
                foreach (var item in devices.Devices)
                {
                    deviceNames.Add(item.Name);
                    deviceIDs.Add(item.Id);
                }
            }
            deviceList = new UIMenuListItem("Pick Device: ", deviceNames, 0);
        }

        /*
         * Instantiates Active Device menu
         */
        private void SetupActiveDevicesMenu()
        {
            activeDevices = modMenuPool.AddSubMenu(mainMenu, "Select Playback Device");
            List<dynamic> deviceNames = new List<dynamic>();
            List<string> deviceIDs = new List<string>();

            UIMenuItem refresh = new UIMenuItem("Refresh Devices");
            activeDevices.AddItem(refresh);

            RefreshDeviceList(deviceNames, deviceIDs);
            activeDevices.AddItem(deviceList);
            UIMenuItem setDevice = new UIMenuItem("Confirm Selection");
            activeDevices.AddItem(setDevice);

            activeDevices.RefreshIndex();

            activeDevices.OnItemSelect += (sender, item, index) =>
            {
                if (item == setDevice)
                {
                    int listIndex = deviceList.Index;
                    string deviceID = deviceIDs[listIndex];
                    try {
                        spotify.ResumePlayback(deviceID, defaultPlaylistId);
                        time = (int)(moodUpdateTime - GTA.Game.FPS);
                        hasDevice = true;
                        if (isSpotifyRadio)
                        {
                            Wait(1000);
                            spotify.SetVolume(volume);
                        }
                        else
                        {
                            spotify.SetVolume(0);
                        }
                        modMenuPool.CloseAllMenus();
                    }
                    catch(AggregateException ex)
                    {
                        if(ex.InnerException.GetType() == typeof(APIException)) {
                            APIException x = (APIException)ex.InnerException;
                            if (x.Response.StatusCode == System.Net.HttpStatusCode.BadGateway)
                            {
                                GTA.UI.Notification.Show("Bad gateway, try to refresh and try again.");
                            }
                            Logger.Log("Transfer Device: " + x.Response.StatusCode + " - " + x.Message);
                        }
                        else
                        {
                            Logger.Log("Transfer Device: " + ex.Message);
                        }
                        hasDevice = false;
                        modMenuPool.CloseAllMenus();
                    }
                    catch(NoActiveDeviceException ex)
                    {
                        Logger.Log("Transfer Device: " + ex.Message);
                        modMenuPool.CloseAllMenus();
                    }

                }
                else if(item == refresh)
                {

                    RefreshDeviceList(deviceNames, deviceIDs);
                    activeDevices.RemoveItemAt(deviceListIndex);
                    activeDevices.RemoveItemAt(deviceListIndex);
                    activeDevices.AddItem(deviceList);
                    activeDevices.AddItem(setDevice);
                    activeDevices.RefreshIndex();
                }
            };
        }

        
        /*
         * Setup the actions for the menu items
         */
        private void OnMainMenuItemSelect(UIMenu sender, UIMenuItem item, int index)
        {
            try
            {
                time = 0;
                if (item == playPausePlayback)
                {
                    var current = spotify.GetCurrentlyPlaying();
                    if (current != null && current.IsPlaying)
                    {
                        spotify.PausePlayback();
                    }
                    else
                    {
                        spotify.ResumePlayback();

                    }
                }
                else if (item == skipTrack)
                {
                    spotify.SkipNext();
                    time = (int)(moodUpdateTime - GTA.Game.FPS);
                }
                else if (item == prevTrack)
                {
                    spotify.SkipPrevious();
                    time = (int)(moodUpdateTime - GTA.Game.FPS);
                }
                else if (item == volumeLevel)
                {
                    try
                    {
                        int vol = int.Parse(Game.GetUserInput(WindowTitle.CustomTeamName, volume.ToString(), 3));
                        if (vol < 0 || vol > 100)
                        {
                            throw new FormatException("Number out of range.");
                        }
                        volume = vol;
                        if (isSpotifyRadio)
                        {
                            Unmute();
                        }
                    }
                    catch (FormatException x)
                    {
                        GTA.UI.Notification.Show("Volume must be number between 0 and 100.");
                        Logger.Log("Invalid input: " + x.Message);
                    }

                }
                else if (item == displayTrackName)
                {
                    //Send user notification of current track name
                    var track = spotify.GetCurrentTrack();
                    if (track != null)
                    {
                        if (track.Type == ItemType.Track)
                        {
                            GTA.UI.Notification.Show(((FullTrack)track).Name + " by " + ((FullTrack)track).Artists[0].Name);
                        }
                        else
                        {
                            GTA.UI.Notification.Show(((FullEpisode)track).Name);
                        }
                        
                    }
                   
                }

            }
            catch (NoActiveDeviceException)
            {
                hasDevice = false;
                modMenuPool.CloseAllMenus();
                activeDevices.Visible = true;
                GTA.UI.Notification.Show("No active device found. Please set an active device using the menu");
            }
        }
        /*
         * Setup actions for check box items in the menu
         */
        private void OnCheckboxChange(UIMenu sender, UIMenuCheckboxItem item, bool Checked)
        {
            if(item == moodButton)
            {
                matchMood = Checked;
                if(Checked)
                {
                    time = 0;
                }
                else {
                    GTA.GameplayCamera.StopShaking();
                    GTA.UI.Screen.StopEffects();
                }
            }
            else if(item == shuffleButton)
            {
                spotify.SetShuffle(Checked);
            }
            

        }

        /*
         * Display menu when menu key is pressed
         */
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (spotify == null)
            {
                return;
            }
            if(e.KeyCode == menuKey && !modMenuPool.IsAnyMenuOpen())
            {
                mainMenu.Visible = !mainMenu.Visible;
            }

        }

        /*
         * Gets the name of the currently playing Radio Station
         */
        private string GetCurrentStationName()
        {
            var name = Function.Call<string>(GTA.Native.Hash.GET_PLAYER_RADIO_STATION_NAME);
            return name;
        }

        /*
         * Disables Weazel news and other GTA ads are disabled
         */
        private void DisableRadioAds()
        {
            Function.Call(GTA.Native.Hash.SET_RADIO_STATION_MUSIC_ONLY, radioName, true);
        }

        /*
         * Setter for isEngineOn
         */
        private void SetEngine(bool status)
        {
            if(spotify.obtainedSpotifyClient)
            {
                isEngineOn = status;
            }
        }

        /*
         * Call the Spotify function to mute audio
         */
        private void Mute()
        {
            try
            {
                spotify.SetVolume(0);
            } catch(NoActiveDeviceException) {}
        } 

        /*
         *  Call spotify function to unmute audio
         */
        private void Unmute()
        {
            try
            {
                spotify.SetVolume(volume);
                hasDevice = true;
            }
            catch(NoActiveDeviceException)
            {
                hasDevice = false;
                modMenuPool.CloseAllMenus();
                activeDevices.Visible = true;
                GTA.UI.Notification.Show("No active device found. Please set an active device using the menu");
            }
            
        }

        /*
         * Argument status is true if the current station is Spotify, false if not
         * If true, then play Spotify music, else mute it
         */
        private void SetRadioStation(bool status)
        {
            if (spotify.obtainedSpotifyClient)
            {
                isSpotifyRadio = status;

                if (status)
                {
                    Unmute();
                }
                else
                {
                    Mute();
                }
            }
            
        }


        /*
         * UUpdate the first person radio to display the song name
         */
        private void DisplaySpotifyTrackOnRadio(string artist, string track)
        {
            try
            {
                // Call function.
                DashboardScaleform.CallFunction("SET_RADIO",
                        "", "Spotify Radio",
                        artist, track);
            }
            catch (Exception exception)
            {
                Logger.Log("Failed to display trackname on Radio dashboard: " + exception.ToString());
            }

        }

        /*
         * Determine whether item is track or podcast then display on radio
         */
        private void UpdateInGameRadio(IPlayableItem track)
        {
            if(track.Type == ItemType.Track)
            {
                DisplaySpotifyTrackOnRadio(((FullTrack)track).Name, ((FullTrack)track).Artists[0].Name);
            }
            else
            {
                DisplaySpotifyTrackOnRadio(((FullEpisode)track).Name, "");
            }
            
        }

        private bool GetEngineStatus()
        {
            return isEngineOn;
        }

        private void OnTick(object sender, EventArgs e)
        {
            //Handle mod menu
            if(modMenuPool != null)
            {
                modMenuPool.ProcessMenus();
            }
            //Handle Spotify Radio
            if(Game.Player.Character.CurrentVehicle != null && Game.Player.Character.CurrentVehicle.IsEngineRunning && !GetEngineStatus() && GetCurrentStationName().Equals(radioName) && !isSpotifyRadio)
            {
                SetEngine(true);
                SetRadioStation(true);
            }
            else if(Game.Player.Character.CurrentVehicle == null && GetEngineStatus() && !GetCurrentStationName().Equals(radioName) && isSpotifyRadio)
            {
                SetEngine(false);
                SetRadioStation(false);
            }
            else if(Game.Player.Character.CurrentVehicle == null && GetEngineStatus())
            {
                SetEngine(false);
                SetRadioStation(false);
            }
            else if (Game.Player.Character.CurrentVehicle != null && Game.Player.Character.CurrentVehicle.IsEngineRunning && !GetEngineStatus())
            {
                SetEngine(true);
            }
            else if(Game.Player.Character.CurrentVehicle != null && !Game.Player.Character.CurrentVehicle.IsEngineRunning && GetEngineStatus() && isSpotifyRadio)
            {
                SetEngine(false);
                SetRadioStation(false);
            }
            else if(Game.Player.Character.CurrentVehicle != null && !Game.Player.Character.CurrentVehicle.IsEngineRunning && GetEngineStatus())
            {
                SetEngine(false);
            }
            else if(GetEngineStatus() && GetCurrentStationName().Equals(radioName) && !isSpotifyRadio)
            {
                SetRadioStation(true);
            }
            else if(GetEngineStatus() && !GetCurrentStationName().Equals(radioName) && isSpotifyRadio)
            {
                SetRadioStation(false);
            }
            if(matchMood && GTA.GameplayCamera.IsShaking && !isSpotifyRadio)
            {
                GTA.GameplayCamera.StopShaking();
                GTA.UI.Screen.StopEffects();
            }
            if(isSpotifyRadio && hasDevice && time == 0)
            {
                var track = spotify.GetCurrentTrack();
                if(track != null)
                {
                    UpdateInGameRadio(track);
                    bool isTrack = track.Type == ItemType.Track;
                    if (matchMood && isTrack)
                    {
                        var trackid = ((FullTrack)track).Id;
                        if(trackid != null && trackid.Length != 0)
                        {
                            moodMatcher.GetMood(spotify.GetAudioFeatures(trackid));
                        }
                    }
                    float fps = GTA.Game.FPS;
                    moodUpdateTime = isTrack ? ((FullTrack)track).DurationMs : ((FullEpisode)track).DurationMs;
                    moodUpdateTime = (int)((moodUpdateTime * fps / 1000) + fps); // convert to tick time and one second worth of frames
                    var current = spotify.GetCurrentPlayback();
                    time = (int)(current.ProgressMs * fps / 1000);
                }

            }
            else if(isSpotifyRadio)
            {
                time = (time + 1) % moodUpdateTime;
            }
            else
            {
                time = 0;
            }
            

        }

    }
}
