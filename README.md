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
  <a href="https://discord.gg/QmGhqkAQBT">
    <img src="https://img.shields.io/badge/Discord-7289DA?style=for-the-badge&logo=discord&logoColor=white" alt="Discord">
  </a>
</p>



# 📜 Disclaimer

> I am in no way affiliated with, maintained, authorized, sponsored, or officially associated with Crunchyroll LLC or any of its subsidiaries or affiliates.
> The official Crunchyroll website can be found at https://crunchyroll.com/.

This tool is meant for private use only. You need a [Crunchyroll Premium](https://www.crunchyroll.com/premium) subscription to access premium content.

The usage of this application may also cause a violation of the Terms of Service between you and the stream provider.

This application enables you to download videos for offline viewing which may be forbidden by law in your country. You are entirely responsible for what happens when you use crunchy-downloader.

# ✏️ Software Requirements

Windows 10 or Windows 11

.NET Desktop Runtime

## ✨ Features

[Github Wiki](https://github.com/Crunchy-DL/Crunchy-Downloader/wiki)


# 🛠️ DRM Decryption Guide

## Decryption Requirements

- **mp4decrypt** (available at [Bento4.com](http://www.bento4.com/)) - Required for the decryption process. This tool should be placed in the `./lib/` directory.

## Instructions

To decrypt DRM content, it's essential to first acquire a CDM (Content Decryption Module). Once obtained, you will need to place the following CDM files into the `./widevine/` directory:

- `device_client_id_blob.bin`
- `device_private_key.pem`

For legal reasons, the CDM is not included with the software package, and you must source it independently.



