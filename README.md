# 💾 Crunchyroll-Downloader

A simple crunchyroll downloader that allows you to download your favorite series and episodes directly from [Crunchyroll](https://www.crunchyroll.com).

> ⚠️ **Disclaimer:** This tool is intended for private use only. It is not affiliated with, maintained, authorized, sponsored, or officially associated with Crunchyroll LLC or any of its subsidiaries or affiliates. Use of this application may violate Crunchyroll's Terms of Service and could be illegal in your country. You are solely responsible for your use of Crunchy-Downloader. You need a [Crunchyroll Premium](https://www.crunchyroll.com/premium) subscription to access premium content.


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
    <img src="https://img.shields.io/github/v/release/Crunchy-DL/Crunchy-Downloader?style=flat-square" alt="Release">
  </a>
</p>
<p align="center">
  <a href="https://discord.gg/QmGhqkAQBT">
    <img src="https://img.shields.io/badge/Discord-7289DA?style=for-the-badge&logo=discord&logoColor=white" alt="Discord">
  </a>
</p>


## 🛠️ System Requirements

- **Operating System:** Windows 10 or Windows 11
- **.NET Desktop Runtime:** Version 8.0
- **Visual C++ Redistributable:** 2015–2022

## 🖥️ Features

- **Download Episodes and Series:** Fetch individual episodes or entire series from Crunchyroll
- **Multiple Subtitle and Audio Tracks:** Support for downloading videos with various subtitles and audio tracks
- **User-Friendly Interface:** Intuitive GUI for easy navigation and operation
- **Calendar View:** View upcoming episodes and schedule downloads
- **Download History:** Keep track of your downloaded content
- **Settings Customization:** Adjust settings to suit your preferences

For detailed information on each feature, please refer to the [GitHub Wiki](https://github.com/Crunchy-DL/Crunchy-Downloader/wiki).

## 🔐 DRM Decryption Guide

### 1. Obtain Decryption Tools

Place one of the following tools in the `./lib/` directory:

- **mp4decrypt:** Available at [Bento4](http://www.bento4.com/)
- **shaka-packager:** Available at [Shaka Packager Releases](https://github.com/shaka-project/shaka-packager/releases/latest)

### 2. Acquire Widevine CDM Files

Create a folder named `widevine` in the root directory of Crunchy-Downloader and place the following files inside:

- `device_client_id_blob.bin`
- `device_private_key.pem`

> ⚠️ **Note:** Due to legal reasons, these CDM files are not provided with the application. You must source them independently.

For more information, refer to the [Widevine FAQ](https://github.com/Crunchy-DL/Crunchy-Downloader/discussions/36)



