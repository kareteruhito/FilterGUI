using System.Reflection.Metadata;
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
        private double _unsharpMaskingK = 1.5d;
        public double UnsharpMaskingK
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
        /// <summary>
        /// オリジナルぼかし
        /// </summary>
        /// <param name="mat">対象画像</param>
        /// <param name="BlurNumberOfTimes">フィルター回数</param>
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
        /// <summary>
        /// アンシャープマスキングフィルタ
        /// </summary>
        /// <param name="mat">対象画像</param>
        /// <param name="k">フィルタ強度</param>
        static private void UnSharpMasking(ref Mat mat, double k=0d)
        {
            double[,] kernel = { { -k/9.0,        -k/9.0, -k/9.0},
                                 { -k/9.0, 1.0+8.0*k/9.0, -k/9.0},
                                 { -k/9.0,        -k/9.0, -k/9.0},
            };
            Cv2.Filter2D(mat, mat, -1, InputArray.Create(kernel));
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
            }

            var dst = new Mat();
            Cv2.LUT(src, InputArray.Create(y), dst);

            dst.ConvertTo(src, MatType.CV_8UC1);
            
        }
        // バイラテラルフィルタ実行回数
        private int _bilateralFilterN = 0;
        public int BilateralFilterN
        {
            get { return _bilateralFilterN; }
            set
            {
                if (_bilateralFilterN != value)
                {
                    _bilateralFilterN = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BilateralFilterN)));
                }
            }
        }
        // バイラテラルフィルタDパラメタ
        private int _bilateralFilterD = 3;
        public int BilateralFilterD
        {
            get { return _bilateralFilterD; }
            set
            {
                if (_bilateralFilterD != value)
                {
                    _bilateralFilterD = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BilateralFilterD)));
                }
            }
        }
        // バイラテラルフィルタ色パラメタ
        private int _bilateralFilterColor = 20;
        public int BilateralFilterColor
        {
            get { return _bilateralFilterColor; }
            set
            {
                if (_bilateralFilterColor != value)
                {
                    _bilateralFilterColor = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BilateralFilterColor)));
                }
            }
        }
        // バイラテラルフィルタ距離パラメタ
        private int _bilateralFilterSpace = 20;
        public int BilateralFilterSpace
        {
            get { return _bilateralFilterSpace; }
            set
            {
                if (_bilateralFilterSpace != value)
                {
                    _bilateralFilterSpace = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BilateralFilterSpace)));
                }
            }
        }
        // ノンローカルミーンフィルタHパラメタ
        private float _nonLocalMeanH = 3.0f;
        public float NonLocalMeanH
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
        private int _nonLocalMeanTempateWindowSize = 7;
        public int NonLocalMeanTempateWindowSize
        {
            get { return _nonLocalMeanTempateWindowSize; }
            set
            {
                if (_nonLocalMeanTempateWindowSize != value)
                {
                    _nonLocalMeanTempateWindowSize = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NonLocalMeanTempateWindowSize)));
                }
            }
        }
        // ノンローカルミーンフィルタサーチウィンドウサイズ
        private int _nonLocalMeanSearchWindowSize = 21;
        public int NonLocalMeanSearchWindowSize
        {
            get { return _nonLocalMeanSearchWindowSize; }
            set
            {
                if (_nonLocalMeanSearchWindowSize != value)
                {
                    _nonLocalMeanSearchWindowSize = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NonLocalMeanSearchWindowSize)));
                }
            }
        }
        /// <summary>
        /// バイラテラルフィルタ
        /// </summary>
        /// <param name="mat">対象画像</param>
        /// <param name="n">フィルタ実行回数</param>
        /// <param name="d">ぼかす領域の広さ</param>
        /// <param name="sigmaColor">色。20以下...小、150以上...大</param>
        /// <param name="sigmaSpace">距離。20以下...小、150以上...大</param>
        static private void BilateralFilter(ref Mat mat, int n, int d, double sigmaColor, double sigmaSpace)
        {
            for(var i=0; i < n; i++)
            {
                using Mat tmp = mat.Clone();
                Cv2.BilateralFilter(tmp, mat, d, sigmaColor, sigmaSpace);
            }
        }
        // メディアンフィルタカーネルサイズ
        private int _medianKsize = 0;
        public int MedianKsize
        {
            get { return _medianKsize; }
            set
            {
                if (_medianKsize != value)
                {
                    _medianKsize = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MedianKsize)));
                }
            }
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

            // メディアンフィルター
            if (MedianKsize > 0)
            {
                Cv2.MedianBlur(mat, mat, MedianKsize + (1 - MedianKsize % 2));
            }

            // バイラテラルフィルタ
            if (BilateralFilterN > 0)
                BilateralFilter(ref mat, BilateralFilterN, BilateralFilterD, BilateralFilterColor, BilateralFilterSpace);

            // ノンローカルミーンフィルタ
            if (NonLocalMeanH > 0f)
            {
                Cv2.FastNlMeansDenoising(mat, mat, NonLocalMeanH, NonLocalMeanTempateWindowSize, NonLocalMeanTempateWindowSize);
            }

            // ラプラシアンフィルタ(輪郭強調)
            if (LaplacianKsize > 0)
            {
                using var edge = mat.Clone();
                var ksize = LaplacianKsize + (1 - LaplacianKsize % 2);
                Cv2.Laplacian(mat, edge, MatType.CV_64F, ksize);

                edge.ConvertTo(edge, MatType.CV_8UC1);
                // 減算
                Cv2.Subtract(mat, edge, mat);
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