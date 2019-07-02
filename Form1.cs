using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AForge.Video.DirectShow;
using Newtonsoft.Json;

namespace finish_design
{
    public partial class Form1 : Form
    {
        FilterInfoCollection videoDevices;
        Timer timer;
        public Form1()
        {
            InitializeComponent();
        }

        private void 调试ToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            button2.Enabled = false;
            videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            int i = 0;
            foreach (FilterInfo device in videoDevices)
            {
                comboBox1.Items.Add(videoDevices[i].Name.ToString());
                i++;
            }

            if (comboBox1.Items.Count > 0)
            {
                comboBox1.SelectedIndex = 0;
                button1.Enabled = true;
            }
            else
            {
                button1.Enabled = false;
            }

            timer = new Timer();
            timer.Interval = 1000;
            timer.Tick += Timer_Tick;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            Bitmap bmpVideo = videoSourcePlayer1.GetCurrentVideoFrame();

            if (bmpVideo == null)
            {
                return;
            }

            MemoryStream ms = new MemoryStream();
            bmpVideo.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
            // Run inference on the image files
            // For multiple images, session.Run() can be called in a loop (and
            // concurrently). Alternatively, images can be batched since the model
            // accepts batches of image data as input.

            //尝试导入图片
            Image image = Image.FromStream(ms);
            image.Save("tmp.jpg");
            match_img("tmp.jpg");

            // You can get the data in two ways, as a multi-dimensional array, or arrays of arrays, 
            // code can be nicer to read with one or the other, pick it based on how you want to process
            // it
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            if (button1.Text == "开始")
            {
                StartCamera();
                timer.Start();

                button1.Text = "停止";
            }
            else
            {
                timer.Stop();
                StopCamera();

                button1.Text = "开始";
            }
        }

        void StartCamera()
        {
            FilterInfo info;
            info = videoDevices[comboBox1.SelectedIndex];
            VideoCaptureDevice videoSource = new VideoCaptureDevice(info.MonikerString);
            videoSourcePlayer1.VideoSource = videoSource;
            videoSourcePlayer1.Start();
        }

        void StopCamera()
        {
            videoSourcePlayer1.SignalToStop();
            videoSourcePlayer1.WaitForStop();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            timer.Stop();
            StopCamera();
        }
        void match_img(string a)
        {
            byte[] bytes = File.ReadAllBytes(a);
            //二进制转字符串
            string base64String = Convert.ToBase64String(bytes);
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(base64String);

            TestJson(FaceSearch.search(json));
            //Console.WriteLine("Finesh");
        }

        public class FaceSearch
        {
            // 人脸搜索
            public static string search(string a)
            {
                string token = "24.34cbd1e958718f830d041f57e6e13b66.2592000.1563092074.282335-16206358";
                string host = "https://aip.baidubce.com/rest/2.0/face/v3/search?access_token=" + token;
                Encoding encoding = Encoding.Default;
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(host);
                request.ContentType = "application/json";
                request.Method = "post";
                request.KeepAlive = true;
                String str = "{\"image\":" + a + ",\"image_type\":\"BASE64\",\"group_id_list\":\"zeanyon\",\"quality_control\":\"LOW\",\"liveness_control\":\"NONE\"}";
                byte[] buffer = encoding.GetBytes(str);
                request.ContentLength = buffer.Length;
                request.GetRequestStream().Write(buffer, 0, buffer.Length);
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.Default);
                string result = reader.ReadToEnd();
                //Console.WriteLine("人脸搜索:");
                Console.WriteLine(result);
                return result;
            }
        }

        void TestJson(string jsonText)
        {
            //RootObject rb = JsonConvert.DeserializeObject<RootObject>(jsonText);
            try
            {
                //label3.Text = "欢迎" + id + "先生/女士";
                //label3.Text = "欢迎" + rb.user_list[0].user_id + "先生/女士";
                RootObject result = null;
                result = JsonConvert.DeserializeObject<RootObject>(jsonText);
                string temp = "";
                if (result.result.user_list[0].user_id == "zeanyon") temp = "阮桢垚";
                else if (result.result.user_list[0].user_id == "jiapeng") temp = "李嘉鹏";
                else if (result.result.user_list[0].user_id == "xiong") temp = "袁雄";
                else if (result.result.user_list[0].user_id == "baobao") temp = "周宝";
                label2.Text = "欢迎" + temp + "先生/女士";
                button2.Enabled = true;
            }
            catch (Exception)
            {
                label2.Text = "未识别到人";
            }
        }

        public class User_list
        {
            public string group_id { get; set; }
            public string user_id { get; set; }
            public string user_info { get; set; }
            public string score { get; set; }
        }

        public class Result
        {
            public string face_token { get; set; }
            public List<User_list> user_list { get; set; }
        }

        public class RootObject
        {
            public string error_code { get; set; }
            public string error_msg { get; set; }
            public string log_id { get; set; }
            public string timestamp { get; set; }
            public string cached { get; set; }
            public Result result { get; set; }
        }

        private void Button2_Click(object sender, EventArgs e)
        {
            StopCamera();
            var form = new Form2();
            form.ShowDialog();
        }
    }
    
}
