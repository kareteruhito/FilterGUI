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
        private int _blurNumberOfTimes = 1;
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

        // ガウシアンフィルタぼかし回数
        private int _gBlurNumberOfTimes = 0;
        public int GBlurNumberOfTimes
        {
            get { return _gBlurNumberOfTimes; }
            set
            {
                if (_gBlurNumberOfTimes != value)
                {
                    _gBlurNumberOfTimes = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GBlurNumberOfTimes)));
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
        static private void CrossAvgBlur(ref Mat mat, int BlurNumberOfTimes=1)
        {
            double[,] kernel = {
                    {0.0/5.0, 1.0/5.0,0.0/5.0},
                    {1.0/5.0, 1.0/5.0,1.0/5.0},
                    {0.0/5.0, 1.0/5.0,0.0/5.0},
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

            double[,] kernel2 = { { -1.0*k/16.0,        -2.0*k/16.0, -1.0*k/16.0},
                                 {  -2.0*k/16.0,    1.0+12.0*k/16.0, -2.0*k/16.0},
                                 {  -1.0*k/16.0,        -2.0*k/16.0, -1.0*k/16.0},
            };

            Cv2.Filter2D(mat, mat, -1, InputArray.Create(kernel));
        }

        // ノンローカルミーンフィルタHパラメタ
        private float _nonLocalMeanH = 12.0f;
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

        // メディアンフィルター
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

        // ノンローカルミーンフィルタH2パラメタ
        private float _nonLocalMeanH2 = 6.0f;
        public float NonLocalMeanH2
        {
            get { return _nonLocalMeanH2; }
            set
            {
                if (_nonLocalMeanH2 != value)
                {
                    _nonLocalMeanH2 = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NonLocalMeanH2)));
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

            // メディアンフィルター
            if (MedianKsize > 0)
            {
                if (MedianKsize % 2 == 0)
                {
                    MedianKsize = MedianKsize + 1;
                }
                Cv2.MedianBlur(mat, mat, MedianKsize);
            }

            // ぼかし処理
            if (BlurNumberOfTimes > 0)
            {
                CrossAvgBlur(ref mat, BlurNumberOfTimes);
            }
            // ガウシアンフィルタ
            if (GBlurNumberOfTimes > 0)
            {
                for(var i = 0; i < GBlurNumberOfTimes; i++)
                {
                    Cv2.GaussianBlur(mat, mat, new Size(3, 3), 0.0d);
                }
            }

            // ノンローカルミーンフィルタ
            if (NonLocalMeanH > 0f)
            {
                Cv2.FastNlMeansDenoising(mat, mat, NonLocalMeanH);
            }

            // アンシャープマスキングフィルタ
            if (UnsharpMaskingK > 0)
                UnSharpMasking(ref mat, UnsharpMaskingK);
            
            // ノンローカルミーンフィルタ
            if (NonLocalMeanH2 > 0f)
            {
                Cv2.FastNlMeansDenoising(mat, mat, NonLocalMeanH2);
            }

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