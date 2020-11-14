using System;
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
            File.AppendAllText("GTASpotify.log", DateTime.Now + " : " + message + Environment.NewLine);
        }
    }
    public class SpotifyRadio : Script
    {
        private static SpotifyClient spotify;
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
        private Scaleform DashboardScaleform;

        private string radioName = "RADIO_47_SPOTIFY_RADIO";

        private string code;
        private static readonly HttpClient client = new HttpClient();
        public SpotifyRadio()
        {
            
            obtainedSpotifyClient = false;
            isEngineOn = false;
            isSpotifyRadio = false;
            isEngineOn = false;
            volume = 100;
            LoginToSpotInit();
           
            if (obtainedSpotifyClient)
            {
                setupMenu();
              //DashboardScaleform = new Scaleform("dashboard");
                KeyDown += OnKeyDown;
                Tick += OnTick;
            }
            else
            {
                Logger.Log("ERROR: Did not login to Spotify");
            }


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

        }

        private void displaySpotifyTrackOnRadio(string artist, string track)
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
                Logger.Log(exception.ToString());
            }

        }

        public string getCurrentStationName()
        {
            var name = Function.Call<string>(GTA.Native.Hash.GET_PLAYER_RADIO_STATION_NAME);
            return name;
        }

        private void setEngine(bool status)
        {
            if(obtainedSpotifyClient)
            {
                isEngineOn = status;
            }
        }

        private async void mute()
        {
            await spotifySetVolume(0);
        } 

        private async void unmute()
        {
            await spotifySetVolume(volume);
        }

        private void setRadioStation(bool status)
        {
            if(obtainedSpotifyClient)
            {
                isSpotifyRadio = status;
                if(status)
                {
                    unmute();
                }
                else
                {
                    mute();
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

        private async Task<FullTrack> spotifyGetCurrentTrack()
        {
            int i = 0;
            do
            {
                try
                {
                    var current = await spotify.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest());
                    var track = current.Item as FullTrack;
                    return track;

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
            return null;
        }

        private void updateInGameRadio()
        {
            var track = spotifyGetCurrentTrack();
            track.Wait();
            var t = track.Result;
            displaySpotifyTrackOnRadio(t.Name, t.Artists[0].Name);
        }

        private bool getEngineStatus()
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
            if(Game.Player.Character.CurrentVehicle != null && Game.Player.Character.CurrentVehicle.IsEngineRunning && !getEngineStatus() && getCurrentStationName().Equals(radioName) && !isSpotifyRadio)
            {
                setEngine(true);
                setRadioStation(true);
            }
            else if(Game.Player.Character.CurrentVehicle == null && getEngineStatus() && !getCurrentStationName().Equals(radioName) && isSpotifyRadio)
            {
                setEngine(false);
                setRadioStation(false);
            }
            else if(Game.Player.Character.CurrentVehicle == null && getEngineStatus())
            {
                setEngine(false);
                setRadioStation(false);
            }
            else if (Game.Player.Character.CurrentVehicle != null && Game.Player.Character.CurrentVehicle.IsEngineRunning && !getEngineStatus())
            {
                setEngine(true);
            }
            else if(Game.Player.Character.CurrentVehicle != null && !Game.Player.Character.CurrentVehicle.IsEngineRunning && getEngineStatus() && isSpotifyRadio)
            {
                setEngine(false);
                setRadioStation(false);
            }
            else if(Game.Player.Character.CurrentVehicle != null && !Game.Player.Character.CurrentVehicle.IsEngineRunning && getEngineStatus())
            {
                setEngine(false);
            }
            else if(getEngineStatus() && getCurrentStationName().Equals(radioName) && !isSpotifyRadio)
            {
                setRadioStation(true);
            }
            else if(getEngineStatus() && !getCurrentStationName().Equals(radioName) && isSpotifyRadio)
            {
                setRadioStation(false);
            }
           /* else if(isSpotifyRadio)
            {
                updateInGameRadio();

            }*/

        }

        private void LoginToSpotInit()
        {
            GuaranteeLogin();
            initialSpotifyRequests();
        }

        private async void initialSpotifyRequests()
        {
            if (obtainedSpotifyClient)
            {
                await spotifySetVolume(0);
                await spotifyResumePlayback();
            }
        }

        private void GuaranteeLogin()
        {
            DisplayBrowser();
            if(code == null)
            {
                Logger.Log("ERROR: Did not login to Spotify");
                obtainedSpotifyClient = false;
            }
            else if(code.Length != 0)
            {
                spotify = new SpotifyClient(code);
                obtainedSpotifyClient = true;
            }
        }

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
            proc.Start();
            code = proc.StandardOutput.ReadLine();

        }

    }
}
