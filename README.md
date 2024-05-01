# 💾 Crunchy-Downloader
This intuitive software allows you to seamlessly download your favorite series and episodes directly from Crunchyroll, ensuring you can enjoy high-quality anime offline, anytime and anywhere.

> I am in no way affiliated with, maintained, authorized, sponsored, or officially associated with Crunchyroll LLC or any of its subsidiaries or affiliates.
> The official Crunchyroll website can be found at https://crunchyroll.com/.

# ✏️ Software Requirements

Windows 10 or Windows 11

# 🖥️ User Interface

Downloads overview:
![ui_downloads](https://github.com/Crunchy-DL/Crunchy-Downloader/assets/75888166/56989620-e8bf-421a-a11f-af282f8fd00b)
Add new downloads:
![ui_adddownload](https://github.com/Crunchy-DL/Crunchy-Downloader/assets/75888166/0b9dc931-e439-4e96-8298-a7923d8a467a)
Calendar:
![ui_calendar](https://github.com/Crunchy-DL/Crunchy-Downloader/assets/75888166/e21610b0-f28a-4c7f-a596-eeec82622a93)
History Overview:
![ui_history](https://github.com/Crunchy-DL/Crunchy-Downloader/assets/75888166/cdbe244d-3f50-40fc-9316-ef25dc9d0d39)
History Series Overview:
![ui_history_series](https://github.com/Crunchy-DL/Crunchy-Downloader/assets/75888166/71e10d2f-302a-4f31-b220-f8d4f802060e)
Settings:
![ui_settings](https://github.com/Crunchy-DL/Crunchy-Downloader/assets/75888166/f8b09911-5036-43e2-907d-8b81accbf149)


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
