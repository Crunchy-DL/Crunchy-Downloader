# 💾 Crunchy-Downloader
This intuitive software allows you to seamlessly download your favorite series and episodes directly from Crunchyroll, ensuring you can enjoy high-quality anime offline, anytime and anywhere.

> I am in no way affiliated with, maintained, authorized, sponsored, or officially associated with Crunchyroll LLC or any of its subsidiaries or affiliates.
> The official Crunchyroll website can be found at https://crunchyroll.com/.

# ✏️ Software Requirements

Windows 10 or Windows 11

# 🖥️ User Interface

Downloads overview:
![ui_downloads](https://github.com/Crunchy-DL/Crunchy-Downloader/assets/75888166/e1284e43-0997-4528-a5de-2c9c4a2cec46)
Add new downloads:
![ui_adddownload](https://github.com/Crunchy-DL/Crunchy-Downloader/assets/75888166/8f2a89bc-3caa-4538-bb8a-94d837f4c424)
Calendar:
![ui_calendar](https://github.com/Crunchy-DL/Crunchy-Downloader/assets/75888166/c5234a8e-8986-41d5-bb25-e84a85dbda9a)
History Overview:
![ui_history](https://github.com/Crunchy-DL/Crunchy-Downloader/assets/75888166/5ac8af06-6462-487f-a5b1-1f6b2ad3b25d)
History Series Overview:
![ui_history_series](https://github.com/Crunchy-DL/Crunchy-Downloader/assets/75888166/71e10d2f-302a-4f31-b220-f8d4f802060e)
Settings:
![ui_settings](https://github.com/Crunchy-DL/Crunchy-Downloader/assets/75888166/b82f801d-9b18-45e2-91cf-3a74a61ab661)



# 🛠️ DRM Decryption Guide

Currently, it is not necessary to include the CDM files as it will use non-DRM URLs if possible.

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
