﻿/************************************
* MPlayer (by Joshua Park & u8sand) *
* updated 2/28/2012                 *
************************************/
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Baka_MPlayer.Forms;

public class MPlayer
{
    #region Variables

    private Process mplayer;
    private readonly MainForm mainForm;
    private bool parsingHeader;
    private bool parsingClipInfo = false;
	private bool cachingFonts = false;
    public bool ignoreUpdate = false;

    #endregion

    #region Constructor

    public MPlayer(MainForm mainForm)
    {
        this.mainForm = mainForm;
    }

    #endregion

    #region Functions

    public bool OpenFile(string url)
    {
        try
        {
            Info.ResetInfo();
            mainForm.CallSetStatus("Loading file...", true);

            if (mplayer != null)
            {
                SendCommand("loadfile \"{0}\"", url.Replace("\\", "\\\\")); // open file
                parsingHeader = true;
                mainForm.ClearOutput();
                return true;
            }
            // mplayer is not running, so start mplayer then load url
            var cmdArgs = string.Format(" -vo {0} -ao {1}", "direct3d", "dsound");
            cmdArgs += " -slave";                		 			// switch on slave mode for frontend
            cmdArgs += " -idle";                 		 			// wait insead of quit
            cmdArgs += " -utf8";                 		 			// handles the subtitle file as UTF-8
            cmdArgs += " -volstep 5";			  		 			// change volume step
            cmdArgs += " -msglevel identify=6:global=6"; 			// set msglevel
            cmdArgs += " -nomouseinput";         		 			// disable mouse input events
            cmdArgs += " -ass";                  		 			// enable .ass subtitle support
            cmdArgs += " -nokeepaspect";         		 			// doesn't keep window aspect ratio when resizing windows
            cmdArgs += " -framedrop";                               // enables soft framedrop
            cmdArgs += string.Format(" -volume {0}", Info.Current.Volume);       // retrieves last volume
            cmdArgs += string.Format(" -wid {0}", mainForm.mplayerPanel.Handle); // output handle

            mplayer = new Process
            {
                StartInfo =
                {
                    FileName = "mplayer2.exe",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    Arguments = cmdArgs + string.Format(" \"{0}\"", url)
                }
            };
            parsingHeader = true;
            mplayer.Start();
            mplayer.EnableRaisingEvents = true;

            mplayer.OutputDataReceived += OutputDataReceived;
            mplayer.ErrorDataReceived += ErrorDataReceived;
            mplayer.Exited += Exited;
            mplayer.BeginOutputReadLine();
            mplayer.BeginErrorReadLine();
        }
        catch (Exception) { return false; }
        return true;
    }
    /// <summary>
    /// Send command to player
    /// </summary>
    public bool SendCommand(string command)
    {
        try
        {
            if (mplayer == null || mplayer.HasExited)
                throw new Exception();

            byte[] buffer = Encoding.UTF8.GetBytes(command);
            mplayer.StandardInput.BaseStream.Write(buffer, 0, buffer.Length);
            mplayer.StandardInput.WriteLine();
            //mplayer.StandardInput.WriteLine(command);
            mplayer.StandardInput.Flush();
        }
        catch (Exception) { return false; }
        return true;
    }
    /// <summary>
    /// Send command to player (uses String.Format)
    /// </summary>
    public bool SendCommand(string command, object value)
    {
        try
        {
            if (mplayer == null || mplayer.HasExited)
                throw new Exception();
            
            byte[] buffer = Encoding.UTF8.GetBytes(string.Format(command,value));
            mplayer.StandardInput.BaseStream.Write(buffer, 0, buffer.Length);
            mplayer.StandardInput.WriteLine();
            //mplayer.StandardInput.WriteLine(command, value);
            mplayer.StandardInput.Flush();
        }
        catch (Exception) { return false; }
        return true;
    }

    public bool MPlayerIsRunning()
    {
        return mplayer != null;
    }
    public string GetMPlayerInfo()
    {
        string strResp = mplayer.Responding ? "" : " - Not Responding";
        return string.Format("MPlayer's ID: {0}{1}", mplayer.Id, strResp);
    }

    public bool Close()
    {
        try
        {
            if (mplayer == null || mplayer.HasExited)
                throw new Exception();

            mplayer.CancelOutputRead();
            mplayer.CancelErrorRead();
            mplayer.Kill();
        }
        catch (Exception) { return false; }
        return true;
    }

    #endregion

    #region API

