﻿using ADLCore.Alert;
using ADLCore.Ext;
using ADLCore.Novels.Models;
using ADLCore.Video.Constructs;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ADLCore.Constructs;

namespace ADLCore.Video.Extractors
{
    public class HAnime : ExtractorBase
    {
        /// <summary>
        /// HAnime Download Class
        /// </summary>
        /// <param name="term">Search term or download link</param>
        /// <param name="mt">Multithreading parameter (ineffective on HAnime)</param>
        /// <param name="path">Download Path, not functional</param>
        /// <param name="continuos">Download multiple videos in a row</param>
        /// <param name="ti">"taskindex" to be used with status update</param>
        /// <param name="statusUpdate">The function will call this when ever a notable update occurs</param>
        public HAnime(argumentList args, int ti = -1, Action<int, string> statusUpdate = null) : base(args, ti,
            statusUpdate, Site.HAnime)
        {
            ao = args;
            if (ao.vRange)
                throw new ArgumentException("HAnime does not support vRange.");
        }

        public override void Begin()
        {
            downloadTo = ao.export;

            if (ao.term.IsValidUri())
                Download(ao.term, ao.mt, ao.cc);
            else
            {
                string a = Search();
                if (a == null)
                    return;
                Download(a, ao.mt, ao.cc);
            }
        }

        private bool ExtractDataFromVideo()
        {
            return false;
        }

        public override bool Download(string path, bool mt, bool continuos)
        {
            GetDownloadUri(videoInfo == null ? new VideoData() {url = path} : videoInfo);

            if (!ao.l)
                downloadTo =
                    $"{Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}HAnime{Path.DirectorySeparatorChar}{videoInfo.name.TrimIntegrals()}{Path.DirectorySeparatorChar}{videoInfo.name}.mp4";
            else if (ao.android)
                downloadTo = Path.Combine(ao.export, "HAnime", videoInfo.name.TrimIntegrals(), ".mp4");
            else
                downloadTo = Path.Combine(ao.export, videoInfo.series, ".mp4");

            Directory.CreateDirectory($"{Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}HAnime{Path.DirectorySeparatorChar}{videoInfo.name.TrimIntegrals()}");
            
            HLSManager manager = new HLSManager(downloadTo, ao.stream);

            //Note: Apparently, WebClient removes the base64 encoding automaticaly.
            manager.encKey = Encoding.UTF8.GetBytes(webClient.DownloadString("https://hanime.tv/sign.bin"));

            //manager.UseAltExport = true; //FFMPEGCore doesn't work well with input streams from this site.
            manager.LoadStream(videoInfo.manifestString);
            //manager.SelectResolution(manager.GetResolutions()[manager.GetResolutions().Count - 1]);
            
            int l = manager.Size;
            double prg;

            ADLUpdates.CallLogUpdate(
                $"Please support HAnime; allow ads on their website while you look for content to download!");
            ADLUpdates.CallLogUpdate($"Beginning download of {videoInfo.name}");

            if(ao.stream)
                startStreamServer();

            while (manager.ProcessStream())
            {
                updateStatus?.Invoke(taskIndex,
                    $"{videoInfo.name} {Strings.calculateProgress('#', manager.Location, l)}");
                continue;
            }

            //manager.FinalizeExport();

            if (continuos && videoInfo.nextVideo.name.RemoveSpecialCharacters().TrimIntegrals() ==
                videoInfo.name.TrimIntegrals())
            {
                HAnime h = new HAnime(
                    new argumentList
                    {
                        term = $"https://hanime.tv/videos/hentai/{videoInfo.url}", mt = mt,
                        export = downloadTo, cc = continuos
                    }, taskIndex, updateStatus);
                h.Begin();
            }

            return true;
        }

        public override void GenerateHeaders()
        {
            throw new NotImplementedException();
        }

        public override string GetDownloadUri(string path)
        {
            return null;
        }

