using System;
using GTA;

namespace GTASpot
{
    internal class MoodMatch
    {
        public MoodMatch() { }

        public void GetMood(SpotifyAPI.Web.TrackAudioFeatures audioFeatures)
        {
            GTA.UI.Screen.StopEffects();
            SetWeather(audioFeatures.Valence);
            SetShake(audioFeatures.Loudness, audioFeatures.Energy, audioFeatures.Tempo, audioFeatures.Instrumentalness);
            SetVisualEffect(audioFeatures.Instrumentalness, audioFeatures.Acousticness);
            SetWantedLevel(audioFeatures.Energy, audioFeatures.Tempo);

        }

        //TODO Add explosions
        private void SetWantedLevel(float energy, float tempo)
        {
            if(energy > .95)
            {
                GTA.Game.Player.WantedLevel = 5;
            }
        }

        private void SetVisualEffect(float instrumentalness, float acousticness)
        {
            if(instrumentalness > 0.5 && acousticness < .5)
            {
                GTA.UI.Screen.StartEffect(GTA.UI.ScreenEffect.DmtFlight, 0, true);
                
            }
            else
            {
                GTA.UI.Screen.StopEffects();
            }
        }
        private void SetShake(float loudness, float energy, float tempo, float instrumentalness)
        {
            float amplitude = 1f;
            if(instrumentalness > .001 && energy < .9)
            {
                amplitude = ((tempo / 200) * 1.5f + .5f) * amplitude;
                GTA.GameplayCamera.Shake(CameraShake.FamilyDrugTrip, amplitude);
            }
            else if(energy < .4)
            {
                GTA.GameplayCamera.StopShaking();
            }
            else if (energy < .6 && loudness > -5)
            {
                amplitude = (energy * 3 - .2f) * amplitude;
                GTA.GameplayCamera.Shake(CameraShake.SkyDiving, amplitude);
            }
            else if (energy < 1 && loudness > -5)
            {
                amplitude = energy * 3 * amplitude;
                GTA.GameplayCamera.Shake(CameraShake.SkyDiving, amplitude);
            }
            else if(energy < .6)
            {
                amplitude = (energy * 3 - .2f) * amplitude;
                GTA.GameplayCamera.Shake(CameraShake.RoadVibration, amplitude);
            }
            else if(energy < 1)
            {
                amplitude = energy * 3 * amplitude;
                GTA.GameplayCamera.Shake(CameraShake.RoadVibration, amplitude);
            }
        }

        private void SetWeather(float valence) 
        {
            float duration = 100.0f;
            // 10% chance of getting something purely random
            Random rand = new Random();
            if(rand.NextDouble() < .1)
            {
                double r = rand.NextDouble();
                RandomWeather(r, duration);
                
            }
            else if(valence < .2)
            {
                GTA.World.TransitionToWeather(GTA.Weather.ThunderStorm, duration);
            }
            else if(valence < .4)
            {
                GTA.World.TransitionToWeather(GTA.Weather.Raining, duration);
            }
            else if(valence < .6)
            {
                GTA.World.TransitionToWeather(GTA.Weather.Neutral, duration);
            }
            else if(valence < .8)
            {
                GTA.World.TransitionToWeather(GTA.Weather.Clear, duration);
            }
            else if(valence <= 1)
            {
                GTA.World.TransitionToWeather(GTA.Weather.ExtraSunny, duration);
            }
        }

        private void RandomWeather(double r, float duration)
        {
            if (r < .25)
            {
                GTA.World.TransitionToWeather(GTA.Weather.Clear, duration);
            }
            else if (r < .5)
            {
                GTA.World.TransitionToWeather(GTA.Weather.Neutral, duration);
            }
            else if (r < .75)
            {
                GTA.World.TransitionToWeather(GTA.Weather.Clouds, duration);
            }
            else if (r < .8)
            {
                GTA.World.TransitionToWeather(GTA.Weather.Raining, duration);
            }
            else if (r < .85)
            {
                GTA.World.TransitionToWeather(GTA.Weather.ExtraSunny, duration);
            }
            else if (r < .9)
            {
                GTA.World.TransitionToWeather(GTA.Weather.Foggy, duration);
            }
            else if (r < .95)
            {
                GTA.World.TransitionToWeather(GTA.Weather.Smog, duration);
            }
            else if (r < .97)
            {
                GTA.World.TransitionToWeather(GTA.Weather.ThunderStorm, duration);
            }
            else if (r < .99)
            {
                GTA.World.TransitionToWeather(GTA.Weather.Blizzard, duration);

            }
            else if (r < 1)
            {
                GTA.World.TransitionToWeather(GTA.Weather.Halloween, duration);
            }
        }
    }
}