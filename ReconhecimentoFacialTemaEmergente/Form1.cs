using Amazon;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ReconhecimentoFacialTemaEmergente
{
    public partial class FacialSelvi : Form
    {
        private CascadeClassifier _rosto;
        private CascadeClassifier _sorriso;
        private Capture _imagemCapturada;
        private float ConfiancaAceitavel = 77f;
        private int QuantidadeResultados = 5;
        static bool GatilhoCapturadaFoto = false;
        static bool GatilhoCapturadaFotoComparacao = false;

        private static BasicAWSCredentials _credentials;
        private static CredentialProfile _profile;

        public FacialSelvi()
        {
            InitializeComponent();
            InicializaTreinos();
            InicializaCredenciaisAws();
        }

        private void InicializaCredenciaisAws()
        {
            var options = new CredentialProfileOptions
            {
                AccessKey = "CHAVE_AWS",
                SecretKey = "KEY_AWS"
            };

            _profile = new Amazon.Runtime.CredentialManagement.CredentialProfile("basic_profile", options);
            _profile.Region = RegionEndpoint.USEast1;
            var netSDKFile = new NetSDKCredentialsFile();
            netSDKFile.RegisterProfile(_profile);
            _credentials = new BasicAWSCredentials(options.AccessKey, options.SecretKey);
        }

        public void InicializaTreinos()
        {
            _rosto = new CascadeClassifier(Application.StartupPath + "//treinos//haarcascade_frontalface_default.xml");
            _sorriso = new CascadeClassifier(Application.StartupPath + "//treinos//haarcascade_smile.xml");
        }

        private void btnFechar_Click(object sender, EventArgs e)
        {
            System.Environment.Exit(1);
        }

        public string RetornaNomeImagem()
        {
            string dataHoraMinuto = DateTime.Now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
            return "face" + dataHoraMinuto + ".png";
        }

        public List<string> CompararDuasImagens(Bitmap imagemCapturada)
        {
            List<string> mensagensExibir = new List<string>();
            try
            {
                string caminhoImagem = System.IO.Directory.GetCurrentDirectory() + "\\Imagens\\" + RetornaNomeImagem();
                imagemCapturada.Save(caminhoImagem, ImageFormat.Png);
                AmazonRekognitionClient rekognitionClient = new AmazonRekognitionClient(_credentials, _profile.Region);
                Amazon.Rekognition.Model.Image imageSource = new Amazon.Rekognition.Model.Image();

                using (FileStream fs = new FileStream(System.IO.Directory.GetCurrentDirectory() + "\\Gabarito\\gabarito.png", FileMode.Open, FileAccess.Read))
                {
                    byte[] data = new byte[fs.Length];
                    fs.Read(data, 0, (int)fs.Length);
                    imageSource.Bytes = new MemoryStream(data);
                }

                Amazon.Rekognition.Model.Image imagem = new Amazon.Rekognition.Model.Image();
                using (FileStream fs = new FileStream(caminhoImagem, FileMode.Open, FileAccess.Read))
                {
                    byte[] data = new byte[fs.Length];
                    data = new byte[fs.Length];
                    fs.Read(data, 0, (int)fs.Length);
                    imagem.Bytes = new MemoryStream(data);
                }

                CompareFacesRequest compareFacesRequest = new CompareFacesRequest()
                {
                    SourceImage = imageSource,
                    TargetImage = imagem,
                    SimilarityThreshold = ConfiancaAceitavel
                };

                CompareFacesResponse compareFacesResponse = rekognitionClient.CompareFaces(compareFacesRequest);
                float posicao = 10;
                foreach (CompareFacesMatch match in compareFacesResponse.FaceMatches)
                {
                    ComparedFace face = match.Face;
                    BoundingBox position = face.BoundingBox;
                    var msg = "Detectada face. Sucesso com " + match.Similarity + "% confiança.";
                    mensagensExibir.Add(msg);
                    EscreveImagem(caminhoImagem, msg, posicao);
                    posicao += 30;
                }

                if (!mensagensExibir.Any())
                    mensagensExibir.Add("Face não reconhecida");

                return mensagensExibir;
            }
            catch (Exception erro)
            {
                mensagensExibir.Add("Erro: " + erro.Message);
                return mensagensExibir;
            }
        }

        public void EscreveImagem(string caminhoImagem, string mensagem, float posicao)
        {
            Bitmap novaImagem;
            using (Bitmap bitmap = (Bitmap)System.Drawing.Image.FromFile(caminhoImagem))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    using (Font arialFont = new Font("Arial", 12))
                    {
                        graphics.DrawString(mensagem, arialFont, Brushes.Yellow, new PointF(10f, posicao));
                    }
                }

                novaImagem = new Bitmap(bitmap);
            }
            novaImagem.Save(caminhoImagem);
            novaImagem.Dispose();
        }

        public List<string> ReconhecerImagemCapturada(Bitmap imagemCapturada)
        {
            List<string> mensagensExibir = new List<string>();
            try
            {
                string caminhoImagem = System.IO.Directory.GetCurrentDirectory() + "\\Imagens\\" + RetornaNomeImagem();
                imagemCapturada.Save(caminhoImagem, ImageFormat.Png);
                Amazon.Rekognition.Model.Image imagem = new Amazon.Rekognition.Model.Image();
                using (FileStream fs = new FileStream(caminhoImagem, FileMode.Open, FileAccess.Read))
                {
                    byte[] data = null;
                    data = new byte[fs.Length];
                    fs.Read(data, 0, (int)fs.Length);
                    imagem.Bytes = new MemoryStream(data);
                }

                AmazonRekognitionClient rekognitionClient = new AmazonRekognitionClient(_credentials, _profile.Region);
                DetectLabelsRequest detectlabelsRequest = new DetectLabelsRequest()
                {
                    Image = imagem,
                    MaxLabels = QuantidadeResultados,
                    MinConfidence = ConfiancaAceitavel
                };

                DetectLabelsResponse detectLabelsResponse = rekognitionClient.DetectLabels(detectlabelsRequest);
                foreach (Amazon.Rekognition.Model.Label label in detectLabelsResponse.Labels)
                {
                    mensagensExibir.Add("Parametro: " + label.Name + "  Grau de confiança: " + label.Confidence);
                }

                float posicao = 10f;
                foreach (var mensagem in mensagensExibir)
                {
                    EscreveImagem(caminhoImagem, mensagem, posicao);
                    posicao += 30;
                }
            }
            catch (Exception erro)
            {
                mensagensExibir.Add("Erro: " + erro.Message);
                return mensagensExibir;
            }
            return mensagensExibir;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                if (_imagemCapturada == null)
                    _imagemCapturada = new Capture(1);

                Rectangle gabarito = new Rectangle(0, 0, pictureBoxCamera.Width / 2, pictureBoxCamera.Height / 2);
                Image<Bgr, byte> img = _imagemCapturada.QueryFrame();
                using (Image<Bgr, byte> proximoFrame = img)
                {
                    if (proximoFrame != null)
                    {
                        using (proximoFrame)
                        {
                            Image<Gray, byte> frameEstabilizado = proximoFrame.Convert<Gray, byte>();
                            frameEstabilizado._EqualizeHist();
                            List<Rectangle> faces = new List<Rectangle>();
                            List<Rectangle> sorrisosRec = new List<Rectangle>();
                            using (Image<Gray, byte> frameFinal = frameEstabilizado.Convert<Gray, byte>())
                            {
                                if (_rosto != null)
                                {
                                    Graphics g = Graphics.FromImage(proximoFrame.Bitmap);
                                    Pen pen2 = new Pen(Color.Transparent, 10);
                                    g.DrawRectangle(pen2, gabarito);
                                    Rectangle[] facesDetected = _rosto.DetectMultiScale(frameFinal, 1.2, 5, new Size(20, 20), Size.Empty);
                                    faces.AddRange(facesDetected);
                                    foreach (Rectangle f in facesDetected)
                                    {
                                        frameFinal.ROI = f;
                                        Rectangle[] sorrisos = _sorriso.DetectMultiScale(frameFinal, 4, 20, new Size(20, 20), Size.Empty);
                                        frameFinal.ROI = Rectangle.Empty;
                                        foreach (Rectangle sorriso in sorrisos)
                                        {
                                            Rectangle sorrisoEc = sorriso;
                                            sorrisoEc.Offset(f.X, f.Y);
                                            sorrisosRec.Add(sorrisoEc);
                                        }
                                    }

                                    foreach (Rectangle face1 in faces)
                                        if (faces.Count() > 1)
                                        {
                                            face1.Inflate(10, 10);
                                            proximoFrame.Draw(face1, new Bgr(Color.Red), 2);
                                            face1.Inflate(20, 20);

                                        }
                                        else
                                            proximoFrame.Draw(face1, new Bgr(Color.Green), 2);

                                    if (sorrisosRec.Any())
                                    {
                                        var mensagens = ReconhecerImagemCapturada(proximoFrame.ToBitmap());
                                        foreach (var item in mensagens)
                                        {
                                            EscreveLog(item);
                                        }
                                    }
                                    else if (GatilhoCapturadaFoto)
                                    {
                                        GatilhoCapturadaFoto = false;
                                        var mensagens = ReconhecerImagemCapturada(proximoFrame.ToBitmap());
                                        foreach (var item in mensagens)
                                        {
                                            EscreveLog(item);
                                        }
                                    }
                                    else if (GatilhoCapturadaFotoComparacao)
                                    {
                                        GatilhoCapturadaFotoComparacao = false;
                                        var mensagens = CompararDuasImagens(proximoFrame.ToBitmap());
                                        foreach (var item in mensagens)
                                        {
                                            EscreveLog(item);
                                        }
                                    }

                                    DesenhaTela(proximoFrame.ToBitmap());
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                EscreveLog("Erro: " + ex.Message);
            }
        }

        private void DesenhaTela(Bitmap bitmap)
        {
            pictureBoxCamera.Image = bitmap;
        }

        private void EscreveLog(string log)
        {
            txtLog.Text += log + " -> " + "\r\n";
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.ScrollToCaret();
        }

        private void btnLigaDesligaCam_Click(object sender, EventArgs e)
        {
            GatilhoCapturadaFoto = true;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            GatilhoCapturadaFotoComparacao = true;
        }
    }
}
