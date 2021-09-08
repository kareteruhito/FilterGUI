using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using OpenCvSharp.WpfExtensions;

using System.ComponentModel;

namespace FilterGUI
{
    class GraphicsModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string name) => PropertyChanged(this, new PropertyChangedEventArgs(name));

        // ぼかし回数
        private int _blurNumberOfTimes = 13;
        public int BlurNumberOfTimes
        {
            get { return _blurNumberOfTimes; }
            set
            {
                if (_blurNumberOfTimes != value)
                {
                    _blurNumberOfTimes = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BlurNumberOfTimes)));
                }
            }
        }

        // ノンローカルミーンフィルタHパラメタ
        private int _nonLocalMeanH = 16;
        public int NonLocalMeanH
        {
            get { return _nonLocalMeanH; }
            set
            {
                if (_nonLocalMeanH != value)
                {
                    _nonLocalMeanH = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NonLocalMeanH)));
                }
            }
        }
        // ラプラシアンフィルタカーネルサイズ
        private int _laplacianKsize = 0;
        public int LaplacianKsize
        {
            get { return _laplacianKsize; }
            set
            {
                if (_laplacianKsize != value)
                {
                    _laplacianKsize = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LaplacianKsize)));
                }
            }
        }
        // アンシャープマスキングKパラメタ
        private int _unsharpMaskingK = 15;
        public int UnsharpMaskingK
        {
            get { return _unsharpMaskingK; }
            set
            {
                if (_unsharpMaskingK != value)
                {
                    _unsharpMaskingK = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UnsharpMaskingK)));
                }
            }
        }

        // ガンマ補正値
        private int _gammaVol = 0;
        public int GammaVol
        {
            get { return _gammaVol; }
            set
            {
                if (_gammaVol != value)
                {
                    _gammaVol = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GammaVol)));
                }
            }
        }
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public GraphicsModel()
        {
            PropertyChanged += (o, e) => {};
        }
        /// <summary>
        /// 設定ファイルのパスを取得
        /// </summary>
        /// <returns>設定ファイルのパス</returns>
        private string getSettingJsonPath()
        {
            var path = System.Reflection.Assembly.GetEntryAssembly().Location;
            var dir = Path.GetDirectoryName(path);
            return Path.Combine(dir, "setting.json");
        }
        /// <summary>
        /// 設定ファイルの保存
        /// </summary>
        /// <param name="path">保存先のパス</param>
        public void Save(string path="")
        {
            path = (path == "") ? getSettingJsonPath() : path;

            var jsonStr = System.Text.Json.JsonSerializer.Serialize(this);

            var encoding = System.Text.Encoding.GetEncoding("utf-8");
            using var writer = new StreamWriter(path, false, encoding);
            writer.WriteLine(jsonStr);
        }

        /// <summary>
        /// 設定ファイルの読み込み
        /// </summary>
        /// <param name="path">読み込み元のパス</param>
        /// <returns>成否フラグ</returns>
        public bool Load(string path="")
        {
            path = (path == "") ? getSettingJsonPath() : path;
            if (File.Exists(path) == false) return false;

            string jsonStr = "";
            var encoding = System.Text.Encoding.GetEncoding("utf-8");
            using(var reader = new StreamReader(path, encoding))
            {
                jsonStr = reader.ReadToEnd();
            }

            var loadObj = System.Text.Json.JsonSerializer.Deserialize<GraphicsModel>(jsonStr);

            var type = loadObj.GetType();

            foreach(var e in type.GetProperties())
            {
                var property = type.GetProperty(e.Name);
                var v = property.GetValue(loadObj);
                property.SetValue(this, v);
            }

            return true;
        }

        static public BitmapSource LoadBitmapSource(string file)
        {
            Mat mat = new(file);

            var fb = BitmapSourceConverter.ToBitmapSource(mat);
            fb.Freeze();
            return fb;
        }
        static public bool SaveBitmapSource(BitmapSource bi, string filename)
        {
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            dir = Path.Combine(dir, DateTime.Now.ToString("yyyyMMdd"));
            if (Directory.Exists(dir) == false) Directory.CreateDirectory(dir);

            var f = Path.GetFileNameWithoutExtension(filename);
            var path = Path.Combine(dir, (f + ".png"));
            

            using (var stream = new FileStream(path, FileMode.Create))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bi));
                encoder.Save(stream);
            }

            return File.Exists(path);
        }

        static private void OrignalBlur(ref Mat mat, int BlurNumberOfTimes=12)
        {
            double[,] kernel = {
                    {0.0/16.0, 1.0/16.0,0.0/16.0},
                    {1.0/16.0,12.0/16.0,1.0/16.0},
                    {0.0/16.0, 1.0/16.0,0.0/16.0},
            };
            
            for(int x=0; x<BlurNumberOfTimes; x++)
            {
                Cv2.Filter2D(mat, mat, -1, InputArray.Create(kernel));
            }
        }
        static private void NonLocalMeans(ref Mat mat, int NonLocalMeanH=16)
        {
            if (NonLocalMeanH == 0) return;
            Cv2.FastNlMeansDenoising(mat, mat, (float)NonLocalMeanH);
        }
        static private Mat Laplacian(ref Mat src, int LaplacianKsize)
        {
            if (LaplacianKsize == 0) return null;
            if (LaplacianKsize % 2 != 1) return null;

            Mat dst = src.Clone();
            //Cv2.Laplacian(src, dst, MatType.CV_8UC1, LaplacianKsize);
            Cv2.Laplacian(src, dst, MatType.CV_64F, LaplacianKsize);
            return dst;
        }
        static private void UnSharpMasking(ref Mat mat, int UnsharpMaskingK=45)
        {
            if (UnsharpMaskingK == 0) return;

            double k = (double)UnsharpMaskingK / 10.0;
            double[,] unsharpKernel = { { -k/9.0,        -k/9.0, -k/9.0},
                                        { -k/9.0, 1.0+8.0*k/9.0, -k/9.0},
                                        { -k/9.0,        -k/9.0, -k/9.0},
            };
            Cv2.Filter2D(mat, mat, -1, InputArray.Create(unsharpKernel));
        }

        // ガンマ補正
        static private void GammaCorrection(ref Mat src, int GammaVal=10)
        {
            var x = new int[256];
            for(var i = 0; i < x.Length; i++) x[i] = i;

            double gamma = GammaVal < 0 ? ( 1.0 / (((double)GammaVal/10.0) * -1.0) ) : ((double)GammaVal/10.0);

            var y = new int[256];
            for(var j = 0; j < y.Length; j++)
            {
                y[j] = (int)(Math.Pow( (double)x[j] / 255.0, gamma ) * 255.0);
                //Debug.Print("y[{0}]:{1} gamma:{2} Pow:{3} {4}", j, y[j], gamma, Math.Pow( x[j] / 255, gamma ), (double)x[j] / 255.0);
            }

            var dst = new Mat();
            Cv2.LUT(src, InputArray.Create(y), dst);

            dst.ConvertTo(src, MatType.CV_8UC1);
            
        }
        /// <summary>
        /// 画像フィルターを実行
        /// </summary>
        /// <param name="src">対象画像</param>
        /// <returns>結果画像</returns>
        public BitmapSource OpenCVFilter(BitmapSource src)
        {
            var mat = BitmapSourceConverter.ToMat(src);
            Cv2.CvtColor(mat, mat, ColorConversionCodes.BGRA2GRAY);

            // ぼかし処理
            if (BlurNumberOfTimes > 0)
            {
                OrignalBlur(ref mat, BlurNumberOfTimes);
            }

            // ノンローカルミーンフィルタ
            if (NonLocalMeanH > 0)
                NonLocalMeans(ref mat, NonLocalMeanH);

            // ラプラシアンフィルタ
            if (LaplacianKsize % 2 == 1)
            {
                var edge = Laplacian(ref mat, LaplacianKsize);
                if (edge != null)
                {
                    edge.ConvertTo(edge, MatType.CV_8UC1);
                    // 減算
                    Cv2.Subtract(mat, edge, mat);
                    edge.Dispose();
                }
            }

            // アンシャープマスキングフィルタ
            if (UnsharpMaskingK > 0)
                UnSharpMasking(ref mat, UnsharpMaskingK);

            // ガンマ補正
            if (GammaVol > 10 || GammaVol < -10)
                GammaCorrection(ref mat, GammaVol);

            var dst = BitmapSourceConverter.ToBitmapSource(mat);
            dst.Freeze();

            if (mat != null) mat.Dispose();

            return dst;
        }

        static public BitmapSource ConvertRGBA(BitmapSource src)
        {
            using var mat = BitmapSourceConverter.ToMat(src);

            Cv2.CvtColor(mat, mat, ColorConversionCodes.GRAY2RGBA);

            Debug.Print("チャンネル数:{0}", mat.Channels());

            for (int y = 0; y < mat.Height; y++)
            {
                for (int x = 0; x < mat.Width; x++)
                {
                    Vec4b pic = mat.At<Vec4b>(y, x);

                    pic[3] = (byte)(Convert.ToByte("255") - pic[0]);   // A

                    mat.Set(y, x, pic);
                }
            }

            return BitmapSourceConverter.ToBitmapSource(mat);
        }
    }
}