using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CRD.Utils.Files;

namespace CRD.Utils.DRM;

public class Widevine{
    private byte[] privateKey = new byte[0];
    private byte[] identifierBlob = new byte[0];

    public bool canDecrypt = false;


    #region Singelton

    private static Widevine? instance;
    private static readonly object padlock = new object();

    public static Widevine Instance{
        get{
            if (instance == null){
                lock (padlock){
                    if (instance == null){
                        instance = new Widevine();
                    }
                }
            }

            return instance;
        }
    }

    #endregion

    public Widevine(){
        try{
            if (Directory.Exists(CfgManager.PathWIDEVINE_DIR)){
                foreach (var file in Directory.EnumerateFiles(CfgManager.PathWIDEVINE_DIR)){
                    var fileInfo = new FileInfo(file);

                    if (fileInfo.Length >= 1024 * 8 || fileInfo.Attributes.HasFlag(FileAttributes.Directory))
                        continue;

                    string fileContents = File.ReadAllText(file, Encoding.UTF8);

                    if (IsPrivateKey(fileContents)){
                        privateKey = File.ReadAllBytes(file);
                    } else if (IsWidevineIdentifierBlob(fileContents)){
                        identifierBlob = File.ReadAllBytes(file);
                    }
                }
            }

            if (privateKey?.Length > 0 && identifierBlob?.Length > 0){
                canDecrypt = true;
            } else{
                canDecrypt = false;
                if (privateKey == null || privateKey.Length == 0){
                    Console.Error.WriteLine("Private key missing");
                }

                if (identifierBlob == null || identifierBlob.Length == 0){
                    Console.Error.WriteLine("Identifier blob missing");
                }
            }
        } catch (IOException ioEx){
            Console.Error.WriteLine("I/O error accessing Widevine files: " + ioEx);
            canDecrypt = false;
        } catch (UnauthorizedAccessException uaEx){
            Console.Error.WriteLine("Permission error accessing Widevine files: " + uaEx);
            canDecrypt = false;
        } catch (Exception ex){
            Console.Error.WriteLine("Unexpected Widevine error: " + ex);
            canDecrypt = false;
        }

        Console.WriteLine($"CDM available: {canDecrypt}");
    }

    private bool IsPrivateKey(string content){
        return content.Contains("-BEGIN RSA PRIVATE KEY-", StringComparison.Ordinal) ||
               content.Contains("-BEGIN PRIVATE KEY-", StringComparison.Ordinal);
    }

    private bool IsWidevineIdentifierBlob(string content){
        return content.Contains("widevine_cdm_version", StringComparison.Ordinal);
    }

    public async Task<List<ContentKey>> getKeys(string? pssh, string licenseServer, Dictionary<string, string> authData){
        if (pssh == null || !canDecrypt){
            Console.Error.WriteLine("Missing pssh or cdm files");
            return new List<ContentKey>();
        }

        try{
            byte[] psshBuffer = Convert.FromBase64String(pssh);

            Session ses = new Session(new ContentDecryptionModule{ identifierBlob = identifierBlob, privateKey = privateKey }, psshBuffer);

            var playbackRequest2 = new HttpRequestMessage(HttpMethod.Post, licenseServer);
            foreach (var keyValuePair in authData){
                playbackRequest2.Headers.Add(keyValuePair.Key, keyValuePair.Value);
            }

            var licenceReq = ses.GetLicenseRequest();
            var content = new ByteArrayContent(licenceReq);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            playbackRequest2.Content = content;

            var response = await HttpClientReq.Instance.SendHttpRequest(playbackRequest2);

            if (!response.IsOk){
                Console.Error.WriteLine("Failed to get Keys!");
                return new List<ContentKey>();
            }

            LicenceReqResp resp = Helpers.Deserialize<LicenceReqResp>(response.ResponseContent, null) ?? new LicenceReqResp();

            ses.ProvideLicense(Convert.FromBase64String(resp.license));

            return ses.ContentKeys;
        } catch (Exception e){
            Console.Error.WriteLine(e);
            return new List<ContentKey>();
        }
    }
}

public class LicenceReqResp{
    public string status{ get; set; }
    public string license{ get; set; }
    public string platform{ get; set; }
    public string message_type{ get; set; }
}