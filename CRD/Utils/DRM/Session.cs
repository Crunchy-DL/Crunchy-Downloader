using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.OpenSsl;
using ProtoBuf;

namespace CRD.Utils.DRM;

public struct ContentDecryptionModule{
    public byte[] privateKey{ get; set; }
    public byte[] identifierBlob{ get; set; }
}

public class DerivedKeys{
    public byte[] Auth1{ get; set; }
    public byte[] Auth2{ get; set; }
    public byte[] Enc{ get; set; }
}

public class Session{
    public byte[] WIDEVINE_SYSTEM_ID = new byte[]{ 237, 239, 139, 169, 121, 214, 74, 206, 163, 200, 39, 220, 213, 29, 33, 237 };

    private RSA _devicePrivateKey;
    private ClientIdentification _identifierBlob;
    private byte[] _identifier;
    private byte[] _pssh;
    private byte[] _rawLicenseRequest;
    private byte[] _sessionKey;
    private DerivedKeys _derivedKeys;
    private OaepEncoding _decryptEngine;
    public List<ContentKey> ContentKeys { get; set; } = new List<ContentKey>();
    public dynamic InitData{ get; set; }

    private AsymmetricCipherKeyPair DeviceKeys{ get; set; }

    public Session(ContentDecryptionModule contentDecryptionModule, byte[] pssh){
        _devicePrivateKey = CreatePrivateKeyFromPem(contentDecryptionModule.privateKey);

        using var reader = new StringReader(Encoding.UTF8.GetString(contentDecryptionModule.privateKey));
        DeviceKeys = (AsymmetricCipherKeyPair)new PemReader(reader).ReadObject();

        _identifierBlob = Serializer.Deserialize<ClientIdentification>(new MemoryStream(contentDecryptionModule.identifierBlob));
        _identifier = GenerateIdentifier();
        _pssh = pssh;
        InitData = ParseInitData(pssh);
        _decryptEngine = new OaepEncoding(new RsaEngine());
        _decryptEngine.Init(false, DeviceKeys.Private);
    }

    private RSA CreatePrivateKeyFromPem(byte[] pemKey){
        RSA rsa = RSA.Create();
        string s = System.Text.Encoding.UTF8.GetString(pemKey);
        rsa.ImportFromPem(s);
        return rsa;
    }

    private byte[] GenerateIdentifier(){
        // Generate 8 random bytes
        byte[] randomBytes = RandomNumberGenerator.GetBytes(8);

        // Convert to hex string
        string hex = BitConverter.ToString(randomBytes).Replace("-", "").ToLower();

        // Concatenate with '01' and '00000000000000'
        string identifier = hex + "01" + "00000000000000";

        // Convert the final string to a byte array
        return Encoding.UTF8.GetBytes(identifier);
    }

    public byte[] GetLicenseRequest(){
        dynamic licenseRequest;

        if (InitData is WidevineCencHeader){
            licenseRequest = new SignedLicenseRequest{
                Type = SignedLicenseRequest.MessageType.LicenseRequest,
                Msg = new LicenseRequest{
                    Type = LicenseRequest.RequestType.New,
                    KeyControlNonce = 1093602366,
                    ProtocolVersion = ProtocolVersion.Current,
                    RequestTime = uint.Parse((DateTime.Now - DateTime.UnixEpoch).TotalSeconds.ToString().Split(",")[0]),
                    ContentId = new LicenseRequest.ContentIdentification{
                        CencId = new LicenseRequest.ContentIdentification.Cenc{
                            LicenseType = LicenseType.Default,
                            RequestId = _identifier,
                            Pssh = InitData
                        }
                    }
                }
            };
        } else{
            licenseRequest = new SignedLicenseRequestRaw{
                Type = SignedLicenseRequestRaw.MessageType.LicenseRequest,
                Msg = new LicenseRequestRaw{
                    Type = LicenseRequestRaw.RequestType.New,
                    KeyControlNonce = 1093602366,
                    ProtocolVersion = ProtocolVersion.Current,
                    RequestTime = uint.Parse((DateTime.Now - DateTime.UnixEpoch).TotalSeconds.ToString().Split(",")[0]),
                    ContentId = new LicenseRequestRaw.ContentIdentification{
                        CencId = new LicenseRequestRaw.ContentIdentification.Cenc{
                            LicenseType = LicenseType.Default,
                            RequestId = _identifier,
                            Pssh = InitData
                        }
                    }
                }
            };
        }

        licenseRequest.Msg.ClientId = _identifierBlob;

        //Logger.Debug("Signing license request");

        using (var memoryStream = new MemoryStream()){
            Serializer.Serialize(memoryStream, licenseRequest.Msg);
            byte[] data = memoryStream.ToArray();
            _rawLicenseRequest = data;

            licenseRequest.Signature = Sign(data);
        }

        byte[] requestBytes;
        using (var memoryStream = new MemoryStream()){
            Serializer.Serialize(memoryStream, licenseRequest);
            requestBytes = memoryStream.ToArray();
        }

        return requestBytes;
    }

