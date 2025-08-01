﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Flow.Launcher.Plugin;
using YoutubeDLSharp;
using YoutubeDLSharp.Metadata;
using YoutubeDLSharp.Options;

namespace Flow.Launcher.Plugin.Rip
{
    /// <summary>
    /// Represents the main plugin class for the Flow Launcher Rip plugin.
    /// </summary>
    public class Rip : IAsyncPlugin
    {
        private PluginInitContext _context;
        private YoutubeDL _ytdl;
        
        private bool _hasUserInput = false; // Flag to track if user input has been provided

        /// <summary>
        /// Initializes the plugin asynchronously.
        /// </summary>
        /// <param name="context">The plugin initialization context.</param>
        public async Task InitAsync(PluginInitContext context)
        {
            _context = context;
            _ytdl = new YoutubeDL();

            // Set the output folder to the user's Downloads directory
            string downloadsFolder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads"
            );
            _ytdl.OutputFolder = downloadsFolder;

            // Set yt-dlp path (bundled with plugin)
            string ytDlpPath = System.IO.Path.Combine(_context.CurrentPluginMetadata.PluginDirectory, "yt-dlp.exe");
            if (!System.IO.File.Exists(ytDlpPath))
            {
                await YoutubeDLSharp.Utils.DownloadYtDlp(_context.CurrentPluginMetadata.PluginDirectory);
            }
            _ytdl.YoutubeDLPath = ytDlpPath;

            // Attempts to use the FFmpeg that is already bundled with Flow Launcher
            // If not found, it will download FFmpeg to the plugin's directory
            string ffmpegPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
            if (!System.IO.File.Exists(ffmpegPath))
            {
                await YoutubeDLSharp.Utils.DownloadFFmpeg(_context.CurrentPluginMetadata.PluginDirectory);
            }
            _ytdl.FFmpegPath = ffmpegPath;
        }

        /// <summary>
        /// Handles user queries asynchronously and returns a list of results.
        /// </summary>
        /// <param name="query">The query object containing user input.</param>
        /// <param name="token">A cancellation token to cancel the operation.</param>
        /// <returns>A list of results based on the query.</returns>
        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            // Show initResult only if no user input has been provided yet
            if (!_hasUserInput)
            {
                var initResult = new Result
                {
                    Title = "🌐 Paste YouTube URL",
                    SubTitle = $"📁 Output folder: {_ytdl.OutputFolder}",
                    IcoPath = "assets/icon.png",
                };

                // If the user hasn't entered anything, return only the initResult
                if (string.IsNullOrWhiteSpace(query.Search))
                {
                    return new List<Result> { initResult };
                }
            }

            // Mark that the user has provided input
            _hasUserInput = true;

            if (IsValidUrl(query.Search))
            {
                var url = $"{query.Search.Trim()}";

                var videoDataResult = await _ytdl.RunVideoDataFetch(url);
                if (videoDataResult.Success)
                {
                    var videoOptionResult = new Result
                    {
                        Title = videoDataResult.Data.Title,
                        SubTitle = "🎥 Download video",
                        Action = _ =>
                        {
                            HandleVideoDownloadAsync(url).ConfigureAwait(false);
                            _context.API.ShowMsg("📁 Download started...", $"🎥 {videoDataResult.Data.Title}", "assets/icon.png");

                            // Reset to initResult after handling the input
                            _hasUserInput = false;
                            return true;
                        },
                        IcoPath = videoDataResult.Data.Thumbnail,
                        Score = 0,
                    };
                    
                    var audioOptionResult = new Result
                    {
                        Title = videoDataResult.Data.Title,
                        SubTitle = "🔊 Download audio",
                        Action = _ =>
                        {
                            HandleAudioDownloadAsync(url).ConfigureAwait(false);
                            _context.API.ShowMsg("📁 Download started...", $"🔊 {videoDataResult.Data.Title}", "assets/icon.png");

                            // Reset to initResult after handling the input
                            _hasUserInput = false;
                            return true;
                        },
                        IcoPath = videoDataResult.Data.Thumbnail,
                        Score = 1,
                    };

                    return new List<Result>() { videoOptionResult, audioOptionResult };
                }
                else
                {
                    var errorResult = new Result
                    {
                        Title = "❌ Error fetching video data :(",
                        SubTitle = String.Join("\n", videoDataResult.ErrorOutput),
                        IcoPath = "assets/icon.png",
                    };

                    // Reset to initResult after an error
                    _hasUserInput = false;
                    return new List<Result>() { errorResult };
                }
            }

            // If the input is invalid, show a message prompting the user to enter a valid URL
            var invalidResult = new Result
            {
                Title = "❌ Invalid URL",
                SubTitle = "Please enter a valid YouTube URL",
                IcoPath = "assets/icon.png",
            };

            // Reset to initResult after invalid input
            _hasUserInput = false;
            return new List<Result> { invalidResult };
        }

        private bool IsValidUrl(string url)
        {
            var regex = new Regex(@"^(https?://)?(www\.)?(youtube\.com/watch\?v=|youtu\.be/)[\w-]+(\?.*)?$");
            return regex.IsMatch(url);
        }

        private async Task HandleVideoDownloadAsync(string url)
        {
            try
            {
                var videoOptions = new OptionSet
                {
                    Format = "bestvideo[height<=1080][ext=mp4]+bestaudio[ext=m4a]/mp4",
                    MergeOutputFormat = DownloadMergeFormat.Mp4
                };

                var videoDownloadResult = await _ytdl.RunVideoDownload(url, overrideOptions: videoOptions);

                if (videoDownloadResult.Success)
                {
                    _context.API.ShowMsg("✅ Download complete!", $"{videoDownloadResult.Data}", "assets/icon.png");
                }
                else
                {
                    _context.API.ShowMsg("❌ Download failed", string.Join("\n", videoDownloadResult.ErrorOutput) ?? "Unknown error occurred.", "assets/icon.png");
                }
            }
            catch (Exception ex)
            {
                _context.API.ShowMsgError("❌ Error", $"{ex.Message}");
            }
        }

        private async Task HandleAudioDownloadAsync(string url)
        {
            try
            {
                var audioDownloadResult = await _ytdl.RunAudioDownload(url, AudioConversionFormat.Mp3);

                if (audioDownloadResult.Success)
                {
                    _context.API.ShowMsg("✅ Download complete!", $"{audioDownloadResult.Data}", "assets/icon.png");
                }
                else
                {
                    _context.API.ShowMsg("❌ Download failed", string.Join("\n", audioDownloadResult.ErrorOutput) ?? "Unknown error occurred.", "assets/icon.png");
                }
            }
            catch (Exception ex)
            {
                _context.API.ShowMsgError("❌ Error", $"{ex.Message}");
            }
        }
    }
}