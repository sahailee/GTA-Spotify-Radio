using System;
using System.Collections.Generic;
using System.Diagnostics;
using SpotifyAPI.Web;
namespace GTASpot
{
    internal class SpotifyController
    {
        private SpotifyClient spotify;
        public bool obtainedSpotifyClient;
        public SpotifyController()
        {
            obtainedSpotifyClient = true;
        }

        /*
         * The browser connects to Spotify's website and Spotify returns an authorization code.
         * This is following the Authorization Code Flow from Spotify's API documentation.
         * https://developer.spotify.com/documentation/general/guides/authorization-guide/#authorization-code-flow
         */
        public void GuaranteeLogin()
        {
            string code = DisplayBrowser();
            if (code == null || code.Length == 0)
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
        private string DisplayBrowser()
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
                string code = proc.StandardOutput.ReadLine();
                return code;
            }
            catch (Exception e)
            {
                Logger.Log("ERROR: Could not launch SpotifyRadio.exe\n" + "Exception info: " + e.Message);
            }
            return null;

        }
        /*
         * Spotify requests need to be made at the start of the game
         */
        public async void InitialSpotifyRequests(string defaultPlaylistId)
        {
            if (obtainedSpotifyClient)
            {
                try
                {
                    await spotify.Player.SetVolume(new PlayerVolumeRequest(0));
                    var resumeRequest = new PlayerResumePlaybackRequest();
                    if (defaultPlaylistId.Length != 0)
                    {
                        resumeRequest.ContextUri = "spotify:playlist:" + defaultPlaylistId;
                    }
                    await spotify.Player.ResumePlayback(resumeRequest);
                }
                catch (Exception ex)
                {
                    Logger.Log("Error In Initial Spotify Requests: " + ex.Message);
                }
            }
        }

        public DeviceResponse GetDevices()
        {
            try
            {
                var task = spotify.Player.GetAvailableDevices();
                task.Wait();
                return task.Result;
            }
            catch (AggregateException ex)
            {
                Logger.Log("Fetching Available Devices Failed: " + ex.InnerException.Message);
            }
            return null;
        }
        /*
         * Wrapper for Spotify Skip Next
         */
        public bool SkipNext()
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
        public bool SkipPrevious()
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
        public bool PausePlayback()
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
        public bool ResumePlayback()
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
        * Wrapper for Spotify Resume with specific requests for a playlist. Empty String will play from saved tracks
        */
        public async void ResumePlayback(string uri)
        {
            var request = new PlayerResumePlaybackRequest();
            if (uri.Length == 0)
            {
                request.Uris = GetSavedTracks();
            }
            else
            {
                request.ContextUri = uri;
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
        }


        /*
        * Wrapper for Spotify Resume Playback and to a specific device
        */
        public void ResumePlayback(string deviceID, string defaultPlaylistId)
        {
            var request = new PlayerResumePlaybackRequest();
            request.DeviceId = deviceID;
            var curr = GetCurrentlyPlaying();
            if (curr == null)
            {
                if (defaultPlaylistId.Length != 0)
                {
                    request.ContextUri = "spotify:playlist:" + defaultPlaylistId;
                }
                else
                {
                    request.Uris = GetSavedTracks();
                }

            }
            else
            {
                if (curr.Context != null)
                {
                    request.ContextUri = curr.Context.Uri;
                    if (curr.Item != null)
                    {
                        request.OffsetParam = new PlayerResumePlaybackRequest.Offset();
                        if (curr.Item.Type == ItemType.Track)
                        {
                            request.OffsetParam.Uri = ((FullTrack)curr.Item).Uri;
                        }
                        else
                        {
                            request.OffsetParam.Uri = ((FullEpisode)curr.Item).Uri;
                        }

                        request.PositionMs = curr.ProgressMs;
                    }

                }
                else if (defaultPlaylistId.Length != 0)
                {
                    request.ContextUri = "spotify:playlist:" + defaultPlaylistId;
                }
                else
                {
                    request.Uris = GetSavedTracks();
                    if (curr.Item != null)
                    {
                        request.OffsetParam = new PlayerResumePlaybackRequest.Offset();
                        if (curr.Item.Type == ItemType.Track)
                        {
                            request.Uris[0] = ((FullTrack)curr.Item).Uri;
                        }
                        else
                        {
                            request.Uris[0] = ((FullEpisode)curr.Item).Uri;
                        }
                        request.OffsetParam = new PlayerResumePlaybackRequest.Offset();
                        request.OffsetParam.Uri = request.Uris[0];
                        request.PositionMs = curr.ProgressMs;
                    }

                }

            }
            var task = spotify.Player.ResumePlayback(request);
            task.Wait();
        }

        /*
         * Wrapper for getting currently playing Spotify
         */
        public CurrentlyPlaying GetCurrentlyPlaying()
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
         * Wrapper for getting currently spotify playback
         */
        public CurrentlyPlayingContext GetCurrentPlayback()
        {
            int i = 0;
            do
            {
                try
                {
                    var task = spotify.Player.GetCurrentPlayback();
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
        public bool SetVolume(int volumeRequest)
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
        public bool SetShuffle(bool shuffleRequest)
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
        public Paging<SimplePlaylist> GetCurrentPlaylist()
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
        public IList<string> GetSavedTracks()
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
            catch (AggregateException ex)
            {
                Logger.Log("Failed to retrieve user's saved tracks: " + ex.InnerException.Message);
            }
            return uris;
        }

        /*
         * Wrapper to get Page object from Spotify
         */
        public IList<SimplePlaylist> Page(IPaginatable<SimplePlaylist> page)
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
        public IPlayableItem GetCurrentTrack()
        {
            int i = 0;
            do
            {
                try
                {
                    var task = spotify.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest());
                    task.Wait();
                    if (task.Result == null)
                    {
                        return null;
                    }
                    var track = task.Result.Item;
                    return track;

                }
                catch (AggregateException ex)
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
         * Gets audio features of the track
         */
        public TrackAudioFeatures GetAudioFeatures(string trackid)
        {
            try
            {
                var task = spotify.Tracks.GetAudioFeatures(trackid);
                task.Wait();
                return task.Result;
            }
            catch (AggregateException ex)
            {
                Logger.Log("Failed to retrieve audio analysis: " + ex.InnerException.Message);
            }
            return null;
        }

        /*
         * Gets audio analysis of the track
         */
        public TrackAudioAnalysis GetAudioAnalysis(string trackid)
        {
            try
            {
                var task = spotify.Tracks.GetAudioAnalysis(trackid);
                task.Wait();
                return task.Result;
            }
            catch (AggregateException ex)
            {
                Logger.Log("Failed to retrieve audio analysis: " + ex.InnerException.Message);
            }
            return null;
        }
    }
}