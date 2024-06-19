# 💾 Crunchyroll-Downloader

A simple crunchyroll downloader that allows you to download your favorite series and episodes directly from [Crunchyroll](https://www.crunchyroll.com).

<p align="center">
  <a href="https://github.com/Crunchy-DL/Crunchy-Downloader">
    <img src="https://img.shields.io/github/languages/code-size/Crunchy-DL/Crunchy-Downloader?style=flat-square" alt="Code size">
  </a>
  <a href="https://github.com/Crunchy-DL/Crunchy-Downloader/releases/latest">
    <img src="https://img.shields.io/github/downloads/Crunchy-DL/Crunchy-Downloader/total?style=flat-square" alt="Download Badge">
  </a>
  <a href="https://github.com/Crunchy-DL/Crunchy-Downloader/blob/master/LICENSE">
    <img src="https://img.shields.io/github/license/Crunchy-DL/Crunchy-Downloader?style=flat-square" alt="License">
  </a>
  <a href="https://github.com/Crunchy-DL/Crunchy-Downloader/releases">
    <img src="https://img.shields.io/github/v/release/Crunchy-DL/Crunchy-Downloader?include_prereleases&style=flat-square" alt="Release">
  </a>
</p>
<p align="center">
  <a href="https://discord.gg/zDKNU8UUqt">
    <img src="https://img.shields.io/badge/Discord-7289DA?style=for-the-badge&logo=discord&logoColor=white" alt="Discord">
  </a>
</p>


> I am in no way affiliated with, maintained, authorized, sponsored, or officially associated with Crunchyroll LLC or any of its subsidiaries or affiliates.
> The official Crunchyroll website can be found at https://crunchyroll.com/.

# ✏️ Software Requirements

Windows 10 or Windows 11

.NET Desktop Runtime

## ✨ Features

- **Account Management**: Sign in and download according to your Crunchyroll subscription status.
- **Download Options**: Choose to download only the video, only the audio of episodes.
- **Crunchyroll Chapters Support**: Download chapters for episodes from Crunchyroll.
- **Select Multiple Dubs and Subtitles**: Choose multiple dubs or subtitles in the settings, or opt for a hardsub.
- **Max Concurrent Downloads Selector**: Control the maximum number of concurrent downloads.
- **Highly Customizable Filename Settings**: Customize the filename settings according to your preferences.
- **Download Management**: Pause, resume, or cancel active downloads at any time.
- **Complete Series Download**: Add an entire series (every season, every episode) to download with one click.
- **Add Single Episodes with URL**: Download single episodes by entering their URL.
- **Simulcast Calendar Integration**: Directly select episodes to download from the Crunchyroll simulcast calendar.
- **History Overview**: View all the series you've downloaded in a comprehensive history overview.
- **New Releases Check**: Automatically check for new releases in your download history.
- **One-Click Add New Releases**: Easily add all new releases to your download queue with one click.
- **Series Overview**: Get a detailed overview of downloaded series to easily download missing episodes and track what's been downloaded.
- **Sonarr Integration**: Link your Sonarr server in the settings to see the episodes available on your Sonarr server in the history overview.

# 🖥️ User Interface

Downloads overview:
![ui_downloads](https://github.com/Crunchy-DL/Crunchy-Downloader/blob/master/images/Download_Queue.png)
Add new downloads:
![ui_adddownload](https://github.com/Crunchy-DL/Crunchy-Downloader/blob/master/images/Add_Downloads.png)
Calendar:
![ui_calendar](https://github.com/Crunchy-DL/Crunchy-Downloader/blob/master/images/Calendar.png)
History Overview:
![ui_history](https://github.com/Crunchy-DL/Crunchy-Downloader/blob/master/images/History_Overview.png)
History Series Overview:
![ui_history_series](https://github.com/Crunchy-DL/Crunchy-Downloader/blob/master/images/History_Series_Overview.png)
Settings:
![ui_settings](https://github.com/Crunchy-DL/Crunchy-Downloader/blob/master/images/Settings.png)



# 🛠️ DRM Decryption Guide

## Decryption Requirements

- **mp4decrypt** (available at [Bento4.com](http://www.bento4.com/)) - Required for the decryption process. This tool should be placed in the `./lib/` directory.

## Instructions

To decrypt DRM content, it's essential to first acquire a CDM (Content Decryption Module). Once obtained, you will need to place the following CDM files into the `./widevine/` directory:

- `device_client_id_blob.bin`
- `device_private_key.pem`

For legal reasons, the CDM is not included with the software package, and you must source it independently.

# 📜 Disclaimer

This tool is meant for private use only. You need a Crunchyroll Premium subscription to access premium content.

You are entirely responsible for what happens when you use crunchy-downloader.
