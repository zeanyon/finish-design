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
using TensorFlow;
using ExampleCommon;

namespace finish_design
{
    public partial class Form2 : Form
    {
        FilterInfoCollection videoDevices;
        Timer timer;

        TFGraph graph;
        byte[] model;
        string[] labels;
        TFSession session;
        private IEnumerable<CatalogItem> _catalog;

        public Form2()
        {
            InitializeComponent();
        }

        private void Form2_Load(object sender, EventArgs e)
        {
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

            // Construct an in-memory graph from the serialized form.
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
            image.Save("image_temp.jpg");
            //match_img("tmp.jpg");

            //var tensor = CreateTensorFromImage(ms.GetBuffer());
            var tensor = ImageUtil.CreateTensorFromImageFile("image_temp.jpg", TFDataType.UInt8);
            //下面这行有问题啊 session is null，现在修复好了
            var graph = new TFGraph();
            var model = File.ReadAllBytes("frozen_inference_graph.pb");
            var labels = File.ReadAllLines("object-detection.pbtxt");
            graph.Import(new TFBuffer(model));

            var session = new TFSession(graph);
            var runner = session.GetRunner();
            runner.AddInput(graph["image_tensor"][0], tensor).Fetch(graph["detection_boxes"][0], graph["detection_scores"][0], graph["detection_classes"][0], graph["num_detections"][0]);
            var output = runner.Run();
            var boxes = (float[,,])output[0].GetValue(jagged: false);
            var scores = (float[,])output[1].GetValue(jagged: false);
            var classes = (float[,])output[2].GetValue(jagged: false);
            var num = (float[])output[3].GetValue(jagged: false);
            //直接核销
            if (scores[0, 0] > 0.9)
            {
                shangpin(classes[0, 0]);
                jiage(classes[0, 0]);
            }
            //MIN_SCORE_FOR_OBJECT_HIGHLIGHTING = 0.6，下面这行是用来画框的！;
            //DrawBoxes(boxes, scores, classes, "image_temp.jpg", "image_temp1.jpg", 0.9);
            //pictureBox1.Load("image_temp1.jpg");
        }

        static TFTensor CreateTensorFromImage(byte[] contents)
        {
            // DecodeJpeg uses a scalar String-valued tensor as input.
            var tensor = TFTensor.CreateString(contents);

            TFGraph graph;
            TFOutput input, output;

            // Construct a graph to normalize the image
            ConstructGraphToNormalizeImage(out graph, out input, out output);

            // Execute that graph to normalize this one image
            using (var session = new TFSession(graph))
            {
                var normalized = session.Run(
                         inputs: new[] { input },
                         inputValues: new[] { tensor },
                         outputs: new[] { output });

                return normalized[0];
            }
        }
        static void ConstructGraphToNormalizeImage(out TFGraph graph, out TFOutput input, out TFOutput output)
        {
            // Some constants specific to the pre-trained model at:
            // https://storage.googleapis.com/download.tensorflow.org/models/inception5h.zip
            //
            // - The model was trained after with images scaled to 224x224 pixels.
            // - The colors, represented as R, G, B in 1-byte each were converted to
            //   float using (value - Mean)/Scale.

            const int W = 224;
            const int H = 224;
            const float Mean = 117;
            const float Scale = 1;

            graph = new TFGraph();
            input = graph.Placeholder(TFDataType.String);

            output = graph.Div(
                x: graph.Sub(
                    x: graph.ResizeBilinear(
                        images: graph.ExpandDims(
                            input: graph.Cast(
                                graph.DecodeJpeg(contents: input, channels: 3), DstT: TFDataType.Float),
                            dim: graph.Const(0, "make_batch")),
                        size: graph.Const(new int[] { W, H }, "size")),
                    y: graph.Const(Mean, "mean")),
                y: graph.Const(Scale, "scale"));
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

        void DrawBoxes(float[,,] boxes, float[,] scores, float[,] classes, string inputFile, string outputFile, double minScore)
        {
            _catalog = CatalogUtil.ReadCatalogItems("object-detection.pbtxt");
            var x = boxes.GetLength(0);
            var y = boxes.GetLength(1);
            var z = boxes.GetLength(2);

            float ymin = 0, xmin = 0, ymax = 0, xmax = 0;

            using (var editor = new ImageEditor(inputFile, outputFile))
            {
                for (int i = 0; i < x; i++)
                {
                    for (int j = 0; j < y; j++)
                    {
                        if (scores[i, j] < minScore) continue;

                        for (int k = 0; k < z; k++)
                        {
                            var box = boxes[i, j, k];
                            switch (k)
                            {
                                case 0:
                                    ymin = box;
                                    break;
                                case 1:
                                    xmin = box;
                                    break;
                                case 2:
                                    ymax = box;
                                    break;
                                case 3:
                                    xmax = box;
                                    break;
                            }

                        }

                        int value = Convert.ToInt32(classes[i, j]);
                        CatalogItem catalogItem = _catalog.FirstOrDefault(item => item.Id == value);
                        editor.AddBox(xmin, xmax, ymin, ymax, $"{catalogItem.DisplayName} : {(scores[i, j] * 100).ToString("0")}%");
                    }
                }
            }
        }

        private void Button2_Click(object sender, EventArgs e)
        {

        }

        void shangpin(float a)
        {
            switch (a)
            {
                case 1:label3.Text= "唇动柠檬蛋糕"; break;
                case 2: label3.Text = "好丽友蛋糕"; break;
                case 3: label3.Text = "浪味仙叉烧味"; break;
                case 4: label3.Text = "浪味仙田园味"; break;
                case 5: label3.Text = "无糖薄荷塘柠檬味"; break;
                case 6: label3.Text = "无糖薄荷塘黑加仑"; break;
                case 7: label3.Text = "银鹭八宝粥"; break;
                case 8: label3.Text = "哇哈哈八宝粥"; break;
                case 9: label3.Text = "百事可乐"; break;
                case 10: label3.Text = "百事可乐无糖"; break;
            }
        }
        void jiage(float b)
        {
            switch (b)
            {
                case 1: label5.Text = "4元"; break;
                case 2: label5.Text = "4元"; break;
                case 3: label5.Text = "5.8元"; break;
                case 4: label5.Text = "5.8元"; break;
                case 5: label5.Text = "9元"; break;
                case 6: label5.Text = "9元"; break;
                case 7: label5.Text = "3.5元"; break;
                case 8: label5.Text = "3.8元"; break;
                case 9: label5.Text = "3.5元"; break;
                case 10: label5.Text = "3.5元"; break;
            }
        }
    }
}

