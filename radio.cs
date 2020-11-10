﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using GTA;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using NativeUI;
using System.Net.Http;
using System.Diagnostics;
using System.Windows.Automation;


namespace GTASpot
{
    /// <summary>
    /// Static logger class that allows direct logging of anything to a text file
    /// </summary>
    public static class Logger
    {
        public static void Log(object message)
        {
            File.AppendAllText("GTASpotify.log", DateTime.Now + " : " + message + Environment.NewLine);
        }
    }

    public class SpotifyRadio : Script
    {
        //TODO
        //Mute spotify if engine is off
        //Mute spotify if game is paused
        //Mute if shot while spotify is on
        //WHen u have it playing leave ur car and then steal someone elses u can hear spotify for a few seconds
        //Use other browsers if chrome is not present
        //Alt can try to make cefsharp work
        //Find out why random gta 5 ads play
        //If spotify is paused the game does not play it
        private static SpotifyClient spotify;
        private bool isInVehicle;
        private bool isEngineOn;
        private bool isSpotifyRadio;
        private bool obtainedSpotifyClient;
        private int volume;

        private UIMenu mainMenu;
        private MenuPool modMenuPool;
        private UIMenuItem playPausePlayback;
        private UIMenuItem skipTrack;
        private UIMenuItem prevTrack;
        private UIMenuItem volumeLevel;

        private UIMenu shuffleSubMenu;
        private UIMenu playlistSubMenu;

        private string code;
        private static readonly HttpClient client = new HttpClient();
        public SpotifyRadio()
        {
            obtainedSpotifyClient = false;
            Logger.Log("Initiating");
            isInVehicle = false;
            isSpotifyRadio = false;
            isEngineOn = false;
            volume = 100;
            LoginToSpotInit();
            setupMenu();
            KeyDown += OnKeyDown;
            Tick += OnTick;
            Logger.Log("Done Iniating");
        }

        private void setupMenu()
        {
            modMenuPool = new MenuPool();
            mainMenu = new UIMenu("Spotify Radio", "Control your music");
            modMenuPool.Add(mainMenu);

            playPausePlayback = new UIMenuItem("Play/Pause Track");
            skipTrack = new UIMenuItem("Skip Track");
            prevTrack = new UIMenuItem("Previous Track");
            volumeLevel = new UIMenuItem("Set Volume Level");

            mainMenu.AddItem(playPausePlayback);
            mainMenu.AddItem(skipTrack);
            mainMenu.AddItem(prevTrack);
            mainMenu.AddItem(volumeLevel);


            mainMenu.OnItemSelect += onMainMenuItemSelect;
            setupShuffleMenu();
            setupPlaylistMenu();

        }

        private void setupShuffleMenu()
        {
            shuffleSubMenu = modMenuPool.AddSubMenu(mainMenu, "Set Shuffle Mode");

            List<dynamic> shuffleStates = new List<dynamic>();
            shuffleStates.Add("On");
            shuffleStates.Add("Off");
            UIMenuListItem shuffleMode = new UIMenuListItem("Toggle Shuffle: ", shuffleStates, 0);
            shuffleSubMenu.AddItem(shuffleMode);

            UIMenuItem setShuffle = new UIMenuItem("Confirm Changes");
            shuffleSubMenu.AddItem(setShuffle);

            shuffleSubMenu.OnItemSelect += async (sender, item, index) =>
            {
                if (item == setShuffle)
                {
                    int listIndex = shuffleMode.Index;
                    if (listIndex == 0)
                    {
                        await spotifySetShuffle(true);
                    }
                    else
                    {
                        await spotifySetShuffle(false);
                    }
                    
                    modMenuPool.CloseAllMenus();
                }
            };
        }