    public bool Play()
    {
        if (Info.Current.PlayState != PlayStates.Playing)
            return SendCommand("pause"); // pause command toggles between pause and play
        return false;
    }
    public bool Pause(bool toggle)
    {
        if (toggle || Info.Current.PlayState == PlayStates.Playing)
            return SendCommand("pause"); // pause command toggles between pause and play
        return false;
    }
    public bool Stop()
    {
        ignoreUpdate = true;
        Info.Current.PlayState = PlayStates.Stopped;
        mainForm.CallPlayStateChanged();

        return SendCommand("pausing seek 0 2");
    }
    public bool Restart()
    {
        return Rewind() && Play();
    }
    public bool Rewind()
    {
        return SendCommand("seek 0 2");
    }
    public bool Mute(bool mute) // true = mute, false = unmute
    {
        return SendCommand("mute {0}", mute ? 1 : 0);
    }
    public bool SkipChapter(bool ahead) // true = skip ahead, false = skip back
    {
        return SendCommand("seek_chapter {0} 0", ahead ? 1 : -1);
    }
    public bool Seek(double sec)
    {
        return SendCommand("seek {0} 2", (int)sec); // set position to time specified
    }
    public bool SeekFrame(double frame)
    {
        // seek <value> [type] [hr-seek] <- force precise seek if possible
        return SendCommand("seek {0} 2 1", frame / Info.VideoInfo.FPS);
    }

    public bool SetPlayRate(float ratio) // 1 = 100%, normal speed. .5 = 50% speed, 2 = 200% double speed.
    {
        return ratio > 0 && SendCommand("speed_mult {0}", ratio); // set the play rate
    }
    public bool SetVolume(int volume)
    {
        return volume >= 0 && SendCommand("volume {0} 1", volume);
    }
    /// <summary>
    /// Set to -1 to hide subs.
    /// </summary>
    public bool SetSubs(int index)
    {
        // sub_visibility [value]
        return SendCommand("sub_select {0}", index);
    }
    /// <summary>
    /// Sets chapter
    /// </summary>
    /// <param name="index">Based on zero index</param>
    public bool SetChapter(int index)
    {
        //seek_chapter <value> [type]
        return SendCommand("seek_chapter {0} 1", index);
    }
    /// <summary>
    /// Shows [text] on the OSD (on screen display)
    /// </summary>
    public bool ShowStatus(string text)
    {
        return SendCommand(string.Format("osd_show_text {0} {1} {2}", text, '4', '1'));
    }

    #endregion

    #region Events

    private void ErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
		if (!cachingFonts && (e.Data.StartsWith("[") && e.Data.Contains("/")))
		{
            cachingFonts = true;
            mainForm.CallSetStatus("Caching fonts...", true);
		}
		
