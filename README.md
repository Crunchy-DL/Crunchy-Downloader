# Crunchy-Downloader
Downloader for Crunchyroll


# DRM Decryption Guide
## Decryption Requirements

- **mp4decrypt** (available at [Bento4.com](http://www.bento4.com/)) - Required for the decryption process. This tool should be placed in the `./bin/` directory.

## Instructions

To decrypt DRM content, it's essential to first acquire a CDM (Content Decryption Module). Once obtained, you will need to place the following CDM files into the `./widevine/` directory:

- `device_client_id_blob.bin`
- `device_private_key.pem`

For legal reasons, the CDM is not included with the software package, and you must source it independently.
