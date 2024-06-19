using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

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
                var files = Directory.GetFiles(CfgManager.PathWIDEVINE_DIR);

                foreach (var file in files){
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.Length < 1024 * 8 && !fileInfo.Attributes.HasFlag(FileAttributes.Directory)){
                        string fileContents = File.ReadAllText(file, Encoding.UTF8);
                        if (fileContents.Contains("-BEGIN RSA PRIVATE KEY-")){
                            privateKey = File.ReadAllBytes(file);
                        }

                        if (fileContents.Contains("widevine_cdm_version")){
                            identifierBlob = File.ReadAllBytes(file);
                        }
                    }
                }
            }


            if (privateKey.Length != 0 && identifierBlob.Length != 0){
                canDecrypt = true;
            } else if (privateKey.Length == 0){
                Console.Error.WriteLine("Private key missing");
                canDecrypt = false;
            } else if (identifierBlob.Length == 0){
                Console.Error.WriteLine("Identifier blob missing");
                canDecrypt = false;
            }
        } catch (Exception e){
            Console.Error.WriteLine("Widevine: " + e);
            canDecrypt = false;
        }
    }
    
    public async Task<List<ContentKey>> getKeys(string? pssh, string licenseServer, Dictionary<string, string> authData){
        if (pssh == null || !canDecrypt) return new List<ContentKey>();

        byte[] psshBuffer = Convert.FromBase64String(pssh);

        Session ses = new Session(new ContentDecryptionModule{ identifierBlob = identifierBlob, privateKey = privateKey }, psshBuffer);

        var playbackRequest2 = new HttpRequestMessage(HttpMethod.Post, licenseServer);
        foreach (var keyValuePair in authData){
            playbackRequest2.Headers.Add(keyValuePair.Key, keyValuePair.Value);
        }

        var licenceReq = ses.GetLicenseRequest();
        playbackRequest2.Content = new ByteArrayContent(licenceReq);

        var response = await HttpClientReq.Instance.SendHttpRequest(playbackRequest2);

        if (!response.IsOk){
            Console.Error.WriteLine("Failed to get Keys!");
            return new List<ContentKey>();
        }

        LicenceReqResp resp = Helpers.Deserialize<LicenceReqResp>(response.ResponseContent,null) ?? new LicenceReqResp();

        ses.ProvideLicense(Convert.FromBase64String(resp.license));

        return ses.ContentKeys;
    }
}

public class LicenceReqResp{
    public string status{ get; set; }
    public string license{ get; set; }
    public string platform{ get; set; }
    public string message_type{ get; set; }
}