		if (cachingFonts)
		{
		    mainForm.CallSetStatus("Caching fonts: " + e.Data, true);
            int current, total;
            int pos1 = e.Data.IndexOf('/');
            int pos2 = e.Data.IndexOf(']');

            int.TryParse(e.Data.Substring(1, pos1-1), out current);
            int.TryParse(e.Data.Substring(pos1+1, pos2 - pos1 - 1), out total);

            if (current.Equals(total))
            {
                cachingFonts = false;
                mainForm.CallSetStatus("Fonts finished caching", false);
            }
		}
    }

    private void OutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (parsingHeader)
        {
            if (e.Data.StartsWith("get_path('")) // ignore get_path(...) (result from msglevel global=6)
                return;

            // show output
            mainForm.SetOutput(e.Data);

            if (e.Data.Equals("Clip info:"))
            {
                parsingClipInfo = true;
                return;
            }
            if (parsingClipInfo)
            {
                ParseClipInfo(e.Data);
                return;
            }

            if (e.Data.StartsWith("ID_"))
            {
                // Parsing "ID_*"
                var key = e.Data.Substring(0, e.Data.IndexOf('='));
                var value = e.Data.Substring(e.Data.IndexOf('=') + 1);

                ProcessDetails(key, value);
                Info.MiscInfo.OtherInfo.Add(new ID_Info(key, value));
                return;
            }

            if (e.Data.StartsWith("Video: no video"))
            {
                Info.VideoInfo.HasVideo = false;
                return;
            }

            if (e.Data.Equals("Starting playback..."))
            {
                parsingHeader = false;
                mainForm.CallHideStatusLabel();

                // tell mainform that new file was opened
                mainForm.CallMediaOpened();
            }
        }
        else
        {
            if (e.Data.StartsWith("A:"))
            {
                ProcessProgress(e.Data);
                return;
            }
            ProcessOther(e.Data);
        }
    }

    private void ProcessProgress(string time)
    {
        if (ignoreUpdate)
        {
            ignoreUpdate = false;
            return;
        }
        if (Info.Current.PlayState != PlayStates.Playing)
        {
            Info.Current.PlayState = PlayStates.Playing;
            mainForm.CallPlayStateChanged();
        }

        double sec;
        double.TryParse(time.Substring(2, time.IndexOf('.')).Trim(), out sec);
        Info.Current.Duration = sec;
        
        if (!mainForm.IsDisposed)
            mainForm.CallDurationChanged();
    }

    private bool ProcessDetails(string key, string value)
    {
        switch (key)
        {
            case "ID_FILENAME":
                Info.URL = value;
                Info.FileName = Path.GetFileName(value);
                break;
            case "ID_VIDEO_WIDTH":
                int width;
                int.TryParse(value, out width);
                Info.VideoInfo.Width = width;
                break;
            case "ID_VIDEO_HEIGHT":
                int height;
                int.TryParse(value, out height);
                Info.VideoInfo.Height = height;
                break;
            case "ID_VIDEO_FPS":
                double fps;
                double.TryParse(value, out fps);
                Info.VideoInfo.FPS = fps;
                break;
            case "ID_VIDEO_ASPECT":
                double ratio;
                double.TryParse(value, out ratio);
                Info.VideoInfo.AspectRatio = ratio;
                break;
            case "ID_LENGTH":
                double length;
                double.TryParse(value, out length);
                Info.Current.TotalLength = length;
                break;
            default:
				if (key.StartsWith("ID_CHAPTER_ID")) // Chapters
                {
                    Info.MiscInfo.Chapters.Add(new Chapter());
                }
                else if (key.StartsWith("ID_CHAPTER_")) // Chapters
                {
                    if (key.Contains("_START"))
                    {
                        long frame;
                        long.TryParse(value, out frame);
                        Info.MiscInfo.Chapters[Info.MiscInfo.Chapters.Count - 1].StartFrame = frame;
                    }
                    else if (key.Contains("_NAME"))
                    {
                        Info.MiscInfo.Chapters[Info.MiscInfo.Chapters.Count - 1].ChapterName = value;
                    }
                    return true;
                }
				
				else if (key.StartsWith("ID_SUBTITLE_ID")) // Subtitles
                {
                    Info.MiscInfo.Subs.Add(new Sub(value));
                }
                else if (key.StartsWith("ID_SID_")) // Subtitles
                {
                    if (key.Contains("_NAME"))
                    {
                        Info.MiscInfo.Subs[Info.MiscInfo.Subs.Count - 1].Name = value;
                    }
                    else if (key.Contains("_LANG"))
                    {
                        Info.MiscInfo.Subs[Info.MiscInfo.Subs.Count - 1].Lang = value;
                    }
                    return true;
                }
                
				else if (key.StartsWith("ID_AUDIO_ID")) // Audio tracks
                {
                    Info.AudioInfo.AudioTracks.Add(new AudioTrack(value));
                }
                else if (key.StartsWith("ID_AID_"))
                {
                    if (key.Contains("_NAME"))
                    {
                        Info.AudioInfo.AudioTracks[Info.AudioInfo.AudioTracks.Count - 1].Name = value;
                    }
                    else if (key.Contains("_LANG"))
                    {
                        Info.AudioInfo.AudioTracks[Info.AudioInfo.AudioTracks.Count - 1].Lang = value;
                    }
                    return true;
                }
                return false;
        }
        return true;
    }

    private void ProcessOther(string output)
    {
        if (output.StartsWith("ID_PAUSED"))
        {
            Info.Current.PlayState = PlayStates.Paused;
            mainForm.CallPlayStateChanged();
        }
        else if (output.StartsWith("EOF code: 1")) //EOF code: 4 ??
        {
            mainForm.CallMediaEnded();
        }
        else
        {
            // Other information while playing
        }
    }
    private void ParseClipInfo(string data)
    {
        if (data.StartsWith(" album_artist:"))
            Info.ID3Tags.Album_Artist = data.Substring(data.IndexOf(": ") + 1);
        else if (data.StartsWith(" encoder:"))
            Info.ID3Tags.Encoder = data.Substring(data.IndexOf(": ") + 1);
        else if (data.StartsWith(" artist:"))
            Info.ID3Tags.Artist = data.Substring(data.IndexOf(": ") + 1);
        else if (data.StartsWith(" genre:"))
            Info.ID3Tags.Genre = data.Substring(data.IndexOf(": ") + 1);
        else if (data.StartsWith(" track:"))
        {
            int track;
            int.TryParse(data.Substring(data.IndexOf(": ") + 1), out track);
            Info.ID3Tags.Track = track;
        }
        else if (data.StartsWith(" disk:"))
        {
            int disk;
            int.TryParse(data.Substring(data.IndexOf(": ") + 1), out disk);
            Info.ID3Tags.Disc = disk;
        }
        else if (data.StartsWith(" title:"))
            Info.ID3Tags.Title = data.Substring(data.IndexOf(": ") + 1);
        else if (data.StartsWith(" album:"))
            Info.ID3Tags.Album = data.Substring(data.IndexOf(": ") + 1);
        else if (data.StartsWith(" date:"))
            Info.ID3Tags.Date = data.Substring(data.IndexOf(": ") + 1);
        else if (data.StartsWith("ID_CLIP_INFO_N"))
            parsingClipInfo = false;
    }
    private void Exited(object sender, EventArgs e)
    {
        Application.Exit();
    }

    #endregion
}