        private async void setupPlaylistMenu()
        {
            playlistSubMenu = modMenuPool.AddSubMenu(mainMenu, "Set Radio Playlist");

            List<dynamic> playListNames = new List<dynamic>();
            List<string> playListIDs = new List<string>();

            var page = await spotifyGetCurrentPlaylist();
            var allPages = await spotifyPage(page);


            foreach (var item in allPages)
            {
                playListNames.Add(item.Name);
                playListIDs.Add(item.Id);
            }
            UIMenuListItem list = new UIMenuListItem("Pick Playlist: ", playListNames, 0);
            playlistSubMenu.AddItem(list);

            UIMenuItem setPlaylist = new UIMenuItem("Confirm Selection");
            playlistSubMenu.AddItem(setPlaylist);


            playlistSubMenu.OnItemSelect += async (sender, item, index) =>
            {
                if (item == setPlaylist)
                {
                    int listIndex = list.Index;
                    var desiredPlaylist = await spotify.Playlists.Get(playListIDs[listIndex]);
                    var values = new Dictionary<string, string>
                    {
                        {"context_uri",  "spotify:playlist:" + desiredPlaylist.Id}
                    };
                    var content = new FormUrlEncodedContent(values);
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", code);

                    StringContent body = new StringContent("{\"context_uri\":\"spotify:playlist:" + desiredPlaylist.Id + "\"}", Encoding.UTF8, "application/json");
                    body.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                    var response = await client.PutAsync("https://api.spotify.com/v1/me/player/play", body);
                    var responseString = await response.Content.ReadAsStringAsync();
                    if(!response.IsSuccessStatusCode)
                    {
                        Logger.Log("Unsuccessful PUT: " + response.ReasonPhrase);
                        Logger.Log(responseString);
                    }
                    modMenuPool.CloseAllMenus();
                }
            };
        }