    static WidevineCencHeader ParseInitData(byte[] initData){
        WidevineCencHeader cencHeader;

        try{
            cencHeader = Serializer.Deserialize<WidevineCencHeader>(new MemoryStream(initData[32..]));
        } catch{
            try{
                //needed for HBO Max

                PSSHBox psshBox = PSSHBox.FromByteArray(initData);
                cencHeader = Serializer.Deserialize<WidevineCencHeader>(new MemoryStream(psshBox.Data));
            } catch{
                //Logger.Verbose("Unable to parse, unsupported init data format");
                return null;
            }
        }

        return cencHeader;
    }


    public byte[] Sign(byte[] data){
        PssSigner eng = new PssSigner(new RsaEngine(), new Sha1Digest());

        eng.Init(true, DeviceKeys.Private);
        eng.BlockUpdate(data, 0, data.Length);
        return eng.GenerateSignature();
    }

    public byte[] Decrypt(byte[] data){
        int blockSize = _decryptEngine.GetInputBlockSize();
        List<byte> plainText = new List<byte>();

        // Process the data in blocks
        for (int chunkPosition = 0; chunkPosition < data.Length; chunkPosition += blockSize){
            int chunkSize = Math.Min(blockSize, data.Length - chunkPosition);
            byte[] decryptedChunk = _decryptEngine.ProcessBlock(data, chunkPosition, chunkSize);
            plainText.AddRange(decryptedChunk);
        }

        return plainText.ToArray();
    }

    public void ProvideLicense(byte[] license){
        SignedLicense signedLicense;
        try{
            signedLicense = Serializer.Deserialize<SignedLicense>(new MemoryStream(license));
        } catch{
            throw new Exception("Unable to parse license");
        }

        try{
            var sessionKey = Decrypt(signedLicense.SessionKey);

            if (sessionKey.Length != 16){
                throw new Exception("Unable to decrypt session key");
            }

            _sessionKey = sessionKey;
        } catch{
            throw new Exception("Unable to decrypt session key");
        }

        _derivedKeys = DeriveKeys(_rawLicenseRequest, _sessionKey);

        byte[] licenseBytes;
        using (var memoryStream = new MemoryStream()){
            Serializer.Serialize(memoryStream, signedLicense.Msg);
            licenseBytes = memoryStream.ToArray();
        }

        byte[] hmacHash = CryptoUtils.GetHMACSHA256Digest(licenseBytes, _derivedKeys.Auth1);

        if (!hmacHash.SequenceEqual(signedLicense.Signature)){
            throw new Exception("License signature mismatch");
        }

        foreach (License.KeyContainer key in signedLicense.Msg.Keys){
            string type = key.Type.ToString();

            if (type == "Signing")
                continue;

            byte[] keyId;
            byte[] encryptedKey = key.Key;
            byte[] iv = key.Iv;
            keyId = key.Id;
            if (keyId == null){
                keyId = Encoding.ASCII.GetBytes(key.Type.ToString());
            }

            byte[] decryptedKey;

            using MemoryStream mstream = new MemoryStream();
            using AesCryptoServiceProvider aesProvider = new AesCryptoServiceProvider{
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7
            };
            using CryptoStream cryptoStream = new CryptoStream(mstream, aesProvider.CreateDecryptor(_derivedKeys.Enc, iv), CryptoStreamMode.Write);
            cryptoStream.Write(encryptedKey, 0, encryptedKey.Length);
            decryptedKey = mstream.ToArray();

            List<string> permissions = new List<string>();
            if (type == "OperatorSession"){
                foreach (PropertyInfo perm in key._OperatorSessionKeyPermissions.GetType().GetProperties()){
                    if ((uint)perm.GetValue(key._OperatorSessionKeyPermissions) == 1){
                        permissions.Add(perm.Name);
                    }
                }
            }

            ContentKeys.Add(new ContentKey{
                KeyID = keyId,
                Type = type,
                Bytes = decryptedKey,
                Permissions = permissions
            });
        }

 
    }

