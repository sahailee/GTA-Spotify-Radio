using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using GTA;
using SpotifyAPI.Web;
using NativeUI;
using System.Net.Http;
using System.Diagnostics;
using GTA.Native;

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
        private static SpotifyClient spotify;
        private bool isEngineOn;
        private bool isSpotifyRadio;
        private bool obtainedSpotifyClient;
        private int volume;
        private string defaultPlaylistId;

        private UIMenu mainMenu;
        private MenuPool modMenuPool;
        private UIMenuItem playPausePlayback;
        private UIMenuItem skipTrack;
        private UIMenuItem prevTrack;
        private UIMenuItem volumeLevel;
        private UIMenuItem displayTrackName;

        private UIMenu shuffleSubMenu;
        private UIMenu playlistSubMenu;
        private UIMenuListItem playlistList;

        private UIMenu activeDevices;
        private UIMenuListItem deviceList;
        private int deviceListIndex = 1;

        private Scaleform DashboardScaleform;

        private string radioName = "RADIO_47_SPOTIFY_RADIO";

        private string code;
        private static readonly HttpClient client = new HttpClient();

        private ScriptSettings config;
        private Keys menuKey;
        public SpotifyRadio()
        {
            
            obtainedSpotifyClient = false;
            isEngineOn = false;
            isSpotifyRadio = false;
            isEngineOn = false;

            File.Create("scripts/GTASpotify.log").Close();
            config = ScriptSettings.Load("scripts/GTASpotify.ini");
            menuKey = config.GetValue("Options", "MenuKey", Keys.F10);
            volume = config.GetValue<int>("Options", "Volume", 100);
            if(volume < 0 || volume > 100) {
                volume = 100;
            }
            defaultPlaylistId = config.GetValue<string>("Options", "DefaultPlaylist", "");
            GuaranteeLogin();
            if (obtainedSpotifyClient)
            {
                InitialSpotifyRequests();
                DisableRadioAds();
                SetupMenu();
              //DashboardScaleform = new Scaleform("dashboard");
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


            mainMenu.AddItem(playPausePlayback);
            mainMenu.AddItem(skipTrack);
            mainMenu.AddItem(prevTrack);
            mainMenu.AddItem(volumeLevel);


            mainMenu.OnItemSelect += OnMainMenuItemSelect;
            SetupShuffleMenu();
            SetupPlaylistMenu();
            SetupActiveDevicesMenu();
            mainMenu.AddItem(displayTrackName);

            mainMenu.RefreshIndex();

        }

        /*
         * Sets up the submenu that will enable or disable shuffle mode
         */
        private void SetupShuffleMenu()
        {
            shuffleSubMenu = modMenuPool.AddSubMenu(mainMenu, "Set Shuffle Mode");

            List<dynamic> shuffleStates = new List<dynamic>();
            shuffleStates.Add("On");
            shuffleStates.Add("Off");
            UIMenuListItem shuffleMode = new UIMenuListItem("Toggle Shuffle: ", shuffleStates, 0);
            shuffleSubMenu.AddItem(shuffleMode);

            UIMenuItem setShuffle = new UIMenuItem("Confirm Changes");
            shuffleSubMenu.AddItem(setShuffle);

            shuffleSubMenu.RefreshIndex();

            shuffleSubMenu.OnItemSelect += async (sender, item, index) =>
            {
                if (item == setShuffle)
                {
                    int listIndex = shuffleMode.Index;
                    if (listIndex == 0)
                    {
                        SpotifySetShuffle(true);
                    }
                    else
                    {
                        SpotifySetShuffle(false);
                    }
                    
                    modMenuPool.CloseAllMenus();
                }
            };
        }

        /*
         * Refreshes the playlists in the playlist sub menu
         */
        private void RefreshPlaylists(List<dynamic> playlistNames, List<string> playlistUris)
        {
            playlistNames.Clear();
            playlistUris.Clear();
            var page = SpotifyGetCurrentPlaylist();
            var allPages = SpotifyPage(page);
            playlistNames.Add("Saved Tracks");
            playlistUris.Add("");
            foreach (var item in allPages)
            {
                playlistNames.Add(item.Name);
                playlistUris.Add(item.Uri);
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


            playlistSubMenu.OnItemSelect += async (sender, item, index) =>
            {
                if (item == setPlaylist)
                {
                    int listIndex = playlistList.Index;
                    var request = new PlayerResumePlaybackRequest();
                    if(listIndex == 0)
                    {
                        request.Uris = SpotifyGetSavedTracks();
                    }
                    else
                    {
                        request.ContextUri = playlistUris[listIndex];
                    }
                    try
                    {
                        var isSuccess = await spotify.Player.ResumePlayback(request);
                        if (!isSuccess)
                        {
                            Logger.Log("Playlist resume unsuccessful");
                        }
                    }
                    catch (APIException ex)
                    {
                        Logger.Log("Playlist resume failed: " + ex.Message);
                    }
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
            var devices = SpotifyGetDevices();
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
                    var request = new PlayerResumePlaybackRequest();
                    request.DeviceId = deviceIDs[listIndex];
                    var curr = SpotifyGetCurrentlyPlaying();
                    if (curr == null)
                    {
                        if (defaultPlaylistId.Length != 0)
                        {
                            request.ContextUri = "spotify:playlist:" + defaultPlaylistId;
                        }                 
                        else
                        {
                            request.Uris = SpotifyGetSavedTracks();
                        }
                        
                    }
                    else
                    {
                        if(curr.Context != null)
                        {
                            request.ContextUri = curr.Context.Uri;
                            if (curr.Item != null)
                            {
                                request.OffsetParam = new PlayerResumePlaybackRequest.Offset();
                                request.OffsetParam.Uri = ((FullTrack)curr.Item).Uri;
                                request.PositionMs = curr.ProgressMs;
                            }

                        }
                        else if(defaultPlaylistId.Length != 0)
                        {
                            request.ContextUri = "spotify:playlist:" + defaultPlaylistId;
                        }
                        else 
                        {
                            request.Uris = SpotifyGetSavedTracks();
                            if(curr.Item != null)
                            {
                                request.Uris[0] = ((FullTrack)curr.Item).Uri;
                                request.OffsetParam = new PlayerResumePlaybackRequest.Offset();
                                request.OffsetParam.Uri = request.Uris[0];
                                request.PositionMs = curr.ProgressMs;
                            }
                            
                        }

                    }
                    try {
                        var task = spotify.Player.ResumePlayback(request);
                        task.Wait();
                        if (isSpotifyRadio)
                        {
                            SpotifySetVolume(volume);
                        }
                        else
                        {
                            SpotifySetVolume(0);
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
                if (item == playPausePlayback)
                {
                    var current = SpotifyGetCurrentlyPlaying();
                    if (current != null && current.IsPlaying)
                    {
                        SpotifyPausePlayback();
                    }
                    else
                    {
                        SpotifyResumePlayback();

                    }
                }
                else if (item == skipTrack)
                {
                    SpotifySkipNext();
                }
                else if (item == prevTrack)
                {
                    SpotifySkipPrevious();
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
                    var track = SpotifyGetCurrentTrack();
                    if (track != null)
                    {
                        GTA.UI.Notification.Show(track.Name + " by " + track.Artists[0].Name);
                    }
                }
            }
            catch (NoActiveDeviceException)
            {
                modMenuPool.CloseAllMenus();
                activeDevices.Visible = true;
                GTA.UI.Notification.Show("No active device found. Please set an active device using the menu");
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
         * Unused function. This would update the first person radio to display the song name
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
            if(obtainedSpotifyClient)
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
                SpotifySetVolume(0);
            } catch(NoActiveDeviceException) { }
        } 

        /*
         *  Call spotify function to unmute audio
         */
        private void Unmute()
        {
            try
            {
                SpotifySetVolume(volume);
            }
            catch(NoActiveDeviceException)
            {
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
            if (obtainedSpotifyClient)
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

        private DeviceResponse SpotifyGetDevices()
        {
            try
            {
                var task = spotify.Player.GetAvailableDevices();
                task.Wait();
                return task.Result;
            } catch(AggregateException ex)
            {
                Logger.Log("Fetching Available Devices Failed: " + ex.InnerException.Message);
            }
            return null;
        }
        /*
         * Wrapper for Spotify Skip Next
         */
        private bool SpotifySkipNext()
        {
            int i = 0;
            do
            {
                try
                {
                    var task = spotify.Player.SkipNext();
                    task.Wait();
                    return task.Result;
                }
                catch (AggregateException ex)
                {
                    if (ex.InnerException.GetType() == typeof(APIUnauthorizedException))
                    {
                        GuaranteeLogin();
                    }
                    else if (ex.InnerException.GetType() == typeof(APITooManyRequestsException))
                    {
                        Logger.Log("Skip next failed: Too many requests. " + ex.InnerException.Message);
                        i = -1;
                    }
                    else if (ex.InnerException.GetType() == typeof(APIException))
                    {
                        APIException e = (APIException)ex.InnerException;
                        if (e.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            throw new NoActiveDeviceException("No Active Spotify Device Found.");
                        }
                        else
                        {
                            Logger.Log("Skip next failed: " + e.Response?.StatusCode + e.Message);
                            i = -1;
                        }
                    }
                    else
                    {
                        Logger.Log("Skip next failed: " + ex.InnerException.Message);
                        i = -1;
                    }
                }
            } while (i == 0);
            return false;
        }

        /*
         * Wrapper for Spotify SKip previous
         */
        private bool SpotifySkipPrevious()
        {
            int i = 0;
            do
            {
                try
                {
                    var task = spotify.Player.SkipPrevious();
                    task.Wait();
                    return task.Result;
                }
                catch (AggregateException ex)
                {
                    if (ex.InnerException.GetType() == typeof(APIUnauthorizedException))
                    {
                        GuaranteeLogin();
                    }
                    else if (ex.InnerException.GetType() == typeof(APITooManyRequestsException))
                    {
                        Logger.Log("Skip prev failed: Too many requests. " + ex.InnerException.Message);
                        i = -1;
                    }
                    else if (ex.InnerException.GetType() == typeof(APIException))
                    {
                        APIException e = (APIException)ex.InnerException;
                        if (e.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            throw new NoActiveDeviceException("No Active Spotify Device Found.");
                        }
                        else
                        {
                            Logger.Log("Skip prev failed: " + e.Response?.StatusCode + e.Message);
                            i = -1;
                        }
                    }
                    else
                    {
                        Logger.Log("Skip prev failed: " + ex.InnerException.Message);
                        i = -1;
                    }
                }
            } while (i == 0);
            return false;
        }

        /*
         * Wrapper for Spoity Pause
         */
        private bool SpotifyPausePlayback()
        {
            int i = 0;
            do
            {
                try
                {
                    var task = spotify.Player.PausePlayback();
                    task.Wait();
                    return task.Result;
                }
                catch (AggregateException ex)
                {
                    if (ex.InnerException.GetType() == typeof(APIUnauthorizedException))
                    {
                        GuaranteeLogin();
                    }
                    else if (ex.InnerException.GetType() == typeof(APITooManyRequestsException))
                    {
                        Logger.Log("Pause failed: Too many requests. " + ex.InnerException.Message);
                        i = -1;
                    }
                    else if (ex.InnerException.GetType() == typeof(APIException))
                    {
                        Logger.Log("Pause failed: " + ((APIException)ex.InnerException).Response?.StatusCode + ex.InnerException.Message);
                        i = -1;
                    }
                    else
                    {
                        Logger.Log("Pause failed: " + ex.InnerException.Message);
                        i = -1;
                    }
                }
            } while (i == 0);
            return false;
        }

        /*
         * Wrapper for Spotify Resume
         */
        private bool SpotifyResumePlayback()
        {
            int i = 0;
            do
            {
                try
                {
                    var task = spotify.Player.ResumePlayback();
                    task.Wait();
                    return task.Result;
                }
                catch (AggregateException ex)
                {
                    if (ex.InnerException.GetType() == typeof(APIUnauthorizedException))
                    {
                        GuaranteeLogin();
                    }
                    else if (ex.InnerException.GetType() == typeof(APITooManyRequestsException))
                    {
                        Logger.Log("Resume failed:: Too many requests. " + ex.InnerException.Message);
                        i = -1;
                    }
                    else if (ex.InnerException.GetType() == typeof(APIException))
                    {
                        APIException e = (APIException)ex.InnerException;
                        if (e.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            throw new NoActiveDeviceException("No Active Spotify Device Found.");
                        }
                        else
                        {
                            Logger.Log("Resume failed: " + e.Response?.StatusCode + e.Message);
                            i = -1;
                        }
                    }
                    else
                    {
                        Logger.Log("Resume failed: " + ex.InnerException.Message);
                        i = -1;
                    }
                }
            } while (i == 0);
            return false;
        }

        /*
         * Wrapper for getting currently playing Spotify
         */
        private CurrentlyPlaying SpotifyGetCurrentlyPlaying()
        {
            int i = 0;
            do
            {
                try
                {
                    var task = spotify.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest());
                    task.Wait();
                    return task.Result;
                }
                catch (AggregateException ex)
                {
                    if (ex.InnerException.GetType() == typeof(APIUnauthorizedException))
                    {
                        GuaranteeLogin();
                    }
                    else if (ex.InnerException.GetType() == typeof(APITooManyRequestsException))
                    {
                        Logger.Log("Get current playing failed: Too many requests. " + ex.InnerException.Message);
                        i = -1;
                    }
                    else if (ex.InnerException.GetType() == typeof(APIException))
                    {
                        Logger.Log("Get current playing failed: " + ((APIException)ex.InnerException).Response?.StatusCode + ex.InnerException.Message);
                        i = -1;
                    }
                    else
                    {
                        Logger.Log("Get current playing failed: " + ex.InnerException.Message);
                        i = -1;
                    }
                }
            } while (i == 0);
            return null;
        }

        /*
         * Wrapper for Spotify set volume
         */
        private bool SpotifySetVolume(int volumeRequest)
        {
            int i = 0;
            do
            {
                try
                {
                    var task = spotify.Player.SetVolume(new PlayerVolumeRequest(volumeRequest));
                    task.Wait();
                    return task.Result;
                }
                catch (AggregateException ex)
                {
                    if (ex.InnerException.GetType() == typeof(APIUnauthorizedException))
                    {
                        GuaranteeLogin();
                    }
                    else if (ex.InnerException.GetType() == typeof(APITooManyRequestsException))
                    {
                        Logger.Log("Volume change failed: Too many requests. " + ex.InnerException.Message);
                        i = -1;
                    }
                    else if (ex.InnerException.GetType() == typeof(APIException))
                    {
                        APIException e = (APIException)ex.InnerException;
                        if (e.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            throw new NoActiveDeviceException("No Active Spotify Device Found.");
                        }
                        else
                        {
                            Logger.Log("Volume change failed: " + e.Response?.StatusCode + e.Message);
                            i = -1;
                        }
                    }
                    else
                    {
                        Logger.Log("Volume change failed: " + ex.InnerException.Message);
                        i = -1;
                    }
                }
            } while (i == 0);
            return false;
        }

        /*
         * Wrapper to set Spotify shuffle
         */
        private bool SpotifySetShuffle(bool shuffleRequest)
        {
            int i = 0;
            do
            {
                try
                {
                    var task = spotify.Player.SetShuffle(new PlayerShuffleRequest(shuffleRequest));
                    task.Wait();
                    return task.Result;
                    
                }
                catch (AggregateException ex)
                {
                    if (ex.InnerException.GetType() == typeof(APIUnauthorizedException))
                    {
                        GuaranteeLogin();
                    }
                    else if (ex.InnerException.GetType() == typeof(APITooManyRequestsException))
                    {
                        Logger.Log("Shuffle change failed: Too many requests. " + ex.InnerException.Message);
                        i = -1;
                    }
                    else if (ex.InnerException.GetType() == typeof(APIException))
                    {
                        Logger.Log("Shuffle change failed: " + ((APIException)ex.InnerException).Response?.StatusCode + ex.InnerException.Message);
                        i = -1;
                    }
                    else
                    {
                        Logger.Log("Shuffle change failed: " + ex.InnerException.Message);
                        i = -1;
                    }
                }
            } while (i == 0);
            return false;
        }

        /*
         * Wrapper to get Spotify current playlist
         */
        private Paging<SimplePlaylist> SpotifyGetCurrentPlaylist()
        {
            int i = 0;
            do
            {
                try
                {
                    var task = spotify.Playlists.CurrentUsers();
                    task.Wait();
                    return task.Result;
                }
                catch (AggregateException ex)
                {
                    if (ex.InnerException.GetType() == typeof(APIUnauthorizedException))
                    {
                        GuaranteeLogin();
                    }
                    else if (ex.InnerException.GetType() == typeof(APITooManyRequestsException))
                    {
                        Logger.Log("Retrieve current user playlists failed: Too many requests. " + ex.InnerException.Message);
                        i = -1;
                    }
                    else if (ex.InnerException.GetType() == typeof(APIException))
                    {
                        Logger.Log("Retrieve current user playlists failed: " + ((APIException)ex.InnerException).Response?.StatusCode + ex.InnerException.Message);
                        i = -1;
                    }
                    else
                    {
                        Logger.Log("Retrieve current user playlists failed: " + ex.InnerException.Message);
                        i = -1;
                    }
                }
            } while (i == 0);
            return null;
        }

        /*
         * Gets the tracks from the user's saved tracks library
         */
        private IList<string> SpotifyGetSavedTracks()
        {
            IList<string> uris = new List<string>();
            try
            {
                var task = spotify.Library.GetTracks();
                task.Wait();
                var pages = task.Result;
                var task2 = spotify.PaginateAll(pages);
                task2.Wait();
                var tracks = task2.Result;
                foreach (var track in tracks)
                {
                    uris.Add(track.Track.Uri);
                }
            }
            catch(AggregateException ex)
            {
                Logger.Log("Failed to retrieve user's saved tracks: " + ex.InnerException.Message);
            }
            return uris;
        }

        /*
         * Wrapper to get Page object from Spotify
         */
        private IList<SimplePlaylist> SpotifyPage(IPaginatable<SimplePlaylist> page)
        {
            int i = 0;
            do
            {
                try
                {
                    var task = spotify.PaginateAll(page);
                    task.Wait();
                    return task.Result;
                }
                catch (AggregateException ex)
                {
                    if (ex.InnerException.GetType() == typeof(APIUnauthorizedException))
                    {
                        GuaranteeLogin();
                    }
                    else if (ex.InnerException.GetType() == typeof(APITooManyRequestsException))
                    {
                        Logger.Log("Retrieve paging failed: Too many requests. " + ex.InnerException.Message);
                        i = -1;
                    }
                    else if (ex.InnerException.GetType() == typeof(APIException))
                    {
                        Logger.Log("Retrieve paging failed: " + ((APIException)ex.InnerException).Response?.StatusCode + ex.InnerException.Message);
                        i = -1;
                    }
                    else
                    {
                        Logger.Log("Retrieve paging failed: " + ex.InnerException.Message);
                        i = -1;
                    }
                }
            } while (i == 0);
            return null;
        }

        /*
         * Returns the currently playing track from Spotify
         */
        private FullTrack SpotifyGetCurrentTrack()
        {
            int i = 0;
            do
            {
                try
                {
                    var task = spotify.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest());
                    task.Wait();
                    if(task.Result == null)
                    {
                        return null;
                    }
                    var track = task.Result.Item as FullTrack;
                    return track;

                }
                catch(AggregateException ex)
                {
                    if (ex.InnerException.GetType() == typeof(APIUnauthorizedException))
                    {
                        GuaranteeLogin();
                    }
                    else if (ex.InnerException.GetType() == typeof(APITooManyRequestsException))
                    {
                        Logger.Log("Get Current Track failed: Too many requests. " + ex.InnerException.Message);
                        i = -1;
                    }
                    else if (ex.InnerException.GetType() == typeof(APIException))
                    {
                        Logger.Log("Get Current Track failed: " + ((APIException)ex.InnerException).Response?.StatusCode + ex.InnerException.Message);
                        i = -1;
                    }
                    else
                    {
                        Logger.Log("Get Current Track failed: " + ex.InnerException.Message);
                        i = -1;
                    }
                }
            } while (i == 0);
            return null;
        }

        /*
         * Unused function. It would display the track name on the display of the first person radio
         */
        private void UpdateInGameRadio()
        {
            var track = SpotifyGetCurrentTrack();
            DisplaySpotifyTrackOnRadio(track.Name, track.Artists[0].Name);
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
           /* else if(isSpotifyRadio)
            {
                updateInGameRadio();

            }*/

        }

        /*
         * Spotify requests need to be made at the start of the game
         */
        private async void InitialSpotifyRequests()
        {
            if (obtainedSpotifyClient)
            {
                try
                {
                    await spotify.Player.SetVolume(new PlayerVolumeRequest(0));
                    var resumeRequest = new PlayerResumePlaybackRequest();
                    if(defaultPlaylistId.Length != 0)
                    {
                        resumeRequest.ContextUri = "spotify:playlist:" + defaultPlaylistId;
                    }
                    await spotify.Player.ResumePlayback(resumeRequest);
                } catch(Exception ex)
                {
                    Logger.Log("Error In Initial Spotify Requests: " + ex.Message);
                }
            }
        }

        /*
         * The browser connects to Spotify's website and Spotify returns an authorization code.
         * This is following the Authorization Code Flow from Spotify's API documentation.
         * https://developer.spotify.com/documentation/general/guides/authorization-guide/#authorization-code-flow
         */
        private void GuaranteeLogin()
        {
            DisplayBrowser();
            if(code == null || code.Length == 0)
            {
                Logger.Log("ERROR: Did not login to Spotify");
                obtainedSpotifyClient = false;
            }
            else
            {
                spotify = new SpotifyClient(code);
                obtainedSpotifyClient = true;
            }
        }

        /*
         * Opens chromium browser
         */
        private void DisplayBrowser()
        {
            
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "spotifyRadio/SpotifyRadio.exe",
                    Arguments = "",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            try
            {
                proc.Start();
                code = proc.StandardOutput.ReadLine();
            }
            catch (Exception e)
            {
                Logger.Log("ERROR: Could not launch SpotifyRadio.exe\n" + "Exception info: " + e.Message);
            }

        }

    }
}