        /*private string SearchPrompt(SearchReq sj, ref int np)
        {
            for (int idx = 0; idx < sj.actualHits.Count; idx++)
                ADLUpdates.CallLogUpdate(
                    $"{idx} -- {sj.actualHits[idx].name} | Ratings: {sj.actualHits[idx].GetRating()}/10\n       tags:{sj.actualHits[idx].tagsAsString()}\n       desc:{new string(sj.actualHits[idx].description.Replace("<p>", string.Empty).Replace("</p>", string.Empty).Replace("\n", string.Empty).Take(60).ToArray())}\n\n");

            ADLUpdates.CallLogUpdate($"\nCommands: \n     page {{page}}/{sj.nbPages}\n     select {{episode num}}");
            c:
            String[] input = Console.ReadLine().ToLower().Split(' ');

            switch (input[0])
            {
                case "select":
                    videoInfo = new Constructs.Video()
                    {
                        hentai_video = new HentaiVideo()
                            {slug = $"https://hanime.tv/videos/hentai/{sj.actualHits[int.Parse(input[1])].slug}"}
                    };
                    ADLUpdates.CallThreadChange(false);
                    return $"https://hanime.tv/videos/hentai/{sj.actualHits[int.Parse(input[1])].slug}";
                case "page":
                    Console.Clear();
                    np = int.Parse(input[1]);
                    return "CNT";
                default:
                    goto c;
            }
            return null;
        }*/

        public override dynamic Search(bool promptUser = false, bool d = false)
        {
            int np = 0;
            string a;
            a:
            try
            {
                HttpWebRequest httpWebRequest = (HttpWebRequest) WebRequest.Create("https://search.htv-services.com/");
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Method = "POST";

                string json =
                    $"{{\"search_text\":\"{ao.term}\",\"tags\":[],\"tags_mode\":\"AND\",\"brands\":[],\"blacklist\":[],\"order_by\":\"released_at_unix\",\"ordering\":\"asc\",\"page\":{np.ToString()}}}";

                using (StreamWriter sw = new StreamWriter(httpWebRequest.GetRequestStream()))
                    sw.Write(json);

                HttpWebResponse response = (HttpWebResponse) httpWebRequest.GetResponse();

                using (StreamReader sr = new StreamReader(response.GetResponseStream()))
                    a = sr.ReadToEnd();

                /*SearchReq sj = JsonSerializer.Deserialize<SearchReq>(a);

                if (sj.actualHits.Count <= 0)
                {
                    ADLUpdates.CallLogUpdate($"No videos matching search query.");
                    return null;
                }

                ADLUpdates.CallLogUpdate($"Hits: {sj.actualHits.Count} {np}/{sj.nbPages} page");

                if (promptUser)
                {
                    ADLUpdates.CallThreadChange(true);
                    while (true)
                    {
                        string searchResponse = SearchPrompt(sj, ref np);
                        if (searchResponse == "CNT")
                            goto a;
                        return searchResponse;
                    }
                }
                else
                    return
                        $"https://hanime.tv/videos/hentai/{sj.actualHits[0].slug}"; // Else return first video returned.*/
            }
            catch
            {
                goto a;
            }
                return null;
        }

        public override string GetDownloadUri(VideoData vid)
        {
            ADLUpdates.CallLogUpdate($"Extracting Download URL for {vid.url}");
            string Data = webClient.DownloadString(vid.url);

            Regex reg = new Regex("(?<=<script>window\\.__NUXT__=)(.*)(?=;</script>)");
            Match mc = reg.Match(Data); // Grab json
            string a = mc.Value;

            JsonDocument jDoc = JsonDocument.Parse(a);

            //respecting HAnime paid content for 1080p; I do not believe in circumventing it, since they are one of the few respectable sites in this area. If you want 1080P, go pay HAnime, which licences the content.
            //I am mainly keeping this downloader here just for those who wish to bypass the captcha verifications on downloading video streams from HAnime, not circumventing paid features.
            //In the future, I may make it so that you can download 1080p content, while logged in to your own premium enabled account.
            JsonElement videoElement = jDoc.RootElement.GetProperty("state").GetProperty("data").GetProperty("video")
                .GetProperty("hentai_video");

            VideoData vidData = new VideoData();
            vidData.name = videoElement.GetProperty("name").GetString();
            vidData.series_id = videoElement.GetProperty("id").GetInt32().ToString();
            vidData.url = "https://hanime.tv/videos/hentai/" + videoElement.GetProperty("slug").GetString();
            vidData.manifestString = 
                jDoc.RootElement.GetProperty("state").GetProperty("data").GetProperty("video")
                    .GetProperty("videos_manifest").GetProperty("servers")[0].GetProperty("streams")[1]
                    .GetProperty("url").GetString();
            
            vid = vidData;
            videoInfo = vidData;
            return null;
        }

        public override dynamic GetMetaData()
        {
            throw new NotImplementedException();
        }

        public override void GrabHome(int amount)
        {
            throw new NotImplementedException();
        }
    }
}