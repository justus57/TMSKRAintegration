using IronBarCode;
using Newtonsoft.Json;
using QRCoder;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Timers;
using System.Web.Script.Serialization;

namespace TMSKRAintegration
{
    public partial class TMSKRAIntergration : ServiceBase
    {
        private Timer _timer = null;
        public TMSKRAIntergration()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            Utilities.WriteLog("Service Started");
            Timer timer = new Timer();
            timer.Elapsed += new System.Timers.ElapsedEventHandler(this._timer_Tick);
            timer.Enabled = true;
            timer.Interval = 10000;
            //timer.Start();
            Utilities.GetServiceConstants();
        }
        static string path = AppDomain.CurrentDomain.BaseDirectory + @"\Config.xml";
        static string localFilePath = Path.GetFullPath(Utilities.GetConfigData("Folderpath"));
        static string Uploaded = Path.GetFullPath(Utilities.GetConfigData("Uploaded"));
        static string imagepath = Path.GetFullPath(Utilities.GetConfigData("Imagepath"));
        static string qrpath = Path.GetFullPath(Utilities.GetConfigData("QRpath"));
        private void _timer_Tick(object sender, ElapsedEventArgs e)
        {
            try
            {
                //get all text files in the folder
                string[] filePaths = Directory.GetFiles(localFilePath, "*.txt");
                List<string> value = filePaths.ToList();

                //update one by one
                foreach (var doc in value)
                {
                    ////get only filename                   
                    //var file = invoicelines(doc);
                    string[] lines = System.IO.File.ReadAllLines(doc);
                    foreach (string line in lines)
                    {
                        // Use a tab to indent each line of the file.
                        if (line.Contains("https:"))
                        {
                            Link = line;

                        }
                        else if (line.Contains("TSIN: "))
                        {
                            TSIN = line.Substring(line.IndexOf(':') + 1).TrimEnd();

                        }
                        else if (line.Contains("DATE:"))
                        {
                            Date = line.Substring(line.IndexOf(':') + 1).TrimEnd();

                        }
                        else if (line.Contains("CUSN: "))
                        {
                            CUSN = line.Substring(line.IndexOf(':') + 1).TrimEnd();

                        }
                        else if (line.Contains("CUIN:"))
                        {
                            CUIN = line.Substring(line.IndexOf(':') + 1).TrimEnd();
                        }
                    }
                    var FiscalSeal = invoicelines(Link);
                    var resTSIN = invoicelines(TSIN);
                    var resDate = invoicelines(Date);
                    var resCUSN = invoicelines(CUSN);
                    var resCUIN = invoicelines(CUIN);
                    //get details from invoice

                    QRCodeGenerator qrcode = new QRCodeGenerator();
                    QRCodeData data = qrcode.CreateQrCode(FiscalSeal, QRCodeGenerator.ECCLevel.Q);
                    QRCode qR = new QRCode(data);
                    var valueda = qR.ToString();
                    System.Web.UI.WebControls.Image imgBarCode = new System.Web.UI.WebControls.Image();
                    imgBarCode.Height = 150;
                    imgBarCode.Width = 150;
                    using (Bitmap bitMap = qR.GetGraphic(20))
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            bitMap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                            byte[] byteImage = ms.ToArray();
                            System.Drawing.Image img = System.Drawing.Image.FromStream(ms);
                            img.Save(imagepath + "QRCODE.Jpeg", System.Drawing.Imaging.ImageFormat.Jpeg);
                        }
                    }

                    string[] image = Directory.GetFiles(imagepath, "*.Jpeg");
                    List<string> qr = image.ToList();
                    foreach (var im in qr)
                    {
                        dqbase64 = GetBase64StringForImage(im);
                        File.Copy(qrpath, im);
                        File.Delete(im);
                    }

                    //update to nav
                    dynamic json1 = updateinfor(dqbase64, resTSIN, resDate, resCUSN, resCUIN);
                    //   var status = json1.Status;
                    var Msg = json1.Msg;

                    if (json1.status == "001")
                    {
                        //copy the successful update file and delete
                        File.Copy(Uploaded, doc);
                        File.Delete(doc);

                        //log response
                        Utilities.WriteLog(Msg);
                    }
                    else
                    {
                        Utilities.WriteLog("Already updated");
                    }
                }
            }
            catch (Exception ex)
            {
                Utilities.WriteLog("Error when excuting file ::" + ex.Message);
            }

        }
        public static string invoicelines(string textfile)
        {

            var position = textfile.Replace("|", string.Empty);
            var values = position.Replace(" ", string.Empty);
            return values;
        }
        public static string GetBase64StringForImage(string imgPath)
        {
            byte[] imageBytes = System.IO.File.ReadAllBytes(imgPath);
            string base64String = Convert.ToBase64String(imageBytes);
            return base64String;
        }
        public static string Date { get; private set; }
        public static string CUSN { get; private set; }
        public static string CUIN { get; private set; }
        public static string TSIN { get; private set; }
        public static string Link { get; private set; }
        public string dqbase64 { get; private set; }

        ///function for getting  updating to nav
        public static string updateinfor(string FiscalSeal, string TSIN, string TXDate, string CUSN, string CUIN)
        {
            string itemlist = @"<Envelope xmlns=""http://schemas.xmlsoap.org/soap/envelope/"">
                                    <Body>
                                        <UpdateSalesInvoiceWithTIMSDetails xmlns=""urn:microsoft-dynamics-schemas/codeunit/TIMSIntegration"">
                                            <prFiscalSeal>" + FiscalSeal + @"</prFiscalSeal>
                                            <prTSIN>" + TSIN + @"</prTSIN>
                                            <prTXDate>" + TXDate + @"</prTXDate>
                                            <prCUSN>" + CUSN + @"</prCUSN>
                                            <prCUIN>" + CUIN + @"</prCUIN>
                                        </UpdateSalesInvoiceWithTIMSDetails>
                                    </Body>
                                </Envelope>";
            string response = Utilities.CallWebService(itemlist);
            return Utilities.GetJSONResponse(response);
        }
        protected override void OnStop()
        {
            Utilities.WriteLog("Service Stopped.");
        }
    }
}