    public static DerivedKeys DeriveKeys(byte[] message, byte[] key){
        byte[] encKeyBase = Encoding.UTF8.GetBytes("ENCRYPTION").Concat(new byte[]{ 0x0, }).Concat(message).Concat(new byte[]{ 0x0, 0x0, 0x0, 0x80 }).ToArray();
        byte[] authKeyBase = Encoding.UTF8.GetBytes("AUTHENTICATION").Concat(new byte[]{ 0x0, }).Concat(message).Concat(new byte[]{ 0x0, 0x0, 0x2, 0x0 }).ToArray();

        byte[] encKey = new byte[]{ 0x01 }.Concat(encKeyBase).ToArray();
        byte[] authKey1 = new byte[]{ 0x01 }.Concat(authKeyBase).ToArray();
        byte[] authKey2 = new byte[]{ 0x02 }.Concat(authKeyBase).ToArray();
        byte[] authKey3 = new byte[]{ 0x03 }.Concat(authKeyBase).ToArray();
        byte[] authKey4 = new byte[]{ 0x04 }.Concat(authKeyBase).ToArray();

        byte[] encCmacKey = CryptoUtils.GetCMACDigest(encKey, key);
        byte[] authCmacKey1 = CryptoUtils.GetCMACDigest(authKey1, key);
        byte[] authCmacKey2 = CryptoUtils.GetCMACDigest(authKey2, key);
        byte[] authCmacKey3 = CryptoUtils.GetCMACDigest(authKey3, key);
        byte[] authCmacKey4 = CryptoUtils.GetCMACDigest(authKey4, key);

        byte[] authCmacCombined1 = authCmacKey1.Concat(authCmacKey2).ToArray();
        byte[] authCmacCombined2 = authCmacKey3.Concat(authCmacKey4).ToArray();

        return new DerivedKeys{
            Auth1 = authCmacCombined1,
            Auth2 = authCmacCombined2,
            Enc = encCmacKey
        };
    }

    // public KeyContainer ParseLicense(byte[] rawLicense){
    //     if (_rawLicenseRequest == null){
    //         throw new InvalidOperationException("Please request a license first.");
    //     }
    //
    //     // Assuming SignedMessage and License have Decode methods that deserialize the respective types
    //     var signedLicense =  Serializer.Deserialize<SignedMessage>(new MemoryStream(rawLicense));
    //     byte[] sessionKey = _devicePrivateKey.Decrypt(signedLicense.SessionKey, RSAEncryptionPadding.OaepSHA256);
    //
    //     var cmac = new AesCmac(sessionKey);
    //     var encKeyBase = Concat("ENCRYPTION\x00", _rawLicenseRequest, "\x00\x00\x00\x80");
    //     var authKeyBase = Concat("AUTHENTICATION\x00", _rawLicenseRequest, "\x00\x00\x02\x00");
    //
    //     byte[] encKey = cmac.ComputeHash(Concat("\x01", encKeyBase));
    //     byte[] serverKey = Concat(
    //         cmac.ComputeHash(Concat("\x01", authKeyBase)),
    //         cmac.ComputeHash(Concat("\x02", authKeyBase))
    //     );
    //
    //     using var hmac = new HMACSHA256(serverKey);
    //     byte[] calculatedSignature = hmac.ComputeHash(signedLicense.Msg);
    //
    //     if (!calculatedSignature.SequenceEqual(signedLicense.Signature)){
    //         throw new InvalidOperationException("Signatures do not match.");
    //     }
    //
    //     var license = License.Decode(signedLicense.Msg);
    //
    //     return license.Key.Select(keyContainer => {
    //         string keyId = keyContainer.Id.Length > 0 ? BitConverter.ToString(keyContainer.Id).Replace("-", "").ToLower() : keyContainer.Type.ToString();
    //         using var aes = Aes.Create();
    //         aes.Key = encKey;
    //         aes.IV = keyContainer.Iv;
    //         aes.Mode = CipherMode.CBC;
    //
    //         using var decryptor = aes.CreateDecryptor();
    //         byte[] decryptedKey = decryptor.TransformFinalBlock(keyContainer.Key, 0, keyContainer.Key.Length);
    //
    //         return new KeyContainer{
    //             Kid = keyId,
    //             Key = BitConverter.ToString(decryptedKey).Replace("-", "").ToLower()
    //         };
    //     }).ToArray();
    // }
}