        private async void onMainMenuItemSelect(UIMenu sender, UIMenuItem item, int index)
        {
            if(item == playPausePlayback)
            {
                var current = await spotifyGetCurrentlyPlaying();
                if (current.IsPlaying)
                {
                    await spotifyPausePlayback();
                }
                else {
                    await spotifyResumePlayback();
                }
            }
            else if(item == skipTrack)
            {
                await spotifySkipNext();
            }
            else if(item == prevTrack)
            {
                await spotifySkipPrevious();
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
                        await spotifySetVolume(volume);
                    }
                }
                catch (FormatException x)
                {
                    GTA.UI.Notification.Show("Volume must be number between 0 and 100.");
                    Logger.Log("Invalid input: " + x.Message);
                }
            }
        }
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (spotify == null)
            {
                return;
            }
            if(e.KeyCode == Keys.F10 && !modMenuPool.IsAnyMenuOpen())
            {
                mainMenu.Visible = !mainMenu.Visible;
            }
            else if(e.KeyCode == Keys.F11)
            {
                GTA.UI.Notification.Show(Game.Player.Character.CurrentVehicle.IsEngineRunning.ToString());
            }
                
        }

        private void setInVehicle(bool status)
        {
            if(obtainedSpotifyClient)
            {
                isInVehicle = status;
            }
        }

        private async void setRadioStation(bool status)
        {
            if(obtainedSpotifyClient)
            {
                isSpotifyRadio = status;
                if(status)
                {
                    await spotifySetVolume(volume);
                }
                else
                {
                    await spotifySetVolume(0);
                }                
            }
        }

        private async Task<bool> spotifySkipNext()
        {
            int i = 0;
            do
            {
                try
                {
                    var curr = await spotify.Player.SkipNext();
                    i = 1;
                    return curr;
                }
                catch (APIUnauthorizedException)
                {
                    GuaranteeLogin();
                }
                catch (APIException ex)
                {
                    Logger.Log("Skip next failed: " + ex.Response?.StatusCode + ex.Message);
                    i = -1;
                }
            } while (i == 0);
            return false;
        }

        private async Task<bool> spotifySkipPrevious()
        {
            int i = 0;
            do
            {
                try
                {
                    var curr = await spotify.Player.SkipPrevious();
                    i = 1;
                    return curr;
                }
                catch (APIUnauthorizedException)
                {
                    GuaranteeLogin();
                }
                catch (APIException ex)
                {
                    Logger.Log("Skip prev failed: " + ex.Response?.StatusCode + ex.Message);
                    i = -1;
                }
            } while (i == 0);
            return false;
        }


        private async Task<bool> spotifyPausePlayback()
        {
            int i = 0;
            do
            {
                try
                {
                    var curr = await spotify.Player.PausePlayback();
                    i = 1;
                    return curr;
                }
                catch (APIUnauthorizedException)
                {
                    GuaranteeLogin();
                }
                catch (APIException ex)
                {
                    Logger.Log("Pause failed: " + ex.Response?.StatusCode + ex.Message);
                    i = -1;
                }
            } while (i == 0);
            return false;
        }


        private async Task<bool> spotifyResumePlayback()
        {
            int i = 0;
            do
            {
                try
                {
                    var curr = await spotify.Player.ResumePlayback();
                    i = 1;
                    return curr;
                }
                catch (APIUnauthorizedException)
                {
                    GuaranteeLogin();
                }
                catch (APIException ex)
                {
                    Logger.Log("Resume failed: " + ex.Response?.StatusCode + ex.Message);
                    i = -1;
                }
            } while (i == 0);
            return false;
        }

        private async Task<CurrentlyPlaying> spotifyGetCurrentlyPlaying()
        {
            int i = 0;
            do
            {
                try
                {
                    var curr = await spotify.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest());
                    i = 1;
                    return curr;
                }
                catch (APIUnauthorizedException)
                {
                    GuaranteeLogin();
                }
                catch (APIException ex)
                {
                    Logger.Log("Get current playing failed: " + ex.Response?.StatusCode + ex.Message);
                    i = -1;
                }
            } while (i == 0);
            return null;
        }

        private async Task<bool> spotifySetVolume(int volumeRequest)
        {
            int i = 0;
            do
            {
                try
                {
                    await spotify.Player.SetVolume(new PlayerVolumeRequest(volumeRequest));
                    i = 1;
                }
                catch (APIUnauthorizedException)
                {
                    GuaranteeLogin();
                }
                catch (APIException ex)
                {
                    Logger.Log("Volume change failed: " + ex.Response?.StatusCode + ex.Message);
                    i = -1;
                }
            } while (i == 0);
            if (i < 0) return false;
            return true;
        }

        private async Task<bool> spotifySetShuffle(bool shuffleRequest)
        {
            int i = 0;
            do
            {
                try
                {
                    await spotify.Player.SetShuffle(new PlayerShuffleRequest(shuffleRequest));
                    i = 1;
                }
                catch (APIUnauthorizedException)
                {
                    GuaranteeLogin();
                }
                catch (APIException ex)
                {
                    Logger.Log("Shuffle change failed: " + ex.Response?.StatusCode + ex.Message);
                    i = -1;
                }
            } while (i == 0);
            if (i < 0) return false;
            return true;
        }

        private async Task<Paging<SimplePlaylist>> spotifyGetCurrentPlaylist()
        {
            int i = 0;
            do
            {
                try
                {
                    var playlists = await spotify.Playlists.CurrentUsers();
                    i = 1;
                    return playlists;
                }
                catch (APIUnauthorizedException)
                {
                    GuaranteeLogin();
                }
                catch (APIException ex)
                {
                    Logger.Log("Retrieve current user playlists failed: " + ex.Response?.StatusCode + ex.Message);
                    i = -1;
                }
            } while (i == 0);
            return null;
        }

        private async Task<IList<SimplePlaylist>> spotifyPage(IPaginatable<SimplePlaylist> page)
        {
            int i = 0;
            do
            {
                try
                {
                    var pages = await spotify.PaginateAll(page);
                    i = 1;
                    return pages;
                }
                catch (APIUnauthorizedException)
                {
                    GuaranteeLogin();
                }
                catch (APIException ex)
                {
                    Logger.Log("Retrieve paging failed: " + ex.Response?.StatusCode + ex.Message);
                    i = -1;
                }
            } while (i == 0);
            return null;
        }

        private bool getInVehicle()
        {
            return isInVehicle;
        }

        private void OnTick(object sender, EventArgs e)
        {
            //Handle mod menu
            if(modMenuPool != null)
            {
                modMenuPool.ProcessMenus();
            }
            //Handle Spotify Radio
            if(Game.Player.Character.CurrentVehicle != null && !getInVehicle() && Game.RadioStation.ToString().Equals("-1") && !isSpotifyRadio)
            {
                setInVehicle(true);
                setRadioStation(true);
                Logger.Log("1:" + Game.Player.Character.CurrentVehicle.IsEngineRunning);
            }
            else if(Game.Player.Character.CurrentVehicle == null && getInVehicle() && !Game.RadioStation.ToString().Equals("-1") && isSpotifyRadio)
            {
                setInVehicle(false);
                setRadioStation(false);
                Logger.Log("2:" + Game.Player.Character.CurrentVehicle.IsEngineRunning);
            }
            else if(Game.Player.Character.CurrentVehicle == null && getInVehicle())
            {
                setInVehicle(false);
                setRadioStation(false);
                Logger.Log("3:" + Game.Player.Character.CurrentVehicle.IsEngineRunning);
            }
            else if (Game.Player.Character.CurrentVehicle != null && !getInVehicle())
            {
                setInVehicle(true);
                Logger.Log("4:" + Game.Player.Character.CurrentVehicle.IsEngineRunning);
            }
            else if(getInVehicle() && Game.RadioStation.ToString().Equals("-1") && !isSpotifyRadio)
            {
                setRadioStation(true);
                Logger.Log("5:" + Game.Player.Character.CurrentVehicle.IsEngineRunning);
            }
            else if(getInVehicle() && !Game.RadioStation.ToString().Equals("-1") && isSpotifyRadio)
            {
                setRadioStation(false);
                Logger.Log("6:" + Game.Player.Character.CurrentVehicle.IsEngineRunning);
            }
            //Game.Player.Character.CurrentVehicle.IsEngineRunning

        }

        private void LoginToSpotInit()
        {
            GuaranteeLogin();
            //Logger.Log("Signed in successfully");
            obtainedSpotifyClient = true;
            initialSpotifyRequests();
        }

        private async void initialSpotifyRequests()
        {
            await spotifySetVolume(0);
            await spotifyResumePlayback();
        }

        private void GuaranteeLogin()
        {
            BrowserUtil.Open(new Uri("https://gta-spotify-radio.web.app/login"));
            string url;
            do
            {
                url = Checkurl("gta-spotify-radio.web.app/index.html?access_token");
            } while (url.Length == 0);
            string tokenLabel = "access_token=";
            code = url.Substring(url.IndexOf(tokenLabel) + tokenLabel.Length);
            spotify = new SpotifyClient(code);
            
        }
        private string Checkurl(string loginUrl)
        {
            foreach (Process process in Process.GetProcessesByName("chrome"))
            {
                string url = GetChromeUrl(process);
                if (url == null)
                    continue;
                if(url.Contains(loginUrl))
                {
                    return url;
                }

            }
            return "";
        }

        private static string GetChromeUrl(Process process)
        {
            if (process == null)
                throw new ArgumentNullException("process");

            if (process.MainWindowHandle == IntPtr.Zero)
                return null;

            AutomationElement element = AutomationElement.FromHandle(process.MainWindowHandle);
            if (element == null)
                return null;

            AutomationElementCollection elm1 = element.FindAll(TreeScope.Subtree, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
            AutomationElement elm = elm1[0];
            string vp = ((ValuePattern)elm.GetCurrentPattern(ValuePattern.Pattern)).Current.Value as string;
            return vp;
        }

    }
}